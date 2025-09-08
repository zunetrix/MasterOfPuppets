using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.Ipc;

internal static class MacroQueueExecutor
{
    private static readonly ConcurrentQueue<(string[] actions, double delay)> MacroQueue = new();
    private static bool _running = false;
    private static CancellationTokenSource _cts = new();

    public static List<string> CurrentActionsExecutionList { get; private set; } = new();
    public static int CurrentActionExecutionIndex { get; private set; } = -1;

    private static readonly Dictionary<string, Func<string, CancellationToken, Task>> CustomMacroActionsHandlers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["wait"] = async (args, token) =>
            {
                if (string.IsNullOrWhiteSpace(args) || !double.TryParse(args, out double seconds))
                {
                    DalamudApi.PluginLog.Warning($"[WAIT] invalid argument: \"{args}\"");
                    return;
                }

                int delayMs = (int)(seconds * 1000);
                DalamudApi.PluginLog.Debug($"[WAIT] {delayMs}...");
                await Task.Delay(delayMs, token);
            },

            ["mopaction"] = async (args, token) =>
            {
                args = args.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(args))
                {
                    DalamudApi.PluginLog.Warning($"[MOPACTION] invalid argument: \"{args}\"");
                    return;
                }

                if (uint.TryParse(args, out uint actionId))
                {
                    GameActionManager.UseActionById(actionId);
                    DalamudApi.PluginLog.Debug($"[MOPACTION] (by Id) {actionId}");
                }
                else
                {
                    GameActionManager.UseActionByName(args);
                    DalamudApi.PluginLog.Debug($"[MOPACTION] (by Name) {args}");
                }

                await Task.CompletedTask;
            },

            ["moptarget"] = async (args, token) =>
            {
                string targetName = args.Trim().Trim('"');
                TargetManager.TargetByName(targetName);
                await Task.CompletedTask;
            },

            ["moptargetof"] = async (args, token) =>
            {
                string targetName = args.Trim().Trim('"');
                TargetManager.TargetOf(targetName);
                await Task.CompletedTask;
            },

            ["moptargetclear"] = async (_, token) =>
            {
                TargetManager.TargetClear();
                await Task.CompletedTask;
            },

            ["mopitem"] = async (args, token) =>
            {
                args = args.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(args))
                {
                    DalamudApi.PluginLog.Warning($"[MOPITEM] invalid argument: \"{args}\"");
                    return;
                }

                if (uint.TryParse(args, out uint itemId))
                {
                    GameActionManager.UseItemById(itemId);
                    DalamudApi.PluginLog.Debug($"[MOPITEM] (by Id) {itemId}");
                }
                else
                {
                    GameActionManager.UseItemByName(args);
                    DalamudApi.PluginLog.Debug($"[MOPITEM] (by Name) {args}");
                }

                await Task.CompletedTask;
            },

            ["petbarslot"] = async (args, token) =>
            {
                if (string.IsNullOrWhiteSpace(args) || !int.TryParse(args, out int slotIndex))
                {
                    DalamudApi.PluginLog.Warning($"[PETBARSLOT] invalid argument: \"{args}\"");
                    return;
                }

                var realSlotIndex = slotIndex - 1;
                var maxHotBarSlots = 15;
                if (realSlotIndex < 0 || realSlotIndex > maxHotBarSlots)
                {
                    DalamudApi.PluginLog.Warning($"[PETBARSLOT] invalid slot {slotIndex}");
                    return;
                }

                HotbarManager.ExecutePetHotbarActionBySlotIndex((uint)realSlotIndex);
                DalamudApi.PluginLog.Debug($"[PETBARSLOT] {slotIndex}");
                await Task.CompletedTask;
            },
        };

    public static void EnqueueMacroActions(string[] actions, double delayBetweenActions)
    {
        MacroQueue.Enqueue((actions, delayBetweenActions));
        CurrentActionsExecutionList.AddRange(actions);
        _ = ProcessQueue();
    }

    private static async Task ProcessQueue()
    {
        if (_running) return;
        _running = true;

        try
        {
            while (MacroQueue.TryDequeue(out var item))
            {
                await ExecuteActions(item.actions, _cts.Token, item.delay);
                if (_cts.IsCancellationRequested) break;
            }
        }
        finally
        {
            CurrentActionsExecutionList.Clear();
            CurrentActionExecutionIndex = -1;
            _running = false;
        }
    }

    private static async Task ExecuteActions(string[] actions, CancellationToken token, double delayBetweenActions)
    {
        foreach (var action in actions)
        {
            CurrentActionExecutionIndex++;
            if (token.IsCancellationRequested) break;

            var match = Regex.Match(action, @"^\/(\w+)\s*(.*)$");
            bool handled = false;

            if (match.Success)
            {
                var command = match.Groups[1].Value;
                var args = match.Groups[2].Value.Trim();

                if (CustomMacroActionsHandlers.TryGetValue(command, out var handler))
                {
                    await handler(args, token);
                    handled = true;

                    var noDelayActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "moptarget",
                        "moptargetof",
                        "moptargetclear",
                    };

                    if (!noDelayActions.Contains(action))
                    {
                        int delayMs = (int)(delayBetweenActions * 1000);
                        DalamudApi.PluginLog.Debug($"[DELAY BETWEEN ACTIONS] {delayMs}...");
                        await Task.Delay(delayMs, token);
                    }
                }
            }

            if (!handled)
            {
                DalamudApi.PluginLog.Debug($"[Execute Action] {action}");

                Chat.SendMessage(action);

                // DalamudApi.Framework.RunOnFrameworkThread(delegate
                // {
                //     Chat.SendMessage(action);
                // });

                int delayMs = (int)(delayBetweenActions * 1000);
                DalamudApi.PluginLog.Debug($"[DELAY BETWEEN ACTIONS] {delayMs}...");
                await Task.Delay(delayMs, token);
            }
        }
    }

    public static void StopMacroQueueExecution()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        MacroQueue.Clear();
    }
}
