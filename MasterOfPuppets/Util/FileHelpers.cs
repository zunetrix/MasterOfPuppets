using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace MasterOfPuppets.Util;

public class FileHelpers
{
    public static void Save(object obj, string fileName)
    {
        var dirName = Path.GetDirectoryName(fileName);

        if (!Directory.Exists(dirName))
            Directory.CreateDirectory(dirName);

        var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
        WriteAllText(fileName, json);
    }

    private static void WriteAllText(string path, string text)
    {
        var exists = File.Exists(path);
        using var fs =
            File.Open(path, exists ? FileMode.Truncate : FileMode.CreateNew,
            FileAccess.Write, FileShare.ReadWrite);
        using var sw = new StreamWriter(fs, Encoding.UTF8);
        sw.Write(text);
    }

    public static T Load<T>(string filePath)
    {
        if (!File.Exists(filePath))
            return default(T);

        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<T>(json);
    }
}

