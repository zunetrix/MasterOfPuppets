using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MasterOfPuppets.Util;

public static class Compressor {
    public static string CompressString(string input) {
        var bytes = Encoding.UTF8.GetBytes(input);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string DecompressString(string input) {
        var data = Convert.FromBase64String(input);
        using var ms = new MemoryStream(data);
        using var gs = new GZipStream(ms, CompressionMode.Decompress);
        using var r = new StreamReader(gs);
        return r.ReadToEnd();
    }
}
