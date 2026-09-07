# Rigid Marching Formation

## Behavior

**Rigid Marching Formation** freezes the participant roster at launch and assigns every follower a row-major slot behind one leader. The leader is never assigned a slot. A selected target becomes the leader; with no selected target, the initiating character becomes the leader. An external target can lead but is not controllable and is not counted as a follower.

Rows fill from the leader's left to right before the next row begins. Every row uses the same centered column coordinates, so an incomplete final row stays column-aligned rather than being recentered. The first row is one `$vertical` unit behind the leader. Followers beyond `$rows * $columns` stop moving and stand down.

For example, with two rows, two columns, and three followers, the assignments are:

| Follower | Row | Column | Offset |
| --- | ---: | ---: | --- |
| 1 | 1 | left | `(-horizontal / 2, -vertical)` |
| 2 | 1 | right | `(horizontal / 2, -vertical)` |
| 3 | 2 | left | `(-horizontal / 2, -2 * vertical)` |

This confirms the proposed example is logically consistent.

## Controls

The launch macro exposes `$anchor`, `$rows`, `$columns`, `$horizontal`, `$vertical`, `$precision`, `$neighbor_correction`, and `$maximum_neighbor_correction`. During a run, broadcast new values without restarting it:

```text
/cwl2 mopbr /mop setvar -var=$rows=3;$columns=6;$horizontal=1.2;$vertical=1.6
```

The script polls every 0.10 seconds and immediately reflows the frozen roster. The roster cannot be changed without starting a new run, but leadership can be handed to the sender's current target with:

```text
/cwl2 mopluavars -var=$anchor="<t>"
```

If the new leader is a puppet, it leaves the grid and the old puppet leader returns in frozen-roster order. An external target leads without being added to the grid. Runs use the normal ten-minute Lua lifetime and can be stopped with `/mop lua stop "Rigid Marching Formation"`.

Controlled followers always cancel persistent emotes before movement. This is intentional and is not configurable for the stock formation.

The launch uses `/mop lua sync`, so the initiating PC resolves visibility once and transmits one authoritative ordered CID roster to every physical PC. Receivers must not compact their own local visibility independently, because doing so can assign multiple remote puppets the same follower slot.

## Movement model and feasibility

This is feasible as a native-assisted Lua behavior, but not as a pure waypoint macro. Each follower uses the same virtual grid pose derived from the leader. Its center is derived directly from the observed leader position so separate clients cannot accumulate permanent straight-line drift. On ordinary turns, the controller rate-limits orientation and reserves angular speed for the outside edge of the grid, so the block rotates only as fast as its widest occupied slot can physically travel. A heading jump of 150 degrees or more in one update is classified as an about-face: the shared frame flips immediately and followers take the shorter straight path through the leader instead of performing a semicircle. Each slot also consults up to four adjacent visible followers for a small bounded correction.

The controller mirrors walk/run cadence, temporarily runs to recover a displaced slot, and mirrors Sprint on a best-effort basis. It cannot override collision, terrain, server corrections, loading distance, status/action restrictions, or the game's locomotion speed limits. A very wide formation, abrupt reversal, obstruction, or teleport therefore causes temporary lag and recovery rather than impossible spacing or a snap. Tight turns work best with moderate horizontal spacing and fewer columns.

## Performance

The Lua layer recomputes a small grid only when control values change and otherwise polls at 10 Hz. The native controller does constant work per rendered frame plus at most four nearby actor lookups per follower. CPU and allocation cost are therefore linear in the number of local game clients, not quadratic in formation size. No per-frame ChatSync or IPC traffic is added; only launch and explicit live-variable updates are broadcast.

For 32 clients, the meaningful performance cost remains the existing per-client object-table scan and movement update. The four-neighbor cap prevents a 32-by-32 all-pairs correction pass. Rendering many game clients and keeping every leader/follower mutually visible will normally dominate this controller's cost.
