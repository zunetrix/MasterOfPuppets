using System.Text.Json.Serialization;

namespace MasterOfPuppets.RemoteControl;

// REST API - Requests

internal sealed class CommandRequest {
    [JsonPropertyName("cmd")] public string Cmd { get; init; } = string.Empty;
}

internal sealed class BroadcastRequest {
    [JsonPropertyName("cmd")] public string Cmd { get; init; } = string.Empty;
}

internal sealed class BroadcastCharRequest {
    [JsonPropertyName("character")] public string Character { get; init; } = string.Empty;
    [JsonPropertyName("cmd")] public string Cmd { get; init; } = string.Empty;
}

internal sealed class BroadcastGroupRequest {
    [JsonPropertyName("group")] public string Group { get; init; } = string.Empty;
    [JsonPropertyName("cmd")] public string Cmd { get; init; } = string.Empty;
}

/// <summary>
/// Free-form chat message sent directly to the game chat box of the
/// <b>server instance</b> (the "master" client running the HTTP server).
///
/// This is intentionally LOCAL-ONLY: it calls <c>ChatBox.SendMessage</c>
/// which hooks the game's chat input directly on this machine.
/// It does NOT go through <c>IpcProvider.BroadCast</c> and will NOT
/// be sent to other connected MasterOfPuppets clients.
///
/// Examples:
///   "/clap"    → plays the clap emote on the master client
///   "/p hello" → sends "hello" to party chat from the master client
///   "hello"    → says "hello" in say chat on the master client
/// </summary>
internal sealed class ChatRequest {
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

internal sealed class RunMacroRequest {
    /// <summary>Macro name or 1-based index.</summary>
    [JsonPropertyName("macro")] public string Macro { get; init; } = string.Empty;
    /// <summary>Optional inline variable string, e.g. "-var=$x=1;$y=2"</summary>
    [JsonPropertyName("vars")] public string? Vars { get; init; }
}

// REST API - Responses

internal sealed class CommandResult {
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }

    public static CommandResult Success() => new() { Ok = true };
    public static CommandResult Fail(string error) => new() { Ok = false, Error = error };
}

internal sealed class StatusResponse {
    [JsonPropertyName("characterName")] public string? CharacterName { get; init; }
    [JsonPropertyName("isLoggedIn")] public bool IsLoggedIn { get; init; }
    [JsonPropertyName("macroRunning")] public bool MacroRunning { get; init; }
    [JsonPropertyName("connectedClients")] public int ConnectedClients { get; init; }
    [JsonPropertyName("serverVersion")] public string ServerVersion { get; init; } = "1.0";
}

internal sealed class MacroEntry {
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("tags")] public string[] Tags { get; init; } = [];
}

internal sealed class MacrosResponse {
    [JsonPropertyName("macros")] public MacroEntry[] Macros { get; init; } = [];
}

internal sealed class ErrorResponse {
    [JsonPropertyName("code")] public string Code { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; }
    public ErrorResponse(string code, string message) { Code = code; Message = message; }
}

//  WebSocket - Messages

/// <summary>Message received from a WebSocket client.</summary>
internal sealed class WsIncoming {
    /// <summary>"command" | "ping"</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    /// <summary>Full command string, e.g. "/mop run MyMacro" or "mopbr /wait 1"</summary>
    [JsonPropertyName("payload")] public string? Payload { get; init; }
}

/// <summary>Event pushed from plugin to all connected WebSocket clients.</summary>
internal sealed class WsEvent {
    [JsonPropertyName("type")] public string Type { get; init; } = "event";
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("data")] public object? Data { get; init; }
}

/// <summary>
/// Server→clients broadcast carrying a command for plugin-client instances to execute.
/// Browser clients display it in the event log under "pluginCommand".
/// </summary>
internal sealed class WsPluginCommand {
    [JsonPropertyName("type")] public string Type { get; init; } = "pluginCommand";
    [JsonPropertyName("payload")] public string Payload { get; init; } = string.Empty;
}
