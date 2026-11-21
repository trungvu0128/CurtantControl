using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace CurtantControl.Services;

public static class BackgroundPreviewLoader
{
    public static Bitmap? Load(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var root = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Path.IsPathRooted(relativePath) || !path.StartsWith(root, comparison))
            throw new InvalidDataException("Preview images must be relative files inside the application folder.");
        using var stream = File.OpenRead(path);
        return Bitmap.DecodeToWidth(stream, 960);
    }
}
