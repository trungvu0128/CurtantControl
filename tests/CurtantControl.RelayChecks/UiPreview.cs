using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using CurtantControl.ViewModels;
using CurtantControl.Views;

internal static class UiPreview
{
    public static void VerifyWindowClose()
    {
        AppBuilder.Configure<CurtantControl.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont().SetupWithoutStarting();
        foreach (var stuck in new[] { false, true })
        {
            var camera = new HangingCameraPreview();
            var catalog = new FakeCameraCatalog { Devices = stuck ? [new("stuck", "Stuck preview")] : [] };
            var output = new RecordingOutput();
            var relays = new CurtantControl.Services.Relays.CurtainRelayController(output, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(5));
            var vm = new MainViewModel(new(), relays, cameraCatalog: catalog, cameraFactory: _ => camera,
                themeStore: new PreviewThemeStore(), paletteStore: new PreviewPaletteStore());
            vm.EnterUserModeCommand.Execute(null);
            var window = new MainWindow { DataContext = vm };
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
            window.Close(); // Repeated Stop while closing must not skip hardware cleanup.
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!closed && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            if (!closed || window.IsVisible || !output.AllOff || !output.Disposed)
                throw new InvalidOperationException("Close request left a window or unreleased relay output.");
            if (stuck && !camera.Started.Task.IsCompleted) throw new InvalidOperationException("Stuck camera scenario never started.");
            camera.Completion.TrySetResult();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Console.WriteLine("Verified window closes after synchronous cleanup and stuck camera; repeated Stop never skips relay release.");
    }

    public static void Render(string path, bool settingsScreen = false, int width = 1280, int height = 760, bool liveSample = false, bool light = false, bool hoverReconnect = false, string mode = "developer", bool hoverCurtain = false, bool verifyModeHold = false, bool nearestRelayOne = false, bool razrII = false, bool razrSetup = false, bool setupBottom = false, bool modelChooser = false, bool verifyRazrState = false, bool pinDialog = false, bool showUpdates = false)
    {
        AppBuilder.Configure<CurtantControl.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont().SetupWithoutStarting();
        var updates = new FakeUpdates();
        updates.Result.TrySetResult(CurtantControl.Services.GitHubAppUpdates.ParseRelease(UpdateChecks.ReleaseJson(), "0.10.5"));
        var viewModel = new MainViewModel(new CurtantControl.Services.PhotoboothSettings { CurtainOrderFrontToBack = nearestRelayOne ? [1, 2, 3, 4] : [4, 3, 2, 1] },
            orderStore: new MemoryOrderStore(), cameraCatalog: new FakeCameraCatalog(),
            cameraFactory: new FakeCameraRig().Create, themeStore: new PreviewThemeStore(), paletteStore: new PreviewPaletteStore(),
            updateService: showUpdates ? updates : null);
        viewModel.IsDarkMode = !light;
        if (mode == "developer") viewModel.EnterDeveloperModeCommand.Execute(null);
        if (mode == "user") viewModel.EnterUserModeCommand.Execute(null);
        if (razrII || razrSetup)
        {
            viewModel.EnterDeveloperModeCommand.Execute(null);
            viewModel.OpenRazrSettingsCommand.Execute(null);
            viewModel.EnterRazrIICommand.Execute(null);
            if (mode == "user") viewModel.EnterUserModeCommand.Execute(null);
            if (razrSetup) viewModel.OpenCameraSettingsCommand.Execute(null);
        }
        var selection = viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[1]);
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (!selection.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!selection.IsCompleted) throw new TimeoutException("Preview selection did not complete.");
        selection.GetAwaiter().GetResult();
        if (settingsScreen)
        {
            viewModel.OpenSettingsCommand.Execute(null);
            viewModel.CurtainSettings.SaveCommand.Execute(null);
        }
        if (modelChooser) viewModel.OpenRazrSettingsCommand.Execute(null);
        if (showUpdates)
        {
            var checking = viewModel.CheckForUpdatesOnStartupAsync();
            while (!checking.IsCompleted)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            checking.GetAwaiter().GetResult();
            viewModel.OpenUpdatesCommand.Execute(null);
        }
        if (pinDialog)
        {
            if (mode == "user") viewModel.OpenRazrSettingsCommand.Execute(null);
            else viewModel.EditSwitchPinCommand.Execute(null);
        }
        var window = new MainWindow
        {
            DataContext = viewModel, Width = width, Height = height, WindowState = WindowState.Normal,
            WindowDecorations = WindowDecorations.None
        };
        window.Show();
        if (verifyRazrState)
        {
            viewModel.OpenCameraSettingsCommand.Execute(null);
            viewModel.RazrDraft.SettleSeconds = 0;
            viewModel.SaveRazrSettingsCommand.Execute(null);
            viewModel.CancelRazrSettingsCommand.Execute(null);
            viewModel.SelectCameraPositionCommand.Execute(viewModel.CameraPositions[0]);
            var applying = viewModel.StartBackgroundCommand.ExecuteAsync(null);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!applying.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            if (!applying.IsCompleted) throw new TimeoutException("Camera simulation did not finish.");
            applying.GetAwaiter().GetResult();
            viewModel.OpenCameraSettingsCommand.Execute(null);
            viewModel.CancelRazrSettingsCommand.Execute(null);
            viewModel.OpenCameraSettingsCommand.Execute(null);
            viewModel.SaveRazrSettingsCommand.Execute(null);
            viewModel.CancelRazrSettingsCommand.Execute(null);
            viewModel.SelectCameraPositionCommand.Execute(viewModel.CameraPositions[1]);
            viewModel.SelectCameraPositionCommand.Execute(viewModel.CameraPositions[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var selectedButton = window.GetVisualDescendants().OfType<Button>()
                .Single(b => b.CommandParameter is CurtainOption { Id: 101 });
            if (!selectedButton.Classes.Contains("selected") || viewModel.CanStart
                || selectedButton.BorderBrush?.ToString() != Avalonia.Media.Brush.Parse("#FF3038").ToString())
                throw new InvalidOperationException("Razr II selected border or applied STATE guard was lost.");
            Console.WriteLine("Verified actual selected red border after STATE/settings/browse-back.");
        }
        if (liveSample)
        {
            var pattern = new WriteableBitmap(new PixelSize(640, 360), new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
            var pixels = new byte[640 * 360 * 4];
            for (var y = 0; y < 360; y++)
                for (var x = 0; x < 640; x++)
                {
                    var offset = (y * 640 + x) * 4;
                    pixels[offset] = (byte)(100 + x * 120 / 640);
                    pixels[offset + 1] = (byte)(50 + y * 150 / 360);
                    pixels[offset + 2] = (byte)(180 - x * 120 / 640);
                    pixels[offset + 3] = 255;
                }
            using (var locked = pattern.Lock())
                for (var row = 0; row < 360; row++)
                    System.Runtime.InteropServices.Marshal.Copy(pixels, row * 640 * 4, locked.Address + row * locked.RowBytes, 640 * 4);
            viewModel.CameraFrame = pattern;
            viewModel.CameraLive = true;
            viewModel.CameraStatus = "Layout test pattern · not a live camera";
        }
        if (hoverCurtain)
        {
            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("photo") && b.Classes.Contains("selected"));
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
        }
        if (hoverReconnect)
        {
            var previewWait = System.Diagnostics.Stopwatch.StartNew();
            while (viewModel.IsBackgroundPreviewVisible && previewWait.Elapsed < TimeSpan.FromSeconds(3))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ReconnectCameraButton" && b.GetVisualAncestors().OfType<Control>().All(c => c.IsVisible));
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
        }
        if (setupBottom)
        {
            foreach (var expander in window.GetVisualDescendants().OfType<Expander>()) expander.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "SetupScroll");
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No UI frame rendered.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        frame.Save(path, new PngBitmapEncoderOptions());
        if (pinDialog && mode == "user")
        {
            void TapPin(string label)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var button = window.GetVisualDescendants().OfType<PinDialogView>().Single()
                    .GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == label);
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, Avalonia.Input.MouseButton.Left);
                window.MouseUp(point, Avalonia.Input.MouseButton.Left);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
            TapPin("9"); TapPin("←");
            if (viewModel.MaskedPin != "— — — —") throw new InvalidOperationException("Touch delete failed.");
            TapPin("Cancel");
            if (viewModel.IsPinDialogOpen || viewModel.IsModelSelectionOpen) throw new InvalidOperationException("Touch Cancel failed.");
            viewModel.OpenRazrSettingsCommand.Execute(null);
            foreach (var digit in "1234") TapPin(digit.ToString());
            TapPin("OK");
            if (!viewModel.IsModelSelectionOpen || viewModel.IsPinDialogOpen) throw new InvalidOperationException("Touch PIN verification failed.");
            Console.WriteLine("Verified actual onscreen numeric buttons, delete, Cancel and authenticated chooser.");
        }
        if ((mode == "user" || mode == "developer") && liveSample)
        {
            foreach (var preset in new CurtantControl.Services.CurtainPaletteStore().Load().Colors)
            {
                using var photo = CurtantControl.Services.BackgroundPreviewLoader.Load(preset.PreviewImage);
                if (photo is null) throw new InvalidOperationException($"Missing photo: {preset.Key}");
            }
            if (!viewModel.IsBackgroundPreviewVisible) throw new InvalidOperationException("Sample overlay was not displayed.");
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (viewModel.IsBackgroundPreviewVisible && wait.Elapsed < TimeSpan.FromSeconds(3))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            if (viewModel.IsBackgroundPreviewVisible || viewModel.CameraFrame is null)
                throw new InvalidOperationException("Sample did not return to camera.");
            viewModel.SelectCurtainCommand.ExecuteAsync(viewModel.Curtains[0]).GetAwaiter().GetResult();
            var start = viewModel.StartBackgroundCommand.ExecuteAsync(null);
            if (viewModel.IsBackgroundPreviewVisible) throw new InvalidOperationException("START retained sample overlay.");
            while (!start.IsCompleted)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            start.GetAwaiter().GetResult();
            Console.WriteLine("Verified all sample images, timed return to camera, and START clearing sample.");
        }
        if (verifyModeHold)
        {
            var area = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ModeHoldArea" && b.GetVisualAncestors().OfType<Control>().All(c => c.IsVisible));
            var point = area.TranslatePoint(new Point(40, 40), window)!.Value;
            void Pump(int milliseconds)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < milliseconds)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(5);
                }
            }
            window.MouseMove(point);
            window.MouseDown(point, Avalonia.Input.MouseButton.Left);
            Pump(200);
            window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            Pump(3100);
            if (viewModel.Mode != ApplicationMode.User) throw new InvalidOperationException("Short tap opened mode chooser.");
            window.MouseDown(point, Avalonia.Input.MouseButton.Left);
            Pump(3200);
            window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            if (viewModel.Mode != ApplicationMode.Startup) throw new InvalidOperationException("Three-second hold did not open chooser.");
            Console.WriteLine("Verified short-tap cancellation and three-second corner hold.");
        }
        window.Close();
        Console.WriteLine($"Rendered UI preview: {Path.GetFullPath(path)}");
    }
}

internal sealed class PreviewThemeStore : CurtantControl.Services.IThemePreferenceStore
{
    public bool LoadDarkMode() => true;
    public void SaveDarkMode(bool dark) { }
}



internal sealed class PreviewPaletteStore : CurtantControl.Services.ICurtainPaletteStore
{
    private CurtantControl.Services.CurtainPalette _value = LoadDefaults();
    private static CurtantControl.Services.CurtainPalette LoadDefaults()
    {
        var value = new CurtantControl.Services.CurtainPaletteStore().Load();
        value.RelayOrderFrontToBack = null;
        return value;
    }
    public CurtantControl.Services.CurtainPalette Load() => _value;
    public void Save(CurtantControl.Services.CurtainPalette palette) => _value = palette;
}
