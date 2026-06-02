using System;

using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace MasterOfPuppets;

public class GameRenderManager : IDisposable {
    private bool _enabled;
    public bool Enabled => _enabled;

    public unsafe void DisableRendering(bool disabled) {
        Manager* manager = Manager.Instance();
        if (manager == null) return;

        if (disabled) {
            manager->Is3DRenderingDisabled = true;
            _enabled = true;
            DalamudApi.ChatGui.Print("", "MOP: RenderHack ON", Style.Colors.SeGreen);
        } else {
            manager->Is3DRenderingDisabled = false;
            _enabled = false;
            DalamudApi.ChatGui.Print("", "MOP: RenderHack OFF", Style.Colors.SeRed);
        }
    }

    public GameRenderManager(Plugin plugin) {
        // _plugin = plugin;
    }

    public void Dispose() {
        DisableRendering(false);
    }
}

// using Dalamud.Utility.Signatures;
// using MasterOfPuppets.Util;
// public class GameRenderManager : IDisposable {
//     private bool _enabled;
//     public bool Enabled => _enabled;

//     // [Signature("83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 B4 24")] // 7.4

//     // 7.5 - sig anchor to function
//     // 1402B9F73                 mov     rdi, [rax+r12]
//     // 4A 8B 3C 20 0f 85 ?? ?? ?? ?? 41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 ?? ??

//     [Signature("41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 AC 24")] // 7.5
//     private nint? _renderSkipMemoryAddress = null;

//     [Signature("41 83 BD ?? ?? ?? ?? ?? 75 ?? 48 8B 0D")] // 7.5
//     private nint? _renderSkipPostEffectMemoryAddress = null;

//     private MemoryReplacement? _memoryPatch;
//     private MemoryReplacement? _memoryPatch2;

//     public GameRenderManager(Plugin plugin) {
//         // _plugin = plugin;

//         DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
//     }


//     public void DisableRendering(bool disabled) {
//         // _plugin.Config.DisableRendering = disabled;
//         if (disabled) {
//             if (_memoryPatch != null || _memoryPatch2 != null) return;

//             if (_renderSkipMemoryAddress is { } addr && addr != nint.Zero) {
//                 // skip the conditional jump
//                 // MemoryReplacement.ReplaceRaw(_renderMemoryAddress.Value + 0x7, [0xFF]);
//                 _memoryPatch = new MemoryReplacement(addr + 0x7, [0x1]);
//                 _memoryPatch.Enable();

//                 _enabled = true;
//                 DalamudApi.ChatGui.Print("", "MOP: RenderHack ON", Style.Colors.SeGreen);
//             }

//             if (_renderSkipPostEffectMemoryAddress is { } addr2 && addr2 != nint.Zero) {
//                 _memoryPatch2 = new MemoryReplacement(addr2 + 0x7, [0x1]);
//                 _memoryPatch2.Enable();
//             }
//         } else {
//             if (_memoryPatch == null) return;
//             _memoryPatch.Disable();
//             _memoryPatch = null;

//             _memoryPatch2.Disable();
//             _memoryPatch2 = null;

//             _enabled = false;
//             DalamudApi.ChatGui.Print("", "MOP: RenderHack OFF", Style.Colors.SeRed);
//         }
//     }

//     public void Dispose() {
//         _enabled = false;
//         _memoryPatch?.Dispose();
//         _memoryPatch = null;

//         _memoryPatch2?.Dispose();
//         _memoryPatch2 = null;
//     }
// }
