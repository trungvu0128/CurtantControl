using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services;

public sealed record AppRelease(string Version, string Notes, Uri Archive, Uri Checksum, long Size);
public interface IAppUpdateService
{
    string CurrentVersion { get; }
    bool CanInstall { get; }
    string? LastInstallMessage { get; }
    Task<AppRelease?> CheckAsync(CancellationToken token);
    Task PrepareInstallAsync(AppRelease release, IProgress<string> progress, CancellationToken token);
    Task CancelPreparedInstallAsync();
}

public sealed class NoAppUpdates : IAppUpdateService
{
    public string CurrentVersion => GitHubAppUpdates.InstalledVersion;
    public bool CanInstall => false;
    public string? LastInstallMessage => null;
    public Task<AppRelease?> CheckAsync(CancellationToken token) => Task.FromResult<AppRelease?>(null);
    public Task PrepareInstallAsync(AppRelease release, IProgress<string> progress, CancellationToken token) => throw new PlatformNotSupportedException();
    public Task CancelPreparedInstallAsync() => Task.CompletedTask;
}

public sealed class GitHubAppUpdates(HttpClient? client = null) : IAppUpdateService
{
    public const string Repository = "trungvu0128/CurtantControl";
    private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly HttpClient _http = client ?? Shared;
    private string? _preparedUnit;
    private string? _preparedCancellationPath;
    private System.Collections.Generic.Dictionary<string,string>? _preparedEnvironment;
    public static string InstalledVersion => typeof(GitHubAppUpdates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
    public string CurrentVersion => InstalledVersion;
    public string? LastInstallMessage
    {
        get
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CurtantControl", "update-status.json");
                if (!File.Exists(path)) return null;
                using var status = JsonDocument.Parse(File.ReadAllText(path));
                return status.RootElement.GetProperty("phase").GetString() switch
                {
                    "failed" => "Previous update failed: " + status.RootElement.GetProperty("message").GetString(),
                    "complete" => "Last update installed successfully.",
                    _ => null
                };
            }
            catch (Exception) { return "Could not read the previous update result."; }
        }
    }
    public bool CanInstall
    {
        get
        {
            if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.Arm) return false;
            try
            {
                return IsBuster(File.ReadAllText("/etc/os-release"))
                    && File.ReadAllText("/proc/device-tree/model").StartsWith("Raspberry Pi 4", StringComparison.Ordinal)
                    && File.Exists(Path.Combine(AppContext.BaseDirectory, "apply-update.py"));
            }
            catch (Exception) { return false; }
        }
    }
    public static bool IsBuster(string text) => Regex.IsMatch(text, "(?m)^VERSION_ID=\"?10\"?\\r?$")
        && Regex.IsMatch(text, "(?m)^ID=\"?(raspbian|debian)\"?\\r?$");
    public static Version ParseVersion(string value)
    {
        var match = Regex.Match(value, "^v?(\\d+\\.\\d+\\.\\d+)(?:-buster)?(?:\\+[a-zA-Z0-9.-]+)?$");
        return match.Success ? Version.Parse(match.Groups[1].Value) : throw new InvalidDataException("Invalid release version.");
    }
    public static AppRelease? ParseRelease(string json, string current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var version = root.GetProperty("tag_name").GetString() ?? "";
        var numeric = ParseVersion(version);
        if (version != "v" + numeric) throw new InvalidDataException("Release tags must use vMAJOR.MINOR.PATCH.");
        if (numeric <= ParseVersion(current)) return null;
        var name = $"CurtantControl-Final-v{numeric}-pi-buster-arm.tar.gz";
        Uri? archive = null, checksum = null;
        long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var assetName = asset.GetProperty("name").GetString();
            if (assetName != name && assetName != name + ".sha256") continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "");
            if (!TrustedAsset(url, version, assetName!)) throw new InvalidDataException("Unexpected update download address.");
            if (assetName == name) { if (archive is not null) throw new InvalidDataException("Duplicate release asset."); archive = url; size = asset.GetProperty("size").GetInt64(); }
            else { if (checksum is not null) throw new InvalidDataException("Duplicate checksum asset."); checksum = url; }
        }
        if (archive is null || checksum is null || size <= 0 || size > 200L * 1024 * 1024)
            throw new InvalidDataException("This release does not contain a complete Pi Buster update and checksum.");
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        return new(numeric.ToString(), notes.Length > 4000 ? notes[..4000] : notes, archive, checksum, size);
    }
    private static bool TrustedAsset(Uri uri, string tag, string name) => uri.Scheme == "https" && uri.Host == "github.com"
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.AbsolutePath == $"/{Repository}/releases/download/{tag}/{name}";
    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("CurtantControl-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    }
    public async Task<AppRelease?> CheckAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await GetAsync(new($"https://api.github.com/repos/{Repository}/releases/latest"), timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (json.Length > 1_000_000) throw new InvalidDataException("Release response is too large.");
        return ParseRelease(json, CurrentVersion);
    }
    public async Task<string> DownloadAsync(AppRelease release, string directory, IProgress<string> progress, CancellationToken token)
    {
        var tag = "v" + ParseVersion(release.Version);
        var filename = $"CurtantControl-Final-v{ParseVersion(release.Version)}-pi-buster-arm.tar.gz";
        if (!TrustedAsset(release.Archive, tag, filename) || !TrustedAsset(release.Checksum, tag, filename + ".sha256"))
            throw new InvalidDataException("Untrusted update address.");
        Directory.CreateDirectory(directory);
        using var checksumResponse = await GetAsync(release.Checksum, token).ConfigureAwait(false);
        checksumResponse.EnsureSuccessStatusCode();
        var checksum = await checksumResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        var match = Regex.Match(checksum, "\\A([a-fA-F0-9]{64})  " + Regex.Escape(filename) + "\\r?\\n?\\z");
        if (!match.Success) throw new InvalidDataException("Invalid update checksum.");
        using var response = await GetAsync(release.Archive, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var path = Path.Combine(directory, filename);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[65536];
                long total = 0;
                int previous = -1;
                while (true)
                {
                    var count = await input.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (count == 0) break;
                    total += count;
                    if (total > release.Size || total > 200L * 1024 * 1024) throw new InvalidDataException("Update size mismatch.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    var percent = (int)(100 * total / release.Size);
                    if (percent != previous) { progress.Report($"Downloading update… {percent}%"); previous = percent; }
                }
                if (total != release.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(match.Groups[1].Value)))
                    throw new InvalidDataException("Update checksum or size verification failed.");
                output.Flush(true);
            }
            File.WriteAllText(path + ".sha256", checksum);
            return path;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public async Task PrepareInstallAsync(AppRelease release, IProgress<string> progress, CancellationToken token)
    {
        if (!CanInstall) throw new PlatformNotSupportedException("Install updates from your Raspberry Pi Buster desktop.");
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "CurtantControl", "updates", Guid.NewGuid().ToString("N"));
        string archive;
        using (var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(10));
            try { archive = await DownloadAsync(release, directory, progress, downloadTimeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("Update download timed out. Please try again."); }
        }
        progress.Report("Verifying and preparing update…");
        var uid = (await RunAsync("/usr/bin/id", ["-u"], null, token).ConfigureAwait(false)).Trim();
        if (!Regex.IsMatch(uid, "^[0-9]+$")) throw new IOException("Could not identify the desktop session.");
        var runtime = "/run/user/" + uid;
        if (!File.Exists(runtime + "/bus")) throw new IOException("Desktop update service unavailable. Sign in to the Pi desktop first.");
        var unit = "curtantcontrol-update-" + Guid.NewGuid().ToString("N");
        var status = Path.Combine(directory, "status.json");
        var args = new[] { "--user", "--collect", "--unit=" + unit,
            "--setenv=DISPLAY=" + (Environment.GetEnvironmentVariable("DISPLAY") ?? ":0"),
            "--setenv=XAUTHORITY=" + (Environment.GetEnvironmentVariable("XAUTHORITY") ?? "/home/pi/.Xauthority"),
            "/usr/bin/python3", "-u", Path.Combine(AppContext.BaseDirectory, "apply-update.py"),
            archive, release.Version, Environment.ProcessId.ToString(), status };
        var environment = new System.Collections.Generic.Dictionary<string,string> { ["XDG_RUNTIME_DIR"] = runtime, ["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=" + runtime + "/bus" };
        var ready = false;
        try
        {
            _preparedUnit = unit;
            _preparedCancellationPath = Path.Combine(directory, "cancelled");
            _preparedEnvironment = environment;
            await RunAsync("/usr/bin/systemd-run", args, environment, token).ConfigureAwait(false);
            for (var i = 0; i < 240; i++)
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(status))
                {
                    using var state = JsonDocument.Parse(await File.ReadAllTextAsync(status, token).ConfigureAwait(false));
                    var phase = state.RootElement.GetProperty("phase").GetString();
                    if (phase == "ready") { ready = true; return; }
                    if (phase == "failed") throw new IOException(state.RootElement.GetProperty("message").GetString());
                }
                await Task.Delay(500, token).ConfigureAwait(false);
            }
            throw new TimeoutException("Update preparation timed out. The current app has not been replaced.");
        }
        finally
        {
            if (!ready) await CancelPreparedInstallAsync().ConfigureAwait(false);
        }
    }
    public async Task CancelPreparedInstallAsync()
    {
        var unit = _preparedUnit;
        _preparedUnit = null;
        if (_preparedCancellationPath is { } marker)
            try { File.WriteAllText(marker, "cancelled"); } catch (Exception) { }
        if (unit is not null)
            try { await RunAsync("/usr/bin/systemctl", ["--user", "stop", unit], _preparedEnvironment, CancellationToken.None).ConfigureAwait(false); } catch (Exception) { }
    }
    private static async Task<string> RunAsync(string executable, string[] args, System.Collections.Generic.Dictionary<string,string>? environment, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        if (environment is not null) foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start) ?? throw new IOException("Could not start desktop update service.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch { try { process.Kill(); } catch (Exception) { } throw; }
        var error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0) throw new IOException("Desktop update service failed: " + error.Trim());
        return await stdout.ConfigureAwait(false);
    }
}
