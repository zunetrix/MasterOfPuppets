using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using TinyIpc.IO;
using TinyIpc.Messaging;

using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets.Ipc;

internal class IpcProvider : IDisposable
{
    private static readonly ConcurrentQueue<(string[] actions, int delayBetweenActions)> MacroQueue = new();
    private static bool _macroQueueRunning = false;
    private static CancellationTokenSource _macroQueueCancellationTokenSource = new();
    public static List<string> CurrentActionsExecutionList { get; private set; } = new();
    public static int CurrentActionExecutionIndex { get; private set; } = -1;

    private readonly bool _initFailed;
    private bool _messagesQueueRunning = true;
    private readonly TinyMessageBus MessageBus;
    private readonly ConcurrentQueue<(byte[] serialized, bool includeSelf)> MessageQueue = new();
    private readonly AutoResetEvent _autoResetEvent = new(false);
    private readonly Dictionary<IpcMessageType, Action<IpcMessage>> _ipcHandlers = new();
    private Plugin Plugin { get; }

    public IpcProvider(Plugin plugin)
    {
        Plugin = plugin;

        RegisterHandlersFromType(this.GetType(), this);

        try
        {
            const long maxFileSize = 1 << 24;
            MessageBus = new TinyMessageBus(new TinyMemoryMappedFile("MasterOfPuppets.Ipc", maxFileSize), true);
            MessageBus.MessageReceived += MessageBusMessageReceived;

            // _methodInfos = typeof(IpcHandler)
            //     .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            //     .Select(i => (i.GetCustomAttribute<IpcHandleAttribute>()?.MessageType, methodInfo: i))
            //     .Where(i => i.MessageType != null)
            //     .ToDictionary(i => (MessageType)i.MessageType,
            //         i => i.methodInfo.CreateDelegate<Action<IpcBroadcastMessage>>(null));

            var thread = new Thread(() =>
            {
                DalamudApi.PluginLog.Information($"IPC message queue worker thread started");
                while (_messagesQueueRunning)
                {
                    // DalamudApi.PluginLog.Debug($"Try dequeue message");
                    while (MessageQueue.TryDequeue(out var dequeue))
                    {
                        try
                        {
                            var message = dequeue.serialized;
                            var messageLength = message.Length;
                            // DalamudApi.PluginLog.Verbose($"Dequeue serialized. length: {Dalamud.Utility.Util.FormatBytes(messageLength)}");
                            if (messageLength > maxFileSize)
                            {
                                throw new InvalidOperationException($"Message size is too large! TinyIpc will crash when handling this, not gonna let it through. maxFileSize: {Dalamud.Utility.Util.FormatBytes(maxFileSize)}");
                            }

                            if (MessageBus.PublishAsync(message).Wait(5000))
                            {
                                DalamudApi.PluginLog.Verbose($"Message published");
                                if (dequeue.includeSelf) MessageBusMessageReceived(null, new TinyMessageReceivedEventArgs(message));
                            }
                            else
                            {
                                throw new TimeoutException("IPC didn't published in 5000 ms");
                            }
                        }
                        catch (Exception e)
                        {
                            DalamudApi.PluginLog.Warning(e, $"Error when try publishing IPC");
                        }
                    }

                    _autoResetEvent.WaitOne();
                }
                DalamudApi.PluginLog.Information($"IPC message queue worker thread ended");
            });

            thread.IsBackground = true;
            thread.Start();
        }
        catch (PlatformNotSupportedException e)
        {
            DalamudApi.PluginLog.Error(e, $"TinyIpc init failed. Unfortunately TinyIpc is not available on Linux. local ensemble sync will not function properly.");
            _initFailed = true;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"TinyIpc init failed. local ensemble sync will not function properly.");
            _initFailed = true;
        }
    }

    private void MessageBusMessageReceived(object sender, TinyMessageReceivedEventArgs e)
    {
        if (_initFailed) return;
        try
        {
            var sw = Stopwatch.StartNew();
            DalamudApi.PluginLog.Verbose($"Message received");
            var bytes = e.Message.ToArray<byte>().Decompress();
            // DalamudApi.PluginLog.Verbose($"message decompressed in {sw.Elapsed.TotalMilliseconds}ms");
            var message = bytes.ProtoDeserialize<IpcMessage>();
            // DalamudApi.PluginLog.Verbose($"proto deserialized in {sw.Elapsed.TotalMilliseconds}ms");
            // DalamudApi.PluginLog.Debug(message.ToString());
            ProcessMessage(message);
        }
        catch (Exception exception)
        {
            DalamudApi.PluginLog.Error(exception, "error when processing received message");
        }
    }

    private void ProcessMessage(IpcMessage message)
    {
        if (!Plugin.Config.SyncClients) return;

        if (_ipcHandlers.TryGetValue(message.MessageType, out var handler))
            handler(message);
        else
            DalamudApi.PluginLog.Warning($"No handler for {message.MessageType}");
    }

    private void RegisterHandlersFromType(Type type, object instance)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<IpcHandleAttribute>();
            if (attr == null) continue;
            var delegateAction = (Action<IpcMessage>)Delegate.CreateDelegate(typeof(Action<IpcMessage>), instance, method);
            _ipcHandlers[attr.MessageType] = delegateAction;
        }
    }

    public void BroadCast(byte[] serialized, bool includeSelf = false)
    {
        if (_initFailed) return;
        if (!Plugin.Config.SyncClients) return;

        try
        {
            // DalamudApi.PluginLog.Verbose($"queuing message. length: {Dalamud.Utility.Util.FormatBytes(serialized.Length)}" + (includeSelf ? " includeSelf" : null));
            MessageQueue.Enqueue(new(serialized, includeSelf));
            _autoResetEvent.Set();
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Warning(e, "error when queuing message");
        }
    }

    private void ReleaseUnmanagedResources(bool disposing)
    {
        try
        {
            StopMacroQueueExecution();
            _messagesQueueRunning = false;
            MessageBus.MessageReceived -= MessageBusMessageReceived;

            if (_initFailed) return;
            _autoResetEvent?.Set();
            _autoResetEvent?.Dispose();
        }
        finally
        {
            // RPCResponse = delegate { };
        }

        if (disposing)
        {
            GC.SuppressFinalize(this);
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources(true);
    }

    ~IpcProvider()
    {
        ReleaseUnmanagedResources(false);
    }

    public void SyncConfiguration()
    {
        Plugin.Config.Save();
        var message = IpcMessage.Create(IpcMessageType.SyncConfiguration, Plugin.Config.JsonSerialize(), Plugin.Config.SaveConfigAfterSync.ToString()).Serialize();
        BroadCast(message, includeSelf: false);
    }

    [IpcHandle(IpcMessageType.SyncConfiguration)]
    private void HandleSyncConfiguration(IpcMessage message)
    {
        var cofigurationString = message.StringData[0];
        bool saveConfigAfterSync = bool.TryParse(message.StringData[1], out var temp) ? temp : false;
        // Plugin.Config = pluginConfig;
        Plugin.Config.UpdateFromJson(cofigurationString);

        if (saveConfigAfterSync)
        {
            Plugin.Config.Save();
        }

        DalamudApi.PluginLog.Debug("SyncConfiguration");
    }

    public void ExecuteTextCommand(string text)
    {
        var message = IpcMessage.Create(IpcMessageType.ExecuteTextCommand, text).Serialize();
        BroadCast(message, includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteTextCommand)]
    private void HandleExecuteTextCommand(IpcMessage message)
    {
        var textCommand = message.StringData[0];

        DalamudApi.Framework.RunOnTick(delegate
        {
            Chat.SendMessage($"{textCommand}");
        });
    }

    public void ExecuteActionCommand(uint actionId)
    {
        var message = IpcMessage.Create(IpcMessageType.ExecuteActionCommand, actionId).Serialize();
        BroadCast(message, includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteActionCommand)]
    private void HandleExecuteActionCommand(IpcMessage message)
    {
        var actionId = message.DataStruct<uint>();
        GameActionManager.UseActionById(actionId);
    }

    public void ExecuteItemCommand(uint itemId)
    {
        var message = IpcMessage.Create(IpcMessageType.ExecuteItemCommand, itemId).Serialize();
        BroadCast(message, includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteItemCommand)]
    private void HandleExecuteItemCommand(IpcMessage message)
    {
        var actionId = message.DataStruct<uint>();
        GameActionManager.UseItemById(actionId);
    }

    public void StopMacroExecution()
    {
        var message = IpcMessage.Create(IpcMessageType.StopMacroExecution).Serialize();
        BroadCast(message, includeSelf: true);
    }

    [IpcHandle(IpcMessageType.StopMacroExecution)]
    private void HandleStopMacroExecution(IpcMessage message)
    {
        DalamudApi.PluginLog.Debug("Stop Macro Execution");
        StopMacroQueueExecution();
    }

    public void RunMacro(int macroIndex, bool includeSelf = true)
    {
        DalamudApi.PluginLog.Debug($"RunMacro {macroIndex}");
        // var macroJson = macroData.JsonSerialize();
        var message = IpcMessage.Create(IpcMessageType.RunMacro, macroIndex).Serialize();
        BroadCast(message, includeSelf);
    }

    [IpcHandle(IpcMessageType.RunMacro)]
    private void HandleRunMacro(IpcMessage message)
    {
        var macroIndex = message.DataStruct<int>();
        var isValidMacroIndex = macroIndex >= 0 && macroIndex < Plugin.Config.Macros.Count;

        if (!isValidMacroIndex)
        {
            DalamudApi.PluginLog.Debug("Invalid Macro index");
            DalamudApi.ShowNotification($"Invalid Macro index", NotificationType.Error, 5000);
            return;
        }

        var macro = Plugin.Config.Macros[macroIndex];
        var playerCid = DalamudApi.ClientState.LocalContentId;


        if (macro.Commands == null || macro.Commands.Count == 0) return;
        var playerActions = macro.Commands.FirstOrDefault(command => command.Cids.Any(cid => cid == playerCid))?.Actions;
        if (playerActions == null)
        {
            DalamudApi.PluginLog.Debug($"No actions for character");
            return;
        }

        string[] actionList = playerActions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // var chatText = $"{str}";
        // Chat.SendMessage(chatText);
        // DalamudApi.ChatGui.Print(chatText);
        EnqueueMacroActions(actionList, Plugin.Config.DelayBetweenActions);
    }

    private static readonly Dictionary<string, Func<string, CancellationToken, Task>> CustomMacroActionsHandlers =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["wait"] = async (args, token) =>
        {
            if (string.IsNullOrWhiteSpace(args) || !int.TryParse(args, out int seconds))
            {
                DalamudApi.PluginLog.Warning($"[WAIT] invalid argument: \"{args}\"");
                return;
            }

            DalamudApi.PluginLog.Debug($"[WAIT] {seconds}...");
            await Task.Delay(seconds * 1000, token);
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

        ["item"] = async (args, token) =>
        {
            args = args.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(args))
            {
                DalamudApi.PluginLog.Warning($"[ITEM] invalid argument: \"{args}\"");
                return;
            }

            if (uint.TryParse(args, out uint actionId))
            {
                GameActionManager.UseItemById(actionId);
                DalamudApi.PluginLog.Debug($"[ITEM] (by Id) {actionId}");
            }
            else
            {
                GameActionManager.UseItemByName(args);
                DalamudApi.PluginLog.Debug($"[ITEM] (by Name) {args}");
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

            var maxHotBarSlots = 15;
            var realSlotIndex = slotIndex - 1;
            if (realSlotIndex < 0 || realSlotIndex > maxHotBarSlots)
            {
                DalamudApi.PluginLog.Debug($"[PETBARSLOT] invalid slot number {args}");
                return;
            }

            HotbarManager.ExecutePetHotbarActionBySlotIndex((uint)realSlotIndex);
            DalamudApi.PluginLog.Debug($"[PETBARSLOT] {args}");
            await Task.CompletedTask;
        },
    };

    private static async Task ExecuteActions(string[] actions, CancellationToken token, int delayBetweenActions = 2)
    {
        foreach (var action in actions)
        {
            CurrentActionExecutionIndex++;

            if (token.IsCancellationRequested)
                break;

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

                    if (handled)
                    {
                        DalamudApi.PluginLog.Debug($"[DELAY BETWEEN ACTIONS] {delayBetweenActions}...");
                        await Task.Delay(delayBetweenActions * 1000, token);
                    }
                }
            }

            if (!handled)
            {
                // DalamudApi.Framework.RunOnTick(delegate
                // {
                //     Chat.SendMessage($"{action}");
                // }, default(TimeSpan), 0, default(System.Threading.CancellationToken));

                // DalamudApi.Framework.RunOnFrameworkThread(delegate
                // {
                //     Chat.SendMessage($"{action}");
                // });

                DalamudApi.PluginLog.Debug($"[Execute Action] {action}");
                Chat.SendMessage($"{action}");
                DalamudApi.PluginLog.Debug($"[DELAY BETWEEN ACTIONS] {delayBetweenActions}...");
                await Task.Delay(delayBetweenActions * 1000, token);
            }
        }
    }

    public static void EnqueueMacroActions(string[] actions, int delayBetweenActions)
    {
        MacroQueue.Enqueue((actions, delayBetweenActions));
        CurrentActionsExecutionList.AddRange(actions);
        _ = ProcessMacroQueue();
    }

    private static async Task ProcessMacroQueue()
    {
        if (_macroQueueRunning) return;

        _macroQueueRunning = true;
        var cancellationTokenSource = _macroQueueCancellationTokenSource;

        try
        {
            while (MacroQueue.TryDequeue(out var item))
            {
                var (actions, delayBetweenActions) = item;
                await ExecuteActions(actions, cancellationTokenSource.Token, delayBetweenActions);

                if (_macroQueueCancellationTokenSource.IsCancellationRequested)
                    break;
            }
        }
        finally
        {
            CurrentActionsExecutionList.Clear();
            CurrentActionExecutionIndex = -1;
            _macroQueueRunning = false;
        }
    }

    private static void StopMacroQueueExecution()
    {
        _macroQueueCancellationTokenSource.Cancel();
        _macroQueueCancellationTokenSource.Dispose();
        _macroQueueCancellationTokenSource = new CancellationTokenSource();

        MacroQueue.Clear();
        // while (MacroQueue.TryDequeue(out _)) { }
    }
}
