using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private IRazrConfigurationStore _razrStore = null!;
    private RazrConfiguration _razr = new();
    private IRazrHardware _razrHardware = null!;
    private Func<RazrConfiguration, IRazrHardware> _razrHardwareFactory = null!;
    private CurtainOption? _pendingCameraPosition;
    private int? _selectedCameraButtonId;
    private DateTimeOffset _selectedCameraButtonSavedAt;
    private ICameraButtonSelectionStore? _cameraButtonSelectionStore;
    private Dictionary<string, string> _cameraColorKeys = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<CurtainColorPreset> CameraPositionChoices { get; private set; } = Array.Empty<CurtainColorPreset>();
    public CurtainColorPreset? CameraPosition1Choice
    {
        get => CameraPositionChoices.FirstOrDefault(c => c.Name == RazrDraft.Position1Name);
        set { if (value is null) return; RazrDraft.Position1Name = value.Name; OnPropertyChanged(nameof(CameraPosition1Choice)); }
    }
    public CurtainColorPreset? CameraPosition2Choice
    {
        get => CameraPositionChoices.FirstOrDefault(c => c.Name == RazrDraft.Position2Name);
        set { if (value is null) return; RazrDraft.Position2Name = value.Name; OnPropertyChanged(nameof(CameraPosition2Choice)); }
    }
    private string[] _cameraPositionPresets = Array.Empty<string>();
    public IReadOnlyList<string> CameraPositionNames => _cameraPositionPresets
        .Concat(new[] { RazrDraft.Position1Name, RazrDraft.Position2Name })
        .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).ToArray();
    partial void OnRazrDraftChanged(RazrConfiguration value) { RefreshCameraChoices(); OnPropertyChanged(nameof(SelectedControlMode)); }
    public IReadOnlyList<RazrModel> RazrModels { get; } = Enum.GetValues<RazrModel>();
    public IReadOnlyList<CurtainOption> CameraPositions { get; private set; } = Array.Empty<CurtainOption>();
    public IReadOnlyList<CurtainOption> SceneOptions => IsRazrII ? CameraPositions : BackgroundOptions;
    public int SceneRows => IsRazrII ? 2 : 5;
    public bool ShowCurtainSettingsButton => Mode == ApplicationMode.Developer;
    public bool IsRazrII => _razr.Model == RazrModel.RazrII;
    public string RazrTitle => IsRazrII ? "CAMERA RAZR II" : "CAMERA RAZR I";
    public bool CanSelectScene => !_disposed && !IsMoving && !IsSettingsOpen && !IsPinDialogOpen && (IsRazrII || _backgrounds is not null);
    public bool IsCurtainSettingsVisible => IsSettingsOpen && !IsRazrSettingsOpen && !IsModelSelectionOpen;
    public bool IsRazrIScreenVisible => IsPhotoboothScreenVisible && !IsRazrII;
    public bool IsRazrIIScreenVisible => IsPhotoboothScreenVisible && IsRazrII;
    [ObservableProperty] public partial bool IsModelSelectionOpen { get; set; }
    public string SceneHeading => IsRazrII ? "CAMERA POSITIONS" : "BACKGROUND 1 → 5";
    public string HardwareSummary => IsRazrII
        ? $"{ControlModeChoices.First(c => c.Mode == _razr.ControlMode).Name} · {(IsSimulatedCameraCommand ? "SIMULATION" : "HARDWARE")}" : RelayMode;
    public sealed record ControlModeChoice(RazrControlMode Mode, string Name);
    public IReadOnlyList<ControlModeChoice> ControlModeChoices { get; } = new[]
    {
        new ControlModeChoice(RazrControlMode.Motor, "Motor"),
        new ControlModeChoice(RazrControlMode.Relays, "Relay 1 / Relay 2"),
        new ControlModeChoice(RazrControlMode.MotorAndRelays, "Motor + Relay (parallel)")
    };
    public ControlModeChoice? SelectedControlMode
    {
        get => ControlModeChoices.FirstOrDefault(c => c.Mode == RazrDraft.ControlMode);
        set { if (value is not null) { RazrDraft.ControlMode = value.Mode; OnPropertyChanged(); } }
    }
    public string RazrRelaySummary => $"Position 1 → R1 · Position 2 → R2 · {_settings.ActivationMilliseconds} ms pulse · {RelayMode}";
    private bool IsSimulatedCameraCommand => (!_razr.UsesMotor || _razr.SimulateMotor)
        && (!_razr.UsesRelays || _settings.SimulateRelays);
    private string? CameraExecutionProfile => !_razr.UsesRelays ? null
        : $"{_razr.ControlMode}:{_razr.SimulateMotor}:{_settings.SimulateRelays}:{_settings.ActiveLow}:" +
          $"{string.Join(",", _settings.BcmPinsByChannel)}:{_settings.ActivationMilliseconds}:{_settings.BreakBeforeMakeMilliseconds}";
    [ObservableProperty] public partial RazrConfiguration RazrDraft { get; set; } = new();
    [ObservableProperty] public partial bool IsRazrSettingsOpen { get; set; }
    [ObservableProperty] public partial string RazrNotice { get; set; } = "";
    public bool CanSwitchRazr => (Mode is ApplicationMode.User or ApplicationMode.Developer) && CanChangeMode;
    public bool CanEditCameraSettings => CanConfigureRazr && IsRazrII;
    public bool CanConfigureRazr => Mode == ApplicationMode.Developer && CanChangeMode;

    private void InitializeRazr(IRazrConfigurationStore? store, Func<RazrConfiguration, IRazrHardware>? factory,
        ICameraButtonSelectionStore? selectionStore)
    {
        // Tests/design previews opt out of disk state; application composition supplies the store.
        _razrStore = store ?? new VolatileRazrStore();
        ReloadCameraNames();
        _razrHardwareFactory = factory ?? (c => new RazrPositionHardware(c, _settings));
        _cameraButtonSelectionStore = selectionStore;
        try { _razr = _razrStore.Load(); }
        catch (Exception error) { AddLog($"Could not load Razr configuration: {error.Message}"); }
        _selectedCameraButtonId = ReadLegacyCameraSelection();
        _selectedCameraButtonSavedAt = _razr.SelectedCameraButtonSavedAt;
        try
        {
            var independent = selectionStore?.Load();
            if (independent is { Id: 101 or 102 } &&
                (_selectedCameraButtonId is null || independent.SavedAtUtc >= _selectedCameraButtonSavedAt))
            {
                _selectedCameraButtonId = independent.Id;
                _selectedCameraButtonSavedAt = independent.SavedAtUtc;
            }
        }
        catch (Exception error) { AddLog($"Could not load camera button selection; using configuration history: {error.Message}"); }
        try { _razr.Validate(_settings); }
        catch (Exception error)
        {
            // Hardware errors must not discard the UI selection or switch to a different controller.
            var model = Enum.IsDefined(_razr.Model) ? _razr.Model : RazrModel.RazrI;
            _razr = new() { Model = model, SelectedCameraButtonId = _selectedCameraButtonId, SelectedCameraButtonSavedAt = _selectedCameraButtonSavedAt };
            AddLog($"Razr configuration invalid · simulated motor defaults: {error.Message}");
        }
        _razrHardware = _razrHardwareFactory(_razr.Copy());
        RebuildCameraPositions();
        if (IsRazrII) Status = "Choose a camera position";
    }

    private int? ReadLegacyCameraSelection()
    {
        var ids = new[] { _razr.SelectedCameraButtonId,
            IsSimulatedCameraCommand ? _razr.SelectedSimulatedCameraPositionId : _razr.SelectedCameraPositionId,
            IsSimulatedCameraCommand ? _razr.SelectedCameraPositionId : _razr.SelectedSimulatedCameraPositionId,
            IsSimulatedCameraCommand ? _razr.LastSimulatedCameraPosition?.Id : _razr.LastCameraPosition?.Id,
            IsSimulatedCameraCommand ? _razr.LastCameraPosition?.Id : _razr.LastSimulatedCameraPosition?.Id };
        return ids.FirstOrDefault(id => id is 101 or 102);
    }

    private bool PersistCameraSelection(int id)
    {
        _selectedCameraButtonId = id;
        var now = DateTimeOffset.UtcNow;
        _selectedCameraButtonSavedAt = now > _selectedCameraButtonSavedAt ? now : _selectedCameraButtonSavedAt.AddTicks(1);
        var updated = _razr.Copy();
        updated.SelectedCameraButtonId = id;
        updated.SelectedCameraButtonSavedAt = _selectedCameraButtonSavedAt;
        if (IsSimulatedCameraCommand) updated.SelectedSimulatedCameraPositionId = id;
        else updated.SelectedCameraPositionId = id;
        _razr = updated;
        var saved = false;
        try { _razrStore.Save(updated); saved = true; }
        catch (Exception error) { AddLog($"Could not save selected camera button to configuration: {error.Message}"); }
        if (_cameraButtonSelectionStore is not null)
        {
            try { _cameraButtonSelectionStore.Save(new(id, _selectedCameraButtonSavedAt)); saved = true; }
            catch (Exception error) { AddLog($"Could not save independent camera button selection: {error.Message}"); }
        }
        return saved;
    }

    private void ReloadCameraNames()
    {
        try
        {
            var options = CameraPositionNameCatalog.LoadOptions();
            _cameraPositionPresets = options.Select(o => o.Name).ToArray();
            _cameraColorKeys = options.ToDictionary(o => o.Name, o => o.Color, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) { AddLog($"Could not load razr2-options.json; keeping current choices: {error.Message}"); }
        RefreshCameraChoices();
    }

    private string ResolveCameraColor(string name)
    {
        static string Normalize(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var key = _cameraColorKeys.TryGetValue(name, out var mapped) ? mapped : name;
        if (System.Text.RegularExpressions.Regex.IsMatch(key, "^#[0-9a-fA-F]{6}$")) return key;
        var preset = _palette.Colors.FirstOrDefault(c => Normalize(c.Key) == Normalize(key) || Normalize(c.Name) == Normalize(key));
        return preset?.Color ?? "#85858C";
    }

    private void RefreshCameraChoices()
    {
        CameraPositionChoices = CameraPositionNames.Select(name => new CurtainColorPreset(name, name, ResolveCameraColor(name))).ToArray();
        OnPropertyChanged(nameof(CameraPositionNames));
        OnPropertyChanged(nameof(CameraPositionChoices));
        OnPropertyChanged(nameof(CameraPosition1Choice));
        OnPropertyChanged(nameof(CameraPosition2Choice));
    }

    private void RebuildCameraPositions()
    {
        CameraPositions = !IsRazrII ? Array.Empty<CurtainOption>() : new[]
        {
            new CurtainOption(101, _razr.Position1Name, ResolveCameraColor(_razr.Position1Name)) { SelectCommand = SelectCameraPositionCommand },
            new CurtainOption(102, _razr.Position2Name, ResolveCameraColor(_razr.Position2Name)) { SelectCommand = SelectCameraPositionCommand }
        };
        if (IsRazrII) RestoreCameraSelection();
        OnPropertyChanged(nameof(SelectionColor));
        OnPropertyChanged(nameof(CameraPositions));
        foreach (var property in new[] { nameof(SceneOptions), nameof(SceneRows), nameof(IsRazrII), nameof(RazrTitle), nameof(SceneHeading), nameof(HardwareSummary), nameof(CanSelectScene), nameof(ShowCurtainSettingsButton), nameof(IsRazrIScreenVisible), nameof(IsRazrIIScreenVisible) })
            OnPropertyChanged(property);
    }

    partial void OnIsRazrSettingsOpenChanged(bool value) => OnPropertyChanged(nameof(IsCurtainSettingsVisible));
    partial void OnIsModelSelectionOpenChanged(bool value) => OnPropertyChanged(nameof(IsCurtainSettingsVisible));

    [RelayCommand]
    private void OpenRazrSettings()
    {
        if (!CanSwitchRazr) return;
        if (Mode == ApplicationMode.User) { ShowPin(false); return; }
        OpenRazrChooser();
    }

    private void OpenRazrChooser()
    {
        CancelUserDraft();
        _pendingCameraPosition = null;
        RazrNotice = "Select the curtain controller or the camera-angle controller.";
        IsModelSelectionOpen = true;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CancelRazrSettings()
    {
        _switchAuthorized = false;
        IsRazrSettingsOpen = false;
        IsModelSelectionOpen = false;
        IsSettingsOpen = false;
        if (IsRazrII) RestoreCameraSelection();
        NotifyUserActions();
    }

    [RelayCommand]
    private void SaveRazrSettings()
    {
        if (!CanEditCameraSettings || !IsRazrSettingsOpen) return;
        try
        {
            var updated = Mode == ApplicationMode.Developer ? RazrDraft.Copy() : _razr.Copy();
            updated.Position1Name = RazrDraft.Position1Name;
            updated.Position2Name = RazrDraft.Position2Name;
            updated.Position1Angle = RazrDraft.Position1Angle;
            updated.Position2Angle = RazrDraft.Position2Angle;
            updated.SettleSeconds = RazrDraft.SettleSeconds;
            // Settings drafts must never roll back selection or command history.
            updated.LastCameraPosition = _razr.LastCameraPosition;
            updated.LastSimulatedCameraPosition = _razr.LastSimulatedCameraPosition;
            updated.SelectedBackgroundId = _razr.SelectedBackgroundId;
            updated.SelectedSimulatedBackgroundId = _razr.SelectedSimulatedBackgroundId;
            updated.SelectedCameraPositionId = _razr.SelectedCameraPositionId;
            updated.SelectedSimulatedCameraPositionId = _razr.SelectedSimulatedCameraPositionId;
            updated.SelectedCameraButtonId = _selectedCameraButtonId;
            updated.SelectedCameraButtonSavedAt = _selectedCameraButtonSavedAt;
            var chosen = _selectedCameraButtonId;
            if (chosen is not null)
            {
                var simulated = (!updated.UsesMotor || updated.SimulateMotor)
                    && (!updated.UsesRelays || _settings.SimulateRelays);
                if (simulated) updated.SelectedSimulatedCameraPositionId = chosen;
                else updated.SelectedCameraPositionId = chosen;
            }
            updated.Model = RazrModel.RazrII;
            updated.Position1Name = updated.Position1Name.Trim();
            updated.Position2Name = updated.Position2Name.Trim();
            updated.Validate(_settings);
            var nextHardware = _razrHardwareFactory(updated.Copy());
            try { _razrStore.Save(updated); }
            catch { nextHardware.Dispose(); throw; }
            // Persistence succeeded. Apply the saved configuration even if old-output release reports a fault.
            string? releaseError = null;
            try { _razrHardware.Dispose(); } catch (Exception error) { releaseError = error.Message; }
            _razrHardware = nextHardware;
            _razr = updated;
            _pendingCameraPosition = null;
            CancelUserDraft();
            RebuildCameraPositions();
            NotifyUserActions();
            RazrNotice = releaseError is null ? "Camera settings saved. No movement." : $"Saved, but old output release failed: {releaseError}. Inspect hardware before use.";
        }
        catch (Exception error) { RazrNotice = $"Could not save: {error.Message}"; }
    }

    [RelayCommand]
    private void SelectCameraPosition(CurtainOption? option)
    {
        if (!IsRazrII || !CanSelectScene || option is null || !CameraPositions.Contains(option)) return;
        var persisted = PersistCameraSelection(option.Id);
        foreach (var position in CameraPositions) position.IsSelected = position == option;
        _pendingCameraPosition = IsCameraPositionApplied(option) ? null : option;
        SelectionLabel = $"{option.Name} selected" + (_pendingCameraPosition is null ? "" : " · press STATE")
            + (persisted ? "" : " · could not save; selection kept for this session");
        NotifyUserActions();
    }

    private async Task StartCameraPositionAsync()
    {
        var position = _pendingCameraPosition;
        if (position is null || !CanSelectScene) return;
        IsMoving = true;
        using var movement = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _movement = movement;
        try
        {
            SaveCameraPosition(position, completed: false);
            if (_razr.UsesRelays) ReportCurtain(new(1, CurtainCommandState.Opening));
            position.CommandStatus = "Moving";
            Status = $"Moving camera · {position.Name}…";
            await _razrHardware.MovePositionAsync(position.Id == 101 ? 1 : 2,
                position.Id == 101 ? _razr.Position1Angle : _razr.Position2Angle, movement.Token);
            await Task.Delay(TimeSpan.FromSeconds(_razr.SettleSeconds), movement.Token);
            SaveCameraPosition(position, completed: true);
            position.CommandStatus = IsSimulatedCameraCommand ? "Simulated position" : (_razr.UsesRelays ? "Command complete" : "Command sent");
            Status = $"{position.Name} · {position.CommandStatus}";
            _pendingCameraPosition = null;
            SelectionLabel = Status;
        }
        catch (OperationCanceledException) { position.CommandStatus = "Unknown"; Status = "Camera command cancelled · physical position unverified"; }
        catch (Exception error) { position.CommandStatus = "Unknown"; Status = $"Camera movement failed: {error.Message}"; }
        finally { _movement = null; IsMoving = false; SelectionLabel = Status; NotifyUserActions(); AddLog(Status); }
    }

    private void SaveCameraPosition(CurtainOption position, bool completed)
    {
        var snapshot = new CameraPositionSnapshot(position.Id,
            position.Id == 101 ? _razr.Position1Angle : _razr.Position2Angle, _razr.FullScaleAngle, completed, CameraExecutionProfile);
        var updated = _razr.Copy();
        updated.SelectedCameraButtonId = position.Id;
        updated.SelectedCameraButtonSavedAt = _selectedCameraButtonSavedAt;
        _selectedCameraButtonId = position.Id;
        if (IsSimulatedCameraCommand) updated.LastSimulatedCameraPosition = snapshot;
        else updated.LastCameraPosition = snapshot;
        _razrStore.Save(updated);
        _razr = updated;
    }

    private bool IsCameraPositionApplied(CurtainOption option)
    {
        var saved = IsSimulatedCameraCommand ? _razr.LastSimulatedCameraPosition : _razr.LastCameraPosition;
        return saved is { Completed: true } && saved.Id == option.Id && saved.ExecutionProfile == CameraExecutionProfile
            && saved.FullScaleAngle == _razr.FullScaleAngle && double.IsFinite(saved.Angle)
            && saved.Angle == (option.Id == 101 ? _razr.Position1Angle : _razr.Position2Angle);
    }

    private void RestoreCameraSelection()
    {
        _pendingCameraPosition = null;
        foreach (var position in CameraPositions) position.IsSelected = false;
        SelectionLabel = "Choose a camera position";
        var chosenId = _selectedCameraButtonId ?? ReadLegacyCameraSelection();
        if (chosenId is 101 or 102) _selectedCameraButtonId = chosenId;
        var option = CameraPositions.FirstOrDefault(p => p.Id == chosenId);
        if (option is not null)
        {
            option.IsSelected = true;
            var applied = IsCameraPositionApplied(option);
            option.CommandStatus = applied
                ? (IsSimulatedCameraCommand ? "Simulated position (saved)" : (_razr.UsesRelays ? "Command complete (saved)" : "Command sent (saved)")) : "Unknown";
            _pendingCameraPosition = applied ? null : option;
            SelectionLabel = $"{option.Name} selected (saved)" + (applied ? "" : " · press STATE");
        }
        OnPropertyChanged(nameof(SelectionColor));
    }

    [RelayCommand]
    private void OpenCameraSettings()
    {
        if (!CanEditCameraSettings || Mode != ApplicationMode.Developer) return;
        CancelUserDraft();
        ReloadCameraNames();
        RazrDraft = _razr.Copy();
        RazrNotice = "";
        IsModelSelectionOpen = false;
        IsRazrSettingsOpen = true;
        IsSettingsOpen = true;
    }

    [RelayCommand] private void EnterRazrI() => SelectRazrModel(RazrModel.RazrI);
    [RelayCommand] private void EnterRazrII() => SelectRazrModel(RazrModel.RazrII);

    private void SelectRazrModel(RazrModel model)
    {
        if (!CanSwitchRazr || !IsModelSelectionOpen || (Mode == ApplicationMode.User && !_switchAuthorized)) return;
        try
        {
            var updated = _razr.Copy();
            updated.Model = model;
            updated.Validate(_settings);
            var nextHardware = _razrHardwareFactory(updated.Copy());
            try { _razrStore.Save(updated); }
            catch { nextHardware.Dispose(); throw; }
            try { _razrHardware.Dispose(); } catch (Exception error) { AddLog($"Camera controller release failed: {error.Message}"); }
            _razrHardware = nextHardware;
            CancelUserDraft();
            _razr = updated;
            if (IsRazrII)
            {
                try { _relays?.Dispose(); }
                catch (Exception error) { AddLog($"Curtain output release failed: {error.Message}"); }
                finally { _relays = null; _backgrounds = null; }
            }
            else if (_relays is null) InitializeCurtainRelays();
            RebuildCameraPositions();
            if (!IsRazrII) { RestoreSavedSelection(); RestoreBackgroundButtonSelection(); Status = "Choose your background"; }
            else Status = "Choose a camera position";
            CancelRazrSettings();
            NotifyUserActions();
        }
        catch (Exception error) { RazrNotice = $"Could not switch model: {error.Message}"; }
    }

    private sealed class VolatileRazrStore : IRazrConfigurationStore
    {
        private RazrConfiguration _value = new();
        public RazrConfiguration Load() => _value.Copy();
        public void Save(RazrConfiguration configuration) => _value = configuration.Copy();
    }
}
