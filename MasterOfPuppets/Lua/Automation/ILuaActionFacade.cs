using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.LuaScripting.Automation;

public sealed record LuaFallbackCandidateSet(
    bool Success,
    string Kind,
    uint RequestedId,
    string Category,
    IReadOnlyList<uint> CandidateIds,
    IReadOnlyList<uint> UniverseIds,
    string EligibilityToken,
    string UniverseSignature,
    string Message);

public interface ILuaActionFacade {
    Task<LuaAutomationResult> ExecuteTextAsync(string text, string scope, CancellationToken cancellationToken);
    Task<LuaAutomationResult> UseActionAsync(string kind, uint id, string scope, bool? persistent, CancellationToken cancellationToken);
    Task<LuaAutomationResult> UseActionOnAsync(string kind, uint id, ulong targetId, bool? persistent, CancellationToken cancellationToken);
    Task<LuaAutomationResult> UseExactActionAsync(string kind, uint id, string scope, bool? persistent, CancellationToken cancellationToken);
    Task<LuaAutomationResult> UseExactActionOnAsync(string kind, uint id, ulong targetId, bool? persistent, CancellationToken cancellationToken);
    Task<LuaAutomationResult> UseGroundActionOnAsync(string kind, uint id, ulong targetId, Vector3? fallbackPosition, CancellationToken cancellationToken);
    Task<LuaFallbackCandidateSet> GetFallbackCandidatesAsync(string kind, uint id, bool? persistent, CancellationToken cancellationToken);
    Task<LuaAutomationResult> StopCosmeticAsync(string kind, CancellationToken cancellationToken);
    Task<LuaAutomationResult> StopEmoteAsync(CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetPoseAsync(byte poseType, byte poseState, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetTargetAsync(ulong targetId, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetTargetOfActorAsync(string actorName, CancellationToken cancellationToken);
    Task<LuaAutomationResult> MirrorTargetViaLeaderAsync(ulong targetId, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetWeaponDrawnAsync(bool drawn, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetHeadgearVisibleAsync(bool visible, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetVisorAsync(bool enabled, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetOnlineStatusAsync(uint statusId, string statusName, CancellationToken cancellationToken);
    Task<IReadOnlyList<GearsetDescriptor>> ListGearsetsAsync(uint? classJobId, CancellationToken cancellationToken);
    Task<GearsetResolution> FindGearsetAsync(GearsetSelector selector, uint? requiredClassJobId, CancellationToken cancellationToken);
    Task<GearsetResolution> EquipGearsetAsync(GearsetSelector selector, uint? requiredClassJobId, CancellationToken cancellationToken);
    Task<GearsetResolution> ChangeJobAsync(uint classJobId, GearsetSelector selector, CancellationToken cancellationToken);
    Task<LuaAutomationResult> SetWalkingAsync(string mode, string scope, CancellationToken cancellationToken);
    Task<LuaAutomationResult> StopMovementAsync(string scope, CancellationToken cancellationToken);
    Task<LuaAutomationResult> PlacePetAsync(Vector3 offset, string anchor, CancellationToken cancellationToken);
    Task<LuaAutomationResult> PlacePetFormationAsync(string formationName, int pointNumber, string anchor, CancellationToken cancellationToken);
}
