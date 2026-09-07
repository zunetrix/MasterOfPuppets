using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Movement;
using MasterOfPuppets.LuaScripting.Watches;

using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using CharacterModes = FFXIVClientStructs.FFXIV.Client.Game.Character.CharacterModes;
using EmoteController = FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController;

namespace MasterOfPuppets.LuaScripting.Snapshots;

internal static class DalamudLuaGameSnapshotCapture {
    private const int MaximumVisibleActors = 256;
    private static readonly (ConditionFlag Flag, string Name)[] ConditionEntries =
        Enum.GetValues<ConditionFlag>()
            .Select(flag => (flag, flag.ToString()))
            .ToArray();

    /// <summary>
    /// Captures only the state consumed by
    /// <see cref="MasterOfPuppets.LuaScripting.Events.LuaGameEventTracker"/>.
    /// This intentionally avoids enumerating, sorting, and materializing the full
    /// object table for the automatic four-hertz event observation path.
    /// </summary>
    public static LuaGameSnapshot CaptureEvents(
        IReadOnlyList<ulong> participantCids,
        IReadOnlyDictionary<ulong, string> configuredNames) {
        var local = DalamudApi.ObjectTable.LocalPlayer;
        var localId = local?.GameObjectId ?? 0;
        LuaActorSnapshot? Resolve(IGameObject? actor, string authority) =>
            actor == null ? null : CaptureActor(actor, localId, authority);

        return new LuaGameSnapshot(
            Resolve(local, "local"),
            Resolve(DalamudApi.TargetManager.Target, "selected-target"),
            Resolve(DalamudApi.TargetManager.FocusTarget, "focus-target"),
            null,
            Array.Empty<LuaActorSnapshot>(),
            CaptureParticipants(participantCids, configuredNames, local, Resolve, worldActors: null),
            DalamudApi.ClientState.TerritoryType,
            DalamudApi.ClientState.MapId,
            DalamudApi.ClientState.Instance,
            DalamudApi.ClientState.IsLoggedIn,
            DalamudApi.ClientState.IsGPosing,
            DalamudApi.ClientState.IsPvP,
            CaptureConditions());
    }

    public static LuaGameSnapshot Capture(
        IReadOnlyList<ulong> participantCids,
        IReadOnlyDictionary<ulong, string> configuredNames,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName) {
        var local = DalamudApi.ObjectTable.LocalPlayer;
        var localId = local?.GameObjectId ?? 0;
        var visible = DalamudApi.ObjectTable
            .Where(actor => actor is { Address: not 0 })
            .OrderBy(actor => actor.CurrentDistance)
            .Take(MaximumVisibleActors)
            .Select(actor => CaptureActor(actor, localId, "visible"))
            .ToArray();
        var byGameObject = visible.ToDictionary(actor => actor.GameObjectId, StringComparer.Ordinal);
        LuaActorSnapshot? Resolve(IGameObject? actor, string authority) {
            if (actor == null)
                return null;
            if (byGameObject.TryGetValue(actor.GameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var snapshot))
                return snapshot with { Authority = authority };
            return CaptureActor(actor, localId, authority);
        }
        LuaBuddySnapshot? CaptureBuddy(IBuddyMember? buddy, string kind) {
            if (buddy == null || buddy.Address == IntPtr.Zero)
                return null;
            var actor = Resolve(buddy.GameObject, "buddy");
            return new LuaBuddySnapshot(
                kind,
                actor?.GameObjectId ?? "0",
                buddy.EntityId,
                buddy.DataID,
                buddy.CurrentHP,
                buddy.MaxHP,
                actor);
        }

        var worldActors = DalamudApi.ObjectTable
            .Where(actor => actor is { Address: not 0 })
            .ToArray();
        var participants = CaptureParticipants(participantCids, configuredNames, local, Resolve, worldActors);

        var runTarget = DalamudApi.ObjectTable.FirstOrDefault(actor =>
            (runTargetEntityId != 0 && runTargetEntityId != 0xE0000000 && actor.EntityId == runTargetEntityId)
            || (runTargetObjectId != 0 && actor.GameObjectId == runTargetObjectId));
        if (runTarget == null && !string.IsNullOrWhiteSpace(runTargetName))
            runTarget = LuaActorQueryResolver.ResolvePlayer(
                DalamudApi.ObjectTable,
                runTargetName).Actor;

        var buddies = DalamudApi.BuddyList
            .Select(buddy => CaptureBuddy(buddy, "battle"))
            .Append(CaptureBuddy(DalamudApi.BuddyList.CompanionBuddy, "companion"))
            .Append(CaptureBuddy(DalamudApi.BuddyList.PetBuddy, "pet"))
            .Where(buddy => buddy != null)
            .Cast<LuaBuddySnapshot>()
            .ToArray();
        return new LuaGameSnapshot(
            Resolve(local, "local"),
            Resolve(DalamudApi.TargetManager.Target, "selected-target"),
            Resolve(DalamudApi.TargetManager.FocusTarget, "focus-target"),
            Resolve(runTarget, "run-target"),
            visible,
            participants,
            DalamudApi.ClientState.TerritoryType,
            DalamudApi.ClientState.MapId,
            DalamudApi.ClientState.Instance,
            DalamudApi.ClientState.IsLoggedIn,
            DalamudApi.ClientState.IsGPosing,
            DalamudApi.ClientState.IsPvP,
            CaptureConditions()) {
            PlayerProfile = CapturePlayerProfile(),
            Party = DalamudApi.PartyList
                .Select(member => Resolve(member.GameObject, "party"))
                .Where(actor => actor != null)
                .Cast<LuaActorSnapshot>()
                .ToArray(),
            Buddies = buddies,
        };
    }

    private static IReadOnlyList<LuaParticipantSnapshot> CaptureParticipants(
        IReadOnlyList<ulong> participantCids,
        IReadOnlyDictionary<ulong, string> configuredNames,
        IGameObject? local,
        Func<IGameObject?, string, LuaActorSnapshot?> resolve,
        IReadOnlyList<IGameObject>? worldActors) {
        var participants = new List<LuaParticipantSnapshot>(participantCids.Count);
        for (var slot = 0; slot < participantCids.Count; slot++) {
            var cid = participantCids[slot];
            var isLocal = cid != 0 && cid == DalamudApi.PlayerState.ContentId;
            var party = DalamudApi.PartyList.FirstOrDefault(member => member.ContentId == cid);
            var configuredName = configuredNames.GetValueOrDefault(cid) ?? string.Empty;
            var worldObject = isLocal
                ? local
                : party?.GameObject ?? FindVisibleRosterActor(configuredName, worldActors);
            var actor = isLocal
                ? resolve(local, "local")
                : worldObject != null
                    ? resolve(worldObject, party?.GameObject != null ? "party" : "visible")
                    : null;
            var name = actor?.Name
                ?? (party == null ? null : $"{party.Name.TextValue}@{party.World.ValueNullable?.Name}")
                ?? configuredName;
            var isVisible = isLocal || actor != null;
            participants.Add(new LuaParticipantSnapshot(
                slot,
                cid,
                name,
                isLocal,
                actor?.Authority ?? "configured",
                actor,
                isVisible));
        }
        return participants;
    }

    private static IGameObject? FindVisibleRosterActor(
        string configuredName,
        IReadOnlyList<IGameObject>? worldActors) {
        if (worldActors == null || string.IsNullOrWhiteSpace(configuredName))
            return null;
        return LuaActorQueryResolver.ResolvePlayer(worldActors, configuredName).Actor;
    }

    private static IReadOnlyDictionary<string, bool> CaptureConditions() {
        var conditions = new Dictionary<string, bool>(ConditionEntries.Length, StringComparer.Ordinal);
        foreach (var (flag, name) in ConditionEntries)
            // Dalamud's ConditionFlag currently contains aliases that stringify
            // to the same name (for example Mounted2). A snapshot is keyed by
            // the public condition name, so aliases should overwrite rather
            // than make every game-state capture fail with a duplicate key.
            conditions[name] = DalamudApi.Condition[flag];
        return conditions;
    }

    private static LuaPlayerProfileSnapshot? CapturePlayerProfile() {
        var player = DalamudApi.PlayerState;
        if (!player.IsLoaded || player.ContentId == 0)
            return null;
        return new LuaPlayerProfileSnapshot(
            player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            player.CharacterName,
            player.EntityId,
            player.CurrentWorld.RowId,
            player.HomeWorld.RowId,
            player.ClassJob.RowId,
            player.Level,
            player.EffectiveLevel,
            player.Race.RowId,
            player.Tribe.RowId,
            player.GrandCompany.RowId,
            player.Sex.ToString(),
            player.IsLoaded,
            player.IsLevelSynced,
            player.IsMentor,
            player.IsBattleMentor,
            player.IsTradeMentor,
            player.IsNovice,
            player.IsReturner,
            player.BaseStrength,
            player.BaseDexterity,
            player.BaseVitality,
            player.BaseIntelligence,
            player.BaseMind,
            player.BasePiety);
    }

    /// <summary>
    /// Captures the compact, allocation-free state consumed by actor watches.
    /// Unlike <see cref="Capture"/>, this does not enumerate the object table or
    /// materialize unrelated world, party, buddy, condition, or profile data.
    /// </summary>
    internal static unsafe LuaActorWatchState CaptureWatchState(IGameObject actor) {
        ArgumentNullException.ThrowIfNull(actor);
        var character = actor as ICharacter;
        var battle = actor as IBattleChara;
        var isLoaded = actor.Address != 0
            && actor.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
            && character != null
            && battle != null
            && actor.EntityId is not (0 or 0xE0000000);
        var native = isLoaded ? (NativeBattleChara*)actor.Address : null;
        var companion = native == null ? null : native->CompanionData.CompanionObject;
        var poseType = ResolvePoseType(native);
        var classJobId = character?.ClassJob.RowId ?? 0;
        if (classJobId == 0 && native != null)
            classJobId = native->Character.CharacterData.ClassJob;
        return new LuaActorWatchState(
            actor.GameObjectId,
            actor.EntityId,
            actor.TargetObjectId,
            actor.Position.X,
            actor.Position.Y,
            actor.Position.Z,
            actor.IsTargetable,
            actor.IsDead,
            isLoaded,
            false,
            native != null
                && actor.GameObjectId == DalamudApi.ObjectTable.LocalPlayer?.GameObjectId
                && native->Character.IsJumping(),
            0,
            native == null ? 0u : native->Character.Mount.MountId,
            companion == null ? 0u : companion->Character.BaseId,
            native == null ? 0u : native->Character.EmoteController.EmoteId,
            native == null ? 0UL : (ulong)native->Character.EmoteController.Target,
            native != null && IsEmoteLooping(
                native->Character.EmoteController.EmoteId,
                native->Character.EmoteController.IsInEmoteLoop(),
                native->Character.Mode is CharacterModes.EmoteLoop or CharacterModes.InPositionLoop),
            (byte)poseType,
            native == null ? (byte)0 : native->Character.EmoteController.CPoseState,
            native == null ? 0u : native->Character.OrnamentData.OrnamentId,
            native == null || native->Character.DrawData.GlassesIds.Length == 0
                ? 0u
                : native->Character.DrawData.GlassesIds[0],
            HasStatus(battle, 50),
            native != null && native->Character.IsWeaponDrawn,
            character?.OnlineStatus.RowId ?? 0,
            classJobId) {
            IsHeadgearVisible = native != null && !native->Character.DrawData.IsHatHidden,
            IsVisorToggled = native != null && native->Character.DrawData.IsVisorToggled,
            IsWalking = actor.GameObjectId == DalamudApi.ObjectTable.LocalPlayer?.GameObjectId && SimpleMovementWalkState.IsWalking,
        };
    }

    internal static bool IsEmoteLooping(
        uint emoteId,
        bool controllerReportsLoop,
        bool modeReportsLoop) =>
        emoteId != 0 && (controllerReportsLoop || modeReportsLoop);

    private static bool HasStatus(IBattleChara? actor, uint statusId) {
        if (actor == null)
            return false;
        foreach (var status in actor.StatusList) {
            if (status.StatusId == statusId)
                return true;
        }
        return false;
    }

    private static unsafe EmoteController.PoseType ResolvePoseType(NativeBattleChara* native) {
        var poseType = native == null
            ? EmoteController.PoseType.Idle
            : native->Character.EmoteController.CurrentPoseType;
        // A remote actor can expose 255 while unloading. Calling GetPoseKind
        // in that state invokes a native function on an invalid transition
        // and was the cause of the synchronized disconnect crash.
        if (native != null && (byte)poseType == byte.MaxValue)
            return EmoteController.PoseType.Idle;
        return Enum.IsDefined(poseType) ? poseType : EmoteController.PoseType.Idle;
    }

    private static unsafe LuaActorSnapshot CaptureActor(IGameObject actor, ulong localId, string authority) {
        var character = actor as ICharacter;
        var battle = actor as IBattleChara;
        var isLoaded = actor.Address != 0
            && actor.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
            && character != null
            && battle != null
            && actor.EntityId is not (0 or 0xE0000000);
        // VisibleActors contains every object kind, including aetherytes and
        // event objects. Their addresses are valid but are not BattleChara
        // instances, so never invoke Character native methods until the
        // Dalamud interfaces and object kind establish that this is a PC.
        var native = isLoaded ? (NativeBattleChara*)actor.Address : null;
        var companion = native == null ? null : native->CompanionData.CompanionObject;
        var poseType = ResolvePoseType(native);
        var classJobId = character?.ClassJob.RowId ?? 0;
        if (classJobId == 0 && native != null)
            classJobId = native->Character.CharacterData.ClassJob;
        return new LuaActorSnapshot(
            actor.GetPlayerNameWorld() ?? actor.Name.TextValue,
            actor.GameObjectId == localId ? "local" : authority,
            actor.ObjectKind.ToString(),
            actor.GameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            actor.EntityId,
            actor.TargetObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            actor.Position,
            actor.Rotation,
            actor.GameObjectId == localId,
            actor.IsTargetable,
            actor.IsDead,
            isLoaded,
            native != null
                && actor.GameObjectId == localId
                && native->Character.IsJumping(),
            native == null ? 0u : native->Character.Mount.MountId,
            companion == null ? 0u : companion->Character.BaseId,
            native == null ? 0u : native->Character.EmoteController.EmoteId,
            (native == null ? 0UL : (ulong)native->Character.EmoteController.Target)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            native != null && (native->Character.EmoteController.IsInEmoteLoop()
                || native->Character.Mode is CharacterModes.EmoteLoop or CharacterModes.InPositionLoop
                || (native->Character.EmoteController.EmoteId != 0 && EmoteHelper.IsPersistent(native->Character.EmoteController.EmoteId))),
            (byte)poseType,
            native == null ? (byte)0 : native->Character.EmoteController.CPoseState,
            native == null ? 0u : native->Character.OrnamentData.OrnamentId,
            native == null || native->Character.DrawData.GlassesIds.Length == 0
                ? 0u
                : native->Character.DrawData.GlassesIds[0],
            native != null && native->Character.IsWeaponDrawn,
            character?.OnlineStatus.RowId ?? 0,
            character?.OnlineStatus.Value.Name.ToString() ?? string.Empty,
            classJobId,
            character?.Level ?? 0,
            character?.CurrentHp ?? 0,
            character?.MaxHp ?? 0,
            character?.CurrentMp ?? 0,
            character?.MaxMp ?? 0,
            character?.CurrentCp ?? 0,
            character?.MaxCp ?? 0,
            character?.CurrentGp ?? 0,
            character?.MaxGp ?? 0,
            character?.StatusFlags.ToString() ?? string.Empty,
            battle?.IsCasting ?? false,
            battle?.CastActionId ?? 0,
            (battle?.CastTargetObjectId ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            battle?.CurrentCastTime ?? 0f,
            battle?.TotalCastTime ?? 0f) {
            IsHeadgearVisible = native != null && !native->Character.DrawData.IsHatHidden,
            IsVisorToggled = native != null && native->Character.DrawData.IsVisorToggled,
            IsWalking = actor.GameObjectId == localId && SimpleMovementWalkState.IsWalking,
            IsMoving = false,
        };
    }
}
