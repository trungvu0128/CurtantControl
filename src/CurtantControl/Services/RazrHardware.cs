using System;
using System.Device.I2c;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services;

public interface IRazrHardware : IDisposable
{
    Task MoveCameraAsync(double angle, CancellationToken token);
    Task MovePositionAsync(int position, double angle, CancellationToken token) => MoveCameraAsync(angle, token);
}

/// <summary>Lazy device access: constructing/saving configuration never writes GPIO or DAC.</summary>
public sealed class RazrHardware(RazrConfiguration configuration,
    Func<I2cConnectionSettings, I2cDevice>? deviceFactory = null) : IRazrHardware
{
    private readonly RazrConfiguration _config = configuration.Copy();
    private I2cDevice? _dac;
    private readonly Func<I2cConnectionSettings, I2cDevice> _deviceFactory = deviceFactory ?? (settings =>
        OperatingSystem.IsLinux() ? I2cDevice.Create(settings)
            : throw new PlatformNotSupportedException("I2C motor control requires Linux."));
    private bool _disposed;

    public async Task MoveCameraAsync(double angle, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_config.Model != RazrModel.RazrII) throw new InvalidOperationException("Camera motor belongs to Razr II only.");
        if (!double.IsFinite(_config.FullScaleAngle) || _config.FullScaleAngle <= 0 || _config.FullScaleAngle > 360
            || !double.IsFinite(angle) || angle < 0 || angle > _config.FullScaleAngle
            || angle < _config.MinimumAngle || angle > _config.MaximumAngle)
            throw new ArgumentOutOfRangeException(nameof(angle));
        token.ThrowIfCancellationRequested();
        if (_config.SimulateMotor) { await Task.Delay(150, token); return; }
        if (!_config.CalibrationConfirmed) throw new InvalidOperationException("Motor calibration is not confirmed.");
        _dac ??= _deviceFactory(new I2cConnectionSettings(_config.I2cBus, _config.DacAddress));
        var code = (int)Math.Round(angle / _config.FullScaleAngle * 4095);
        // MCP4725 fast write, normal operation; never writes EEPROM.
        _dac.Write(new byte[] { (byte)((code >> 8) & 0x0F), (byte)(code & 0xFF) });
        // Command accepted by DAC. The view model applies Wait time and persists
        // the button state; no ADC reads, target-angle checks or stability wait.
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { _dac?.Dispose(); }
        finally { _disposed = true; }
        // Releasing I2C does not stop the servo or reset its target voltage.
    }
}
