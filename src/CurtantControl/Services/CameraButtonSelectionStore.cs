using System;
using System.IO;
using System.Text.Json;

namespace CurtantControl.Services;

public sealed record CameraButtonSelection(int Id, DateTimeOffset SavedAtUtc);

public interface ICameraButtonSelectionStore
{
    CameraButtonSelection? Load();
    void Save(CameraButtonSelection selection);
}

/// <summary>UI choice only. Independent of motor configuration, simulation and command completion.</summary>
public sealed class CameraButtonSelectionStore(string? path = null) : ICameraButtonSelectionStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "camera-button-selection.json");

    public CameraButtonSelection? Load()
    {
        if (!File.Exists(_path)) return null;
        var saved = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(_path));
        if (saved is null || saved.Version != 1 || saved.SelectedId is not (101 or 102))
            throw new InvalidDataException("Invalid saved camera button selection.");
        return new(saved.SelectedId, saved.SavedAtUtc);
    }

    public void Save(int id) => Save(new CameraButtonSelection(id, DateTimeOffset.UtcNow));

    public void Save(CameraButtonSelection selection)
    {
        var id = selection.Id;
        if (id is not (101 or 102)) throw new ArgumentOutOfRangeException(nameof(id));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(file, new Snapshot(1, id, selection.SavedAtUtc));
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Snapshot(int Version, int SelectedId, DateTimeOffset SavedAtUtc);
}
