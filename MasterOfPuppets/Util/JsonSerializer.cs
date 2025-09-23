using Newtonsoft.Json;

namespace MasterOfPuppets.Util;

public static class JsonSerializer
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
    {
        TypeNameHandling = TypeNameHandling.None,
        TypeNameAssemblyFormatHandling =
        TypeNameAssemblyFormatHandling.Simple
    };

    public static string JsonSerialize<T>(this T obj) where T : class => JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializerSettings);

    public static T JsonDeserialize<T>(this string str) where T : class => JsonConvert.DeserializeObject<T>(str);

    public static T JsonClone<T>(this T obj) where T : class => JsonDeserialize<T>(JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializerSettings));
}
