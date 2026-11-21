using System;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services;

/// <summary>Every new selection replaces the previous preview deadline.</summary>
public sealed class BackgroundPreviewTimer(TimeSpan? duration = null) : IDisposable
{
    private CancellationTokenSource? _pending;
    private volatile int _generation;
    public Task RestartAsync(Action elapsed)
    {
        Stop();
        _pending = new CancellationTokenSource();
        return WaitAsync(_generation, _pending.Token, elapsed);
    }
    private async Task WaitAsync(int generation, CancellationToken token, Action elapsed)
    {
        try
        {
            await Task.Delay(duration ?? TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
            if (!token.IsCancellationRequested && generation == _generation) elapsed();
        }
        catch (OperationCanceledException) { }
    }
    public void Stop()
    {
        _generation++;
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }
    public void Dispose() => Stop();
}
