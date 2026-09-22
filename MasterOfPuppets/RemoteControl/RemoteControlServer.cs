using System;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.RemoteControl;

/// <summary>
/// Embedded HTTP server that:
///  - Serves static web UI assets (GET /, /app.js, /styles.css).
///  - Exposes a REST API under /api/v1/ (commands, broadcast, status, macros).
///  - Upgrades GET /ws to a WebSocket connection managed by <see cref="RemoteControlWebSocketManager"/>.
///
/// Only one instance can bind to a given port at a time; a failed bind (port in use)
/// is caught and surfaced via <see cref="StatusMessage"/>.
/// </summary>
internal sealed class RemoteControlServer : IDisposable {
    private readonly Plugin _plugin;
    private readonly RemoteCommandDispatcher _dispatcher;
    public readonly RemoteControlWebSocketManager WsManager;

    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private Task? _acceptLoop;
    private string _token;
    private int _port;

    public bool IsListening { get; private set; }
    public string StatusMessage { get; private set; } = "Stopped";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public RemoteControlServer(Plugin plugin) {
        _plugin = plugin;
        _dispatcher = new RemoteCommandDispatcher(plugin);
        WsManager = new RemoteControlWebSocketManager(_dispatcher);
    }

    public void Start(int port, string token) {
        _port = port;
        _token = token;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try {
            _listener.Start();
            IsListening = true;
            StatusMessage = $"Listening on http://127.0.0.1:{port}/";
            DalamudApi.PluginLog.Information($"[RemoteControl] {StatusMessage}");
            _acceptLoop = Task.Run(AcceptLoopAsync);
        } catch (HttpListenerException ex) {
            IsListening = false;
            StatusMessage = $"Failed to start: {ex.Message}";
            DalamudApi.PluginLog.Warning($"[RemoteControl] {StatusMessage}");
        }
    }

    private async Task AcceptLoopAsync() {
        while (!_cts.Token.IsCancellationRequested) {
            HttpListenerContext ctx;
            try {
                ctx = await _listener.GetContextAsync();
            } catch (HttpListenerException) when (_cts.IsCancellationRequested) {
                return;
            } catch (ObjectDisposedException) {
                return;
            } catch (Exception ex) {
                DalamudApi.PluginLog.Warning(ex, "[RemoteControl] Accept error.");
                continue;
            }
            _ = Task.Run(() => HandleContextAsync(ctx));
        }
    }

    private async Task HandleContextAsync(HttpListenerContext ctx) {
        var req = ctx.Request;
        var resp = ctx.Response;
        var path = req.Url?.AbsolutePath ?? "/";

        try {
            // Static assets (no auth)
            if (req.HttpMethod == "GET" && RemoteControlWebAssets.TryGet(path, out var assetData, out var contentType)) {
                resp.ContentType = contentType;
                resp.ContentLength64 = assetData.Length;
                resp.AddHeader("Cache-Control", "no-store");
                resp.StatusCode = 200;
                await resp.OutputStream.WriteAsync(assetData, _cts.Token);
                resp.Close();
                return;
            }

            // WebSocket upgrade (auth required)
            // NOTE: ctx.Request.IsWebSocketRequest can return false when behind cloudflared/ngrok
            // because the tunnel proxy may normalise header casing or add extra headers.
            // We detect the upgrade manually using the standard HTTP Upgrade headers instead.
            bool isWsUpgrade =
                string.Equals(req.Headers["Upgrade"], "websocket", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(req.Headers["Sec-WebSocket-Key"]);

            if (req.HttpMethod == "GET" && path == "/ws" || isWsUpgrade && path == "/ws") {
                // Browsers cannot set custom headers on WebSocket - accept token via query param too
                var queryToken = req.QueryString["token"];
                var authHeader = req.Headers["Authorization"]
                    ?? (string.IsNullOrEmpty(queryToken) ? null : "Bearer " + queryToken);

                if (!IsAuthorized(authHeader)) {
                    await WriteErrorAsync(resp, 401, "unauthorized", "Invalid or missing token.");
                    return;
                }

                if (!isWsUpgrade && !ctx.Request.IsWebSocketRequest) {
                    await WriteErrorAsync(resp, 400, "invalid_request", "Expected WebSocket upgrade.");
                    return;
                }

                HttpListenerWebSocketContext wsCtx;
                try {
                    wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
                } catch (Exception ex) {
                    DalamudApi.PluginLog.Warning(ex, "[RemoteControl] WS upgrade failed.");
                    return;
                }

                var ipAddress = ctx.Request.RemoteEndPoint?.ToString() ?? "Unknown IP";
                var userAgent = ctx.Request.UserAgent ?? "Unknown User-Agent";

                await WsManager.HandleClientAsync(wsCtx.WebSocket, ipAddress, userAgent);
                return;
            }

            // REST API (auth required)
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) {
                if (!IsAuthorized(req.Headers["Authorization"])) {
                    await WriteErrorAsync(resp, 401, "unauthorized", "Invalid or missing token.");
                    return;
                }

                await RouteApiAsync(req, resp, path);
                return;
            }

            await WriteErrorAsync(resp, 404, "not_found", "Unknown path.");
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[RemoteControl] Unhandled error.");
            try { await WriteErrorAsync(resp, 500, "internal_error", ex.Message); } catch { /* ignore */ }
        }
    }

    private async Task RouteApiAsync(HttpListenerRequest req, HttpListenerResponse resp, string path) {
        // GET /api/v1/status
        if (req.HttpMethod == "GET" && path == "/api/v1/status") {
            var status = new StatusResponse {
                CharacterName = DalamudApi.ClientState.IsLoggedIn ? DalamudApi.PlayerState.CharacterName : null,
                IsLoggedIn = DalamudApi.ClientState.IsLoggedIn,
                MacroRunning = _plugin.MacroHandler.MacroCurrentId != null,
                ConnectedClients = WsManager.ConnectedClients,
            };
            await WriteJsonAsync(resp, 200, status);
            return;
        }

        // GET /api/v1/macros
        if (req.HttpMethod == "GET" && path == "/api/v1/macros") {
            var macros = _plugin.Config.Macros
                .Select((m, i) => new MacroEntry {
                    Index = i,
                    Name = m.Name ?? string.Empty,
                    Tags = m.Tags?.ToArray() ?? [],
                })
                .ToArray();
            await WriteJsonAsync(resp, 200, new MacrosResponse { Macros = macros });
            return;
        }

        // POST /api/v1/command
        // Executes locally on master AND broadcasts to all connected plugin-WS-clients.
        if (req.HttpMethod == "POST" && path == "/api/v1/command") {
            var body = await ReadJsonAsync<CommandRequest>(req);
            if (body == null) { await WriteErrorAsync(resp, 400, "invalid_request", "Body required."); return; }
            CommandResult result = default!;
            await DalamudApi.Framework.RunOnFrameworkThread(() => { result = _dispatcher.Dispatch(body.Cmd); });
            if (result.Ok) WsManager.BroadcastPluginCommand(body.Cmd);
            await WriteJsonAsync(resp, result.Ok ? 200 : 400, result);
            return;
        }

        // POST /api/v1/broadcast
        if (req.HttpMethod == "POST" && path == "/api/v1/broadcast") {
            var body = await ReadJsonAsync<BroadcastRequest>(req);
            if (body == null) { await WriteErrorAsync(resp, 400, "invalid_request", "Body required."); return; }
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                _plugin.IpcProvider.EnqueueMacroActions(body.Cmd, includeSelf: true);
            });
            await WriteJsonAsync(resp, 200, CommandResult.Success());
            return;
        }

        // POST /api/v1/broadcast/char
        if (req.HttpMethod == "POST" && path == "/api/v1/broadcast/char") {
            var body = await ReadJsonAsync<BroadcastCharRequest>(req);
            if (body == null) { await WriteErrorAsync(resp, 400, "invalid_request", "Body required."); return; }
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                _plugin.IpcProvider.EnqueueCharacterMacroActions(body.Cmd, body.Character);
            });
            await WriteJsonAsync(resp, 200, CommandResult.Success());
            return;
        }

        // POST /api/v1/broadcast/group
        if (req.HttpMethod == "POST" && path == "/api/v1/broadcast/group") {
            var body = await ReadJsonAsync<BroadcastGroupRequest>(req);
            if (body == null) { await WriteErrorAsync(resp, 400, "invalid_request", "Body required."); return; }
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                _plugin.IpcProvider.EnqueueGroupMacroActions(body.Cmd, body.Group);
            });
            await WriteJsonAsync(resp, 200, CommandResult.Success());
            return;
        }

        // POST /api/v1/macro/run
        if (req.HttpMethod == "POST" && path == "/api/v1/macro/run") {
            var body = await ReadJsonAsync<RunMacroRequest>(req);
            if (body == null || string.IsNullOrWhiteSpace(body.Macro)) {
                await WriteErrorAsync(resp, 400, "invalid_request", "Field 'macro' (name or index) is required.");
                return;
            }

            Exception? ex = null;
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                try {
                    int idx = _plugin.MacroManager.FindMacroIndex(body.Macro);
                    var inlineVars = string.IsNullOrWhiteSpace(body.Vars)
                        ? null
                        : ArgumentParser.ParseInlineVars(body.Vars);
                    _plugin.IpcProvider.RunMacro(idx, inlineVars, includeSelf: true);
                } catch (Exception e) { ex = e; }
            });

            if (ex != null) { await WriteErrorAsync(resp, 400, "invalid_request", ex.Message); return; }
            await WriteJsonAsync(resp, 200, CommandResult.Success());
            return;
        }

        // POST /api/v1/chat
        // Sends any text directly to the game chat box - identical to typing it in-game.
        // "/clap"  → emote, "/p hello" → party, plain text → say, etc.
        if (req.HttpMethod == "POST" && path == "/api/v1/chat") {
            var body = await ReadJsonAsync<ChatRequest>(req);
            if (body == null || string.IsNullOrWhiteSpace(body.Message)) {
                await WriteErrorAsync(resp, 400, "invalid_request", "Field 'message' is required.");
                return;
            }

            Exception? chatEx = null;
            await DalamudApi.Framework.RunOnFrameworkThread(() => {
                try {
                    ChatBox.SendMessage(body.Message);
                } catch (Exception e) { chatEx = e; }
            });

            if (chatEx != null) { await WriteErrorAsync(resp, 400, "invalid_request", chatEx.Message); return; }
            await WriteJsonAsync(resp, 200, CommandResult.Success());
            return;
        }

        await WriteErrorAsync(resp, 404, "not_found", $"Unknown endpoint: {req.HttpMethod} {path}");
    }

    //  Auth

    private bool IsAuthorized(string? authHeader) {
        if (string.IsNullOrWhiteSpace(_token))
            return true; // no token configured → open

        const string prefix = "Bearer ";
        if (authHeader == null || !authHeader.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var supplied = Encoding.UTF8.GetBytes(authHeader[prefix.Length..]);
        var expected = Encoding.UTF8.GetBytes(_token);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    //  HTTP helpers

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest req) {
        try {
            using var ms = new System.IO.MemoryStream();
            await req.InputStream.CopyToAsync(ms);
            if (ms.Length == 0) return default;
            ms.Position = 0;
            return await JsonSerializer.DeserializeAsync<T>(ms, JsonOptions);
        } catch { return default; }
    }

    private static async Task WriteJsonAsync<T>(HttpListenerResponse resp, int status, T value) {
        var body = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        resp.StatusCode = status;
        resp.ContentType = "application/json; charset=utf-8";
        resp.ContentLength64 = body.Length;
        resp.AddHeader("Cache-Control", "no-store");
        resp.AddHeader("Access-Control-Allow-Origin", "*");
        await resp.OutputStream.WriteAsync(body);
        resp.Close();
    }

    private static async Task WriteErrorAsync(HttpListenerResponse resp, int status, string code, string message) {
        await WriteJsonAsync(resp, status, new ErrorResponse(code, message));
    }

    //  Lifecycle

    public void Dispose() {
        if (_cts?.IsCancellationRequested == true) return;
        IsListening = false;
        StatusMessage = "Stopped";
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
        WsManager.Dispose();
        _cts?.Dispose();
    }
}
