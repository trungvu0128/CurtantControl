using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CurtantControl.Services;

public sealed record Razr3Configuration
{
    public const int OutsideScene = 5;
    public double? OutsideAngle { get; set; }
    public double? RoomAngle { get; set; }
    public double SettleSeconds { get; set; } = 3;
    public double MinimumAngle { get; set; }
    public double MaximumAngle { get; set; } = 180;
    public double FullScaleAngle { get; set; } = 360;
    public double DacReferenceVolts { get; set; } = 5;
    public int I2cBus { get; set; } = 1;
    public int DacAddress { get; set; } = 0x60;
    public bool Simulate { get; set; } = true;
    public bool CalibrationConfirmed { get; set; }
    public string FixedColor { get; set; } = "white";
    public string M1Color { get; set; } = "blue";
    public string M2Color { get; set; } = "green";
    public string M3Color { get; set; } = "purple";
    public string M4Color { get; set; } = "gray";
    public int? SelectedSceneId { get; set; }

    public string[] GetColorKeys() => [FixedColor, M1Color, M2Color, M3Color, M4Color];

    public RazrConfiguration MotorSettings() => new()
    {
        Model = RazrModel.RazrIII,
        Position1Angle = OutsideAngle ?? 45,
        Position2Angle = RoomAngle ?? 135,
        SettleSeconds = SettleSeconds,
        MinimumAngle = MinimumAngle,
        MaximumAngle = MaximumAngle,
        FullScaleAngle = FullScaleAngle,
        DacReferenceVolts = DacReferenceVolts,
        I2cBus = I2cBus,
        DacAddress = DacAddress,
        SimulateMotor = Simulate,
        CalibrationConfirmed = CalibrationConfirmed
    };

    public void Validate(PhotoboothSettings installation, CurtainColorPreset[] colors)
    {
        var keys = GetColorKeys();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length ||
            keys.Any(key => !colors.Any(color => color.Key == key)))
            throw new InvalidDataException("Choose five different colors from the palette.");
        if (SelectedSceneId is < 0 or > OutsideScene)
            throw new InvalidDataException("Invalid RAZR 3 scene selection.");
        if (!double.IsFinite(SettleSeconds) || SettleSeconds is < 0 or > 30)
            throw new InvalidDataException("Set the wait time between 0 and 30 seconds.");
        if (!Simulate)
        {
            if (OutsideAngle is null || RoomAngle is null || SettleSeconds <= 0)
                throw new InvalidDataException("Enter both measured angles and a positive wait time for hardware mode.");
            if (installation.SimulateRelays || installation.ActiveLow is null ||
                installation.BcmPinsByChannel is not { Length: 8 } pins ||
                pins.Distinct().Count() != 8 || pins.Any(pin => pin is < 0 or > 27 or 2 or 3))
                throw new InvalidDataException("Enable the installed GPIO relays with explicit polarity and eight distinct BCM pins, excluding GPIO2/3.");
        }
        if (installation.ActivationMilliseconds <= 0 ||
            installation.ActivationMilliseconds > installation.MaximumActivationMilliseconds ||
            installation.BreakBeforeMakeMilliseconds <= 0)
            throw new InvalidDataException("Check relay timing in photobooth.json.");
        var motor = MotorSettings();
        motor.ValidateMotor();
        if (motor.Position1Angle == motor.Position2Angle)
            throw new InvalidDataException("Outside and Room 1 must use different angles.");
    }
}

public interface IRazr3ConfigurationStore
{
    Razr3Configuration Load();
    void Save(Razr3Configuration configuration);
}

public sealed class Razr3ConfigurationStore(string? path = null) : IRazr3ConfigurationStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "razr3.json");

    public Razr3Configuration Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<Razr3Configuration>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("RAZR 3 configuration is empty.") : new();

    public void Save(Razr3Configuration configuration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(file, configuration, new JsonSerializerOptions { WriteIndented = true });
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
