using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CurtantControl.Services;

public sealed record CurtainColorPreset(string Key, string Name, string Color, string? PreviewImage = null);
public sealed class CurtainPalette
{
    public CurtainColorPreset[] Colors { get; set; } =
    [new("pink", "Pink", "#D989AE"), new("blue", "Sky Blue", "#60BCD5"),
     new("green", "Green", "#42AD6A"), new("purple", "Purple", "#9580D5")];
    public int[]? RelayOrderFrontToBack { get; set; }
    public bool ReverseRelayAssignments { get; set; }
    public string FixedBackground { get; set; } = "white";
    public CurtainColorPreset GetFixedBackground() => Colors.FirstOrDefault(c => c.Key == FixedBackground)
        ?? new CurtainColorPreset("white", "White", "#FFFFFF", "Backgrounds/white.png");
    public string[] Slots { get; set; } = ["pink", "blue", "green", "purple"];

    // Slot 0 is the fixed background. For moving curtains the same mapping works in both directions.
    internal static int MapMotor(int id, bool reversed) => reversed && id != 0 ? 5 - id : id;

    public void Validate()
    {
        if (RelayOrderFrontToBack is not null) CurtainOrderStore.Validate(RelayOrderFrontToBack);
        if (Colors is null || Colors.Length < 1 || Colors.Any(c => c is null ||
            string.IsNullOrWhiteSpace(c.Key) || string.IsNullOrWhiteSpace(c.Name) ||
            c.Color is null || !Regex.IsMatch(c.Color, "^#[0-9a-fA-F]{6}$")) ||
            Colors.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != Colors.Length)
            throw new InvalidDataException("colors.json: use unique color keys, nonempty names and #RRGGBB colors.");
        if (string.IsNullOrWhiteSpace(FixedBackground) || (FixedBackground != "white" && !Colors.Any(c => c.Key == FixedBackground)))
            throw new InvalidDataException("colors.json: FixedBackground must be a palette key (default white).");
        if (Slots is null || Slots.Length != 4 || Slots.Distinct(StringComparer.Ordinal).Count() != 4 || Slots.Any(key => !Colors.Any(c => c.Key == key)))
            throw new InvalidDataException("colors.json: Slots must contain four different keys from Colors, in curtain ID 1..4 order.");
    }
}

public interface ICurtainPaletteStore
{
    CurtainPalette Load();
    void Save(CurtainPalette palette);
}

public sealed class CurtainPaletteStore(string? path = null) : ICurtainPaletteStore
{
    private readonly string _path = path ?? Path.Combine(AppContext.BaseDirectory, "colors.json");
    public CurtainPalette Load()
    {
        var palette = File.Exists(_path)
            ? JsonSerializer.Deserialize<CurtainPalette>(File.ReadAllText(_path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("colors.json is empty.")
            : new CurtainPalette();
        palette.Validate();
        var defaultsPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_path))!, "colors.defaults.json");
        if (File.Exists(defaultsPath))
        {
            var defaults = JsonSerializer.Deserialize<CurtainPalette>(File.ReadAllText(defaultsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("colors.defaults.json is empty.");
            defaults.Validate();
            var merged = palette.Colors.ToList();
            foreach (var preset in defaults.Colors)
            {
                var index = merged.FindIndex(c => c.Key == preset.Key);
                if (index < 0) merged.Add(preset);
                else if (string.IsNullOrWhiteSpace(merged[index].PreviewImage))
                    merged[index] = merged[index] with { PreviewImage = preset.PreviewImage };
            }
            palette.Colors = merged.ToArray();
        }
        return palette;
    }

    public void Save(CurtainPalette palette)
    {
        palette.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(palette, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
