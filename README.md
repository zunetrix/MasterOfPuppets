# Master Of Puppets

FFXIV plugin that lets you create and send custom actions (similar to in-game macros), either locally or via chat-based broadcast. It supports broadcasting actions to multiple clients locally, or through in-game chat channels such as Party, Linkshell, and Cross-World Linkshell. Use it to trigger custom actions like emotes, minions, mounts, fashion changes, and more.


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

TODO:
 - moploop block (start end):
 - add chat watcher init to prevent register on chat game listener if use chat option is disabled
 - refactor macro commands to individual handlers

```
start code no loop block

/moploopstart
loop code
/moploopend
```

# Reference projects

## Repos
 - https://github.com/WorkingRobot/EXDViewer
 - https://github.com/KazWolfe/XIVDeck
 - https://github.com/Caraxi/SimpleTweaksPlugin
 - https://github.com/PunishXIV/Questionable
 - https://github.com/grittyfrog/MacroMate
 - https://github.com/awgil/ffxiv_navmesh/
 - https://github.com/Ennea/VeryImportantItem
 - https://github.com/una-xiv/umbra
 - https://github.com/Infiziert90/DeathRoll
 - https://github.com/Zeffuro/AetherBags
 - https://github.com/NightmareXIV/Stylist
 - https://github.com/MidoriKami/VanillaPlus
 - https://github.com/Critical-Impact/DalaMock/tree/main/DalaMock.PluginTemplate

# Game Sheet Preview
 - https://exd.camora.dev

## Sig maker
 - https://github.com/A200K/IDA-Pro-SigMaker/releases

