using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

using Dalamud.Hooking;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace MasterOfPuppets;

/// <summary>
/// Observes server-confirmed action effects after the game has decoded them.
/// This deliberately hooks the stable client processing function instead of
/// relying on patch-sensitive network opcodes.
/// </summary>
internal sealed class CombatActionObserver : IDisposable {
    private const int MaximumCapturedTargets = 32;

    private unsafe delegate void ReceiveActionEffectDelegate(
        uint casterEntityId,
        nint caster,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds);

    private Hook<ReceiveActionEffectDelegate>? _hook;

    public event Action<CombatActionObservation>? ActionObserved;
    public bool IsAvailable => _hook != null;

    public unsafe CombatActionObserver() {
        var address = (nint)ActionEffectHandler.MemberFunctionPointers.Receive;
        if (address == 0) {
            DalamudApi.PluginLog.Error("[CombatAction] ActionEffectHandler.Receive was unresolved; action mirroring is unavailable.");
            return;
        }

        try {
            _hook = DalamudApi.GameInteropProvider.HookFromAddress<ReceiveActionEffectDelegate>(
                address,
                ReceiveDetour);
            _hook.Enable();
            DalamudApi.PluginLog.Information($"[CombatAction] action-effect hook enabled at 0x{address:X}");
        } catch (Exception ex) {
            _hook?.Dispose();
            _hook = null;
            DalamudApi.PluginLog.Error(ex, "[CombatAction] failed to install action-effect hook; action mirroring is unavailable");
        }
    }

    private unsafe void ReceiveDetour(
        uint casterEntityId,
        nint caster,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds) {
        CombatActionObservation? observation = null;
        try {
            if (header != null && header->ActionId != 0) {
                var targetCount = Math.Min((int)header->NumTargets, MaximumCapturedTargets);
                var targets = new List<string>(targetCount);
                if (targetEntityIds != null) {
                    for (var index = 0; index < targetCount; index++)
                        targets.Add(targetEntityIds[index].Id.ToString(CultureInfo.InvariantCulture));
                }

                observation = new CombatActionObservation(
                    casterEntityId,
                    header->ActionId,
                    header->ActionType,
                    header->GlobalSequence,
                    header->SourceSequence,
                    header->SpellId,
                    header->AnimationTargetId.Id,
                    targets,
                    targetPosition == null ? null : *targetPosition,
                    DateTimeOffset.UtcNow);
            }
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[CombatAction] failed to capture action-effect metadata");
        }

        var hook = _hook;
        if (hook == null)
            return;
        hook.Original(casterEntityId, caster, targetPosition, header, effects, targetEntityIds);

        if (observation == null)
            return;
        try {
            ActionObserved?.Invoke(observation);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[CombatAction] observer callback failed");
        }
    }

    public void Dispose() {
        ActionObserved = null;
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
    }
}

internal sealed record CombatActionObservation(
    uint SourceEntityId,
    uint ActionId,
    byte ActionType,
    uint GlobalSequence,
    ushort SourceSequence,
    ushort SpellId,
    ulong AnimationTargetId,
    IReadOnlyList<string> TargetIds,
    Vector3? TargetPosition,
    DateTimeOffset Timestamp);
