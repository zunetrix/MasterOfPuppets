using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Synchronization;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

using Lumina.Text.ReadOnly;

namespace MasterOfPuppets;

internal class ChatWatcher : IDisposable {
    private const int MaximumChatBytes = 500;

    private Plugin Plugin { get; }
    // private bool _isRegistered;

    public readonly HashSet<XivChatType> AllowedChatTypes = new()
    {
        // XivChatType.Say,
        XivChatType.Party,
        // XivChatType.CrossParty,
        XivChatType.FreeCompany,
        // XivChatType.Alliance,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
    };

    private readonly Dictionary<string, Action<string[], string>> CommandHandlers;
    private readonly LuaReplayWindow _luaReplayWindow = new();
    private readonly LuaReplayWindow _formationReplayWindow = new();
    private readonly LuaChatSyncFragmentAssembler _luaFragmentAssembler = new();
    internal LuaDistributedSessionRegistry LuaDistributedSessions { get; } = new();
    internal LuaDistributedLaunchController LuaDistributedLaunches { get; }

    public ChatWatcher(Plugin plugin) {
        Plugin = plugin;
        LuaDistributedLaunches = new LuaDistributedLaunchController(plugin, LuaDistributedSessions);
        CommandHandlers = new(StringComparer.OrdinalIgnoreCase) {
            ["moprun"] = HandleRunMacro,
            ["mopstop"] = HandleStopMacroExecution,
            ["mopbr"] = HandleBroadcastCommandExecution,
            ["mopbrn"] = HandleBroadcastNotMeCommandExecution,
            ["mopbrc"] = HandleBroadcastCharacterCommandExecution,
            ["mopbrg"] = HandleBroadcastGroupCommandExecution,
            [FormationChatSyncCodec.CommandName] = HandleFormationAnchorSnapshot,
            ["mopluarun"] = HandleLuaRun,
            ["mopluavars"] = HandleLuaVariableUpdate,
            [LuaChatSyncFragmentCodec.CommandName] = HandleLuaChunk,
            ["mopluastop"] = HandleLuaStop,
            ["mopluaemoteresync"] = HandleLuaEmoteResync,
            ["mopluaphase"] = HandleLuaPhase,
        };

        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
        DalamudApi.ChatGui.CheckMessageHandled += OnCheckMessageHandled;
        // UpdateRegistration();
    }

    public void Dispose() {
        LuaDistributedLaunches.CancelAll("plugin disposed");
        _luaFragmentAssembler.Clear();
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
        DalamudApi.ChatGui.CheckMessageHandled -= OnCheckMessageHandled;
    }

    // public void UpdateRegistration() {
    //     if (Plugin.Config.UseChatSync && !_isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    //         _isRegistered = true;
    //     } else if (!Plugin.Config.UseChatSync && _isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    //         _isRegistered = false;
    //     }
    // }

    // public void Dispose() {
    //     if (_isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    //         _isRegistered = false;
    //     }
    // }

    private void OnChatMessage(IChatMessage message) {
        if (!Plugin.Config.UseChatSync) return;
        if (message.IsHandled)
            return;

        if (!AllowedChatTypes.Contains(message.LogKind))
            return;

        var parsedArgs = ArgumentParser.ParseChatArgs(ResolveTextWithIcons(message.Message));
        if (!parsedArgs.Any()) return;

        SuppressInternalSyncEnvelope(message, parsedArgs);

        var senderName = GetSenderName(message);
        if (!Plugin.Config.ListenedChatTypes.Contains(message.LogKind)
            || !IsAllowedSender(senderName))
            return;

#if DEBUG
        DalamudApi.PluginLog.Debug($"OnChatMessage ({senderName} - {message.LogKind}): [{parsedArgs[0]}]: {string.Join("|", parsedArgs.Skip(1))}");
#endif

        if (parsedArgs[0].Equals("mopformation", StringComparison.OrdinalIgnoreCase)) {
            HandleFormationCommand(parsedArgs.Skip(1).ToArray(), senderName, message.LogKind);
        } else if (CommandHandlers.TryGetValue(parsedArgs[0], out var action)) {
            action.Invoke(parsedArgs.Skip(1).ToArray(), senderName);
        }
    }

    private void OnCheckMessageHandled(IChatMessage message) {
        // Suppression is independent of outbound/inbound sync. A frame may
        // have been sent by another client while local chat sync is disabled.
        if (message.IsHandled
            || !AllowedChatTypes.Contains(message.LogKind))
            return;

        var parsedArgs = ArgumentParser.ParseChatArgs(ResolveTextWithIcons(message.Message));
        if (parsedArgs.Any())
            SuppressInternalSyncEnvelope(message, parsedArgs);
    }

    private static void SuppressInternalSyncEnvelope(
        IChatMessage message,
        IReadOnlyList<string> parsedArgs) {
        if (IsInternalLuaSyncEnvelope(parsedArgs)
            && message is IHandleableChatMessage handleable)
            handleable.PreventOriginal();
    }

    internal static bool IsInternalLuaSyncEnvelope(IReadOnlyList<string> parsedArgs) {
        return (parsedArgs.Count == 3
                && parsedArgs[0].Equals("mopluarun", StringComparison.OrdinalIgnoreCase)
                && IpcProvider.TryDecodeLuaChatSyncEnvelope(parsedArgs[2], out _))
            || (parsedArgs.Count == 2
                && parsedArgs[0].Equals("mopluaphase", StringComparison.OrdinalIgnoreCase)
                && LuaDistributedWireCodec.TryDecode(parsedArgs[1], out _, out _))
            || (parsedArgs.Count == 6
                && parsedArgs[0].Equals("mopluaemoteresync", StringComparison.OrdinalIgnoreCase)
                && IpcProvider.TryParseMirrorEmoteResyncArguments(
                    parsedArgs.Skip(1).ToArray(), out _, out _, out _, out _, out _))
            || (parsedArgs.Count == 2
                && parsedArgs[0].Equals("mopluastop", StringComparison.OrdinalIgnoreCase)
                && IpcProvider.TryParseMirrorStopArguments(parsedArgs.Skip(1).ToArray(), out _))
            || (parsedArgs.Count == 6
                && parsedArgs[0].Equals(LuaChatSyncFragmentCodec.CommandName, StringComparison.OrdinalIgnoreCase)
                && LuaChatSyncFragmentCodec.TryParseArguments(parsedArgs.Skip(1).ToArray(), out _, out _))
            || (parsedArgs.Count == 2
                && parsedArgs[0].Equals(FormationChatSyncCodec.CommandName, StringComparison.OrdinalIgnoreCase)
                && FormationChatSyncCodec.TryDecode(parsedArgs[1], out _));
    }

    private void HandleRunMacro(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <macro name>");
            return;
        }

        var inlineVars = args.Length > 1
            ? ArgumentParser.ParseInlineVars(args[1])
            : null;

        if (!string.IsNullOrWhiteSpace(senderName)) {
            inlineVars ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!inlineVars.ContainsKey("mop_origin"))
                inlineVars["mop_origin"] = senderName;
        }

        string macroNameOrIndex = args[0];
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrIndex);
        // Keep action templates and their variable context intact. Resolving to strings here
        // would freeze every variable and prevent later ChatSync updates from taking effect.
        Plugin.MacroHandler.ExecuteMacro(macroIndex, inlineVars);
    }

    public void SendChatStopMacroExecution() {
        var message = $"/p mopstop";
        Chat.SendMessage(message);
    }

    private void HandleStopMacroExecution(string[] args, string senderName) {
        Plugin.IpcProvider.StopMacroExecution();
    }

    private void HandleBroadcastCommandExecution(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <command>");
            return;
        }

        var textCommand = string.Join(" ", args);
        if (TryHandleImmediateMacroVariableUpdate(textCommand, senderName))
            return;
        Plugin.MacroHandler.EnqueueMacroActions("#mopbr-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleBroadcastNotMeCommandExecution(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <command>");
            return;
        }

        var localPlayerName = DalamudApi.PlayerState.CharacterName;
        if (string.Equals(localPlayerName, senderName, StringComparison.OrdinalIgnoreCase)) return;

        var textCommand = string.Join(" ", args);
        if (TryHandleImmediateMacroVariableUpdate(textCommand, senderName))
            return;
        Plugin.MacroHandler.EnqueueMacroActions("#mopbrn-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleBroadcastCharacterCommandExecution(string[] args, string senderName) {
        if (args.Length < 2) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 2 \"Character Name\" <command>");
            return;
        }

        var characterName = args[0];
        var textCommand = string.Join(" ", args.Skip(1));
        var localPlayerName = $"{DalamudApi.PlayerState.CharacterName}@{DalamudApi.PlayerState.HomeWorld.Value.Name}";
        if (!localPlayerName.Contains(characterName, StringComparison.InvariantCultureIgnoreCase)) return;

        if (TryHandleImmediateMacroVariableUpdate(textCommand, senderName))
            return;
        Plugin.MacroHandler.EnqueueMacroActions("#mopbrc-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleBroadcastGroupCommandExecution(string[] args, string senderName) {
        if (args.Length < 2) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 2 \"Group Name\" <command>");
            return;
        }

        var groupName = args[0];
        var textCommand = string.Join(" ", args.Skip(1));
        bool groupHasCid = Plugin.Config.CidsGroups.Any(group =>
            group.Name.Equals(groupName, StringComparison.InvariantCultureIgnoreCase) &&
            group.Cids.Contains(DalamudApi.PlayerState.ContentId)
        );
        if (!groupHasCid) return;
        if (TryHandleImmediateMacroVariableUpdate(textCommand, senderName))
            return;
        Plugin.MacroHandler.EnqueueMacroActions("#mop-inline-macro-group", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleLuaRun(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError("Invalid command arguments expected 1 <script name>");
            return;
        }

        // Backward compatibility: If an encoded envelope token was passed explicitly
        if (args.Length == 2 && IpcProvider.TryDecodeLuaChatSyncEnvelope(args[1], out var envelope)) {
            if (!_luaReplayWindow.TryAccept(
                    envelope.MessageId,
                    envelope.CreatedUnixMilliseconds,
                    DateTimeOffset.UtcNow,
                    out var replayError)) {
                DalamudApi.PluginLog.Warning($"[LuaSync] rejected message {envelope.MessageId} from {senderName}: {replayError}");
                return;
            }

            Plugin.IpcProvider.StartChatSyncedLuaScriptLocal(
                args[0],
                envelope,
                senderName);
            return;
        }

        // Direct readable chat run. Only the actual chat sender may turn this
        // into a synchronized envelope; if every receiving PC independently
        // resolves visible participants, each remote machine can assign its
        // local character the same compact slot.
        Dictionary<string, string>? inlineVariables = null;
        for (var i = 1; i < args.Length; i++) {
            var arg = args[i];
            if (arg.StartsWith("-var=", StringComparison.OrdinalIgnoreCase)) {
                var parsed = ArgumentParser.ParseInlineVars(arg);
                if (parsed != null && parsed.Count > 0) {
                    inlineVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (k, v) in parsed)
                        inlineVariables[k] = v;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(senderName)) {
            inlineVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!inlineVariables.ContainsKey("mop_origin"))
                inlineVariables["mop_origin"] = senderName;
            if (!inlineVariables.ContainsKey("anchor")
                && ShouldDefaultReadableLuaRunAnchorToSender(args[0]))
                inlineVariables["anchor"] = senderName;
        }

        var localName = MacroRuntimeVariables.FromCurrentGameState().Me;
        if (!ShouldRelayReadableLuaRun(senderName, localName)) {
            DalamudApi.PluginLog.Debug(
                $"[LuaSync] waiting for {senderName} to publish the authoritative launch roster");
            return;
        }

        Plugin.IpcProvider.StartChatSyncedLuaScript(args[0], inlineVariables);
    }

    internal static bool ShouldRelayReadableLuaRun(string senderName, string localName) =>
        !string.IsNullOrWhiteSpace(senderName)
        && !string.IsNullOrWhiteSpace(localName)
        && FormationCharacterName.Matches(senderName, localName);

    internal static bool ShouldDefaultReadableLuaRunAnchorToSender(string scriptName) =>
        !MirrorRunTargetValidator.RequiresPlayerRunTarget(scriptName);

    private void HandleLuaChunk(string[] args, string senderName) {
        if (!LuaChatSyncFragmentCodec.TryParseArguments(args, out var fragment, out var parseError)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected malformed launch fragment from {senderName}: {parseError}");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var status = _luaFragmentAssembler.Accept(
            fragment,
            senderName,
            now,
            out var scriptName,
            out var encodedEnvelope,
            out var assemblyError);
        if (status == LuaChatSyncAssemblyStatus.Pending)
            return;
        if (status == LuaChatSyncAssemblyStatus.Rejected) {
            DalamudApi.PluginLog.Warning(
                $"[LuaSync] rejected launch fragment {fragment.Index}/{fragment.Count} "
                + $"for {fragment.MessageId} from {senderName}: {assemblyError}");
            return;
        }
        if (!IpcProvider.TryDecodeLuaChatSyncEnvelope(encodedEnvelope, out var envelope)) {
            DalamudApi.PluginLog.Warning(
                $"[LuaSync] rejected assembled launch {fragment.MessageId} from {senderName}: invalid envelope");
            return;
        }
        if (!Guid.TryParse(envelope.MessageId, out var envelopeMessageId)
            || envelopeMessageId != fragment.MessageId) {
            DalamudApi.PluginLog.Warning(
                $"[LuaSync] rejected assembled launch {fragment.MessageId} from {senderName}: message ID mismatch");
            return;
        }
        if (!_luaReplayWindow.TryAccept(
                envelope.MessageId,
                envelope.CreatedUnixMilliseconds,
                now,
                out var replayError)) {
            DalamudApi.PluginLog.Warning(
                $"[LuaSync] rejected assembled launch {fragment.MessageId} from {senderName}: {replayError}");
            return;
        }

        Plugin.IpcProvider.StartChatSyncedLuaScriptLocal(scriptName, envelope, senderName);
    }

    private void HandleLuaVariableUpdate(string[] args, string senderName) {
        var variables = ParseLuaVariableUpdateArguments(args);
        if (variables.Count == 0) {
            DalamudApi.PluginLog.Warning(
                $"[LuaSync] rejected empty mopluavars update from {senderName}");
            return;
        }

        Plugin.IpcProvider.UpdateMacroVariables(variables);
        DalamudApi.PluginLog.Information(
            $"[LuaSync] accepted {variables.Count} live variable(s) from {senderName}");
    }

    internal static Dictionary<string, string> ParseLuaVariableUpdateArguments(
        IReadOnlyList<string> args) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arg in args) {
            if (!arg.StartsWith("-var=", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var (name, value) in ArgumentParser.ParseInlineVars(arg))
                result[name] = value;
        }
        return result;
    }

    private void HandleLuaStop(string[] args, string senderName) {
        if (args.Length == 0) {
            Plugin.IpcProvider.StopLuaScript();
            return;
        }
        if (!IpcProvider.TryParseMirrorStopArguments(args, out var runId)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected malformed mopluastop from {senderName}");
            return;
        }
        Plugin.IpcProvider.StopLuaScript(runId);
    }

    private void HandleLuaEmoteResync(string[] args, string senderName) {
        if (!IpcProvider.TryParseMirrorEmoteResyncArguments(
                args, out var runId, out var emoteId, out var persistent, out var targetId, out var messageId)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected malformed Mirror emote resync from {senderName}");
            return;
        }
        if (Plugin.LuaScriptManager.PublishMirrorEmoteResync(
                runId, emoteId, persistent, targetId, messageId, senderName))
            DalamudApi.PluginLog.Information(
                $"[LuaSync] accepted Mirror emote resync run={runId} emote={emoteId} "
                + $"message={messageId:D} from={senderName}");
    }

    private void HandleLuaPhase(string[] args, string senderName) {
        var decodeError = "expected one compact phase token";
        if (args.Length != 1 || !LuaDistributedWireCodec.TryDecode(args[0], out var envelope, out decodeError)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected phase frame from {senderName}: {decodeError}");
            return;
        }

        var localIdentity = new LuaDistributedSenderIdentity(
            DalamudApi.PlayerState.ContentId,
            FormationCharacterName.FormatPlayerNameWorld(
                DalamudApi.PlayerState.CharacterName,
                DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString()));
        var configured = Plugin.Config.Characters
            .Where(character => character.Cid != 0)
            .Select(character => new LuaDistributedSenderIdentity(character.Cid, character.Name))
            .ToArray();
        if (!LuaDistributedSenderPolicy.MatchesClaimedContentId(
                senderName,
                envelope.SenderContentId,
                localIdentity,
                configured,
                out var identityError)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected phase sender {senderName}: {identityError}");
            return;
        }

        if (!_luaReplayWindow.TryAccept(
                envelope.MessageId.ToString("D"),
                envelope.CreatedUnixMilliseconds,
                DateTimeOffset.UtcNow,
                out var replayError)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected phase replay {envelope.MessageId} from {senderName}: {replayError}");
            return;
        }
        if (!LuaDistributedSessions.TryAccept(
                envelope,
                FormationCharacterName.NormalizeWorldSeparator(senderName),
                DateTimeOffset.UtcNow,
                out var session,
                out var protocolError)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected {envelope.Kind} for {envelope.RunToken}: {protocolError}");
            return;
        }
        DalamudApi.PluginLog.Debug(
            $"[LuaSync] {envelope.Kind} {envelope.RunToken} from {senderName}; "
            + $"phase={session!.Protocol.Phase} ready={session.Protocol.ReadyCount}/{session.Protocol.Participants.Count}");
        LuaDistributedLaunches.OnPhaseAccepted(envelope);
        if (envelope.Kind == LuaDistributedMessageKind.Stop) {
            foreach (var run in Plugin.LuaScriptManager.ActiveRuns.Where(run =>
                         LuaDistributedWireCodec.CreateRunToken(run.RunId).Equals(envelope.RunToken, StringComparison.OrdinalIgnoreCase)))
                Plugin.LuaScriptManager.Stop(run.RunId, envelope.Detail, out _);
        }
    }

    private static bool IsLocalPlayerSender(string senderName) {
        if (DalamudApi.PlayerState.ContentId == 0)
            return false;

        var localName = FormationCharacterName.FormatPlayerNameWorld(
            DalamudApi.PlayerState.CharacterName,
            DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString());
        return FormationCharacterName.MatchScore(senderName, localName) >= int.MaxValue - 1;
    }

    /// <summary>
    /// Handles live-variable control messages outside the action queues. This is essential
    /// when the queue being controlled is already occupied by a long-running macro.
    /// </summary>
    private bool TryHandleImmediateMacroVariableUpdate(string textCommand, string senderName) {
        const string mopPrefix = "/mop ";
        textCommand = textCommand.Trim();
        if (!textCommand.StartsWith(mopPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var parsedArgs = ArgumentParser.ParseCommandArgs(textCommand[mopPrefix.Length..]);
        if (parsedArgs.Count == 0 ||
            (!parsedArgs[0].Equals("setvar", StringComparison.OrdinalIgnoreCase) &&
             !parsedArgs[0].Equals("setvars", StringComparison.OrdinalIgnoreCase)))
            return false;

        if (parsedArgs.Count < 2) {
            DalamudApi.ChatGui.PrintError("Invalid ChatSync variable update. Usage: mopbr /mop setvar -var=$name=value[;$other=value]");
            return true;
        }

        var variables = ArgumentParser.ParseInlineVars(parsedArgs[1]);
        if (variables.Count == 0) {
            DalamudApi.ChatGui.PrintError("ChatSync variable update contained no valid variables.");
            return true;
        }

        // Do not elect a bridge here. Membership in the game's chat channel is
        // per character, so the elected IPC peer might never receive this line.
        // Every actual receiver fans the idempotent assignment across its local
        // IPC group; duplicate assignments are safe.
        Plugin.IpcProvider.UpdateMacroVariables(variables);
        DalamudApi.PluginLog.Debug(
            $"[ChatSync] Forwarded {variables.Count} live variable(s) across the local IPC group");
        if (IsLocalPlayerSender(senderName)) {
            var chatPrefix = Plugin.Config.DefaultChatSyncPrefix?.Trim();
            if (!string.IsNullOrWhiteSpace(chatPrefix)) {
                var varsToken = "-var=" + string.Join(";", variables.Select(pair =>
                    $"${pair.Key}={FormatLiveVariableValue(pair.Value)}"));
                Chat.SendMessage($"{chatPrefix} mopluavars {varsToken}");
                DalamudApi.PluginLog.Debug(
                    $"[LuaSync] promoted nested setvar to dedicated mopluavars control frame");
            }
        }
        return true;
    }

    private static string FormatLiveVariableValue(string value) =>
        string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace)
            ? $"\"{value.Replace("\"", string.Empty, StringComparison.Ordinal)}\""
            : value;

    private void HandleFormationCommand(string[] args, string senderName, XivChatType chatType) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError("Invalid command arguments expected 1 <formation name>");
            return;
        }

        var anchor = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            args.Skip(1),
            FormationAnchorReference.Sender);
        if (anchor.InvalidArgument != null) {
            DalamudApi.ChatGui.PrintError($"Invalid command argument: {anchor.InvalidArgument}");
            return;
        }

        if (anchor.Anchor.Kind == FormationAnchorKind.Named
            && (string.Equals(anchor.Anchor.Name, "<t>", StringComparison.OrdinalIgnoreCase)
                || string.Equals(anchor.Anchor.Name, "[t]", StringComparison.OrdinalIgnoreCase))) {
            anchor = anchor with { Anchor = FormationAnchorReference.Target };
        }

        if (anchor.Anchor.Kind == FormationAnchorKind.Sender && string.IsNullOrWhiteSpace(anchor.Anchor.Name)) {
            anchor = anchor with { Anchor = anchor.Anchor with { Name = senderName } };
        }

        if (anchor.Anchor.Kind == FormationAnchorKind.FocusTarget) {
            DalamudApi.ChatGui.PrintError(
                "Focus target anchor is not supported for chat-sync formation commands. Use /mopformationmove or /mop formation instead.");
            return;
        }

        // A target is client-local in FFXIV chat. The sender publishes one
        // authoritative transform and the set of formation members visible to it.
        if (anchor.Anchor.Kind == FormationAnchorKind.Target && IsLocalPlayerSender(senderName)) {
            var formation = Plugin.Config.Formations.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, args[0], StringComparison.OrdinalIgnoreCase));
            if (formation == null) {
                DalamudApi.PluginLog.Warning($"[mopformation] formation not found: \"{args[0]}\"");
                return;
            }

            var targetResolved = FormationAnchorResolver.TryResolve(
                    Plugin,
                    new Formation(),
                    anchor.Anchor,
                    out var resolvedTarget,
                    out var failureReason,
                    out var failureKind);
            var anchorContentId = 0UL;
            if (!targetResolved) {
                if (failureKind != FormationAnchorFailureKind.NoTargetSelected
                    || !FormationAnchorResolver.TryResolve(
                        Plugin,
                        formation,
                        FormationAnchorReference.Self,
                        out resolvedTarget,
                        out _,
                        out _)) {
                    LogFormationAnchorFailure(failureReason, failureKind);
                    return;
                }

                anchorContentId = resolvedTarget.ContentId ?? DalamudApi.PlayerState.ContentId;
                DalamudApi.PluginLog.Debug(
                    "[mopformation] no target selected; using the command sender as the formation anchor");
            }

            var channelPrefix = chatType.ToChatPrefix();
            if (string.IsNullOrWhiteSpace(channelPrefix))
                return;

            var eligibleMemberBits = FormationMemberVisibility.CaptureEligibleMemberBits(Plugin, formation);
            var snapshot = new FormationChatAnchorPayload(
                FormationChatSyncCodec.CurrentSchemaVersion,
                Guid.NewGuid().ToString("D"),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                args[0],
                DalamudApi.ClientState.TerritoryType,
                eligibleMemberBits,
                resolvedTarget.Position.X,
                resolvedTarget.Position.Y,
                resolvedTarget.Position.Z,
                resolvedTarget.Rotation,
                resolvedTarget.Name,
                anchorContentId,
                resolvedTarget.GameObjectId ?? 0,
                SimpleInputMovement.FormatMode(anchor.MovementMode));
            var payload = FormationChatSyncCodec.Encode(snapshot);
            var command = $"{channelPrefix} {FormationChatSyncCodec.CommandName} {payload}";
            if (Encoding.UTF8.GetByteCount(command) > MaximumChatBytes) {
                DalamudApi.PluginLog.Warning("[mopformation] target snapshot exceeds the game chat limit; executing locally only");
                FormationLocalMovementExecutor.ExecuteChatSyncedFormationSnapshot(
                    Plugin,
                    args[0],
                    resolvedTarget,
                    anchorContentId,
                    anchor.MovementMode,
                    eligibleMemberBits);
                return;
            }

            try {
                Chat.SendMessage(command);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, "[mopformation] failed to send target snapshot");
                return;
            }

            // The sender executes immediately after the transport accepts the
            // authoritative frame. Its own echoed frame is replay-suppressed.
            _formationReplayWindow.TryAccept(
                snapshot.MessageId,
                snapshot.CreatedUnixMilliseconds,
                DateTimeOffset.UtcNow,
                out _);
            FormationLocalMovementExecutor.ExecuteChatSyncedFormationSnapshot(
                Plugin,
                args[0],
                resolvedTarget,
                anchorContentId,
                anchor.MovementMode,
                eligibleMemberBits);
            return;
        }

        // In an all-current-client group, the original target token is only the
        // trigger. The following snapshot is the authoritative executable frame.
        if (anchor.Anchor.Kind == FormationAnchorKind.Target)
            return;

        _ = DalamudApi.Framework.RunOnFrameworkThread(() =>
            FormationLocalMovementExecutor.ExecuteChatSyncedFormation(
                Plugin,
                args[0],
                anchor.Anchor,
                anchor.MovementMode));
    }

    private static void LogFormationAnchorFailure(
        string failureReason,
        FormationAnchorFailureKind failureKind) {
        var message = $"[mopformation] {failureReason}";
        if (FormationLocalMovementExecutor.IsTransientAnchorFailure(failureKind))
            DalamudApi.PluginLog.Debug(message);
        else
            DalamudApi.PluginLog.Warning(message);
    }

    private void HandleFormationAnchorSnapshot(string[] args, string senderName) {
        if (args.Length != 1 || !FormationChatSyncCodec.TryDecode(args[0], out var payload))
            return;

        if (payload!.TerritoryId != DalamudApi.ClientState.TerritoryType) {
            DalamudApi.PluginLog.Debug(
                $"[mopformation] ignored target snapshot for territory {payload.TerritoryId}");
            return;
        }

        if (!_formationReplayWindow.TryAccept(
                payload.MessageId,
                payload.CreatedUnixMilliseconds,
                DateTimeOffset.UtcNow,
                out var replayError)) {
            DalamudApi.PluginLog.Debug($"[mopformation] rejected target snapshot: {replayError}");
            return;
        }

        if (!SimpleInputMovement.TryParseMode(payload.MovementMode, out var movementMode))
            return;

        _ = DalamudApi.Framework.RunOnFrameworkThread(() =>
                FormationLocalMovementExecutor.ExecuteChatSyncedFormationSnapshot(
                    Plugin,
                    payload.FormationName,
                    new FormationResolvedAnchor(
                        new System.Numerics.Vector3(payload.X, payload.Y, payload.Z),
                        payload.Rotation,
                        null,
                        payload.AnchorName,
                        payload.AnchorGameObjectId == 0 ? null : payload.AnchorGameObjectId),
                payload.AnchorContentId,
                movementMode,
                payload.EligibleMemberBits));
    }

    private static string SanitizeSenderName(string raw) {
        var i = 0;
        while (i < raw.Length && !char.IsLetter(raw[i])) i++;
        return i > 0 ? raw[i..] : raw;
    }

    private bool IsAllowedSender(string senderName) {
        if (!Plugin.Config.UseChatCommandSenderWhitelist)
            return true;

        return Plugin.Config.ChatCommandSenderWhitelist.Any(allowed =>
            string.Equals(
                FormationCharacterName.NormalizeWorldSeparator(allowed),
                senderName,
                StringComparison.OrdinalIgnoreCase));
    }

    internal static string GetSenderName(IChatMessage message) {
        var senderName = GetPlayerPayloadSenderName(message.Sender);
        if (!string.IsNullOrWhiteSpace(senderName))
            return senderName;

        senderName = GetExtractedSenderName(message.OriginalSender);
        if (!string.IsNullOrWhiteSpace(senderName))
            return senderName;

        return GetSenderTextName(message.Sender);
    }

    private static string GetPlayerPayloadSenderName(SeString sender) {
        foreach (var payload in sender.Payloads.OfType<PlayerPayload>()) {
            var formattedName = FormationCharacterName.FormatPlayerNameWorld(
                payload.PlayerName,
                payload.World.ValueNullable?.Name.ToString(),
                payload.DisplayedName);

            if (!string.IsNullOrWhiteSpace(formattedName))
                return formattedName;
        }

        return string.Empty;
    }

    private static string GetExtractedSenderName(ReadOnlySeString sender) =>
        FormationCharacterName.NormalizeWorldSeparator(SanitizeSenderName(sender.ExtractText()));

    private static string GetSenderTextName(SeString sender) {
        var senderName = FormationCharacterName.NormalizeWorldSeparator(SanitizeSenderName(ResolveTextWithIcons(sender)));
        return !string.IsNullOrWhiteSpace(senderName)
            ? senderName
            : FormationCharacterName.NormalizeWorldSeparator(SanitizeSenderName(ResolveTextWithIcons(sender)));
    }

    internal static string ResolveTextWithIcons(SeString seString) {
        var sb = new System.Text.StringBuilder();
        foreach (var payload in seString.Payloads) {
            if (payload is TextPayload textPayload) {
                sb.Append(textPayload.Text);
            } else if (payload is IconPayload iconPayload) {
                if (iconPayload.Icon == BitmapFontIcon.CrossWorld) {
                    sb.Append('@');
                }
            }
        }
        return sb.ToString();
    }

    private static string ExtractTextWithIcons(SeString seString) {
        var sb = new System.Text.StringBuilder();
        foreach (var payload in seString.Payloads) {
            if (payload is TextPayload textPayload) {
                sb.Append(textPayload.Text);
            } else if (payload is IconPayload iconPayload) {
                if (Enum.TryParse<SeIconChar>(iconPayload.Icon.ToString(), out var seIconChar)) {
                    sb.Append((char)seIconChar);
                }
            }
        }
        return sb.ToString();
    }
}
