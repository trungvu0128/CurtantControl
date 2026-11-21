using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private readonly BackgroundPreviewTimer _backgroundPreviewTimer = new();
    private Task? _backgroundPreviewTask;
    private int _backgroundPreviewVersion;
    private string _previewColorName = "";
    [ObservableProperty] public partial Bitmap? BackgroundPreview { get; set; }
    public bool IsBackgroundPreviewVisible => BackgroundPreview is not null && (Mode is ApplicationMode.User or ApplicationMode.Developer);
    public bool IsUserCameraOffline => CameraOffline && !IsBackgroundPreviewVisible;
    private void NotifyPreview()
    {
        OnPropertyChanged(nameof(IsBackgroundPreviewVisible));
        OnPropertyChanged(nameof(IsUserCameraOffline));
        OnPropertyChanged(nameof(UserCameraLabel));
        OnPropertyChanged(nameof(CameraIndicator));
    }
    private void ClearBackgroundPreview()
    {
        _backgroundPreviewVersion++;
        _backgroundPreviewTimer.Stop();
        var previous = BackgroundPreview;
        BackgroundPreview = null;
        previous?.Dispose();
        NotifyPreview();
    }
    private void ShowBackgroundPreview(CurtainOption curtain)
    {
        ClearBackgroundPreview();
        try { BackgroundPreview = BackgroundPreviewLoader.Load(curtain.PreviewImage); }
        catch (Exception error) { AddLog($"Background example unavailable · {curtain.Name}: {error.Message}"); }
        if (BackgroundPreview is null) return;
        _previewColorName = curtain.Name;
        NotifyPreview();
        var version = _backgroundPreviewVersion;
        _backgroundPreviewTask = _backgroundPreviewTimer.RestartAsync(() => Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed && version == _backgroundPreviewVersion) ClearBackgroundPreview();
        }));
    }
}
