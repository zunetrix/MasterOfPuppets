using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Watches;
using MasterOfPuppets.Movement;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets.LuaScripting.Automation;

internal sealed class PluginLuaActionFacade : ILuaActionFacade {
    private readonly Plugin _plugin;
    private readonly Action<LuaResourceKind, string> _ensureResource;

    public PluginLuaActionFacade(Plugin plugin, Action<LuaResourceKind, string> ensureResource) {
        _plugin = plugin;
        _ensureResource = ensureResource;
    }

    public Task<LuaAutomationResult> ExecuteTextAsync(string text, string scope, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.ChatActionBudget | LuaResourceKind.GameActions, "commands.execute");
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Contains('\r') || text.Contains('\n') || text.Contains('\0'))
            return Task.FromResult(LuaAutomationResult.Rejected("text command must be one non-empty line"));
        if (Encoding.UTF8.GetByteCount(text) > 500)
            return Task.FromResult(LuaAutomationResult.Rejected("text command exceeds 500 UTF-8 bytes"));
        return OnFramework(() => {
            switch (NormalizeScope(scope)) {
                case "local":
                    Chat.SendMessage(text);
                    break;
                case "current_pc":
                    _plugin.IpcProvider.ExecuteTextCommand(text, includeSelf: true);
                    break;
                default:
                    return LuaAutomationResult.Rejected("command scope must be 'local' or 'current_pc'");
            }
            return LuaAutomationResult.Queued("text command dispatched");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> UseActionAsync(string kind, uint id, string scope, bool? persistent, CancellationToken cancellationToken) =>
        UseActionCoreAsync(kind, id, scope, persistent, allowFallback: true, cancellationToken);

    public Task<LuaAutomationResult> UseExactActionAsync(string kind, uint id, string scope, bool? persistent, CancellationToken cancellationToken) =>
        UseActionCoreAsync(kind, id, scope, persistent, allowFallback: false, cancellationToken);

    public Task<LuaFallbackCandidateSet> GetFallbackCandidatesAsync(
        string kind,
        uint id,
        bool? persistent,
        CancellationToken cancellationToken) => OnFramework(() => {
            var result = CosmeticActionFallbackResolver.EligibleFallbacks(kind, id, persistent);
            return new LuaFallbackCandidateSet(
                result.Success,
                result.Kind,
                result.RequestedId,
                result.Category,
                result.CandidateIds,
                result.UniverseIds,
                result.EligibilityToken,
                result.UniverseSignature,
                result.Message);
        }, cancellationToken);

    private Task<LuaAutomationResult> UseActionCoreAsync(string kind, uint id, string scope, bool? persistent, bool allowFallback, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.use");
        if (id == 0)
            return Task.FromResult(LuaAutomationResult.Rejected("action ID must be greater than zero"));
        return OnFramework(() => {
            var normalizedKind = kind?.Trim().ToLowerInvariant().Replace('-', '_') ?? string.Empty;
            var normalizedScope = NormalizeScope(scope);
            if (normalizedScope is not ("local" or "current_pc"))
                return LuaAutomationResult.Rejected("action scope must be 'local' or 'current_pc'");
            if (normalizedScope == "local") {
                switch (normalizedKind) {
                    case "action": GameActionManager.UseAction(id); break;
                    case "pvp" or "pvp_action": GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.PvPAction, id); break;
                    case "pet" or "pet_action": GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.PetAction, id); break;
                    case "general" or "general_action": GameActionManager.UseGeneralAction(id); break;
                    case "mount" or "companion" or "minion" or "emote" or "facewear"
                        or "ornament" or "accessory" or "fashion" or "fashion_accessory":
                        return ExecuteCosmetic(normalizedKind, id, persistent, allowFallback);
                    case "item": GameActionManager.UseItem(id); break;
                    default: return LuaAutomationResult.Rejected("action kind must be action, pvp_action, pet_action, general_action, mount, companion/minion, emote, facewear, fashion_accessory, or item");
                }
            } else {
                switch (normalizedKind) {
                    case "action": _plugin.IpcProvider.ExecuteActionCommand(id); break;
                    case "general" or "general_action": _plugin.IpcProvider.ExecuteGeneralActionCommand(id); break;
                    case "item": _plugin.IpcProvider.ExecuteItemCommand(id); break;
                    default: return LuaAutomationResult.Rejected("broadcast action kind must be action, general_action, or item");
                }
            }
            return LuaAutomationResult.Queued($"{normalizedKind} {id} dispatched");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> UseActionOnAsync(string kind, uint id, ulong targetId, bool? persistent, CancellationToken cancellationToken) =>
        UseActionOnCoreAsync(kind, id, targetId, persistent, allowFallback: true, cancellationToken);

    public Task<LuaAutomationResult> UseExactActionOnAsync(string kind, uint id, ulong targetId, bool? persistent, CancellationToken cancellationToken) =>
        UseActionOnCoreAsync(kind, id, targetId, persistent, allowFallback: false, cancellationToken);

    private Task<LuaAutomationResult> UseActionOnCoreAsync(string kind, uint id, ulong targetId, bool? persistent, bool allowFallback, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.use_on");
        if (id == 0)
            return Task.FromResult(LuaAutomationResult.Rejected("action ID must be greater than zero"));
        if (targetId == 0)
            return Task.FromResult(LuaAutomationResult.Rejected("target ID must be greater than zero"));
        return OnFramework(() => {
            var normalizedKind = kind?.Trim().ToLowerInvariant().Replace('-', '_') ?? string.Empty;
            var target = DalamudApi.ObjectTable.FirstOrDefault(candidate =>
                candidate != null && candidate.GameObjectId == targetId);
            var currentTargetMatches = DalamudApi.ObjectTable.LocalPlayer?.TargetObjectId == targetId;
            if (target == null && !currentTargetMatches)
                return LuaAutomationResult.Rejected($"target {targetId} is not loaded on this client");

            if (normalizedKind == "emote") {
                var resolved = CosmeticActionFallbackResolver.Resolve("emote", id, persistent, allowFallback);
                if (!resolved.Success)
                    return LuaAutomationResult.Rejected(resolved.Message);
                if (target != null)
                    DalamudApi.TargetManager.Target = target;
                var dispatched = ExecuteNativeEmote(resolved.ActionId);
                return dispatched.Success
                    ? LuaAutomationResult.Queued($"{resolved.Message}; targeted and dispatched to {(target?.Name.TextValue ?? targetId.ToString())}")
                    : dispatched;
            }

            var type = normalizedKind switch {
                "action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action,
                "pvp" or "pvp_action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.PvPAction,
                "pet" or "pet_action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.PetAction,
                _ => FFXIVClientStructs.FFXIV.Client.Game.ActionType.None,
            };
            if (type == FFXIVClientStructs.FFXIV.Client.Game.ActionType.None)
                return LuaAutomationResult.Rejected("targeted action kind must be action, pvp_action, pet_action, or emote");

            if (target != null)
                DalamudApi.TargetManager.Target = target;
            GameActionManager.UseAction(type, id, targetId);
            return LuaAutomationResult.Queued($"{normalizedKind} {id} targeted and dispatched to {(target?.Name.TextValue ?? targetId.ToString())}");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> UseGroundActionOnAsync(
        string kind,
        uint id,
        ulong targetId,
        Vector3? fallbackPosition,
        CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.use_ground_on");
        if (id == 0)
            return Task.FromResult(LuaAutomationResult.Rejected("action ID must be greater than zero"));
        return OnFramework(() => {
            var normalizedKind = kind?.Trim().ToLowerInvariant().Replace('-', '_') ?? string.Empty;
            var type = normalizedKind switch {
                "action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action,
                "pvp" or "pvp_action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.PvPAction,
                "pet" or "pet_action" => FFXIVClientStructs.FFXIV.Client.Game.ActionType.PetAction,
                _ => FFXIVClientStructs.FFXIV.Client.Game.ActionType.None,
            };
            if (type == FFXIVClientStructs.FFXIV.Client.Game.ActionType.None)
                return LuaAutomationResult.Rejected("ground action kind must be action, pvp_action, or pet_action");
            if (type == FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action
                && !ActionHelper.IsGroundTargeted(id))
                return LuaAutomationResult.Rejected($"action {id} is not marked as ground-targeted in game data");

            var location = TryGetActorPosition(targetId, out var actorPosition)
                ? actorPosition
                : fallbackPosition;
            if (!location.HasValue)
                return LuaAutomationResult.Rejected($"no loaded target or observed ground position is available for action {id}");

            var nativeTargetId = targetId is 0 or 0xE0000000 ? 0xE0000000UL : targetId;
            var accepted = GameActionManager.UseActionLocation(type, id, nativeTargetId, location.Value);
            return accepted
                ? LuaAutomationResult.Queued(
                    $"ground action {id} submitted directly at ({location.Value.X:F2}, {location.Value.Y:F2}, {location.Value.Z:F2})")
                : LuaAutomationResult.Rejected($"game rejected direct ground placement for action {id}");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> StopCosmeticAsync(string kind, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.stop_cosmetic");
        return OnFramework(() => StopCosmetic(kind), cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> StopEmoteAsync(CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.stop_emote");
        return OnFramework(() => {
            var player = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.GetLocalPlayer();
            if (player == null)
                return LuaAutomationResult.Rejected("local player is unavailable");

            var character = &player->Character;
            var hasActiveEmote = character->EmoteController.EmoteId != 0
                || character->Timeline.BaseOverride != 0
                || character->Timeline.LipsOverride != 0
                || character->EmoteController.IsEmoting()
                || character->EmoteController.IsInEmoteLoop()
                || character->Mode is CharacterModes.EmoteLoop or CharacterModes.InPositionLoop;
            if (!hasActiveEmote)
                return LuaAutomationResult.Completed("local player has no active emote to stop");

            // This is the same in-place loop-exit command issued by the game.
            // Unlike clearing timeline/controller fields, it performs a real
            // game-state transition that is visible to other clients.
            const int emoteLoopExitInPlaceCommand = 0x1F7;
            if (FFXIVClientStructs.FFXIV.Client.Game.GameMain.ExecuteCommand(
                    emoteLoopExitInPlaceCommand, 0, 0, 0, 0))
                return LuaAutomationResult.Queued("game accepted the in-place emote stop");

            // Some states reject the in-place command. Jump is FFXIV's public,
            // network-visible universal interruption for a looping emote.
            var actionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
            if (actionManager != null && actionManager->UseAction(
                    FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction,
                    2))
                return LuaAutomationResult.Queued(
                    "in-place emote stop was unavailable; game accepted a jump interruption");

            return LuaAutomationResult.Rejected("game rejected both emote-stop methods");
        }, cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> SetPoseAsync(byte poseType, byte poseState, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.pose");
        return OnFramework(() => {
            var player = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.GetLocalPlayer();
            if (player == null)
                return LuaAutomationResult.Rejected("local player is unavailable");

            var requestedType = (EmoteController.PoseType)poseType;
            if (!Enum.IsDefined(requestedType))
                return LuaAutomationResult.Rejected($"pose type {poseType} is invalid");

            var controller = &player->Character.EmoteController;
            var currentType = controller->CurrentPoseType;
            if ((byte)currentType == byte.MaxValue) {
                var poseKind = (EmoteController.PoseType)controller->GetPoseKind();
                currentType = Enum.IsDefined(poseKind) ? poseKind : EmoteController.PoseType.Idle;
            }
            if (currentType != requestedType)
                return LuaAutomationResult.Rejected($"local pose type {(byte)currentType} does not match target pose type {poseType}");

            var maximumState = EmoteController.GetAvailablePoses(requestedType);
            if (poseState > maximumState)
                return LuaAutomationResult.Rejected($"target pose state {poseState} is unavailable locally (maximum {maximumState})");
            if (controller->CPoseState == poseState)
                return LuaAutomationResult.Completed($"pose type {poseType} state {poseState} already matches");

            var playerState = PlayerState.Instance();
            if (playerState == null || !playerState->IsLoaded)
                return LuaAutomationResult.Rejected("player pose settings are unavailable");

            // Change Pose advances one step. Seed the predecessor, then invoke
            // the same game emote function a hotbar slot uses; no chat command
            // is involved in the mirror path.
            playerState->SelectedPoses[(int)requestedType] = poseState == 0 ? maximumState : (byte)(poseState - 1);
            var changePoseEmoteId = EmoteHelper.GetChangePoseEmoteId();
            if (!changePoseEmoteId.HasValue)
                return LuaAutomationResult.Rejected("the Change Pose emote could not be resolved from game data");
            HotbarManager.ExecuteHotbarEmoteAction(changePoseEmoteId.Value);
            return LuaAutomationResult.Queued($"exact pose type {poseType} state {poseState} requested");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> SetTargetAsync(ulong targetId, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.target");
        return OnFramework(() => {
            if (targetId is 0 or 0xE0000000) {
                DalamudApi.TargetManager.Target = null;
                return LuaAutomationResult.Completed("target cleared");
            }
            if (!TrySetTargetDirect(targetId, out var targetName))
                return LuaAutomationResult.Rejected($"target {targetId} is not loaded on this client");
            return LuaAutomationResult.Completed($"target set to {targetName}");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> SetTargetOfActorAsync(string actorName, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.target_of");
        return OnFramework(() => {
            actorName = FormationCharacterName.NormalizeWorldSeparator(actorName ?? string.Empty).Trim();
            if (actorName.Length == 0)
                return LuaAutomationResult.Rejected("source player name or live actor ID is required");
            var resolution = LuaActorQueryResolver.ResolvePlayer(DalamudApi.ObjectTable, actorName);
            if (resolution.Status.Equals("ambiguous", StringComparison.Ordinal))
                return LuaAutomationResult.Rejected(
                    $"source player '{actorName}' is ambiguous; use Name@World or a live actor ID");
            var actor = resolution.Actor;
            if (actor == null)
                return LuaAutomationResult.Rejected($"source actor '{actorName}' is not loaded");
            var observedTarget = actor.TargetObject;
            if (observedTarget == null || observedTarget.GameObjectId is 0 or 0xE0000000)
                return LuaAutomationResult.Rejected($"source actor '{actorName}' has no target");
            DalamudApi.TargetManager.Target = observedTarget;
            return LuaAutomationResult.Completed(
                $"target copied directly from {actorName} to {observedTarget.Name.TextValue}");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> MirrorTargetViaLeaderAsync(ulong targetId, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.target_via_leader");
        return OnFramework(() => {
            if (targetId is 0 or 0xE0000000) {
                DalamudApi.TargetManager.Target = null;
                return LuaAutomationResult.Completed("target cleared");
            }

            if (TrySetTargetDirect(targetId, out var directName))
                return LuaAutomationResult.Completed($"targeted {directName} directly");

            // Any visible real player can act as an observation bridge. The
            // source does not need to be configured, grouped, in the party, or
            // controlled by this PC. This scan runs only after direct targeting
            // failed, never in the steady-state actor-watch loop.
            foreach (var observer in DalamudApi.ObjectTable) {
                if (!LuaActorQueryResolver.IsUsablePlayer(observer)
                    || observer.TargetObjectId != targetId)
                    continue;
                var observedTarget = observer.TargetObject;
                if (observedTarget == null || observedTarget.GameObjectId != targetId)
                    continue;
                DalamudApi.TargetManager.Target = observedTarget;
                return LuaAutomationResult.Completed(
                    $"copied {observer.GetPlayerNameWorld() ?? observer.Name.TextValue}'s target directly to {observedTarget.Name.TextValue}");
            }

            return LuaAutomationResult.Rejected(
                $"target {targetId} is hidden and no visible player is currently targeting it");
        }, cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> SetWeaponDrawnAsync(bool drawn, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.weapon");
        return OnFramework(() => {
            var uiState = UIState.Instance();
            if (uiState == null)
                return LuaAutomationResult.Rejected("weapon state is unavailable");
            if (uiState->WeaponState.IsUnsheathed == drawn)
                return LuaAutomationResult.Completed(drawn ? "weapon already drawn" : "weapon already sheathed");
            var accepted = uiState->WeaponState.SetUnsheathed(drawn, true, false);
            return accepted
                ? LuaAutomationResult.Queued(drawn ? "weapon draw requested directly" : "weapon sheath requested directly")
                : LuaAutomationResult.Rejected(drawn ? "game rejected direct weapon draw" : "game rejected direct weapon sheath");
        }, cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> SetHeadgearVisibleAsync(bool visible, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.headgear_visible");
        return OnFramework(() => {
            var player = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.GetLocalPlayer();
            if (player == null)
                return LuaAutomationResult.Rejected("local player is unavailable");

            player->Character.DrawData.HideHeadgear(0, !visible);
            return LuaAutomationResult.Queued(
                visible
                    ? "headgear visibility requested directly; verify by actor snapshot"
                    : "headgear hiding requested directly; verify by actor snapshot");
        }, cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> SetVisorAsync(bool enabled, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.visor");
        return OnFramework(() => {
            var player = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.GetLocalPlayer();
            if (player == null)
                return LuaAutomationResult.Rejected("local player is unavailable");

            player->Character.DrawData.SetVisor(enabled);
            return LuaAutomationResult.Queued(
                enabled
                    ? "visor enable requested directly; verify by actor snapshot"
                    : "visor disable requested directly; verify by actor snapshot");
        }, cancellationToken);
    }

    public unsafe Task<LuaAutomationResult> SetOnlineStatusAsync(uint statusId, string statusName, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.online_status");
        return OnFramework(() => {
            var local = DalamudApi.ObjectTable.LocalPlayer;
            if (local != null && local.OnlineStatus.RowId == statusId)
                return LuaAutomationResult.Completed($"online status already matches {statusName}");

            var detail = InfoProxyDetail.Instance();
            if (detail == null)
                return LuaAutomationResult.Rejected("online-status service is unavailable");
            detail->SendOnlineStatusUpdate(statusId);
            return LuaAutomationResult.Queued($"online status {statusName} ({statusId}) requested directly");
        }, cancellationToken);
    }

    public Task<IReadOnlyList<GearsetDescriptor>> ListGearsetsAsync(
        uint? classJobId,
        CancellationToken cancellationToken) {
        if (classJobId is 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(classJobId), "class/job ID must be between 1 and 255");
        return OnFramework<IReadOnlyList<GearsetDescriptor>>(
            () => GearsetManager.GetGearsets(classJobId),
            cancellationToken);
    }

    public Task<GearsetResolution> FindGearsetAsync(
        GearsetSelector selector,
        uint? requiredClassJobId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(selector);
        if (requiredClassJobId is 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(requiredClassJobId), "class/job ID must be between 1 and 255");
        return OnFramework(
            () => GearsetManager.ResolveGearset(
                requiredClassJobId.HasValue ? (byte)requiredClassJobId.Value : null,
                selector),
            cancellationToken);
    }

    public Task<GearsetResolution> EquipGearsetAsync(
        GearsetSelector selector,
        uint? requiredClassJobId,
        CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "gearsets.equip");
        ArgumentNullException.ThrowIfNull(selector);
        if (requiredClassJobId is 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(requiredClassJobId), "class/job ID must be between 1 and 255");
        return OnFramework(() => ResolveAndEquip(requiredClassJobId, selector), cancellationToken);
    }

    public Task<GearsetResolution> ChangeJobAsync(
        uint classJobId,
        GearsetSelector selector,
        CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.job");
        if (classJobId is 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(classJobId), "class/job ID must be between 1 and 255");
        ArgumentNullException.ThrowIfNull(selector);
        return OnFramework(() => ResolveAndEquip(classJobId, selector), cancellationToken);
    }

    private GearsetResolution ResolveAndEquip(uint? requiredClassJobId, GearsetSelector selector) {
        var resolution = GearsetManager.ResolveGearset(
            requiredClassJobId.HasValue ? (byte)requiredClassJobId.Value : null,
            selector);
        if (!resolution.Success || resolution.Gearset == null)
            return resolution;
        GearsetManager.ChangeGearset(_plugin, resolution.Gearset.Number - 1);
        return resolution with {
            Status = "queued",
            Message = requiredClassJobId.HasValue
                ? $"class/job {requiredClassJobId.Value} requested through gearset {resolution.Gearset.Number} '{resolution.Gearset.Name}'"
                : $"gearset {resolution.Gearset.Number} '{resolution.Gearset.Name}' requested",
        };
    }

    public Task<LuaAutomationResult> SetWalkingAsync(string mode, string scope, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.Movement, "actions.walk");
        var normalizedMode = mode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedMode is not ("on" or "off" or "toggle"))
            return Task.FromResult(LuaAutomationResult.Rejected("walk mode must be on, off, or toggle"));
        return OnFramework(() => {
            switch (NormalizeScope(scope)) {
                case "local":
                    SimpleMovementWalkState.IsWalking = normalizedMode switch {
                        "on" => true,
                        "off" => false,
                        _ => !SimpleMovementWalkState.IsWalking,
                    };
                    break;
                case "current_pc":
                    _plugin.IpcProvider.ExecuteTextCommand($"/mop walk {normalizedMode}", includeSelf: true);
                    break;
                default:
                    return LuaAutomationResult.Rejected("walk scope must be 'local' or 'current_pc'");
            }
            return LuaAutomationResult.Completed($"walk mode {normalizedMode}");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> StopMovementAsync(string scope, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.Movement, "actions.stop_movement");
        return OnFramework(() => {
            switch (NormalizeScope(scope)) {
                case "local": _plugin.StopNonLuaMovementLocal(); break;
                case "current_pc": _plugin.IpcProvider.StopManagedMovement(); break;
                default: return LuaAutomationResult.Rejected("movement scope must be 'local' or 'current_pc'");
            }
            return LuaAutomationResult.Completed("movement stopped");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> PlacePetAsync(Vector3 offset, string anchor, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.place_pet");
        return OnFramework(() => {
            var defaultAnchor = DalamudApi.TargetManager.Target != null
                ? FormationAnchorReference.Target
                : FormationAnchorReference.Self;
            var anchorParse = FormationAnchorArgumentParser.ParseAnchorAndArrival(
                string.IsNullOrWhiteSpace(anchor) ? [] : [anchor],
                defaultAnchor);

            if (!FormationAnchorResolver.TryResolve(_plugin, new Formation(), anchorParse.Anchor, out var resolved, out var failureReason, out _)) {
                if (anchorParse.Fallback != null && FormationAnchorResolver.TryResolve(_plugin, new Formation(), anchorParse.Fallback, out var fallbackResolved, out _, out _)) {
                    resolved = fallbackResolved;
                } else {
                    return LuaAutomationResult.Rejected($"failed to resolve anchor: {failureReason}");
                }
            }

            var worldPos = offset.ApplyLeaderRotation(resolved.Rotation, resolved.Position);
            worldPos.Y = resolved.Position.Y + offset.Y;

            bool isSelf = anchorParse.Anchor.Kind == FormationAnchorKind.Self;
            var targetActor = resolved.Actor ?? (resolved.GameObjectId.HasValue ? DalamudApi.ObjectTable.FirstOrDefault(a => a != null && a.GameObjectId == resolved.GameObjectId) : null);

            GameActionManager.PlacePet(worldPos, targetActor, isSelf);
            return LuaAutomationResult.Completed($"pet placed at offset ({offset.X:F2}, {offset.Y:F2}, {offset.Z:F2})");
        }, cancellationToken);
    }

    public Task<LuaAutomationResult> PlacePetFormationAsync(string formationName, int pointNumber, string anchor, CancellationToken cancellationToken) {
        _ensureResource(LuaResourceKind.GameActions, "actions.place_pet_formation");
        if (pointNumber < 1)
            return Task.FromResult(LuaAutomationResult.Rejected("point number must be greater than zero"));

        return OnFramework(() => {
            var anchorParse = FormationAnchorArgumentParser.ParseAnchorAndArrival(
                string.IsNullOrWhiteSpace(anchor) ? [] : [anchor],
                FormationAnchorReference.Self);

            var success = FormationLocalMovementExecutor.ExecuteFormationPetPlace(
                _plugin,
                formationName,
                pointNumber - 1,
                anchorParse.Anchor,
                logPrefix: "lua place_pet_formation",
                fallbackAnchor: anchorParse.Fallback);

            return success
                ? LuaAutomationResult.Completed($"pet placed at formation \"{formationName}\" point {pointNumber}")
                : LuaAutomationResult.Rejected($"failed to place pet at formation \"{formationName}\" point {pointNumber}");
        }, cancellationToken);
    }

    private Task<T> OnFramework<T>(Func<T> action, CancellationToken cancellationToken) =>
        DalamudApi.Framework.RunOnFrameworkThread(action).WaitAsync(cancellationToken);

    private static LuaAutomationResult ExecuteCosmetic(string kind, uint requestedId, bool? persistent, bool allowFallback) {
        var normalized = NormalizeCosmeticKind(kind);
        var resolved = CosmeticActionFallbackResolver.Resolve(normalized, requestedId, persistent, allowFallback);
        if (!resolved.Success)
            return LuaAutomationResult.Rejected(resolved.Message);

        switch (normalized) {
            case "emote":
                var dispatched = ExecuteNativeEmote(resolved.ActionId);
                return dispatched.Success ? LuaAutomationResult.Queued(resolved.Message) : dispatched;
            case "mount": GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Mount, resolved.ActionId); break;
            case "minion": GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Companion, resolved.ActionId); break;
            case "fashion_accessory": GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Ornament, resolved.ActionId); break;
            case "facewear": HotbarManager.ExecuteHotbarAction(RaptureHotbarModule.HotbarSlotType.Glasses, resolved.ActionId); break;
            default: return LuaAutomationResult.Rejected($"unsupported cosmetic kind '{kind}'");
        }
        return LuaAutomationResult.Queued(resolved.Message);
    }

    private static unsafe LuaAutomationResult ExecuteNativeEmote(uint emoteId) {
        if (emoteId is 0 or > ushort.MaxValue)
            return LuaAutomationResult.Rejected($"emote ID {emoteId} is outside the game's supported range");
        var manager = EmoteManager.Instance();
        if (manager != null && manager->CanExecuteEmote((ushort)emoteId)) {
            if (manager->ExecuteEmote((ushort)emoteId))
                return LuaAutomationResult.Queued($"emote {emoteId} dispatched through the native emote manager");
        }

        HotbarManager.ExecuteHotbarEmoteAction(emoteId);
        return LuaAutomationResult.Queued($"emote {emoteId} dispatched through hotbar module");
    }

    private static unsafe LuaAutomationResult StopCosmetic(string kind) {
        var normalized = NormalizeCosmeticKind(kind);
        var player = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.GetLocalPlayer();
        if (player == null)
            return LuaAutomationResult.Rejected("local player is unavailable");
        switch (normalized) {
            case "facewear":
                var glasses = player->Character.DrawData.GlassesIds;
                if (glasses.Length > 0 && glasses[0] > 0)
                    HotbarManager.ExecuteHotbarAction(RaptureHotbarModule.HotbarSlotType.Glasses, glasses[0]);
                return LuaAutomationResult.Queued("facewear removal requested");
            case "fashion_accessory":
                var ornamentId = (uint)player->Character.OrnamentData.OrnamentId;
                if (ornamentId > 0)
                    GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Ornament, ornamentId);
                return LuaAutomationResult.Queued("fashion accessory dismissal requested");
            case "minion":
                var companion = player->Character.CompanionData.CompanionObject;
                if (companion != null)
                    GameActionManager.UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Companion, companion->Character.BaseId);
                return LuaAutomationResult.Queued("minion dismissal requested");
            default:
                return LuaAutomationResult.Rejected($"cosmetic kind '{kind}' cannot be stopped");
        }
    }

    private static string NormalizeCosmeticKind(string kind) => (kind ?? string.Empty)
        .Trim().ToLowerInvariant().Replace('-', '_') switch {
            "companion" => "minion",
            "ornament" or "accessory" or "fashion" => "fashion_accessory",
            var normalized => normalized,
        };

    private static unsafe bool TrySetTargetDirect(ulong targetId, out string targetName) {
        targetName = targetId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var managed = DalamudApi.ObjectTable.FirstOrDefault(candidate =>
            candidate != null && candidate.GameObjectId == targetId);
        if (managed != null) {
            DalamudApi.TargetManager.Target = managed;
            targetName = managed.Name.TextValue;
            return true;
        }

        var manager = GameObjectManager.Instance();
        var targetSystem = TargetSystem.Instance();
        if (manager == null || targetSystem == null)
            return false;
        var native = manager->Objects.GetObjectByGameObjectId(targetId);
        if (native == null)
            return false;
        if (!targetSystem->SetHardTarget(native, true, false, 0))
            return false;
        targetName = native->NameString;
        return true;
    }

    private static unsafe bool TryGetActorPosition(ulong targetId, out Vector3 position) {
        position = default;
        if (targetId is 0 or 0xE0000000)
            return false;
        var managed = DalamudApi.ObjectTable.FirstOrDefault(candidate =>
            candidate != null && candidate.GameObjectId == targetId);
        if (managed != null) {
            position = managed.Position;
            return true;
        }
        var manager = GameObjectManager.Instance();
        if (manager == null)
            return false;
        var native = manager->Objects.GetObjectByGameObjectId(targetId);
        if (native == null)
            return false;
        position = new Vector3(native->Position.X, native->Position.Y, native->Position.Z);
        return true;
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "local" : scope.Trim().ToLowerInvariant().Replace('-', '_');
}
