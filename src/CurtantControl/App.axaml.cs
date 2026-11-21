using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CurtantControl.ViewModels;
using CurtantControl.Views;
using CurtantControl.Services;
using System;

namespace CurtantControl;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel viewModel;
            try
            {
                var settings = PhotoboothSettings.Load();
                // Paper mapping: M4 nearest camera, then M3, M2, M1, fixed White.
                settings.CurtainOrderFrontToBack = [4, 3, 2, 1];
                viewModel = new MainViewModel(settings,
                    commandStateStore: new CurtainCommandStateStore(settings.SimulateRelays),
                    razrStore: new RazrConfigurationStore(),
                    cameraButtonSelectionStore: new CameraButtonSelectionStore(), switchPinStore: new SwitchPinStore(), updateService: new GitHubAppUpdates());
            }
            catch (Exception error)
            {
                viewModel = new MainViewModel(new PhotoboothSettings { CurtainOrderFrontToBack = [4, 3, 2, 1] }, switchPinStore: new SwitchPinStore(), updateService: new GitHubAppUpdates()) { Status = $"Configuration error · using simulation: {error.Message}" };
            }
            viewModel.EnterUserModeCommand.Execute(null);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
                WindowState = Avalonia.Controls.WindowState.FullScreen,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
