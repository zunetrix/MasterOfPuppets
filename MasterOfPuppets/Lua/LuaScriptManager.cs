using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dalamud.Game.Chat;
using Dalamud.Game.Text;

using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.Movement;
using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Snapshots;
using MasterOfPuppets.LuaScripting.Watches;

namespace MasterOfPuppets.LuaScripting;

internal sealed class LuaScriptManager : IDisposable {
    private static readonly TimeSpan MaximumRunTime = TimeSpan.FromMinutes(10);
    private const int MaximumHistoryCount = 50;

    private readonly Plugin _plugin;
    private readonly LuaActorWatchService _actorWatches = new();
    private readonly LuaResourceLeaseManager _resourceLeases = new();
    private readonly object _stateLock = new();
    private readonly Dictionary<string, ManagedLuaRun> _activeRuns = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<LuaRunSnapshot> _history = new();
    private readonly LinkedList<LuaRunDiagnosticsSnapshot> _diagnosticHistory = new();
    private string? _primaryRunId;
    private string _idleDetail = "idle";

    public LuaScriptManager(Plugin plugin) {
        _plugin = plugin;
        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
        _plugin.CombatActionObserver.ActionObserved += OnCombatActionObserved;
        _plugin.EmoteObserver.EmotePlayed += OnEmotePlayed;
    }

    public string Status => GetStatusText();

    public LuaRunSnapshot? ActiveRun {
        get {
            lock (_stateLock)
                return GetPrimaryUnsafe()?.Instance.Snapshot;
        }
    }

    public IReadOnlyList<LuaRunSnapshot> ActiveRuns {
        get {
            lock (_stateLock)
                return _activeRuns.Values.Select(run => run.Instance.Snapshot).OrderByDescending(run => run.CreatedAt).ToArray();
        }
    }

    public IReadOnlyList<LuaRunSnapshot> History {
        get { lock (_stateLock) return _history.ToArray(); }
    }

    internal LuaRunSnapshot? FindActiveSnapshot(string runId) {
        lock (_stateLock)
            return _activeRuns.TryGetValue(runId, out var run) ? run.Instance.Snapshot : null;
    }

    internal LuaRunSnapshot? FindHistorySnapshot(string runId) {
        lock (_stateLock)
            return _history.FirstOrDefault(run => run.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
    }

    internal bool TryUpdateParticipantRoster(
        string runId,
        IReadOnlyList<ulong> participants,
        ulong senderContentId,
        out string reason) {
        lock (_stateLock) {
            if (!_activeRuns.TryGetValue(runId, out var run)) {
                reason = "Lua run is not active";
                return false;
            }
            var normalized = participants.Where(cid => cid != 0).Distinct().ToArray();
            if (run.Coordination is not LuaRunCoordinationState state
                || senderContentId != state.ConductorContentId) {
                reason = "participant roster update sender is not the run conductor";
                return false;
            }
            if (!state.TryUpdateParticipantRoster(normalized, out reason))
                return false;
            run.ParticipantCids.Clear();
            run.ParticipantCids.AddRange(normalized);
            foreach (var cid in normalized) {
                var configured = _plugin.Config.Characters.FirstOrDefault(character => character.Cid == cid);
                if (configured != null)
                    run.ConfiguredParticipantNames[cid] = configured.Name ?? string.Empty;
            }
            reason = string.Empty;
            return true;
        }
    }

    internal bool TryApplyCoordinationVariable(
        string runId,
        string key,
        string value,
        long sequence,
        ulong senderContentId,
        DateTimeOffset receivedAt,
        out string reason) {
        lock (_stateLock) {
            if (!_activeRuns.TryGetValue(runId, out var run)
                || run.Coordination is not LuaRunCoordinationState state) {
                reason = "Lua coordination run is not active";
                return false;
            }
            return state.ApplyVariable(key, value, sequence, senderContentId, receivedAt, out reason);
        }
    }

    internal bool TryApplyCoordinationMessage(
        string runId,
        LuaParticipantMessageSnapshot message,
        out string reason) {
        lock (_stateLock) {
            if (!_activeRuns.TryGetValue(runId, out var run)
                || run.Coordination is not LuaRunCoordinationState state) {
                reason = "Lua coordination run is not active";
                return false;
            }
            return state.ApplyMessage(message, out reason);
        }
    }

    internal bool TryGetCoordinationParticipantSlot(string runId, ulong senderContentId, out int slot) {
        lock (_stateLock) {
            if (_activeRuns.TryGetValue(runId, out var run)
                && run.Coordination is LuaRunCoordinationState state)
                return state.TryGetParticipantSlot(senderContentId, out slot);
        }
        slot = -1;
        return false;
    }

    internal bool PublishMirrorEmoteResync(
        string runId,
        uint emoteId,
        bool persistent,
        ulong targetId,
        Guid messageId,
        string senderName) {
        ManagedLuaRun? run;
        lock (_stateLock) {
            if (!_activeRuns.TryGetValue(runId, out run)
                || !run.Instance.Snapshot.ScriptName.Equals(
                    LuaScriptCatalog.MirrorScriptV2Name,
                    StringComparison.OrdinalIgnoreCase))
                return false;
        }
        run.Events.Publish("mirror.remote-emote-resync", new Dictionary<string, string> {
            ["run_id"] = runId,
            ["emote_id"] = emoteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["persistent"] = persistent ? "true" : "false",
            ["target_id"] = targetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["message_id"] = messageId.ToString("D"),
            ["sender"] = senderName ?? string.Empty,
        });
        return true;
    }

    public IReadOnlyList<LuaRunDiagnosticsSnapshot> Diagnostics {
        get {
            lock (_stateLock) {
                var active = _activeRuns.Values
                    .OrderByDescending(run => run.Instance.CreatedAt)
                    .Select(SnapshotDiagnostics);
                return active.Concat(_diagnosticHistory).ToArray();
            }
        }
    }

    internal bool TryAcquireStagingLease(string ownerId, out LuaResourceLease? lease, out string reason) {
        if (_resourceLeases.TryAcquire(
                ownerId,
                LuaResourceKind.Movement | LuaResourceKind.SynchronizedControl,
                out lease,
                out var conflict)) {
            reason = string.Empty;
            return true;
        }
        reason = conflict?.Message ?? "staging resources are unavailable";
        return false;
    }

    public void StartScript(
        string scriptName,
        string scriptHash,
        string scriptSource,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName,
        IReadOnlyList<ulong> participantCids,
        long startUtcTicks,
        int seed,
        IReadOnlyDictionary<string, string>? variables = null,
        TimeSpan? localStartDelay = null,
        long? startServerTimeSeconds = null,
        IReadOnlyDictionary<string, string>? modules = null,
        LuaResourceKind? requiredResources = null,
        IReadOnlyList<string>? declaredCapabilities = null,
        string conductorName = "",
        Func<double>? sharedTimeSeconds = null,
        ILuaCoordinationFacade? coordination = null) {
        var localCid = DalamudApi.PlayerState.ContentId;
        var orderedParticipants = participantCids.Where(cid => cid != 0).Distinct().ToList();
        var slot = orderedParticipants.IndexOf(localCid);
        if (slot < 0)
            return;

        var resources = LuaResourceKinds.ValidateMask(requiredResources ?? LuaResourceKinds.LegacyExclusive);
        if (!requiredResources.HasValue)
            ReplaceLegacyRuns();

        if (sharedTimeSeconds == null && startUtcTicks > 0)
            sharedTimeSeconds = () => Math.Max(0.0, (DateTime.UtcNow.Ticks - startUtcTicks) / (double)TimeSpan.TicksPerSecond);

        var runId = CreateUniqueRunId(startUtcTicks, seed, scriptHash);
        var instance = new LuaRunInstance(
            runId,
            scriptName,
            scriptHash,
            slot,
            orderedParticipants.Count,
            seed,
            MaximumRunTime);
        if (!_resourceLeases.TryAcquire(runId, resources, out var lease, out var conflict)) {
            var conflictingRuns = FindActive(null)
                .Where(r => r.Instance.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (conflictingRuns.Count > 0) {
                foreach (var run in conflictingRuns) {
                    run.Instance.RequestStop("superseded by new run");
                    StopMovement(run);
                    run.Events.Publish("run.stop-requested", new Dictionary<string, string> { ["reason"] = "superseded" });
                    _resourceLeases.Release(run.Instance.RunId, run.Lease.Resources);
                }
                _resourceLeases.TryAcquire(runId, resources, out lease, out conflict);
            }
        }

        if (lease == null) {
            var exception = new LuaResourceConflictException(conflict!);
            instance.Fail(exception);
            AddHistory(instance.Snapshot);
            instance.Dispose();
            DalamudApi.PluginLog.Warning($"[Lua:{runId}] {exception.Message}");
            DalamudApi.ChatGui.PrintError($"[MoP] {exception.Message}");
            return;
        }

        var configuredParticipantNames = _plugin.Config.Characters
            .Where(character => character.Cid != 0)
            .GroupBy(character => character.Cid)
            .ToDictionary(group => group.Key, group => group.First().Name ?? string.Empty);
        coordination ??= new LuaRunCoordinationState(
            localCid,
            orderedParticipants,
            isConductor: true,
            isDistributed: false);
        var liveVariables = new ConcurrentDictionary<string, string>(
            variables ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);

        var managed = new ManagedLuaRun(
            instance,
            lease!,
            resources,
            new LuaTrajectoryController(_plugin),
            new LuaActorFollowController(_plugin),
            new LuaChatMessageCoordinator(),
            new LuaEventHub(),
            new LuaRunLogBuffer(),
            new LuaGameEventTracker(),
            orderedParticipants,
            configuredParticipantNames,
            liveVariables,
            runTargetObjectId,
            runTargetEntityId,
            runTargetName,
            coordination);
        managed.VisibleCompactSlot = slot;
        managed.VisibleCount = Math.Max(1, orderedParticipants.Count);
        if (coordination is LuaRunCoordinationState coordinationState)
            coordinationState.AttachEvents(managed.Events);
        managed.ChatMessages.BeginRun();
        instance.MarkWaiting("waiting for synchronized start");
        managed.Events.Publish("run.waiting", new Dictionary<string, string> {
            ["run_id"] = runId,
            ["script"] = scriptName,
        });
        lock (_stateLock) {
            _activeRuns.Add(runId, managed);
            _primaryRunId = runId;
            _idleDetail = string.Empty;
        }

        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        var localName = localPlayer == null
            ? string.Empty
            : FormationCharacterName.FormatPlayerNameWorld(
                DalamudApi.PlayerState.CharacterName,
                DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString());
        var context = new LuaScriptContext(
            slot,
            orderedParticipants.Count,
            seed,
            sample => {
                EnsureResource(managed, LuaResourceKind.Movement, "trajectory_update");
                managed.Trajectory.Publish(sample);
            },
            text => {
                managed.Logs.Append("info", text);
                DalamudApi.PluginLog.Information($"[Lua:{runId}] {text}");
            },
            message => {
                EnsureResource(managed, LuaResourceKind.ChatActionBudget, "chat");
                Chat.SendMessage(message);
            },
            localPlayer?.Name.TextValue ?? string.Empty,
            managed.ChatMessages.WaitForAsync,
            runTargetName,
            async (anchorName, cancellationToken) => {
                cancellationToken.ThrowIfCancellationRequested();
                await instance.Control.WaitIfPausedAsync(cancellationToken);
                EnsureResource(managed, LuaResourceKind.Movement, "set_anchor");
                var normalizedAnchor = FormationCharacterName.NormalizeWorldSeparator(anchorName);
                await DalamudApi.Framework.RunOnFrameworkThread(() => {
                    if (!IsCurrent(managed))
                        return;
                    var isRunTarget = !string.IsNullOrWhiteSpace(runTargetName)
                        && FormationCharacterName.Matches(runTargetName, normalizedAnchor);
                    var anchorObjectId = isRunTarget ? runTargetObjectId : 0;
                    var anchorEntityId = isRunTarget ? runTargetEntityId : 0;
                    var localIsAnchor = (anchorObjectId != 0 && localPlayer?.GameObjectId == anchorObjectId)
                        || (anchorEntityId != 0 && localPlayer?.EntityId == anchorEntityId)
                        || FormationCharacterName.Matches(normalizedAnchor, localName);
                    if (localIsAnchor) {
                        managed.Trajectory.Stop(stopMovement: true);
                        managed.ActorFollow.Stop(stopMovement: true);
                        instance.SetDetail($"anchor: {normalizedAnchor} (stationary)");
                    } else {
                        managed.ActorFollow.Stop(stopMovement: true);
                        managed.Trajectory.Start(runId, anchorObjectId, anchorEntityId, normalizedAnchor);
                    }
                });
            },
            FollowActor: async (request, cancellationToken) => {
                cancellationToken.ThrowIfCancellationRequested();
                await instance.Control.WaitIfPausedAsync(cancellationToken);
                EnsureResource(managed, LuaResourceKind.Movement, "follow_actor");
                await DalamudApi.Framework.RunOnFrameworkThread(() => {
                    if (!IsCurrent(managed))
                        return;
                    managed.Trajectory.Stop(stopMovement: true);
                    managed.ActorFollow.Start(
                        runId,
                        request,
                        runTargetObjectId,
                        runTargetEntityId,
                        runTargetName);
                    instance.SetDetail($"following: {request.AnchorCandidates[0]}");
                });
            },
            Variables: liveVariables,
            RunId: runId,
            ScriptName: scriptName,
            DalamudVersion: typeof(Dalamud.Plugin.Services.IFramework).Assembly.GetName().Version?.ToString() ?? "unknown",
            GameVersion: "unknown",
            ClientStructsVersion: typeof(FFXIVClientStructs.FFXIV.Client.System.Framework.Framework).Assembly.GetName().Version?.ToString() ?? "unknown",
            Modules: modules,
            ExecutionControl: instance.Control,
            RequestStop: reason => {
                managed.Events.Publish("run.stop-requested", new Dictionary<string, string> { ["reason"] = reason });
                instance.RequestStop(reason);
            },
            DeclaredCapabilities: declaredCapabilities,
            CaptureGameSnapshot: cancellationToken => DalamudApi.Framework
                .RunOnFrameworkThread(() => DalamudLuaGameSnapshotCapture.Capture(
                    orderedParticipants,
                    configuredParticipantNames,
                    runTargetObjectId,
                    runTargetEntityId,
                    runTargetName))
                .WaitAsync(cancellationToken),
            Automation: new PluginLuaAutomationFacade(
                _plugin,
                (resource, operation) => EnsureResource(managed, resource, operation)),
            Actions: new PluginLuaActionFacade(
                _plugin,
                (resource, operation) => EnsureResource(managed, resource, operation)),
            Events: managed.Events,
            ConductorName: conductorName,
            SharedTimeSeconds: sharedTimeSeconds,
            Coordination: coordination,
            GetTargetSpeed: () => managed.Trajectory.AnchorSpeed,
            GetGroup: name => {
                var group = _plugin.Config.CidsGroups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (group == null) return null;
                var characterMap = _plugin.Config.Characters
                    .Where(c => c.Cid != 0)
                    .GroupBy(c => c.Cid)
                    .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
                return group.Cids.Select(cid => characterMap.TryGetValue(cid, out var charName) && !string.IsNullOrWhiteSpace(charName) ? charName : cid.ToString()).ToList();
            },
            IsWalking: () => SimpleMovementWalkState.IsWalking,
            GetVisibleRoster: () => (managed.VisibleCompactSlot, managed.VisibleCount),
            WatchActor: (query, cancellationToken) => DalamudApi.Framework
                .RunOnFrameworkThread(() => _actorWatches.Watch(runId, query, managed.Events))
                .WaitAsync(cancellationToken),
            UnwatchActor: (watchId, cancellationToken) => DalamudApi.Framework
                .RunOnFrameworkThread(() => _actorWatches.Unwatch(runId, watchId))
                .WaitAsync(cancellationToken),
            RequestGlobalStop: MirrorRunTargetValidator.AppliesTo(scriptName)
                ? (reason, cancellationToken) => _plugin.IpcProvider.BroadcastChatSyncedMirrorStopAsync(
                    runId, reason, slot, cancellationToken)
                : null,
            RequestEmoteResync: MirrorRunTargetValidator.AppliesTo(scriptName)
                ? (emoteId, persistent, targetId, cancellationToken) =>
                    _plugin.IpcProvider.BroadcastChatSyncedMirrorEmoteResyncAsync(
                        runId, emoteId, persistent, targetId, cancellationToken)
                : null,
            GetActorEventSources: () => new LuaActorEventSources(
                StateChanges: true,
                CombatActions: _plugin.CombatActionObserver.IsAvailable,
                Emotes: _plugin.EmoteObserver.IsAvailable));

        _ = Task.Run(async () => {
            try {
                await WaitForStartAsync(instance, startUtcTicks, localStartDelay, startServerTimeSeconds);
                var targetStatus = string.IsNullOrWhiteSpace(runTargetName) ? string.Empty : $"target {runTargetName}";
                instance.MarkRunning(targetStatus);
                managed.Events.Publish("run.running", new Dictionary<string, string> {
                    ["run_id"] = runId,
                    ["script"] = scriptName,
                    ["conductor"] = conductorName,
                });
                DalamudApi.PluginLog.Information(
                    $"[Lua:{runId}] running {scriptName} hash={scriptHash[..Math.Min(12, scriptHash.Length)]} "
                    + $"slot={slot + 1}/{orderedParticipants.Count} resources={resources}");
                using var runner = new LuaScriptRunner(context);
                await runner.RunAsync(scriptSource, instance.CancellationToken);
                instance.Complete();
            } catch (OperationCanceledException) when (instance.CancellationToken.IsCancellationRequested) {
                instance.CompleteCancellation();
            } catch (Exception ex) {
                instance.Fail(ex);
                managed.Events.Publish("run.error", new Dictionary<string, string> {
                    ["message"] = ex.Message,
                    ["category"] = instance.Snapshot.Error?.Category ?? "runtime",
                });
                managed.Logs.Append("error", ex.ToString());
                DalamudApi.PluginLog.Error(ex, $"[Lua:{runId}] script failed: {scriptName}");
            } finally {
                _ = DalamudApi.Framework.RunOnFrameworkThread(() => FinalizeRun(managed));
            }
        });
    }

    public void Update() {
        _actorWatches.Update(Environment.TickCount64);
        ManagedLuaRun[] runs;
        lock (_stateLock)
            runs = _activeRuns.Values.ToArray();
        foreach (var run in runs) {
            if (run.Instance.Snapshot.State == LuaRunState.Paused)
                continue;
            run.Trajectory.Update();
            run.ActorFollow.Update();
            ObserveGameEvents(run);
        }
    }

    public int UpdateActiveVariables(IReadOnlyDictionary<string, string> variables) {
        if (variables.Count == 0)
            return 0;

        ManagedLuaRun[] runs;
        lock (_stateLock)
            runs = _activeRuns.Values.ToArray();
        foreach (var run in runs) {
            foreach (var (name, value) in variables)
                run.Variables[name] = value;
            run.Events.Publish("run.variables-updated", variables);
        }
        return runs.Length;
    }

    private static void ObserveGameEvents(ManagedLuaRun run) {
        var now = DateTimeOffset.UtcNow;
        if (!run.GameEvents.ShouldObserve(now))
            return;
        try {
            var snapshot = DalamudLuaGameSnapshotCapture.CaptureEvents(
                run.ParticipantCids,
                run.ConfiguredParticipantNames);
            foreach (var item in run.GameEvents.Observe(snapshot))
                run.Events.Publish(item.Name, item.Data);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, $"[Lua:{run.Instance.RunId}] failed to capture typed game events");
        }
    }

    public void StopLocal(string reason = "stopped") {
        ManagedLuaRun[] runs;
        lock (_stateLock) {
            runs = _activeRuns.Values.ToArray();
            if (runs.Length == 0)
                _idleDetail = reason;
        }
        foreach (var run in runs) {
            run.Events.Publish("run.stop-requested", new Dictionary<string, string> { ["reason"] = reason });
            run.Instance.RequestStop(reason);
            StopMovement(run);
        }
    }

    public void CancelForHostTransition(string reason) {
        ManagedLuaRun[] runs;
        lock (_stateLock) {
            runs = _activeRuns.Values.ToArray();
            if (runs.Length == 0)
                _idleDetail = reason;
        }
        foreach (var run in runs) {
            run.Events.Publish("host.changed", new Dictionary<string, string> { ["reason"] = reason });
            run.Events.Publish("run.cancel-requested", new Dictionary<string, string> { ["reason"] = reason });
            run.Instance.RequestCancel(reason);
            StopMovement(run);
        }
    }

    public bool Stop(string? selector, string reason, out string message) {
        var runs = FindActive(selector);
        if (runs.Count == 0) {
            message = NotFound(selector);
            return false;
        }
        var changed = 0;
        foreach (var run in runs) {
            run.Events.Publish("run.stop-requested", new Dictionary<string, string> { ["reason"] = reason });
            if (run.Instance.RequestStop(reason))
                changed++;
            StopMovement(run);
        }
        message = $"Stopping {changed} Lua run(s): {string.Join(", ", runs.Select(run => run.Instance.RunId))}.";
        return changed > 0;
    }

    public bool Pause(string? selector, out string message) {
        var runs = FindActive(selector);
        if (runs.Count == 0) {
            message = NotFound(selector);
            return false;
        }
        var changed = new List<string>();
        foreach (var run in runs) {
            if (!run.Instance.Pause())
                continue;
            run.Trajectory.PauseMovement();
            run.ActorFollow.PauseMovement();
            run.Events.Publish("run.paused");
            changed.Add(run.Instance.RunId);
        }
        message = changed.Count > 0
            ? $"Paused Lua run(s): {string.Join(", ", changed)}."
            : "Matching Lua runs are not pausable from their current state.";
        return changed.Count > 0;
    }

    public bool Resume(string? selector, out string message) {
        var runs = FindActive(selector);
        if (runs.Count == 0) {
            message = NotFound(selector);
            return false;
        }
        var changed = runs.Where(run => {
            var resumed = run.Instance.Resume("resumed by user");
            if (resumed)
                run.Events.Publish("run.resumed");
            return resumed;
        }).Select(run => run.Instance.RunId).ToArray();
        message = changed.Length > 0
            ? $"Resumed Lua run(s): {string.Join(", ", changed)}."
            : "Matching Lua runs are not paused.";
        return changed.Length > 0;
    }

    public string GetStatusText(string? selector = null) {
        lock (_stateLock) {
            if (string.IsNullOrWhiteSpace(selector)) {
                var active = _activeRuns.Values
                    .Select(run => run.Instance.Snapshot)
                    .OrderByDescending(run => run.CreatedAt)
                    .ToArray();
                if (active.Length == 1)
                    return active[0].ToStatusText();
                if (active.Length > 1)
                    return $"{active.Length} active runs: " + string.Join(" | ", active.Select(run => run.ToStatusText()));
                return _history.First?.Value.ToStatusText() ?? _idleDetail;
            }

            var normalized = selector.Trim();
            var activeMatches = _activeRuns.Values
                .Select(run => run.Instance.Snapshot)
                .Where(run => Matches(run, normalized));
            var historicalMatches = _history.Where(run => Matches(run, normalized));
            var matches = activeMatches.Concat(historicalMatches).Take(10).ToArray();
            return matches.Length == 0
                ? $"no run matches '{normalized}'"
                : string.Join(" | ", matches.Select(run => run.ToStatusText()));
        }
    }

    public bool TryResolveScriptName(string? selector, out string scriptName) {
        lock (_stateLock) {
            if (string.IsNullOrWhiteSpace(selector)) {
                var latest = GetPrimaryUnsafe()?.Instance.Snapshot ?? _history.First?.Value;
                scriptName = latest?.ScriptName ?? string.Empty;
                return latest != null;
            }

            var normalized = selector.Trim();
            var match = _activeRuns.Values
                .Select(run => run.Instance.Snapshot)
                .Concat(_history)
                .FirstOrDefault(run => Matches(run, normalized));
            scriptName = match?.ScriptName ?? normalized;
            return match != null;
        }
    }

    private async Task WaitForStartAsync(
        LuaRunInstance run,
        long startUtcTicks,
        TimeSpan? localStartDelay,
        long? startServerTimeSeconds) {
        if (startUtcTicks > 0) {
            var delay = localStartDelay
                ?? new DateTime(startUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, run.CancellationToken);
        } else if (startServerTimeSeconds.HasValue) {
            while (LuaChoreographyClock.GetServerTimeSeconds() < startServerTimeSeconds.Value) {
                await run.Control.WaitIfPausedAsync(run.CancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(20), run.CancellationToken);
            }
        } else {
            var delay = localStartDelay
                ?? new DateTime(startUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, run.CancellationToken);
        }
        await run.Control.WaitIfPausedAsync(run.CancellationToken);
    }

    private void ReplaceLegacyRuns() {
        ManagedLuaRun[] runs;
        lock (_stateLock) {
            runs = _activeRuns.Values.ToArray();
            _activeRuns.Clear();
            _primaryRunId = null;
        }
        foreach (var run in runs) {
            run.Instance.RequestStop("restarted");
            StopMovement(run);
            run.Lease.Dispose();
        }
    }

    private void FinalizeRun(ManagedLuaRun run) {
        var snapshot = run.Instance.Snapshot;
        lock (_stateLock) {
            if (_activeRuns.TryGetValue(run.Instance.RunId, out var current)
                && ReferenceEquals(current, run))
                _activeRuns.Remove(run.Instance.RunId);
            if (_primaryRunId == run.Instance.RunId)
                _primaryRunId = _activeRuns.Values.OrderByDescending(value => value.Instance.CreatedAt).FirstOrDefault()?.Instance.RunId;
        }
        StopMovement(run);
        _actorWatches.ReleaseOwner(run.Instance.RunId);
        run.Lease.Dispose();
        run.Logs.Append("lifecycle", $"run finished as {snapshot.State.ToString().ToLowerInvariant()}: {snapshot.StopReason}");
        run.Events.Publish("run.terminal", new Dictionary<string, string> {
            ["state"] = snapshot.State.ToString().ToLowerInvariant(),
            ["reason"] = snapshot.StopReason,
        });
        AddHistory(snapshot, SnapshotDiagnostics(run));
        run.Events.Dispose();
        run.Instance.Dispose();
    }

    private void AddHistory(LuaRunSnapshot snapshot, LuaRunDiagnosticsSnapshot? diagnostics = null) {
        lock (_stateLock) {
            _history.AddFirst(snapshot);
            _diagnosticHistory.AddFirst(diagnostics ?? new LuaRunDiagnosticsSnapshot(
                snapshot,
                Array.Empty<LuaRunLogEntry>(),
                new LuaEventHubStatistics(0, 0, 0, 0, true)));
            while (_history.Count > MaximumHistoryCount)
                _history.RemoveLast();
            while (_diagnosticHistory.Count > MaximumHistoryCount)
                _diagnosticHistory.RemoveLast();
            _idleDetail = snapshot.ToStatusText();
        }
    }

    private static LuaRunDiagnosticsSnapshot SnapshotDiagnostics(ManagedLuaRun run) => new(
        run.Instance.Snapshot,
        run.Logs.Snapshot(),
        run.Events.Snapshot()) {
        Trajectory = run.Trajectory.SnapshotDiagnostics(),
    };

    private IReadOnlyList<ManagedLuaRun> FindActive(string? selector) {
        lock (_stateLock) {
            if (string.IsNullOrWhiteSpace(selector))
                return _activeRuns.Values.ToArray();
            var normalized = selector.Trim();
            if (_activeRuns.TryGetValue(normalized, out var exact))
                return [exact];
            return _activeRuns.Values
                .Where(run => run.Instance.ScriptName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(run => run.Instance.CreatedAt)
                .ToArray();
        }
    }

    private bool IsCurrent(ManagedLuaRun run) {
        lock (_stateLock)
            return _activeRuns.TryGetValue(run.Instance.RunId, out var current)
                && ReferenceEquals(current, run)
                && !run.Instance.Snapshot.State.IsTerminal();
    }

    private static void EnsureResource(ManagedLuaRun run, LuaResourceKind resource, string operation) {
        if ((run.Resources & resource) != resource)
            throw new InvalidOperationException(
                $"Lua operation '{operation}' requires declared resource '{resource}'.");
    }

    private static void StopMovement(ManagedLuaRun run) {
        run.Trajectory.Stop(stopMovement: true);
        run.ActorFollow.Stop(stopMovement: true);
    }

    private ManagedLuaRun? GetPrimaryUnsafe() =>
        _primaryRunId != null && _activeRuns.TryGetValue(_primaryRunId, out var primary)
            ? primary
            : _activeRuns.Values.OrderByDescending(run => run.Instance.CreatedAt).FirstOrDefault();

    private static bool Matches(LuaRunSnapshot run, string selector) =>
        run.RunId.Equals(selector, StringComparison.OrdinalIgnoreCase)
        || run.ScriptName.Equals(selector, StringComparison.OrdinalIgnoreCase);

    private static string NotFound(string? selector) => string.IsNullOrWhiteSpace(selector)
        ? "No Lua run is active."
        : $"No active Lua run matches '{selector}'.";

    private string CreateUniqueRunId(long startUtcTicks, int seed, string scriptHash) {
        var baseId = CreateRunId(startUtcTicks, seed, scriptHash);
        lock (_stateLock) {
            var candidate = baseId;
            var suffix = 2;
            while (_activeRuns.ContainsKey(candidate))
                candidate = $"{baseId}-{suffix++}";
            return candidate;
        }
    }

    internal static string CreateRunId(long startUtcTicks, int seed, string scriptHash) {
        var hash = string.IsNullOrWhiteSpace(scriptHash)
            ? "00000000"
            : scriptHash[..Math.Min(8, scriptHash.Length)].ToLowerInvariant();
        return $"{unchecked((ulong)startUtcTicks):x16}-{unchecked((uint)seed):x8}-{hash}";
    }

    private void OnChatMessage(IChatMessage message) {
        if (message.LogKind != XivChatType.Say)
            return;
        var speaker = ChatWatcher.GetSenderName(message);
        var text = ChatWatcher.ResolveTextWithIcons(message.Message);
        ManagedLuaRun[] runs;
        lock (_stateLock)
            runs = _activeRuns.Values.ToArray();
        foreach (var run in runs)
            run.ChatMessages.Publish(speaker, text);
        foreach (var run in runs)
            run.Events.Publish("chat.message", new Dictionary<string, string> {
                ["speaker"] = speaker,
                ["text"] = text,
                ["channel"] = message.LogKind.ToString(),
            });
    }

    private void OnCombatActionObserved(CombatActionObservation observation) {
        ManagedLuaRun[] runs;
        lock (_stateLock)
            runs = _activeRuns.Values
                .Where(run => run.Events.IsInterested("combat.action"))
                .ToArray();

        var hasObservedSource = _actorWatches.HasObservedSource(observation.SourceEntityId);
        if (runs.Length == 0 && !hasObservedSource)
            return;
        var isGroundTargeted = ActionHelper.IsGroundTargeted(observation.ActionId);
        if (hasObservedSource)
            _actorWatches.PublishCombatAction(observation, isGroundTargeted);
        if (runs.Length == 0)
            return;

        var targets = string.Join(',', observation.TargetIds);
        var data = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["source_entity_id"] = observation.SourceEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["action_id"] = observation.ActionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["action_type"] = observation.ActionType.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["global_sequence"] = observation.GlobalSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["source_sequence"] = observation.SourceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["spell_id"] = observation.SpellId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["animation_target_id"] = observation.AnimationTargetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["target_ids"] = targets,
            ["is_ground_targeted"] = isGroundTargeted ? "true" : "false",
        };
        if (observation.TargetPosition is { } position) {
            data["target_x"] = position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            data["target_y"] = position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            data["target_z"] = position.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        foreach (var run in runs)
            run.Events.Publish("combat.action", data, observation.Timestamp);
    }

    private void OnEmotePlayed(EmoteObservation observation) {
        _actorWatches.PublishEmote(observation);
        ManagedLuaRun[] runs;
        lock (_stateLock)
            runs = _activeRuns.Values
                .Where(run => run.Events.IsInterested("emote.played"))
                .ToArray();
        if (runs.Length == 0)
            return;
        var data = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["source_entity_id"] = observation.SourceEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["emote_id"] = observation.EmoteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["target_id"] = observation.TargetId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["is_persistent"] = observation.IsPersistent ? "true" : "false",
        };
        foreach (var run in runs)
            run.Events.Publish("emote.played", data, observation.Timestamp);
    }

    public void Dispose() {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
        _plugin.CombatActionObserver.ActionObserved -= OnCombatActionObserved;
        _plugin.EmoteObserver.EmotePlayed -= OnEmotePlayed;
        ManagedLuaRun[] runs;
        lock (_stateLock) {
            runs = _activeRuns.Values.ToArray();
            _activeRuns.Clear();
            _primaryRunId = null;
        }
        foreach (var run in runs) {
            run.Instance.RequestCancel("plugin disposed");
            StopMovement(run);
            _actorWatches.ReleaseOwner(run.Instance.RunId);
            run.Lease.Dispose();
            run.Events.Dispose();
        }
        _actorWatches.Dispose();
    }

    private sealed record ManagedLuaRun(
        LuaRunInstance Instance,
        LuaResourceLease Lease,
        LuaResourceKind Resources,
        LuaTrajectoryController Trajectory,
        LuaActorFollowController ActorFollow,
        LuaChatMessageCoordinator ChatMessages,
        LuaEventHub Events,
        LuaRunLogBuffer Logs,
        LuaGameEventTracker GameEvents,
        List<ulong> ParticipantCids,
        Dictionary<ulong, string> ConfiguredParticipantNames,
        ConcurrentDictionary<string, string> Variables,
        ulong RunTargetObjectId,
        uint RunTargetEntityId,
        string RunTargetName,
        ILuaCoordinationFacade Coordination) {
        public int VisibleCompactSlot { get; set; }
        public int VisibleCount { get; set; } = 1;
    }
}
