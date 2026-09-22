using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MasterOfPuppets.RemoteControl;

/// <summary>
/// Serves the embedded web UI assets from EmbeddedResource entries.
/// </summary>
internal static class RemoteControlWebAssets {
    private static readonly Dictionary<string, (byte[] Data, string ContentType)> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> PathToResource = new(StringComparer.OrdinalIgnoreCase) {
        ["/"]           = "MasterOfPuppets.RemoteControl.Web.index.html",
        ["/index.html"] = "MasterOfPuppets.RemoteControl.Web.index.html",
        ["/app.js"]     = "MasterOfPuppets.RemoteControl.Web.app.js",
        ["/styles.css"] = "MasterOfPuppets.RemoteControl.Web.styles.css",
    };

    private static readonly Dictionary<string, string> ResourceToContentType = new(StringComparer.OrdinalIgnoreCase) {
        [".html"] = "text/html; charset=utf-8",
        [".js"]   = "application/javascript; charset=utf-8",
        [".css"]  = "text/css; charset=utf-8",
    };

    public static bool TryGet(string urlPath, out byte[] data, out string contentType) {
        if (_cache.TryGetValue(urlPath, out var cached)) {
            data = cached.Data;
            contentType = cached.ContentType;
            return true;
        }

        if (!PathToResource.TryGetValue(urlPath, out var resourceName)) {
            data = Array.Empty<byte>();
            contentType = string.Empty;
            return false;
        }

        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null) {
            DalamudApi.PluginLog.Warning($"[RemoteControl] Embedded resource not found: {resourceName}");
            data = Array.Empty<byte>();
            contentType = string.Empty;
            return false;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        data = ms.ToArray();

        var ext = Path.GetExtension(resourceName);
        contentType = ResourceToContentType.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";

        _cache[urlPath] = (data, contentType);
        return true;
    }
}
