using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CurtantControl.Services;

public sealed record CameraNameColor(string Name, string Color);

public static class CameraPositionNameCatalog
{
    public static CameraNameColor[] LoadOptions(string? path = null)
    {
        var optionsPath = path ?? Path.Combine(AppContext.BaseDirectory, "razr2-options.json");
        if (!File.Exists(optionsPath) && path is null)
        {
            var names = Load();
            var legacyColorsPath = Path.Combine(AppContext.BaseDirectory, "razr2-colors.json");
            var colors = File.Exists(legacyColorsPath) ? LoadColorKeys(legacyColorsPath) : new Dictionary<string, string>();
            return names.Select(n => new CameraNameColor(n, colors.TryGetValue(n, out var c) ? c : n)).ToArray();
        }
        var values = JsonSerializer.Deserialize<CameraNameColor[]>(File.ReadAllText(optionsPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Razr II options are empty.");
        if (values.Length is < 2 or > 100 || values.Any(v => v is null || string.IsNullOrWhiteSpace(v.Name)
            || v.Name.Trim().Length > 40 || string.IsNullOrWhiteSpace(v.Color) || v.Color.Length > 64))
            throw new InvalidDataException("Supply 2–100 name/color entries, names of 1–40 characters.");
        var normalized = values.Select(v => new CameraNameColor(v.Name.Trim(), v.Color.Trim())).ToArray();
        if (normalized.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
            throw new InvalidDataException("Razr II option names must be unique.");
        return normalized;
    }

    public static Dictionary<string, string> LoadColorKeys(string? path = null)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
            path ?? Path.Combine(AppContext.BaseDirectory, "razr2-colors.json")))
            ?? throw new InvalidDataException("Camera color map is empty.");
        if (values.Count > 100 || values.Any(v => string.IsNullOrWhiteSpace(v.Key) || string.IsNullOrWhiteSpace(v.Value)))
            throw new InvalidDataException("Camera color map requires nonempty names and palette keys.");
        return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public static string[] Load(string? path = null)
    {
        var names = JsonSerializer.Deserialize<string?[]>(File.ReadAllText(
            path ?? Path.Combine(AppContext.BaseDirectory, "razr2-names.json")))
            ?? throw new InvalidDataException("Camera name list is empty.");
        if (names.Length > 100 || names.Any(n => string.IsNullOrWhiteSpace(n) || n.Trim().Length > 40))
            throw new InvalidDataException("Camera names must contain 1–40 characters (maximum 100 entries).");
        var unique = names.Select(n => n!.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (unique.Length < 2) throw new InvalidDataException("Supply at least two different camera names.");
        return unique;
    }
}
