using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CurtantControl.Services;
using CurtantControl.Services.Camera;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CurtantControl.ViewModels;

public enum ApplicationMode { Startup, Developer, User }

public partial class MainViewModel
{
    [ObservableProperty] public partial ApplicationMode Mode { get; set; } = ApplicationMode.Startup;
    private bool _cameraRequested;
    private bool _restoreBackgroundButtonOnModeEntry;
    private void RestoreBackgroundButtonSelection(bool stage = true)
    {
        var id = _settings.SimulateRelays ? _razr.SelectedSimulatedBackgroundId : _razr.SelectedBackgroundId;
        var selected = BackgroundOptions.FirstOrDefault(p => p.Id == id);
        if (selected is null) return;
        foreach (var option in BackgroundOptions) option.IsSelected = option == selected;
        _selectionRestored = true;
        _pendingUserCurtain = stage && !IsBackgroundApplied(selected) ? selected : null;
        _restoreBackgroundButtonOnModeEntry = !stage;
        SelectionLabel = $"{selected.Name} selected (saved)";
        OnPropertyChanged(nameof(SelectionColor));
        NotifyUserActions();
    }
    private CurtainOption? _pendingUserCurtain;
    private bool IsBackgroundApplied(CurtainOption curtain) => curtain.Id == 0
        ? Curtains.All(c => c.CommandStatus == "Closed")
        : curtain.CommandStatus == "Opened"
        && DisplayCurtains.TakeWhile(c => c != curtain).All(c => c.CommandStatus == "Closed");
    public bool IsModeSelectionVisible => Mode == ApplicationMode.Startup;
    public bool IsDeveloperScreenVisible => Mode == ApplicationMode.Developer && !IsSettingsOpen;
    public bool IsUserScreenVisible => Mode == ApplicationMode.User && !IsSettingsOpen;
    public bool IsPhotoboothScreenVisible => IsDeveloperScreenVisible || IsUserScreenVisible;
    public Avalonia.Styling.ThemeVariant PhotoboothTheme => !IsDarkMode ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
    public bool CanChangeMode => !IsMoving && !_disposed && !IsPinDialogOpen && !IsUpdatesOpen && !IsPreparingUpdate;
    public bool CanStart => (Mode is ApplicationMode.User or ApplicationMode.Developer) && CanSelectScene && _razr.Model switch
    {
        RazrModel.RazrII => _pendingCameraPosition is not null,
        RazrModel.RazrIII => _razr3Selection is not null && _razr3Selection.Id != _razr3Scenes.CompletedScene,
        _ => _pendingUserCurtain is not null
    };
    public string UserStatus => !IsRazrI ? Status : _backgrounds is null ? "Controls unavailable. Please ask an operator."
        : IsMoving ? "Changing background…" : _pendingUserCurtain is not null ? "Ready · press START to apply your selection"
        : Status.StartsWith("Relay error", StringComparison.Ordinal) ? "Unable to change background. Please ask an operator."
        : "Choose a color to set your scene";
    public string CameraDisplayName => SelectedCamera is null || SelectedCamera.Id == CameraCatalog.AutomaticCameraId ? "Auto" : SelectedCamera.Name;
    public string UserCameraLabel => IsBackgroundPreviewVisible ? $"Sample · {_previewColorName}" : CameraLive ? "Live Camera Preview" : "Camera preview offline";
    public IBrush CameraIndicator => IsBackgroundPreviewVisible ? Brushes.Coral : CameraLive ? Brushes.MediumSeaGreen : Brushes.Gray;

    partial void OnModeChanged(ApplicationMode value)
    {
        _switchAuthorized = false;
        if (IsPinDialogOpen) CancelPin();
        if (_restoreBackgroundButtonOnModeEntry && IsRazrI && value is ApplicationMode.User or ApplicationMode.Developer)
            RestoreBackgroundButtonSelection();
        OnPropertyChanged(nameof(IsModeSelectionVisible));
        OnPropertyChanged(nameof(ShowCurtainSettingsButton));
        OnPropertyChanged(nameof(IsDeveloperScreenVisible));
        OnPropertyChanged(nameof(IsUserScreenVisible));
        OnPropertyChanged(nameof(IsPhotoboothScreenVisible));
        OnPropertyChanged(nameof(IsRazrIScreenVisible));
        OnPropertyChanged(nameof(IsRazrIIScreenVisible));
        OnPropertyChanged(nameof(IsRazr3ScreenVisible));
        OnPropertyChanged(nameof(IsCurtainPhotoboothScreenVisible));
        OnPropertyChanged(nameof(PhotoboothTheme));
        NotifyUserActions();
    }
    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(UserStatus));
    private void NotifyUserActions()
    {
        NotifyUpdateActions();
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(UserStatus));
        OnPropertyChanged(nameof(CanChangeMode));
        OnPropertyChanged(nameof(CanConfigureRazr));
        OnPropertyChanged(nameof(CanSwitchRazr));
        OnPropertyChanged(nameof(CanEditCameraSettings));
    }
    private void CancelUserDraft()
    {
        ClearBackgroundPreview();
        _pendingCameraPosition = null;
        if (IsRazrII) { RestoreCameraSelection(); NotifyUserActions(); return; }
        if (IsRazr3) return;
        if (_pendingUserCurtain is null) return;
        _pendingUserCurtain = null;
        foreach (var curtain in BackgroundOptions) curtain.IsSelected = false;
        SelectionLabel = "No background selected";
        OnPropertyChanged(nameof(SelectionColor));
        NotifyUserActions();
    }
    [RelayCommand]
    private void EnterDeveloperMode()
    {
        if (!CanChangeMode) return;
        CancelUserDraft();
        IsSettingsOpen = false;
        Mode = ApplicationMode.Developer;
        if (_cameraRequested) StartCamera();
    }
    [RelayCommand]
    private void EnterUserMode()
    {
        if (!CanChangeMode) return;
        CancelUserDraft();
        IsSettingsOpen = false;
        ClearLog();
        Mode = ApplicationMode.User;
        if (_cameraRequested) StartCamera();
    }
    [RelayCommand]
    private void ChooseMode()
    {
        if (!CanChangeMode) return;
        CancelUserDraft();
        IsSettingsOpen = false;
        Mode = ApplicationMode.Startup;
    }
    [RelayCommand]
    private async Task StartBackgroundAsync()
    {
        if (!CanStart) return;
        if (IsRazrII) { await StartCameraPositionAsync(); return; }
        if (IsRazr3) { await StartRazr3Async(_razr3Selection!); return; }
        ClearBackgroundPreview();
        var curtain = _pendingUserCurtain!;
        _pendingUserCurtain = null;
        _selectionRestored = false;
        SelectionLabel = $"{curtain.Name} selected";
        NotifyUserActions();
        await MoveAsync(curtain.Id == 0 ? null : curtain);
        if (!IsBackgroundApplied(curtain)) _pendingUserCurtain = curtain;
        NotifyUserActions();
    }
}
