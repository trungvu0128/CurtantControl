using System;
using System.IO;

namespace CurtantControl.Services.Camera;

public static class CameraAccess
{
    // Advisory only: permissions and races prevent a complete ownership check.
    // The capture backend remains authoritative and must still handle EBUSY/ENOSPC.
    public static void CheckAvailable(string? device)
    {
        if (!OperatingSystem.IsLinux() || CameraProcessPlan.IsHttpStream(device)) return;
        // /dev/vchiq is shared with non-camera GPU clients, so it cannot prove CSI ownership.
        if (device == CameraCatalog.LegacyCameraId) return;
        var target = device;
        if (target is null || !target.StartsWith("/dev/", StringComparison.Ordinal)) return;
        foreach (var process in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(process), out var pid) || pid == Environment.ProcessId) continue;
            try
            {
                foreach (var fd in Directory.EnumerateFiles(Path.Combine(process, "fd")))
                {
                    if (new FileInfo(fd).LinkTarget == target)
                        throw new CameraInUseException($"{target} is held by process {pid}. Use its stream service or release the camera in that application.");
                }
            }
            catch (CameraInUseException) { throw; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}

public sealed class CameraInUseException(string message) : IOException(message);
