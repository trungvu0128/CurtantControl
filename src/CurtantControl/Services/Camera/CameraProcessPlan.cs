using System.Collections.Generic;
using System;

namespace CurtantControl.Services.Camera;

/// <summary>Arguments are passed directly to processes, never through a shell.</summary>
public static class CameraProcessPlan
{
    public static bool IsHttpStream(string? device) => Uri.TryCreate(device, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    public static IReadOnlyList<string> Ffmpeg(string? device, bool windows)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin" };
        if (IsHttpStream(device))
            args.AddRange(["-f", "mjpeg", "-i", "pipe:0"]);
        else if (device == CameraCatalog.LegacyCameraId)
            args.AddRange(["-probesize", "32768", "-analyzeduration", "0", "-f", "h264", "-i", "pipe:0"]);
        else
            args.AddRange(["-f", windows ? "dshow" : "v4l2", "-i", windows ? $"video={device}" : device ?? "/dev/video0"]);
        args.AddRange(["-an", "-vf", "fps=15,scale=640:360:force_original_aspect_ratio=increase,crop=640:360",
            "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1"]);
        return args;
    }

    public static IReadOnlyList<string> Raspivid() =>
        ["-n", "-t", "0", "-w", "640", "-h", "360", "-fps", "15", "-b", "1000000", "-g", "15", "-o", "-"];
}
