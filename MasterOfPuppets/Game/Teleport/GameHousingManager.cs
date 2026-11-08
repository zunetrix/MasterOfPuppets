using System;
using System.Linq;
using System.Numerics;
using System.Threading;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace MasterOfPuppets;

public enum HousingDoorKind {
    HouseEntrance,
    HouseExit,
}

/// <summary>
/// House entrance and exit interactions, plus the proximity sensor used by <see cref="HousingOverlayWindow"/>.
/// Flow: find door → dismount → walk into range → target → interact (retried) → confirm the
/// expected dialog only (house entrance Yesno / exit Yesno).
/// </summary>
public static class GameHousingManager {
    private const uint HouseEntranceBaseId = 2002737;
    private const uint HouseEntranceAltBaseId = 2002739; // also named Entrance
    private const uint HouseExitBaseId = 2002738;

    private const uint TerritoryUseHousingOutdoor = 13;
    private const uint TerritoryUseHousingIndoor = 14;

    private const uint DismountGeneralActionId = 23;

    /// <summary>Distance the player walks to before interacting</summary>
    private const float InteractDistance = 3.5f;

    /// <summary>Max distance a door is searched for when a command runs.</summary>
    private const float SearchDistance = 30f;

    private const long InteractRetryMs = 1500;
    private const int MaxInteractAttempts = 4; // e.g. a locked door never opens the dialog
    private const int FlowTimeoutMs = 20000;

    // Warp row 131148 = house entrance confirmation question
    private static string? TextConfirmHouseEntrance =>
        DalamudApi.DataManager.GetExcelSheet<Warp>(DalamudApi.ClientState.ClientLanguage)
            .GetRowOrDefault(131148)?.Question.ExtractText();

    private static CancellationTokenSource? _flowCts;

    public static void InteractWithNearestHouseEntrance(Plugin plugin) => StartDoorFlow(plugin, HousingDoorKind.HouseEntrance);

    public static unsafe void InteractWithNearestHouseExit(Plugin plugin) {
        if (!HousingManager.Instance()->IsInside()) return;
        StartDoorFlow(plugin, HousingDoorKind.HouseExit);
    }

    public static unsafe void MoveToFrontDoor() {
        var housingManager = HousingManager.Instance();
        if (!housingManager->IsInside()) return;

        housingManager->MoveToEntry();
    }

    // sensor
    public static bool IsHousingTerritory(out bool indoor) {
        indoor = false;
        var territory = DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(DalamudApi.ClientState.TerritoryType);
        var use = territory?.TerritoryIntendedUse.RowId ?? 0;
        indoor = use == TerritoryUseHousingIndoor;
        return use is TerritoryUseHousingOutdoor or TerritoryUseHousingIndoor;
    }

    public static unsafe bool IsInsideHouse() => HousingManager.Instance()->IsInside();

    /// <summary>Nearest door of the given kinds within <paramref name="maxDistance"/> (2D, doors sit at ground level).</summary>
    public static IGameObject? FindNearestDoor(float maxDistance, out HousingDoorKind kind, out float distance, params HousingDoorKind[] kinds) {
        kind = default;
        distance = float.MaxValue;
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return null;

        IGameObject? nearest = null;
        foreach (var obj in DalamudApi.ObjectTable) {
            if (obj == null || obj.ObjectKind != ObjectKind.EventObj || !obj.IsTargetable) continue;
            if (GetDoorKind(obj.BaseId) is not { } objKind || !kinds.Contains(objKind)) continue;

            var dist = Distance2D(player.Position, obj.Position);
            if (dist > maxDistance || dist >= distance) continue;

            nearest = obj;
            kind = objKind;
            distance = dist;
        }
        return nearest;
    }

    private static HousingDoorKind? GetDoorKind(uint baseId) => baseId switch {
        HouseEntranceBaseId or HouseEntranceAltBaseId => HousingDoorKind.HouseEntrance,
        HouseExitBaseId => HousingDoorKind.HouseExit,
        _ => null,
    };

    private static float Distance2D(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));

    // flow
    private enum Phase { FrontDoor, Dismount, Approach, Target, Interact, Confirm }

    private static void StartDoorFlow(Plugin plugin, HousingDoorKind kind) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _flowCts?.Cancel();
            var cts = _flowCts = new CancellationTokenSource();
            var tag = $"[Housing:{kind}]";

            var door = FindNearestDoor(SearchDistance, out _, out var distance, kind);
            var phase = Phase.Dismount;
            if (kind == HousingDoorKind.HouseExit && (door == null || distance > InteractDistance)) {
                // inside a house the exit can be far away (or not loaded): use "move to front door" first
                MoveToFrontDoor();
                phase = Phase.FrontDoor;
            } else if (door == null) {
                DalamudApi.PluginLog.Warning($"{tag} no door within {SearchDistance}y");
                return;
            }

            var phaseStartMs = Environment.TickCount64;
            long lastInteractMs = 0;
            long lastDismountMs = 0;
            var interactAttempts = 0;
            var moveIssued = false;
            var done = false;

            void SetPhase(Phase next) {
                phase = next;
                phaseStartMs = Environment.TickCount64;
            }

            Coroutine.StartRunOnFramework(
                runFunction: () => {
                    var player = DalamudApi.ObjectTable.LocalPlayer;
                    if (player == null || DalamudApi.Condition[ConditionFlag.BetweenAreas]) return;
                    var now = Environment.TickCount64;

                    switch (phase) {
                        case Phase.FrontDoor:
                            // give the native move a moment, then walk the remaining distance if needed
                            door = FindNearestDoor(SearchDistance, out _, out distance, kind);
                            if (door != null && (distance <= InteractDistance || now - phaseStartMs > 1500)) SetPhase(Phase.Dismount);
                            else if (now - phaseStartMs > 5000) Fail("exit door not found after moving to the front door");
                            break;

                        case Phase.Dismount:
                            if (!DalamudApi.Condition[ConditionFlag.Mounted]) { SetPhase(Phase.Approach); break; }
                            if (now - lastDismountMs > 1000) {
                                lastDismountMs = now;
                                GameActionManager.UseGeneralAction(DismountGeneralActionId);
                            }
                            break;

                        case Phase.Approach:
                            if (door == null || !door.IsValid()) { Fail("door object no longer valid"); break; }
                            if (Distance2D(player.Position, door.Position) <= InteractDistance) {
                                if (moveIssued) plugin.MovementManager.StopMove();
                                SetPhase(Phase.Target);
                                break;
                            }
                            if (!moveIssued) {
                                moveIssued = true;
                                plugin.MovementManager.MoveTo(door.GameObjectId);
                            }
                            break;

                        case Phase.Target:
                            if (DalamudApi.TargetManager.Target?.GameObjectId == door!.GameObjectId) { SetPhase(Phase.Interact); break; }
                            DalamudApi.TargetManager.Target = door;
                            break;

                        case Phase.Interact:
                            if (IsExpectedDialogOpen(kind)) { SetPhase(Phase.Confirm); break; }
                            if (now - lastInteractMs <= InteractRetryMs) break;
                            if (interactAttempts++ >= MaxInteractAttempts) { Fail("dialog did not open (door locked?)"); break; }
                            lastInteractMs = now;
                            door!.Interact();
                            break;

                        case Phase.Confirm:
                            if (TryConfirm(kind)) done = true;
                            else if (!IsExpectedDialogOpen(kind)) SetPhase(Phase.Interact); // dialog closed: retry interact
                            break;
                    }
                },
                stopWhen: () => done || cts.IsCancellationRequested,
                callback: () => {
                    if (_flowCts == cts) _flowCts = null;
                    if (done) return;
                    if (moveIssued) plugin.MovementManager.StopMove();
                    if (cts.IsCancellationRequested) return;
                    DalamudApi.PluginLog.Warning($"{tag} timeout in phase {phase}");
                },
                timeoutMs: FlowTimeoutMs);

            void Fail(string reason) {
                DalamudApi.PluginLog.Warning($"{tag} {reason}");
                cts.Cancel();
            }
        });
    }

    private static bool IsExpectedDialogOpen(HousingDoorKind kind) =>
        GameDialogManager.IsAddonReady(GameDialogManager.AddonName.SelectYesno);

    private static bool TryConfirm(HousingDoorKind kind) {
        switch (kind) {
            case HousingDoorKind.HouseEntrance:
                // only accept the entrance question
                var expected = TextConfirmHouseEntrance;
                if (!string.IsNullOrEmpty(expected)
                    && GameDialogManager.TryGetSelectYesnoText(out var text)
                    && !Normalize(text).Contains(Normalize(expected), StringComparison.OrdinalIgnoreCase))
                    return false;
                return GameDialogManager.ClickYes();

            default:
                return GameDialogManager.ClickYes();
        }
    }

    private static string Normalize(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
}
