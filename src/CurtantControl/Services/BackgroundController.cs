using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CurtantControl.Services.Relays;

namespace CurtantControl.Services;

public enum CurtainCommandState { Opening, Closing, Opened, Closed, Unknown }
public sealed record CurtainCommandUpdate(int Curtain, CurtainCommandState State);

/// <summary>Closes layers in front of the selected curtain before opening it.</summary>
public sealed class BackgroundController
{
    private readonly CurtainRelayController _relays;
    private readonly int[] _frontToBack;
    private readonly TimeSpan _activation;
    private readonly Func<int, CurtainCommandState>? _readState;

    public BackgroundController(CurtainRelayController relays, int[] frontToBack, TimeSpan activation,
        Func<int, CurtainCommandState>? readState = null)
    {
        ArgumentNullException.ThrowIfNull(frontToBack);
        if (frontToBack.Length != 4 || !frontToBack.Order().SequenceEqual(new[] { 1, 2, 3, 4 }))
            throw new ArgumentException("Curtain order must contain curtain IDs 1, 2, 3, 4 exactly once.", nameof(frontToBack));
        _relays = relays;
        _frontToBack = (int[])frontToBack.Clone();
        _activation = activation;
        _readState = readState;
    }

    public async Task SelectAsync(int curtain, CancellationToken cancellationToken = default,
        Action<CurtainCommandUpdate>? report = null)
    {
        var index = Array.IndexOf(_frontToBack, curtain);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(curtain));
        await Task.WhenAll(_frontToBack.Take(index)
            .Select(id => RunAsync(id, false, cancellationToken, report)));
        cancellationToken.ThrowIfCancellationRequested();
        await RunAsync(curtain, true, cancellationToken, report);
    }

    public Task CloseAllAsync(CancellationToken cancellationToken = default,
        Action<CurtainCommandUpdate>? report = null)
        => Task.WhenAll(_frontToBack.Select(id => RunAsync(id, false, cancellationToken, report)));

    public Task ApplyAsync(int curtain, bool usePaperSequence, CancellationToken cancellationToken,
        Action<CurtainCommandUpdate> report, Action<int> reportRelay)
    {
        if (usePaperSequence) return ApplyPaperSequenceAsync(curtain, cancellationToken, report, reportRelay);
        return curtain == 0 ? CloseAllAsync(cancellationToken, report) : SelectAsync(curtain, cancellationToken, report);
    }

    private async Task ApplyPaperSequenceAsync(int curtain, CancellationToken cancellationToken,
        Action<CurtainCommandUpdate> report, Action<int> reportRelay)
    {
        var index = Array.IndexOf(_frontToBack, curtain);
        if (curtain != 0 && index < 0)
            throw new ArgumentOutOfRangeException(nameof(curtain));
        // White closes all motors in relay-number order. Color scenes close
        // foreground layers in camera order before opening their target.
        var sequence = curtain == 0
            ? _frontToBack.Order().Select(id => id * 2)
            : _frontToBack.Take(index).Select(id => id * 2).Append(curtain * 2 - 1);
        foreach (var relay in sequence)
        {
            var motor = (relay + 1) / 2;
            var open = relay % 2 == 1;
            var completed = open ? CurtainCommandState.Opened : CurtainCommandState.Closed;
            await PulseAsync(motor, open, completed, cancellationToken, update =>
            {
                report(update);
                if (update.State is CurtainCommandState.Opening or CurtainCommandState.Closing) reportRelay(relay);
            });
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task RunAsync(int curtain, bool open, CancellationToken cancellationToken,
        Action<CurtainCommandUpdate>? report)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var desired = open ? CurtainCommandState.Opened : CurtainCommandState.Closed;
        if (_readState?.Invoke(curtain) == desired) return;
        await PulseAsync(curtain, open, desired, cancellationToken, report);
    }

    private async Task PulseAsync(int curtain, bool open, CurtainCommandState completed,
        CancellationToken cancellationToken, Action<CurtainCommandUpdate>? report)
    {
        cancellationToken.ThrowIfCancellationRequested();
        report?.Invoke(new(curtain, open ? CurtainCommandState.Opening : CurtainCommandState.Closing));
        try
        {
            await _relays.ExecuteAsync(curtain, open ? CurtainDirection.Open : CurtainDirection.Close,
                _activation, cancellationToken);
            report?.Invoke(new(curtain, completed));
        }
        catch
        {
            report?.Invoke(new(curtain, CurtainCommandState.Unknown));
            throw;
        }
    }
}
