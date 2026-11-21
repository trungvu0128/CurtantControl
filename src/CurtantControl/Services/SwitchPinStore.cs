using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace CurtantControl.Services;

public interface ISwitchPinStore
{
    bool Verify(string pin);
    void Save(string pin);
}

public sealed class SwitchPinStore(string? path = null, bool inMemory = false) : ISwitchPinStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "switch-pin.json");
    private Credential? _memory;
    public sealed record Credential(string Salt, string Hash);
    public static bool IsValid(string pin) => pin.Length is >= 4 and <= 8 && System.Linq.Enumerable.All(pin, c => c is >= '0' and <= '9');
    private static byte[] Hash(string pin, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(pin, salt, 100_000, HashAlgorithmName.SHA256, 32);
    public bool Verify(string pin)
    {
        if (!IsValid(pin)) return false;
        var saved = inMemory ? _memory : File.Exists(_path)
            ? JsonSerializer.Deserialize<Credential>(File.ReadAllText(_path)) ?? throw new InvalidDataException("Invalid PIN configuration.") : null;
        if (saved is null) return pin == "1234";
        return CryptographicOperations.FixedTimeEquals(Hash(pin, Convert.FromBase64String(saved.Salt)), Convert.FromBase64String(saved.Hash));
    }
    public void Save(string pin)
    {
        if (!IsValid(pin)) throw new ArgumentException("Use 4–8 digits.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var credential = new Credential(Convert.ToBase64String(salt), Convert.ToBase64String(Hash(pin, salt)));
        if (inMemory) { _memory = credential; return; }
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                JsonSerializer.Serialize(stream, credential);
                stream.Flush(true);
            }
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
