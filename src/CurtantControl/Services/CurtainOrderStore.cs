using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CurtantControl.Services;

public interface ICurtainOrderStore
{
    int[]? Load();
    void Save(int[] frontToBack);
}

/// <summary>Persists only layer order; deployment hardware settings remain unchanged.</summary>
public sealed class CurtainOrderStore(string? path = null) : ICurtainOrderStore
{
    private readonly string _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CurtantControl", "curtain-order.json");

    public int[]? Load()
    {
        if (!File.Exists(_path)) return null;
        var order = JsonSerializer.Deserialize<int[]>(File.ReadAllText(_path));
        Validate(order);
        return order;
    }

    public void Save(int[] frontToBack)
    {
        Validate(frontToBack);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(frontToBack));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static void Validate(int[]? order)
    {
        if (order is null || order.Length != 4 || !order.Order().SequenceEqual(new[] { 1, 2, 3, 4 }))
            throw new InvalidDataException("Order must contain each curtain exactly once.");
    }
}
