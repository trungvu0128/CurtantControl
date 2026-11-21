using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Camera;

/// <summary>One owner at a time; failed sessions finish cleanup before the next attempt.</summary>
public sealed class AutomaticCameraPreview(
    Func<CancellationToken, Task<IReadOnlyList<CameraDevice>>> discover,
    Func<string, ICameraPreview> create,
    Action<string> report,
    TimeSpan? retryDelay = null) : ICameraPreview
{
    public async Task RunAsync(Func<byte[], Task> onFrame, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<CameraDevice> candidates = Array.Empty<CameraDevice>();
            try { candidates = await discover(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { report($"Camera discovery: {error.Message}"); }
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                report($"Trying {candidate.Name} · {candidate.Id}");
                try
                {
                    var firstFrame = true;
                    await create(candidate.Id).RunAsync(async pixels =>
                    {
                        if (firstFrame) { report($"Connected · {candidate.Name}"); firstFrame = false; }
                        await onFrame(pixels);
                    }, cancellationToken);
                    throw new IOException("Camera stream ended.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception error) { report($"Unavailable · {candidate.Name}: {error.Message}"); }
            }
            report("No working camera · retrying automatically (Refresh or select a camera to override)");
            await Task.Delay(retryDelay ?? TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}
