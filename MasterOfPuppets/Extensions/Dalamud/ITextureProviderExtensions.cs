using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class ITextureProviderExtensions {
    public static void DrawIcon(this ITextureProvider textureProvider, GameIconLookup gameIconLookup, Vector2 size, Vector2? uv0 = null, Vector2? uv1 = null, Vector4? tintCol = null, Vector4? borderCol = null) {
        try {
            var iconTexture = DalamudApi.TextureProvider.GetFromGameIcon(gameIconLookup).GetWrapOrEmpty().Handle;
            ImGui.Image(
                iconTexture,
                size,
                uv0 ?? Vector2.Zero,
                uv1 ?? Vector2.One,
                tintCol ?? Vector4.One,
                borderCol ?? Vector4.Zero);
        } catch {
            ImGui.Dummy(size);
        }

        // if (!ImGui.IsRectVisible(size)
        //     || !textureProvider.TryGetFromGameIcon(gameIconLookup, out var texture)
        //     || !texture.TryGetWrap(out var wrap, out _)) {
        //     ImGui.Dummy(size);
        //     return;
        // }

        // ImGui.Image(wrap.Handle, size);
    }

    // var iconSize = ImGuiHelpers.ScaledVector2(50, 50);
    // var icon = DalamudApi.TextureProvider.GetFromGameIcon(undefinedIconId).GetWrapOrEmpty().Handle;
    // var icon = DalamudApi.TextureProvider.GetMacroIcon(_macroIconId).GetWrapOrEmpty().Handle;
    // ImGui.Image(icon, iconSize);

    public static ISharedImmediateTexture GetMacroIcon(
        this ITextureProvider self,
        uint iconId
    ) {
        uint undefinedIconId = 60042;
        return self.GetIconOrFallback(iconId, undefinedIconId);
    }

    public static ISharedImmediateTexture GetIconOrFallback(
        this ITextureProvider self,
        uint iconId,
        uint fallback
    ) {
        if (self.TryGetFromGameIcon(iconId, out var iconTexture)) {
            return iconTexture;
        } else {
            return self.GetFromGameIcon(fallback);
        }
    }
}

