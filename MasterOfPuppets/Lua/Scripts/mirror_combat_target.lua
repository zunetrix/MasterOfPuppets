-- Mirror Combat Target
-- Copy of Mirror Target Combat (revision 37), without selected-target changes.
-- Uses actor watches without targeting players to reveal them: the anchor must
-- already be visible/loadable. Ordinary actions use the client's current target;
-- ground actions may copy the observed position without changing selection.
-- Missing exact jobs/cosmetics/emotes are left unchanged.

local launch_anchor = mop.get_var("anchor") or ""
local explicit_target = mop.get_var("mop_run_target_explicit") == "true"
local target_name = explicit_target and launch_anchor or mop.get_run_target()
if not target_name or target_name == "" then
    error("Mirror Combat Target could not resolve the initiator or selected target")
end

mop.capabilities.require("mop.actions", "4.0.0")
mop.capabilities.require("mop.events", "3.0.0")
mop.capabilities.require("mop.game-state", "4.0.0")
-- This mirror consumes actor watches, not broad condition/party snapshots.
if mop.events.set_game_sampling then mop.events.set_game_sampling(false) end

-- Script-owned gearset policy. Values are exact local names or 1-based
-- gearset numbers. Unmapped jobs are deliberately left unchanged.
local gearset_for_job = {
    [33] = 40, -- Astrologian
    [23] = 41, -- Bard
    [25] = 42, -- Black Mage
    [36] = 43, -- Blue Mage
    [38] = 44, -- Dancer
    [32] = 45, -- Dark Knight
    [22] = 46, -- Dragoon
    [37] = 47, -- Gunbreaker
    [31] = 48, -- Machinist
    [20] = 49, -- Monk
    [30] = 50, -- Ninja
    [19] = 51, -- Paladin
    [42] = 52, -- Pictomancer
    [39] = 53, -- Reaper
    [35] = 54, -- Red Mage
    [40] = 55, -- Sage
    [34] = 56, -- Samurai
    [28] = 57, -- Scholar
    [27] = 58, -- Summoner
    [41] = 59, -- Viper
    [21] = 60, -- Warrior
    [24] = 61, -- White Mage
}

local function as_bool(value)
    return value == true or value == "true" or value == "1"
end

local function object_id(value)
    local numeric = tonumber(value) or 0
    if numeric == 0 or numeric == 3758096384 then return "0" end
    return tostring(value)
end

local function state_from(value)
    if not value then return nil end
    return {
        name = value.name or "",
        game_object_id = object_id(value.game_object_id),
        entity_id = tonumber(value.entity_id) or 0,
        target_game_object_id = object_id(value.target_game_object_id),
        is_dead = as_bool(value.is_dead),
        is_loaded = as_bool(value.is_loaded),
        is_moving = as_bool(value.is_moving),
        is_jumping = as_bool(value.is_jumping),
        jump_sequence = tonumber(value.jump_sequence) or 0,
        mount_id = tonumber(value.mount_id) or 0,
        companion_id = tonumber(value.companion_id) or 0,
        emote_id = tonumber(value.emote_id) or 0,
        is_emote_looping = as_bool(value.is_emote_looping),
        pose_type = tonumber(value.pose_type) or 0,
        pose_state = tonumber(value.pose_state) or 0,
        ornament_id = tonumber(value.ornament_id) or 0,
        facewear_id = tonumber(value.facewear_id) or 0,
        is_sprinting = as_bool(value.is_sprinting),
        is_weapon_drawn = as_bool(value.is_weapon_drawn),
        online_status_id = tonumber(value.online_status_id) or 0,
        online_status_name = value.online_status_name or "",
        class_job_id = tonumber(value.class_job_id) or 0,
    }
end

local function action_kind(action_type)
    if action_type == 1 then return "action" end
    if action_type == 11 then return "pet_action" end
    if action_type == 14 then return "pvp_action" end
    return nil
end

local function report(label, id, result)
    if result and not result.ok then
        mop.log("Unable to mirror " .. label .. " " .. tostring(id)
            .. ": " .. (result.message or "unknown error"))
    end
    return result
end

local function wait_for_watch(name)
    local watch = mop.actors.watch(name)
    while mop.is_running() do
        if watch.status == "found" and watch.actor then
            return watch.watch_id, state_from(watch.actor)
        end
        local event = mop.events.next(nil, 0.20)
        if event.status == "event"
            and event.name == "actor.found"
            and event.data.watch_id == watch.watch_id then
            return watch.watch_id, state_from(event.data)
        end
        watch = mop.actors.watch(name)
    end
    return watch.watch_id, nil
end

local self_watch = mop.self.watch()
local self_watch_id = self_watch.watch_id
local self_state = state_from(self_watch.actor)
local target_watch_id, target_state = wait_for_watch(target_name)
if not target_state then return end

local local_is_target = self_state and self_state.entity_id == target_state.entity_id
local target_lost = false
local force_reconcile = true
local last_global_sequence = ""
local unavailable = {}
local retry_at = {}
local pending_job = nil
local pending_weapon = nil
local pending_pose = nil
local pending_status = nil
local pending_jump = nil
local pending_sprint = nil
local pending_emote_stop = nil
-- Actor snapshots can clear EmoteId/IsInEmoteLoop before the visual base
-- animation has actually stopped. Retain ownership of every persistent emote
-- started by this mirror until the direct native stop succeeds.
local mirrored_persistent_emote_id = nil
local emote_stop_exhausted = false
local next_emote_stop_call = 0
local pending_combat = nil
-- Actor watches deliver state changes immediately; this is only a bounded
-- recovery poll for missed/unavailable watch events.
local next_reconcile = mop.time() + 2.0
local next_reconcile_pass = 0
local weapon_attempts = {}
local pose_attempts = {}

local function unsupported_result(result)
    local message = result and result.message or ""
    return string.find(message, "not unlocked", 1, true)
        or string.find(message, "no local gearset", 1, true)
        or string.find(message, "is unavailable", 1, true)
        or string.find(message, "unknown ", 1, true)
end

local function exact_use(kind, id, persistent)
    local key = kind .. ":" .. tostring(id)
    if unavailable[key] or (retry_at[key] or 0) > mop.time() then return nil end
    -- Never inject an emote after locomotion has begun. Doing so bypasses the
    -- game's normal emote/movement exclusion and creates the sliding-emote
    -- state even though IsInEmoteLoop() can remain false.
    if kind == "emote" and self_state and self_state.is_moving then return nil end
    local result
    if persistent == nil then
        result = mop.actions.use_exact(kind, id, "local")
    else
        result = mop.actions.use_exact(kind, id, "local", persistent)
    end
    if not result.ok then
        -- An emote failure is not recoverable during this run when the local
        -- character does not own the target's emote. Cache it immediately so
        -- actor-state reconciliation cannot turn one missing emote into a
        -- repeated native-call and log storm.
        if kind == "emote" or unsupported_result(result) then unavailable[key] = true end
        retry_at[key] = mop.time() + 1.0
        report(kind, id, result)
    else
        retry_at[key] = mop.time() + 0.35
        if kind == "emote" then
            mirrored_persistent_emote_id = persistent == true and id or nil
            if persistent == true then emote_stop_exhausted = false end
        end
    end
    return result
end

local function request_emote_stop(reason, force)
    if emote_stop_exhausted then return false end
    local has_mirrored_loop = mirrored_persistent_emote_id ~= nil
    local has_observed_emote = self_state
        and self_state.is_emote_looping
    if not self_state or (not force and not has_mirrored_loop and not has_observed_emote) then
        pending_emote_stop = nil
        emote_stop_exhausted = false
        return false
    end
    local now = mop.time()
    if now < next_emote_stop_call then return pending_emote_stop ~= nil end
    if not pending_emote_stop or now >= pending_emote_stop.next_try then
        local call_ok, raw_result = pcall(function() return mop.actions.stop_emote() end)
        local result = call_ok and raw_result or { ok = false, message = tostring(raw_result) }
        next_emote_stop_call = now + 0.25
        result = report("emote stop (" .. reason .. ")", 0, result)
        if result and result.ok then
            mirrored_persistent_emote_id = nil
            pending_emote_stop = nil
            emote_stop_exhausted = false
            return true
        end
        local previous_stop = pending_emote_stop
        pending_emote_stop = {
            attempts = (previous_stop and previous_stop.attempts or 0) + 1,
            next_try = now + 0.10,
            deadline = previous_stop and previous_stop.deadline or now + 1.0,
        }
    end
    return true
end

local function request_job(id)
    local key = "job:" .. tostring(id)
    if unavailable[key] then return false end
    local selector = gearset_for_job[id]
    if selector == nil then
        unavailable[key] = true
        mop.log("No gearset selector is coded for class/job " .. tostring(id))
        return false
    end
    local result = report("class/job", id, mop.actions.job(id, selector))
    if result and not result.ok and unsupported_result(result) then
        unavailable[key] = true
        pending_job = nil
        return false
    end
    pending_job = {
        id = id,
        attempts = pending_job and pending_job.id == id and pending_job.attempts + 1 or 1,
        next_try = mop.time() + 0.35,
    }
    return true
end

local function request_weapon(drawn)
    local key = tostring(drawn)
    if (weapon_attempts[key] or 0) >= 3 then return end
    if self_state and (self_state.is_emote_looping
        or mirrored_persistent_emote_id ~= nil) then
        request_emote_stop("weapon transition", true)
        return
    end
    report("weapon", drawn and 1 or 0, mop.actions.weapon(drawn))
    weapon_attempts[key] = (weapon_attempts[key] or 0) + 1
    pending_weapon = {
        drawn = drawn,
        attempts = pending_weapon and pending_weapon.drawn == drawn and pending_weapon.attempts + 1 or 1,
        next_try = mop.time() + 0.20,
    }
end

local function request_pose(pose_type, pose_state)
    local key = "pose:" .. tostring(pose_type) .. ":" .. tostring(pose_state)
    if unavailable[key] then return end
    -- Change Pose only selects a variant within the current pose type.
    if not self_state or self_state.pose_type ~= pose_type
        or self_state.is_moving or self_state.is_jumping then
        pending_pose = nil
        return
    end
    if (pose_attempts[key] or 0) >= 3 then return end
    pose_attempts[key] = (pose_attempts[key] or 0) + 1
    local result = report("pose", pose_state, mop.actions.pose(pose_type, pose_state))
    if result and not result.ok then
        unavailable[key] = true
        pending_pose = nil
        return
    end
    pending_pose = {
        pose_type = pose_type,
        pose_state = pose_state,
        attempts = pending_pose and pending_pose.pose_type == pose_type
            and pending_pose.pose_state == pose_state and pending_pose.attempts + 1 or 1,
        next_try = mop.time() + 0.35,
    }
end

local function request_online_status(id, name)
    local key = "status:" .. tostring(id)
    if unavailable[key] then return end
    local result = report("online status", id, mop.actions.online_status(id, name))
    if not result or not result.ok then
        unavailable[key] = true
        pending_status = nil
        return
    end
    -- The native request has no eligibility return value. Treat the actor's
    -- verified state as the answer: if it has not changed within this window,
    -- the server rejected that status for this character (for example
    -- /nastatus on a character no longer eligible for New Adventurer).
    pending_status = {
        id = id,
        name = name,
        deadline = mop.time() + 2.0,
    }
end

local function request_jump(sequence)
    if local_is_target or not self_state or self_state.is_jumping then return end
    local now = mop.time()
    if self_state.is_emote_looping
        or mirrored_persistent_emote_id ~= nil then
        request_emote_stop("jump", true)
        if not pending_jump or pending_jump.sequence ~= sequence then
            pending_jump = {
                sequence = sequence,
                baseline = self_state.jump_sequence,
                attempts = 0,
                next_try = now + 0.25,
                deadline = now + 1.0,
            }
        end
        return
    end
    report("jump", 2, mop.actions.jump())
    pending_jump = {
        sequence = sequence,
        baseline = self_state.jump_sequence,
        attempts = 1,
        next_try = now + 0.25,
        deadline = now + 0.25,
    }
end

local function request_sprint()
    if local_is_target or (self_state and self_state.is_sprinting) then return end
    report("sprint", 3, mop.actions.sprint())
    pending_sprint = {
        attempts = pending_sprint and pending_sprint.attempts + 1 or 1,
        next_try = mop.time() + 0.20,
        deadline = mop.time() + 1.40,
    }
end

local function reconcile()
    local now = mop.time()
    if now < next_reconcile_pass then return end
    next_reconcile_pass = now + 0.10
    force_reconcile = false
    if target_lost or not target_state or not self_state or local_is_target then return end
    if not target_state.is_loaded or target_state.is_dead or not self_state.is_loaded or self_state.is_dead then
        force_reconcile = true
        return
    end

    -- Job is a barrier: other actions wait until the exact gearset is active.
    -- If that gearset does not exist, only the job change is skipped.
    if target_state.class_job_id > 0
        and self_state.class_job_id ~= target_state.class_job_id
        and not unavailable["job:" .. tostring(target_state.class_job_id)] then
        if pending_job and pending_job.id == target_state.class_job_id
            and pending_job.attempts >= 8 then
            unavailable["job:" .. tostring(target_state.class_job_id)] = true
            mop.log("Exact job " .. tostring(target_state.class_job_id)
                .. " did not activate after 8 direct attempts; continuing the remaining mirror state")
            pending_job = nil
        elseif not pending_job or pending_job.id ~= target_state.class_job_id
            or mop.time() >= pending_job.next_try then
            request_job(target_state.class_job_id)
        end
        if not unavailable["job:" .. tostring(target_state.class_job_id)] then return end
    end
    pending_job = nil

    local target_wants_loop = target_state.is_emote_looping
        and not target_state.is_moving
        and not target_state.is_jumping
        and target_state.emote_id > 0
    local must_stop_loop = (self_state.is_emote_looping
        or mirrored_persistent_emote_id ~= nil) and (
        not target_wants_loop or self_state.is_moving
        or self_state.is_weapon_drawn ~= target_state.is_weapon_drawn
        or pending_combat ~= nil)
    if must_stop_loop and request_emote_stop("source stopped or movement/action began", true) then return end

    if self_state.mount_id ~= target_state.mount_id then
        if target_state.mount_id > 0 then exact_use("mount", target_state.mount_id, nil)
        elseif self_state.mount_id > 0 and (retry_at.dismount or 0) <= mop.time() then
            report("dismount", 23, mop.actions.use("general_action", 23, "local"))
            retry_at.dismount = mop.time() + 0.75
        end
    end
    if self_state.companion_id ~= target_state.companion_id then
        if target_state.companion_id > 0 then exact_use("minion", target_state.companion_id, nil)
        elseif self_state.companion_id > 0 and (retry_at.stop_minion or 0) <= mop.time() then
            report("minion dismissal", 0, mop.actions.stop_cosmetic("minion"))
            retry_at.stop_minion = mop.time() + 0.75
        end
    end
    if self_state.ornament_id ~= target_state.ornament_id then
        if target_state.ornament_id > 0 then exact_use("fashion_accessory", target_state.ornament_id, nil)
        elseif self_state.ornament_id > 0 and (retry_at.stop_accessory or 0) <= mop.time() then
            report("accessory dismissal", 0, mop.actions.stop_cosmetic("fashion_accessory"))
            retry_at.stop_accessory = mop.time() + 0.75
        end
    end
    if self_state.facewear_id ~= target_state.facewear_id then
        if target_state.facewear_id > 0 then exact_use("facewear", target_state.facewear_id, nil)
        elseif self_state.facewear_id > 0 and (retry_at.stop_facewear or 0) <= mop.time() then
            report("facewear removal", 0, mop.actions.stop_cosmetic("facewear"))
            retry_at.stop_facewear = mop.time() + 0.75
        end
    end

    -- Weapon transitions are a barrier. Finish and verify the draw/sheath
    -- before a stale target emote snapshot can replay the animation.
    if self_state.is_weapon_drawn ~= target_state.is_weapon_drawn then
        if not pending_weapon or mop.time() >= pending_weapon.next_try then
            request_weapon(target_state.is_weapon_drawn)
        end
        return
    else
        pending_weapon = nil
    end

    if target_wants_loop
        and (not self_state.is_emote_looping or self_state.emote_id ~= target_state.emote_id) then
        exact_use("emote", target_state.emote_id, true)
    end

    if not target_wants_loop and not self_state.is_emote_looping
        and (self_state.pose_type ~= target_state.pose_type
            or self_state.pose_state ~= target_state.pose_state)
        and (not pending_pose or mop.time() >= pending_pose.next_try) then
        request_pose(target_state.pose_type, target_state.pose_state)
    end

    local status_key = "status:" .. tostring(target_state.online_status_id)
    if self_state.online_status_id ~= target_state.online_status_id
        and not unavailable[status_key]
        and (not pending_status or pending_status.id ~= target_state.online_status_id) then
        request_online_status(target_state.online_status_id, target_state.online_status_name)
    elseif self_state.online_status_id == target_state.online_status_id then
        pending_status = nil
    end
    if target_state.is_sprinting and not self_state.is_sprinting and not pending_sprint then request_sprint() end
    force_reconcile = false
end

local function apply_self(next_state)
    local previous = self_state
    self_state = next_state
    if not self_state then return end
    local_is_target = target_state and self_state.entity_id == target_state.entity_id
    if previous and ((previous.is_dead and not self_state.is_dead)
        or (not previous.is_loaded and self_state.is_loaded)) then
        pending_job, pending_weapon, pending_pose, pending_status = nil, nil, nil, nil
        pending_jump, pending_sprint, pending_emote_stop, pending_combat = nil, nil, nil, nil
        emote_stop_exhausted = false
        force_reconcile = true
    end
    if pending_emote_stop and mirrored_persistent_emote_id == nil
        and not self_state.is_emote_looping then
        pending_emote_stop = nil
        emote_stop_exhausted = false
    end
    if self_state.is_moving and (self_state.is_emote_looping
        or mirrored_persistent_emote_id ~= nil) then
        request_emote_stop("local movement safety", true)
    end
    if pending_weapon and self_state.is_weapon_drawn == pending_weapon.drawn then pending_weapon = nil end
    if pending_job and self_state.class_job_id == pending_job.id then pending_job = nil; force_reconcile = true end
    if pending_pose and self_state.pose_type == pending_pose.pose_type
        and self_state.pose_state == pending_pose.pose_state then pending_pose = nil end
    if pending_status and self_state.online_status_id == pending_status.id then pending_status = nil end
    if pending_jump and (self_state.is_jumping or self_state.jump_sequence > pending_jump.baseline) then pending_jump = nil end
    if pending_sprint and self_state.is_sprinting then pending_sprint = nil end
end

local function apply_target(next_state, found)
    if not next_state then return end
    local previous = target_state
    target_state = next_state
    if not previous or previous.is_weapon_drawn ~= target_state.is_weapon_drawn or found then
        weapon_attempts = {}
    end
    if not previous or previous.pose_type ~= target_state.pose_type
        or previous.pose_state ~= target_state.pose_state or found then
        pose_attempts = {}
    end
    if pending_status and pending_status.id ~= target_state.online_status_id then pending_status = nil end
    target_lost = false
    local_is_target = self_state and self_state.entity_id == target_state.entity_id
    if found or not previous or previous.is_dead ~= target_state.is_dead
        or previous.is_loaded ~= target_state.is_loaded then force_reconcile = true end
    if previous and target_state.jump_sequence > previous.jump_sequence then request_jump(target_state.jump_sequence) end
    if target_state.is_sprinting and (not previous or not previous.is_sprinting) then request_sprint() end
    if previous and previous.is_emote_looping and not target_state.is_emote_looping then
        request_emote_stop("source ended persistent emote")
    end
    if previous and target_state.emote_id > 0
        and target_state.emote_id ~= previous.emote_id
        and not target_state.is_emote_looping then
        exact_use("emote", target_state.emote_id, false)
    end
    reconcile()
end

local function run_pending_combat()
    if not pending_combat or not self_state or self_state.is_emote_looping
        or mirrored_persistent_emote_id ~= nil then return end
    local action = pending_combat
    pending_combat = nil
    local result
    if action.is_ground_targeted then
        if action.has_position then
            result = mop.actions.use_ground_on(action.kind, action.id, action.target_id,
                action.x, action.y, action.z)
        else
            result = mop.actions.use_ground_on(action.kind, action.id, action.target_id)
        end
    else
        result = mop.actions.use(action.kind, action.id, "local")
    end
    report("action", action.id, result)
end

local function process_retries()
    local now = mop.time()
    if pending_emote_stop and self_state
        and (self_state.is_emote_looping
            or mirrored_persistent_emote_id ~= nil)
        and now >= pending_emote_stop.next_try then
        if now >= pending_emote_stop.deadline or pending_emote_stop.attempts >= 8 then
            mop.log("Unable to verify persistent-emote stop after 8 direct attempts")
            pending_emote_stop = nil
            emote_stop_exhausted = true
        else request_emote_stop("verification retry") end
    end
    if pending_jump and now >= (pending_jump.next_try or 0) then
        if now >= (pending_jump.deadline or now + 1)
            or pending_jump.attempts >= 1 then
            pending_jump = nil
        else request_jump(pending_jump.sequence) end
    end
    if pending_sprint and now >= pending_sprint.next_try then
        if now >= pending_sprint.deadline or pending_sprint.attempts >= 5 then pending_sprint = nil
        else request_sprint() end
    end
    if pending_weapon and now >= pending_weapon.next_try then
        if pending_weapon.attempts >= 8 then pending_weapon = nil
        else request_weapon(pending_weapon.drawn) end
    end
    if pending_status and now >= pending_status.deadline then
        -- Refresh the local actor immediately before deciding eligibility so a
        -- delayed watch event cannot turn a successful status change into a
        -- false rejection.
        local tested_status = pending_status
        local refreshed_self = mop.self.watch()
        if refreshed_self.status == "found" then apply_self(state_from(refreshed_self.actor)) end
        local key = "status:" .. tostring(tested_status.id)
        if (not self_state or self_state.online_status_id ~= tested_status.id)
            and pending_status and pending_status.id == tested_status.id then
            unavailable[key] = true
            mop.log("Online status " .. (tested_status.name or tostring(tested_status.id))
                .. " is not eligible on this character; it will not be retried")
        end
        pending_status = nil
    end
    run_pending_combat()
    reconcile()
end

local function switch_target(name)
    if mirrored_persistent_emote_id ~= nil then
        request_emote_stop("mirror target changed", true)
    end
    mop.log("Mirror target change requested; waiting for " .. name)
    mop.actors.unwatch(target_watch_id)
    target_name = name
    target_watch_id, target_state = wait_for_watch(target_name)
    if not target_state then return false end
    last_global_sequence = ""
    pending_job, pending_weapon, pending_pose, pending_status = nil, nil, nil, nil
    pending_jump, pending_sprint, pending_emote_stop, pending_combat = nil, nil, nil, nil
    emote_stop_exhausted = false
    target_lost = false
    force_reconcile = true
    reconcile()
    mop.log("Now mirroring " .. target_name .. " through direct game functions")
    return true
end

mop.log("Mirroring " .. target_name .. " through direct game functions; exact owned actions only")
reconcile()

while mop.is_running() do
    local event = mop.events.next(nil, 0.25)
    if event.status == "event" then
        if (event.name == "actor.state" or event.name == "actor.found")
            and event.data.watch_id == self_watch_id then
            apply_self(state_from(event.data))
            reconcile()
        elseif (event.name == "actor.state" or event.name == "actor.found")
            and event.data.watch_id == target_watch_id then
            apply_target(state_from(event.data), event.name == "actor.found")
        elseif event.name == "actor.lost" and event.data.watch_id == target_watch_id then
            target_lost = true
            force_reconcile = true
            if mirrored_persistent_emote_id ~= nil then
                request_emote_stop("mirror target unloaded", true)
            end
            mop.log("Mirror target unloaded; waiting safely for the same actor to reload")
        elseif event.name == "run.variables-updated" then
            local requested = mop.get_var("anchor")
            if requested and requested ~= "" and not mop.names_match(requested, target_name) then
                if not switch_target(requested) then return end
            end
        elseif event.name == "actor.emote_played" then
            local source_entity_id = tonumber(event.data.source_entity_id or "0") or 0
            local emote_id = tonumber(event.data.emote_id or "0") or 0
            if self_state and source_entity_id == self_state.entity_id and self_state.is_moving then
                request_emote_stop("emote began during local movement", true)
            elseif target_state and source_entity_id == target_state.entity_id
                and emote_id > 0 and not local_is_target then
                exact_use("emote", emote_id, as_bool(event.data.is_persistent))
            end
        elseif event.name == "actor.combat_action" then
            local source_entity_id = tonumber(event.data.source_entity_id or "0") or 0
            local sequence = event.data.global_sequence or ""
            local id = tonumber(event.data.action_id or "0") or 0
            local kind = action_kind(tonumber(event.data.action_type or "0") or 0)
            if target_state and source_entity_id == target_state.entity_id
                and sequence ~= "" and sequence ~= last_global_sequence and id > 0 and kind then
                last_global_sequence = sequence
                if not local_is_target then
                    local animation_target = object_id(event.data.animation_target_id)
                    local ground_target = animation_target
                    if ground_target == "0" then
                        ground_target = object_id(target_state.target_game_object_id)
                    end
                    if ground_target == "0" then
                        ground_target = object_id(target_state.game_object_id)
                    end
                    local ground_x = tonumber(event.data.target_x)
                    local ground_y = tonumber(event.data.target_y)
                    local ground_z = tonumber(event.data.target_z)
                    pending_combat = {
                        kind = kind,
                        id = id,
                        target_id = as_bool(event.data.is_ground_targeted)
                            and ground_target or animation_target,
                        is_ground_targeted = as_bool(event.data.is_ground_targeted),
                        has_position = ground_x ~= nil and ground_y ~= nil and ground_z ~= nil,
                        x = ground_x,
                        y = ground_y,
                        z = ground_z,
                    }
                    if self_state and (self_state.is_emote_looping
                        or mirrored_persistent_emote_id ~= nil) then
                        request_emote_stop("combat action", true)
                    end
                    run_pending_combat()
                end
            end
        end
    end

    process_retries()
    local now = mop.time()
    if now >= next_reconcile then
        local refreshed_self = mop.self.watch()
        if refreshed_self.status == "found" then apply_self(state_from(refreshed_self.actor)) end
        local refreshed_target = mop.actors.watch(target_name)
        if refreshed_target.status == "found" then
            apply_target(state_from(refreshed_target.actor), false)
        else
            target_lost = true
        end
        reconcile()
        next_reconcile = now + 2.0
    end
end

-- Stopping the mirror must not strand an animation that this run started.
if mirrored_persistent_emote_id ~= nil then mop.actions.stop_emote() end
