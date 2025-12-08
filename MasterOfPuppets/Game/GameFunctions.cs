using System.Runtime.InteropServices;

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

}
