# Lua Scripting

> **Status:** This document describes the currently implemented Lua system. The
> broader, versioned architecture and coverage requirements are defined in the
> [Lua Automation V2 Long-Term Goal](architecture/LUA_AUTOMATION_V2_GOAL.md).

## Overview

Master of Puppets now has two separate automation systems:

| System | Best For | Authoring Model |
| :--- | :--- | :--- |
| **Macros** | Ordered game commands, existing group assignments, macro variables, and saved formations. | A list of commands interpreted by the macro engine. |
| **Lua Scripts** | Stateful behavior, loops, calculations, conversations, dynamic targets, and movement logic that reacts while it runs. | Lua code executed by the Lua runtime through the explicit `mop` API. |

Lua does not execute a macro or load a saved formation unless an API is deliberately added to do so. The current actor-follow and trajectory features run independently from the macro queue and saved formation definitions. They share only the plugin's lower-level services, such as actor lookup, chat, movement input, local IPC, and Chat Sync.

## What Changed

The implementation adds:

* A **Scripts** section in the main UI, separate from Macros.
* A script list with search, tags, icons, colors, duplicate, import, export, edit, local-PC run, and local-PC stop actions.
* A schema-v2 script editor containing source, bundle modules, typed parameters, capability declarations, resource leases, participant formation, appearance, and tags.
* Local multi-client execution and cross-PC Chat Sync execution.
* A synchronized run context containing the selected target, start time, seed, participant information, and script identity.
* Lua APIs for movement trajectories, dynamic actor following, chat, synchronized conversations, timing, per-character identity, runtime metadata, and capability discovery.
* A bounded per-run typed event stream for lifecycle and chat observations.
* Safe standard Lua base, math, string, table, bitwise, and coroutine features.
* Sandboxed, bundle-local reusable modules loaded with `require`.
* User-imported scripts covering freeform motion, dialogue, dynamic following,
  movement, and typed-event orchestration.
* Independent movement policies so ordinary formations retain exact arrival behavior while continuous Lua trajectories can use smoother pursuit behavior.

## Script Storage and Deployment

Scripts shown and edited in the in-game Script Editor are stored as `LuaScripts` entries in `MasterOfPuppets.json`. New installations do not receive bundled scripts automatically. Scripts can be created in the editor, imported through the normal Lua import workflow, or supplied by a local development deployment.

The project build still needs to be deployed to every PC that runs the plugin. A shared OneDrive project makes the same source available, but it does not by itself replace a running DLL or an existing saved script.

## Running and Stopping Scripts

### Current PC

```text
/mop lua run "<imported script>"
/mop lua status
/mop lua stop
```

Here, **local** means all active MoP clients on the current PC, not only the game window where the command was entered. MoP sends a launch request with a shared start time, seed, and complete bundle-contract hash. Every receiver executes only its own installed copy after source, module, resource, capability, parameter, and identity hashes match; transmitted text is never the execution trust root.

### Multiple PCs through Chat Sync

```text
/cwl2 mopluarun "<imported script>"
/cwl2 mopluastop
```

Use the chat prefix configured in **Settings > Chat Sync**; `/cwl2` is only an example. `/mop lua sync "<imported script>"` is a local alias that sends the same cross-PC command through the configured prefix.

The readable `mopluarun` command is expanded by the sender's MoP client into a synchronization envelope containing a future start time, shared seed, SHA-256 script hash, and encoded run target. Listening MoP clients suppress that internal envelope from their chat display. The short readable command remains visible.

Chat Sync does not transmit script or module source. Each PC must already have the identical installed schema-v2 bundle. A missing or different copy is rejected instead of silently running different behavior. Envelope version 6 carries the full contract hash, a unique message ID, and a creation timestamp. Receivers reject duplicate, stale, future-dated, or malformed envelopes.

Cross-PC Lua starts and stops use the normal Chat Sync channel and optional
sender-whitelist settings, just like macro commands. Anyone accepted by those
settings can start or stop Lua scripts; no separate conductor authorization is
required.

The next distributed protocol generation also has a compact binary wire codec
for PREPARE, STAGE/READY, GO, clock probe/reply, shared variables, participant
messages, heartbeat, ACK/NACK, stop, completion, and error
frames. It detects corruption and fits an authoritative 32-CID PREPARE roster
under the 500-byte chat-command ceiling. Internal `mopluaphase` handlers now
validate exact sender/CID identity, conductor authority, replay age, phase
ordering, and roster membership, and expose participant state in the Scripts
window.

**Settings > Lua Synchronization > Experimental PREPARE / READY / GO
staging** opts the public `mopluarun` flow into the readiness bridge. Each client
materializes PREPARE from the authenticated v6 manifest, takes a temporary
movement/synchronized-control lease, moves its local actor to the configured
Participant Formation with precise movement, emits STAGE and READY only after a
settled interval, and starts Lua at the conductor's future GO epoch. Readiness
regresses if movement restarts. Timeout policy can abort, continue only ready
performers, or continue everyone. Running participants send bounded heartbeats
and terminal status frames. Non-conductors also exchange bounded NTP-style
clock probes with the conductor. The filtered offset drives `mop.time()`,
`mop.runtime.shared_time()`, runtime elapsed diagnostics, and trajectory sample
time throughout the run; per-sample correction is capped to avoid phase snaps.
If a valid GO arrives late by at most five seconds, the client starts
immediately at the original shared phase instead of restarting the show at
phase zero. Older late joins are rejected explicitly. Loss of the conductor's
heartbeat aborts the local distributed session and stops its managed Lua run;
there is no implicit conductor election.

This mode defaults off pending live multi-PC validation. Every participant must
run this build, have identical scripts/formations and exact CID/name@world
configuration, trust the conductor, and share the same Chat Sync channel. The
GO frame carries both a wall-clock fallback and a high-resolution shared-clock
epoch. Live multi-PC validation is still required before enabling this mode by
default.

`mopluastop` is sufficient for a cross-PC stop. It does not need to be nested inside `mopbr`, because it is itself a Chat Sync command. Likewise, `mopluarun` does not need `mopbr` or `moprun`.

## Run Target Behavior

The target is dynamic. Select any player, NPC, enemy, or other visible actor before starting a target-aware script; MoP captures that actor and exposes its name through:

```lua
local anchor = mop.get_run_target()
```

The script therefore does not need to hardcode the target's name.

* `/mop lua run`: if no target is selected, `mop.get_run_target()` returns `nil`.
* `mopluarun` through Chat Sync: a selected target is used; if none is selected, the command sender becomes the effective run target.

The sender fallback lets a cross-PC run start behind its conductor without requiring the conductor to target themselves. Individual scripts can require an anchor when their behavior needs one.

## Available Lua API

The safe base, math, string, table, bitwise, and coroutine libraries are exposed.
`require` loads only modules stored in the script bundle. File access,
operating-system access, unrestricted package searchers, debug/IO libraries,
dynamic compilation, networking, process access, and CLR access are unavailable.
`math.random` is a deterministic run-local stream initialized from the shared
run seed; it does not use ambient process randomness.

Source and every module are compiled before save/import and again before launch.
Syntax failures include the script or module chunk name, line, column, and nearby
token when the runtime supplies one.

Each run currently permits 1,000 log lines, 256 KiB of UTF-8 log text, 32 pending
waiters, 200 total chat actions, and at most 10 chat actions in any five-second
window. Crossing a limit stops the script with a quota error. `mop.runtime.info()`
and `mop.runtime.status()` expose current quota usage in their `quota` table.

### Versioned runtime API

| Function | Purpose |
| :--- | :--- |
| `mop.api_version()` | Returns the current namespaced API version (`4.0.0`). |
| `mop.capabilities.list()` | Returns capability descriptors installed for this run. |
| `mop.capabilities.has(name, minimum_version?)` | Tests whether a capability and optional minimum version are available. |
| `mop.capabilities.describe(name)` | Returns one descriptor or `nil`. |
| `mop.capabilities.require(name, minimum_version?)` | Returns the descriptor or raises an actionable error. |
| `mop.runtime.info()` | Returns run ID, script, character, slot, participant count, seed, elapsed time, cancellation state, and host versions. |
| `mop.runtime.status()` | Returns the current run state and runtime metadata. |
| `mop.runtime.cancelled()` | Reports cancellation to cooperative script logic. |
| `mop.runtime.paused()` | Reports whether operator pause is active. |
| `mop.runtime.stop(reason?)` | Requests a structured stop of the current run. |
| `mop.runtime.local_time()` | Returns monotonic elapsed time for this run. |
| `mop.runtime.shared_time()` | Returns continuously corrected elapsed choreography time for distributed runs. |
| `mop.runtime.dalamud_version()` | Returns the host Dalamud version or `unknown`. |
| `mop.runtime.game_version()` | Returns the game version when available, otherwise `unknown`. |
| `mop.runtime.clientstructs_version()` | Returns the host ClientStructs version or `unknown`. |

Capability descriptors contain `name`, `version`, `description`, and a
`permissions` array. An absent capability is explicit, allowing portable scripts
to degrade gracefully.

### Typed event API (`mop.events`)

The `mop.events` capability is version `3.0.0`.
`mop.events.poll(name?)` returns the next matching event immediately or `nil`.
`mop.events.next(name?, timeout_seconds?)` waits without blocking the game
thread and returns either an event or `{ status = "timeout" }`.
Named filters are non-destructive: events with another name remain queued for
another reaction. `mop.events.subscribe(name)` and `unsubscribe(name)` control
high-volume raw world streams such as `combat.action` and `emote.played`.
`mop.events.stats()` reports capacity, published, consumed, dropped, and
completed counters. Events contain `status`, `sequence`, `name`,
`timestamp_unix_ms`, and a string-valued `data` table.

Each run owns a bounded 256-event queue. Producers never call Lua directly;
when the queue is full, the oldest entry is dropped and the pressure is visible
through `stats()`. Name and actor-watch filters preserve unmatched entries.
Current producers include run
lifecycle, host transition, `/say` chat, selected/focus target changes,
condition changes, and participant visibility/loss events. Game observations
are sampled at most four times per second from immutable framework-thread
snapshots.

### Shared variables and participant messages (`mop.coordination`)

`mop.shared.get(key)`, `mop.shared.list()`, and
`mop.shared.wait(key, timeout, after_sequence?)` read bounded per-run state.
`mop.shared.set(key, value)` updates it locally, but in distributed runs only
the authenticated conductor may write. Updates are monotonic and publish a
`sync.variable` event.

`mop.messages.send(topic, payload, target_slot?, schema_version?)` sends an
ordered message to the full authoritative roster or one zero-based participant
slot. `mop.messages.poll(topic?)` and `mop.messages.next(topic?, timeout?)`
consume the bounded per-run inbox. Messages expose ID, topic, payload, schema
version, sequence, sender/target CIDs, and receive time, and also publish a
`sync.message` event. Names are limited to 32 UTF-8 bytes, values/payloads to 96
UTF-8 bytes, schemas to versions 1–255, and queues to 256 messages. Distributed
coordination requires the experimental PREPARE/READY/GO bridge because its
authenticated protocol session supplies the roster and conductor authority.

### Typed game-state API (`mop.game-state`)

The `mop.game-state` capability is version `4.0.0`. Actor APIs operate on any
locally observable real player. The source does not need to be a configured
Master Of Puppets character, a participant, a party member, or a client
controlled by the same PC.

`mop.self.snapshot()`, `mop.target.snapshot(kind)`, `mop.actors.list()`,
`mop.actors.find(query)`, `mop.participants.list()`, and `mop.game.snapshot()`
return immutable tables. Actor lookup reports `found`, `missing`, or `ambiguous`
instead of guessing. Game-object and content IDs are strings so 64-bit values are
not rounded by Lua numbers. Snapshot `authority` distinguishes local, visible,
party, run-target, and configured-only data.

`mop.party.list()` exposes all currently observed party actors through the same
actor schema. `mop.player.profile()` adds locally authoritative content/world,
job, level-sync, race/tribe/company, mentor/returner, sex, and base-attribute
fields. It returns `nil` until Dalamud reports the player state loaded.

`mop.buddies.list()` returns immutable battle-buddy, companion, and pet
snapshots with typed IDs, HP, data ID, and an actor snapshot when the buddy has
a visible game object. `mop.actors.wait_visible(query, timeout)`,
`mop.actors.wait_lost(query, timeout)`, and
`mop.actors.wait_proximity(query, yalms, timeout)` provide bounded cancellable
waits without busy polling. An ambiguous name returns `ambiguous` immediately;
it is never treated as missing or lost. `mop.target.wait_changed(timeout)` waits for the
selected target identity to change, and
`mop.game.wait_condition(name, active?, timeout?)` matches condition names
case-insensitively. Every wait returns a structured status table; timeout is
`{ status = "timeout" }`.

`mop.actors.watch(query)` is the low-overhead dynamic observation primitive.
The query may be an exact game-object ID, entity ID, or player name (prefer
`Name@World`). The result contains `count`, a stable `watch_id`, monotonic
`revision`, a boolean `changes` table, and the current `actor`. `count` is
greater than one when a name is ambiguous. Repeating the same query in
one run reuses the same shared native watch; it does not create another world
scan. `mop.actors.wait_changed(query, after_revision, timeout?)` waits until the
cached watch advances and returns `status = "changed"`, or `timeout`.

Watch actors expose position, movement/walk/jump state, target, mount,
companion, emote, pose, ornament, facewear, headgear, visor, Sprint, weapon,
online status, and class/job. Corresponding change flags allow scripts to react
without comparing every field. `actor.state`, `actor.found`, and `actor.lost`
events carry the same watch ID and revision for event-loop integration.

`mop.actors.next_event(query, kind, timeout?)` is the concise, source-bound
reaction primitive. It creates or reuses the cached actor watch and waits only
for that actor, preserving events belonging to other watched players. Supported
kinds are `action`, `combat_action`, `general_action`, `jump`, `sprint`,
`emote`, `emote_state`, `fashion_accessory`, `mount`, `facewear`, `target`,
`idle_pose`, and `weapon`. The returned event includes `watch_id`, `query`,
source identity, revision, current actor state, and fields specific to that
edge. `mop.actors.event_sources()` reports whether state sampling, the native
combat/general-action hook, and the native short-emote hook are available. A
missing native hook produces `status = "unavailable"`, not a misleading timeout.

State-backed actor events are `actor.jump`, `actor.sprint`,
`actor.emote_state`, `actor.fashion_accessory`, `actor.mount`,
`actor.facewear`, `actor.target`, `actor.idle_pose`, and `actor.weapon`.
Native edges are `actor.action`, `actor.combat_action`,
`actor.general_action`, and `actor.emote_played`. Jump and Sprint use their
state events because those general actions do not reliably produce action
effects; short emotes use the native edge because they may begin and clear
between state samples.

`mop.actors.job(query)` observes the class/job of any visible player.
`mop.actors.wait_job_changed(query, previous_id, timeout?,
expected_game_object_id?)` provides a convenient identity-bound job wait and
reports `changed`, `timeout`, `ambiguous`, `actor_changed`, or `actor_lost`.
These actor-query functions are the portable choice for dynamic scripts;
`mop.target.job` remains a convenience for selected/focus/run/self slots.

`mop.target.job(kind?)` returns a structured observation containing
`class_job_id` and the bound actor. `mop.target.wait_job_changed(previous_id,
timeout?, kind?, expected_game_object_id?)` waits for that actor's class/job to
change and reports `changed`, `timeout`, `target_changed`, or `target_lost`.
Passing the expected game-object ID prevents a script from silently switching
to a newly selected player between waits.

See [Universal Lua Building Blocks](lua-universal-building-blocks.md) for
identity-safe observation loops, arbitrary-player job mirroring, action
reactions, cleanup, and performance guidance.

### Macro and formation API (`mop.automation`)

`mop.macros.list/status/run/pause/resume/stop` and
`mop.formations.list/find/run/stop` reuse the existing MoP managers. Mutating
calls require the corresponding resource leases. Every call specifies `local`
or `current_pc`; formation launch currently supports `current_pc` because the
existing formation executor is a coordinated local-PC operation.

`mop.macros.await(timeout_seconds?)` and
`mop.formations.await(timeout_seconds?)` provide cancellable, timeout-bounded
completion waits. They observe the current client's macro queue or local
formation movement; they do not claim that every remote PC completed. Results
use `completed` or `timeout` status and also publish `macro.*` or `formation.*`
events to the run event stream.

### Commands and game actions (`mop.actions`)

The `mop.actions` capability is version `4.0.0`.

`mop.commands.execute(text, scope?)` dispatches one validated line to `local`
or `current_pc`; empty, multiline, NUL-containing, and over-500-byte commands
are rejected. `mop.actions.use(kind, id, scope?)` supports typed `action`,
`general_action`, and `item` IDs. `mop.actions.walk(on|off|toggle, scope?)` and
`mop.actions.stop_movement(scope?)` reuse existing MoP services.

`mop.actions.jump()` and `mop.actions.sprint()` are simple local reaction
helpers. They request FFXIV's universal General Action IDs 2 and 3 respectively,
consume the normal game-action quota, and return the standard
`ok`/`status`/`message` result. Scripts do not need to repeat those built-in IDs.

### Script-controlled emotes

Emote policy also belongs entirely to Lua. `mop.actors.next_event(query,
"emote", timeout)` observes the native start edge even for short animations,
while an actor watch exposes `emote_id`, `emote_target_game_object_id`, and
`is_emote_looping` for persistent state and stop transitions.

`mop.actions.use_exact("emote", id, "local", persistent)` executes one exact
locally owned emote without choosing a fallback. Use
`mop.actions.use_exact_on("emote", id, target_id, persistent)` to retain an
observed target, and `mop.actions.stop_emote()` to stop a loop the script owns.
`stop_emote()` requests FFXIV's network-visible in-place loop exit and falls
back to the game's jump interruption when that state rejects an in-place exit;
it does not merely clear local animation memory. Every call returns the normal
`ok`/`status`/`message` result.

An imported script can combine those primitives for any
visible run target. It intentionally ignores the source emote's target and
always uses `mop.actions.use_exact`, so detecting an emote never changes a
recipient's selected game target to the source's target-of-target. Emote row IDs
are universal, so the default is to mirror the same ID. Events are
edge-triggered: each observed activation is dispatched once, including
multiple activations of the same persistent emote in rapid succession. The
script does not infer that a repeated ID is an automatic duplicate merely
because the preceding loop is still active. Each new persistent activation has
its own sampled-state confirmation window, so delayed stop or movement samples
from the preceding activation cannot cancel the new one.
A coder can keep all policy in the script with one optional table:

```lua
local emote_for_observed = {
    [123] = 456,  -- replace observed emote 123 with local emote 456
    [789] = false -- ignore observed emote 789
}
```

IDs absent from the table mirror unchanged. There is no host gearset-style
selection, emote UI configuration, or silent random fallback. An unavailable
exact emote is rejected and reported so the script author can choose an explicit
replacement.

### Script-controlled gearsets (`mop.gearsets`)

Gearset policy belongs to Lua rather than a hardcoded host table:

| Function | Purpose |
| :--- | :--- |
| `mop.gearsets.list(class_job_id?)` | Lists existing local gearsets, optionally filtered to one exact class/job ID. |
| `mop.gearsets.find(selector, class_job_id?)` | Resolves an exact name or 1-based number without equipping it. |
| `mop.gearsets.equip(selector, class_job_id?)` | Validates and equips an exact name or 1-based number. |
| `mop.actions.job(class_job_id, selector)` | Validates that the selected gearset has the exact requested class/job, then equips it. |

A selector is either an exact case-insensitive gearset name or a 1-based
gearset number. Names that match multiple gearsets are `ambiguous`; use a
number to distinguish them. Missing selectors, missing gearsets, and job
mismatches are rejected without equipping anything. The host never chooses a
preferred slot, treats a base class as its job, or selects the first match.

```lua
local gearset_for_job = {
    [19] = "Performance Paladin",
    [21] = 12,
}

local target_job_id = 19
local selector = gearset_for_job[target_job_id]
if selector ~= nil then
    local result = mop.actions.job(target_job_id, selector)
    if not result.ok then mop.log(result.status .. ": " .. result.message) end
end
```

Gearset descriptors contain `number`, `name`, and `class_job_id`. Resolution
and equip results contain `ok`, `status`, `message`, `gearset`, and
`candidates`, so scripts can handle `missing`, `ambiguous`, and `job_mismatch`
without parsing text.

These calls require declared game-action, chat/action-budget, or movement
resources as appropriate and consume the centralized action-rate quota. Scope
never broadcasts implicitly. The movement-only stop intentionally leaves the
owning Lua run alive; the legacy global Stop Movement command still stops Lua
runs as before.

### Bundle-local modules

Imported/exported script definitions may contain a `Modules` object whose keys
are dot-separated identifiers and whose values are Lua source:

```json
{
  "Modules": {
    "theatre.dialogue": "return { opening = 'Welcome.' }"
  }
}
```

```lua
local dialogue = require("theatre.dialogue")
mop.say(dialogue.opening)
```

Modules are editable in the Script Editor and cached per run. Names containing traversal, slashes, empty segments,
or non-identifier segments are rejected. A bundle supports at most 64 modules,
64 KiB per module, and 256 KiB of module source in total.

### Legacy flat API

| Function | Purpose |
| :--- | :--- |
| `mop.log(text)` | Writes a line to the plugin log. |
| `mop.wait(seconds)` | Pauses the script for 0 to 10 seconds without blocking the game thread. |
| `mop.time()` | Returns elapsed seconds for the current run. |
| `mop.is_running()` | Returns false after the run is stopped or cancelled. |
| `mop.get_name()` | Returns the current character's name. |
| `mop.get_slot()` | Returns this local client's zero-based synchronized participant slot. |
| `mop.get_count()` | Returns the synchronized participant count. |
| `mop.get_seed()` | Returns the shared random seed for the run. |
| `mop.get_run_target()` | Returns the captured dynamic run target, or `nil` when unavailable. |
| `mop.names_match(a, b)` | Compares character names using MoP's world/name normalization. |
| `mop.set_anchor(name)` | Selects the actor used as the origin of subsequent trajectory samples. |
| `mop.trajectory_update(x, z, facing)` | Publishes the next anchor-relative trajectory sample. |
| `mop.follow_actor(options)` | Continuously follows the first visible actor from an ordered fallback list. |
| `mop.chat(text)` | Queues one chat command or message. |
| `mop.say(text)` | Sends one `/say` message. |
| `mop.wait_for_chat(speaker, text, timeout)` | Waits for an exact `/say` line from the expected speaker. |

Every run has a unique synchronized ID and a maximum lifetime of ten minutes.
Use `/mop lua pause|resume|stop|restart|status [run-id|"Script Name"]`, or the
per-run controls in the Scripts window. Disjoint declared resources may coexist;
conflicting leases are rejected with the owning run ID.

Expand **Logs and diagnostics** below an active or most recent run to inspect a
bounded 200-line script/error console and the event queue's published,
consumed, and dropped counts. Finished-run diagnostics are retained with the
last 50 lifecycle history entries.

Plugin unload, logout/character transition, and territory changes cancel every
active run, stop Lua-owned movement immediately, and release resources through
the same terminal cleanup path as script completion and failure.

## Dynamic Actor Following

`mop.follow_actor` accepts an ordered list of possible anchors and movement policy values:

```lua
mop.follow_actor {
    anchors = { "Nearest Predecessor", "Earlier Predecessor", leader },
    offset_x = 0,
    offset_y = 0,
    offset_z = -0.8,
    face_anchor = true,
    facing_offset = 0,
    precision = 0.1,
    brake_at_position = true,
    pursuit_prediction = false,
    immediate_steering = true,
    rigid_formation = false,
    formation_radius = 0,
    neighbors = {},
    neighbor_correction = 0.25,
    maximum_neighbor_correction = 0.35,
    mirror_walk_run = false,
    mirror_sprint = false,
}
```

The controller reevaluates visibility continuously and follows the first visible candidate that is not the local player. The relative offset is rotated by the chosen anchor's current facing. Defaults are zero offset, face the anchor, `0.1` precision, braking enabled, pursuit prediction disabled, and immediate steering enabled.

This ordered fallback is what allows a chain to survive missing performers. If the nearest predecessor is absent, the follower attaches to the next visible predecessor. If a previously missing predecessor later becomes visible while its script is already running, it automatically becomes the preferred anchor.

With `rigid_formation = true`, all clients evolve the same rate-limited virtual leader frame. `formation_radius` lets the controller reserve enough movement speed for outside slots during a turn instead of rotating a wide grid faster than characters can physically travel. A script may also provide up to four visible `neighbors` as `{ name, offset_x, offset_y, offset_z }` entries. The bounded neighbor correction reduces small client-to-client observation differences without turning the grid into a follow-the-character chain. Reissuing `mop.follow_actor` for the same run and anchor updates offsets and policy in place, which enables live grid reflow.

`mirror_walk_run` infers a remote leader's gait and temporarily switches the follower between walk and run; followers still run while catching up. `mirror_sprint` mirrors status 50 (Sprint) on a best-effort basis, including removing the local Sprint status when the leader's Sprint ends.

Automatic Lua movement always cancels persistent emotes before movement is accepted. There is no general follow option to preserve an emote while translating.

## Script Coordination

Imported scripts can use the coordination API to share variables, identify
participants, and synchronize actions across clients. The plugin does not ship
or install a private performer roster.

## Relevant Files

* `MasterOfPuppets/Lua/LuaScriptRunner.cs`: compatibility facade for one run.
* `MasterOfPuppets/Lua/Runtime/`: sandbox, validation, module loader, deterministic random stream, and provider registry.
* `MasterOfPuppets/Lua/Providers/`: versioned runtime and legacy API providers.
* `MasterOfPuppets/Lua/LuaScriptManager.cs`: synchronized run lifecycle and cancellation.
* `MasterOfPuppets/Lua/LuaActorFollowController.cs`: dynamic actor resolution and following.
* `MasterOfPuppets/Lua/LuaTrajectoryController.cs`: anchor-relative continuous trajectories.
* `MasterOfPuppets/Lua/LuaScriptCatalog.cs`: script import/export and local development loading.
* `MasterOfPuppets/Ipc/IpcProvider.Lua.cs`: local IPC and cross-PC synchronization.
* `MasterOfPuppets/Game/ChatWatcher.cs`: `mopluarun` and `mopluastop` handling.
* `MasterOfPuppets/Ui/Windows/LuaScriptsWindow.cs`: Scripts list and convenience actions.
* `MasterOfPuppets/Ui/Windows/LuaScriptEditorWindow.cs`: script editor.
