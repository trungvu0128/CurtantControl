using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CurtantControl.Services;

public enum RazrModel { RazrI, RazrII, RazrIII }
public enum RazrControlMode { Motor, Relays, MotorAndRelays }

public sealed record CameraPositionSnapshot(int Id, double Angle, double FullScaleAngle, bool Completed,
    string? ExecutionProfile = null);

public sealed class RazrConfiguration
{
    public RazrModel Model { get; set; }
    public int? SelectedBackgroundId { get; set; }
    public int? SelectedSimulatedBackgroundId { get; set; }
    public RazrControlMode ControlMode { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UsesMotor => ControlMode != RazrControlMode.Relays;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UsesRelays => ControlMode != RazrControlMode.Motor;
    public int? SelectedCameraPositionId { get; set; }
    public int? SelectedCameraButtonId { get; set; }
    public DateTimeOffset SelectedCameraButtonSavedAt { get; set; }
    public int? SelectedSimulatedCameraPositionId { get; set; }
    public CameraPositionSnapshot? LastCameraPosition { get; set; }
    public CameraPositionSnapshot? LastSimulatedCameraPosition { get; set; }
    public string Position1Name { get; set; } = "Black Hole";
    public string Position2Name { get; set; } = "Miller";
    public double Position1Angle { get; set; } = 45;
    public double Position2Angle { get; set; } = 135;
    public double SettleSeconds { get; set; } = 3;
    public bool SimulateMotor { get; set; } = true;
    public bool CalibrationConfirmed { get; set; }
    public double MinimumAngle { get; set; } = 0;
    public double MaximumAngle { get; set; } = 180;
    public double FullScaleAngle { get; set; } = 360;
    public double DacReferenceVolts { get; set; } = 5;
    public double FeedbackMinVolts { get; set; } = 0.05;
    public double FeedbackMaxVolts { get; set; } = 3.16;
    public double AngleTolerance { get; set; } = 2;
    public double MoveTimeoutSeconds { get; set; } = 15;
    public int I2cBus { get; set; } = 1;
    public int DacAddress { get; set; } = 0x60;
    public int AdcAddress { get; set; } = 0x48;

    public RazrConfiguration Copy() => (RazrConfiguration)MemberwiseClone();

    public void Validate(PhotoboothSettings installation)
    {
        static bool Between(double v, double min, double max) => double.IsFinite(v) && v >= min && v <= max;
        if (!Enum.IsDefined(Model)) throw new InvalidDataException("Choose a Razr model.");
        if (Model != RazrModel.RazrII) return;
        if (!Enum.IsDefined(ControlMode)) throw new InvalidDataException("Choose Motor, Relay 1/2 or Motor + Relay.");
        if (UsesRelays)
        {
            if (installation.ActivationMilliseconds <= 0 || installation.ActivationMilliseconds > installation.MaximumActivationMilliseconds
                || installation.BreakBeforeMakeMilliseconds <= 0)
                throw new InvalidDataException("Check relay pulse duration and off interval in photobooth.json.");
            if (!installation.SimulateRelays && (installation.ActiveLow is null || installation.BcmPinsByChannel.Length != 8
                || installation.BcmPinsByChannel.Any(p => p is < 0 or > 27 || p is 2 or 3)
                || installation.BcmPinsByChannel.Distinct().Count() != 8))
                throw new InvalidDataException("Set relay polarity and eight distinct BCM pins, excluding I2C GPIO2/3.");
        }
        if (!Between(SettleSeconds, 0, 30)) throw new InvalidDataException("Settling time must be 0–30 seconds.");
        if (string.IsNullOrWhiteSpace(Position1Name) || string.IsNullOrWhiteSpace(Position2Name)
            || Position1Name.Length > 40 || Position2Name.Length > 40 || Position1Name.Trim() == Position2Name.Trim())
            throw new InvalidDataException("Give the two camera positions different names (1–40 characters).");
        if (UsesMotor) ValidateMotor();
    }

    internal void ValidateMotor()
    {
        static bool Between(double v, double min, double max) => double.IsFinite(v) && v >= min && v <= max;
        if (!Between(FullScaleAngle, 1, 360) || !Between(MinimumAngle, 0, FullScaleAngle)
            || !Between(MaximumAngle, MinimumAngle + 0.1, FullScaleAngle)
            || !Between(Position1Angle, MinimumAngle, MaximumAngle) || !Between(Position2Angle, MinimumAngle, MaximumAngle))
            throw new InvalidDataException("Both camera positions must be within the configured motor angle limits.");
        if (!Between(DacReferenceVolts, 0.1, 5.5) || I2cBus is < 0 or > 10 || DacAddress is < 0x60 or > 0x67)
            throw new InvalidDataException("Check motor voltage, I2C bus and DAC address.");
        if (!SimulateMotor && !CalibrationConfirmed)
            throw new InvalidDataException("Confirm measured motor calibration before enabling real motor control.");
    }
}

public interface IRazrConfigurationStore
{
    RazrConfiguration Load();
    void Save(RazrConfiguration configuration);
}

public sealed class RazrConfigurationStore(string? path = null) : IRazrConfigurationStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "razr.json");
    public RazrConfiguration Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<RazrConfiguration>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("Razr configuration is empty.") : new();
    public void Save(RazrConfiguration configuration)
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
