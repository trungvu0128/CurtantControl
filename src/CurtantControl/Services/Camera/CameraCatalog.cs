using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Camera;

public sealed record CameraDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public interface ICameraCatalog
{
    Task<IReadOnlyList<CameraDevice>> ListAsync(CancellationToken cancellationToken);
}

public interface ICameraPreview
{
    Task RunAsync(Func<byte[], Task> onFrame, CancellationToken cancellationToken);
}

public sealed class CameraCatalog(string ffmpegPath, string? streamUrl = null, bool automatic = false) : ICameraCatalog
{
    public const string AutomaticCameraId = "automatic://camera";
    public const string LegacyCameraId = "raspivid://0";
    public static string? FindRaspivid()
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                     .Concat(new[] { "/usr/bin", "/opt/vc/bin" }))
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            var candidate = Path.Combine(folder, "raspivid");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public async Task<IReadOnlyList<CameraDevice>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (automatic)
        {
            var choices = new List<CameraDevice> { new(AutomaticCameraId, "Auto") };
            choices.AddRange(await ListCandidatesAsync(cancellationToken));
            return choices;
        }
        if (!string.IsNullOrWhiteSpace(streamUrl))
        {
            if (!CameraProcessPlan.IsHttpStream(streamUrl))
                throw new ArgumentException("CameraStreamUrl must be an absolute HTTP or HTTPS URL.");
            // The existing server owns the physical camera. Do not enumerate or open it here.
            return new[] { new CameraDevice(streamUrl, "Existing camera stream (Python / MJPEG)") };
        }
        if (OperatingSystem.IsWindows())
            return ParseDirectShow(await RunAsync(ffmpegPath,
                ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"], cancellationToken));
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Camera discovery supports Windows and Linux.");

        var cameras = new List<CameraDevice>();
        if (FindRaspivid() is not null)
        {
            var firmware = await ProbeAsync("vcgencmd", ["get_camera"], cancellationToken);
            if (HasLegacyCamera(firmware))
                cameras.Add(new(LegacyCameraId, "Raspberry Pi ribbon camera (legacy CSI)"));
        }
        if (!Directory.Exists("/sys/class/video4linux")) return cameras;
        foreach (var folder in Directory.GetDirectories("/sys/class/video4linux").Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = "/dev/" + Path.GetFileName(folder);
            var namePath = Path.Combine(folder, "name");
            var name = File.Exists(namePath) ? (await File.ReadAllTextAsync(namePath, cancellationToken)).Trim() : id;
            if (IsInternalPipeline(name)) continue;
            var details = await ProbeAsync("v4l2-ctl", ["--device", id, "--all"], cancellationToken);
            if (!HasCaptureCapability(details)) continue;
            cameras.Add(new(id, $"{name} ({id})"));
        }
        return cameras;
    }

    public async Task<IReadOnlyList<CameraDevice>> ListCandidatesAsync(CancellationToken token)
    {
        var choices = new List<CameraDevice>();
        var url = streamUrl;
        if (string.IsNullOrWhiteSpace(url) && OperatingSystem.IsLinux())
            url = "http://127.0.0.1:5000/video_feed";
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!CameraProcessPlan.IsHttpStream(url))
                throw new ArgumentException("CameraStreamUrl must be an absolute HTTP or HTTPS URL.");
            choices.Add(new(url, "Existing camera stream (Python / MJPEG)"));
        }
        try
        {
            var hardware = await new CameraCatalog(ffmpegPath).ListAsync(token);
            // USB/direct V4L2 before legacy CSI; never open them during discovery.
            choices.AddRange(hardware.OrderBy(c => c.Id == LegacyCameraId ? 1 : 0));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            // A missing discovery utility must not hide an independently usable HTTP feed.
            if (choices.Count == 0) throw;
        }
        return choices.DistinctBy(c => c.Id).ToArray();
    }

    public static bool HasLegacyCamera(string firmware) => Regex.IsMatch(firmware, @"\bsupported=1\b")
        && Regex.IsMatch(firmware, @"\bdetected=1\b");

    private static async Task<string> ProbeAsync(string executable, string[] arguments, CancellationToken token)
    {
        try { return await RunAsync(executable, arguments, token); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            return string.Empty;
        }
    }

    public static bool IsInternalPipeline(string name) =>
        name.StartsWith("bcm2835-codec", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("bcm2835-isp", StringComparison.OrdinalIgnoreCase);

    // Prefer per-node Device Caps so metadata-only nodes aren't mistaken for capture nodes.
    public static bool HasCaptureCapability(string details)
    {
        var block = Regex.Match(details, @"(?m)^[ \t]*Device Caps[ \t]*:[ \t]*0x([0-9a-fA-F]+)");
        if (!block.Success)
            block = Regex.Match(details, @"(?m)^[ \t]*Capabilities[ \t]*:[ \t]*0x([0-9a-fA-F]+)");
        if (block.Success && uint.TryParse(block.Groups[1].Value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var flags))
        {
            const uint capture = 0x00000001;
            const uint memoryToMemory = 0x00004000 | 0x00008000;
            // The direct FFmpeg input uses single-planar V4L2 capture, not codec/ISP pipelines.
            return (flags & capture) != 0 && (flags & memoryToMemory) == 0;
        }
        var index = details.IndexOf("Device Caps", StringComparison.Ordinal);
        var capabilities = index < 0 ? details : details[index..];
        var nextSection = Regex.Match(capabilities, @"\r?\n[^\s]");
        if (nextSection.Success) capabilities = capabilities[..nextSection.Index];
        return capabilities.Contains("Video Capture", StringComparison.Ordinal)
            && !capabilities.Contains("Memory-to-Memory", StringComparison.OrdinalIgnoreCase)
            && !capabilities.Contains("Multiplanar", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<CameraDevice> ParseDirectShow(string details)
    {
        var cameras = new List<CameraDevice>();
        var videoSection = false;
        var pendingVideo = false;
        foreach (var line in details.Split('\n'))
        {
            if (line.Contains("DirectShow video devices")) videoSection = true;
            if (line.Contains("DirectShow audio devices")) videoSection = false;
            var quoted = Regex.Match(line, "\"([^\"]+)\"");
            if (!quoted.Success) continue;
            if (line.Contains("Alternative name"))
            {
                if (pendingVideo) cameras[^1] = cameras[^1] with { Id = quoted.Groups[1].Value };
                pendingVideo = false;
            }
            else
            {
                pendingVideo = line.Contains("(video)") || (videoSection && !line.Contains("(audio)"));
                if (pendingVideo) cameras.Add(new(quoted.Groups[1].Value, quoted.Groups[1].Value));
            }
        }
        return cameras.DistinctBy(c => c.Id).ToArray();
    }

    private static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException($"Could not start {executable}.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var registration = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return await stdout + "\n" + await stderr;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("Camera discovery timed out.");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
        }
    }
}
