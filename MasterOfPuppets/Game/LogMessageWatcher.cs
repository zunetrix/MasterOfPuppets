using System;
using System.Linq;

using Dalamud.Game.Chat;

namespace MasterOfPuppets;

internal class ChatLogMessageWatcher : IDisposable {
    private Plugin Plugin { get; }

    public ChatLogMessageWatcher(Plugin plugin) {
        Plugin = plugin;

        DalamudApi.ChatGui.LogMessage += ChatOnLogMessage;
    }

    public void Dispose() {
        DalamudApi.ChatGui.LogMessage -= ChatOnLogMessage;
    }

    private void ChatOnLogMessage(ILogMessage message) {
#if DEBUG
        DalamudApi.PluginLog.Warning($"LogMessageId: {message.LogMessageId}");
        DalamudApi.PluginLog.Debug($"SourceEntity: {message.SourceEntity}");
        DalamudApi.PluginLog.Debug($"TargetEntity: {message.TargetEntity}");
        DalamudApi.PluginLog.Debug($"ParameterCount: {message.ParameterCount}");
        DalamudApi.PluginLog.Debug($"GameData.RowId: {message.GameData.RowId}");
        DalamudApi.PluginLog.Debug($"GameData.Value.LogKind: {message.GameData.Value.LogKind}");
        DalamudApi.PluginLog.Debug($"GameData.Value.Text: {message.GameData.Value.Text}");
        DalamudApi.PluginLog.Debug($"ParameterCount: {message.ParameterCount}");
        DalamudApi.PluginLog.Debug($"Parameters: {string.Join(", ", message.Parameters.Select(x => $"{x.StringValue}"))}");
#endif
    }
}
