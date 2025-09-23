using System;
using System.Runtime.InteropServices;

namespace MasterOfPuppets;

public static class MovementManager
{
    private static class Signatures
    {
        internal const string WalkMode = "40 38 35 ?? ?? ?? ?? 75 2D";
    }

    // set byte 1 to walk
    private delegate void ToggleWalkDelegate(uint arg1);

    private static ToggleWalkDelegate? _toggleWalk { get; }
    private static readonly uint WalkStatusEnabled = 1;
    private static readonly uint WalkStatusDisabled = 0;

    static MovementManager()
    {
        // _toggleWalk = Marshal.GetDelegateForFunctionPointer<ToggleWalkDelegate>(DalamudApi.SigScanner.ScanText(Signatures.WalkMode));
        // var _toggleWalk2 = (IntPtr*)DalamudApi.SigScanner.GetStaticAddressFromSig(Signatures.WalkMode);

        if (DalamudApi.SigScanner.TryScanText(Signatures.WalkMode, out var _toggleWalkPtr))
        {
            _toggleWalk = Marshal.GetDelegateForFunctionPointer<ToggleWalkDelegate>(_toggleWalkPtr);
        }
    }

    public static void EnableWalk()
    {
        if (_toggleWalk == null)
        {
            DalamudApi.PluginLog.Error($"Could not find signature for toggle walk");
            return;
        }

        try
        {
            _toggleWalk(WalkStatusEnabled);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Could not execute toggle walk");
        }
    }

    public static void DisableWalk()
    {
        if (_toggleWalk == null)
        {
            DalamudApi.PluginLog.Error($"Could not find signature for toggle walk");
            return;
        }

        try
        {
            _toggleWalk(WalkStatusDisabled);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Could not execute toggle walk");
        }
    }
}
