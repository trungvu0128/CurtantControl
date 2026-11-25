using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;
using CurtantControl.Services.Relays;

namespace CurtantControl.ViewModels;

public partial class MainViewModel
{
    private static readonly CurtainColorPreset[] Razr3DefaultColors =
    [new("white", "White", "#FFFFFF"), new("blue", "Sky Blue", "#60BCD5"), new("green", "Green", "#42AD6A"),
     new("purple", "Purple", "#9580D5"), new("gray", "Gray", "#85858C")];

    private readonly Razr3SceneController _razr3Scenes = new();
    private IRazr3ConfigurationStore _razr3Store = null!;
    private Func<RazrConfiguration, IRazrHardware> _razr3MotorFactory = null!;
    private Razr3Configuration _razr3 = new Razr3Configuration().WithSimulationAngleDefaults();
    private IRazrHardware? _razr3Motor;
    private CurtainRelayController? _razr3ActiveRelays;
    private CurtainRelayController? _razr3SimulationRelays;
    private readonly CurtainCommandState[] _razr3SimulationStates =
        Enumerable.Repeat(CurtainCommandState.Closed, 4).ToArray();
    private CurtainOption? _razr3Selection;
    private bool UsesSeparateRazr3Simulation => _razr3.Simulate && !_settings.SimulateRelays;

    public bool IsRazr3 => _razr.Model == RazrModel.RazrIII;
    public bool IsRazr3ScreenVisible => IsPhotoboothScreenVisible && IsRazr3;
    public string Razr3HardwareSummary => _razr3.Simulate ? "RAZR 3 · SIMULATION" : "RAZR 3 · HARDWARE";
    public IReadOnlyList<CurtainOption> Razr3Backgrounds { get; private set; } = Array.Empty<CurtainOption>();
    public string[] Razr3RelayPairs => Enumerable.Range(1, 4).Select(slot =>
    {
        var motor = CurtainPalette.MapMotor(slot, _palette.ReverseRelayAssignments);
        return $"Curtain {motor} · R{motor * 2 - 1}/R{motor * 2}";
    }).ToArray();
    public CurtainOption Razr3Outside { get; } = new(Razr3Configuration.OutsideScene, "Open landscape", "#315F72");
    public CurtainColorPreset[] Razr3Colors => _palette.Colors.Concat(Razr3DefaultColors)
        .DistinctBy(color => color.Key).ToArray();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Razr3FixedColor), nameof(Razr3M1Color), nameof(Razr3M2Color), nameof(Razr3M3Color), nameof(Razr3M4Color))]
    public partial Razr3Configuration Razr3Draft { get; set; } = new();
    [ObservableProperty] public partial bool IsRazr3SettingsOpen { get; set; }

    public CurtainColorPreset? Razr3FixedColor
    {
        get => FindRazr3Color(Razr3Draft.FixedColor);
        set { if (value is not null) Razr3Draft.FixedColor = value.Key; }
    }
    public CurtainColorPreset? Razr3M1Color
    {
        get => FindRazr3Color(Razr3Draft.M1Color);
        set { if (value is not null) Razr3Draft.M1Color = value.Key; }
    }
    public CurtainColorPreset? Razr3M2Color
    {
        get => FindRazr3Color(Razr3Draft.M2Color);
        set { if (value is not null) Razr3Draft.M2Color = value.Key; }
    }
    public CurtainColorPreset? Razr3M3Color
    {
        get => FindRazr3Color(Razr3Draft.M3Color);
        set { if (value is not null) Razr3Draft.M3Color = value.Key; }
    }
    public CurtainColorPreset? Razr3M4Color
    {
        get => FindRazr3Color(Razr3Draft.M4Color);
        set { if (value is not null) Razr3Draft.M4Color = value.Key; }
    }

    private CurtainColorPreset? FindRazr3Color(string key) => Razr3Colors.FirstOrDefault(color => color.Key == key);

    partial void OnIsRazr3SettingsOpenChanged(bool value) => OnPropertyChanged(nameof(IsCurtainSettingsVisible));

    private void InitializeRazr3(IRazr3ConfigurationStore? store, Func<RazrConfiguration, IRazrHardware>? motorFactory)
    {
        _razr3Store = store ?? new VolatileRazr3Store();
        _razr3MotorFactory = motorFactory ?? (settings => new RazrHardware(settings));
        try
        {
            var saved = _razr3Store.Load().WithSimulationAngleDefaults();
            saved.Validate(_settings, Razr3Colors);
            _razr3 = saved;
        }
        catch (Exception error) { AddLog($"RAZR 3 profile could not be loaded · using simulation: {error.Message}"); }
        RebuildRazr3Backgrounds();
        if (IsRazr3) Status = "Choose a scene";
    }

    private void RebuildRazr3Backgrounds()
    {
        var colors = Razr3Colors;
        Razr3Backgrounds = _razr3.GetColorKeys().Select((key, id) =>
        {
            var preset = colors.First(color => color.Key == key);
            return new CurtainOption(id, preset.Name, preset.Color) { SelectCommand = SelectRazr3SceneCommand };
        }).ToArray();
        Razr3Outside.SelectCommand = SelectRazr3SceneCommand;
        SetRazr3Selection(Razr3Backgrounds.Append(Razr3Outside).FirstOrDefault(scene => scene.Id == _razr3.SelectedSceneId));
        OnPropertyChanged(nameof(Razr3Backgrounds));
        OnPropertyChanged(nameof(PhotoboothBackgrounds));
        OnPropertyChanged(nameof(SceneOptions));
    }

    private void SetRazr3Selection(CurtainOption? scene)
    {
        _razr3Selection = scene;
        foreach (var item in Razr3Backgrounds.Append(Razr3Outside)) item.IsSelected = item == scene;
        if (IsRazr3)
        {
            SelectionLabel = scene is null ? "Choose a scene" : $"{scene.Name} selected";
            OnPropertyChanged(nameof(SelectionColor));
            NotifyUserActions();
        }
    }

    [RelayCommand]
    private void SelectRazr3Scene(CurtainOption? scene)
    {
        if (!IsRazr3 || !CanSelectScene || scene is null ||
            (scene != Razr3Outside && !Razr3Backgrounds.Contains(scene))) return;
        SetRazr3Selection(scene);
        var updated = _razr3 with { SelectedSceneId = scene.Id };
        try
        {
            _razr3Store.Save(updated);
            Status = _razr3Scenes.CompletedScene == scene.Id ? "Ready" : "Press Start to apply your selection";
        }
        catch (Exception error)
        {
            Status = "Selection kept for this session · could not save";
            AddLog($"RAZR 3 selection could not be saved: {error.Message}");
        }
        _razr3 = updated;
    }

    private void OpenRazr3Settings()
    {
        if (!IsRazr3 || !CanConfigureRazr || IsSettingsOpen) return;
        OnPropertyChanged(nameof(Razr3Colors));
        Razr3Draft = _razr3 with { };
        RazrNotice = "";
        IsRazr3SettingsOpen = true;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void SaveRazr3Settings()
    {
        if (!IsRazr3 || !CanConfigureRazr || !IsRazr3SettingsOpen) return;
        try
        {
            var updated = Razr3Draft with { SelectedSceneId = _razr3.SelectedSceneId };
            updated.Validate(_settings, Razr3Colors);
            _razr3Store.Save(updated);
            var motorChanged = !SameRazr3MotorSettings(updated.MotorSettings(), _razr3.MotorSettings());
            var colorsChanged = !updated.GetColorKeys().SequenceEqual(_razr3.GetColorKeys());
            _razr3 = updated;
            if (motorChanged) ResetRazr3Motor();
            else if (colorsChanged) _razr3Scenes.InvalidateScene();
            RebuildRazr3Backgrounds();
            Status = _razr3Selection is null ? "Choose a scene"
                : _razr3Scenes.CompletedScene == _razr3Selection.Id ? "Ready" : "Press Start to apply your selection";
            OnPropertyChanged(nameof(Razr3HardwareSummary));
            OnPropertyChanged(nameof(HardwareSummary));
            RazrNotice = "RAZR 3 settings saved";
        }
        catch (Exception error) { RazrNotice = $"Could not save: {error.Message}"; }
    }

    private static bool SameRazr3MotorSettings(RazrConfiguration current, RazrConfiguration previous) =>
        current.Position1Angle == previous.Position1Angle && current.Position2Angle == previous.Position2Angle &&
        current.SettleSeconds == previous.SettleSeconds && current.MinimumAngle == previous.MinimumAngle &&
        current.MaximumAngle == previous.MaximumAngle && current.FullScaleAngle == previous.FullScaleAngle &&
        current.DacReferenceVolts == previous.DacReferenceVolts && current.I2cBus == previous.I2cBus &&
        current.DacAddress == previous.DacAddress && current.SimulateMotor == previous.SimulateMotor &&
        current.CalibrationConfirmed == previous.CalibrationConfirmed;

    private void ResetRazr3Motor()
    {
        _razr3Scenes.Reset();
        try { _razr3Motor?.Dispose(); }
        catch (Exception error) { AddLog($"RAZR 3 motor release failed: {error.Message}"); }
        finally { _razr3Motor = null; }
    }

    private async Task StartRazr3Async(CurtainOption scene)
    {
        using var movement = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _movement = movement;
        IsMoving = true;
        try
        {
            var motor = _razr3.MotorSettings();
            await _razr3Scenes.ApplyAsync(scene.Id, motor, (angle, token) => MoveRazr3CameraAsync(motor, angle, token),
                ApplyRazr3BackgroundAsync, stage => ReportRazr3Stage(stage, scene), movement.Token);
            Status = "Ready";
            AddLog($"RAZR 3 · {scene.Name} · {(_razr3.Simulate ? "simulation complete" : "command sequence complete")}");
        }
        catch (OperationCanceledException)
        {
            Status = "RAZR 3 stopped";
            AddLog(Status);
        }
        catch (Exception error)
        {
            Status = $"RAZR 3 failed · {Status}: {error.Message}";
            AddLog(Status);
        }
        finally
        {
            _razr3ActiveRelays = null;
            _movement = null;
            IsMoving = false;
        }
    }

    private Task MoveRazr3CameraAsync(RazrConfiguration motor, double angle, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!motor.SimulateMotor && _razr.LastCameraPosition is { Completed: true } last &&
            last.ExecutionProfile?.StartsWith("Relays:", StringComparison.Ordinal) != true)
        {
            var updated = _razr.Copy();
            updated.LastCameraPosition = last with { Completed = false };
            _razrStore.Save(updated);
            _razr = updated;
        }
        token.ThrowIfCancellationRequested();
        _razr3Motor ??= _razr3MotorFactory(motor);
        return _razr3Motor.MoveCameraAsync(angle, token);
    }

    private Task ApplyRazr3BackgroundAsync(int slot, CancellationToken token)
    {
        var motor = CurtainPalette.MapMotor(slot, _palette.ReverseRelayAssignments);
        var name = Razr3Backgrounds[slot].Name;
        return GetRazr3BackgroundController().ApplyAsync(motor, UsesPaperRelaySequence, token, ReportRazr3Curtain,
            relay => AddLog($"RAZR 3 · {name} · sending R{relay} pulse"));
    }

    private BackgroundController GetRazr3BackgroundController()
    {
        if (UsesSeparateRazr3Simulation)
        {
            _razr3SimulationRelays ??= new CurtainRelayController(new SimulatedRelayOutput(),
                TimeSpan.FromMilliseconds(_settings.MaximumActivationMilliseconds),
                TimeSpan.FromMilliseconds(_settings.BreakBeforeMakeMilliseconds));
            _razr3ActiveRelays = _razr3SimulationRelays;
            return new BackgroundController(_razr3SimulationRelays, _settings.CurtainOrderFrontToBack,
                TimeSpan.FromMilliseconds(_settings.ActivationMilliseconds), id => _razr3SimulationStates[id - 1]);
        }
        if (_backgrounds is null) InitializeCurtainRelays(_relays, diagnose: false);
        if (_backgrounds is null) throw new InvalidOperationException(_relaySetupError ?? "Curtain controls are unavailable.");
        _razr3ActiveRelays = _relays;
        return _backgrounds;
    }

    private void ReportRazr3Stage(Razr3Stage stage, CurtainOption scene)
    {
        Status = stage switch
        {
            Razr3Stage.MovingCamera => scene == Razr3Outside ? "Turning toward Outside" : "Turning toward Room 1",
            Razr3Stage.Settling => "Waiting for the camera to settle",
            _ => $"Changing background · {scene.Name}"
        };
        AddLog($"RAZR 3 · {Status}");
    }

    private void ReportRazr3Curtain(CurtainCommandUpdate update)
    {
        var slot = CurtainPalette.MapMotor(update.Curtain, _palette.ReverseRelayAssignments);
        var name = Razr3Backgrounds[slot].Name;
        if (UsesSeparateRazr3Simulation)
        {
            _razr3SimulationStates[update.Curtain - 1] = update.State;
            AddLog($"RAZR 3 simulation · {name} · {update.State}");
        }
        else ReportCurtain(update, name);
    }

    private sealed class VolatileRazr3Store : IRazr3ConfigurationStore
    {
        private Razr3Configuration _value = new();
        public Razr3Configuration Load() => _value with { };
        public void Save(Razr3Configuration configuration) => _value = configuration with { };
    }
}
