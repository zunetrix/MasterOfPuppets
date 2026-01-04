# Master Of Puppets

FFXIV plugin to send custom individual actions (like game macros) with local sync or via chat (party, linkshell, crossworld linkshell)


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

# Game Sheet Preview
 - https://exd.camora.dev

## Sig maker
 - https://github.com/A200K/IDA-Pro-SigMaker/releases

