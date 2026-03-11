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

    private readonly Channel<(string macroId, string[] actions, double delay)> _macroChannel =
        Channel.CreateUnbounded<(string, string[], double)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<(string macroId, string[] actions, double delay)> _loopChannel =
        Channel.CreateUnbounded<(string, string[], double)>(new UnboundedChannelOptions { SingleReader = true });

    private CancellationTokenSource _cts = new();

    public List<string> CurrentActionsExecutionList { get; private set; } = new();
    public int CurrentActionExecutionIndex { get; private set; } = -1;

    public List<string> CurrentActionsLoopExecutionList { get; private set; } = new();
    public int CurrentActionLoopExecutionIndex { get; private set; } = -1;

    private static readonly Regex ActionRegex = new(@"^\/(\w+)\s*(.*)$", RegexOptions.Compiled);

    private record MacroCommand(
        Func<string, string, CancellationToken, Task> Handler,
        bool SkipGlobalDelay = false
    );

    private readonly Dictionary<string, MacroCommand> _commands;

    public MacroHandler(Plugin plugin) {
        Plugin = plugin;

        _commands = new Dictionary<string, MacroCommand>(StringComparer.OrdinalIgnoreCase) {
            ["mopwait"]            = new(HandleMopWait,            SkipGlobalDelay: true),
            ["moploop"]            = new(HandleMopLoop,            SkipGlobalDelay: true),
            ["mopmacro"]           = new(HandleMopMacro,           SkipGlobalDelay: true),
            ["mopobjectquantity"]  = new(HandleMopObjectQuantity,  SkipGlobalDelay: true),
            ["moptarget"]          = new(HandleMopTarget,          SkipGlobalDelay: true),
            ["moptargetof"]        = new(HandleMopTargetOf,        SkipGlobalDelay: true),
            ["moptargetclear"]     = new(HandleMopTargetClear,     SkipGlobalDelay: true),
            ["moptargetmyminion"]  = new(HandleMopTargetMyMinion,  SkipGlobalDelay: true),
            ["mopaction"]          = new(HandleMopAction),
            ["mopitem"]            = new(HandleMopItem),
            ["moppetbarslot"]      = new(HandleMopPetBarSlot),
            ["mophotbar"]          = new(HandleMopHotbar),
            ["mophotbaremote"]     = new(HandleMopHotbarEmote),
            ["mopmove"]            = new(HandleMopMove),
            ["mopmoverelativeto"]  = new(HandleMopMoveRelativeTo),
            ["mopmovetotarget"]    = new(HandleMopMoveToTarget),
            ["mopmovetocharacter"] = new(HandleMopMoveToCharacter),
            ["mopstopmove"]        = new(HandleMopStopMove),
            ["mopenablewalk"]      = new(HandleMopEnableWalk,      SkipGlobalDelay: true),
            ["mopdisablewalk"]     = new(HandleMopDisableWalk,     SkipGlobalDelay: true),
            ["moptogglewalk"]      = new(HandleMopToggleWalk,      SkipGlobalDelay: true),
        };

        Task.Run(() => RunWorker(_macroChannel, isLoop: false, _cts.Token));
        Task.Run(() => RunWorker(_loopChannel, isLoop: true, _cts.Token));
    }

    public void EnqueueMacroActions(string macroId, string[] actions, double delayBetweenActions) {
        bool hasLoop = actions.Any(a => a.Contains("/moploop", StringComparison.OrdinalIgnoreCase));
        if (hasLoop) {
            _loopChannel.Writer.TryWrite((macroId, actions, delayBetweenActions));
            CurrentActionsLoopExecutionList.AddRange(actions);
        } else {
            _macroChannel.Writer.TryWrite((macroId, actions, delayBetweenActions));
            CurrentActionsExecutionList.AddRange(actions);
        }
    }

    private async Task RunWorker(Channel<(string macroId, string[] actions, double delay)> channel, bool isLoop, CancellationToken ct) {
        try {
            await foreach (var (macroId, actions, delay) in channel.Reader.ReadAllAsync(ct)) {
                await ExecuteActions(macroId, actions, delay, ct, isLoop);
                if (channel.Reader.Count == 0) {
                    if (isLoop) ClearActionsLoopExecutionList();
                    else ClearActionsExecutionList();
                }
            }
        } catch (OperationCanceledException) { }
    }

    private async Task ExecuteActions(string macroId, string[] actions, double delayBetweenActions, CancellationToken token, bool isLoop) {
        foreach (var action in actions) {
            if (token.IsCancellationRequested) break;

            if (isLoop) CurrentActionLoopExecutionIndex++;
            else CurrentActionExecutionIndex++;

            var match = ActionRegex.Match(action);
            bool handled = false;

            if (match.Success) {
                var command = match.Groups[1].Value;
                var args = match.Groups[2].Value.Trim();

                if (_commands.TryGetValue(command, out var cmd)) {
                    await cmd.Handler(macroId, args, token);
                    handled = true;

                    if (!cmd.SkipGlobalDelay && delayBetweenActions > 0.0) {
                        var delayMs = TimeSpan.FromSeconds(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero));
                        DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                        await Task.Delay(delayMs, token);
                    }
                }
            }

            if (!handled) {
                DalamudApi.PluginLog.Debug($"[Execute Action] {action}");
                _ = DalamudApi.Framework.RunOnFrameworkThread(delegate {
                    Chat.SendMessage(action);
                });

                if (delayBetweenActions > 0.0) {
                    var delayMs = TimeSpan.FromSeconds(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero));
                    DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                    await Task.Delay(delayMs, token);
                }
            }
        }
    }

    private string[] GetLocalPlayerMacroActions(string macroNameOrNumber) {
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrNumber);
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerCid = DalamudApi.PlayerState.ContentId;
        return macro.GetCidActions(playerCid);
    }

    public void ExecuteMacro(int macroIndex) {
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerCid = DalamudApi.PlayerState.ContentId;
        var actions = macro.GetCidActions(playerCid);
        EnqueueMacroActions(macro.Name, actions, Plugin.Config.DelayBetweenActions);
    }

    private void ClearActionsExecutionList() {
        CurrentActionsExecutionList.Clear();
        CurrentActionExecutionIndex = -1;
    }

    private void ClearActionsLoopExecutionList() {
        CurrentActionsLoopExecutionList.Clear();
        CurrentActionLoopExecutionIndex = -1;
    }

    public void StopMacroQueueExecution() {
        _cts.Cancel();
        _cts.Dispose();

        while (_macroChannel.Reader.TryRead(out _)) { }
        while (_loopChannel.Reader.TryRead(out _)) { }

        ClearActionsExecutionList();
        ClearActionsLoopExecutionList();

        _cts = new CancellationTokenSource();
        Task.Run(() => RunWorker(_macroChannel, isLoop: false, _cts.Token));
        Task.Run(() => RunWorker(_loopChannel, isLoop: true, _cts.Token));
    }

    public void Dispose() {
        _cts.Cancel();
        _macroChannel.Writer.Complete();
        _loopChannel.Writer.Complete();
        _cts.Dispose();
    }
}
