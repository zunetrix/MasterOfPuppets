// using System;
// using System.IO;
// using System.Threading.Tasks;

// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.PixelFormats;
// using SixLabors.ImageSharp.Formats.Png;

// using Dalamud.Plugin.Services;
// using Dalamud.Utility;

// using Lumina.Data.Files;

namespace MasterOfPuppets.Util;

public static class TextureExporter {
    // public static async Task SaveTextureToPngAsync(TexFile texture, string outputPath) {
    //     try {
    //         var dir = Path.GetDirectoryName(outputPath);
    //         if (!string.IsNullOrEmpty(dir))
    //             Directory.CreateDirectory(dir);

    //         using var image = Image.LoadPixelData<Rgba32>(
    //             texture.GetRgbaImageData(),
    //             texture.Header.Width,
    //             texture.Header.Height);

    //         await image.SaveAsPngAsync(outputPath, new PngEncoder {
    //             CompressionLevel = PngCompressionLevel.BestCompression
    //         });
    //     } catch (Exception ex) {
    //         DalamudApi.PluginLog.Error(ex, $"Error while exporting image {outputPath}");
    //     }
    // }

    // public static async Task ExportIconToFileAsync(
    //     IDataManager dataManager,
    //     uint iconId,
    //     string outputPath,
    //     bool isHr = false) {
    //     try {
    //         var iconPath = GetIconPath(iconId, isHr);

    //         var texFile = dataManager.GetFile<TexFile>(iconPath)
    //             ?? throw new FileNotFoundException($"Texture {iconId} not found.");

    //         await SaveTextureToPngAsync(texFile, outputPath);
    //     } catch (Exception ex) {
    //         DalamudApi.PluginLog.Error(ex, $"Error while exportions icon {iconId} to {outputPath}");
    //     }
    // }

    public static string GetIconPath(uint iconId, bool isHr = false) {
        var iconGroup = iconId - (iconId % 1000);
        return $"ui/icon/{iconGroup:D6}/{iconId:D6}{(isHr ? "_hr1" : "")}.tex";
    }

    public static string GetXivApiIconAssetUrl(uint iconId, string format = "png") {
        return $"https://v2.xivapi.com/api/asset/{GetIconPath(iconId)}?format={format}";
    }
}
