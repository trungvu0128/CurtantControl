using System;
using System.IO;
using System.Text.Json;

namespace CurtantControl.Services;

public interface IThemePreferenceStore
{
    bool LoadDarkMode();
    void SaveDarkMode(bool dark);
}

public sealed class ThemePreferenceStore(string? path = null) : IThemePreferenceStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "appearance.json");

    public bool LoadDarkMode()
    {
        try { return !File.Exists(_path) || JsonSerializer.Deserialize<Preference>(File.ReadAllText(_path))?.DarkMode != false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return true; }
    }

    public void SaveDarkMode(bool dark)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Preference(dark)));
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record Preference(bool DarkMode);
}
