using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;

namespace CurtantControl.Services.Relays;

/// <summary>One BCM GPIO per channel. Owns its GPIO controller; create only on the Pi.</summary>
public sealed class GpioRelayOutput : IRelayOutput
{
    private readonly GpioController _gpio;
    private readonly int[] _pins;
    private readonly bool _activeLow;
    private bool _disposed;

    public GpioRelayOutput(IReadOnlyList<int> bcmPinsByChannel, bool activeLow)
    {
        ArgumentNullException.ThrowIfNull(bcmPinsByChannel);
        _pins = bcmPinsByChannel.ToArray();
        if (_pins.Length != 8 || _pins.Any(pin => pin is < 0 or > 27) || _pins.Distinct().Count() != 8)
            throw new ArgumentException("Supply eight distinct BCM GPIO numbers (0..27) in relay channel order.", nameof(bcmPinsByChannel));
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Use SimulatedRelayOutput for development outside Linux.");

        _activeLow = activeLow;
        _gpio = new GpioController();
        try
        {
            foreach (var pin in _pins)
                _gpio.OpenPin(pin, PinMode.Output, ToPinValue(false));
        }
        catch
        {
            _gpio.Dispose();
            throw;
        }
    }

    public void Write(int channel, bool energized)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gpio.Write(_pins[SimulatedRelayOutput.ValidateChannel(channel)], ToPinValue(energized));
    }

    private PinValue ToPinValue(bool energized) => energized != _activeLow ? PinValue.High : PinValue.Low;

    public bool? Read(int channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var level = _gpio.Read(_pins[SimulatedRelayOutput.ValidateChannel(channel)]);
        return (level == PinValue.High) != _activeLow;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        var errors = new List<Exception>();
        foreach (var pin in _pins)
        {
            try { _gpio.Write(pin, ToPinValue(false)); }
            catch (Exception error) { errors.Add(error); }
        }
        try { _gpio.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count > 0)
            throw new AggregateException("Could not shut down every GPIO output.", errors);
    }
}
