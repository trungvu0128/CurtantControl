using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CurtantControl.ViewModels;

namespace CurtantControl.Views;

public partial class UserPhotoboothView : UserControl
{
    private readonly CurtantControl.Services.BackgroundPreviewTimer _modeHoldTimer = new(TimeSpan.FromSeconds(3));
    private IPointer? _heldPointer;
    private int _holdVersion;

    public UserPhotoboothView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => CancelModeHold();
    }

    private void CompleteModeHold(int version)
    {
        if (version != _holdVersion) return;
        var canChoose = _heldPointer is not null && IsVisible && ModeHoldArea.IsVisible
            && DataContext is MainViewModel { Mode: ApplicationMode.User, CanChangeMode: true, IsSettingsOpen: false };
        CancelModeHold();
        if (canChoose) ((MainViewModel)DataContext!).ChooseModeCommand.Execute(null);
    }

    private void OnModeHoldPressed(object? sender, PointerPressedEventArgs args)
    {
        if (DataContext is not MainViewModel { Mode: ApplicationMode.User, CanChangeMode: true }
            || !args.GetCurrentPoint(ModeHoldArea).Properties.IsLeftButtonPressed) return;
        CancelModeHold();
        _heldPointer = args.Pointer;
        args.Pointer.Capture(ModeHoldArea);
        var version = _holdVersion;
        _ = _modeHoldTimer.RestartAsync(() => Dispatcher.UIThread.Post(() => CompleteModeHold(version)));
        args.Handled = true;
    }

    private void OnModeHoldReleased(object? sender, PointerReleasedEventArgs args) => CancelModeHold();
    private void OnModeHoldCaptureLost(object? sender, PointerCaptureLostEventArgs args) => CancelModeHold();
    private void OnModeHoldMoved(object? sender, PointerEventArgs args)
    {
        if (_heldPointer is not null && !new Rect(ModeHoldArea.Bounds.Size).Contains(args.GetPosition(ModeHoldArea)))
            CancelModeHold();
    }
    private void CancelModeHold()
    {
        _holdVersion++;
        _modeHoldTimer.Stop();
        var pointer = _heldPointer;
        _heldPointer = null;
        pointer?.Capture(null);
    }
}
