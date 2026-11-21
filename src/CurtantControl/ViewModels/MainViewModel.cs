using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurtantControl.Services;
using CurtantControl.Services.Camera;
using CurtantControl.Services.Relays;

namespace CurtantControl.ViewModels;

public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly PhotoboothSettings _settings;
    private CurtainRelayController? _relays;
    private string? _relaySetupError;
    private BackgroundController? _backgrounds;
    private readonly IThemePreferenceStore _themeStore;
    private readonly ICurtainPaletteStore _paletteStore;
    private CurtainPalette _palette = new();
    private readonly ICurtainCommandStateStore? _commandStateStore;
    private readonly string[] _lastCommandStates = ["Unknown", "Unknown", "Unknown", "Unknown"];
    private bool _selectionRestored;
    private bool _themeReady;
    [ObservableProperty] public partial bool IsDarkMode { get; set; } = true;
    public CurtainSettingsViewModel CurtainSettings { get; }
    [ObservableProperty] public partial bool IsSettingsOpen { get; set; }
    public bool IsMainScreenVisible => !IsSettingsOpen;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _movement;
    private Task? _cameraTask;
    private Task? _discoveryTask;
    private Task? _cameraSwitchTask;
    private CancellationTokenSource? _cameraCancellation;
    private readonly SemaphoreSlim _cameraGate = new(1, 1);
    private readonly ICameraCatalog _cameraCatalog;
    private readonly Func<string, ICameraPreview> _cameraFactory;
    private bool _refreshingCameras;
    private bool _scanningCameras;
    private string? _activeCameraId;
    public ObservableCollection<CameraDevice> Cameras { get; } = new();
    [ObservableProperty] public partial CameraDevice? SelectedCamera { get; set; }
    private bool _disposed;
    private readonly object _logSync = new();
    public ObservableCollection<string> ActivityLog { get; } = new();

    public ObservableCollection<CurtainOption> Curtains { get; } = new();
    public IReadOnlyList<CurtainOption> DisplayCurtains { get; private set; }
    public CurtainOption FixedWhiteBackground { get; } = new(0, "White", "#FFFFFF", "Backgrounds/white.png");
    public IReadOnlyList<CurtainOption> BackgroundOptions { get; private set; } = Array.Empty<CurtainOption>();

    [ObservableProperty] public partial string Status { get; set; } = "Choose your background";
    [ObservableProperty] public partial string SelectionLabel { get; set; } = "No background selected";
    public IBrush SelectionColor => SceneOptions.FirstOrDefault(c => c.IsSelected)?.Color ?? Brushes.Gray;
    [ObservableProperty] public partial string CameraStatus { get; set; } = "Camera is offline";
    [ObservableProperty] public partial bool CameraLive { get; set; }
    [ObservableProperty] public partial WriteableBitmap? CameraFrame { get; set; }
    [ObservableProperty] public partial bool IsMoving { get; set; }
    public bool CameraOffline => !CameraLive;
    public string RelayMode => _settings.SimulateRelays ? "SIMULATED RELAYS" : "GPIO RELAYS";
    public bool CanSelect => _backgrounds is not null && !IsMoving && !IsSettingsOpen;

    public MainViewModel() : this(new PhotoboothSettings()) { }

    public MainViewModel(PhotoboothSettings settings, CurtainRelayController? relayController = null,
        ICurtainOrderStore? orderStore = null, ICameraCatalog? cameraCatalog = null,
        Func<string, ICameraPreview>? cameraFactory = null, IThemePreferenceStore? themeStore = null,
        ICurtainPaletteStore? paletteStore = null, ICurtainCommandStateStore? commandStateStore = null,
        IRazrConfigurationStore? razrStore = null, Func<RazrConfiguration, IRazrHardware>? razrHardwareFactory = null,
        ICameraButtonSelectionStore? cameraButtonSelectionStore = null, ISwitchPinStore? switchPinStore = null,
        IAppUpdateService? updateService = null)
    {
        _settings = settings;
        _updates = updateService ?? new NoAppUpdates();
        _switchPinStore = switchPinStore ?? new SwitchPinStore(inMemory: true);
        _commandStateStore = commandStateStore;
        _paletteStore = paletteStore ?? new CurtainPaletteStore();
        string? paletteError = null;
        try { _palette = _paletteStore.Load(); _palette.Validate(); }
        catch (Exception error) { _palette = new CurtainPalette(); paletteError = error.Message; }
        if (_palette.RelayOrderFrontToBack is { } savedLayerOrder)
            settings.CurtainOrderFrontToBack = (int[])savedLayerOrder.Clone();
        var fixedPreset = _palette.GetFixedBackground();
        FixedWhiteBackground.Name = fixedPreset.Name;
        FixedWhiteBackground.Color = Brush.Parse(fixedPreset.Color);
        FixedWhiteBackground.PreviewImage = fixedPreset.PreviewImage;
        for (var id = 1; id <= 4; id++)
        {
            var preset = _palette.Colors.Single(c => c.Key == _palette.Slots[id - 1]);
            Curtains.Add(new(id, preset.Name, preset.Color, preset.PreviewImage));
        }
        foreach (var curtain in Curtains) curtain.PropertyChanged += OnCurtainStatusChanged;
        UpdateFixedBackgroundStatus();
        var order = settings.CurtainOrderFrontToBack;
        DisplayCurtains = order is { Length: 4 } && order.Order().SequenceEqual(new[] { 1, 2, 3, 4 })
            ? order.Select(id => Curtains.Single(c => c.Id == id)).ToArray()
            : Curtains.ToArray();
        for (var index = 0; index < DisplayCurtains.Count; index++)
            DisplayCurtains[index].DisplayLevel = index + 1;
        FixedWhiteBackground.DisplayLevel = 5;
        BackgroundOptions = new[] { FixedWhiteBackground }.Concat(Curtains).ToArray();
        _themeStore = themeStore ?? new ThemePreferenceStore();
        // The kiosk now always starts dark, including installations with an old light preference.
        IsDarkMode = true;
        ApplyTheme();
        _themeReady = true;
        var catalog = new CameraCatalog(settings.FfmpegPath, settings.CameraStreamUrl, automatic: true);
        _cameraCatalog = cameraCatalog ?? catalog;
        _cameraFactory = cameraFactory ?? (device => device == CameraCatalog.AutomaticCameraId
            ? new AutomaticCameraPreview(catalog.ListCandidatesAsync,
                id => new FfmpegCameraPreview(settings.FfmpegPath, id, settings.EnablePythonCameraActivation), message =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_disposed) return;
                        CameraStatus = message;
                        if (!message.StartsWith("Connected", StringComparison.Ordinal))
                        {
                            CameraLive = false;
                            var previous = CameraFrame;
                            CameraFrame = null;
                            previous?.Dispose();
                        }
                        AddLog(message);
                    }))
            : new FfmpegCameraPreview(settings.FfmpegPath, device, settings.EnablePythonCameraActivation));
        CurtainSettings = new CurtainSettingsViewModel(SaveCurtainColors, () => IsSettingsOpen = false);
        if (paletteError is not null) AddLog($"Color palette invalid · using defaults: {paletteError}");
        foreach (var curtain in BackgroundOptions) curtain.SelectCommand = SelectCurtainCommand;
        if (settings.SimulateRelays)
        {
            foreach (var curtain in Curtains) curtain.CommandStatus = "Closed";
            Array.Fill(_lastCommandStates, "Closed");
            FixedWhiteBackground.IsSelected = true;
            SelectionLabel = $"{FixedWhiteBackground.Name} selected";
            AddLog("Ready · all simulated curtains initialized Closed (off)");
        }
        else
        {
            AddLog("Ready · all curtain positions unknown");
        }
        try
        {
            var saved = _commandStateStore?.Load();
            if (saved is not null)
            {
                for (var index = 0; index < 4; index++)
                {
                    _lastCommandStates[index] = saved[index];
                    Curtains[index].CommandStatus = saved[index] == "Unknown" ? "Unknown" : $"{saved[index]} (saved)";
                }
                AddLog("Restored last commands · saved estimates only, physical positions unverified");
                RestoreSavedSelection();
            }
        }
        catch (Exception error) { AddLog($"Could not restore curtain commands: {error.Message}"); }
        InitializeRazr(razrStore, razrHardwareFactory, cameraButtonSelectionStore);
        if (!IsRazrII) RestoreBackgroundButtonSelection(stage: false);
        if (!IsRazrII) InitializeCurtainRelays(relayController);
        else _relays = relayController; // A supplied controller is already initialized by its caller.
    }

    private void InitializeCurtainRelays(CurtainRelayController? relayController = null)
    {
        try
        {
            _relays = relayController ?? _settings.CreateRelays();
            _backgrounds = new BackgroundController(_relays, _settings.CurtainOrderFrontToBack,
                TimeSpan.FromMilliseconds(_settings.ActivationMilliseconds),
                ReadCurrentCurtainState);
            AddLog("Startup · outputs initialized OFF; reading control signals");
            CheckRelays();
        }
        catch (Exception error)
        {
            _relaySetupError = error.Message;
            try { _relays?.Dispose(); } catch { }
            _relays = null;
            _backgrounds = null;
            Status = $"Relay setup failed: {error.Message}";
            AddLog(Status);
        }
    }

    private void RestoreSavedSelection()
    {
        FixedWhiteBackground.IsSelected = false;
        foreach (var curtain in DisplayCurtains)
        {
            var state = _lastCommandStates[curtain.Id - 1];
            if (state == "Closed") continue;
            if (state == "Opened")
            {
                curtain.IsSelected = true;
                _selectionRestored = true;
                SelectionLabel = $"{curtain.Name} selected (saved)";
                OnPropertyChanged(nameof(SelectionColor));
                AddLog($"Background restored · {curtain.Name} · saved estimate, unverified");
            }
            else
            {
                SelectionLabel = "Saved background unknown";
                AddLog("Background not restored · foreground curtain state unknown");
            }
            return;
        }
        FixedWhiteBackground.IsSelected = true;
        _selectionRestored = true;
        SelectionLabel = $"{FixedWhiteBackground.Name} selected (saved)";
        OnPropertyChanged(nameof(SelectionColor));
    }

    partial void OnCameraLiveChanged(bool value)
    {
        OnPropertyChanged(nameof(CameraOffline));
        OnPropertyChanged(nameof(UserCameraLabel));
        OnPropertyChanged(nameof(CameraIndicator));
        OnPropertyChanged(nameof(IsUserCameraOffline));
    }
    partial void OnIsDarkModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PhotoboothTheme));
        ApplyTheme();
        if (!_themeReady) return;
        try { _themeStore.SaveDarkMode(value); }
        catch (Exception error) { AddLog($"Appearance changed but could not save: {error.Message}"); }
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = IsDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
    }
    partial void OnIsSettingsOpenChanged(bool value)
    {
        NotifyUpdateActions();
        if (!value) { IsRazrSettingsOpen = false; IsModelSelectionOpen = false; }
        OnPropertyChanged(nameof(IsMainScreenVisible));
        OnPropertyChanged(nameof(IsCurtainSettingsVisible));
        OnPropertyChanged(nameof(CanSelectScene));
        OnPropertyChanged(nameof(CanSelect));
        OnPropertyChanged(nameof(IsDeveloperScreenVisible));
        OnPropertyChanged(nameof(IsPhotoboothScreenVisible));
        OnPropertyChanged(nameof(IsRazrIScreenVisible));
        OnPropertyChanged(nameof(IsRazrIIScreenVisible));
        OnPropertyChanged(nameof(IsUserScreenVisible));
        NotifyUserActions();
    }
    partial void OnIsMovingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSelectScene));
        OnPropertyChanged(nameof(CanSelect));
        NotifyUserActions();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (_disposed || IsMoving || Mode == ApplicationMode.User || IsPinDialogOpen) return;
        if (IsRazrII) { OpenCameraSettings(); return; }
        IsRazrSettingsOpen = false;
        CurtainSettings.Begin(_palette, _settings.CurtainOrderFrontToBack, Mode == ApplicationMode.Developer);
        IsSettingsOpen = true;
    }

    private string? SaveCurtainColors(string[] keys, string fixedKey, int[] frontToBack)
    {
        if (_disposed || IsMoving) return "Wait until curtain commands finish.";
        try
        {
            var updated = new CurtainPalette { Colors = CurtainSettings.FixedColors, Slots = keys, FixedBackground = fixedKey, RelayOrderFrontToBack = (int[])frontToBack.Clone() };
            updated.Validate();
            var orderChanged = !_settings.CurtainOrderFrontToBack.SequenceEqual(frontToBack);
            if (orderChanged && Mode != ApplicationMode.Developer) return "Change relay layer order in Developer mode.";
            var nextBackgrounds = orderChanged && _relays is not null
                ? new BackgroundController(_relays, frontToBack, TimeSpan.FromMilliseconds(_settings.ActivationMilliseconds), ReadCurrentCurtainState)
                : _backgrounds;
            _paletteStore.Save(updated);
            _palette = updated;
            var fixedPreset = updated.GetFixedBackground();
            FixedWhiteBackground.Name = fixedPreset.Name;
            FixedWhiteBackground.Color = Brush.Parse(fixedPreset.Color);
            FixedWhiteBackground.PreviewImage = fixedPreset.PreviewImage;
            for (var index = 0; index < 4; index++)
            {
                var preset = updated.Colors.Single(c => c.Key == keys[index]);
                Curtains[index].Name = preset.Name;
                Curtains[index].Color = Brush.Parse(preset.Color);
                Curtains[index].PreviewImage = preset.PreviewImage;
            }
            if (orderChanged)
            {
                _settings.CurtainOrderFrontToBack = (int[])frontToBack.Clone();
                _backgrounds = nextBackgrounds;
                DisplayCurtains = frontToBack.Select(id => Curtains.Single(c => c.Id == id)).ToArray();
                for (var index = 0; index < DisplayCurtains.Count; index++) DisplayCurtains[index].DisplayLevel = index + 1;
                BackgroundOptions = new[] { FixedWhiteBackground }.Concat(Curtains).ToArray();
                OnPropertyChanged(nameof(BackgroundOptions));
                OnPropertyChanged(nameof(SceneOptions));
                OnPropertyChanged(nameof(DisplayCurtains));
                ReconcileLayerOrderSelection();
                AddLog("Layer order saved · nearest to camera: " + string.Join(" → ", frontToBack.Select(id => $"R{id * 2 - 1}/R{id * 2}")));
            }
            var selected = BackgroundOptions.FirstOrDefault(c => c.IsSelected);
            if (selected is not null) SelectionLabel = $"{selected.Name} selected" + (_selectionRestored ? " (saved)" : "");
            OnPropertyChanged(nameof(SelectionColor));
            AddLog("Settings saved · colors and layer order applied without movement");
            return null;
        }
        catch (Exception error) { return $"Could not save colors: {error.Message}"; }
    }

    public void StartCamera()
    {
        _cameraRequested = true;
        if (Mode == ApplicationMode.Startup) return;
        if (!_disposed && _discoveryTask is null) _discoveryTask = RefreshCamerasCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ReconnectCameraAsync()
    {
        if (_disposed) return;
        if (SelectedCamera is null) await RefreshCamerasAsync();
        else
        {
            _cameraSwitchTask = SwitchCameraAsync(SelectedCamera, force: true);
            await _cameraSwitchTask;
        }
    }

    partial void OnSelectedCameraChanged(CameraDevice? value)
    {
        OnPropertyChanged(nameof(CameraDisplayName));
        OnPropertyChanged(nameof(UserCameraLabel));
        if (!_refreshingCameras && !_disposed) _cameraSwitchTask = SwitchCameraAsync(value);
    }

    [RelayCommand]
    private async Task RefreshCamerasAsync()
    {
        if (_disposed || _scanningCameras) return;
        _scanningCameras = true;
        try
        {
            if (!CameraLive) CameraStatus = "Looking for cameras…";
            var detected = await _cameraCatalog.ListAsync(_lifetime.Token);
            if (_disposed) return;
            AddLog($"Camera discovery · {detected.Count} capture device(s)");
            var preferred = SelectedCamera?.Id ?? _settings.CameraDevice;
            _refreshingCameras = true;
            try
            {
                Cameras.Clear();
                foreach (var camera in detected) Cameras.Add(camera);
                SelectedCamera = Cameras.FirstOrDefault(c => c.Id == preferred || c.Name == preferred)
                    ?? Cameras.FirstOrDefault();
            }
            finally { _refreshingCameras = false; }
            _cameraSwitchTask = SwitchCameraAsync(SelectedCamera);
            await _cameraSwitchTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!_disposed) { CameraStatus = $"Camera discovery failed: {error.Message}"; AddLog(CameraStatus); }
        }
        finally { _scanningCameras = false; }
    }

    private async Task SwitchCameraAsync(CameraDevice? camera, bool force = false)
    {
        try
        {
            await _cameraGate.WaitAsync(_lifetime.Token);
            try
            {
                if (_disposed || SelectedCamera?.Id != camera?.Id) return;
                if (!force && camera?.Id == _activeCameraId && _cameraTask is { IsCompleted: false }) return;
                _cameraCancellation?.Cancel();
                if (_cameraTask is not null) await _cameraTask;
                _cameraCancellation?.Dispose();
                _cameraCancellation = null;
                _activeCameraId = null;
                if (_disposed || SelectedCamera?.Id != camera?.Id) return;
                if (camera is null)
                {
                    CameraStatus = "No cameras detected · connect a webcam and refresh";
                    return;
                }
                _cameraCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _activeCameraId = camera.Id;
                _cameraTask = RunCameraAsync(camera, _cameraCancellation.Token);
            }
            finally { _cameraGate.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!_disposed) { CameraStatus = $"Camera switch failed: {error.Message}"; AddLog(CameraStatus); }
        }
    }

    private async Task RunCameraAsync(CameraDevice device, CancellationToken cancellationToken)
    {
        CameraStatus = $"Connecting to {device.Name}…";
        AddLog($"Camera connecting · {device.Name} · {device.Id}");
        try
        {
            var camera = _cameraFactory(device.Id);
            await camera.RunAsync(async pixels =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed || cancellationToken.IsCancellationRequested) return;
                    var frame = new WriteableBitmap(new PixelSize(FfmpegCameraPreview.Width, FfmpegCameraPreview.Height),
                        new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                    using (var buffer = frame.Lock())
                    {
                        for (var row = 0; row < FfmpegCameraPreview.Height; row++)
                            Marshal.Copy(pixels, row * FfmpegCameraPreview.Width * 4,
                                buffer.Address + row * buffer.RowBytes, FfmpegCameraPreview.Width * 4);
                    }
                    var previous = CameraFrame;
                    CameraFrame = frame;
                    previous?.Dispose();
                    CameraLive = true;
                    CameraStatus = $"{device.Name} · 640 × 360 / 15 fps";
                });
            }, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!_disposed) { CameraStatus = $"Camera unavailable: {error.Message}"; AddLog(CameraStatus); }
        }
        finally
        {
            CameraLive = false;
            var previous = CameraFrame;
            CameraFrame = null;
            previous?.Dispose();
        }
    }

    [RelayCommand]
    private async Task SelectCurtainAsync(CurtainOption? curtain)
    {
        if (IsRazrII) return;
        if (curtain is null || IsMoving || IsSettingsOpen || _disposed || _backgrounds is null) return;
        if (!BackgroundOptions.Contains(curtain)) return;
        var selected = _razr.Copy();
        if (_settings.SimulateRelays) selected.SelectedSimulatedBackgroundId = curtain.Id;
        else selected.SelectedBackgroundId = curtain.Id;
        try { _razrStore.Save(selected); _razr = selected; }
        catch (Exception error) { AddLog($"Could not save selected background: {error.Message}"); }
        if (curtain.IsSelected && !_selectionRestored) return;
        if (Mode is ApplicationMode.User or ApplicationMode.Developer)
        {
            _selectionRestored = false;
            foreach (var option in BackgroundOptions) option.IsSelected = option == curtain;
            _pendingUserCurtain = IsBackgroundApplied(curtain) ? null : curtain;
            SelectionLabel = _pendingUserCurtain is null ? $"{curtain.Name} selected" : $"{curtain.Name} selected · press START";
            OnPropertyChanged(nameof(SelectionColor));
            NotifyUserActions();
            ShowBackgroundPreview(curtain);
            return;
        }
        _selectionRestored = false;
        foreach (var option in BackgroundOptions) option.IsSelected = option == curtain;
        OnPropertyChanged(nameof(SelectionColor));
        SelectionLabel = $"{curtain.Name} selected";
        await MoveAsync(curtain.Id == 0 ? null : curtain);
    }

    [RelayCommand]
    private async Task CloseAllAsync()
    {
        if (IsRazrII || IsMoving || IsSettingsOpen || _disposed || _backgrounds is null) return;
        CancelUserDraft();
        if (Curtains.All(c => c.CommandStatus == "Closed")) return;
        _selectionRestored = false;
        foreach (var option in BackgroundOptions) option.IsSelected = false;
        OnPropertyChanged(nameof(SelectionColor));
        SelectionLabel = "Close all curtains";
        await MoveAsync(null);
    }

    private async Task MoveAsync(CurtainOption? selected)
    {
        IsMoving = true;
        using var movement = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _movement = movement;
        try
        {
            Status = selected is null ? $"Setting {FixedWhiteBackground.Name} · closing all curtains…" : $"Setting {selected.Name} · clearing curtains in front…";
            if (selected is not null)
                await _backgrounds!.SelectAsync(selected.Id, movement.Token, ReportCurtain);
            else
                await _backgrounds!.CloseAllAsync(movement.Token, ReportCurtain);
            Status = _settings.SimulateRelays ? "Simulated relay command complete" : "Relay command complete";
        }
        catch (OperationCanceledException) { Status = "Stopped · curtain position unknown"; AddLog(Status); }
        catch (Exception error) { Status = $"Relay error: {error.Message}"; AddLog(Status); }
        finally { _movement = null; IsMoving = false; }
    }

    [RelayCommand]
    private void Stop()
    {
        ClearBackgroundPreview();
        _movement?.Cancel();
        try { _relays?.StopAll(); Status = "Stopped · curtain position unknown"; }
        catch (Exception error) { Status = $"Stop failed: {error.Message}"; }
        AddLog(Status);
    }

    private void ReportCurtain(CurtainCommandUpdate update)
    {
        lock (_logSync)
        {
            var curtain = Curtains[update.Curtain - 1];
            curtain.CommandStatus = update.State.ToString();
            _lastCommandStates[update.Curtain - 1] = update.State is CurtainCommandState.Opened or CurtainCommandState.Closed
                ? update.State.ToString() : "Unknown";
            if (_commandStateStore is not null)
            {
                try { _commandStateStore.Save(_lastCommandStates); }
                catch (Exception error)
                {
                    AddLog($"Could not save curtain commands: {error.Message}");
                    if (update.State is CurtainCommandState.Opening or CurtainCommandState.Closing)
                    {
                        curtain.CommandStatus = "Unknown";
                        throw new InvalidOperationException("Cannot record pending curtain command; no new pulse sent for this curtain.", error);
                    }
                }
            }
            AddLog($"{curtain.Name} · {update.State}" +
                (update.State is CurtainCommandState.Opened or CurtainCommandState.Closed
                    ? " (command complete)" : ""));
        }
    }

    private void AddLog(string message)
    {
        lock (_logSync)
        {
            ActivityLog.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss}  {(_settings.SimulateRelays ? "[SIM] " : "")}{message}");
            while (ActivityLog.Count > 100) ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    [RelayCommand]
    private void ClearLog() { lock (_logSync) ActivityLog.Clear(); }

    [RelayCommand]
    private void CheckRelays()
    {
        if (_disposed) return;
        if (_relays is null)
        {
            Status = $"Cannot check relays · setup failed: {_relaySetupError ?? "Relay controller is unavailable."}";
            AddLog(Status);
            return;
        }
        try
        {
            var readings = _relays.ReadOutputs();
            foreach (var relay in readings)
            {
                var curtain = Curtains[(relay.Channel - 1) / 2];
                var direction = relay.Channel % 2 == 1 ? "open" : "close";
                var level = relay.Energized switch { true => "ON", false => "OFF", null => "UNKNOWN" };
                AddLog($"R{relay.Channel} · {curtain.Name} {direction} · {level}" +
                    (relay.Error is null ? " (control signal)" : $" · {relay.Error}"));
            }
            Status = $"Relay check complete · {readings.Count} control signals read (not curtain positions)";
            AddLog(Status);
        }
        catch (Exception error)
        {
            Status = $"Relay check failed: {error.Message}";
            AddLog(Status);
        }
    }

    public event Action<string>? ShutdownProgress;
    private void ReportShutdown(string message)
    {
        AddLog(message);
        ShutdownProgress?.Invoke(message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        ReportShutdown("Shutdown requested; cancelling background work.");
        ClearBackgroundPreview();
        _backgroundPreviewTimer.Dispose();
        foreach (var cancellation in new[] { _lifetime, _movement, _cameraCancellation })
        {
            try { cancellation?.Cancel(); }
            catch (Exception error) { ReportShutdown($"Cancellation cleanup: {error.Message}"); }
        }
        try { _relays?.StopAll(); }
        catch (Exception error) { ReportShutdown($"Relay stop error: {error.Message}"); }
        try
        {
            var movementTasks = new[] { StartBackgroundCommand.ExecutionTask, SelectCurtainCommand.ExecutionTask, CloseAllCommand.ExecutionTask }
                .Where(task => task is not null).Cast<Task>().Distinct();
            await Task.WhenAll(movementTasks);
        }
        catch (Exception error) { ReportShutdown($"Movement cleanup: {error.Message}"); }
        finally
        {
            try { _razrHardware.Dispose(); }
            catch (Exception error) { ReportShutdown($"Camera controller shutdown error: {error.Message}"); }
            try { _relays?.Dispose(); }
            catch (Exception error) { ReportShutdown($"Relay release error: {error.Message}"); }
        }
        ReportShutdown("Hardware released; waiting for camera cleanup (maximum 3 seconds).");
        var backgroundTasks = new[] { _backgroundPreviewTask, _discoveryTask, _startupUpdateTask, CheckUpdatesCommand.ExecutionTask,
                _updateHandoff ? null : InstallUpdateCommand.ExecutionTask, RefreshCamerasCommand.ExecutionTask,
                ReconnectCameraCommand.ExecutionTask, _cameraSwitchTask, _cameraTask }
            .Where(task => task is not null).Cast<Task>().Distinct();
        var cleanup = Task.WhenAll(backgroundTasks);
        try { await cleanup.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException)
        {
            ReportShutdown("Camera cleanup timed out; hardware is released, completing window close.");
            // Observe any late fault; do not keep the window open for an unresponsive camera.
            _ = cleanup.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (Exception error) { ReportShutdown($"Camera cleanup: {error.Message}"); }
        _cameraCancellation?.Dispose();
        _lifetime.Dispose();
        ReportShutdown("Shutdown cleanup complete.");
    }
}
