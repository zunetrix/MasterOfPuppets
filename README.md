# Master Of Puppets

FFXIV plugin that lets you create and send custom actions (similar to in-game macros), either locally or via chat-based broadcast. It supports broadcasting actions to multiple clients locally, or through in-game chat channels such as Party, Linkshell, and Cross-World Linkshell. Use it to trigger custom actions like emotes, minions, mounts, fashion changes, and more.

## Install through Dalamud

Add this URL under **Dalamud Settings > Experimental > Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/zunetrix/DalamudPlugins/main/pluginmaster.json
```

Save the settings, open the Plugin Installer, and search for **Master Of Puppets**.


# Builds
```sh
dotnet build -c Debug
```

```sh
dotnet build -c Release
```

# Tests
```sh
dotnet test ./MasterOfPuppetsTests/
```

# Init submodules
```sh
git submodule update --init --recursive

git submodule sync
git submodule update --init --recursive --force

```

# Update submodules
```sh
cd /submodule/
git checkout main
git pull origin main
```



# Reference projects

## Repos
 - https://github.com/WorkingRobot/EXDViewer
 - https://github.com/KazWolfe/XIVDeck
 - https://github.com/Caraxi/SimpleTweaksPlugin
 - https://github.com/PunishXIV/Questionable
 - https://github.com/grittyfrog/MacroMate
 - https://github.com/awgil/ffxiv_navmesh
 - https://github.com/Ennea/VeryImportantItem
 - https://github.com/una-xiv/umbra
 - https://github.com/Infiziert90/DeathRoll
 - https://github.com/Zeffuro/AetherBags
 - https://github.com/NightmareXIV/Stylist
 - https://github.com/MidoriKami/VanillaPlus
 - https://github.com/Critical-Impact/DalaMock/tree/main/DalaMock.PluginTemplate
 - https://github.com/UnknownX7/Cammy
 - https://github.com/UnknownX7/Hypostasis
 - https://github.com/Infiziert90/ChatTwo
 - https://github.com/Haselnussbomber/HaselDebug
 - https://github.com/Haselnussbomber/HaselCommon
 - https://github.com/rail2025/AetherBlackbox
 - https://github.com/bilk/RenderManager
 - https://github.com/BoxuChan/RenderManager
 - https://github.com/Knightmore/game-reversing
 - https://github.com/Knightmore/Henchman
 - https://github.com/Jaksuhn/ffxiv-bundleoftweaks
 - https://github.com/VeraNala/VIWI
 - https://github.com/Infiziert90/ChatTwo

# Game Sheet Preview
 - https://exd.camora.dev

## IDA Sig maker
 - https://github.com/A200K/IDA-Pro-SigMaker/releases
 - https://github.com/mahmoudimus/ida-sigmaker
