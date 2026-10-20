using System.Threading;
using System.Windows;
using ElectroERP.Manager.Services;

namespace ElectroERP.Manager;

public partial class App : Application
{
    private const string MutexName = @"Global\ElectroERP_Manager_SingleInstance";
    private Mutex? _singleInstanceMutex;
    private SystemTrayService? _trayService;
    private ProcessSupervisorService? _supervisor;
    private CloudflareTunnelService? _tunnelService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool createdNew;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        }
        catch
        {
            createdNew = true;
        }

        if (!createdNew)
        {
            MessageBox.Show(
                "ElectroERP Manager is already running in the background or system tray.",
                "Already Running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            MessageBox.Show(
                $"An unexpected error occurred: {args.Exception.Message}\n\n{args.Exception}",
                "ElectroERP Manager Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        // Initialize Services
        var settingsService = new SettingsService();
        var pinAuthService = new PinAuthService(settingsService);
        var startupService = new StartupService();
        var errorLogService = new ErrorLogService();
        _supervisor = new ProcessSupervisorService(settingsService, errorLogService);
        _tunnelService = new CloudflareTunnelService(errorLogService);
        _trayService = new SystemTrayService();

        bool startMinimized = e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        var mainWindow = new MainWindow(
            _supervisor,
            pinAuthService,
            settingsService,
            startupService,
            errorLogService,
            _trayService,
            _tunnelService,
            startMinimized);

        MainWindow = mainWindow;

        if (!startMinimized)
        {
            var pinDialog = new Views.PinDialog(
                pinAuthService,
                "Enter your 6-digit PIN to access ElectroERP Manager",
                "ElectroERP Manager")
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            if (pinDialog.ShowDialog() == true)
            {
                mainWindow.IsAuthenticated = true;
                mainWindow.Show();
            }
            else
            {
                _trayService?.Dispose();
                Shutdown();
                return;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tunnelService?.Dispose();
        _supervisor?.Dispose();
        _trayService?.Dispose();

        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
            }
            catch { }
        }

        base.OnExit(e);
    }
}
