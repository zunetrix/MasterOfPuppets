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

namespace MasterOfPuppets.Ipc;

internal class IpcProvider : IDisposable
{

    private static readonly ConcurrentQueue<(string[] actions, int delayBetweenActions)> MacroQueue = new();
    private static bool _macroQueueRunning = false;

    private readonly bool initFailed;
    private bool _messagesQueueRunning = true;
    private readonly TinyMessageBus MessageBus;
    private readonly ConcurrentQueue<(byte[] serialized, bool includeSelf)> MessageQueue = new();
    private readonly AutoResetEvent _autoResetEvent = new(false);
    private readonly Dictionary<IpcMessageType, Action<IpcMessage>> _ipcHandlers = new();
    private Plugin Plugin { get; }

    internal IpcProvider(Plugin plugin)
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
            initFailed = true;
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"TinyIpc init failed. local ensemble sync will not function properly.");
            initFailed = true;
        }
    }

    private void MessageBusMessageReceived(object sender, TinyMessageReceivedEventArgs e)
    {
        if (initFailed) return;
        try
        {
            var sw = Stopwatch.StartNew();
            DalamudApi.PluginLog.Verbose($"message received");
            var bytes = e.Message.ToArray<byte>().Decompress();
            // DalamudApi.PluginLog.Verbose($"message decompressed in {sw.Elapsed.TotalMilliseconds}ms");
            var message = bytes.ProtoDeserialize<IpcMessage>();
            // DalamudApi.PluginLog.Verbose($"proto deserialized in {sw.Elapsed.TotalMilliseconds}ms");
            DalamudApi.PluginLog.Debug(message.ToString());
            ProcessMessage(message);
        }
        catch (Exception exception)
        {
            DalamudApi.PluginLog.Error(exception, "error when processing received message");
        }
    }

    private void ProcessMessage(IpcMessage message)
    {
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
        if (initFailed) return;
        // if (!Plugin.Config.SyncClients) return;
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
            _messagesQueueRunning = false;
            MessageBus.MessageReceived -= MessageBusMessageReceived;
            if (initFailed) return;
            _autoResetEvent?.Set();
            _autoResetEvent?.Dispose();
        }
        finally
        {
            //RPCResponse = delegate { };
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
        var message = IpcMessage.Create(IpcMessageType.SyncConfiguration, Plugin.Config.JsonSerialize()).Serialize();
        BroadCast(message, includeSelf: false);
    }

    [IpcHandle(IpcMessageType.SyncConfiguration)]
    private void HandleSyncConfiguration(IpcMessage message)
    {
        var str = message.StringData[0];
        var pluginConfig = str.JsonDeserialize<Configuration>();
        Plugin.Config = pluginConfig;

        DalamudApi.PluginLog.Debug("SyncConfiguration");
    }

    public void RunMacro(int macroIndex, bool includeSelf = true)
    {
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
            DalamudApi.ShowNotification($"Invalid Macro number", NotificationType.Error, 5000);
            return;
        }

        var macro = Plugin.Config.Macros[macroIndex];
        var playerCid = DalamudApi.ClientState.LocalContentId;


        if (macro.Commands == null || macro.Commands.Count == 0) return;
        var playerActions = macro.Commands.FirstOrDefault(c => c.Characters.Any(ch => ch.Cid == playerCid))?.Actions;
        if (playerActions == null)
        {
            DalamudApi.PluginLog.Debug($"No Actions for CID ({playerCid})");
            return;
        }

        string[] actionList = playerActions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // var chatText = $"{str}";
        // Chat.SendMessage(chatText);
        // DalamudApi.ChatGui.Print(chatText);
        EnqueueActions(actionList, Plugin.Config.DelayBetweenActions);
    }

    static async Task ExecuteActions(string[] actions, int delayBetweenActions = 1)
    {
        foreach (var action in actions)
        {
            var match = Regex.Match(action, @"^/wait\s*(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int seconds))
                {
                    DalamudApi.PluginLog.Debug($"[WAIT] {seconds}...");
                    await Task.Delay(seconds * 1000);
                }
            }
            else
            {
                Chat.SendMessage($"{action}");
                DalamudApi.PluginLog.Debug($"[ExecuteAction] {action}");
                DalamudApi.PluginLog.Debug($"[WAIT] {delayBetweenActions}...");
                await Task.Delay(delayBetweenActions * 1000);
            }
        }
    }

    public static void EnqueueActions(string[] actions, int delayBetweenActions)
    {
        MacroQueue.Enqueue((actions, delayBetweenActions));
        _ = ProcessQueue();
    }

    private static async Task ProcessQueue()
    {
        if (_macroQueueRunning) return;

        _macroQueueRunning = true;

        try
        {
            while (MacroQueue.TryDequeue(out var item))
            {
                var (actions, delayBetweenActions) = item;
                await ExecuteActions(actions, delayBetweenActions);
            }
        }
        finally
        {
            _macroQueueRunning = false;
        }
    }

}
