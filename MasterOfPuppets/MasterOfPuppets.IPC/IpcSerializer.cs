using System;
using System.IO;
using System.IO.Compression;

using Newtonsoft.Json;

namespace MasterOfPuppets.Ipc;

static class IpcSerializer
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
    {
        TypeNameHandling = TypeNameHandling.None,
        TypeNameAssemblyFormatHandling =
        TypeNameAssemblyFormatHandling.Simple
    };

    public static byte[] Compress(this byte[] bytes)
    {
        using MemoryStream memoryStream1 = new MemoryStream(bytes);
        using MemoryStream memoryStream2 = new MemoryStream();
        using (GZipStream destination = new GZipStream(memoryStream2, CompressionLevel.Fastest))
            memoryStream1.CopyTo((Stream)destination);
        var compress = memoryStream2.ToArray();

        // DalamudApi.PluginLog.Verbose($"original: {Dalamud.Utility.Util.FormatBytes(bytes.Length)}, compressed: {Dalamud.Utility.Util.FormatBytes(compress.Length)}, ratio: {(double)compress.Length / bytes.Length:P}");
        return compress;
    }

    public static byte[] Decompress(this byte[] bytes)
    {
        using MemoryStream memoryStream = new MemoryStream(bytes);
        using MemoryStream destination = new MemoryStream();
        using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            gzipStream.CopyTo((Stream)destination);
        return destination.ToArray();
    }

    public static unsafe byte[] ToBytesUnmanaged<T>(this T stru) where T : unmanaged
    {
        var size = sizeof(T);
        var b = (byte*)&stru;
        var bytes = new byte[size];
        fixed (byte* f = bytes)
        {
            for (int i = 0; i < size; i++)
            {
                f[i] = b[i];
            }
        }

        return bytes;
    }

    public static unsafe T ToStructUnmanaged<T>(this byte[] bytes) where T : unmanaged
    {
        var foo = *bytes.AsPtr<T>();
        return foo;
    }

    public static unsafe T* AsPtr<T>(this byte[] bytes, int offset = 0) where T : unmanaged
    {
        if (bytes == null) return null;
        fixed (byte* f = bytes)
        {
            return (T*)(f + offset);
        }
    }

    public static byte[] ProtoSerialize<T>(this T obj)
    {
        using var memoryStream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(memoryStream, obj);
        return memoryStream.ToArray();
    }

    public static T ProtoDeserialize<T>(this byte[] bytes) => ProtoBuf.Serializer.Deserialize<T>((ReadOnlySpan<byte>)bytes);

    public static T ProtoDeepClone<T>(this T obj) => ProtoBuf.Serializer.DeepClone(obj);

    public static string JsonSerialize<T>(this T obj) where T : class => JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializerSettings);

    public static T JsonDeserialize<T>(this string str) where T : class => JsonConvert.DeserializeObject<T>(str);

    public static T JsonClone<T>(this T obj) where T : class => JsonDeserialize<T>(JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializerSettings));
}
