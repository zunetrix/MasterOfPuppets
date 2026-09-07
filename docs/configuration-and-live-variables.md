# Configuration Management & Live Variable Updates

## Overview

This document details configuration reloading, out-of-band live macro variable updates, and clipboard/importer improvements added to Master of Puppets.

---

## 1. Configuration Hot-Reload

### 1.1 Command Syntax
```text
/mop reload
/mop loadconfig
/mop reloadconfig
```

All three commands are aliases for the same operation.

### 1.2 Behavior & Implementation
* **Disk Reload**: Reads `MasterOfPuppets.json` directly from the user's plugin configuration directory (`DalamudApi.PluginInterface.ConfigFile`).
* **In-Memory Refresh**: Calls `Config.UpdateFromJson(json)` to refresh character lists, formations, window layouts, macros, and saved Lua scripts in the running plugin instance without requiring a game restart or plugin toggle.
* **IPC Synchronization**: Automatically triggers `IpcProvider.SyncConfiguration()` to broadcast the refreshed configuration across all local multi-boxed client instances.
* **User Feedback**: Displays an in-game success notification upon completion or an error alert if the JSON is malformed.

This reloads configuration data. It does **not** reload a newly built plugin DLL; DLL changes still require the normal Dalamud/plugin hot-reload path.

### 1.3 Lua synchronization

Cross-PC `mopluarun` and `mopluastop` use the normal Chat Sync channel and
optional sender-whitelist settings, just like macro commands. No separate Lua
conductor authorization is required. Duplicate, stale, future-dated, and
malformed Lua envelopes are rejected independently.

`LuaDistributedReadinessEnabled` defaults to `false`. When enabled, cross-PC Lua
uses opt-in formation staging and waits for conductor GO. Configure
`LuaReadinessTimeoutSeconds` from 5 to 120 and
`LuaReadinessTimeoutPolicy` as `abort`, `continue_ready`, or `continue_all`.
Enable it only after all participating PCs run the same build and have matching
participant formations; the Scripts window shows live protocol/participant
diagnostics.

### 1.4 Relevant Files
* `MasterOfPuppets/Commands/PluginCommandManager.cs`: Command parsing for `reload`, `loadconfig`, and `reloadconfig`.
* `MasterOfPuppets/Plugin.cs`: `ReloadConfigFromDisk()` implementation and IPC broadcast.

---

## 2. Live Macro Variable Updates (`/mop setvar`)

### 2.1 Problem
Previously, variables inside active or looping macros could only be declared when the macro was initially launched (e.g. via `-var=$speed=1.0`). Once running in a loop (`/moploop`), there was no mechanism to adjust variables on the fly without stopping the macro and restarting it.

Furthermore, sending a command across ChatSync (`/cwl2 mopbr ...`) normally appends the command to the character's macro action queue, meaning a variable update would be blocked behind the currently executing loop.

### 2.2 Solution & Syntax

#### Local Plugin Command:
```text
/mop setvar -var=$name=value[;$other=value]
/mop setvars -var=$name=value[;$other=value]
```

#### ChatSync Broadcast:
```text
/cwl2 mopbr /mop setvar -var=$name=value[;$other=value]
/cwl2 mopluavars -var=$name=value[;$other=value]
```

`mopluavars` is the direct cross-PC Lua control frame. When the chat sender uses the older nested `mopbr /mop setvar` form, MoP automatically emits the dedicated frame as well. The direct form is preferred when diagnosing chat-command routing.

### 2.3 Out-of-Band ChatSync Interception
* `ChatWatcher.TryHandleImmediateMacroVariableUpdate` inspects incoming chat messages for `/mop setvar` before they enter the action queue.
* Every client that actually receives the ChatSync line forwards the idempotent assignment across that machine's IPC group. This does not assume that a particular elected client belongs to or can hear the same game chat channel. Variable changes therefore reach all active macro and Lua execution contexts, not only the receiving client.
* The update takes effect immediately when the next action in the loop resolves, without interrupting physical character movement or resetting macro loop counters.

### 2.4 Macro Editor UI Integration
* Added the **"Apply to Running Macro"** button in the Macro Editor window (`MacroEditorWindow.cs`).
* Allows editing macro variables in the UI and pushing them in real-time to all running clients.

### 2.5 Relevant Files
* `MasterOfPuppets/Commands/PluginCommandManager.cs`: Local command handler.
* `MasterOfPuppets/Game/ChatWatcher.cs`: Out-of-band immediate variable extraction.
* `MasterOfPuppets/MopMacro/MacroHandler.cs`: `UpdateActiveMacroVariables()` state updating.
* `MasterOfPuppets/Ipc/IpcProvider.Macro.cs`: `IpcMessageType.UpdateMacroVariables` IPC handler.

---

## 3. Macro Importer & Windows Native Clipboard

### 3.1 Win32 Native Clipboard Support
* **Issue**: ImGui's built-in `ImGui.GetClipboardText()` has fixed internal buffer constraints that can truncate large multi-command macro or formation payloads on Windows.
* **Solution**: `WindowsApi.GetClipboardText()` uses direct Win32 `OpenClipboard`, `GetClipboardData(CF_UNICODETEXT)`, and `GlobalLock` to retrieve full-length clipboard strings reliably on Windows.

### 3.2 Flexible Import Formats
`MacroManager.ImportMacroFromString` supports:
1. **Raw JSON Object**: `{ "name": "My Macro", "commands": [...] }`
2. **JSON Array of Macros**: `[ { "name": "Macro 1" }, { "name": "Macro 2" } ]`
3. **Compressed Base64 Blobs**: Legacy compressed share strings.
4. **Markdown Stripping**: Automatically detects and strips leading/trailing markdown code fences (```` ``` ```` or ```json) when copying snippets from Discord, GitHub, or documentation.

### 3.3 Relevant Files
* `MasterOfPuppets/Util/WindowsApi.cs`: Win32 clipboard API wrapper.
* `MasterOfPuppets/MopMacro/MacroManager.cs`: `ImportMacroFromString` format detection and parsing.
