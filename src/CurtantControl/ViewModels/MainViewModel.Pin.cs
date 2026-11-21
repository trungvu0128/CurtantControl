using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private ISwitchPinStore _switchPinStore = null!;
    private string _pin = "";
    private string? _newPin;
    private bool _settingPin;
    private bool _switchAuthorized;
    [ObservableProperty] public partial bool IsPinDialogOpen { get; set; }
    [ObservableProperty] public partial string PinNotice { get; set; } = "";
    public string PinTitle => !_settingPin ? "Switch Razr" : _newPin is null ? "Set Switch PIN" : "Confirm new PIN";
    public string PinHint => _settingPin ? "Use 4–8 digits" : "Enter your PIN to continue";
    public string MaskedPin => _pin.Length == 0 ? "— — — —" : new string('●', _pin.Length);
    partial void OnIsPinDialogOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSelectScene));
        NotifyUserActions();
    }
    private void RefreshPin()
    {
        OnPropertyChanged(nameof(MaskedPin));
        OnPropertyChanged(nameof(PinTitle));
        OnPropertyChanged(nameof(PinHint));
    }
    private void ShowPin(bool setting)
    {
        _switchAuthorized = false;
        _settingPin = setting;
        _newPin = null;
        _pin = "";
        PinNotice = "";
        RefreshPin();
        IsPinDialogOpen = true;
    }
    [RelayCommand] private void EditSwitchPin()
    {
        if (!CanConfigureRazr) return;
        ShowPin(true);
    }
    [RelayCommand] private void PinDigit(string? digit)
    {
        if (!IsPinDialogOpen || string.IsNullOrEmpty(digit) || digit.Length != 1 || digit[0] is < '0' or > '9' || _pin.Length >= 8) return;
        _pin += digit;
        PinNotice = "";
        RefreshPin();
    }
    [RelayCommand] private void PinBackspace()
    {
        if (!IsPinDialogOpen) return;
        if (_pin.Length > 0) _pin = _pin[..^1];
        RefreshPin();
    }
    [RelayCommand] private void CancelPin()
    {
        _pin = ""; _newPin = null; _switchAuthorized = false;
        IsPinDialogOpen = false;
        RefreshPin();
    }
    [RelayCommand] private void ConfirmPin()
    {
        if (!IsPinDialogOpen || IsMoving || _disposed) return;
        if (!SwitchPinStore.IsValid(_pin)) { PinNotice = "Enter 4–8 digits."; return; }
        try
        {
            if (_settingPin)
            {
                if (Mode != ApplicationMode.Developer) { CancelPin(); return; }
                if (_newPin is null) { _newPin = _pin; _pin = ""; RefreshPin(); return; }
                if (_newPin != _pin) { _pin = ""; PinNotice = "PINs do not match. Try again."; RefreshPin(); return; }
                _switchPinStore.Save(_pin);
                CancelPin();
                Status = "Switch Razr PIN saved.";
                return;
            }
            if (!_switchPinStore.Verify(_pin)) { _pin = ""; PinNotice = "Incorrect PIN. Try again."; RefreshPin(); return; }
            _pin = "";
            IsPinDialogOpen = false;
            _switchAuthorized = true;
            OpenRazrChooser();
            RefreshPin();
        }
        catch (Exception) { _pin = ""; PinNotice = "Could not read or save PIN. Please check configuration."; RefreshPin(); }
    }
}
