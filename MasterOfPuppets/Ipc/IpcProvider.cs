using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using MasterOfPuppets.Util;

using TinyIpc.IO;
using TinyIpc.Messaging;

namespace MasterOfPuppets.Ipc;

internal class IpcProvider : IDisposable {
    private readonly TinyMessageBus MessageBus;
    private readonly ConcurrentQueue<(byte[] serialized, bool includeSelf)> MessageQueue = new();
    private readonly AutoResetEvent _autoResetEvent = new(false);
    private readonly Dictionary<IpcMessageType, Action<IpcMessage>> _ipcHandlers = new();
    private readonly bool _initFailed;
    private bool _messagesQueueRunning = true;

    private Plugin Plugin { get; }

    public IpcProvider(Plugin plugin) {
        Plugin = plugin;

        RegisterHandlersFromType(typeof(IpcHandlers), new IpcHandlers(plugin));

        try {
            const long maxFileSize = 1 << 24;
            MessageBus = new TinyMessageBus(new TinyMemoryMappedFile("MasterOfPuppets.IPC", maxFileSize), true);
            MessageBus.MessageReceived += OnMessageReceived;

            var thread = new Thread(ProcessMessageQueue) { IsBackground = true };
            thread.Start();
        }
        catch (Exception e) {
            DalamudApi.PluginLog.Error(e, "TinyIpc init failed.");
            _initFailed = true;
        }
    }

    private void RegisterHandlersFromType(Type type, object instance) {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            var attr = method.GetCustomAttribute<IpcHandleAttribute>();
            if (attr == null) continue;
            var del = (Action<IpcMessage>)Delegate.CreateDelegate(typeof(Action<IpcMessage>), instance, method);
            _ipcHandlers[attr.MessageType] = del;
        }
    }

    private void OnMessageReceived(object sender, TinyMessageReceivedEventArgs e) {
        if (_initFailed) return;
        try {
            var message = e.Message.ToArray<byte>().Decompress().ProtoDeserialize<IpcMessage>();
            if (_ipcHandlers.TryGetValue(message.MessageType, out var handler))
                handler(message);
            else
                DalamudApi.PluginLog.Warning($"No handler for {message.MessageType}");
        }
        catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Error processing IPC message");
        }
    }

    private void ProcessMessageQueue() {
        DalamudApi.PluginLog.Information("IPC message queue worker started");
        while (_messagesQueueRunning) {
            while (MessageQueue.TryDequeue(out var dequeue)) {
                try {
                    if (MessageBus.PublishAsync(dequeue.serialized).Wait(5000) && dequeue.includeSelf)
                        OnMessageReceived(null, new TinyMessageReceivedEventArgs(dequeue.serialized));
                }
                catch (Exception e) {
                    DalamudApi.PluginLog.Warning(e, "Error publishing IPC");
                }
            }
            _autoResetEvent.WaitOne();
        }
        DalamudApi.PluginLog.Information("IPC message queue worker ended");
    }

    public void BroadCast(byte[] serialized, bool includeSelf = false) {
        if (_initFailed || !Plugin.Config.SyncClients) return;

        MessageQueue.Enqueue(new(serialized, includeSelf));
        _autoResetEvent.Set();
    }

    public void Dispose() {
        _messagesQueueRunning = false;
        _autoResetEvent.Set();
        _autoResetEvent.Dispose();
        MessageBus.MessageReceived -= OnMessageReceived;
    }

    public void SyncConfiguration() {
        Plugin.Config.Save();
        var message = IpcMessage.Create(IpcMessageType.SyncConfiguration, Plugin.Config.JsonSerialize(), Plugin.Config.SaveConfigAfterSync.ToString()).Serialize();
        BroadCast(message, includeSelf: false);
    }

    public void ExecuteTextCommand(string text) {
        var message = IpcMessage.Create(IpcMessageType.ExecuteTextCommand, text).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void ExecuteActionCommand(uint actionId) {
        var message = IpcMessage.Create(IpcMessageType.ExecuteActionCommand, actionId).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void ExecuteItemCommand(uint itemId) {
        var message = IpcMessage.Create(IpcMessageType.ExecuteItemCommand, itemId).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void ExecuteTargetMyTarget() {
        if (DalamudApi.ClientState.LocalPlayer == null) return;
        // string asssistCharacterName = DalamudApi.ClientState.LocalPlayer.Name.TextValue;
        var assitTargetObjectId = DalamudApi.ClientState.LocalPlayer.TargetObjectId;
        if (assitTargetObjectId == 0) return;

        var message = IpcMessage.Create(IpcMessageType.ExecuteTargetMyTarget, assitTargetObjectId).Serialize();
        BroadCast(message, includeSelf: false);
    }

    public void ExecuteTargetClear() {
        var message = IpcMessage.Create(IpcMessageType.ExecuteTargetClear).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void SetGameSettingsObjectQuantity(SettingsDisplayObjectLimitType displayObjectLimitType) {
        var message = IpcMessage.Create(IpcMessageType.SetGameSettingsObjectQuantity, displayObjectLimitType).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void StopMacroExecution() {
        var message = IpcMessage.Create(IpcMessageType.StopMacroExecution).Serialize();
        BroadCast(message, includeSelf: true);
    }

    public void RunMacro(int macroIndex, bool includeSelf = true) {
        DalamudApi.PluginLog.Debug($"[Run Macro] {macroIndex}");
        var message = IpcMessage.Create(IpcMessageType.RunMacro, macroIndex).Serialize();
        BroadCast(message, includeSelf);
    }
}
