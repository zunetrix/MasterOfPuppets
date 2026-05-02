using System;

// using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

// using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public class GameRenderManager : IDisposable {
    private bool _enabled;
    public bool Enabled => _enabled;

    // public delegate void RenderModelsDelegate(nint thisPtr, nint a2);
    // public delegate void RenderModel5Delegate(nint thisPtr, uint a2, nint a3, byte a4, nint a5, nint a6);
    // public delegate void ModelRendererDelegate(nint thisPtr);
    // public delegate void RenderHumanDelegate(nint thisPtr);
    // public delegate void RenderCharaBaseDelegate(nint thisPtr);
    // public delegate void RenderCharaBaseMatDelegate(nint thisPtr);
    // public delegate void CharaUpdateAnimationsDelegate(nint thisPtr);
    // public delegate void RenderTerrainDelegate(nint thisPtr);
    // public delegate void RenderWaterDelegate(nint thisPtr);
    // public delegate void RenderLightsDelegate(nint thisPtr, nint a2, float a3);
    // public delegate void CameraSetMatricesDelegate(nint thisPtr, nint a2);
    // public delegate void GeometryRendererDelegate(nint thisPtr);

    // public readonly HookEntry<RenderModelsDelegate> RenderModels;
    // public readonly HookEntry<RenderModel5Delegate> RenderModel5;
    // public readonly HookEntry<ModelRendererDelegate> ModelRenderer;
    // public readonly HookEntry<RenderHumanDelegate> RenderHuman;
    // public readonly HookEntry<RenderCharaBaseDelegate> RenderCharaBase;
    // public readonly HookEntry<RenderCharaBaseMatDelegate> RenderCharaBaseMat;
    // public readonly HookEntry<VfxObject.Delegates.UpdateRender> RenderVfxObject;
    // public readonly HookEntry<CharaUpdateAnimationsDelegate> CharaAnimations;
    // public readonly HookEntry<RenderTerrainDelegate> RenderTerrain;
    // public readonly HookEntry<RenderWaterDelegate> RenderWater;
    // public readonly HookEntry<RenderLightsDelegate> RenderLights;
    // public readonly HookEntry<CameraSetMatricesDelegate> CameraMatrices;
    // public readonly HookEntry<GeometryRendererDelegate> GeometryRenderer;


    // .text:00000001402B9F20 ; =============== S U B R O U T I N E =======================================
    // .text:00000001402B9F20
    // .text:00000001402B9F20
    // .text:00000001402B9F20 sub_1402B9F20   proc near               ; CODE XREF: .text:00000001400D4267↑j
    // .text:00000001402B9F20                 push    rbx
    // .text:00000001402B9F22                 push    rdi
    // .text:00000001402B9F23                 push    r12
    // .text:00000001402B9F25                 push    r13
    // .text:00000001402B9F27                 sub     rsp, 58h
    // .text:00000001402B9F2B                 mov     rax, gs:58h
    // .text:00000001402B9F34                 mov     r13, rcx
    // .text:00000001402B9F37                 mov     edx, cs:TlsIndex
    // .text:00000001402B9F3D                 mov     rbx, cs:qword_1428EFF80
    // .text:00000001402B9F44                 mov     r12, [rax+rdx*8]
    // .text:00000001402B9F48                 mov     eax, 203C4h
    // .text:00000001402B9F4D                 cmp     byte ptr [rax+r12], 0
    // .text:00000001402B9F52                 jnz     short loc_1402B9F59
    // .text:00000001402B9F54                 call    sub_141D96724
    // .text:00000001402B9F59
    // .text:00000001402B9F59 loc_1402B9F59:                          ; CODE XREF: sub_1402B9F20+32↑j
    // .text:00000001402B9F59                 cmp     byte ptr [r13+38358h], 0
    // .text:00000001402B9F61                 mov     eax, 238h
    // .text:00000001402B9F66                 mov     [rsp+98h], r14
    // .text:00000001402B9F6E                 mov     [rsp+50h], r15
    // .text:00000001402B9F73                 mov     rdi, [rax+r12]
    // .text:00000001402B9F77                 jnz     loc_1402BA4A2
    // .text:00000001402B9F7D                 cmp     dword ptr [r13+3834Ch], 0FFFFFFFFh
    // .text:00000001402B9F85                 jnz     loc_1402BA4A2
    // .text:00000001402B9F8B                 mov     [rsp+88h], rbp
    // .text:00000001402B9F93                 xor     r15d, r15d
    // .text:00000001402B9F96                 mov     [rsp+90h], rsi
    // .text:00000001402B9F9E                 mov     rsi, cs:qword_1428F5E18
    // .text:00000001402B9FA5                 mov     [rsi+2470h], r15
    // .text:00000001402B9FAC                 mov     rcx, cs:qword_1428F6710
    // .text:00000001402B9FB3                 call    sub_1402D7290
    // .text:00000001402B9FB8                 movzx   eax, byte ptr [r13+2EF0h]
    // .text:00000001402B9FC0                 test    al, 1
    // .text:00000001402B9FC2                 jz      loc_1402BA073
    // .text:00000001402B9FC8                 mov     rcx, [r13+29DD0h]
    // .text:00000001402B9FCF                 lea     rdi, [r13+29D40h]
    // .text:00000001402B9FD6                 mov     rbp, [r13+3028h]
    // .text:00000001402B9FDD                 lea     r9d, [r15+2]
    // .text:00000001402B9FE1                 xor     r8d, r8d
    // .text:00000001402B9FE4                 xor     edx, edx
    // .text:00000001402B9FE6                 call    sub_14021F4E0
    // .text:00000001402B9FEB                 test    rax, rax
    // .text:00000001402B9FEE                 jz      short loc_1402BA00B
    // .text:00000001402B9FF0                 mov     ecx, [rdi+2D8h]
    // .text:00000001402B9FF6                 mov     [rax], ecx
    // .text:00000001402B9FF8                 mov     [rax+4], r15
    // .text:00000001402B9FFC                 mov     [rax+0Ch], r15d
    // .text:00000001402BA000                 mov     rax, [rdi+90h]
    // .text:00000001402BA007                 mov     [rax+30h], r15d
    // .text:00000001402BA00B
    // .text:00000001402BA00B loc_1402BA00B:                          ; CODE XREF: sub_1402B9F20+CE↑j
    // .text:00000001402BA00B                 mov     rcx, [rdi+1A0h]
    // .text:00000001402BA012                 xor     edx, edx
    // .text:00000001402BA014                 lea     r9d, [rdx+2]
    // .text:00000001402BA018                 lea     r8d, [rdx+10h]
    // .text:00000001402BA01C                 call    sub_14021F4E0
    // .text:00000001402BA021                 test    rax, rax
    // .text:00000001402BA024                 jz      short loc_1402BA04C
    // .text:00000001402BA026                 mov     dword ptr [rax], 3BA3D70Ah
    // .text:00000001402BA02C                 mov     dword ptr [rax+4], 3BA3D70Ah
    // .text:00000001402BA033                 mov     dword ptr [rax+8], 3BA3D70Ah
    // .text:00000001402BA03A                 mov     dword ptr [rax+0Ch], 41200000h
    // .text:00000001402BA041                 mov     rax, [rdi+1A0h]
    // .text:00000001402BA048                 mov     [rax+30h], r15d
    // .text:00000001402BA04C
    // .text:00000001402BA04C loc_1402BA04C:                          ; CODE XREF: sub_1402B9F20+104↑j
    // .text:00000001402BA04C                 lea     rdx, [rbp+90h]
    // .text:00000001402BA053                 mov     rcx, rdi
    // .text:00000001402BA056                 call    sub_14028F8E0
    // .text:00000001402BA05B                 movzx   eax, byte ptr [r13+2EF0h]
    // .text:00000001402BA063                 test    al, 1
    // .text:00000001402BA065                 jz      short loc_1402BA073
    // .text:00000001402BA067                 lea     rcx, [r13+10AF8h]
    // .text:00000001402BA06E                 call    sub_140281660
    // .text:00000001402BA073
    // .text:00000001402BA073 loc_1402BA073:                          ; CODE XREF: sub_1402B9F20+A2↑j
    // .text:00000001402BA073                                         ; sub_1402B9F20+145↑j
    // .text:00000001402BA073                 mov     rcx, cs:qword_142A8EF80
    // .text:00000001402BA07A                 call    sub_14025CD10
    // .text:00000001402BA07F                 call    sub_140393EC0
    // .text:00000001402BA084                 call    sub_1403941B0
    // .text:00000001402BA089                 mov     edi, r15d
    // .text:00000001402BA08C                 lea     r14, [r13+3837Ch]
    // .text:00000001402BA093                 lea     rbp, [r13+3835Ch]
    // .text:00000001402BA09A                 nop     word ptr [rax+rax+00h]
    // .text:00000001402BA0A0
    // .text:00000001402BA0A0 loc_1402BA0A0:                          ; CODE XREF: sub_1402B9F20+2C2↓j

    // [Signature("83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 B4 24")] // 7.4

    // 7.5 - strong sig anchor to function
    // 1402B9F73                 mov     rdi, [rax+r12]
    // 4A 8B 3C 20 0f 85 ?? ?? ?? ?? 41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 ?? ??

    [Signature("41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 AC 24")] // 7.5
    private nint? _renderSkipMemoryAddress = null;

    [Signature("41 83 BD ?? ?? ?? ?? ?? 75 ?? 48 8B 0D")] // 7.5
    private nint? _renderSkipPostEffectMemoryAddress = null;

    private MemoryReplacement? _memoryPatch;
    private MemoryReplacement? _memoryPatch2;

    // private static Hook<T>? Sig<T>(string sig, T detour) where T : Delegate
    //     => DalamudApi.GameInteropProvider.HookFromSignature<T>(sig, detour);

    public GameRenderManager(Plugin plugin) {
        // _plugin = plugin;

        /*
        RenderModels = new(
            Sig<RenderModelsDelegate>("E8 ?? ?? ?? ?? 48 83 7C 24 ?? ?? 48 8B 5C 24", (_, _) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        RenderModel5 = new(
            Sig<RenderModel5Delegate>("40 53 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? F6 41", (_, _, _, _, _, _) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        ModelRenderer = new(
            Sig<ModelRendererDelegate>("40 53 48 83 EC ?? 65 48 8B 04 25 ?? ?? ?? ?? 48 8B D9 8B 15 ?? ?? ?? ?? 48 89 74 24", (_) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        RenderHuman = new(
            Sig<RenderHumanDelegate>("40 53 48 83 EC ?? 48 8B D9 E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 83 BB", (_) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        RenderCharaBase = new(
            Sig<RenderCharaBaseDelegate>("48 89 5C 24 ?? 57 48 83 EC ?? 33 FF 48 8B D9 89 B9 ?? ?? ?? ?? 40 88 B9", (_) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        RenderCharaBaseMat = new(
            Sig<RenderCharaBaseMatDelegate>("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 83 EC ?? 4C 89 7C 24", (_) => { }),
            () => _plugin.Config.DisableModels,
            v => _plugin.Config.DisableModels = v,
            _plugin.Config.Save);

        RenderVfxObject = new(
            Sig<VfxObject.Delegates.UpdateRender>("48 89 7C 24 ?? 55 48 8B EC 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 0F B6 41", (_) => { }),
            () => _plugin.Config.DisableVfxObject,
            v => _plugin.Config.DisableVfxObject = v,
            _plugin.Config.Save);

        CharaAnimations = new(
            Sig<CharaUpdateAnimationsDelegate>("48 89 5C 24 ?? 57 48 83 EC ?? 48 8D 59 ?? BF ?? ?? ?? ?? 48 8B 0B 48 85 C9 74 ?? E8", (_) => { }),
            () => _plugin.Config.DisableCharaAnimations,
            v => _plugin.Config.DisableCharaAnimations = v,
            _plugin.Config.Save);

        RenderTerrain = new(
            Sig<RenderTerrainDelegate>("48 89 5C 24 ?? 57 48 83 EC ?? 65 48 8B 04 25 ?? ?? ?? ?? 48 8B F9 8B 15 ?? ?? ?? ?? 48 8B 1C ?? B8 ?? ?? ?? ?? 80 3C ?? ?? 75 ?? E8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 48 8B 1C", (_) => { }),
            () => _plugin.Config.DisableTerrain,
            v => _plugin.Config.DisableTerrain = v,
            _plugin.Config.Save);

        RenderWater = new(
            Sig<RenderWaterDelegate>("4C 8B DC 55 57 49 8D AB ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 80 B9 ?? ?? ?? ?? ?? 48 8B F9 0F 84", (_) => { }),
            () => _plugin.Config.DisableWater,
            v => _plugin.Config.DisableWater = v,
            _plugin.Config.Save);

        RenderLights = new(
            Sig<RenderLightsDelegate>("40 53 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 8B D9 F3 0F 10 05", (_, _, _) => { }),
            () => _plugin.Config.DisableLights,
            v => _plugin.Config.DisableLights = v,
            _plugin.Config.Save);

        CameraMatrices = new(
            Sig<CameraSetMatricesDelegate>("E8 ?? ?? ?? ?? 0F 10 43 ?? C6 83", (_, _) => { }),
            () => _plugin.Config.DisableCameraMatrices,
            v => _plugin.Config.DisableCameraMatrices = v,
            _plugin.Config.Save);
        */

        // GeometryRenderer = new(
        //     Sig<GeometryRendererDelegate>("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 33 DB 4C 8D 99", (_) => { }),
        //     () => _plugin.Config.DisableGeometryRenderer,
        //     v => _plugin.Config.DisableGeometryRenderer = v,
        //     _plugin.Config.Save);

        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);

        // RestoreAll();
    }

    // private void RestoreAll() {
    //     RenderModels.RestoreFromConfig();
    //     RenderModel5.RestoreFromConfig();
    //     ModelRenderer.RestoreFromConfig();
    //     RenderHuman.RestoreFromConfig();
    //     RenderCharaBase.RestoreFromConfig();
    //     RenderCharaBaseMat.RestoreFromConfig();
    //     RenderVfxObject.RestoreFromConfig();
    //     CharaAnimations.RestoreFromConfig();
    //     RenderTerrain.RestoreFromConfig();
    //     RenderWater.RestoreFromConfig();
    //     RenderLights.RestoreFromConfig();
    //     CameraMatrices.RestoreFromConfig();
    //     GeometryRenderer.RestoreFromConfig();
    // }

    public void DisableRendering(bool disabled) {
        // _plugin.Config.DisableRendering = disabled;
        if (disabled) {
            if (_memoryPatch != null || _memoryPatch2 != null) return;

            if (_renderSkipMemoryAddress is { } addr && addr != nint.Zero) {
                // skip the conditional jump
                // MemoryReplacement.ReplaceRaw(_renderMemoryAddress.Value + 0x7, [0xFF]);
                _memoryPatch = new MemoryReplacement(addr + 0x7, [0x1]);
                _memoryPatch.Enable();

                _enabled = true;
                DalamudApi.ChatGui.Print("", "MOP: RenderHack ON", Style.Colors.SeGreen);
            }

            if (_renderSkipPostEffectMemoryAddress is { } addr2 && addr2 != nint.Zero) {
                _memoryPatch2 = new MemoryReplacement(addr2 + 0x7, [0x1]);
                _memoryPatch2.Enable();
            }
        } else {
            if (_memoryPatch == null) return;
            _memoryPatch.Disable();
            _memoryPatch = null;

            _memoryPatch2.Disable();
            _memoryPatch2 = null;

            _enabled = false;
            DalamudApi.ChatGui.Print("", "MOP: RenderHack OFF", Style.Colors.SeRed);
        }
    }

    public void Dispose() {
        // RenderModels.Dispose();
        // RenderModel5.Dispose();
        // ModelRenderer.Dispose();
        // RenderHuman.Dispose();
        // RenderCharaBase.Dispose();
        // RenderCharaBaseMat.Dispose();
        // RenderVfxObject.Dispose();
        // CharaAnimations.Dispose();
        // RenderTerrain.Dispose();
        // RenderWater.Dispose();
        // RenderLights.Dispose();
        // CameraMatrices.Dispose();
        // GeometryRenderer.Dispose();
        _enabled = false;
        _memoryPatch?.Dispose();
        _memoryPatch = null;

        _memoryPatch2?.Dispose();
        _memoryPatch2 = null;
    }
}

// public class HookEntry<T> where T : Delegate {
//     public Hook<T>? Hook { get; }
//     private readonly Func<bool> _getConfig;
//     private readonly Action<bool> _setConfig;
//     private readonly Action _save;

//     public bool IsEnabled => _getConfig();

//     public HookEntry(Hook<T>? hook, Func<bool> get, Action<bool> set, Action save) {
//         Hook = hook;
//         _getConfig = get;
//         _setConfig = set;
//         _save = save;
//     }

//     public void RestoreFromConfig() {
//         if (_getConfig() && Hook is { IsEnabled: false })
//             Hook.Enable();
//     }

//     public void Toggle() {
//         bool next = !_getConfig();
//         _setConfig(next);
//         ApplyHook(next);
//         _save();
//     }

//     public void ApplyHook(bool enabled) {
//         if (enabled) Hook?.Enable();
//         else Hook?.Disable();
//     }

//     public void Dispose() => Hook?.Dispose();
// }
