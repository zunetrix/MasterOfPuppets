using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.RemoteControl;

internal sealed record WsClientInfo(Guid Id, WebSocket Socket, DateTime ConnectedAt, string IpAddress, string UserAgent);

/// <summary>
/// Tracks all active WebSocket connections, fans out events to all clients,
/// and reads inbound messages dispatching them via <see cref="RemoteCommandDispatcher"/>.
/// </summary>
internal sealed class RemoteControlWebSocketManager : IDisposable {
    private readonly ConcurrentDictionary<Guid, WsClientInfo> _clients = new();
    private readonly RemoteCommandDispatcher _dispatcher;
    private readonly CancellationTokenSource _cts = new();

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public int ConnectedClients => _clients.Count;
    public System.Collections.Generic.IEnumerable<WsClientInfo> ActiveClients => _clients.Values;

    public RemoteControlWebSocketManager(RemoteCommandDispatcher dispatcher) {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Called when the HTTP server upgrades a connection to WebSocket.
    /// Blocks until the client disconnects.
    /// </summary>
    public async Task HandleClientAsync(WebSocket ws, string ipAddress, string userAgent) {
        var id = Guid.NewGuid();
        var clientInfo = new WsClientInfo(id, ws, DateTime.Now, ipAddress, userAgent);
        _clients[id] = clientInfo;
        DalamudApi.PluginLog.Debug($"[WS] Client connected: {id} from {ipAddress} (total: {_clients.Count})");

        try {
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested) {
                WebSocketReceiveResult result;
                try {
                    result = await ws.ReceiveAsync(buffer, _cts.Token);
                } catch (OperationCanceledException) {
                    break;
                } catch (WebSocketException) {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text) {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessIncomingAsync(ws, json);
                }
            }
        } finally {
            _clients.TryRemove(id, out _);
            DalamudApi.PluginLog.Debug($"[WS] Client disconnected: {id}  (total: {_clients.Count})");
        }
    }

    private async Task ProcessIncomingAsync(WebSocket ws, string json) {
        WsIncoming? msg;
        try {
            msg = JsonSerializer.Deserialize<WsIncoming>(json, JsonOptions);
        } catch {
            await SendToAsync(ws, new WsEvent { Name = "error", Data = new { error = "Invalid JSON." } });
            return;
        }

        if (msg == null) return;

        if (msg.Type.Equals("ping", StringComparison.OrdinalIgnoreCase)) {
            await SendToAsync(ws, new WsEvent { Name = "pong" });
            return;
        }

        if (msg.Type.Equals("command", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(msg.Payload)) {
                await SendToAsync(ws, new WsEvent { Name = "commandResult", Data = CommandResult.Fail("Empty payload.") });
                return;
            }

            // Dispatch on the game framework thread to ensure safety
            CommandResult? cmdResult = null;
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                cmdResult = _dispatcher.Dispatch(msg.Payload!);
            });
            await SendToAsync(ws, new WsEvent { Name = "commandResult", Data = cmdResult });
            return;
        }

        // type:"chat" - sends text directly to the game chat box (like typing it in-game).
        // "/clap" → emote, "/p hello" → party, plain text → say, etc.
        if (msg.Type.Equals("chat", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(msg.Payload)) {
                await SendToAsync(ws, new WsEvent { Name = "chatResult", Data = CommandResult.Fail("Empty payload.") });
                return;
            }

            Exception? chatEx = null;
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                try { ChatBox.SendMessage(msg.Payload!); } catch (Exception e) { chatEx = e; }
            });
            var chatResult = chatEx == null ? CommandResult.Success() : CommandResult.Fail(chatEx.Message);
            await SendToAsync(ws, new WsEvent { Name = "chatResult", Data = chatResult });
        }
    }

    // Event broadcast helpers (called by Plugin when state changes)

    public void NotifyMacroStarted(string macroName) =>
        _ = BroadcastEventAsync(new WsEvent { Name = "macroStarted", Data = new { macro = macroName } });

    public void NotifyMacroStopped() =>
        _ = BroadcastEventAsync(new WsEvent { Name = "macroStopped" });

    public void NotifyBroadcastReceived(string cmd) =>
        _ = BroadcastEventAsync(new WsEvent { Name = "broadcastReceived", Data = new { cmd } });

    /// <summary>
    /// Sends a <c>pluginCommand</c> message to all connected WebSocket clients
    /// (both browser UI clients and plugin-client instances).
    /// Browser clients display it in the event log; plugin clients execute it.
    /// </summary>
    public void BroadcastPluginCommand(string command) =>
        _ = BroadcastPluginCommandAsync(new WsPluginCommand { Payload = command });

    // Internal

    public async Task BroadcastEventAsync(WsEvent ev) => await BroadcastBytesAsync(SerializeObject(ev));

    public async Task BroadcastPluginCommandAsync(WsPluginCommand cmd) => await BroadcastBytesAsync(SerializeObject(cmd));

    private async Task BroadcastBytesAsync(ArraySegment<byte> payload) {
        if (_clients.IsEmpty) return;
        foreach (var (id, client) in _clients) {
            if (client.Socket.State != WebSocketState.Open) {
                _clients.TryRemove(id, out _);
                continue;
            }
            try {
                await client.Socket.SendAsync(payload, WebSocketMessageType.Text, true, _cts.Token);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Warning(ex, $"[WS] Error sending to client {id}");
                _clients.TryRemove(id, out _);
            }
        }
    }

    private static async Task SendToAsync(WebSocket ws, WsEvent ev) {
        if (ws.State != WebSocketState.Open) return;
        try {
            await ws.SendAsync(SerializeObject(ev), WebSocketMessageType.Text, true, CancellationToken.None);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[WS] Error sending to client.");
        }
    }

    private static ArraySegment<byte> SerializeObject<T>(T obj) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions);
        return new ArraySegment<byte>(bytes);
    }

    public void Dispose() {
        _cts.Cancel();
        foreach (var client in _clients.Values) {
            try { client.Socket.Abort(); } catch { /* ignore */ }
        }
        _clients.Clear();
        _cts.Dispose();
    }
}
