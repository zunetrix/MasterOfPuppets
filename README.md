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

## Movement Coordinate System in Final Fantasy XIV

The movement system in Final Fantasy XIV is based on a 3D Cartesian coordinate system, combined with a facing (rotation) value expressed in radians.

---

## World Axes (Top View)

```
           North (-Z)
               ↑
               |
West (-X) ←----+----→ East (+X)
               |
               ↓
           South (+Z)
```

* X increases to the **right (East)**
* Z increases **downward (South)**

---

## Facing = North (π radians)

```
           Forward (-Z)
               ↑
               |
Left (-X)  ←---+---→  Right (+X)
               |
               ↓
           Backward (+Z)
```

* Right → +X
* Left → -X
* Forward → -Z
* Backward → +Z

---

## Facing = South (0 radians)

```
           Backward (-Z)
               ↑
               |
Right (-X) ←---+---→ Left (+X)
               |
               ↓
           Forward (+Z)
```

* Left → +X
* Right → -X
* Forward → +Z
* Backward → -Z

---

## Y Axis (Vertical)

```
        Up (+Y)
          ↑
          |
          ● (character)
          |
          ↓
        Down (-Y)
```

* Up → +Y
* Down → -Y

---

## Facing (Rotation)

* `0` radians → facing **South**
* Rotation increases **counterclockwise**
* Rotation decreases **clockwise**

```
             π (North)
               ↑
               |
 +π/2 (West) ← + → -π/2 (East)
               |
               ↓
             0 (South)
```

Facing is represented in radians:

- South → 0
- North → π (≈ 3.14159)
- East → -π/2 (≈ -1.5708)
- West → +π/2 (≈ 1.5708)

Notes:
- Rotation increases counterclockwise
- Rotation decreases clockwise

---

## Notes

* The coordinate system is **world-aligned** (not camera-based).
* Movement directions depend on the current facing.
* Y axis is independent from X/Z movement.

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

# Game Sheet Preview
 - https://exd.camora.dev

## Sig maker
 - https://github.com/A200K/IDA-Pro-SigMaker/releases

