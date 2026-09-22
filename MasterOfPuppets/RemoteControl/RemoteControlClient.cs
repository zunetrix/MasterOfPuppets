using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.RemoteControl;

/// <summary>
/// Runs on "slave" plugin instances. Connects as a WebSocket client to the
/// master's RemoteControlServer and executes any <c>pluginCommand</c> messages
/// received via the local <see cref="RemoteCommandDispatcher"/>.
///
/// This allows commands issued via the master's web UI or REST API to be
/// forwarded to all connected plugin instances over the network, independently
/// of the local IPC transport (TinyIpc/XivIpc).
/// </summary>
internal sealed class RemoteControlClient : IDisposable {
    private readonly RemoteCommandDispatcher _dispatcher;
    private readonly Plugin _plugin;
    private ClientWebSocket? _ws;
    private CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
    };

    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public string StatusMessage { get; private set; } = "Stopped";

    public RemoteControlClient(Plugin plugin) {
        _dispatcher = new RemoteCommandDispatcher(plugin);
        _plugin = plugin;
    }

    /// <summary>
    /// Start connecting to <paramref name="serverUrl"/> (http/https accepted;
    /// converted internally to ws/wss). Reconnects automatically on disconnect.
    /// </summary>
    public void Start(string serverUrl, string token) {
        if (_disposed) return;

        var wsUrl = BuildWsUrl(serverUrl, token);
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ConnectLoopAsync(wsUrl, _cts.Token));
    }

    private async Task ConnectLoopAsync(string wsUrl, CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            _ws?.Dispose();
            _ws = new ClientWebSocket();

            string charName = "Unknown Character";
            await DalamudApi.Framework.Run(() => {
                charName = DalamudApi.ObjectTable.LocalPlayer?.Name.ToString() ?? "Unknown Character";
            });
            _ws.Options.SetRequestHeader("User-Agent", $"MasterOfPuppets / {charName}");

            StatusMessage = "Connecting…";

            try {
                await _ws.ConnectAsync(new Uri(wsUrl), ct);
                StatusMessage = "Connected";
                DalamudApi.PluginLog.Information($"[RemoteClient] Connected to {wsUrl.Split('?')[0]}");
                await ReceiveLoopAsync(_ws, ct);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                StatusMessage = $"Error: {ex.Message}";
                DalamudApi.PluginLog.Warning(ex, "[RemoteClient] Connection error.");
            }

            if (!ct.IsCancellationRequested) {
                StatusMessage = "Reconnecting in 4 s…";
                DalamudApi.PluginLog.Debug("[RemoteClient] Reconnecting in 4 s.");
                try { await Task.Delay(4000, ct); } catch (OperationCanceledException) { break; }
            }
        }

        StatusMessage = "Stopped";
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct) {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            WebSocketReceiveResult result;
            try {
                result = await ws.ReceiveAsync(buffer, ct);
            } catch (OperationCanceledException) {
                break;
            } catch (WebSocketException) {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close) {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { /* ignore */ }
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage) continue;

            var json = sb.ToString();
            sb.Clear();

            await ProcessMessageAsync(json);
        }
    }

    private async Task ProcessMessageAsync(string json) {
        WsServerMessage? msg;
        try {
            msg = JsonSerializer.Deserialize<WsServerMessage>(json, JsonOptions);
        } catch {
            DalamudApi.PluginLog.Warning($"[RemoteClient] Malformed message: {json[..Math.Min(100, json.Length)]}");
            return;
        }

        if (msg == null) return;

        switch (msg.Type.ToLowerInvariant()) {
            // Command broadcast: master wants all plugin clients to execute this
            case "plugincommand":
                if (string.IsNullOrWhiteSpace(msg.Payload)) return;
                DalamudApi.PluginLog.Debug($"[RemoteClient] Received pluginCommand: {msg.Payload}");
                await DalamudApi.Framework.RunOnFrameworkThread(() => {
                    _dispatcher.Dispatch(msg.Payload!);
                });
                break;

            // Server heartbeat / ack - nothing to do
            case "pong":
            case "commandresult":
            case "macroresult":
            case "event":
                break;

            default:
                DalamudApi.PluginLog.Debug($"[RemoteClient] Unknown message type: {msg.Type}");
                break;
        }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { _ws?.Abort(); } catch { /* ignore */ }
        _ws?.Dispose();
        _cts.Dispose();
        StatusMessage = "Stopped";
    }

    private static string BuildWsUrl(string serverUrl, string token) {
        var wsUrl = serverUrl.TrimEnd('/')
            .Replace("https://", "wss://")
            .Replace("http://", "ws://");
        wsUrl += "/ws";
        if (!string.IsNullOrWhiteSpace(token))
            wsUrl += "?token=" + Uri.EscapeDataString(token);
        return wsUrl;
    }
}

/// <summary>Message received from the RemoteControlServer on the client side.</summary>
internal sealed class WsServerMessage {
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Command string for <c>pluginCommand</c> messages.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("payload")]
    public string? Payload { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; init; }
}
