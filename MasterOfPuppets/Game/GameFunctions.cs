using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

public static class GameFunctions {
    private static class Signatures {
        internal const string AbandonDuty = "E8 ?? ?? ?? ?? 41 B2 01 EB 39";
    }

    private delegate void AbandonDutyDelegate(bool a1);
    private static AbandonDutyDelegate? _abandonDuty { get; }

    static GameFunctions() {
        if (DalamudApi.SigScanner.TryScanText(Signatures.AbandonDuty, out var abandonDutyAddr)) {
            _abandonDuty = Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(abandonDutyAddr);
        }
    }

    // qst gamefunction
    public static void AbandonDuty() => _abandonDuty(false);

    public static unsafe Vector3? GetCharacterPositionByName(string targetName) {
        try {
            if (string.IsNullOrWhiteSpace(targetName)) {
                DalamudApi.PluginLog.Warning($"Invalid target: \"{targetName}\"");
                return null;
            }

            foreach (var actor in DalamudApi.Objects) {
                if (actor == null) continue;
                var lookupName = actor.Name.TextValue;
                if (actor.ObjectKind == ObjectKind.Player) {
                    var playerObject = actor as IPlayerCharacter;
                    var playerWorld = playerObject.HomeWorld.ValueNullable?.Name;
                    if (playerWorld != null)
                        lookupName = $"{lookupName}@{playerWorld}";
                }
                // DalamudApi.PluginLog.Warning($"Target: \"{lookupName}\"");

                if (!lookupName.Contains(targetName, StringComparison.InvariantCultureIgnoreCase)
                    || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;

                return actor.Position;
            }

            return null;

        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while targeting \"{targetName}\"");
            return null;
        }
    }

}
