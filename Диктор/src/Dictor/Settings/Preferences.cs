using System.Text.Json;

namespace Dictor.Settings;

internal sealed record Preferences
{
    public int Version { get; init; } = 1;
    public string? Microphone { get; init; }
    public uint Modifiers { get; init; } = 3;
    public uint Key { get; init; } = 0x20;
    public bool Paste { get; init; } = true;
    public bool Cpu { get; init; }
    public string Theme { get; init; } = "System";
    public double Scale { get; init; } = 1;
    public int Monitor { get; init; }
    public string Position { get; init; } = "Bottom";

    public static Preferences Load(string path, out bool recovered)
    {
        recovered = false;
        if (!File.Exists(path)) return new();
        try { return Read(path); }
        catch
        {
            recovered = true;
            try { return Read(path + ".bak"); } catch { return new(); }
        }
    }
    private static Preferences Read(string path)
    {
        var p = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        if (p.Version != 1 || p.Scale < .75 || p.Scale > 2 || p.Key == 0 || p.Key > 255 || p.Modifiers > 15) throw new InvalidDataException();
        return p;
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
}
