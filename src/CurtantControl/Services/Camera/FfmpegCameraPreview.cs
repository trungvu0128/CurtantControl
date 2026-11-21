using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Camera;

public sealed class FfmpegCameraPreview(string executable, string? device, bool activatePythonCamera = true) : ICameraPreview
{
    public const int Width = 640;
    public const int Height = 360;

    public async Task RunAsync(Func<byte[], Task> onFrame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(device))
            throw new InvalidOperationException("Set CameraDevice to your webcam name in photobooth.json.");
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Camera capture supports Windows and Linux.");
        var legacyCsi = device == CameraCatalog.LegacyCameraId;
        var httpStream = CameraProcessPlan.IsHttpStream(device);
        CameraAccess.CheckAvailable(device);
        if (legacyCsi && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Legacy ribbon-camera capture requires Raspberry Pi OS.");
        var raspivid = legacyCsi ? CameraCatalog.FindRaspivid()
            ?? throw new FileNotFoundException("raspivid was not found. This camera requires Buster's legacy camera tools.") : null;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = legacyCsi || httpStream
        };
        foreach (var argument in CameraProcessPlan.Ffmpeg(device, OperatingSystem.IsWindows()))
            start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var source = legacyCsi ? new Process { StartInfo = new ProcessStartInfo(raspivid!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        } } : null;
        var errors = new Queue<string>();
        void Record(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lock (errors)
            {
                errors.Enqueue(message);
                while (errors.Count > 8) errors.Dequeue();
            }
        }
        string Details()
        {
            lock (errors) return errors.Count == 0
                ? "Camera stream ended. Check camera access and configuration."
                : string.Join(Environment.NewLine, errors);
        }
        process.ErrorDataReceived += (_, args) => Record(args.Data);
        if (source is not null)
        {
            foreach (var argument in CameraProcessPlan.Raspivid()) source.StartInfo.ArgumentList.Add(argument);
            source.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null) Record("raspivid: " + args.Data);
            };
        }
        process.Start();
        process.BeginErrorReadLine();
        Task? copy = null;
        using var pipeline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sourceStarted = false;
        void Kill(Process target)
        {
            try { if (!target.HasExited) target.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        using var registration = pipeline.Token.Register(() =>
        {
            if (sourceStarted) Kill(source!);
            Kill(process);
        });
        try
        {
            if (source is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                source.Start();
                sourceStarted = true;
                if (pipeline.IsCancellationRequested) Kill(source);
                source.BeginErrorReadLine();
                copy = CopyEncodedAsync();
            }
            else if (httpStream) copy = CopyEncodedAsync();
            async Task CopyEncodedAsync()
            {
                try
                {
                    if (httpStream)
                        await HttpMjpegInput.CopyAsync(device!, process.StandardInput.BaseStream, pipeline.Token, activatePythonCamera).ConfigureAwait(false);
                    else
                        await source!.StandardOutput.BaseStream.CopyToAsync(process.StandardInput.BaseStream, pipeline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { if (!pipeline.IsCancellationRequested) Record("HTTP camera connection timed out."); }
                catch (Exception error) { Record("Camera input: " + error.Message); }
                finally
                {
                    try { process.StandardInput.Close(); }
                    catch (InvalidOperationException) { }
                }
            }
            var pixels = new byte[Width * Height * 4];
            while (true)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    await process.StandardOutput.BaseStream.ReadExactlyAsync(pixels, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("No camera frames received for 10 seconds. " + Details());
                }
                await onFrame(pixels).ConfigureAwait(false);
            }
        }
        catch (EndOfStreamException) when (!cancellationToken.IsCancellationRequested)
        {
            pipeline.Cancel();
            if (sourceStarted) await source!.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw new IOException(Details());
        }
        finally
        {
            pipeline.Cancel();
            if (sourceStarted) Kill(source!);
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (sourceStarted) await source!.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (copy is not null) await copy.ConfigureAwait(false);
        }
    }
}
