using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public static class MacroManager
{
    private static string CompressString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string DecompressString(string s)
    {
        var data = Convert.FromBase64String(s);
        using var ms = new MemoryStream(data);
        using var gs = new GZipStream(ms, CompressionMode.Decompress);
        using var r = new StreamReader(gs);
        return r.ReadToEnd();
    }

    // public static string SerializeObject(object o, bool saveAllValues) => !saveAllValues
    //  ? JsonConvert.SerializeObject(o, new JsonSerializerSettings
    //  {
    //      TypeNameHandling = TypeNameHandling.Objects,
    //      NullValueHandling = NullValueHandling.Ignore,
    //      DefaultValueHandling = DefaultValueHandling.Ignore,
    //  })
    //  : JsonConvert.SerializeObject(o, new JsonSerializerSettings
    //  {
    //      TypeNameHandling = TypeNameHandling.Objects
    //  });

    // public static T DeserializeObject<T>(string o) => JsonConvert.DeserializeObject<T>(o, new JsonSerializerSettings
    // {
    //     TypeNameHandling = TypeNameHandling.Objects,
    // });

    // public static string ExportObject(object o, bool saveAllValues) => CompressString(SerializeObject(o, saveAllValues));

    // public static T ImportObject<T>(string import) => DeserializeObject<T>(DecompressString(import));

    public static string MacroToExportString(Macro macro)
    {
        string macroJson = macro.JsonSerialize();
        return CompressString(macroJson);
    }

    public static Macro MacroExportStringToMacro(string compressedMacroExportString)
    {
        string macroExportString = DecompressString(compressedMacroExportString);
        var newMacro = macroExportString.JsonDeserialize<Macro>();
        return newMacro;
    }
}
