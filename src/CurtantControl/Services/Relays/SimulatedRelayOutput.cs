using System;

namespace CurtantControl.Services.Relays;

/// <summary>In-memory output for development; never accesses GPIO.</summary>
public sealed class SimulatedRelayOutput : IRelayOutput
{
    private readonly bool[] _states = new bool[8];
    private readonly object _sync = new();
    private bool _disposed;

    public bool IsEnergized(int channel)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _states[ValidateChannel(channel)];
        }
    }

    public bool? Read(int channel) => IsEnergized(channel);

    public void Write(int channel, bool energized)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _states[ValidateChannel(channel)] = energized;
        }
    }

    internal static int ValidateChannel(int channel)
    {
        if (channel is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Relay channel must be 1 through 8.");
        return channel - 1;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            Array.Clear(_states);
            _disposed = true;
        }
    }
}
