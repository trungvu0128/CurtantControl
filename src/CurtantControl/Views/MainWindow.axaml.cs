using Avalonia.Controls;
using CurtantControl.ViewModels;
using Avalonia.Threading;
using System;
using System.IO;

namespace CurtantControl.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            _updatesViewModel = vm;
            vm.UpdateInstallReady += CloseForUpdate;
            vm.StartCamera();
            _ = vm.CheckForUpdatesOnStartupAsync();
        };
        Closed += (_, _) => { if (_updatesViewModel is not null) _updatesViewModel.UpdateInstallReady -= CloseForUpdate; };
        Closing += async (_, args) =>
        {
            if (_shutdownComplete) return;
            args.Cancel = true;
            if (_shuttingDown) return;
            _shuttingDown = true;
            var viewModel = DataContext as MainViewModel;
            WriteShutdownLog("Window close received.");
            try
            {
                if (viewModel is not null)
                {
                    viewModel.ShutdownProgress += WriteShutdownLog;
                    await viewModel.DisposeAsync();
                }
            }
            catch (Exception error) { WriteShutdownLog("Window cleanup failed: " + error); }
            finally
            {
                if (viewModel is not null) viewModel.ShutdownProgress -= WriteShutdownLog;
                _shutdownComplete = true;
                // Re-enter close only after the original cancelled Closing event unwinds.
                Dispatcher.UIThread.Post(() => { WriteShutdownLog("Completing window close."); Close(); });
            }
        };
    }

    private bool _shuttingDown;
    private MainViewModel? _updatesViewModel;
    private void CloseForUpdate() => Close();
    private bool _shutdownComplete;
    private static void WriteShutdownLog(string message)
    {
        var line = DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine;
        Console.Error.Write(line);
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CurtantControl");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "shutdown.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65_536) File.WriteAllText(path, line);
            else File.AppendAllText(path, line);
        }
        catch (Exception) { /* Closing must not depend on log storage. */ }
    }
}
