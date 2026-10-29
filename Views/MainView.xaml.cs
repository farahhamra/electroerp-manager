using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElectroERP.Manager.Models;
using ElectroERP.Manager.Services;

namespace ElectroERP.Manager.Views;

public partial class MainView : UserControl
{
    private readonly ProcessSupervisorService _supervisor;
    private readonly PinAuthService _pinAuthService;
    private readonly ErrorLogService _errorLogService;
    private readonly CloudflareTunnelService _tunnelService;
    private readonly WebsiteSupervisorService? _websiteSupervisor;
    private readonly string _appVersion;
    private bool _isDiagnosticsExpanded;

    public MainView(
        ProcessSupervisorService supervisor,
        PinAuthService pinAuthService,
        ErrorLogService errorLogService,
        CloudflareTunnelService tunnelService,
        WebsiteSupervisorService? websiteSupervisor = null)
    {
        InitializeComponent();

        _supervisor = supervisor;
        _pinAuthService = pinAuthService;
        _errorLogService = errorLogService;
        _tunnelService = tunnelService;
        _websiteSupervisor = websiteSupervisor;

        var rawVersion = Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = rawVersion != null && (rawVersion.Major > 1 || rawVersion.Minor > 0)
            ? $"v{rawVersion.Major}.{rawVersion.Minor}.{rawVersion.Build}"
            : "v1.4.2";

        OnlineAccessCard.Address = $"{CloudflareTunnelService.TunnelName}.electroerp.app";
        WebsiteCard.Address = "/store";

        _supervisor.StateChanged += OnStateChanged;
        _errorLogService.ErrorLogged += OnErrorLogged;
        _tunnelService.OnlineStateChanged += OnTunnelOnlineChanged;
        if (_websiteSupervisor != null)
        {
            _websiteSupervisor.StateChanged += OnWebsiteStateChanged;
        }

        Loaded += (_, _) => RefreshAllUI();
    }

    private void OnWebsiteStateChanged(ServerState _) => Dispatcher.Invoke(RefreshAllUI);
    private void OnTunnelOnlineChanged(bool _) => Dispatcher.Invoke(RefreshAllUI);
    private void OnErrorLogged(string _) => Dispatcher.Invoke(RefreshErrorDisplay);

    private void OnStateChanged(ServerState state)
    {
        if (state is ServerState.Stopped or ServerState.Crashed)
        {
            if (_websiteSupervisor?.CurrentState is ServerState.Running or ServerState.Starting)
            {
                _websiteSupervisor.StopWebsite();
            }
            if (_tunnelService.IsOnline)
            {
                _tunnelService.StopTunnel();
            }
        }
        Dispatcher.Invoke(RefreshAllUI);
    }

    private void RefreshAllUI()
    {
        var erpState = _supervisor.CurrentState;
        var isErpRunning = erpState == ServerState.Running;
        var isOnline = _tunnelService.IsOnline;
        var websiteState = _websiteSupervisor?.CurrentState ?? ServerState.Stopped;

        UpdateSummaryBar(erpState, isOnline);
        UpdateErpCard(erpState);
        UpdateOnlineCard(isOnline, isErpRunning);
        UpdateWebsiteCard(websiteState, isErpRunning);
        RefreshErrorDisplay();
    }

    private void UpdateSummaryBar(ServerState erpState, bool isOnline)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var stopActionBrush = (Brush)FindResource("ActionStopBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");

        var (dotBrush, stateLabel) = erpState switch
        {
            ServerState.Running => (runningBrush, "ElectroERP running"),
            ServerState.Starting => (accentBrush, "ElectroERP starting"),
            ServerState.Stopping => (stoppedBrush, "ElectroERP stopping"),
            ServerState.Crashed => (stopActionBrush, "ElectroERP crashed"),
            _ => (stoppedBrush, "ElectroERP stopped")
        };

        var onlineLabel = isOnline ? "Online" : "Offline";
        SummaryStatusDot.Fill = dotBrush;
        SummaryStatusText.Text = $"{stateLabel} · {onlineLabel} · {_appVersion}";
        RestartAllButton.IsEnabled = erpState == ServerState.Running;
    }

    private void UpdateErpCard(ServerState state)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var stopActionBrush = (Brush)FindResource("ActionStopBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var primaryText = (Brush)FindResource("TextPrimaryBrush");
        var secondaryText = (Brush)FindResource("TextSecondaryBrush");
        var primaryStyle = (Style)FindResource("PrimaryButtonStyle");
        var stopStyle = (Style)FindResource("StopButtonStyle");

        switch (state)
        {
            case ServerState.Running:
                ErpServerCard.UpdateCardState("Running", runningBrush, primaryText, "Stop", stopStyle, true, true, true);
                break;
            case ServerState.Starting:
                ErpServerCard.UpdateCardState("Starting...", accentBrush, secondaryText, "Starting...", primaryStyle, false, false, false);
                break;
            case ServerState.Stopping:
                ErpServerCard.UpdateCardState("Stopping...", stoppedBrush, secondaryText, "Stopping...", stopStyle, false, false, false);
                break;
            case ServerState.Crashed:
                ErpServerCard.UpdateCardState("Crashed", stopActionBrush, stopActionBrush, "Launch", primaryStyle, true, false, false);
                break;
            default:
                ErpServerCard.UpdateCardState("Stopped", stoppedBrush, secondaryText, "Launch", primaryStyle, true, false, false);
                break;
        }
    }

    private void UpdateOnlineCard(bool isOnline, bool isErpRunning)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var primaryText = (Brush)FindResource("TextPrimaryBrush");
        var secondaryText = (Brush)FindResource("TextSecondaryBrush");
        var primaryStyle = (Style)FindResource("PrimaryButtonStyle");
        var stopStyle = (Style)FindResource("StopButtonStyle");
        var hint = isErpRunning ? "" : "Start the ERP server first";

        if (isOnline && isErpRunning)
        {
            OnlineAccessCard.UpdateCardState("Connected", runningBrush, primaryText, "Go Offline", stopStyle, true, false, true, hint);
        }
        else
        {
            OnlineAccessCard.UpdateCardState("Offline", stoppedBrush, secondaryText, "Go Online", primaryStyle, isErpRunning, false, isErpRunning, hint);
        }
    }

    private void UpdateWebsiteCard(ServerState state, bool isErpRunning)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var stopActionBrush = (Brush)FindResource("ActionStopBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var primaryText = (Brush)FindResource("TextPrimaryBrush");
        var secondaryText = (Brush)FindResource("TextSecondaryBrush");
        var primaryStyle = (Style)FindResource("PrimaryButtonStyle");
        var stopStyle = (Style)FindResource("StopButtonStyle");
        var hint = isErpRunning ? "" : "Start the ERP server first";

        switch (state)
        {
            case ServerState.Running:
                WebsiteCard.UpdateCardState("Running", runningBrush, primaryText, "Stop", stopStyle, isErpRunning, false, isErpRunning, hint);
                break;
            case ServerState.Starting:
                WebsiteCard.UpdateCardState("Starting...", accentBrush, secondaryText, "Starting...", primaryStyle, false, false, false, hint);
                break;
            case ServerState.Stopping:
                WebsiteCard.UpdateCardState("Stopping...", stoppedBrush, secondaryText, "Stopping...", stopStyle, false, false, false, hint);
                break;
            case ServerState.Crashed:
                WebsiteCard.UpdateCardState("Crashed", stopActionBrush, stopActionBrush, "Launch", primaryStyle, isErpRunning, false, false, hint);
                break;
            default:
                WebsiteCard.UpdateCardState("Stopped", stoppedBrush, secondaryText, "Launch", primaryStyle, isErpRunning, false, false, hint);
                break;
        }
    }

    private void OnServerActionClick(object sender, RoutedEventArgs e)
    {
        var isRunning = _supervisor.CurrentState == ServerState.Running;
        var prompt = isRunning ? "Enter your 6-digit PIN to stop server" : "Enter your 6-digit PIN to launch server";
        if (!PromptPin(prompt, isRunning ? "Stop ElectroERP" : "Launch ElectroERP")) return;

        if (isRunning)
        {
            _websiteSupervisor?.StopWebsite();
            _tunnelService.StopTunnel();
            _supervisor.StopServer();
        }
        else
        {
            var (success, error) = _supervisor.StartServer();
            if (!success && !string.IsNullOrEmpty(error))
                MessageBox.Show(error, "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRestartServerClick(object sender, RoutedEventArgs e)
    {
        if (!PromptPin("Enter your 6-digit PIN to restart server", "Restart ElectroERP")) return;
        var (success, error) = _supervisor.RestartServer();
        if (!success && !string.IsNullOrEmpty(error))
            MessageBox.Show(error, "Restart Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnRestartAllClick(object sender, RoutedEventArgs e)
    {
        if (!PromptPin("Enter your 6-digit PIN to restart running services", "Restart All Services")) return;

        var wasWebsiteRunning = _websiteSupervisor?.CurrentState == ServerState.Running;
        var wasTunnelOnline = _tunnelService.IsOnline;

        if (wasWebsiteRunning) _websiteSupervisor?.StopWebsite();
        if (wasTunnelOnline) _tunnelService.StopTunnel();

        var (success, error) = _supervisor.RestartServer();
        if (!success && !string.IsNullOrEmpty(error))
        {
            MessageBox.Show(error, "Restart Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (wasWebsiteRunning) _websiteSupervisor?.StartWebsite();
        if (wasTunnelOnline) _tunnelService.StartTunnel();
    }

    private void OnOnlineTunnelClick(object sender, RoutedEventArgs e)
    {
        if (_supervisor.CurrentState != ServerState.Running) return;
        var isOnline = _tunnelService.IsOnline;
        var prompt = isOnline
            ? $"Enter your 6-digit PIN to disconnect tunnel '{CloudflareTunnelService.TunnelName}'"
            : $"Enter your 6-digit PIN to launch tunnel '{CloudflareTunnelService.TunnelName}'";
        if (!PromptPin(prompt, isOnline ? "Go Offline" : "Go Online")) return;

        if (isOnline) _tunnelService.StopTunnel();
        else
        {
            var (success, error) = _tunnelService.StartTunnel();
            if (!success && !string.IsNullOrEmpty(error))
                MessageBox.Show(error, "Cloudflare Tunnel Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnWebsiteActionClick(object sender, RoutedEventArgs e)
    {
        if (_websiteSupervisor == null || _supervisor.CurrentState != ServerState.Running) return;
        var isRunning = _websiteSupervisor.CurrentState == ServerState.Running;
        var prompt = isRunning ? "Enter your 6-digit PIN to stop Website" : "Enter your 6-digit PIN to launch Website";
        if (!PromptPin(prompt, isRunning ? "Stop Website" : "Launch Website")) return;

        if (isRunning) _websiteSupervisor.StopWebsite();
        else
        {
            var (success, error) = _websiteSupervisor.StartWebsite();
            if (!success && !string.IsNullOrEmpty(error))
                MessageBox.Show(error, "Website Startup Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs e) => OpenUrl("http://localhost:3000");
    private void OnOpenWebsiteClick(object sender, RoutedEventArgs e) => OpenUrl("http://localhost:3000/store");

    private async void OnCopyOnlineUrlClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = _tunnelService.IsOnline
                ? $"https://{CloudflareTunnelService.TunnelName}.electroerp.app"
                : "http://localhost:3000";
            System.Windows.Clipboard.SetText(url);
            OnlineAccessCard.LinkText = "Copied! ✓";
            await Task.Delay(1500);
            OnlineAccessCard.LinkText = "Copy URL ⧉";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy URL: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnToggleDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        _isDiagnosticsExpanded = !_isDiagnosticsExpanded;
        DiagnosticsChevron.Text = _isDiagnosticsExpanded ? "▾" : "▸";
        DiagnosticsLogBorder.Visibility = _isDiagnosticsExpanded ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnCopyErrorsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(LogOutput.Text ?? string.Empty);
            CopyErrorsButton.Content = "Copied!";
            await Task.Delay(1500);
            CopyErrorsButton.Content = "Copy";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy logs to clipboard: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch web browser: {ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool PromptPin(string prompt, string title)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        return mainWindow != null
            ? mainWindow.ShowModalPin(prompt, title)
            : new PinDialog(_pinAuthService, prompt, title).ShowDialog() == true;
    }

    private void RefreshErrorDisplay()
    {
        var recent = _errorLogService.GetRecentErrors();
        ErrorCountLabel.Text = $"Error diagnostics ({recent.Count} today)";
        if (recent.Count == 0)
        {
            LogOutput.Text = "No runtime errors recorded.";
            LogOutput.Foreground = (Brush)FindResource("TextMutedBrush");
        }
        else
        {
            LogOutput.Text = string.Join(Environment.NewLine, recent.TakeLast(30));
            LogOutput.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }
    }
}
