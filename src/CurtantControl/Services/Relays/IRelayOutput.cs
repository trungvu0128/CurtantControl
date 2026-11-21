using System;

namespace CurtantControl.Services.Relays;

/// <summary>Logical channels 1..8; true means energized, independent of electrical polarity.</summary>
public interface IRelayOutput : IDisposable
{
    void Write(int channel, bool energized);
    /// <summary>Reads the control signal, not the physical relay contacts. Null means unsupported.</summary>
    bool? Read(int channel) => null;
}
