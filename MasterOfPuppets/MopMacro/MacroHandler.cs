using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

public class MacroHandler : IDisposable {
    private Plugin Plugin { get; }

    private readonly ConcurrentQueue<(string macroId, string[] actions, double delay)> MacroQueue = new();

    private CancellationTokenSource _cancelTokenSource = new();
    private bool _runningMacroQueue = false;

    public List<string> CurrentActionsExecutionList { get; private set; } = new();
    public int CurrentActionExecutionIndex { get; private set; } = -1;


    private readonly ConcurrentQueue<(string macroId, string[] actions, double delay)> MacroLoopQueue = new();
    private bool _runningMacroLoopQueue = false;
    public List<string> CurrentActionsLoopExecutionList { get; private set; } = new();
    public int CurrentActionLoopExecutionIndex { get; private set; } = -1;

    private readonly Dictionary<string, Func<string, string, CancellationToken, Task>> CustomMacroActionHandlers;

    //  private readonly Dictionary<string, IMacroActionHandler> CustomMacroActionHandlers;

    public MacroHandler(Plugin plugin) {
        Plugin = plugin;

        // var handlers = new IMacroActionHandler[]
        // {
        //     new MopWaitHandler(),
        //     new MopLoopHandler(this, plugin),
        //     new MopMacroHandler(this, plugin),
        //     new MopObjectQuantityHandler(),
        //     new MopActionHandler(),
        //     new MopItemHandler(),
        //     new MopTargetHandler(),
        //     new MopTargetOfHandler(),
        //     new MopTargetClearHandler(),
        //     new MopPetBarSlotHandler()
        // };

        // CustomMacroActionHandlers = handlers.ToDictionary(h => h.Command, h => h, StringComparer.OrdinalIgnoreCase);

        // TODO: refactor into individual handlers strategy pattern with help stuff
        CustomMacroActionHandlers = new(StringComparer.OrdinalIgnoreCase) {
            ["mopwait"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args) ||
                !double.TryParse(args, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)) {
                    DalamudApi.PluginLog.Warning($"[mopwait] invalid argument: \"{args}\"");
                    return;
                }

                var secondsRound = Math.Round(seconds, 2, MidpointRounding.AwayFromZero);
                var delayMs = TimeSpan.FromSeconds(secondsRound);

                DalamudApi.PluginLog.Debug($"[mopwait] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}...");
                await Task.Delay(delayMs, token);
            },

            ["moploop"] = async (macroId, args, token) => {
                var actions = this.GetLocalPlayerMacroActions(macroId);

                // no args run indefinitely
                if (string.IsNullOrWhiteSpace(args)) {
                    DalamudApi.PluginLog.Debug($"[moploop]");
                    this.ClearActionsLoopExecutionList();
                    this.EnqueueMacroActions(macroId, actions, Plugin.Config.DelayBetweenActions);
                    return;
                }

                if (!uint.TryParse(args, out uint runAmount)) {
                    DalamudApi.PluginLog.Warning($"[moploop] invalid argument: \"{args}\"");
                    return;
                }

                // remove loop and add n times
                string[] noLoopActions = actions
                .Where((action) => !action.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase))
                .ToArray();

                this.ClearActionsLoopExecutionList();
                for (int i = 0; i < runAmount; i++) {
                    this.EnqueueMacroActions(macroId, noLoopActions, Plugin.Config.DelayBetweenActions);
                }

                DalamudApi.PluginLog.Debug($"[moploop] {runAmount}");
                await Task.CompletedTask;
            },

            ["mopmacro"] = async (macroId, args, token) => {
                // tirar aspas
                var macroName = args.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(macroName)) {
                    DalamudApi.PluginLog.Warning($"[mopmacro] invalid argument: \"{args}\"");
                    return;
                }

                var actions = this.GetLocalPlayerMacroActions(macroName);
                this.EnqueueMacroActions(args, actions, Plugin.Config.DelayBetweenActions);

                await Task.CompletedTask;
            },

            ["mopobjectquantity"] = async (macroId, args, token) => {
                if (!Enum.TryParse<SettingsDisplayObjectLimitType>(args, ignoreCase: true, out var displayObjectLimitType)
                    || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
                    DalamudApi.PluginLog.Warning($"[mopsetobjectquantity] Invalid object quantity value (0-5): {displayObjectLimitType}");
                    return;
                }

                GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
                await Task.CompletedTask;
            },

            ["mopaction"] = async (macroId, args, token) => {
                var actionIdOrName = args.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(actionIdOrName)) {
                    DalamudApi.PluginLog.Warning($"[mopaction] invalid argument: \"{actionIdOrName}\"");
                    return;
                }

                if (uint.TryParse(actionIdOrName, out uint actionId)) {
                    GameActionManager.UseActionById(actionId);
                    DalamudApi.PluginLog.Debug($"[mopaction] {actionId}");
                } else {
                    GameActionManager.UseActionByName(actionIdOrName);
                    DalamudApi.PluginLog.Debug($"[mopaction] {actionIdOrName}");
                }

                await Task.CompletedTask;
            },

            ["mopitem"] = async (macroId, args, token) => {
                var itemIdOrName = args.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(itemIdOrName)) {
                    DalamudApi.PluginLog.Warning($"[mopitem] invalid argument: \"{itemIdOrName}\"");
                    return;
                }

                if (uint.TryParse(itemIdOrName, out uint itemId)) {
                    GameActionManager.UseItemById(itemId);
                    // GameActionManager.UseInventoryItemById(itemId);
                    DalamudApi.PluginLog.Debug($"[mopitem] {itemId}");
                } else {
                    GameActionManager.UseItemByName(itemIdOrName);
                    // GameActionManager.UseInventoryItemByName(itemIdOrName);
                    DalamudApi.PluginLog.Debug($"[mopitem] {itemIdOrName}");
                }

                await Task.CompletedTask;
            },

            ["moptarget"] = async (macroId, args, token) => {
                string targetName = args.Trim().Trim('"');
                TargetManager.TargetByName(targetName);
                DalamudApi.PluginLog.Debug($"[moptarget] {targetName}");
                await Task.CompletedTask;
            },

            ["moptargetof"] = async (macroId, args, token) => {
                string targetName = args.Trim().Trim('"');
                TargetManager.TargetOf(targetName);
                DalamudApi.PluginLog.Debug($"[moptargetof] {targetName}");
                await Task.CompletedTask;
            },

            ["moptargetclear"] = async (macroId, args, token) => {
                TargetManager.TargetClear();
                DalamudApi.PluginLog.Debug($"[moptargetclear]");
                await Task.CompletedTask;
            },

            ["moppetbarslot"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args) || !int.TryParse(args, out int slotIndex)) {
                    DalamudApi.PluginLog.Warning($"[moppetbarslot] invalid argument: \"{args}\"");
                    return;
                }

                var realSlotIndex = slotIndex - 1;
                var maxHotBarSlots = 15;
                if (realSlotIndex < 0 || realSlotIndex > maxHotBarSlots) {
                    DalamudApi.PluginLog.Warning($"[moppetbarslot] invalid slot {slotIndex}");
                    return;
                }

                HotbarManager.ExecutePetHotbarActionBySlotIndex((uint)realSlotIndex);
                DalamudApi.PluginLog.Debug($"[moppetbarslot] {slotIndex}");
                await Task.CompletedTask;
            },
        };
    }

    public void EnqueueMacroActions(string macroId, string[] actions, double delayBetweenActions) {
        bool hasLoop = actions.Any(a => a.Contains("/moploop", StringComparison.OrdinalIgnoreCase));
        if (hasLoop) {
            MacroLoopQueue.Enqueue((macroId, actions, delayBetweenActions));
            CurrentActionsLoopExecutionList.AddRange(actions);
            _ = ProcessLoopQueue();
        } else {
            MacroQueue.Enqueue((macroId, actions, delayBetweenActions));
            CurrentActionsExecutionList.AddRange(actions);
            _ = ProcessQueue();
        }
    }

    private async Task ProcessQueue() {
        if (_runningMacroQueue) return;
        _runningMacroQueue = true;

        try {
            while (MacroQueue.TryDequeue(out var item)) {
                if (_cancelTokenSource.IsCancellationRequested) break;
                await ExecuteMacroActions(item.macroId, item.actions, item.delay, _cancelTokenSource.Token);
            }
        } finally {
            this.ClearActionsExecutionList();
            _runningMacroQueue = false;
        }
    }

    private async Task ProcessLoopQueue() {
        if (_runningMacroLoopQueue) return;
        _runningMacroLoopQueue = true;

        try {
            while (MacroLoopQueue.TryDequeue(out var item)) {
                if (_cancelTokenSource.IsCancellationRequested) break;
                await ExecuteMacroLoopActions(item.macroId, item.actions, item.delay, _cancelTokenSource.Token);
            }
        } finally {
            this.ClearActionsLoopExecutionList();
            _runningMacroLoopQueue = false;
        }
    }

    private async Task ExecuteMacroActions(string macroId, string[] actions, double delayBetweenActions, CancellationToken token) {
        foreach (var action in actions) {
            if (token.IsCancellationRequested) break;

            CurrentActionExecutionIndex++;

            var match = Regex.Match(action, @"^\/(\w+)\s*(.*)$");
            bool handled = false;

            if (match.Success) {
                var command = match.Groups[1].Value;
                var args = match.Groups[2].Value.Trim();

                if (CustomMacroActionHandlers.TryGetValue(command, out var handlerFn)) {
                    await handlerFn(macroId, args, token);
                    handled = true;

                    var noGlobalDelayActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "moptarget",
                        "moptargetof",
                        "moptargetclear",
                        "mopwait",
                        "moploop",
                        "mopmacro",
                        "mopobjectquantity"
                    };

                    if (!noGlobalDelayActions.Contains(command) && (delayBetweenActions > 0.0)) {
                        var secondsRound = Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero);
                        var delayMs = TimeSpan.FromSeconds(secondsRound);
                        DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                        await Task.Delay(delayMs, token);
                    }
                }
            }

            if (!handled) {
                DalamudApi.PluginLog.Debug($"[Execute Action] {action}");

                // Chat.SendMessage(action);
                _ = DalamudApi.Framework.RunOnFrameworkThread(delegate {
                    Chat.SendMessage(action);
                });

                if (delayBetweenActions > 0.0) {
                    var secondsRound = Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero);
                    var delayMs = TimeSpan.FromSeconds(secondsRound);
                    DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                    await Task.Delay(delayMs, token);
                }
            }
        }
    }

    // TODO: refactor duplicated
    private async Task ExecuteMacroLoopActions(string macroId, string[] actions, double delayBetweenActions, CancellationToken token) {
        foreach (var action in actions) {
            if (token.IsCancellationRequested) break;

            CurrentActionLoopExecutionIndex++;

            var match = Regex.Match(action, @"^\/(\w+)\s*(.*)$");
            bool handled = false;

            if (match.Success) {
                var command = match.Groups[1].Value;
                var args = match.Groups[2].Value.Trim();

                if (CustomMacroActionHandlers.TryGetValue(command, out var handlerFn)) {
                    await handlerFn(macroId, args, token);
                    handled = true;

                    var noGlobalDelayActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "moptarget",
                        "moptargetof",
                        "moptargetclear",
                        "mopwait",
                        "moploop",
                        "mopmacro",
                        "mopobjectquantity"
                    };

                    if (!noGlobalDelayActions.Contains(command) && (delayBetweenActions > 0.0)) {
                        var secondsRound = Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero);
                        var delayMs = TimeSpan.FromSeconds(secondsRound);
                        DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                        await Task.Delay(delayMs, token);
                    }
                }
            }

            if (!handled) {
                DalamudApi.PluginLog.Debug($"[Execute Action] {action}");

                // Chat.SendMessage(action);
                _ = DalamudApi.Framework.RunOnFrameworkThread(delegate {
                    Chat.SendMessage(action);
                });

                if (delayBetweenActions > 0.0) {
                    var secondsRound = Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero);
                    var delayMs = TimeSpan.FromSeconds(secondsRound);
                    DalamudApi.PluginLog.Debug($"[Global Delay] {delayMs.TotalMinutes:00}:{delayMs.Seconds:00}.{delayMs.Milliseconds:00}");
                    await Task.Delay(delayMs, token);
                }
            }
        }
    }

    private string[] GetLocalPlayerMacroActions(string macroNameOrNumber) {
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrNumber);
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerCid = DalamudApi.ClientState.LocalContentId;
        var actions = macro.GetCidActions(playerCid);
        return actions;
    }

    public void ExecuteMacro(int macroIndex) {
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerCid = DalamudApi.ClientState.LocalContentId;
        var actions = macro.GetCidActions(playerCid);

        this.EnqueueMacroActions(macro.Name, actions, Plugin.Config.DelayBetweenActions);
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
        _cancelTokenSource.Cancel();
        _cancelTokenSource.Dispose();
        _cancelTokenSource = new CancellationTokenSource();

        MacroQueue.Clear();
        MacroLoopQueue.Clear();

        this.ClearActionsExecutionList();
        this.ClearActionsLoopExecutionList();
    }

    public void Dispose() {
        StopMacroQueueExecution();
    }
}
