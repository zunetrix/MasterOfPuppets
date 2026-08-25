# Configuration Management & Live Variable Updates

## Overview

This document details configuration reloading, out-of-band live macro variable updates, and clipboard/importer improvements added to Master of Puppets.

---

## 1. Configuration Hot-Reload (`/mop reload` / `/mop loadconfig`)

### 1.1 Command Syntax
```text
/mop reload
/mop loadconfig
```

### 1.2 Behavior & Implementation
* **Disk Reload**: Reads `MasterOfPuppets.json` directly from the user's plugin configuration directory (`DalamudApi.PluginInterface.ConfigFile`).
* **In-Memory Refresh**: Calls `Config.UpdateFromJson(json)` to refresh all character lists, formations, window layouts, and macros in the running plugin instance without requiring a game restart or plugin toggle.
* **IPC Synchronization**: Automatically triggers `IpcProvider.SyncConfiguration()` to broadcast the refreshed configuration across all local multi-boxed client instances.
* **User Feedback**: Displays an in-game success notification upon completion or an error alert if the JSON is malformed.

### 1.3 Relevant Files
* `MasterOfPuppets/Commands/PluginCommandManager.cs`: Command parsing for `reload` and `loadconfig`.
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
```

### 2.3 Out-of-Band ChatSync Interception
* `ChatWatcher.TryHandleImmediateMacroVariableUpdate` inspects incoming chat messages for `/mop setvar` before they enter the action queue.
* Variable changes are applied directly to all active `MacroState` and `LoopState` execution contexts (`Plugin.MacroHandler.UpdateActiveMacroVariables`).
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
