using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CurtantControl.Services.Relays;

namespace CurtantControl.Services;

/// <summary>Owns the selected Razr II outputs. Construction and settings saves perform no hardware access.</summary>
public sealed class RazrPositionHardware : IRazrHardware
{
    private readonly RazrConfiguration _configuration;
    private readonly Func<IRazrHardware> _motorFactory;
    private readonly Func<CurtainRelayController> _relayFactory;
    private readonly TimeSpan _pulse;
    private IRazrHardware? _motor;
    private CurtainRelayController? _relays;
    private bool _disposed;

    public RazrPositionHardware(RazrConfiguration configuration, PhotoboothSettings installation,
        Func<IRazrHardware>? motorFactory = null, Func<CurtainRelayController>? relayFactory = null)
    {
        _configuration = configuration.Copy();
        _motorFactory = motorFactory ?? (() => new RazrHardware(_configuration));
        _relayFactory = relayFactory ?? installation.CreateRelays;
        _pulse = TimeSpan.FromMilliseconds(installation.ActivationMilliseconds);
    }

    public Task MoveCameraAsync(double angle, CancellationToken token)
        => throw new InvalidOperationException("Select position 1 or 2 before sending a Razr II command.");

    public async Task MovePositionAsync(int position, double angle, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_configuration.Model != RazrModel.RazrII || position is < 1 or > 2)
            throw new InvalidOperationException("Choose Razr II position 1 or 2.");
        token.ThrowIfCancellationRequested();
        // Initialize all requested resources before starting either command.
        if (_configuration.UsesRelays) _relays ??= _relayFactory();
        if (_configuration.UsesMotor) _motor ??= _motorFactory();
        using var command = CancellationTokenSource.CreateLinkedTokenSource(token);
        async Task Run(Func<Task> action)
        {
            try { await action(); }
            catch { command.Cancel(); throw; }
        }
        var tasks = new List<Task>();
        if (_configuration.UsesMotor)
            tasks.Add(Run(() => _motor!.MoveCameraAsync(angle, command.Token)));
        if (_configuration.UsesRelays)
            tasks.Add(Run(() => _relays!.ExecuteAsync(1,
                position == 1 ? CurtainDirection.Open : CurtainDirection.Close, _pulse, command.Token)));
        // Await both finalizers even when one fails; the relay controller always attempts pair-off.
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var errors = new List<Exception>();
        foreach (var output in new IDisposable?[] { _motor, _relays })
            try { output?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count > 0) throw new AggregateException("Could not release every Razr II output.", errors);
    }
}
