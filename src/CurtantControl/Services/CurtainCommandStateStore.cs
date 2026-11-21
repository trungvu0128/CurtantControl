using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CurtantControl.Services;

public interface ICurtainCommandStateStore
{
    string[]? Load();
    void Save(string[] states);
}

/// <summary>Last commanded estimates, indexed by physical curtain ID; never sensor feedback.</summary>
public sealed class CurtainCommandStateStore(bool simulated = false, string? path = null) : ICurtainCommandStateStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CurtantControl",
        simulated ? "simulated-curtain-command-state.json" : "curtain-command-state.json");
    public string[]? Load()
    {
        if (!File.Exists(_path)) return null;
        var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("Saved curtain state is empty.");
        if (snapshot.Version != 1) throw new InvalidDataException("Unsupported saved curtain state version.");
        Validate(snapshot.States);
        return snapshot.States;
    }
    public void Save(string[] states)
    {
        Validate(states);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(file, new Snapshot(1, DateTimeOffset.UtcNow, (string[])states.Clone()));
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(string[]? states)
    {
        if (states is null || states.Length != 4 || states.Any(s => s is not ("Opened" or "Closed" or "Unknown")))
            throw new InvalidDataException("Saved curtain state must contain four Opened/Closed/Unknown values.");
    }
    private sealed record Snapshot(int Version, DateTimeOffset SavedAtUtc, string[] States);
}
