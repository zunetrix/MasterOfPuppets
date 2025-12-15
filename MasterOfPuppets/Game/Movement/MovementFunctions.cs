using System.Runtime.InteropServices;

namespace MasterOfPuppets.Movement;

public static class MovementFunctions {
    private static class Signatures {
        // internal const string ToggleWalk = "3B 51 ?? 7D ?? 48 8B 41 ?? 48 63 D2 ?? ?? ?? ?? 74 ?? ?? ?? ?? ?? C6 41 ?? ?? C3 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 3B 51";
        internal const string ToggleWalk = "E8 ?? ?? ?? ?? 41 89 9E ?? ?? ?? ?? 48 8B 8C 24";
    }

    // set byte 1 to walk
    private delegate void ToggleWalkDelegate(uint arg1);

    private static ToggleWalkDelegate? _toggleWalk { get; }

    static MovementFunctions() {
        if (DalamudApi.SigScanner.TryScanText(Signatures.ToggleWalk, out var _toggleWalkPtr)) {
            _toggleWalk = Marshal.GetDelegateForFunctionPointer<ToggleWalkDelegate>(_toggleWalkPtr);
        }
    }

    public static void EnableWalk() => _toggleWalk(1);

    public static void DisableWalk() => _toggleWalk(0);
}
