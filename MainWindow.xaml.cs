using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ElectroERP.Manager.Models;
using ElectroERP.Manager.Services;
using ElectroERP.Manager.Views;

namespace ElectroERP.Manager;

public partial class MainWindow : Window
{
    private readonly ProcessSupervisorService _supervisor;
    private readonly PinAuthService _pinAuthService;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly ErrorLogService _errorLogService;
    private readonly SystemTrayService _trayService;
    private readonly CloudflareTunnelService _tunnelService;
    private readonly WebsiteSupervisorService? _websiteSupervisor;

    private readonly MainView _mainView;
    private readonly UpdaterView _updaterView;
    private readonly SettingsView _settingsView;

    private bool _isExplicitExit;
    public bool IsAuthenticated { get; set; }

    public MainWindow(
        ProcessSupervisorService supervisor,
        PinAuthService pinAuthService,
        SettingsService settingsService,
        StartupService startupService,
        ErrorLogService errorLogService,
        SystemTrayService trayService,
        CloudflareTunnelService tunnelService,
        bool startMinimized = false,
        WebsiteSupervisorService? websiteSupervisor = null)
    {
        InitializeComponent();

        _supervisor = supervisor;
        _pinAuthService = pinAuthService;
        _settingsService = settingsService;
        _startupService = startupService;
        _errorLogService = errorLogService;
        _trayService = trayService;
        _tunnelService = tunnelService;
        _websiteSupervisor = websiteSupervisor;

        var backupService = new BackupService(_settingsService);
        _mainView = new MainView(_supervisor, _pinAuthService, _errorLogService, _tunnelService, _websiteSupervisor);
        _updaterView = new UpdaterView();
        _settingsView = new SettingsView(_settingsService, _pinAuthService, _startupService, backupService, _supervisor);

        PageContentHost.Content = _mainView;
        TabStart.IsChecked = true;

        // Wire tray service events
        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.ExitRequested += OnTrayExitRequested;
        _trayService.LaunchRequested += OnTrayLaunchRequested;
        _trayService.StopRequested += OnTrayStopRequested;

        _supervisor.StateChanged += state =>
        {
            Dispatcher.Invoke(() => _trayService.UpdateServerState(state));
        };

        Closing += OnWindowClosing;

        if (startMinimized)
        {
            Hide();
        }
    }

    public bool ShowModalPin(string prompt, string? title = null)
    {
        var blur = new System.Windows.Media.Effects.BlurEffect
        {
            Radius = 16,
            RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
        };

        bool wasVisible = IsVisible;
        if (wasVisible)
        {
            Effect = blur;
        }

        try
        {
            var pinDialog = new PinDialog(_pinAuthService, prompt, title);
            if (wasVisible)
            {
                pinDialog.Owner = this;
                pinDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                pinDialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return pinDialog.ShowDialog() == true;
        }
        finally
        {
            if (wasVisible)
            {
                Effect = null;
            }
        }
    }

    public bool ShowModalChangePin()
    {
        var blur = new System.Windows.Media.Effects.BlurEffect
        {
            Radius = 16,
            RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
        };

        bool wasVisible = IsVisible;
        if (wasVisible)
        {
            Effect = blur;
        }

        try
        {
            var dialog = new ChangePinDialog(_pinAuthService);
            if (wasVisible)
            {
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return dialog.ShowDialog() == true;
        }
        finally
        {
            if (wasVisible)
            {
                Effect = null;
            }
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                OnMaximizeRestoreClick(sender, e);
            }
            else if (WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIcon.Text = "🗖";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIcon.Text = "❐";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
        _trayService.ShowBalloon("ElectroERP Manager", "Application minimized to system tray. Server continues running.");
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        if (PageContentHost == null) return;

        if (TabStart?.IsChecked == true && _mainView != null)
        {
            PageContentHost.Content = _mainView;
        }
        else if (TabUpdater?.IsChecked == true && _updaterView != null)
        {
            PageContentHost.Content = _updaterView;
        }
        else if (TabSettings?.IsChecked == true && _settingsView != null)
        {
            PageContentHost.Content = _settingsView;
        }
    }

    private void OnTrayOpenRequested()
    {
        Dispatcher.Invoke(() =>
        {
            if (!IsAuthenticated)
            {
                if (ShowModalPin("Enter your 6-digit PIN to access ElectroERP Manager", "ElectroERP Manager"))
                {
                    IsAuthenticated = true;
                }
                else
                {
                    return;
                }
            }

            Show();
            WindowState = WindowState.Maximized;
            Activate();
        });
    }

    private void OnTrayLaunchRequested()
    {
        Dispatcher.Invoke(() =>
        {
            if (ShowModalPin("Enter PIN to Launch ElectroERP", "Launch Server"))
            {
                var (success, error) = _supervisor.StartServer();
                if (!success && !string.IsNullOrEmpty(error))
                {
                    _trayService.ShowBalloon("Startup Failed", error, System.Windows.Forms.ToolTipIcon.Warning);
                }
            }
        });
    }

    private void OnTrayStopRequested()
    {
        Dispatcher.Invoke(() =>
        {
            if (ShowModalPin("Enter PIN to Stop Server", "Stop Server"))
            {
                _supervisor.StopServer();
            }
        });
    }

    private void OnTrayExitRequested()
    {
        Dispatcher.Invoke(() =>
        {
            if (_supervisor.CurrentState == ServerState.Running ||
                _tunnelService.IsOnline ||
                _websiteSupervisor?.CurrentState == ServerState.Running)
            {
                if (!ShowModalPin("Enter PIN to Stop Server & Exit", "Exit Application"))
                {
                    return;
                }
                _supervisor.StopServer();
                _tunnelService.StopTunnel();
                _websiteSupervisor?.StopWebsite();
            }

            _isExplicitExit = true;
            _trayService.Dispose();
            Application.Current.Shutdown();
        });
    }
}
