using System.Security.Cryptography;
using System.Text.Json;
namespace Dictor.Recognition;

internal static class ModelIntegrity
{
    public static string Verify(string directory, string file)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var entry = manifest.RootElement.EnumerateArray().Single(x => x.GetProperty("file").GetString() == file);
        var path = Path.Combine(directory, file);
        using var stream = File.OpenRead(path);
        if (stream.Length != entry.GetProperty("size").GetInt64() || !Convert.ToHexString(SHA256.HashData(stream)).Equals(entry.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("MODEL_HASH_MISMATCH");
        return path;
    }
}
