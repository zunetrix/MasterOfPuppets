using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace MasterOfPuppets;

// from: https://github.com/PunishXIV/Questionable/blob/new-main/Questionable/Functions/GameFunctions.cs
public static class GameFunctions {
    // 7.3
    // private static class Signatures {
    //     internal const string AbandonDuty = "E8 ?? ?? ?? ?? 41 B2 01 EB 39";
    // }

    // private static AbandonDutyDelegate? _abandonDuty { get; }

    // static GameFunctions() {
    //     if (DalamudApi.SigScanner.TryScanText(Signatures.AbandonDuty, out var abandonDutyAddr)) {
    //         _abandonDuty = Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(abandonDutyAddr);
    //     }
    // }

    private delegate void AbandonDutyDelegate(bool a1);
    private static readonly AbandonDutyDelegate _abandonDuty =
            Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(EventFramework.Addresses.LeaveCurrentContent.Value);

    public static void AbandonDuty() => _abandonDuty(false);


    // full character name data: name@world
    public static unsafe void InviteToParty(string character) {
        if (string.IsNullOrWhiteSpace(character))
            return;

        var atIndex = character.IndexOf('@');

        if (atIndex <= 0 || atIndex == character.Length - 1)
            return;

        var charName = character[..atIndex];
        var worldName = character[(atIndex + 1)..];

        var worldId = WorldHelper.GetWorldId(worldName);
        if (worldId is null)
            return;

        DalamudApi.Framework.RunOnTick(() => {
            InfoProxyPartyInvite.Instance()
                ->InviteToParty(0, charName, (ushort)worldId.Value);
        });
    }
}
