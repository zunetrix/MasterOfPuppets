# Live Formations & Natural Movement

## Overview

This document details the live formation tracking framework, the `Natural` movement mode, dynamic anchor resolution, and shape generation modes added to Master of Puppets.

---

## 1. Movement Modes: `Natural` vs `Precise` vs `Continuous`

When issuing formation commands (`/mopformationgoto`, `/mopformationmove`, or `/mop formation`), you can select one of three movement modes:

| Mode | Movement Behavior | Walk / Run State | Best Used For |
| :--- | :--- | :--- | :--- |
| **`precise`** | Native navigation directly to fixed coordinates; halts on sub-centimeter arrival. | Temporarily switches walk/run as needed. | Static staging, grid setups, initial positioning. |
| **`continuous`** | Native path follower that transitions between waypoint legs without coming to a complete stop. | Temporarily switches walk/run as needed. | Waypoint patrols, multi-point routes. |
| **`natural`** | Continuous live tracking on the framework update loop. Dynamically recalculates slot coordinates relative to moving anchors. | **Preserves character walk/run state**. Characters stay walking if walking, or running if running. | Conga lines, moving parades, dance choreography, trailing behind player/NPC anchors. |

---

## 2. Live Tracking Architecture (`FormationTrackingSession`)

### 2.1 Framework Update Loop
In `Natural` mode, the follower character does not navigate to a static world coordinate. Instead, `FormationTrackingSession.Update()` executes on every game tick on the framework thread:
1. Locates the anchor entity (local player, target, focus target, or named character in the object table).
2. Computes the anchor's current world position, velocity, and facing angle.
3. Classifies anchor locomotion (forward, backward, or strafe) using `FormationAnchorLocomotionTracker`.
4. Calculates the follower's world coordinate offset based on the formation shape.
5. Injects input via `FormationNaturalMovementStrategy` and `ForwardInputMovementController`.

### 2.2 Anti-Overshoot & Slot Arrival Holding
To prevent followers from overshooting their slot, turning around, or jittering when walking close to their anchor:
* **Slot Arrival Detection**: When a follower enters the slot arrival radius (`distance <= holdRadius`), forward movement stops immediately and the character matches the formation heading (`ApplyFormationFacing`).
* **Hysteresis Buffer**: A 3-inch resume buffer prevents rapid start-stop toggling.
* **Slot Spacing Preservation**: When the leader comes to a halt, followers halt cleanly at their exact assigned slot offsets (e.g. `0.5y` intervals), preventing formation collapse or bunching up into the leader.

---

## 3. Anchor Tokens & Reference Resolvers

Formation commands support dynamic anchor tokens:

| Token / Format | Target Resolved | Behavior |
| :--- | :--- | :--- |
| `self` | Local Character | Formation is placed relative to the local character. |
| `target`, `<t>`, `[t]` | Current Target | Anchors the formation to the local player's current target. |
| `ftarget`, `<f>`, `[f]`, `<focus>`, `[focus]` | Current Focus Target | Anchors the formation to the local player's focus target. If the focus target is the local character, the character stands still as leader while others form around them. |
| `sender` | Broadcast Sender | Anchors the formation to the character who sent the IPC or ChatSync command. |
| `"Character Name@World"` | Named Character | Searches the local object table for a visible character matching the specified name and world. |

---

## 4. Dynamic Origin & Targetless Leadership

### 4.1 Problem
Formations where Point 1 was left unassigned (`Cids: []`) or designed for arbitrary leaders previously required creating separate formation copies for each potential leader character to prevent offset corruption.

### 4.2 Dynamic Assignment
* When a formation with an unassigned Point 1 is triggered without an explicit external target (e.g. `/cwl2 moprun "The Line" -var=$anchor="<t>"` with no target selected):
* `FormationLocalMovementExecutor` detects that Point 1 is unassigned and automatically assigns the broadcast initiator to **Position 1 (`0, 0, 0`)** at the front/center.
* All other characters take their respective assigned follower slots behind or around the initiator without coordinate drift.

---

## 5. Formation Shape Generator: Reverse Tangent

In the Formation Shape Generator UI (`FormationShapeGenerator.cs`):
* Added **`Reverse Tangent`** facing mode alongside `Outward`, `Inward`, `North`, and `Tangent`.
* **Behavior**: Points along circular, ring, or spiral curves face backwards along the direction of curve progression ($+180^\circ$ from tangent).
* **Use Case**: Backwards-walking or outward-revolving circle dances.

---

## 6. Relevant Files

* `MasterOfPuppets/Formations/FormationTrackingSession.cs`: Live framework tracking engine.
* `MasterOfPuppets/Game/Movement/FormationNaturalMovementStrategy.cs`: Natural movement steering and facing.
* `MasterOfPuppets/Game/Movement/FormationTargetTracker.cs`: Motion hold debounce and slot arrival holding.
* `MasterOfPuppets/Formations/FormationAnchorResolver.cs`: Dynamic anchor resolution (`target`, `ftarget`, `sender`, name).
* `MasterOfPuppets/Formations/FormationLocalMovementExecutor.cs`: Dynamic origin slot mapping.
* `MasterOfPuppets/Formations/FormationShapeGenerator.cs`: `ReverseTangent` math and facing generation.
