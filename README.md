# Master Of Puppets

Master Of Puppets is a Dalamud plugin for coordinating FFXIV performers across
local clients and multiple PCs. It provides broadcast actions, programmable
macros, formations, synchronized movement, and a Lua scripting system for
stateful theatrical automation.

The [documentation index](docs/index.md) separates current user-facing behavior
from the [long-term Lua automation goal](docs/architecture/LUA_AUTOMATION_V2_GOAL.md).

## Installation

Add this URL under **Dalamud Settings > Experimental > Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/zunetrix/DalamudPlugins/main/pluginmaster.json
```

Save the settings, open the Plugin Installer, and search for **Master Of Puppets**.

## Development

The solution targets .NET 10 for Windows. Build from the repository root:

```sh
dotnet build -c Debug
dotnet build -c Release
dotnet test ./MasterOfPuppetsTests/
```

## Repository layout

- `MasterOfPuppets/` - plugin source and packaged Lua scripts.
- `MasterOfPuppetsTests/` - automated tests.
- `docs/` - maintained architecture, user guides, and templates.
- `tools/` - development and deployment helpers.

Lua-related architecture and API decisions are documented in the architecture
documents linked above.
