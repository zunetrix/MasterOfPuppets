using System;

namespace MasterOfPuppets.LuaScripting.Watches;

[Flags]
public enum LuaActorWatchChange : uint {
    None = 0,
    Jump = 1 << 0,
    Target = 1 << 1,
    Mount = 1 << 2,
    Companion = 1 << 3,
    Emote = 1 << 4,
    Pose = 1 << 5,
    Ornament = 1 << 6,
    Facewear = 1 << 7,
    Weapon = 1 << 8,
    OnlineStatus = 1 << 9,
    ClassJob = 1 << 10,
    Availability = 1 << 11,
    Sprint = 1 << 12,
    Movement = 1 << 13,
    Headgear = 1 << 14,
    Visor = 1 << 15,
    WalkMode = 1 << 16,
}

/// <summary>
/// Allocation-free actor state used by long-running Lua watches. It deliberately
/// contains only fields that can change mirroring decisions; broad world,
/// participant, party, buddy, condition, and profile data remain in the one-shot
/// snapshot API.
/// </summary>
public readonly record struct LuaActorWatchState(
    ulong GameObjectId,
    uint EntityId,
    ulong TargetGameObjectId,
    float PositionX,
    float PositionY,
    float PositionZ,
    bool IsTargetable,
    bool IsDead,
    bool IsLoaded,
    bool IsMoving,
    bool IsJumping,
    long JumpSequence,
    uint MountId,
    uint CompanionId,
    uint EmoteId,
    ulong EmoteTargetGameObjectId,
    bool IsEmoteLooping,
    byte PoseType,
    byte PoseState,
    uint OrnamentId,
    uint FacewearId,
    bool IsSprinting,
    bool IsWeaponDrawn,
    uint OnlineStatusId,
    uint ClassJobId) {

    public bool IsHeadgearVisible { get; init; }
    public bool IsVisorToggled { get; init; }
    public bool IsWalking { get; init; }

    public LuaActorWatchChange ChangesFrom(in LuaActorWatchState previous) {
        var changes = LuaActorWatchChange.None;
        if (IsJumping != previous.IsJumping || JumpSequence != previous.JumpSequence)
            changes |= LuaActorWatchChange.Jump;
        if (IsMoving != previous.IsMoving)
            changes |= LuaActorWatchChange.Movement;
        if (IsWalking != previous.IsWalking)
            changes |= LuaActorWatchChange.WalkMode;
        if (TargetGameObjectId != previous.TargetGameObjectId)
            changes |= LuaActorWatchChange.Target;
        if (MountId != previous.MountId)
            changes |= LuaActorWatchChange.Mount;
        if (CompanionId != previous.CompanionId)
            changes |= LuaActorWatchChange.Companion;
        if (EmoteId != previous.EmoteId
            || EmoteTargetGameObjectId != previous.EmoteTargetGameObjectId
            || IsEmoteLooping != previous.IsEmoteLooping)
            changes |= LuaActorWatchChange.Emote;
        if (PoseType != previous.PoseType || PoseState != previous.PoseState)
            changes |= LuaActorWatchChange.Pose;
        if (OrnamentId != previous.OrnamentId)
            changes |= LuaActorWatchChange.Ornament;
        if (FacewearId != previous.FacewearId)
            changes |= LuaActorWatchChange.Facewear;
        if (IsHeadgearVisible != previous.IsHeadgearVisible)
            changes |= LuaActorWatchChange.Headgear;
        if (IsVisorToggled != previous.IsVisorToggled)
            changes |= LuaActorWatchChange.Visor;
        if (IsSprinting != previous.IsSprinting)
            changes |= LuaActorWatchChange.Sprint;
        if (IsWeaponDrawn != previous.IsWeaponDrawn)
            changes |= LuaActorWatchChange.Weapon;
        if (OnlineStatusId != previous.OnlineStatusId)
            changes |= LuaActorWatchChange.OnlineStatus;
        if (ClassJobId != previous.ClassJobId)
            changes |= LuaActorWatchChange.ClassJob;
        if (GameObjectId != previous.GameObjectId
            || EntityId != previous.EntityId
            || IsTargetable != previous.IsTargetable
            || IsDead != previous.IsDead
            || IsLoaded != previous.IsLoaded)
            changes |= LuaActorWatchChange.Availability;
        return changes;
    }
}

public sealed record LuaActorWatchRegistration(
    string WatchId,
    string Query,
    string Status,
    string Name,
    string OnlineStatusName,
    LuaActorWatchState? State) {
    /// <summary>
    /// Monotonic per-query revision. Lua can wait after a known revision
    /// without rescanning the object table or comparing every state field.
    /// </summary>
    public long Revision { get; init; }

    /// <summary>The field groups that produced <see cref="Revision"/>.</summary>
    public LuaActorWatchChange Changes { get; init; }

    /// <summary>
    /// Number of visible players matching the query. This is 0 for missing,
    /// 1 for found, and greater than 1 for an ambiguous name.
    /// </summary>
    public int MatchCount { get; init; }
}

public readonly record struct LuaActorEventSources(
    bool StateChanges,
    bool CombatActions,
    bool Emotes);
