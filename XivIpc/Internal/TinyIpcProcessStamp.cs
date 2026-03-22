using System.Reflection;
using System.Security.Cryptography;

namespace XivIpc.Internal;

internal sealed record TinyIpcProcessStamp(
    string AssemblyPath,
    string AssemblyName,
    string InformationalVersion,
    string FileVersion,
    string Sha256,
    string ProcessPath,
    string ProcessSha256) {
    internal static TinyIpcProcessStamp Create(Type anchorType) {
        Assembly assembly = anchorType.Assembly;
        string assemblyPath = assembly.Location;
        AssemblyName assemblyName = assembly.GetName();
        string informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assemblyName.Version?.ToString() ?? string.Empty;
        string fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? string.Empty;
        string sha256 = ComputeSha256(assemblyPath);
        string processPath = Environment.ProcessPath ?? string.Empty;
        string processSha256 = ComputeSha256(processPath);

        return new TinyIpcProcessStamp(
            assemblyPath,
            assemblyName.Name ?? string.Empty,
            informationalVersion,
            fileVersion,
            sha256,
            processPath,
            processSha256);
    }

    internal static string ComputeSha256(string assemblyPath) {
        try {
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
                return string.Empty;

            using FileStream stream = File.OpenRead(assemblyPath);
            return Convert.ToHexString(SHA256.HashData(stream));
        } catch {
            return string.Empty;
        }
    }
}
