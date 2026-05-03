using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public partial class MacroHandler : IDisposable {
    private Plugin Plugin { get; }

    private sealed class MacroQueueState : IDisposable {
        public List<string> PendingMacros { get; } = new();  // macro names waiting in queue
        public string? CurrentMacroId { get; set; }           // name of macro being executed
        public List<string> CurrentActions { get; } = new(); // actions of current macro
        public int ActionIndex { get; set; } = -1;

        public ManualResetEventSlim PauseGate { get; } = new(initialState: true);
        public CancellationTokenSource StopCts { get; private set; } = new();

        public bool IsPaused => !PauseGate.IsSet;

        public void Pause() => PauseGate.Reset();
        public void Resume() => PauseGate.Set();

        public void RequestStop() {
            PauseGate.Set();
            var old = StopCts;
            StopCts = new CancellationTokenSource();
            old.Cancel();
            old.Dispose();
        }

        public void Clear() {
            CurrentMacroId = null;
            CurrentActions.Clear();
            ActionIndex = -1;
        }

        public void Dispose() {
            PauseGate.Dispose();
            StopCts.Dispose();
        }
    }

    private readonly MacroQueueState _macroState = new();
    private readonly MacroQueueState _loopState = new();

    // Public surface used by MacroQueueWindow
    public List<string> MacroPendingQueue => _macroState.PendingMacros;
    public string? MacroCurrentId => _macroState.CurrentMacroId;
    public List<string> MacroCurrentActions => _macroState.CurrentActions;
    public int MacroCurrentIndex => _macroState.ActionIndex;

    public List<string> LoopPendingQueue => _loopState.PendingMacros;
    public string? LoopCurrentId => _loopState.CurrentMacroId;
    public List<string> LoopCurrentActions => _loopState.CurrentActions;
    public int LoopCurrentIndex => _loopState.ActionIndex;

    private readonly Channel<(string macroId, string[] actions, double delay)> _macroChannel =
        Channel.CreateUnbounded<(string, string[], double)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<(string macroId, string[] actions, double delay)> _loopChannel =
        Channel.CreateUnbounded<(string, string[], double)>(new UnboundedChannelOptions { SingleReader = true });

    private readonly CancellationTokenSource _cts = new();

    // Global pause/resume (IPC broadcast path)
    public bool IsPaused => _macroState.IsPaused || _loopState.IsPaused;
    public void Pause() { _macroState.Pause(); _loopState.Pause(); }
    public void Resume() { _macroState.Resume(); _loopState.Resume(); }

    // Per-queue pause/resume
    public bool IsMacroQueuePaused => _macroState.IsPaused;
    public bool IsLoopQueuePaused => _loopState.IsPaused;
    public void PauseMacroQueue() => _macroState.Pause();
    public void ResumeMacroQueue() => _macroState.Resume();
    public void PauseLoopQueue() => _loopState.Pause();
    public void ResumeLoopQueue() => _loopState.Resume();

    private static readonly Regex ActionRegex = new(@"^\/(\w+)\s*(.*)$", RegexOptions.Compiled);

    private record MacroCommand(
        Func<string, string, CancellationToken, Task> Handler,
        bool SkipGlobalDelay = false
    );

    private sealed record ResolvedMacroActions(string MacroName, string[] Actions);

    private readonly Dictionary<string, MacroCommand> _commands;

    public static bool CommandSkipsGlobalDelay(string command) =>
        command.ToLowerInvariant() switch {
            "mopwait" or
            "mopmacro" or
            "mopobjectquantity" or
            "moptarget" or
            "moptargetof" or
            "moptargetclear" or
            "moptargetmyminion" or
            "mopenablewalk" or
            "mopdisablewalk" or
            "moptogglewalk" or
            "mopface" or
            "mopfaceabs" or
            "mopmovegearsets" => true,
            _ => false,
        };

    public MacroHandler(Plugin plugin) {
        Plugin = plugin;

        _commands = new Dictionary<string, MacroCommand>(StringComparer.OrdinalIgnoreCase) {
            ["mopwait"] = new(HandleMopWait, SkipGlobalDelay: CommandSkipsGlobalDelay("mopwait")),
            ["mopmacro"] = new(HandleMopMacro, SkipGlobalDelay: CommandSkipsGlobalDelay("mopmacro")),
            ["mopobjectquantity"] = new(HandleMopObjectQuantity, SkipGlobalDelay: CommandSkipsGlobalDelay("mopobjectquantity")),
            ["moptarget"] = new(HandleMopTarget, SkipGlobalDelay: CommandSkipsGlobalDelay("moptarget")),
            ["moptargetof"] = new(HandleMopTargetOf, SkipGlobalDelay: CommandSkipsGlobalDelay("moptargetof")),
            ["moptargetclear"] = new(HandleMopTargetClear, SkipGlobalDelay: CommandSkipsGlobalDelay("moptargetclear")),
            ["moptargetmyminion"] = new(HandleMopTargetMyMinion, SkipGlobalDelay: CommandSkipsGlobalDelay("moptargetmyminion")),
            ["mopaction"] = new(HandleMopAction),
            ["mopitem"] = new(HandleMopItem),
            ["moppetbarslot"] = new(HandleMopPetBarSlot),
            ["mophotbar"] = new(HandleMopHotbar),
            ["mophotbaremote"] = new(HandleMopHotbarEmote),
            ["mopmove"] = new(HandleMopMove),
            ["mopmoverelativeto"] = new(HandleMopMoveRelativeTo),
            ["mopformationmove"] = new(HandleMopFormationMove),
            ["mopmovetotarget"] = new(HandleMopMoveToTarget),
            ["mopmovetocharacter"] = new(HandleMopMoveToCharacter),
            ["mopstopmove"] = new(HandleMopStopMove),
            ["mopenablewalk"] = new(HandleMopEnableWalk, SkipGlobalDelay: CommandSkipsGlobalDelay("mopenablewalk")),
            ["mopdisablewalk"] = new(HandleMopDisableWalk, SkipGlobalDelay: CommandSkipsGlobalDelay("mopdisablewalk")),
            ["moptogglewalk"] = new(HandleMopToggleWalk, SkipGlobalDelay: CommandSkipsGlobalDelay("moptogglewalk")),
            ["mopface"] = new(HandleMopFace, SkipGlobalDelay: CommandSkipsGlobalDelay("mopface")),
            ["mopfaceabs"] = new(HandleMopFaceAbs, SkipGlobalDelay: CommandSkipsGlobalDelay("mopfaceabs")),
            ["mopmovegearsets"] = new(HandleMopMoveGearsets, SkipGlobalDelay: CommandSkipsGlobalDelay("mopmovegearsets")),
        };

        Task.Run(() => RunWorker(_macroChannel, _macroState, _cts.Token));
        Task.Run(() => RunWorker(_loopChannel, _loopState, _cts.Token));
    }

    public void EnqueueMacroActions(string macroId, string[] actions, double delayBetweenActions) {
        bool hasLoop = actions.Any(a =>
            a.Contains("/moploop", StringComparison.OrdinalIgnoreCase) ||
            a.Contains("/moploopstart", StringComparison.OrdinalIgnoreCase));
        var state = hasLoop ? _loopState : _macroState;
        var channel = hasLoop ? _loopChannel : _macroChannel;

        state.PendingMacros.Add(macroId);
        channel.Writer.TryWrite((macroId, actions, delayBetweenActions));
    }

    private async Task RunWorker(
        Channel<(string macroId, string[] actions, double delay)> channel,
        MacroQueueState state,
        CancellationToken lifetimeCt) {
        try {
            await foreach (var (macroId, actions, delay) in channel.Reader.ReadAllAsync(lifetimeCt)) {
                if (state.PendingMacros.Count > 0) state.PendingMacros.RemoveAt(0);
                state.CurrentMacroId = macroId;
                state.CurrentActions.Clear();
                state.CurrentActions.AddRange(actions);
                state.ActionIndex = -1;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCt, state.StopCts.Token);
                try {
                    await ExecuteActions(macroId, actions, delay, linkedCts.Token, state);
                } catch (OperationCanceledException) when (lifetimeCt.IsCancellationRequested) {
                    return;
                } catch (OperationCanceledException) {
                    // stopped mid-execution - continue waiting for next item
                } catch (Exception ex) {
                    DalamudApi.PluginLog.Error(ex, "[MacroHandler] Unexpected error executing actions");
                } finally {
                    state.Clear();
                }
            }
        } catch (OperationCanceledException) { }
    }

    private async Task ExecuteActions(
        string macroId,
        string[] actions,
        double delayBetweenActions,
        CancellationToken token,
        MacroQueueState state) {
        bool shouldLoop;
        int? loopsLeft = null;

        do {
            shouldLoop = false;
            state.ActionIndex = -1;

            // State for /moploopstart … /moploopend block (reset each outer loop pass).
            int loopBlockStart = -1;
            int? loopBlockIterLeft = null;

            for (int i = 0; i < actions.Length; i++) {
                if (token.IsCancellationRequested) return;
                state.PauseGate.Wait(token);

                state.ActionIndex = i;

                // Apply dynamic token substitution ({random(min,max)}, etc.)
                var action = MacroTokenProcessor.Process(actions[i]);

                var match = ActionRegex.Match(action);
                var command = match.Success ? match.Groups[1].Value : null;
                var args = match.Success ? match.Groups[2].Value.Trim() : null;

                // /moploop - whole-macro loop (existing behaviour)
                if (command != null && command.Equals("moploop", StringComparison.OrdinalIgnoreCase)) {
                    if (string.IsNullOrWhiteSpace(args)) {
                        shouldLoop = true;
                    } else if (uint.TryParse(args, out uint count)) {
                        if (loopsLeft == null) loopsLeft = (int)count - 1;
                        else loopsLeft--;
                        shouldLoop = loopsLeft > 0;
                    } else {
                        DalamudApi.PluginLog.Warning($"[moploop] invalid argument: \"{args}\"");
                    }
                    break;
                }

                // /moploopstart [N] - begin a loop block (runs N times, or forever)
                if (command != null && command.Equals("moploopstart", StringComparison.OrdinalIgnoreCase)) {
                    loopBlockStart = i + 1;
                    if (string.IsNullOrWhiteSpace(args)) {
                        loopBlockIterLeft = null; // infinite
                    } else if (uint.TryParse(args, out uint count) && count > 0) {
                        loopBlockIterLeft = (int)count - 1; // -1: first pass already in progress
                    } else {
                        DalamudApi.PluginLog.Warning($"[moploopstart] invalid argument: \"{args}\"");
                    }
                    continue;
                }

                // /moploopend - jump back to loopBlockStart, or fall through when exhausted
                if (command != null && command.Equals("moploopend", StringComparison.OrdinalIgnoreCase)) {
                    if (loopBlockStart < 0) {
                        DalamudApi.PluginLog.Warning("[moploopend] no matching /moploopstart found");
                    } else if (loopBlockIterLeft == null || loopBlockIterLeft > 0) {
                        if (loopBlockIterLeft != null) loopBlockIterLeft--;
                        i = loopBlockStart - 1; // -1 because the for loop will i++
                    } else {
                        // loop exhausted - reset block state and continue
                        loopBlockStart = -1;
                        loopBlockIterLeft = null;
                    }
                    continue;
                }

                if (command != null && _commands.TryGetValue(command, out var cmd)) {
                    await cmd.Handler(macroId, args!, token);
                    if (!cmd.SkipGlobalDelay && delayBetweenActions > 0.0) {
                        var delayMs = TimeSpan.FromSeconds(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero));
                        DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                        await Task.Delay(delayMs, token);
                    }
                } else {
                    // No regex match OR unrecognised command - forward raw to chat
                    DalamudApi.PluginLog.Debug($"[Execute Action] {action}");
                    _ = DalamudApi.Framework.RunOnFrameworkThread(() => { Chat.SendMessage(action); });
                    if (delayBetweenActions > 0.0) {
                        var delayMs = TimeSpan.FromSeconds(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero));
                        await Task.Delay(delayMs, token);
                    }
                }
            }
        } while (shouldLoop && !token.IsCancellationRequested);
    }

    private ResolvedMacroActions ResolveLocalPlayerMacroActions(int macroIndex, Dictionary<string, string>? inlineVars = null) {
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerCid = DalamudApi.PlayerState.ContentId;
        var actions = macro.GetCidActions(
            playerCid,
            Plugin.Config.CidsGroups,
            inlineVars,
            MacroRuntimeVariables.FromCurrentGameState());
        return new ResolvedMacroActions(macro.Name, actions);
    }

    private ResolvedMacroActions ResolveLocalPlayerMacroActions(string macroNameOrNumber, Dictionary<string, string>? inlineVars = null) {
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrNumber);
        return ResolveLocalPlayerMacroActions(macroIndex, inlineVars);
    }

    private async Task<ResolvedMacroActions> ResolveLocalPlayerMacroActionsOnFrameworkThread(
        int macroIndex,
        Dictionary<string, string>? inlineVars = null) {
        ResolvedMacroActions? resolved = null;
        await DalamudApi.Framework.RunOnFrameworkThread(() => {
            resolved = ResolveLocalPlayerMacroActions(macroIndex, inlineVars);
        });
        return resolved ?? new ResolvedMacroActions(string.Empty, []);
    }

    private async Task<ResolvedMacroActions> ResolveLocalPlayerMacroActionsOnFrameworkThread(
        string macroNameOrNumber,
        Dictionary<string, string>? inlineVars = null) {
        ResolvedMacroActions? resolved = null;
        await DalamudApi.Framework.RunOnFrameworkThread(() => {
            resolved = ResolveLocalPlayerMacroActions(macroNameOrNumber, inlineVars);
        });
        return resolved ?? new ResolvedMacroActions(string.Empty, []);
    }

    private async Task ResolveAndEnqueueLocalPlayerMacroActions(
        int macroIndex,
        Dictionary<string, string>? inlineVars,
        CancellationToken token = default) {
        try {
            var resolved = await ResolveLocalPlayerMacroActionsOnFrameworkThread(macroIndex, inlineVars);
            if (token.IsCancellationRequested)
                return;
            EnqueueMacroActions(resolved.MacroName, resolved.Actions, Plugin.Config.DelayBetweenActions);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, $"[MacroHandler] Failed to resolve macro {macroIndex + 1}");
        }
    }

    private async Task ResolveAndEnqueueLocalPlayerMacroActions(
        string macroNameOrNumber,
        Dictionary<string, string>? inlineVars,
        CancellationToken token = default) {
        try {
            var resolved = await ResolveLocalPlayerMacroActionsOnFrameworkThread(macroNameOrNumber, inlineVars);
            if (token.IsCancellationRequested)
                return;
            EnqueueMacroActions(resolved.MacroName, resolved.Actions, Plugin.Config.DelayBetweenActions);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, $"[MacroHandler] Failed to resolve macro {macroNameOrNumber}");
        }
    }

    public void ExecuteMacro(int macroIndex, Dictionary<string, string>? inlineVars = null) {
        _ = ResolveAndEnqueueLocalPlayerMacroActions(macroIndex, inlineVars);
    }

    // Per-queue stop
    public void StopMacroQueue() {
        _macroState.RequestStop();
        while (_macroChannel.Reader.TryRead(out _)) { }
        _macroState.PendingMacros.Clear();
        _macroState.Clear();
    }

    public void StopLoopQueue() {
        _loopState.RequestStop();
        while (_loopChannel.Reader.TryRead(out _)) { }
        _loopState.PendingMacros.Clear();
        _loopState.Clear();
    }

    // Global stop (IPC broadcast path)
    public void StopMacroQueueExecution() {
        StopMacroQueue();
        StopLoopQueue();
    }

    public void Dispose() {
        _macroState.RequestStop();
        _loopState.RequestStop();
        _cts.Cancel();
        _macroChannel.Writer.Complete();
        _loopChannel.Writer.Complete();
        _cts.Dispose();
        _macroState.Dispose();
        _loopState.Dispose();
    }
}
