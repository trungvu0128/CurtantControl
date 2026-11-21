using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private IAppUpdateService _updates = null!;
    private AppRelease? _availableUpdate;
    private Task? _startupUpdateTask;
    private CancellationTokenSource? _updateCancellation;
    private bool _updateDismissed;
    private bool _updateHandoff;
    public event Action? UpdateInstallReady;
    [ObservableProperty] public partial bool IsUpdatesOpen { get; set; }
    [ObservableProperty] public partial bool IsCheckingUpdate { get; set; }
    [ObservableProperty] public partial bool IsPreparingUpdate { get; set; }
    [ObservableProperty] public partial string UpdateStatus { get; set; } = "Check for a new version.";
    public string CurrentAppVersion => _updates.CurrentVersion;
    public string LatestAppVersion => _availableUpdate?.Version ?? "—";
    public string UpdateNotes => _availableUpdate?.Notes ?? "";
    public string UpdateDownloadSize => _availableUpdate is not { } release ? "" : release.SmallUpdate is { } small
        ? $"Small update · {small.Size / 1048576d:0.0} MB if compatible"
        : $"Full update · {release.Size / 1048576d:0.0} MB";
    public string LastUpdateOutcome => _updates.LastInstallMessage ?? "";
    public string UpdateNotification => $"Update {LatestAppVersion} available";
    public bool HasUpdateNotification => _availableUpdate is not null && !_updateDismissed && !IsUpdatesOpen
        && !IsSettingsOpen && !IsPinDialogOpen && !IsMoving;
    public bool IsUpdateBusy => IsCheckingUpdate || IsPreparingUpdate;
    public bool CanInstallUpdate => Mode == ApplicationMode.Developer && IsUpdatesOpen && !IsUpdateBusy
        && !IsMoving && !_disposed && _availableUpdate is not null && _updates.CanInstall;
    public bool CanCheckUpdate => Mode == ApplicationMode.Developer && IsUpdatesOpen && !IsUpdateBusy && !_disposed;
    public string UpdateInstallHint => _updates.CanInstall ? "The app will back up, close, update and restart. Your settings and PIN are preserved."
        : "Check updates here. Install updates from your Raspberry Pi desktop.";
    partial void OnIsUpdatesOpenChanged(bool value) { NotifyUserActions(); NotifyUpdateActions(); }
    partial void OnIsCheckingUpdateChanged(bool value) => NotifyUpdateActions();
    partial void OnIsPreparingUpdateChanged(bool value) { NotifyUserActions(); NotifyUpdateActions(); }
    private void NotifyUpdateActions()
    {
        foreach (var property in new[] { nameof(CanInstallUpdate), nameof(CanCheckUpdate), nameof(IsUpdateBusy),
                     nameof(HasUpdateNotification), nameof(UpdateNotification), nameof(LatestAppVersion), nameof(UpdateNotes), nameof(UpdateDownloadSize), nameof(LastUpdateOutcome), nameof(CanSelectScene) })
            OnPropertyChanged(property);
    }
    public Task CheckForUpdatesOnStartupAsync() => _startupUpdateTask ??= CheckUpdatesCoreAsync(true);
    [RelayCommand] private void OpenUpdates()
    {
        if (!CanConfigureRazr || IsSettingsOpen) return;
        IsUpdatesOpen = true;
        if (_availableUpdate is not null) UpdateStatus = "A new version is available.";
    }
    [RelayCommand] private void CloseUpdates()
    {
        if (_updateHandoff) return;
        _updateCancellation?.Cancel();
        IsUpdatesOpen = false;
    }
    [RelayCommand] private void DismissUpdate()
    {
        _updateDismissed = true;
        NotifyUpdateActions();
    }
    [RelayCommand] private async Task CheckUpdatesAsync()
    {
        if (CanCheckUpdate) await CheckUpdatesCoreAsync(false);
    }
    private async Task CheckUpdatesCoreAsync(bool automatic)
    {
        if (_disposed || IsUpdateBusy) return;
        IsCheckingUpdate = true;
        UpdateStatus = "Checking GitHub Releases…";
        try
        {
            // Yield so startup rendering and camera startup never wait for HTTP.
            await Task.Yield();
            var release = await _updates.CheckAsync(_lifetime.Token);
            if (_disposed) return;
            _availableUpdate = release;
            _updateDismissed = false;
            UpdateStatus = release is null ? "No newer published version is available." : "A new version is available.";
            if (release is not null) AddLog($"Update available: {release.Version}");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            UpdateStatus = "Could not check updates. Check your connection and try again.";
            AddLog($"Update check failed{(automatic ? " at startup" : "")}: {error.Message}");
        }
        finally { IsCheckingUpdate = false; NotifyUpdateActions(); }
    }
    [RelayCommand] private async Task InstallUpdateAsync()
    {
        if (!CanInstallUpdate) return;
        IsPreparingUpdate = true;
        _updateCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            await _updates.PrepareInstallAsync(_availableUpdate!, new Progress<string>(message =>
            {
                if (!_disposed) UpdateStatus = message;
            }), _updateCancellation.Token);
            _updateCancellation.Token.ThrowIfCancellationRequested();
            if (_disposed || !IsUpdatesOpen || Mode != ApplicationMode.Developer) throw new OperationCanceledException();
            _updateHandoff = true;
            UpdateStatus = "Update ready. Closing the app to install…";
            UpdateInstallReady?.Invoke();
        }
        catch (OperationCanceledException) { if (!_disposed) UpdateStatus = "Update cancelled. The current app was kept."; }
        catch (Exception error) { UpdateStatus = "Update could not start: " + error.Message; AddLog(UpdateStatus); }
        finally
        {
            if (!_updateHandoff) await _updates.CancelPreparedInstallAsync();
            _updateCancellation.Dispose(); _updateCancellation = null; IsPreparingUpdate = false;
        }
    }
}
