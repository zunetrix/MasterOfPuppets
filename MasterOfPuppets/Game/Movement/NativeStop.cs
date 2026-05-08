using Dalamud.Utility.Signatures;

namespace MasterOfPuppets.Movement;

internal unsafe sealed class NativeStop {
    [Signature("74 0C 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ??", ScanType = ScanType.StaticAddress)]
    private nint _moveControllerSubMemberForMineInstance = 0;

    [Signature("40 53 48 83 EC ?? 48 8B 41 20 48 8B D9 80 B8 02 02 00 00 ??", ScanType = ScanType.Text)]
    private readonly delegate* unmanaged<nint, long> _moveStop = null;

    public NativeStop() {
        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
        DalamudApi.PluginLog.Debug($"[SimpleInputMovement] MoveControllerInstance: {_moveControllerSubMemberForMineInstance:X}");
    }

    public void Stop() {
        if (_moveStop == null || _moveControllerSubMemberForMineInstance == 0)
            return;

        try {
            _moveStop(_moveControllerSubMemberForMineInstance);
        } catch {
            // ignored
        }
    }
}
