using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public class MacroHandler : IDisposable {
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

    private static readonly HashSet<string> NoGlobalDelayCommands = new(StringComparer.OrdinalIgnoreCase) {
        "moptarget",
        "moptargetof",
        "moptargetclear",
        "moptargetmyminion",
        "mopwait",
        "moploop",
        "mopmacro",
        "mopobjectquantity",
        "mopenablewalk",
        "mopdisablewalk",
        "moptogglewalk"
    };

    private readonly Dictionary<string, Func<string, string, CancellationToken, Task>> CustomMacroActionHandlers;

    public MacroHandler(Plugin plugin) {
        Plugin = plugin;

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
                    GameActionManager.UseAction(actionId);
                    DalamudApi.PluginLog.Debug($"[mopaction] {actionId}");
                } else {
                    GameActionManager.UseAction(actionIdOrName);
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
                    GameActionManager.UseItem(itemId);
                    DalamudApi.PluginLog.Debug($"[mopitem] {itemId}");
                } else {
                    GameActionManager.UseItem(itemIdOrName);
                    DalamudApi.PluginLog.Debug($"[mopitem] {itemIdOrName}");
                }

                await Task.CompletedTask;
            },

            ["moptargetmyminion"] = async (macroId, args, token) => {
                GameTargetManager.TargetMyMinion();
                DalamudApi.PluginLog.Debug($"[moptargetmyminion]");
                await Task.CompletedTask;
            },

            ["moptarget"] = async (macroId, args, token) => {
                string targetName = args.Trim().Trim('"');
                GameTargetManager.TargetObject(targetName);
                DalamudApi.PluginLog.Debug($"[moptarget] {targetName}");
                await Task.CompletedTask;
            },

            ["moptargetof"] = async (macroId, args, token) => {
                string targetName = args.Trim().Trim('"');
                GameTargetManager.TargetOf(targetName);
                DalamudApi.PluginLog.Debug($"[moptargetof] {targetName}");
                await Task.CompletedTask;
            },

            ["moptargetclear"] = async (macroId, args, token) => {
                GameTargetManager.TargetClear();
                DalamudApi.PluginLog.Debug($"[moptargetclear]");
                await Task.CompletedTask;
            },

            ["moppetbarslot"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args) || !int.TryParse(args, out int slotIndex)) {
                    DalamudApi.PluginLog.Warning($"[moppetbarslot] invalid argument: \"{args}\"");
                    return;
                }

                HotbarManager.ExecutePetHotbarActionByIndex((uint)(slotIndex - 1));
                DalamudApi.PluginLog.Debug($"[moppetbarslot] {slotIndex}");
                await Task.CompletedTask;
            },

            ["mophotbar"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args)) {
                    DalamudApi.PluginLog.Warning($"[mophotbar] missing arguments");
                    return;
                }

                var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2) {
                    DalamudApi.PluginLog.Warning($"[mophotbar] expected 2 arguments, got {parts.Length}: \"{args}\"");
                    return;
                }

                if (!int.TryParse(parts[0], out int hotbarIndex) || !int.TryParse(parts[1], out int slotIndex)) {
                    DalamudApi.PluginLog.Warning($"[mophotbar] invalid numbers: \"{args}\"");
                    return;
                }

                int realHotbarIndex = hotbarIndex - 1;
                int realSlotIndex = slotIndex - 1;

                if (realHotbarIndex < 0 || realSlotIndex < 0) {
                    DalamudApi.PluginLog.Warning($"[mophotbar] invalid index (must be >= 1): \"{args}\"");
                    return;
                }

                HotbarManager.ExecuteHotbarActionByIndex((uint)realHotbarIndex, (uint)realSlotIndex);
                DalamudApi.PluginLog.Debug($"[mophotbar] {realHotbarIndex} {realSlotIndex}");
                await Task.CompletedTask;
            },

            ["mophotbaremote"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args)) {
                    DalamudApi.PluginLog.Warning($"[mophotbaremote] missing arguments");
                    return;
                }

                if (!int.TryParse(args, out int actionId)) {
                    DalamudApi.PluginLog.Warning($"[mophotbaremote] invalid numbers: \"{args}\"");
                    return;
                }

                HotbarManager.ExecuteHotbarEmoteAction((uint)actionId);
                DalamudApi.PluginLog.Debug($"[mophotbaremote] {actionId}");
                await Task.CompletedTask;
            },

            ["mopmove"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args)) return;
                var parts = ArgumentParser.ParseMacroArgs(args);
                if (parts.Count != 3) return;

                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
                    DalamudApi.PluginLog.Warning($"[mopmove] invalid argument parse: \"{args}\"");
                    return;
                }

                Plugin.MovementManager.MoveToPosition(new Vector3(x, y, z));
                DalamudApi.PluginLog.Debug($"[mopmove] {x}, {y}, {z}");
                await Task.CompletedTask;
            },

            ["mopmoverelativeto"] = async (macroId, args, token) => {
                var parts = ArgumentParser.ParseMacroArgs(args);
                if (parts.Count != 4) return;
                string relativeCharacterName = parts[3];

                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
                    DalamudApi.PluginLog.Warning($"[mopmoverelativeto] invalid argument parse: \"{args}\"");
                    return;
                }

                Plugin.MovementManager.MoveToPositionRelative(new Vector3(x, y, z), relativeCharacterName);
                DalamudApi.PluginLog.Debug($"[mopmoverelativeto] {x}, {y}, {z} ({relativeCharacterName})");
                await Task.CompletedTask;
            },

            ["mopmovetotarget"] = async (macroId, args, token) => {
                Plugin.MovementManager.MoveToTargetPosition();
                DalamudApi.PluginLog.Debug($"[mopmovetotarget]");
                await Task.CompletedTask;
            },

            ["mopmovetocharacter"] = async (macroId, args, token) => {
                if (string.IsNullOrWhiteSpace(args)) {
                    DalamudApi.PluginLog.Warning($"Invalid arguments expected character name");
                    return;
                }

                Plugin.MovementManager.MoveToObject(args.Replace("\"", ""));
                DalamudApi.PluginLog.Debug($"[mopmovetocharacter] {args}");
                await Task.CompletedTask;
            },

            ["mopstopmove"] = async (macroId, args, token) => {
                Plugin.MovementManager.StopMove();
                DalamudApi.PluginLog.Debug($"[mopstopmove] {args}");
                await Task.CompletedTask;
            },

            ["mopenablewalk"] = async (macroId, args, token) => {
                Plugin.MovementManager.SetWalking(true);
                DalamudApi.PluginLog.Debug($"[mopenablewalk]");
                await Task.CompletedTask;
            },

            ["mopdisablewalk"] = async (macroId, args, token) => {
                Plugin.MovementManager.SetWalking(false);
                DalamudApi.PluginLog.Debug($"[mopdisablewalk]");
                await Task.CompletedTask;
            },

            ["moptogglewalk"] = async (macroId, args, token) => {
                Plugin.MovementManager.ToggleWalking();
                DalamudApi.PluginLog.Debug($"[moptogglewalk]");
                await Task.CompletedTask;
            },
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

            var match = Regex.Match(action, @"^\/(\w+)\s*(.*)$");
            bool handled = false;

            if (match.Success) {
                var command = match.Groups[1].Value;
                var args = match.Groups[2].Value.Trim();

                if (CustomMacroActionHandlers.TryGetValue(command, out var handlerFn)) {
                    await handlerFn(macroId, args, token);
                    handled = true;

                    if (!NoGlobalDelayCommands.Contains(command) && delayBetweenActions > 0.0) {
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
