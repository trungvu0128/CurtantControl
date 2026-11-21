using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Relays;

public enum CurtainDirection { Open, Close }
public sealed record RelayOutputStatus(int Channel, bool? Energized, string? Error = null);

/// <summary>
/// Four curtains: channels 1/2, 3/4, 5/6, 7/8 (open/close).
/// Owns the output. All access to that output must go through this controller.
/// Commands describe relay activation, not confirmed physical curtain position.
/// </summary>
public sealed class CurtainRelayController : IDisposable
{
    private readonly IRelayOutput _output;
    private readonly TimeSpan _maximumActivation;
    private readonly TimeSpan _breakBeforeMake;
    private readonly CancellationTokenSource?[] _running = new CancellationTokenSource?[4];
    private readonly object _sync = new();
    private bool _disposed;
    private bool _faulted;

    public CurtainRelayController(IRelayOutput output, TimeSpan maximumActivation, TimeSpan breakBeforeMake)
    {
        ArgumentNullException.ThrowIfNull(output);
        ValidateDelay(maximumActivation, nameof(maximumActivation));
        ValidateDelay(breakBeforeMake, nameof(breakBeforeMake));
        _output = output;
        _maximumActivation = maximumActivation;
        _breakBeforeMake = breakBeforeMake;
        try { AllOff(); }
        catch
        {
            try { _output.Dispose(); } catch { /* Preserve the initialization error. */ }
            throw;
        }
    }

    public Task OpenAsync(int curtain, TimeSpan activation, CancellationToken cancellationToken = default)
        => ExecuteAsync(curtain, CurtainDirection.Open, activation, cancellationToken);

    public Task CloseAsync(int curtain, TimeSpan activation, CancellationToken cancellationToken = default)
        => ExecuteAsync(curtain, CurtainDirection.Close, activation, cancellationToken);

    public async Task ExecuteAsync(int curtain, CurtainDirection direction, TimeSpan activation,
        CancellationToken cancellationToken = default)
    {
        var index = ValidateCurtain(curtain);
        if (direction is not (CurtainDirection.Open or CurtainDirection.Close))
            throw new ArgumentOutOfRangeException(nameof(direction));
        ValidateDelay(activation, nameof(activation));
        if (activation > _maximumActivation)
            throw new ArgumentOutOfRangeException(nameof(activation), "Activation exceeds the configured maximum.");

        CancellationTokenSource command;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_faulted)
                throw new InvalidOperationException("Relay output has faulted. Inspect hardware and recreate the controller.");
            cancellationToken.ThrowIfCancellationRequested();
            if (_running[index] is not null)
                throw new InvalidOperationException("This curtain already has a command in progress. Stop and await it before retrying.");
            command = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _running[index] = command;
        }

        try
        {
            lock (_sync)
            {
                command.Token.ThrowIfCancellationRequested();
                PairOff(index);
            }
            // Both contacts remain off before every command, including reversals.
            await Task.Delay(_breakBeforeMake, command.Token).ConfigureAwait(false);
            lock (_sync)
            {
                command.Token.ThrowIfCancellationRequested();
                if (_faulted)
                    throw new InvalidOperationException("Relay output has faulted.");
                Write(index * 2 + (direction == CurtainDirection.Open ? 1 : 2), true);
            }
            await Task.Delay(activation, command.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                try
                {
                    if (!_disposed)
                        PairOff(index);
                }
                finally
                {
                    _running[index] = null;
                    command.Dispose();
                }
            }
        }
    }

    /// <summary>Cancels this curtain's command and immediately attempts to turn both relays off.</summary>
    public void Stop(int curtain)
    {
        var index = ValidateCurtain(curtain);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _running[index]?.Cancel();
            PairOff(index);
        }
    }

    public void StopAll()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var command in _running)
                command?.Cancel();
            AllOff();
        }
    }

    /// <summary>Reads all control outputs without changing them. Physical contact state is not measured.</summary>
    public IReadOnlyList<RelayOutputStatus> ReadOutputs()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var outputs = new List<RelayOutputStatus>(8);
            for (var channel = 1; channel <= 8; channel++)
            {
                try { outputs.Add(new(channel, _output.Read(channel))); }
                catch (Exception error) { outputs.Add(new(channel, null, error.Message)); }
            }
            return outputs;
        }
    }

    private void Write(int channel, bool energized)
    {
        try { _output.Write(channel, energized); }
        catch
        {
            _faulted = true;
            foreach (var command in _running)
                command?.Cancel();
            throw;
        }
    }

    private void PairOff(int index) => TurnOff(new[] { index * 2 + 1, index * 2 + 2 });

    private void AllOff() => TurnOff(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });

    private void TurnOff(IEnumerable<int> channels)
    {
        var errors = new List<Exception>();
        foreach (var channel in channels)
        {
            try { Write(channel, false); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0)
            throw new AggregateException("One or more relays could not be turned off; physical state is unknown.", errors);
    }

    private static int ValidateCurtain(int curtain)
    {
        if (curtain is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(curtain), "Curtain must be 1 through 4.");
        return curtain - 1;
    }

    private static void ValidateDelay(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(name, "Duration must be positive and supported by Task.Delay.");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var command in _running)
                command?.Cancel();
            var errors = new List<Exception>();
            try { AllOff(); }
            catch (Exception error) { errors.Add(error); }
            try { _output.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0)
                throw new AggregateException("Relay controller shutdown failed.", errors);
        }
    }
}
