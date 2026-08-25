# Project Agent Rules

## Configuration & Client Data Source

Anytime a new session begins or when handling client-specific requests in this project, **always ensure you are working with the latest `MasterOfPuppets.json` configuration file** located at:
`%APPDATA%\XIVLauncher\pluginConfigs\MasterOfPuppets.json`

Use this JSON file as the source of truth to pull:
- **Characters** (names, CIDs, home worlds, job/class info)
- **Formations** (formations, slots, offsets, spacing, loop definitions)
- **Macros** (commands, actions, triggers, tags, variables)
- **Character Groups (`CidsGroups`)**
- **Window Layouts & Profiles**
- Any other client-specific or configuration-relevant data needed for the request.

## Read-Only Policy & Exportable Blob Format

- **`MasterOfPuppets.json` is strictly READ-ONLY**: Do NOT write to or modify `MasterOfPuppets.json` unless the user explicitly requests direct file modifications.
- **Exportable Blobs**: When the user requests new or modified formations or macros, output them in the standard exportable blob format (or exportable `.blob.txt` files / snippets as requested) so they can be imported via the plugin UI.

