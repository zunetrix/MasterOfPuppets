using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class TextureProviderExt
{
    /// <summary>
    /// Get the texture for an icon if possible, if the icon isn't valid use
    /// a fallback icon.
    /// </summary>
    public static ISharedImmediateTexture GetMacroIcon(
        this ITextureProvider self,
        uint iconId
    )
    {
        uint undefinedIconId = 60042;
        return self.GetIconOrFallback(iconId, undefinedIconId);
    }

    /// <summary>
    /// Get the texture for an icon if possible, if the icon isn't valid use
    /// a fallback icon.
    /// </summary>
    public static ISharedImmediateTexture GetIconOrFallback(
        this ITextureProvider self,
        uint iconId,
        uint fallback
    )
    {
        if (self.TryGetFromGameIcon(iconId, out var iconTexture))
        {
            return iconTexture;
        }
        else
        {
            return self.GetFromGameIcon(fallback);
        }
    }
}
