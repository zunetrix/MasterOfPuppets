

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
