using System.Collections.Generic;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting.Snapshots;

public sealed record LuaActorSnapshot(
    string Name,
    string Authority,
    string ObjectKind,
    string GameObjectId,
    uint EntityId,
    string TargetGameObjectId,
    Vector3 Position,
    float Rotation,
    bool IsLocal,
    bool IsTargetable,
    bool IsDead,
    bool IsLoaded,
    bool IsJumping,
    uint MountId,
    uint CompanionId,
    uint EmoteId,
    string EmoteTargetGameObjectId,
    bool IsEmoteLooping,
    byte PoseType,
    byte PoseState,
    uint OrnamentId,
    uint FacewearId,
    bool IsWeaponDrawn,
    uint OnlineStatusId,
    string OnlineStatusName,
    uint ClassJobId,
    int Level,
    uint CurrentHp,
    uint MaxHp,
    uint CurrentMp,
    uint MaxMp,
    uint CurrentCp,
    uint MaxCp,
    uint CurrentGp,
    uint MaxGp,
    string StatusFlags,
    bool IsCasting,
    uint CastActionId,
    string CastTargetGameObjectId,
    float CurrentCastTime,
    float TotalCastTime) {
    public bool IsHeadgearVisible { get; init; }
    public bool IsVisorToggled { get; init; }
    public bool IsMoving { get; init; }
    public bool IsWalking { get; init; }
}

public sealed record LuaParticipantSnapshot(
    int Slot,
    ulong ContentId,
    string Name,
    bool IsLocal,
    string Authority,
    LuaActorSnapshot? Actor,
    bool IsVisible = false);

public sealed record LuaPlayerProfileSnapshot(
    string ContentId,
    string CharacterName,
    uint EntityId,
    uint CurrentWorldId,
    uint HomeWorldId,
    uint ClassJobId,
    int Level,
    int EffectiveLevel,
    uint RaceId,
    uint TribeId,
    uint GrandCompanyId,
    string Sex,
    bool IsLoaded,
    bool IsLevelSynced,
    bool IsMentor,
    bool IsBattleMentor,
    bool IsTradeMentor,
    bool IsNovice,
    bool IsReturner,
    int Strength,
    int Dexterity,
    int Vitality,
    int Intelligence,
    int Mind,
    int Piety);

public sealed record LuaBuddySnapshot(
    string Kind,
    string GameObjectId,
    uint EntityId,
    uint DataId,
    uint CurrentHp,
    uint MaxHp,
    LuaActorSnapshot? Actor);

public sealed record LuaGameSnapshot(
    LuaActorSnapshot? Self,
    LuaActorSnapshot? SelectedTarget,
    LuaActorSnapshot? FocusTarget,
    LuaActorSnapshot? RunTarget,
    IReadOnlyList<LuaActorSnapshot> VisibleActors,
    IReadOnlyList<LuaParticipantSnapshot> Participants,
    uint TerritoryId,
    uint MapId,
    uint InstanceId,
    bool IsLoggedIn,
    bool IsGPosing,
    bool IsPvP,
    IReadOnlyDictionary<string, bool> Conditions) {
    public LuaPlayerProfileSnapshot? PlayerProfile { get; init; }
    public IReadOnlyList<LuaActorSnapshot> Party { get; init; } = [];
    public IReadOnlyList<LuaBuddySnapshot> Buddies { get; init; } = [];
}
