using System;

using Dalamud.Memory;

namespace MasterOfPuppets.Util;

// from: https://github.com/Haselnussbomber/HaselCommon/blob/main/HaselCommon/Utils/MemoryReplacement.cs
public class MemoryReplacement(nint address, byte[] replacementBytes) : IDisposable {
    private byte[]? _originalBytes;

    public void Enable() {
        if (_originalBytes != null)
            return;

        _originalBytes = ReplaceRaw(address, replacementBytes);
    }

    public void Disable() {
        if (_originalBytes == null)
            return;

        ReplaceRaw(address, _originalBytes);
        _originalBytes = null;
    }

    public void Dispose() {
        Disable();
    }

    public static byte[] ReplaceRaw(nint address, byte[] data) {
        var originalBytes = MemoryHelper.ReadRaw(address, data.Length);

        MemoryHelper.ChangePermission(address, data.Length, MemoryProtection.ExecuteReadWrite, out var oldPermissions);
        MemoryHelper.WriteRaw(address, data);
        MemoryHelper.ChangePermission(address, data.Length, oldPermissions);

        return originalBytes;
    }
}
