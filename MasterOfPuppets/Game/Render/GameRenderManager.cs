using System;

using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

// using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public class GameRenderManager : IDisposable {
    private readonly Plugin _plugin;

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


    // [Signature("83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 B4 24")] // 7.4

    // 1402B9F73                 mov     rdi, [rax+r12]
    // 4A 8B 3C 20 0f 85 ?? ?? ?? ?? 41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 ?? ??


    // 1402B9F20 ; =============== S U B R O U T I N E =======================================
    // 1402B9F20
    // 1402B9F20
    // 1402B9F20 sub_1402B9F20   proc near               ; CODE XREF: sub_1400D4260+7↑j
    // 1402B9F20                                         ; DATA XREF: .rdata:00000001425EA4C8↓o ...
    // 1402B9F20
    // 1402B9F20 var_58          = dword ptr -58h
    // 1402B9F20 var_50          = dword ptr -50h
    // 1402B9F20 var_48          = dword ptr -48h
    // 1402B9F20 var_40          = dword ptr -40h
    // 1402B9F20 var_38          = xmmword ptr -38h
    // 1402B9F20 var_28          = qword ptr -28h
    // 1402B9F20 arg_0           = qword ptr  8
    // 1402B9F20 arg_8           = qword ptr  10h
    // 1402B9F20 arg_10          = qword ptr  18h
    // 1402B9F20 arg_18          = qword ptr  20h
    // 1402B9F20
    // 1402B9F20                 push    rbx
    // 1402B9F22                 push    rdi
    // 1402B9F23                 push    r12
    // 1402B9F25                 push    r13
    // 1402B9F27                 sub     rsp, 58h
    // 1402B9F2B                 mov     rax, gs:58h
    // 1402B9F34                 mov     r13, rcx
    // 1402B9F37                 mov     edx, cs:TlsIndex
    // 1402B9F3D                 mov     rbx, cs:qword_1428EFF80
    // 1402B9F44                 mov     r12, [rax+rdx*8]
    // 1402B9F48                 mov     eax, 203C4h
    // 1402B9F4D                 cmp     byte ptr [rax+r12], 0
    // 1402B9F52                 jnz     short loc_1402B9F59
    // 1402B9F54                 call    __dyn_tls_on_demand_init
    // 1402B9F59
    // 1402B9F59 loc_1402B9F59:                          ; CODE XREF: sub_1402B9F20+32↑j
    // 1402B9F59                 cmp     byte ptr [r13+38358h], 0
    // 1402B9F61                 mov     eax, 238h
    // 1402B9F66                 mov     [rsp+78h+arg_18], r14
    // 1402B9F6E                 mov     [rsp+78h+var_28], r15
    // 1402B9F73                 mov     rdi, [rax+r12]
    // 1402B9F77                 jnz     loc_1402BA4A2
    // 1402B9F7D                 cmp     dword ptr [r13+3834Ch], 0FFFFFFFFh
    // 1402B9F85                 jnz     loc_1402BA4A2
    // 1402B9F8B
    [Signature("41 83 BD ?? ?? ?? ?? ?? 0F 85 ?? ?? ?? ?? 48 89 AC 24")] // 7.5
    private nint? _renderSkipMemoryAddress;

    // [Signature("41 83 BD ?? ?? ?? ?? ?? 75 ?? 48 8B 0D")] // 7.5
    // private nint? _renderSkipPostEffectMemoryAddress;

    private MemoryReplacement? _memoryPatch;
    // private MemoryReplacement? _memoryPatch2;

    private static Hook<T>? Sig<T>(string sig, T detour) where T : Delegate
        => DalamudApi.GameInteropProvider.HookFromSignature<T>(sig, detour);

    public GameRenderManager(Plugin plugin) {
        _plugin = plugin;

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

    private void RestoreAll() {
        // RenderModels.RestoreFromConfig();
        // RenderModel5.RestoreFromConfig();
        // ModelRenderer.RestoreFromConfig();
        // RenderHuman.RestoreFromConfig();
        // RenderCharaBase.RestoreFromConfig();
        // RenderCharaBaseMat.RestoreFromConfig();
        // RenderVfxObject.RestoreFromConfig();
        // CharaAnimations.RestoreFromConfig();
        // RenderTerrain.RestoreFromConfig();
        // RenderWater.RestoreFromConfig();
        // RenderLights.RestoreFromConfig();
        // CameraMatrices.RestoreFromConfig();
        // GeometryRenderer.RestoreFromConfig();
    }

    public void DisableRendering(bool toggle) {
        _plugin.Config.DisableRendering = toggle;
        if (toggle) {
            if (_memoryPatch != null) return;

            if (_renderSkipMemoryAddress is { } addr && addr != nint.Zero) {
                // .text:00000001402AF90B                 cmp     dword ptr [rbp+4033Ch], 0FFFFFFFFh
                // skip the conditional jump
                // MemoryReplacement.ReplaceRaw(_renderMemoryAddress.Value + 0x6, [0xFF]);
                _memoryPatch = new MemoryReplacement(addr + 0x6, [0x1]);
                _memoryPatch.Enable();
                DalamudApi.ChatGui.Print("", "MOP: RenderHack ON", Style.Colors.SeGreen);
            }

            // if (_renderSkipPostEffectMemoryAddress is { } addr2 && addr2 != nint.Zero) {
            //     _memoryPatch2 = new MemoryReplacement(addr2 + 0x6, [0x1]);
            //     _memoryPatch2.Enable();
            // }
        } else {
            if (_memoryPatch == null) return;
            _memoryPatch.Disable();
            _memoryPatch = null;

            // _memoryPatch2.Disable();
            // _memoryPatch2 = null;
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

        _memoryPatch?.Dispose();
        _memoryPatch = null;
    }
}

public class HookEntry<T> where T : Delegate {
    public Hook<T>? Hook { get; }
    private readonly Func<bool> _getConfig;
    private readonly Action<bool> _setConfig;
    private readonly Action _save;

    public bool IsEnabled => _getConfig();

    public HookEntry(Hook<T>? hook, Func<bool> get, Action<bool> set, Action save) {
        Hook = hook;
        _getConfig = get;
        _setConfig = set;
        _save = save;
    }

    public void RestoreFromConfig() {
        if (_getConfig() && Hook is { IsEnabled: false })
            Hook.Enable();
    }

    public void Toggle() {
        bool next = !_getConfig();
        _setConfig(next);
        ApplyHook(next);
        _save();
    }

    public void ApplyHook(bool enabled) {
        if (enabled) Hook?.Enable();
        else Hook?.Disable();
    }

    public void Dispose() => Hook?.Dispose();
}

