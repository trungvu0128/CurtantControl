using CurtantControl.Services;
using CurtantControl.ViewModels;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class UpdateChecks
{
    internal static string ReleaseJson(string version = "0.11.0", bool prerelease = false, string? host = null) => JsonSerializer.Serialize(new
    {
        tag_name = "v" + version, draft = false, prerelease, body = "Release notes",
        assets = new[] {
            new { name = $"CurtantControl-Final-v{version}-pi-buster-arm.tar.gz", size = 4, browser_download_url = $"https://{host ?? "github.com"}/trungvu0128/CurtantControl/releases/download/v{version}/CurtantControl-Final-v{version}-pi-buster-arm.tar.gz" },
            new { name = $"CurtantControl-Final-v{version}-pi-buster-arm.tar.gz.sha256", size = 120, browser_download_url = $"https://github.com/trungvu0128/CurtantControl/releases/download/v{version}/CurtantControl-Final-v{version}-pi-buster-arm.tar.gz.sha256" }
        }
    });
    public static async Task Run(Func<string, Func<Task>, Task> check, Action<bool,string> assert)
    {
        await check("a small update matches only its exact baseline and needs both release assets", () =>
        {
            var feed = System.Text.Json.Nodes.JsonNode.Parse(ReleaseJson("0.11.2"))!;
            var assets = feed["assets"]!.AsArray();
            const string filename = "CurtantControl-Update-v0.11.1-to-v0.11.2-pi-buster-arm.zip";
            foreach (var name in new[] { filename, filename + ".sha256" })
                assets.Add(JsonSerializer.SerializeToNode(new { name, size = 1000,
                    browser_download_url = $"https://github.com/trungvu0128/CurtantControl/releases/download/v0.11.2/{name}" }));
            assert(GitHubAppUpdates.ParseRelease(feed.ToJsonString(), "0.11.1-buster")?.SmallUpdate?.Name == filename, "Matching small update missed.");
            assert(GitHubAppUpdates.ParseRelease(feed.ToJsonString(), "0.11.0-buster") is { SmallUpdate: null }, "Wrong baseline offered a small update.");
            assets.RemoveAt(assets.Count - 1);
            assert(GitHubAppUpdates.ParseRelease(feed.ToJsonString(), "0.11.1-buster") is { SmallUpdate: null }, "Small update offered without checksum.");
            return Task.CompletedTask;
        });
        await check("update feed compares semantic versions, ignores previews and rejects wrong assets/hosts", () =>
        {
            assert(GitHubAppUpdates.ParseRelease(ReleaseJson(), "0.10.5-buster+abc")?.Version == "0.11.0", "New release missed.");
            assert(GitHubAppUpdates.ParseRelease(ReleaseJson(), "0.11.0-buster") is null, "Same release offered again.");
            assert(GitHubAppUpdates.ParseRelease(ReleaseJson(), "0.12.0") is null, "Downgrade offered.");
            assert(GitHubAppUpdates.ParseRelease(ReleaseJson(prerelease: true), "0.10.5") is null, "Preview offered.");
            foreach (var json in new[] { ReleaseJson(host: "evil.example"), ReleaseJson().Replace("buster-arm", "modern-arm64"), ReleaseJson().Replace(".sha256", ".missing") })
            {
                try { GitHubAppUpdates.ParseRelease(json,"0.10.5"); throw new Exception("Invalid release accepted."); }
                catch (InvalidDataException) { }
            }
            assert(GitHubAppUpdates.IsBuster("ID=raspbian\nVERSION_ID=\"10\"\n") && !GitHubAppUpdates.IsBuster("ID=raspbian\nVERSION_ID=\"12\"\n"), "OS filter failed.");
            return Task.CompletedTask;
        });
        await check("update check handles unpublished release and uses public GitHub API headers", async () =>
        {
            using var http = new HttpClient(new DelegateHttpHandler(request =>
            {
                assert(request.RequestUri!.Host == "api.github.com" && request.Headers.UserAgent.Any() && request.Headers.Authorization is null, "Wrong feed or embedded credentials.");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }));
            assert(await new GitHubAppUpdates(http).CheckAsync(default) is null, "No release became an error/update.");
        });
        await check("update download verifies exact checksum/size and removes corrupt partial files", async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(),"update-download-"+Guid.NewGuid());
            var release = GitHubAppUpdates.ParseRelease(ReleaseJson(),"0.10.5")!;
            var bytes = Encoding.ASCII.GetBytes("test");
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            try
            {
                foreach (var corrupt in new[] {false,true})
                {
                    using var http = new HttpClient(new DelegateHttpHandler(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = request.RequestUri!.AbsolutePath.EndsWith(".sha256")
                        ? new StringContent((corrupt ? new string('0',64) : sha)+"  "+Path.GetFileName(release.Archive.AbsolutePath)+"\n")
                        : new ByteArrayContent(bytes) })));
                    var job=Path.Combine(folder,corrupt.ToString());
                    try
                    {
                        var path=await new GitHubAppUpdates(http).DownloadAsync(release,job,new Progress<string>(),default);
                        assert(!corrupt && File.ReadAllBytes(path).SequenceEqual(bytes),"Corrupt download accepted.");
                    }
                    catch(InvalidDataException) { assert(corrupt && !Directory.EnumerateFiles(job,"*.tar.gz").Any(),"Bad download left a package."); }
                }
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        });
        await check("startup checks once without blocking User controls, only notifies and never installs", async () =>
        {
            var service = new FakeUpdates();
            await using var vm = new MainViewModel(new(), cameraCatalog:new FakeCameraCatalog(), updateService:service);
            vm.EnterUserModeCommand.Execute(null);
            var first=vm.CheckForUpdatesOnStartupAsync();
            assert(ReferenceEquals(first,vm.CheckForUpdatesOnStartupAsync()) && vm.CanSelectScene,"Startup blocked controls or checked twice.");
            service.Result.TrySetResult(GitHubAppUpdates.ParseRelease(ReleaseJson(),"0.10.5"));
            await first;
            assert(service.Checks==1 && service.Installs==0 && vm.HasUpdateNotification && !vm.IsUpdatesOpen,"Startup installed or opened a modal.");
            vm.OpenUpdatesCommand.Execute(null); await vm.InstallUpdateCommand.ExecuteAsync(null);
            assert(!vm.IsUpdatesOpen && service.Installs==0,"User bypassed Developer update gate.");
            vm.DismissUpdateCommand.Execute(null); assert(!vm.HasUpdateNotification,"Notice did not dismiss.");
        });
        await check("Developer update waits for prepared worker before requesting close; errors keep app open", async () =>
        {
            foreach(var fail in new[]{false,true})
            {
                var service=new FakeUpdates { FailInstall=fail };
                service.Result.TrySetResult(GitHubAppUpdates.ParseRelease(ReleaseJson(),"0.10.5"));
                await using var vm=new MainViewModel(new(),cameraCatalog:new FakeCameraCatalog(),updateService:service);
                await vm.CheckForUpdatesOnStartupAsync(); vm.EnterDeveloperModeCommand.Execute(null); vm.OpenUpdatesCommand.Execute(null);
                var closes=0; vm.UpdateInstallReady+=()=>closes++;
                var prepare=vm.InstallUpdateCommand.ExecuteAsync(null);
                assert(vm.IsPreparingUpdate && closes==0 && !vm.CanSelectScene,"App closed before worker ready.");
                service.Prepared.TrySetResult(); await prepare;
                assert(closes==(fail?0:1) && service.Installs==1,"Handoff/error gate failed.");
            }
        });
        await check("cancel preparation and offline startup keep current app usable", async () =>
        {
            var service=new FakeUpdates();
            service.Result.TrySetResult(GitHubAppUpdates.ParseRelease(ReleaseJson(),"0.10.5"));
            await using var vm=new MainViewModel(new(),cameraCatalog:new FakeCameraCatalog(),updateService:service);
            await vm.CheckForUpdatesOnStartupAsync(); vm.EnterDeveloperModeCommand.Execute(null); vm.OpenUpdatesCommand.Execute(null);
            var closes=0; vm.UpdateInstallReady+=()=>closes++;
            var prepare=vm.InstallUpdateCommand.ExecuteAsync(null); vm.CloseUpdatesCommand.Execute(null); await prepare;
            assert(closes==0 && service.Cancels==1 && !vm.IsUpdatesOpen && vm.CanSelectScene,"Cancel closed/blocked app or left worker armed.");
            var offline=new FakeUpdates(); offline.Result.TrySetException(new HttpRequestException("offline"));
            await using var user=new MainViewModel(new(),cameraCatalog:new FakeCameraCatalog(),updateService:offline);
            user.EnterUserModeCommand.Execute(null); await user.CheckForUpdatesOnStartupAsync();
            assert(user.CanSelectScene && !user.IsUpdatesOpen && !user.HasUpdateNotification,"Offline startup blocked User.");
        });
    }
}

internal sealed class FakeUpdates : IAppUpdateService
{
    public string CurrentVersion=>"0.10.5-buster";
    public bool CanInstall=>true;
    public string? LastInstallMessage=>null;
    public int Checks,Installs,Cancels;
    public bool FailInstall;
    public TaskCompletionSource<AppRelease?> Result {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Prepared {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<AppRelease?> CheckAsync(CancellationToken token) { Checks++; return Result.Task.WaitAsync(token); }
    public async Task PrepareInstallAsync(AppRelease release,IProgress<string> progress,CancellationToken token)
    { Installs++; await Prepared.Task.WaitAsync(token); if(FailInstall) throw new IOException("worker not ready"); }
    public Task CancelPreparedInstallAsync() { Cancels++; return Task.CompletedTask; }
}
