# Universal Lua building blocks

Master Of Puppets Lua separates **observation**, **policy**, and **action**:

1. Observation discovers state from any player the local FFXIV client can see.
2. The Lua script decides whether and how to react.
3. Typed actions affect the local character executing that script.

An observed player does not need to exist in the plugin configuration, belong
to a formation or group, be in the party, or run Master Of Puppets. A remote
player and a character controlled by the same PC use the same actor APIs.

The only unavoidable boundary is the game client's own knowledge: an actor
that is out of range or unloaded cannot be observed until Dalamud exposes that
actor again. APIs report `missing`/`actor_lost` instead of inventing state.

## Require the capability

```lua
mop.capabilities.require("mop.game-state", "4.0.0")
```

Use `Name@World` when starting from a name. Once an actor is found, use its
string `game_object_id` or numeric `entity_id` when an exact live identity is
required. Game-object and content IDs remain strings to avoid Lua number
rounding.

## One-shot discovery

```lua
local result = mop.actors.find("Athena Potato@Sargatanas")

if result.status == "found" then
    local actor = result.actor
    mop.log(actor.name .. " is job " .. tostring(actor.class_job_id))
elseif result.status == "ambiguous" then
    error("Use Name@World or an actor ID")
else
    mop.log("Actor is not locally visible")
end
```

Lookup never silently chooses between ambiguous name matches.
The same resolver backs `mop.actors.watch`, `mop.actions.target_of`, and
Lua actor-follow anchors: exact live IDs win, one player-name match succeeds,
and ambiguous names are rejected rather than depending on object-table order.

## Shared low-overhead watches

```lua
local query = "Athena Potato@Sargatanas"
local watched = mop.actors.watch(query)

if watched.status ~= "found" then
    error("Source player is not uniquely visible: " .. watched.status
        .. " (matches: " .. tostring(watched.count) .. ")")
end

local revision = watched.revision

while mop.is_running() do
    local update = mop.actors.wait_changed(query, revision, 30)

    if update.status == "changed" then
        revision = update.revision

        if update.actor_status == "found" then
            local actor = update.actor
            if update.changes.class_job then
                mop.log("Job changed to " .. tostring(actor.class_job_id))
            end
            if update.changes.weapon then
                mop.log("Weapon state changed")
            end
            if update.changes.emote then
                mop.log("Emote state changed")
            end
        elseif update.changes.availability then
            mop.log("Actor visibility changed: " .. update.actor_status)
        end
    end
end

mop.actors.unwatch(watched.watch_id)
```

Each normalized query is acquired once and cached by the plugin. Multiple Lua
runs watching the same actor share that cached native observation. Steady-state
updates sample the cached actor directly; they do not enumerate the object
table. `wait_changed` checks the monotonic revision at 10 Hz and suspends the
Lua coroutine between checks, so it does not block Dalamud's framework thread.
All actor lookup and wait results expose `count`. Ambiguous waits return
`status = "ambiguous"` immediately instead of being mistaken for a lost actor.

The actor table includes:

- identity: `name`, `game_object_id`, `entity_id`;
- location: `position.x/y/z`, movement, walk, jump and jump sequence;
- targeting and availability;
- job, mount, companion, Sprint and weapon state;
- emote target/loop, pose, ornament, facewear, headgear and visor;
- online status.

The `changes` table groups those fields into stable boolean flags. Scripts only
need to inspect the groups they care about.

## Event-driven reactions

The simplest reaction API binds the event to the actor query for you:

```lua
local query = "Athena Potato@Sargatanas"
local jump = mop.actors.next_event(query, "jump", 30)

if jump.status == "event" and jump.data.is_jumping == "true" then
    mop.log("Jump " .. jump.data.jump_sequence)
end
```

The same function accepts `action`, `combat_action`, `general_action`,
`sprint`, `emote`, `emote_state`, `fashion_accessory`, `mount`, `facewear`,
`target`, `idle_pose`, and `weapon`. It preserves events for other actors and
other categories rather than consuming them while filtering.

For a combined event loop, watches publish `actor.state`, `actor.found`,
`actor.lost`, and the dedicated actor event names. Filter by the returned watch
ID because one run may watch several actors:

```lua
mop.capabilities.require("mop.events", "3.0.0")

local watched = mop.actors.watch("Athena Potato@Sargatanas")

while mop.is_running() do
    local event = mop.events.next(nil, 30)
    if event.status == "event"
        and event.data.watch_id == watched.watch_id then
        if event.name == "actor.state" then
            mop.log("Changed groups: " .. event.data.changes)
        elseif event.name == "actor.lost" then
            mop.log("Source unloaded; waiting for safe reacquisition")
        elseif event.name == "actor.found" then
            mop.log("Source is visible again")
        end
    end
end
```

Use watch revisions for state reconciliation and events for prompt edges. This
combination is resilient to bounded event-queue pressure: a script can always
read the latest cached state after receiving an event or timeout.

### Detection contracts

| Lua kind | Event | Authoritative observation |
| :--- | :--- | :--- |
| `combat_action` | `actor.combat_action` | Server-confirmed native action effect from the watched source. |
| `general_action` | `actor.general_action` | Native action effect when that general action produces one. |
| `jump` | `actor.jump` | Native local jump flag or bounded remote vertical-rise inference, with monotonic `jump_sequence`. |
| `sprint` | `actor.sprint` | Sprint status 50 entering or leaving the watched actor. |
| `emote` | `actor.emote_played` | Native emote playback edge, including short non-looping emotes. |
| `emote_state` | `actor.emote_state` | Persistent emote ID, target, loop, and stop transitions. |
| `fashion_accessory` | `actor.fashion_accessory` | Ornament/fashion-accessory ID transition, including removal to ID 0. |
| `mount` | `actor.mount` | Mount ID transition, including dismount to ID 0. |
| `facewear` | `actor.facewear` | Facewear ID transition, including removal to ID 0. |
| `target` | `actor.target` | Target game-object ID transition, including clearing to ID 0. |
| `idle_pose` | `actor.idle_pose` | Pose type or pose-state transition. |
| `weapon` | `actor.weapon` | Drawn/sheathed boolean transition. |

Check native edge availability before a long-running action/emote script:

```lua
local sources = mop.actors.event_sources()
if not sources.combat_actions then
    error("Native combat-action observation is unavailable")
end
```

Raw zone-wide `combat.action` and `emote.played` streams remain available for
discovery, but are opt-in to prevent unrelated combat traffic from flooding a
run. Call `mop.events.subscribe("combat.action")` before waiting for that raw
stream. Actor-scoped events need no subscription.

## Universal job mirroring with script policy

FFXIV class/job IDs are the observation contract. Gearset numbers and names are
local policy. The source player's gearset number is never needed.

```lua
mop.capabilities.require("mop.game-state", "4.0.0")
mop.capabilities.require("mop.actions", "4.0.0")

local gearset_for_job = {
    [33] = 40,                 -- local Astrologian gearset number
    [23] = "Performance Bard", -- or an exact local gearset name
}

local source = mop.target.snapshot("run")
if not source or source.object_kind ~= "Pc" or not source.is_loaded then
    error("A visible player run target is required")
end

local query = source.game_object_id
local observed = mop.actors.job(query)
if observed.status ~= "observed" then
    error("Could not observe source job: " .. observed.status)
end

local job_id = observed.class_job_id

local function apply_job(id)
    local selector = gearset_for_job[id]
    if selector == nil then
        mop.log("No local gearset policy for job " .. tostring(id))
        return
    end

    local result = mop.actions.job(id, selector)
    if not result.ok then mop.log(result.status .. ": " .. result.message) end
end

apply_job(job_id)

while mop.is_running() do
    local changed = mop.actors.wait_job_changed(
        query, job_id, 30, source.game_object_id)

    if changed.status == "changed" then
        job_id = changed.class_job_id
        apply_job(job_id)
    elseif changed.status == "timeout" then
        -- A bounded wait boundary; continue.
    elseif changed.status == "actor_lost"
        or changed.status == "actor_changed" then
        return
    else
        error("Job observation failed: " .. changed.status)
    end
end
```

This works for any observed player because job ID `33` always means
Astrologian. The script deliberately maps that universal ID to the executing
character's chosen local gearset. If several local gearsets have the same job,
the script's exact selector removes ambiguity.

## Run targets versus selected targets

Use `mop.target.snapshot("run")` for distributed scripts. The initiating client
captures the selected arbitrary player and transports the run-target identity
to recipients. Each recipient resolves that same player from its own local
object table. Do not depend on every recipient having the same UI target.

Use `selected` or `focus` only when the script intentionally follows that
specific local UI slot. Use `mop.actors.watch` for long-lived behavior because
it is independent of later UI target changes.

## Typed action boundary

Observing a remote player never grants control of that player. `mop.actions`
operates on the local executing character and validates resources, IDs,
eligibility and action quotas. A dynamic script therefore follows this pattern:

```lua
local source = mop.actors.watch("Some Player@World")
local self = mop.self.watch()

-- Observe source.actor, decide policy, then request a local typed action.
-- Always inspect result.ok/status/message.
```

For common general-action reactions, use `mop.actions.jump()` and
`mop.actions.sprint()`. For every other action, the script can pass the observed
Dalamud/FFXIV action kind and ID to the generic typed functions such as
`mop.actions.use_exact`, `use_exact_on`, or `use_ground_on`.

Keep selection policy in Lua. The host should expose facts and safe operations,
not silently choose gearsets, fallback actions, targets, or participants.
`mop.actions.target_of(query)` accepts any uniquely visible player or live actor
ID. The compatibility function `mop.actions.target_via_leader(id)` is also
roster-independent: if direct targeting fails, it may copy the requested target
from any visible real player already targeting exactly that ID.

## Performance checklist

- Create one watch per logical actor and reuse its `watch_id` and `revision`.
- Prefer `wait_changed` or events over repeatedly calling broad snapshots.
- Use `mop.actors.list()` only for discovery, not every frame.
- Reconcile from the latest watch after event timeouts or queue pressure.
- Use bounded waits so cancellation and pause remain responsive.
- Call `unwatch` when a long-running script no longer needs an actor.
- Avoid logging every poll; log state transitions and actionable failures.
- Keep action retries bounded and honor structured rejection results.

These rules preserve a small fixed amount of work per watched actor and avoid
work proportional to the entire visible world during steady-state reactions.
