using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Runtime;
using MasterOfPuppets.LuaScripting.Synchronization;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {
    private const int LuaPeerRefreshDelayTicks = 90;
    private static readonly TimeSpan LuaStartLeadTime = TimeSpan.FromSeconds(1.25);
    private const int ChatSyncedLuaServerLeadSeconds = 4;
    private const int FragmentedChatSyncedLuaServerLeadSeconds = 6;
    private static readonly TimeSpan LuaChatFragmentInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MirrorDynamicRosterPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MirrorChatHeartbeatInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MirrorStopRepeatInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MirrorStopTombstoneLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MirrorStopRelaySlotInterval = TimeSpan.FromMilliseconds(250);
    private readonly object _luaChatSendQueueLock = new();
    private readonly object _mirrorStopTombstoneLock = new();
    private readonly CancellationTokenSource _luaChatSendCancellation = new();
    private Task _luaChatSendTail = Task.CompletedTask;
    private MirrorLocalLaunchState? _mirrorLocalLaunch;
    private MirrorChatLaunchState? _mirrorChatLaunch;
    private readonly Dictionary<string, MirrorStopTombstoneState> _mirrorStopTombstones =
        new(StringComparer.OrdinalIgnoreCase);

    public void StartLuaScript(string scriptName, Dictionary<string, string>? inlineVariables = null) {
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => {
            MirrorRunTargetIdentity? mirrorIdentity = null;
            ulong runTargetObjectId;
            uint runTargetEntityId;
            string runTargetName;
            bool runTargetExplicit;
            if (MirrorRunTargetValidator.RequiresPlayerRunTarget(scriptName)) {
                var decision = CaptureMirrorRunTarget(scriptName);
                if (!decision.Success) {
                    RejectMirrorRunTarget(decision.Error);
                    return;
                }
                mirrorIdentity = decision.Identity;
                runTargetObjectId = decision.Identity.GameObjectId;
                runTargetEntityId = decision.Identity.EntityId;
                runTargetName = decision.Identity.Name;
                runTargetExplicit = decision.Identity.WasExplicitlySelected;
            } else {
                (runTargetObjectId, runTargetEntityId, runTargetName) = CaptureRunAnchor();
                runTargetExplicit = DalamudApi.TargetManager.Target != null;
            }
            RequestCharacterData();
            DalamudApi.Framework.RunOnTick(
                () => BroadcastLuaScript(
                    scriptName,
                    runTargetObjectId,
                    runTargetEntityId,
                    runTargetName,
                    runTargetExplicit,
                    mirrorIdentity,
                    inlineVariables),
                delayTicks: LuaPeerRefreshDelayTicks);
        });
    }

    public void StartLuaScriptDirectLocal(string scriptName, Dictionary<string, string>? inlineVariables = null, string senderName = "") {
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => {
            var script = LuaScriptCatalog.Find(Plugin.Config, scriptName);
            if (script == null) {
                var error = $"The configured Lua script '{scriptName}' was not found.";
                DalamudApi.ChatGui.PrintError($"[MoP] {error}");
                return;
            }
            try {
                script.Validate();
            } catch (Exception ex) {
                DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' is invalid: {ex.Message}");
                return;
            }

            Dictionary<string, string> variables;
            try {
                variables = ResolveLuaVariables(script, inlineVariables);
            } catch (ArgumentException ex) {
                DalamudApi.ChatGui.PrintError($"[MoP] Lua parameters are invalid: {ex.Message}");
                return;
            }

            ulong runTargetObjectId = 0;
            uint runTargetEntityId = 0;
            string runTargetName = string.Empty;
            var hasConfiguredAnchor = variables.TryGetValue("anchor", out var configuredAnchor)
                && !string.IsNullOrWhiteSpace(configuredAnchor);
            if (hasConfiguredAnchor) {
                runTargetName = configuredAnchor.Trim();
                var targetActor = DalamudApi.ObjectTable?.FirstOrDefault(a =>
                    a != null && a.Address != nint.Zero &&
                    (FormationCharacterName.MatchScore(runTargetName, a.GetPlayerNameWorld() ?? a.Name.TextValue) >= 0 ||
                     (uint.TryParse(runTargetName, out var numId) && (a.EntityId == numId || a.GameObjectId == numId))));
                if (targetActor != null) {
                    runTargetObjectId = targetActor.GameObjectId;
                    runTargetEntityId = targetActor.EntityId;
                    runTargetName = targetActor.GetPlayerNameWorld() ?? targetActor.Name.TextValue;
                }
            } else {
                (runTargetObjectId, runTargetEntityId, runTargetName) = CaptureRunAnchor();
            }

            var freshPeers = GetFreshPeerCharacterData();
            IReadOnlyList<ulong> orderedParticipants;
            if (MirrorRunTargetValidator.AppliesTo(script.Name)) {
                orderedParticipants = ResolveActiveMirrorLaunchRoster(
                    freshPeers.Select(peer => peer.ContentId),
                    DalamudApi.PlayerState.ContentId);
            } else if (AllConfiguredRequested(variables)) {
                orderedParticipants = ConfiguredRoster();
            } else if (!string.IsNullOrWhiteSpace(script.ParticipantFormation)) {
                if (!LuaParticipantResolver.TryResolve(
                        script,
                        Plugin.Config.Formations,
                        Plugin.Config.CidsGroups,
                        out orderedParticipants,
                        out var participantError)) {
                    DalamudApi.ChatGui.PrintError($"[MoP] {participantError}");
                    return;
                }
            } else if (variables.ContainsKey("group")) {
                if (!TryResolveParticipantGroup(Plugin.Config.CidsGroups, variables, out var groupName, out orderedParticipants, out var groupError)) {
                    DalamudApi.ChatGui.PrintError($"[MoP] {groupError}");
                    return;
                }
                variables["group"] = groupName;
            } else {
                var participants = freshPeers
                    .Where(peer => peer.ContentId != 0)
                    .Select(peer => peer.ContentId)
                    .ToHashSet();

                var localCid = DalamudApi.PlayerState.ContentId;
                if (localCid != 0)
                    participants.Add(localCid);

                orderedParticipants = participants.OrderBy(cid => cid).ToArray();
            }
            orderedParticipants = CompactVisibleLaunchRoster(
                orderedParticipants,
                VisibleOnlyRequested(variables));

            if (orderedParticipants.Count == 0) {
                var localCid = DalamudApi.PlayerState.ContentId;
                if (localCid != 0)
                    orderedParticipants = [localCid];
            }

            var localVariables = AddLocalRuntimeVariables(variables);
            var seed = unchecked((int)DateTime.UtcNow.Ticks);
            var startUtcTicks = DateTime.UtcNow.Ticks;
            var conductorCid = orderedParticipants.Count > 0 ? orderedParticipants[0] : DalamudApi.PlayerState.ContentId;
            var localPlayerCid = DalamudApi.PlayerState.ContentId;

            var runId = LuaScriptManager.CreateRunId(startUtcTicks, seed, script.Hash);
            var coordination = new LuaRunCoordinationState(
                localPlayerCid,
                orderedParticipants,
                isConductor: localPlayerCid == conductorCid,
                isDistributed: true,
                conductorContentId: conductorCid);
            coordination.ConfigureTransport(
                (key, value, sequence) => BroadcastLocalLuaSharedVariable(runId, key, value, sequence),
                (messageId, topic, payload, schemaVersion, sequence, targetContentId) =>
                    BroadcastLocalLuaParticipantMessage(
                        runId,
                        messageId,
                        topic,
                        payload,
                        schemaVersion,
                        sequence,
                        targetContentId));

            Plugin.LuaScriptManager.StartScript(
                script.Name,
                script.Hash,
                script.Source,
                runTargetObjectId,
                runTargetEntityId,
                runTargetName,
                orderedParticipants,
                startUtcTicks,
                seed,
                localVariables,
                modules: script.Modules,
                requiredResources: script.RequiredResources,
                declaredCapabilities: script.DeclaredCapabilities,
                conductorName: senderName,
                coordination: coordination);
        });
    }

    public void StartChatSyncedLuaScript(string scriptName, Dictionary<string, string>? inlineVariables = null) {
        var script = LuaScriptCatalog.Find(Plugin.Config, scriptName);
        if (script == null) {
            DalamudApi.ChatGui.PrintError($"[MoP] The configured Lua script '{scriptName}' was not found.");
            return;
        }

        try {
            script.Validate();
        } catch (Exception ex) {
            DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' is invalid: {ex.Message}");
            return;
        }

        MirrorRunTargetIdentity? mirrorIdentity = null;
        if (MirrorRunTargetValidator.RequiresPlayerRunTarget(script.Name)) {
            var decision = CaptureMirrorRunTarget(script.Name);
            if (!decision.Success) {
                RejectMirrorRunTarget(decision.Error);
                return;
            }
            mirrorIdentity = decision.Identity;
        }

        var chatPrefix = Plugin.Config.DefaultChatSyncPrefix?.Trim();
        if (string.IsNullOrWhiteSpace(chatPrefix)) {
            DalamudApi.ChatGui.PrintError("[MoP] Configure a default Chat Sync prefix before starting synchronized Lua.");
            return;
        }

        if (script.Name.Contains('"')) {
            DalamudApi.ChatGui.PrintError("[MoP] Synchronized Lua script names cannot contain quotation marks.");
            return;
        }

        Dictionary<string, string> variables;
        try {
            variables = ResolveSharedLuaVariables(script, inlineVariables);
        } catch (ArgumentException ex) {
            DalamudApi.ChatGui.PrintError($"[MoP] Lua parameters are invalid: {ex.Message}");
            return;
        }
        IReadOnlyList<ulong> participantCids;
        if (MirrorRunTargetValidator.AppliesTo(script.Name)) {
            participantCids = ResolveActiveMirrorLaunchRoster(
                GetFreshPeerCharacterData().Select(peer => peer.ContentId),
                DalamudApi.PlayerState.ContentId);
        } else if (AllConfiguredRequested(variables)) {
            participantCids = ConfiguredRoster();
        } else if (!LuaParticipantResolver.TryResolve(
                script,
                Plugin.Config.Formations,
                Plugin.Config.CidsGroups,
                out participantCids,
                out var participantError)) {
            // Compatibility for scripts created before participant formations existed.
            if (!TryResolveParticipantGroup(Plugin.Config.CidsGroups, variables, out _, out participantCids, out _)) {
                DalamudApi.ChatGui.PrintError($"[MoP] {participantError}");
                return;
            }
        }

        var seed = Random.Shared.Next();
        var (selectedTargetObjectId, selectedTargetEntityId, selectedTargetName) =
            mirrorIdentity is { } frozen
                ? (frozen.GameObjectId, frozen.EntityId, frozen.Name)
                : CaptureRunAnchor();
        var selectedTargetContentId = mirrorIdentity?.ContentId ?? 0;
        var hasConfiguredAnchor = variables.TryGetValue("anchor", out var configuredAnchor)
            && !string.IsNullOrWhiteSpace(configuredAnchor);
        var hasSelectedTarget = mirrorIdentity?.WasExplicitlySelected
            ?? DalamudApi.TargetManager.Target != null;
        var runTargetName = hasConfiguredAnchor
                ? configuredAnchor.Trim()
                : selectedTargetName;
        var senderName = MacroRuntimeVariables.FromCurrentGameState().Me;
        runTargetName = ResolveChatSyncedRunTarget(runTargetName, senderName);
        var targetIdentityMatches = FormationCharacterName.MatchScore(selectedTargetName, runTargetName) >= 0;
        if (!targetIdentityMatches) {
            selectedTargetObjectId = 0;
            selectedTargetEntityId = 0;
            selectedTargetContentId = 0;
        }
        variables["anchor"] = runTargetName;
        variables["mop_run_target_explicit"] = hasConfiguredAnchor || hasSelectedTarget ? "true" : "false";
        if (mirrorIdentity.HasValue) {
            var binding = MirrorRunTargetValidator.ResolveBinding(
                mirrorIdentity.Value,
                hasConfiguredAnchor ? runTargetName : null);
            MirrorRunTargetValidator.WriteMetadata(variables, binding);
        }
        participantCids = CompactVisibleLaunchRoster(
            participantCids,
            VisibleOnlyRequested(variables));
        var envelope = new LuaChatSyncEnvelope {
            MessageId = Guid.NewGuid().ToString("D"),
            CreatedUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Seed = seed,
            ScriptHash = script.Hash,
            DependencyManifestHash = script.DependencyManifestHash,
            BundleHash = script.BundleHash,
            RequiredResources = script.RequiredResources,
            RunTargetName = runTargetName,
            RunTargetEntityId = selectedTargetEntityId,
            Variables = variables,
            ParticipantCids = participantCids.ToList(),
        };
        QueueChatSyncedLuaEnvelope(chatPrefix, script.Name, envelope);
    }

    private void QueueChatSyncedLuaEnvelope(
        string chatPrefix,
        string scriptName,
        LuaChatSyncEnvelope envelope,
        bool preserveStartIdentity = false) {
        lock (_luaChatSendQueueLock) {
            _luaChatSendTail = _luaChatSendTail.ContinueWith(
                    _ => SendChatSyncedLuaEnvelopeAsync(
                        chatPrefix, scriptName, envelope, preserveStartIdentity, _luaChatSendCancellation.Token),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task SendChatSyncedLuaEnvelopeAsync(
        string chatPrefix,
        string scriptName,
        LuaChatSyncEnvelope envelope,
        bool preserveStartIdentity,
        CancellationToken cancellationToken) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (!preserveStartIdentity) {
                envelope.StartUtcTicks = DateTime.UtcNow.Ticks;
                envelope.StartServerTimeSeconds =
                    LuaChoreographyClock.GetServerTimeSeconds() + ChatSyncedLuaServerLeadSeconds;
            }
            var token = EncodeLuaChatSyncEnvelope(envelope);
            IReadOnlyList<string> commands = [
                $"{chatPrefix} mopluarun \"{scriptName}\" {token}",
            ];
            if (Encoding.UTF8.GetByteCount(commands[0]) > LuaChatSyncFragmentCodec.MaximumChatBytes) {
                if (!preserveStartIdentity)
                    envelope.StartServerTimeSeconds =
                        LuaChoreographyClock.GetServerTimeSeconds() + FragmentedChatSyncedLuaServerLeadSeconds;
                token = EncodeLuaChatSyncEnvelope(envelope);
                if (!LuaChatSyncFragmentCodec.TryCreateCommands(
                        chatPrefix,
                        scriptName,
                        envelope.MessageId,
                        token,
                        out commands,
                        out var fragmentError))
                    throw new InvalidOperationException($"Could not fragment synchronized Lua: {fragmentError}.");
            }

            for (var index = 0; index < commands.Count; index++) {
                var command = commands[index];
                await DalamudApi.Framework
                    .RunOnFrameworkThread(() => Chat.SendMessageImmediate(command))
                    .WaitAsync(cancellationToken);
                if (index + 1 < commands.Count)
                    await Task.Delay(LuaChatFragmentInterval, cancellationToken);
            }

            var targetStatus = string.IsNullOrWhiteSpace(envelope.RunTargetName)
                ? string.Empty
                : $" with target {envelope.RunTargetName}";
            var fragmentStatus = commands.Count == 1 ? string.Empty : $" in {commands.Count} fragments";
            if (!preserveStartIdentity) {
                await DalamudApi.Framework.RunOnFrameworkThread(() => DalamudApi.ShowNotification(
                    $"Synchronizing Lua '{scriptName}' across Chat Sync clients{targetStatus}{fragmentStatus}",
                    NotificationType.Success,
                    6000));
            }
            if (!preserveStartIdentity && MirrorRunTargetValidator.AppliesTo(scriptName)) {
                _mirrorChatLaunch = new MirrorChatLaunchState(
                    chatPrefix,
                    scriptName,
                    CloneEnvelope(envelope),
                    DateTime.UtcNow + MirrorChatHeartbeatInterval);
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // Plugin disposal cancels queued chat sends.
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, $"[LuaSync] failed to send synchronized Lua '{scriptName}'");
            if (!cancellationToken.IsCancellationRequested) {
                await DalamudApi.Framework.RunOnFrameworkThread(() =>
                    DalamudApi.ChatGui.PrintError($"[MoP] Could not synchronize Lua '{scriptName}': {ex.Message}"));
            }
        }
    }

    private void DisposeLuaChatSyncSender() => _luaChatSendCancellation.Cancel();

    public async Task<bool> BroadcastChatSyncedMirrorStopAsync(
        string runId,
        string reason,
        int relaySlot,
        CancellationToken cancellationToken) {
        var prefix = Plugin.Config.DefaultChatSyncPrefix?.Trim();
        if (string.IsNullOrWhiteSpace(prefix) || !IsSafeLuaRunId(runId) || relaySlot is < 0 or > 31)
            return false;
        var relayDelay = GetMirrorStopRelayDelay(relaySlot);
        if (relayDelay > TimeSpan.Zero)
            await Task.Delay(relayDelay, cancellationToken);
        await DalamudApi.Framework.RunOnFrameworkThread(() => {
            RecordMirrorStopTombstone(runId);
            StopLuaScript(runId);
            DalamudApi.PluginLog.Information(
                $"[LuaSync] handled terminal Mirror stop run={runId} prefix={prefix} "
                + $"relaySlot={relaySlot} delayMs={relayDelay.TotalMilliseconds:0} reason={reason}");
        }).WaitAsync(cancellationToken);
        return true;
    }

    internal static TimeSpan GetMirrorStopRelayDelay(int relaySlot) =>
        relaySlot is < 0 or > 31
            ? throw new ArgumentOutOfRangeException(nameof(relaySlot))
            : TimeSpan.FromTicks(MirrorStopRelaySlotInterval.Ticks * relaySlot);

    public Task<bool> BroadcastChatSyncedMirrorEmoteResyncAsync(
        string runId,
        uint emoteId,
        bool persistent,
        ulong targetId,
        CancellationToken cancellationToken) {
        // Chat broadcast of mopluaemoteresync is disabled to eliminate CWLS chat spam.
        // Each puppet client observes emote events and target actor state directly.
        return Task.FromResult(true);
    }

    internal static bool TryParseMirrorEmoteResyncArguments(
        IReadOnlyList<string> args,
        out string runId,
        out uint emoteId,
        out bool persistent,
        out ulong targetId,
        out Guid messageId) {
        runId = string.Empty;
        emoteId = 0;
        persistent = false;
        targetId = 0;
        messageId = Guid.Empty;
        if (args.Count != 5
            || !IsSafeLuaRunId(args[0])
            || !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out emoteId)
            || emoteId == 0
            || args[2] is not ("0" or "1")
            || !ulong.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out targetId)
            || !Guid.TryParseExact(args[4], "D", out messageId)
            || messageId == Guid.Empty)
            return false;
        runId = args[0];
        persistent = args[2] == "1";
        return true;
    }

    internal static bool TryParseMirrorStopArguments(IReadOnlyList<string> args, out string runId) {
        runId = string.Empty;
        if (args.Count != 1 || !IsSafeLuaRunId(args[0]))
            return false;
        runId = args[0];
        return true;
    }

    private static bool IsSafeLuaRunId(string? runId) =>
        !string.IsNullOrWhiteSpace(runId)
        && runId.Length is >= 16 and <= 80
        && runId.All(character => char.IsAsciiHexDigit(character) || character == '-');

    public void StartChatSyncedLuaScriptLocal(
        string scriptName,
        LuaChatSyncEnvelope envelope,
        string senderName) {
        var script = LuaScriptCatalog.Find(Plugin.Config, scriptName);
        if (script == null) {
            DalamudApi.PluginLog.Warning(
                $"[Lua] synchronized script '{scriptName}' from {senderName} is not installed locally");
            return;
        }

        try {
            script.Validate();
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, $"[Lua] synchronized script '{scriptName}' is invalid");
            return;
        }

        var bundleHashMatch = envelope.BundleHash.Length == 32
            ? script.BundleHash.StartsWith(envelope.BundleHash, StringComparison.OrdinalIgnoreCase)
            : string.Equals(script.BundleHash, envelope.BundleHash, StringComparison.OrdinalIgnoreCase);
        if (!bundleHashMatch) {
            DalamudApi.PluginLog.Warning(
                $"[Lua] synchronized bundle contract mismatch for '{scriptName}' from {senderName}; " +
                $"expected={envelope.BundleHash} local={script.BundleHash}");
            DalamudApi.ChatGui.PrintError(
                $"[MoP] Lua script '{scriptName}' differs from the conductor's copy "
                + "(source, modules, or capabilities). Save/install the identical script on this client.");
            return;
        }
        envelope.BundleHash = script.BundleHash;

        if (string.IsNullOrEmpty(envelope.ScriptHash)) {
            envelope.ScriptHash = script.Hash;
            envelope.DependencyManifestHash = script.DependencyManifestHash;
        } else {
            if (!string.Equals(script.Hash, envelope.ScriptHash, StringComparison.OrdinalIgnoreCase)) {
                DalamudApi.PluginLog.Warning(
                    $"[Lua] synchronized script hash mismatch for '{scriptName}' from {senderName}; " +
                    $"expected={envelope.ScriptHash} local={script.Hash}");
                DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' differs from the conductor's copy.");
                return;
            }
            if (!string.IsNullOrEmpty(envelope.DependencyManifestHash) &&
                !string.Equals(
                    script.DependencyManifestHash,
                    envelope.DependencyManifestHash,
                    StringComparison.OrdinalIgnoreCase)) {
                DalamudApi.PluginLog.Warning(
                    $"[Lua] synchronized dependency manifest mismatch for '{scriptName}' from {senderName}; " +
                    $"expected={envelope.DependencyManifestHash} local={script.DependencyManifestHash}");
                DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' modules differ from the conductor's copy.");
                return;
            }
        }
        if (script.RequiredResources != envelope.RequiredResources) {
            DalamudApi.PluginLog.Warning(
                $"[Lua] synchronized resource declaration mismatch for '{scriptName}' from {senderName}; " +
                $"expected={envelope.RequiredResources?.ToString() ?? "legacy"} " +
                $"local={script.RequiredResources?.ToString() ?? "legacy"}");
            DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' resource declaration differs from the conductor's copy.");
            return;
        }

        var localCid = DalamudApi.PlayerState.ContentId;
        if (localCid == 0)
            return;

        var isMirrorV2 = MirrorRunTargetValidator.AppliesTo(script.Name);
        if (isMirrorV2) {
            // Chat Sync spans physical PCs, while fresh peer discovery only spans
            // clients attached to this local plugin instance. The sender therefore
            // cannot enumerate every listening band. Merge the transmitted roster
            // with this PC's active peers and always admit the receiving character,
            // matching moprun's all-listeners behavior without changing ordinary
            // formation-scoped Lua launches.
            envelope.ParticipantCids = ResolveReceivedMirrorLaunchRoster(
                    envelope.ParticipantCids,
                    GetFreshPeerCharacterData().Select(peer => peer.ContentId),
                    localCid)
                .ToList();
        } else if (envelope.ParticipantCids.Count > 0) {
            if (!string.IsNullOrWhiteSpace(script.ParticipantFormation)
                && LuaParticipantResolver.TryResolve(
                    script,
                    Plugin.Config.Formations,
                    Plugin.Config.CidsGroups,
                    out var localParticipants,
                    out _)) {
                var allowed = localParticipants.ToHashSet();
                if (envelope.ParticipantCids.Any(cid => !allowed.Contains(cid))) {
                    DalamudApi.ChatGui.PrintError(
                        $"[MoP] Participant Formation '{script.ParticipantFormation}' differs from the conductor's copy.");
                    return;
                }
            }
        } else if (!string.IsNullOrWhiteSpace(script.ParticipantFormation)) {
            if (!LuaParticipantResolver.TryResolve(
                    script,
                    Plugin.Config.Formations,
                    Plugin.Config.CidsGroups,
                    out var localParticipants,
                    out var participantError)) {
                DalamudApi.ChatGui.PrintError($"[MoP] {participantError}");
                return;
            }
            envelope.ParticipantCids = localParticipants.ToList();
        } else if (envelope.ParticipantCids.Count == 0 && envelope.Variables.ContainsKey("group")) {
            if (TryResolveParticipantGroup(Plugin.Config.CidsGroups, envelope.Variables, out _, out var groupParticipants, out _)) {
                envelope.ParticipantCids = groupParticipants.ToList();
            }
        }

        if (!envelope.ParticipantCids.Contains(localCid))
            return;

        var effectiveRunTargetName = ResolveChatSyncedRunTarget(envelope.RunTargetName, senderName);
        var effectiveVariables = AddLocalRuntimeVariables(envelope.Variables);

        if (isMirrorV2 && Plugin.Config.SyncClients) {
            var localBridgeCid = envelope.ParticipantCids[0];
            if (localCid != localBridgeCid) {
                DalamudApi.PluginLog.Information(
                    $"[LuaSync] accepted Mirror launch {envelope.MessageId} from {senderName}; "
                    + $"waiting for local bridge cid={localBridgeCid} localCid={localCid}");
                return;
            }
            DalamudApi.PluginLog.Information(
                $"[LuaSync] accepted Mirror launch {envelope.MessageId} from {senderName}; "
                + $"local bridge cid={localCid} participants={envelope.ParticipantCids.Count} "
                + $"target={effectiveRunTargetName}");
            SendLocalLuaLaunch(
                script,
                0,
                envelope.RunTargetEntityId,
                effectiveRunTargetName,
                envelope.ParticipantCids,
                envelope.StartUtcTicks,
                envelope.Seed,
                effectiveVariables,
                senderName,
                envelope.StartServerTimeSeconds);
            _mirrorLocalLaunch = new MirrorLocalLaunchState(
                script.Clone(),
                0,
                envelope.RunTargetEntityId,
                effectiveRunTargetName,
                envelope.ParticipantCids.ToList(),
                envelope.StartUtcTicks,
                envelope.Seed,
                new Dictionary<string, string>(effectiveVariables, StringComparer.OrdinalIgnoreCase),
                senderName,
                envelope.ParticipantCids.ToHashSet(),
                DateTime.UtcNow + MirrorDynamicRosterPollInterval,
                envelope.StartServerTimeSeconds);
            return;
        }

        DalamudApi.PluginLog.Information(
            $"[LuaSync] accepted synchronized launch {envelope.MessageId} script={script.Name} "
            + $"from={senderName} localCid={localCid} participants={envelope.ParticipantCids.Count}");

        if (Plugin.ChatWatcher.LuaDistributedLaunches.QueueLaunch(
                script,
                envelope,
                senderName,
                effectiveRunTargetName,
                effectiveVariables,
                out var stagingError)) {
            if (!string.IsNullOrWhiteSpace(stagingError)) {
                DalamudApi.PluginLog.Warning($"[LuaSync] staging rejected for '{script.Name}': {stagingError}");
                DalamudApi.ChatGui.PrintError($"[MoP] Lua staging rejected: {stagingError}.");
            }
            return;
        }

        _ = DalamudApi.Framework.RunOnFrameworkThread(() =>
            Plugin.LuaScriptManager.StartScript(
                script.Name,
                script.Hash,
                script.Source,
                0,
                envelope.RunTargetEntityId,
                effectiveRunTargetName,
                envelope.ParticipantCids,
                envelope.StartUtcTicks,
                envelope.Seed,
                effectiveVariables,
                startServerTimeSeconds: envelope.StartServerTimeSeconds,
                modules: script.Modules,
                requiredResources: script.RequiredResources,
                declaredCapabilities: script.DeclaredCapabilities,
                conductorName: senderName));
    }

    private void BroadcastLuaScript(
        string scriptName,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName,
        bool runTargetExplicit,
        MirrorRunTargetIdentity? mirrorIdentity,
        Dictionary<string, string>? inlineVariables) {
        var script = LuaScriptCatalog.Find(Plugin.Config, scriptName);
        if (script == null) {
            var error = $"The configured Lua script '{scriptName}' was not found.";
            DalamudApi.ChatGui.PrintError($"[MoP] {error}");
            DalamudApi.ShowNotification(error, NotificationType.Error, 6000);
            return;
        }
        try {
            script.Validate();
        } catch (Exception ex) {
            DalamudApi.ChatGui.PrintError($"[MoP] Lua script '{scriptName}' is invalid: {ex.Message}");
            return;
        }

        Dictionary<string, string> variables;
        try {
            variables = ResolveLuaVariables(script, inlineVariables);
        } catch (ArgumentException ex) {
            DalamudApi.ChatGui.PrintError($"[MoP] Lua parameters are invalid: {ex.Message}");
            return;
        }
        var hasConfiguredAnchor = variables.TryGetValue("anchor", out var configuredAnchor)
            && !string.IsNullOrWhiteSpace(configuredAnchor);
        if (hasConfiguredAnchor) {
            runTargetName = configuredAnchor.Trim();
            var targetActor = DalamudApi.ObjectTable?.FirstOrDefault(a =>
                a != null && a.Address != nint.Zero &&
                (FormationCharacterName.MatchScore(runTargetName, a.GetPlayerNameWorld() ?? a.Name.TextValue) >= 0 ||
                 (uint.TryParse(runTargetName, out var numId) && (a.EntityId == numId || a.GameObjectId == numId))));
            if (targetActor != null) {
                runTargetObjectId = targetActor.GameObjectId;
                runTargetEntityId = targetActor.EntityId;
                runTargetName = targetActor.GetPlayerNameWorld() ?? targetActor.Name.TextValue;
                mirrorIdentity = new MirrorRunTargetIdentity(
                    runTargetName,
                    runTargetObjectId,
                    runTargetEntityId,
                    ResolveMirrorContentId(targetActor),
                    true,
                    "anchor-override");
            } else if (FormationCharacterName.MatchScore(runTargetName, configuredAnchor) < 0) {
                runTargetObjectId = 0;
                runTargetEntityId = 0;
            }
        }
        variables["mop_run_target_explicit"] = hasConfiguredAnchor || runTargetExplicit ? "true" : "false";
        if (mirrorIdentity.HasValue) {
            var binding = MirrorRunTargetValidator.ResolveBinding(
                mirrorIdentity.Value,
                hasConfiguredAnchor ? runTargetName : null);
            MirrorRunTargetValidator.WriteMetadata(variables, binding);
        }

        var freshPeers = GetFreshPeerCharacterData();
        IReadOnlyList<ulong> orderedParticipants;
        if (MirrorRunTargetValidator.AppliesTo(script.Name)) {
            orderedParticipants = ResolveActiveMirrorLaunchRoster(
                freshPeers.Select(peer => peer.ContentId),
                DalamudApi.PlayerState.ContentId);
        } else if (AllConfiguredRequested(variables)) {
            orderedParticipants = ConfiguredRoster();
        } else if (!string.IsNullOrWhiteSpace(script.ParticipantFormation)) {
            if (!LuaParticipantResolver.TryResolve(
                    script,
                    Plugin.Config.Formations,
                    Plugin.Config.CidsGroups,
                    out orderedParticipants,
                    out var participantError)) {
                DalamudApi.ChatGui.PrintError($"[MoP] {participantError}");
                return;
            }
        } else if (variables.ContainsKey("group")) {
            if (!TryResolveParticipantGroup(Plugin.Config.CidsGroups, variables, out var groupName, out orderedParticipants, out var groupError)) {
                DalamudApi.ChatGui.PrintError($"[MoP] {groupError}");
                return;
            }
            variables["group"] = groupName;
        } else {
            var participants = freshPeers
                .Where(peer => peer.ContentId != 0)
                .Select(peer => peer.ContentId)
                .ToHashSet();

            var localCid = DalamudApi.PlayerState.ContentId;
            if (localCid != 0)
                participants.Add(localCid);

            orderedParticipants = participants.OrderBy(cid => cid).ToArray();
        }
        orderedParticipants = CompactVisibleLaunchRoster(
            orderedParticipants,
            VisibleOnlyRequested(variables));
        if (orderedParticipants.Count == 0) {
            const string error = "No active Lua clients were found.";
            DalamudApi.PluginLog.Warning(
                $"[Lua] no clients found; runTarget={runTargetName}; freshPeers={freshPeers.Count}");
            DalamudApi.ChatGui.PrintError($"[MoP] {error}");
            DalamudApi.ShowNotification(error, NotificationType.Error, 6000);
            return;
        }

        DalamudApi.PluginLog.Information(
            $"[Lua] broadcasting script; runTarget={runTargetName}; " +
            $"freshPeers={freshPeers.Count}; participants={orderedParticipants.Count}");

        var startUtcTicks = (DateTime.UtcNow + LuaStartLeadTime).Ticks;
        var seed = Random.Shared.Next();
        SendLocalLuaLaunch(
            script,
            runTargetObjectId,
            runTargetEntityId,
            runTargetName,
            orderedParticipants,
            startUtcTicks,
            seed,
            variables,
            MacroRuntimeVariables.FromCurrentGameState().Me);
        if (MirrorRunTargetValidator.AppliesTo(script.Name)) {
            _mirrorLocalLaunch = new MirrorLocalLaunchState(
                script.Clone(),
                runTargetObjectId,
                runTargetEntityId,
                runTargetName,
                orderedParticipants.ToList(),
                startUtcTicks,
                seed,
                new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase),
                MacroRuntimeVariables.FromCurrentGameState().Me,
                orderedParticipants.ToHashSet(),
                DateTime.UtcNow + MirrorDynamicRosterPollInterval);
        }

        var targetContext = string.IsNullOrWhiteSpace(runTargetName) ? string.Empty : $"; target: {runTargetName}";
        DalamudApi.ShowNotification(
            $"Starting Lua '{script.Name}': {orderedParticipants.Count} clients{targetContext}",
            NotificationType.Success,
            6000);
    }

    private void SendLocalLuaLaunch(
        LuaScriptDefinition script,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName,
        IReadOnlyList<ulong> orderedParticipants,
        long startUtcTicks,
        int seed,
        IReadOnlyDictionary<string, string> variables,
        string conductorName,
        long? startServerTimeSeconds = null) => BroadCast(IpcMessage.Create(
            IpcMessageType.RunLuaScript,
            script.Name,
            script.Hash,
            script.Source,
            runTargetObjectId.ToString(CultureInfo.InvariantCulture),
            runTargetEntityId.ToString(CultureInfo.InvariantCulture),
            runTargetName,
            string.Join(',', orderedParticipants),
            startUtcTicks.ToString(CultureInfo.InvariantCulture),
            seed.ToString(CultureInfo.InvariantCulture),
            EncodeLuaVariablesToken(variables),
            EncodeLuaModulesToken(script.Modules),
            EncodeLuaResourcesToken(script.RequiredResources),
            script.BundleHash,
            conductorName,
            startServerTimeSeconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Serialize(), includeSelf: true);

    public void UpdateMirrorDynamicLaunch() {
        UpdateMirrorStopTombstones();
        UpdateMirrorChatHeartbeat();
        var state = _mirrorLocalLaunch;
        if (state == null || DateTime.UtcNow < state.NextRosterPollAt)
            return;
        state.NextRosterPollAt = DateTime.UtcNow + MirrorDynamicRosterPollInterval;

        var runId = LuaScriptManager.CreateRunId(state.StartUtcTicks, state.Seed, state.Script.Hash);
        if (Plugin.LuaScriptManager.FindActiveSnapshot(runId) == null) {
            _mirrorLocalLaunch = null;
            return;
        }

        var active = GetFreshPeerCharacterData()
            .Select(peer => peer.ContentId)
            .Append(DalamudApi.PlayerState.ContentId)
            .Where(cid => cid != 0)
            .Distinct()
            .ToHashSet();
        var reappeared = state.ParticipantCids.Any(cid => active.Contains(cid) && !state.LastActiveCids.Contains(cid));
        var rosterChanged = false;
        foreach (var cid in active.Where(cid => !state.ParticipantCids.Contains(cid)).OrderBy(cid => cid)) {
            if (state.ParticipantCids.Count < LuaDistributedWireCodec.MaximumRosterCount) {
                state.ParticipantCids.Add(cid);
                rosterChanged = true;
                continue;
            }
            var reusableSlot = state.ParticipantCids.FindIndex(1, existing => !active.Contains(existing));
            if (reusableSlot < 0)
                break;
            state.ParticipantCids[reusableSlot] = cid;
            rosterChanged = true;
        }
        state.LastActiveCids = active;
        if (!rosterChanged && !reappeared)
            return;

        SendLocalLuaLaunch(
            state.Script,
            state.RunTargetObjectId,
            state.RunTargetEntityId,
            state.RunTargetName,
            state.ParticipantCids,
            state.StartUtcTicks,
            state.Seed,
            state.Variables,
            state.ConductorName,
            state.StartServerTimeSeconds);
    }

    private void UpdateMirrorChatHeartbeat() {
        var state = _mirrorChatLaunch;
        if (state == null || DateTime.UtcNow < state.NextBroadcastAt)
            return;
        state.NextBroadcastAt = DateTime.UtcNow + MirrorChatHeartbeatInterval;
        var runId = LuaScriptManager.CreateRunId(
            state.Envelope.StartUtcTicks,
            state.Envelope.Seed,
            state.Envelope.ScriptHash);
        if (Plugin.LuaScriptManager.FindActiveSnapshot(runId) == null) {
            _mirrorChatLaunch = null;
            return;
        }
        var heartbeat = CloneEnvelope(state.Envelope);
        heartbeat.MessageId = Guid.NewGuid().ToString("D");
        heartbeat.CreatedUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        QueueChatSyncedLuaEnvelope(state.ChatPrefix, state.ScriptName, heartbeat, preserveStartIdentity: true);
        DalamudApi.PluginLog.Information(
            $"[LuaSync] queued Mirror launch heartbeat run={runId} message={heartbeat.MessageId}");
    }

    private static LuaChatSyncEnvelope CloneEnvelope(LuaChatSyncEnvelope source) => new() {
        MessageId = source.MessageId,
        CreatedUnixMilliseconds = source.CreatedUnixMilliseconds,
        StartUtcTicks = source.StartUtcTicks,
        StartServerTimeSeconds = source.StartServerTimeSeconds,
        Seed = source.Seed,
        ScriptHash = source.ScriptHash,
        DependencyManifestHash = source.DependencyManifestHash,
        BundleHash = source.BundleHash,
        RequiredResources = source.RequiredResources,
        RunTargetName = source.RunTargetName,
        RunTargetEntityId = source.RunTargetEntityId,
        Variables = new Dictionary<string, string>(source.Variables, StringComparer.OrdinalIgnoreCase),
        ParticipantCids = source.ParticipantCids.ToList(),
    };

    public void StopLuaScript(string? selector = null) {
        _mirrorLocalLaunch = null;
        _mirrorChatLaunch = null;
        BroadCast(IpcMessage.Create(
            IpcMessageType.StopLuaScript,
            string.IsNullOrWhiteSpace(selector) ? string.Empty : selector.Trim()).Serialize(), includeSelf: true);
    }

    public void PauseLuaScript(string? selector = null) {
        BroadCast(IpcMessage.Create(
            IpcMessageType.PauseLuaScript,
            string.IsNullOrWhiteSpace(selector) ? string.Empty : selector.Trim()).Serialize(), includeSelf: true);
    }

    public void ResumeLuaScript(string? selector = null) {
        BroadCast(IpcMessage.Create(
            IpcMessageType.ResumeLuaScript,
            string.IsNullOrWhiteSpace(selector) ? string.Empty : selector.Trim()).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.RunLuaScript)]
    private void HandleRunLuaScript(IpcMessage message) {
        if (message.StringData is not { Length: >= 9 } data)
            return;
        var scriptName = data[0];
        var scriptHash = data[1];
        var scriptSource = data[2];
        if (scriptSource.Length > LuaScriptDefinition.MaximumSourceLength
            || !string.Equals(
                LuaScriptDefinition.ComputeHash(scriptSource),
                scriptHash,
                StringComparison.OrdinalIgnoreCase)) {
            DalamudApi.PluginLog.Warning($"[Lua] rejected script payload with invalid source/hash: {scriptName}");
            return;
        }

        if (!ulong.TryParse(data[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetObjectId))
            return;
        if (!uint.TryParse(data[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetEntityId))
            return;
        if (!TryParseCids(data[6], out var participantCids))
            return;
        if (!long.TryParse(data[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var startUtcTicks))
            return;
        if (!int.TryParse(data[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            return;

        var targetName = data[5];
        IReadOnlyDictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (data.Length >= 10 && !TryDecodeLuaVariablesToken(data[9], out variables))
            return;
        IReadOnlyDictionary<string, string> modules = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data.Length >= 11 && !TryDecodeLuaModulesToken(data[10], out modules))
            return;
        LuaResourceKind? requiredResources = null;
        if (data.Length >= 12 && !TryDecodeLuaResourcesToken(data[11], out requiredResources))
            return;
        if (data.Length < 13 || data[12].Length != 64 || data[12].Any(character => !Uri.IsHexDigit(character))) {
            DalamudApi.PluginLog.Warning($"[Lua] rejected local IPC run '{scriptName}': missing trusted bundle contract hash");
            return;
        }
        var conductorName = data.Length >= 14 ? data[13] : string.Empty;
        long? startServerTimeSeconds = null;
        if (data.Length >= 15 && !string.IsNullOrWhiteSpace(data[14])) {
            if (!long.TryParse(data[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedServerStart))
                return;
            startServerTimeSeconds = parsedServerStart;
        }
        var localScript = LuaScriptCatalog.Find(Plugin.Config, scriptName);
        if (!LuaTrustedBundlePolicy.TrySelectInstalled(
                localScript,
                scriptHash,
                modules,
                requiredResources,
                data[12],
                out var trustedScript,
                out var trustError)) {
            DalamudApi.PluginLog.Warning($"[Lua] rejected local IPC run '{scriptName}': {trustError}");
            DalamudApi.ChatGui.PrintError($"[MoP] Lua run rejected: {trustError}.");
            return;
        }
        // IpcBus dispatches handlers on its publish worker. All Dalamud-backed
        // identity and game-state reads must happen on the framework thread;
        // otherwise a chat-relayed Mirror launch is accepted but fails before
        // the local band can start (ObjectTable.LocalPlayer rejects the worker).
        var decodedVariables = variables;
        var broadcasterId = message.BroadcasterId;
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => {
            var localCid = DalamudApi.PlayerState.ContentId;
            if (localCid == 0 || !participantCids.Contains(localCid))
                return;
            var runId = LuaScriptManager.CreateRunId(startUtcTicks, seed, trustedScript.Hash);
            if (HasMirrorStopTombstone(runId)) {
                DalamudApi.PluginLog.Information($"[Lua:{runId}] rejected launch for a terminal Mirror generation");
                return;
            }
            if (Plugin.LuaScriptManager.FindActiveSnapshot(runId) != null) {
                if (!MirrorRunTargetValidator.AppliesTo(trustedScript.Name))
                    return;
                var extensionError = "participant roster update sender is invalid";
                if (broadcasterId <= 0
                    || !Plugin.LuaScriptManager.TryUpdateParticipantRoster(
                        runId,
                        participantCids,
                        checked((ulong)broadcasterId),
                        out extensionError))
                    DalamudApi.PluginLog.Warning($"[Lua:{runId}] rejected participant roster update: {extensionError}");
                return;
            }
            if (Plugin.LuaScriptManager.FindHistorySnapshot(runId) != null) {
                DalamudApi.PluginLog.Information($"[Lua:{runId}] rejected replay of a terminal Lua generation");
                return;
            }
            var localVariables = AddLocalRuntimeVariables(decodedVariables);
            var conductorCid = participantCids[0];
            var coordination = new LuaRunCoordinationState(
                localCid,
                participantCids,
                isConductor: localCid == conductorCid,
                isDistributed: true,
                conductorContentId: conductorCid);
            coordination.ConfigureTransport(
                (key, value, sequence) => BroadcastLocalLuaSharedVariable(runId, key, value, sequence),
                (messageId, topic, payload, schemaVersion, sequence, targetContentId) =>
                    BroadcastLocalLuaParticipantMessage(
                        runId,
                        messageId,
                        topic,
                        payload,
                        schemaVersion,
                        sequence,
                        targetContentId));
            Plugin.LuaScriptManager.StartScript(
                trustedScript.Name,
                trustedScript.Hash,
                trustedScript.Source,
                targetObjectId,
                targetEntityId,
                targetName,
                participantCids,
                startUtcTicks,
                seed,
                localVariables,
                modules: trustedScript.Modules,
                requiredResources: trustedScript.RequiredResources,
                declaredCapabilities: trustedScript.DeclaredCapabilities,
                conductorName: conductorName,
                startServerTimeSeconds: startServerTimeSeconds,
                coordination: coordination);
        });
    }

    private LuaCoordinationResult BroadcastLocalLuaSharedVariable(
        string runId,
        string key,
        string value,
        long sequence) {
        if (!Plugin.Config.SyncClients)
            return LuaCoordinationResult.Failure("local IPC synchronization is disabled");
        BroadCast(IpcMessage.Create(
            IpcMessageType.LuaSharedVariable,
            runId,
            sequence.ToString(CultureInfo.InvariantCulture),
            key,
            value).Serialize(), includeSelf: true);
        return LuaCoordinationResult.Success("broadcast");
    }

    private LuaCoordinationResult BroadcastLocalLuaParticipantMessage(
        string runId,
        Guid messageId,
        string topic,
        string payload,
        int schemaVersion,
        long sequence,
        ulong targetContentId) {
        if (!Plugin.Config.SyncClients)
            return LuaCoordinationResult.Failure("local IPC synchronization is disabled");
        BroadCast(IpcMessage.Create(
            IpcMessageType.LuaParticipantMessage,
            runId,
            messageId.ToString("D"),
            sequence.ToString(CultureInfo.InvariantCulture),
            schemaVersion.ToString(CultureInfo.InvariantCulture),
            targetContentId.ToString(CultureInfo.InvariantCulture),
            topic,
            payload).Serialize(), includeSelf: true);
        return LuaCoordinationResult.Success("sent");
    }

    [IpcHandle(IpcMessageType.LuaSharedVariable)]
    private void HandleLocalLuaSharedVariable(IpcMessage message) {
        if (message.BroadcasterId <= 0 || message.StringData is not { Length: 4 } data
            || !long.TryParse(data[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence)
            || sequence <= 0)
            return;
        Plugin.LuaScriptManager.TryApplyCoordinationVariable(
            data[0],
            data[2],
            data[3],
            sequence,
            checked((ulong)message.BroadcasterId),
            DateTimeOffset.UtcNow,
            out _);
    }

    [IpcHandle(IpcMessageType.LuaParticipantMessage)]
    private void HandleLocalLuaParticipantMessage(IpcMessage message) {
        if (message.BroadcasterId <= 0 || message.StringData is not { Length: 7 } data
            || !Guid.TryParse(data[1], out var messageId)
            || !long.TryParse(data[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence)
            || !int.TryParse(data[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var schemaVersion)
            || !ulong.TryParse(data[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetContentId)
            || sequence <= 0 || schemaVersion is < 1 or > byte.MaxValue)
            return;
        var accepted = Plugin.LuaScriptManager.TryApplyCoordinationMessage(
            data[0],
            new LuaParticipantMessageSnapshot(
                messageId,
                data[5],
                data[6],
                schemaVersion,
                sequence,
                checked((ulong)message.BroadcasterId),
                targetContentId,
                DateTimeOffset.UtcNow),
            out _);
        var activeScriptName = Plugin.LuaScriptManager.FindActiveSnapshot(data[0])?.ScriptName;
        if (accepted
            && Plugin.LuaScriptManager.TryGetCoordinationParticipantSlot(
                data[0], checked((ulong)message.BroadcasterId), out var senderSlot)
            // Local IPC broadcaster IDs are a same-user/process-boundary trust
            // assertion, not cryptographic remote identity. The run roster and
            // claimed protocol slot still have to agree exactly before persistence.
            && IsValidMirrorStopTombstoneMessage(
                activeScriptName, data[5], data[6], schemaVersion, targetContentId,
                checked((ulong)message.BroadcasterId), senderSlot))
            RecordMirrorStopTombstone(data[0]);
    }

    internal static bool IsValidMirrorStopTombstoneMessage(
        string? scriptName,
        string topic,
        string payload,
        int schemaVersion,
        ulong targetContentId,
        ulong senderContentId,
        int senderSlot) {
        if (!MirrorRunTargetValidator.AppliesTo(scriptName)
            || !topic.Equals("mirror.v2.stop", StringComparison.Ordinal)
            || schemaVersion != 2
            || targetContentId != 0
            || senderContentId == 0
            || senderSlot is < 0 or > 31)
            return false;
        var fields = payload.Split('|', StringSplitOptions.None);
        return fields.Length is 4 or 5
            && fields[0].Equals("2", StringComparison.Ordinal)
            && fields[1].Equals("X", StringComparison.Ordinal)
            && fields[2].Equals("target", StringComparison.Ordinal)
            && int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var claimedSlot)
            && claimedSlot == senderSlot
            && (fields.Length == 4 || fields[4] is "pressure" or "watch-lost" or "reconcile" or "identity" or "validation");
    }

    [IpcHandle(IpcMessageType.StopLuaScript)]
    private void HandleStopLuaScript(IpcMessage message) {
        var selector = message.StringData?.FirstOrDefault();
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => {
            if (IsSafeLuaRunId(selector))
                RecordMirrorStopTombstone(selector!);
            foreach (var run in Plugin.LuaScriptManager.ActiveRuns.Where(run =>
                         run.ScriptName.Equals(LuaScriptCatalog.MirrorScriptV2Name, StringComparison.OrdinalIgnoreCase)
                         && (string.IsNullOrWhiteSpace(selector)
                             || run.RunId.Equals(selector, StringComparison.OrdinalIgnoreCase)
                             || run.ScriptName.Equals(selector, StringComparison.OrdinalIgnoreCase))))
                RecordMirrorStopTombstone(run.RunId);
            Plugin.LuaScriptManager.Stop(
                string.IsNullOrWhiteSpace(selector) ? null : selector,
                "stopped by operator",
                out _);
        });
    }

    [IpcHandle(IpcMessageType.PauseLuaScript)]
    private void HandlePauseLuaScript(IpcMessage message) {
        var selector = message.StringData?.FirstOrDefault();
        _ = DalamudApi.Framework.RunOnFrameworkThread(() =>
            Plugin.LuaScriptManager.Pause(string.IsNullOrWhiteSpace(selector) ? null : selector, out _));
    }

    [IpcHandle(IpcMessageType.ResumeLuaScript)]
    private void HandleResumeLuaScript(IpcMessage message) {
        var selector = message.StringData?.FirstOrDefault();
        _ = DalamudApi.Framework.RunOnFrameworkThread(() =>
            Plugin.LuaScriptManager.Resume(string.IsNullOrWhiteSpace(selector) ? null : selector, out _));
    }

    private static bool TryParseCids(string serialized, out IReadOnlyList<ulong> cids) {
        var parsed = new List<ulong>();
        foreach (var value in serialized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cid)) {
                cids = Array.Empty<ulong>();
                return false;
            }
            parsed.Add(cid);
        }
        cids = parsed;
        return parsed.Count > 0;
    }

    private static (ulong ObjectId, uint EntityId, string Name) CaptureRunAnchor() {
        var target = DalamudApi.TargetManager.Target;
        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        if (target == null) {
            if (localPlayer == null)
                return (0, 0, MacroRuntimeVariables.FromCurrentGameState().Me);

            return (
                localPlayer.GameObjectId,
                localPlayer.EntityId,
                FormationCharacterName.FormatPlayerNameWorld(
                    DalamudApi.PlayerState.CharacterName,
                    DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString(),
                    localPlayer.Name.TextValue));
        }

        var isSelf = localPlayer != null
            && (target.GameObjectId == localPlayer.GameObjectId || target.Address == localPlayer.Address);
        var playerName = isSelf
            ? FormationCharacterName.FormatPlayerNameWorld(
                DalamudApi.PlayerState.CharacterName,
                DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString(),
                target.Name.TextValue)
            : target.GetPlayerNameWorld();
        return (target.GameObjectId, target.EntityId, playerName ?? target.Name.TextValue);
    }

    private static MirrorRunTargetDecision CaptureMirrorRunTarget(string scriptName) {
        var selected = DalamudApi.TargetManager.Target;
        var initiator = DalamudApi.ObjectTable.LocalPlayer;
        return MirrorRunTargetValidator.Decide(
            CaptureMirrorCandidate(selected, selected != null),
            CaptureMirrorCandidate(initiator, initiator != null),
            scriptName);
    }

    private static MirrorRunTargetCandidate CaptureMirrorCandidate(IGameObject? actor, bool isPresent) {
        if (actor == null)
            return default;
        var player = actor as ICharacter;
        var isPlayer = actor.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
            && player != null;
        var name = isPlayer
            ? actor.GetPlayerNameWorld() ?? actor.Name.TextValue
            : actor.Name.TextValue;
        name = FormationCharacterName.NormalizeWorldSeparator(name).Trim();
        return new MirrorRunTargetCandidate(
            isPresent,
            isPlayer,
            actor.Address != nint.Zero && player?.ClassJob.RowId != 0,
            actor.GameObjectId,
            actor.EntityId,
            ResolveMirrorContentId(actor),
            name,
            actor.ObjectKind.ToString());
    }

    private static ulong ResolveMirrorContentId(IGameObject actor) {
        var local = DalamudApi.ObjectTable.LocalPlayer;
        if (local != null
            && (local.GameObjectId == actor.GameObjectId || local.Address == actor.Address))
            return DalamudApi.PlayerState.ContentId;

        foreach (var member in DalamudApi.PartyList) {
            var gameObject = member.GameObject;
            if (gameObject != null
                && (gameObject.GameObjectId == actor.GameObjectId || gameObject.EntityId == actor.EntityId))
                return member.ContentId;
        }
        return 0;
    }

    private static void RejectMirrorRunTarget(string error) {
        DalamudApi.PluginLog.Warning($"[MirrorTarget] launch rejected: {error}");
        DalamudApi.ChatGui.PrintError($"[MoP] {error}");
        DalamudApi.ShowNotification(error, NotificationType.Error, 6000);
    }

    internal static string EncodeRunTargetToken(string runTargetName) =>
        string.IsNullOrWhiteSpace(runTargetName)
            ? "-"
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(runTargetName));

    internal static string ResolveChatSyncedRunTarget(string runTargetName, string senderName) =>
        string.IsNullOrWhiteSpace(runTargetName)
            ? senderName?.Trim() ?? string.Empty
            : runTargetName.Trim();

    internal static bool TryDecodeRunTargetToken(string token, out string runTargetName) {
        if (token == "-") {
            runTargetName = string.Empty;
            return true;
        }

        try {
            runTargetName = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return true;
        } catch (FormatException) {
            runTargetName = string.Empty;
            return false;
        }
    }

    internal static string EncodeLuaVariablesToken(IReadOnlyDictionary<string, string> variables) {
        if (variables.Count == 0)
            return "-";
        var serialized = variables.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase).JsonSerialize();
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized));
    }

    internal static bool TryDecodeLuaVariablesToken(
        string token,
        out IReadOnlyDictionary<string, string> variables) {
        if (token == "-") {
            variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        try {
            var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parsed = serialized.JsonDeserialize<Dictionary<string, string>>()
                ?? new Dictionary<string, string>();
            variables = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
            return true;
        } catch (Exception ex) when (ex is FormatException || ex is Newtonsoft.Json.JsonException) {
            variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return false;
        }
    }

    internal static string EncodeLuaModulesToken(IReadOnlyDictionary<string, string> modules) {
        if (modules == null || modules.Count == 0)
            return "-";
        var normalized = LuaModuleManifest.NormalizeAndValidate(modules);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(normalized.JsonSerialize()));
    }

    internal static bool TryDecodeLuaModulesToken(
        string token,
        out IReadOnlyDictionary<string, string> modules) {
        if (token == "-") {
            modules = new Dictionary<string, string>(StringComparer.Ordinal);
            return true;
        }

        try {
            var bytes = Convert.FromBase64String(token);
            if (bytes.Length > LuaModuleManifest.MaximumTotalSourceLength * 2)
                throw new InvalidDataException("Lua module payload is too large.");
            var parsed = Encoding.UTF8.GetString(bytes).JsonDeserialize<Dictionary<string, string>>()
                ?? new Dictionary<string, string>();
            modules = LuaModuleManifest.NormalizeAndValidate(parsed);
            return true;
        } catch (Exception ex) when (ex is FormatException
            or Newtonsoft.Json.JsonException
            or ArgumentException
            or InvalidDataException) {
            modules = new Dictionary<string, string>(StringComparer.Ordinal);
            return false;
        }
    }

    internal static string EncodeLuaResourcesToken(LuaResourceKind? resources) =>
        resources.HasValue
            ? ((int)LuaResourceKinds.ValidateMask(resources.Value)).ToString(CultureInfo.InvariantCulture)
            : "-";

    internal static bool TryDecodeLuaResourcesToken(string token, out LuaResourceKind? resources) {
        if (token == "-") {
            resources = null;
            return true;
        }
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)) {
            resources = null;
            return false;
        }
        try {
            resources = LuaResourceKinds.ValidateMask((LuaResourceKind)raw);
            return true;
        } catch (ArgumentOutOfRangeException) {
            resources = null;
            return false;
        }
    }

    internal static string EncodeLuaChatSyncEnvelope(LuaChatSyncEnvelope envelope) {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new BinaryWriter(gzip, Encoding.UTF8, leaveOpen: true)) {
            writer.Write((byte)8);
            writer.Write(envelope.StartUtcTicks);
            writer.Write(envelope.StartServerTimeSeconds);
            writer.Write(envelope.Seed);
            writer.Write(Convert.FromHexString(envelope.BundleHash)[..16]);
            writer.Write(envelope.RequiredResources.HasValue);
            if (envelope.RequiredResources.HasValue)
                writer.Write((int)LuaResourceKinds.ValidateMask(envelope.RequiredResources.Value));
            writer.Write(Guid.Parse(envelope.MessageId).ToByteArray());
            writer.Write(envelope.CreatedUnixMilliseconds);
            writer.Write(envelope.RunTargetName ?? string.Empty);
            writer.Write(envelope.RunTargetEntityId);
            var vars = envelope.Variables
                .Where(pair => !pair.Key.Equals("anchor", StringComparison.OrdinalIgnoreCase) || !string.Equals(pair.Value, envelope.RunTargetName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            writer.Write((ushort)vars.Count);
            foreach (var (key, value) in vars) {
                writer.Write(key);
                writer.Write(value ?? string.Empty);
            }
            writer.Write((ushort)envelope.ParticipantCids.Count);
            ulong prev = 0;
            foreach (var cid in envelope.ParticipantCids) {
                writer.Write7BitEncodedInt64((long)cid - (long)prev);
                prev = cid;
            }
        }
        return Convert.ToBase64String(compressed.ToArray());
    }

    internal static bool TryDecodeLuaChatSyncEnvelope(string token, out LuaChatSyncEnvelope envelope) {
        try {
            var compressedBytes = Convert.FromBase64String(token);
            if (compressedBytes.Length > 1024)
                throw new InvalidDataException("Lua sync envelope is too large.");
            using var compressed = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            var buffer = new byte[1024];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0) {
                decompressed.Write(buffer, 0, read);
                if (decompressed.Length > 16 * 1024)
                    throw new InvalidDataException("Lua sync envelope expands beyond its limit.");
            }
            decompressed.Position = 0;
            using var reader = new BinaryReader(decompressed, Encoding.UTF8);
            var version = reader.ReadByte();
            if (version is < 1 or > 8)
                throw new InvalidDataException("Unsupported Lua sync envelope version.");
            var startUtcTicks = reader.ReadInt64();
            var startServerTimeSeconds = version >= 2 ? reader.ReadInt64() : 0;
            var seed = reader.ReadInt32();
            string hash;
            string dependencyManifestHash;
            string bundleHash;
            if (version >= 8) {
                hash = string.Empty;
                dependencyManifestHash = string.Empty;
                bundleHash = Convert.ToHexString(reader.ReadBytes(16)).ToLowerInvariant();
            } else if (version >= 7) {
                hash = string.Empty;
                dependencyManifestHash = string.Empty;
                bundleHash = Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
            } else {
                hash = Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
                dependencyManifestHash = version >= 3
                    ? Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant()
                    : LuaModuleManifest.ComputeHash(null);
                bundleHash = version >= 5
                    ? Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant()
                    : string.Empty;
            }
            LuaResourceKind? requiredResources = null;
            if (version >= 4) {
                var hasRequiredResources = reader.ReadBoolean();
                if (hasRequiredResources)
                    requiredResources = LuaResourceKinds.ValidateMask((LuaResourceKind)reader.ReadInt32());
            }
            var messageId = version >= 6
                ? new Guid(reader.ReadBytes(16)).ToString("D")
                : string.Empty;
            var createdUnixMilliseconds = version >= 6 ? reader.ReadInt64() : 0;
            var runTargetName = reader.ReadString();
            var runTargetEntityId = version >= 2 ? reader.ReadUInt32() : 0;
            var variableCount = reader.ReadUInt16();
            if (variableCount > 64)
                throw new InvalidDataException("Too many Lua variables.");
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < variableCount; index++) {
                var key = reader.ReadString();
                var value = reader.ReadString();
                if (key.Length is 0 or > 100 || value.Length > 1000)
                    throw new InvalidDataException("Invalid Lua variable.");
                variables[key] = value;
            }
            if (version >= 8 && !string.IsNullOrWhiteSpace(runTargetName) && !variables.ContainsKey("anchor")) {
                variables["anchor"] = runTargetName;
            }
            var participantCount = reader.ReadUInt16();
            if (participantCount > 256)
                throw new InvalidDataException("Invalid Lua participant count.");
            var participantCids = new List<ulong>(participantCount);
            if (version >= 7) {
                ulong current = 0;
                for (var index = 0; index < participantCount; index++) {
                    var delta = reader.Read7BitEncodedInt64();
                    current = (ulong)((long)current + delta);
                    participantCids.Add(current);
                }
            } else {
                for (var index = 0; index < participantCount; index++)
                    participantCids.Add(reader.ReadUInt64());
            }
            if (decompressed.Position != decompressed.Length)
                throw new InvalidDataException("Unexpected Lua sync envelope data.");

            envelope = new LuaChatSyncEnvelope {
                MessageId = messageId,
                CreatedUnixMilliseconds = createdUnixMilliseconds,
                StartUtcTicks = startUtcTicks,
                StartServerTimeSeconds = startServerTimeSeconds,
                Seed = seed,
                ScriptHash = hash,
                DependencyManifestHash = dependencyManifestHash,
                BundleHash = bundleHash,
                RequiredResources = requiredResources,
                RunTargetName = runTargetName,
                RunTargetEntityId = runTargetEntityId,
                Variables = variables,
                ParticipantCids = participantCids,
            };
            var validBundleHashLength = version >= 8 ? 32 : 64;
            if (envelope.StartUtcTicks <= 0
                || (version >= 2 && envelope.StartServerTimeSeconds <= 0)
                || (version < 7 && (envelope.ScriptHash.Length != 64 || envelope.ScriptHash.Any(character => !Uri.IsHexDigit(character))))
                || (version < 7 && (envelope.DependencyManifestHash.Length != 64 || envelope.DependencyManifestHash.Any(character => !Uri.IsHexDigit(character))))
                || version < 6
                || !Guid.TryParse(envelope.MessageId, out var messageGuid)
                || messageGuid == Guid.Empty
                || envelope.CreatedUnixMilliseconds <= 0
                || envelope.BundleHash.Length != validBundleHashLength
                || envelope.BundleHash.Any(character => !Uri.IsHexDigit(character))
                || envelope.ParticipantCids.Count > 256
                || envelope.ParticipantCids.Any(cid => cid == 0)
                || envelope.ParticipantCids.Distinct().Count() != envelope.ParticipantCids.Count) {
                envelope = new LuaChatSyncEnvelope();
                return false;
            }
            return true;
        } catch {
            envelope = new LuaChatSyncEnvelope();
            return false;
        }
    }

    private static Dictionary<string, string> ResolveLuaVariables(
        LuaScriptDefinition script,
        Dictionary<string, string>? inlineVariables) {
        var runtime = MacroRuntimeVariables.FromCurrentGameState();
        var variables = new Dictionary<string, string>(runtime.ToDictionary(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ResolveSharedLuaVariables(script, inlineVariables))
            variables[key] = value;
        return variables;
    }

    private static Dictionary<string, string> ResolveSharedLuaVariables(
        LuaScriptDefinition script,
        Dictionary<string, string>? inlineVariables) {
        var runtime = MacroRuntimeVariables.FromCurrentGameState();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(script.Variables)) {
            foreach (var (key, value) in Command.ExtractVariables(Command.PreprocessLines(script.Variables)))
                variables[key] = value;
        }
        foreach (var parameter in script.Parameters ?? [])
            if (!variables.ContainsKey(parameter.Name) && parameter.DefaultValue.Length > 0)
                variables[parameter.Name] = parameter.DefaultValue;
        foreach (var (key, value) in runtime.ResolveInlinePlaceholders(inlineVariables))
            variables[key] = value;
        foreach (var parameter in script.Parameters ?? [])
            parameter.ValidateValue(variables.GetValueOrDefault(parameter.Name) ?? string.Empty);
        return variables;
    }

    private IReadOnlyDictionary<string, string> AddLocalRuntimeVariables(
        IReadOnlyDictionary<string, string> sharedVariables) {
        var result = new Dictionary<string, string>(
            MacroRuntimeVariables.FromCurrentGameState().ToDictionary(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in sharedVariables)
            result[key] = value;
        return result;
    }

    internal static bool TryResolveParticipantGroup(
        IReadOnlyList<CidGroup> groups,
        IReadOnlyDictionary<string, string> variables,
        out string groupName,
        out IReadOnlyList<ulong> participantCids,
        out string error) {
        groupName = variables.FirstOrDefault(pair => pair.Key.Equals("group", StringComparison.OrdinalIgnoreCase)).Value?.Trim()
            ?? string.Empty;
        if (groupName.Length == 0) {
            participantCids = Array.Empty<ulong>();
            error = "Synchronized Lua requires -var=$group=\"Group Name\".";
            return false;
        }

        var requestedGroupName = groupName;
        var group = groups.FirstOrDefault(candidate =>
            candidate.Name.Equals(requestedGroupName, StringComparison.OrdinalIgnoreCase));
        if (group == null) {
            participantCids = Array.Empty<ulong>();
            error = $"Character group '{groupName}' was not found.";
            return false;
        }

        groupName = group.Name;
        participantCids = group.Cids.Where(cid => cid != 0).Distinct().ToArray();
        if (participantCids.Count == 0) {
            error = $"Character group '{groupName}' has no characters.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool VisibleOnlyRequested(IReadOnlyDictionary<string, string> variables) =>
        variables.TryGetValue("visible_only", out var value)
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal));

    internal static bool AllConfiguredRequested(IReadOnlyDictionary<string, string> variables) =>
        variables.TryGetValue("all_configured", out var value)
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal));

    internal static IReadOnlyList<ulong> ResolveConfiguredRoster(IEnumerable<Character> characters) =>
        characters
            .Where(character => character.Cid != 0)
            .Select(character => character.Cid)
            .Distinct()
            .ToArray();

    internal static IReadOnlyList<ulong> ResolveActiveMirrorLaunchRoster(
        IEnumerable<ulong> freshPeerContentIds,
        ulong localContentId) =>
        freshPeerContentIds
            .Where(cid => cid != 0 && cid != localContentId)
            .Distinct()
            .OrderBy(cid => cid)
            .Prepend(localContentId)
            .Where(cid => cid != 0)
            .Take(LuaDistributedWireCodec.MaximumRosterCount)
            .ToArray();

    internal static IReadOnlyList<ulong> ResolveReceivedMirrorLaunchRoster(
        IEnumerable<ulong> transmittedContentIds,
        IEnumerable<ulong> freshPeerContentIds,
        ulong localContentId) {
        var localActive = freshPeerContentIds
            .Append(localContentId)
            .Where(cid => cid != 0)
            .Distinct()
            .OrderBy(cid => cid)
            .ToArray();
        var localSet = localActive.ToHashSet();
        return transmittedContentIds
            .Where(localSet.Contains)
            .Concat(localActive)
            .Distinct()
            .Take(LuaDistributedWireCodec.MaximumRosterCount)
            .ToArray();
    }

    private IReadOnlyList<ulong> ConfiguredRoster() => ResolveConfiguredRoster(Plugin.Config.Characters);

    private IReadOnlyList<ulong> CompactVisibleLaunchRoster(
        IReadOnlyList<ulong> roster,
        bool visibleOnly = false) {
        if (!visibleOnly)
            return roster.Where(cid => cid != 0).Distinct().ToArray();

        var alwaysInclude = GetFreshPeerCharacterData()
            .Select(peer => peer.ContentId)
            .Append(DalamudApi.PlayerState.ContentId)
            .Where(cid => cid != 0)
            .ToHashSet();
        return LuaParticipantResolver.CompactVisible(
            roster,
            LuaParticipantResolver.CharacterNames(Plugin.Config.Characters),
            alwaysInclude);
    }

    private void RecordMirrorStopTombstone(string runId) {
        if (string.IsNullOrWhiteSpace(runId))
            return;
        var now = DateTime.UtcNow;
        var active = GetFreshPeerCharacterData()
            .Select(peer => peer.ContentId)
            .Append(DalamudApi.PlayerState.ContentId)
            .Where(cid => cid != 0)
            .Distinct()
            .ToHashSet();
        lock (_mirrorStopTombstoneLock) {
            if (_mirrorStopTombstones.TryGetValue(runId, out var existing)) {
                existing.ExpiresAt = now + MirrorStopTombstoneLifetime;
                return;
            }
            _mirrorStopTombstones[runId] = new MirrorStopTombstoneState(
                runId,
                active,
                now,
                now + MirrorStopTombstoneLifetime);
        }
    }

    private bool HasMirrorStopTombstone(string runId) {
        lock (_mirrorStopTombstoneLock)
            return _mirrorStopTombstones.TryGetValue(runId, out var state)
                && state.ExpiresAt > DateTime.UtcNow;
    }

    private void UpdateMirrorStopTombstones() {
        var now = DateTime.UtcNow;
        var active = GetFreshPeerCharacterData()
            .Select(peer => peer.ContentId)
            .Append(DalamudApi.PlayerState.ContentId)
            .Where(cid => cid != 0)
            .Distinct()
            .ToHashSet();
        var localCid = DalamudApi.PlayerState.ContentId;
        lock (_mirrorStopTombstoneLock) {
            foreach (var tombstone in _mirrorStopTombstones.Values.ToArray()) {
                if (now >= tombstone.ExpiresAt) {
                    _mirrorStopTombstones.Remove(tombstone.RunId);
                    continue;
                }
                var reappeared = active.Any(cid => !tombstone.LastActiveCids.Contains(cid));
                var elected = localCid != 0 && active.Count > 0 && localCid == active.Min();
                if (elected && (reappeared || now >= tombstone.NextBroadcastAt)) {
                    BroadCast(IpcMessage.Create(IpcMessageType.StopLuaScript, tombstone.RunId).Serialize());
                    tombstone.NextBroadcastAt = now + MirrorStopRepeatInterval;
                }
                tombstone.LastActiveCids = active;
            }
        }
    }

    private sealed class MirrorLocalLaunchState(
        LuaScriptDefinition script,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName,
        List<ulong> participantCids,
        long startUtcTicks,
        int seed,
        IReadOnlyDictionary<string, string> variables,
        string conductorName,
        HashSet<ulong> lastActiveCids,
        DateTime nextRosterPollAt,
        long? startServerTimeSeconds = null) {
        public LuaScriptDefinition Script { get; } = script;
        public ulong RunTargetObjectId { get; } = runTargetObjectId;
        public uint RunTargetEntityId { get; } = runTargetEntityId;
        public string RunTargetName { get; } = runTargetName;
        public List<ulong> ParticipantCids { get; } = participantCids;
        public long StartUtcTicks { get; } = startUtcTicks;
        public int Seed { get; } = seed;
        public IReadOnlyDictionary<string, string> Variables { get; } = variables;
        public string ConductorName { get; } = conductorName;
        public HashSet<ulong> LastActiveCids { get; set; } = lastActiveCids;
        public DateTime NextRosterPollAt { get; set; } = nextRosterPollAt;
        public long? StartServerTimeSeconds { get; } = startServerTimeSeconds;
    }

    private sealed class MirrorStopTombstoneState(
        string runId,
        HashSet<ulong> lastActiveCids,
        DateTime nextBroadcastAt,
        DateTime expiresAt) {
        public string RunId { get; } = runId;
        public HashSet<ulong> LastActiveCids { get; set; } = lastActiveCids;
        public DateTime NextBroadcastAt { get; set; } = nextBroadcastAt;
        public DateTime ExpiresAt { get; set; } = expiresAt;
    }

    private sealed class MirrorChatLaunchState(
        string chatPrefix,
        string scriptName,
        LuaChatSyncEnvelope envelope,
        DateTime nextBroadcastAt) {
        public string ChatPrefix { get; } = chatPrefix;
        public string ScriptName { get; } = scriptName;
        public LuaChatSyncEnvelope Envelope { get; } = envelope;
        public DateTime NextBroadcastAt { get; set; } = nextBroadcastAt;
    }
}

internal sealed class LuaChatSyncEnvelope {
    public string MessageId { get; set; } = string.Empty;
    public long CreatedUnixMilliseconds { get; set; }
    public long StartUtcTicks { get; set; }
    public long StartServerTimeSeconds { get; set; }
    public int Seed { get; set; }
    public string ScriptHash { get; set; } = string.Empty;
    public string DependencyManifestHash { get; set; } = LuaModuleManifest.ComputeHash(null);
    public string BundleHash { get; set; } = string.Empty;
    public LuaResourceKind? RequiredResources { get; set; }
    public string RunTargetName { get; set; } = string.Empty;
    public uint RunTargetEntityId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ulong> ParticipantCids { get; set; } = new();
}
