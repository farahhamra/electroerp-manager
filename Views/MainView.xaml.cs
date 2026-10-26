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

        _supervisor.StateChanged += OnStateChanged;
        _errorLogService.ErrorLogged += OnErrorLogged;
        _tunnelService.OnlineStateChanged += OnTunnelOnlineChanged;
        if (_websiteSupervisor != null)
        {
            _websiteSupervisor.StateChanged += OnWebsiteStateChanged;
        }

        Loaded += (_, _) =>
        {
            UpdateStateUI(_supervisor.CurrentState);
            UpdateTunnelUI(_tunnelService.IsOnline);
            if (_websiteSupervisor != null)
            {
                UpdateWebsiteUI(_websiteSupervisor.CurrentState);
            }
            RefreshErrorDisplay();
        };
    }

    private void OnWebsiteStateChanged(ServerState state)
    {
        Dispatcher.Invoke(() => UpdateWebsiteUI(state));
    }

    private void OnStateChanged(ServerState state)
    {
        Dispatcher.Invoke(() => UpdateStateUI(state));
    }

    private void OnTunnelOnlineChanged(bool isOnline)
    {
        Dispatcher.Invoke(() => UpdateTunnelUI(isOnline));
    }

    private void OnErrorLogged(string logLine)
    {
        Dispatcher.Invoke(RefreshErrorDisplay);
    }

    private void UpdateStateUI(ServerState state)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var stopActionBrush = (Brush)FindResource("ActionStopBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var primaryTextStyle = (Brush)FindResource("TextPrimaryBrush");
        var secondaryTextStyle = (Brush)FindResource("TextSecondaryBrush");

        var primaryBtnStyle = (Style)FindResource("PrimaryButtonStyle");
        var stopBtnStyle = (Style)FindResource("StopButtonStyle");

        var isRunning = state == ServerState.Running;
        OpenBrowserButton.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        RestartServerButton.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;

        switch (state)
        {
            case ServerState.Running:
                StatusDot.Fill = runningBrush;
                StatusLabel.Text = "Status: Running";
                StatusLabel.Foreground = primaryTextStyle;
                ServerActionButton.Content = "Stop Server";
                ServerActionButton.Style = stopBtnStyle;
                ServerActionButton.IsEnabled = true;
                RestartServerButton.IsEnabled = true;
                break;

            case ServerState.Starting:
                StatusDot.Fill = accentBrush;
                StatusLabel.Text = "Status: Starting...";
                StatusLabel.Foreground = secondaryTextStyle;
                ServerActionButton.Content = "Starting...";
                ServerActionButton.IsEnabled = false;
                break;

            case ServerState.Stopping:
                StatusDot.Fill = stoppedBrush;
                StatusLabel.Text = "Status: Stopping...";
                StatusLabel.Foreground = secondaryTextStyle;
                ServerActionButton.Content = "Stopping...";
                ServerActionButton.IsEnabled = false;
                break;

            case ServerState.Crashed:
                StatusDot.Fill = stopActionBrush;
                StatusLabel.Text = "Status: Crashed";
                StatusLabel.Foreground = stopActionBrush;
                ServerActionButton.Content = "Launch ElectroERP";
                ServerActionButton.Style = primaryBtnStyle;
                ServerActionButton.IsEnabled = true;
                break;

            case ServerState.Stopped:
            default:
                StatusDot.Fill = stoppedBrush;
                StatusLabel.Text = "Status: Stopped";
                StatusLabel.Foreground = secondaryTextStyle;
                ServerActionButton.Content = "Launch ElectroERP";
                ServerActionButton.Style = primaryBtnStyle;
                ServerActionButton.IsEnabled = true;
                break;
        }
    }

    private void UpdateTunnelUI(bool isOnline)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var mutedBrush = (Brush)FindResource("TextMutedBrush");
        var borderMutedBrush = (Brush)FindResource("BorderMutedBrush");

        if (isOnline)
        {
            TunnelStatusDot.Fill = runningBrush;
            TunnelStatusLabel.Text = $"Tunnel ({CloudflareTunnelService.TunnelName}): Online";
            TunnelStatusLabel.Foreground = runningBrush;
            OnlineTunnelButton.Content = "Stop Online";
            OnlineTunnelButton.BorderBrush = runningBrush;
        }
        else
        {
            TunnelStatusDot.Fill = stoppedBrush;
            TunnelStatusLabel.Text = $"Tunnel ({CloudflareTunnelService.TunnelName}): Offline";
            TunnelStatusLabel.Foreground = mutedBrush;
            OnlineTunnelButton.Content = "Go Online";
            OnlineTunnelButton.BorderBrush = borderMutedBrush;
        }
    }

    private void UpdateWebsiteUI(ServerState state)
    {
        var runningBrush = (Brush)FindResource("StatusRunningBrush");
        var stoppedBrush = (Brush)FindResource("StatusStoppedBrush");
        var stopActionBrush = (Brush)FindResource("ActionStopBrush");
        var accentBrush = (Brush)FindResource("AccentBrush");
        var mutedBrush = (Brush)FindResource("TextMutedBrush");
        var borderMutedBrush = (Brush)FindResource("BorderMutedBrush");
        var port = _websiteSupervisor?.GetWebsitePort() ?? 3001;

        var isRunning = state == ServerState.Running;
        OpenWebsiteButton.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;

        switch (state)
        {
            case ServerState.Running:
                WebsiteStatusDot.Fill = runningBrush;
                WebsiteStatusLabel.Text = $"Website (localhost:{port}): Running";
                WebsiteStatusLabel.Foreground = runningBrush;
                WebsiteActionButton.Content = "Stop Website";
                WebsiteActionButton.BorderBrush = runningBrush;
                WebsiteActionButton.IsEnabled = true;
                break;

            case ServerState.Starting:
                WebsiteStatusDot.Fill = accentBrush;
                WebsiteStatusLabel.Text = $"Website (localhost:{port}): Starting...";
                WebsiteStatusLabel.Foreground = mutedBrush;
                WebsiteActionButton.Content = "Starting Website...";
                WebsiteActionButton.IsEnabled = false;
                break;

            case ServerState.Stopping:
                WebsiteStatusDot.Fill = stoppedBrush;
                WebsiteStatusLabel.Text = $"Website (localhost:{port}): Stopping...";
                WebsiteStatusLabel.Foreground = mutedBrush;
                WebsiteActionButton.Content = "Stopping Website...";
                WebsiteActionButton.IsEnabled = false;
                break;

            case ServerState.Crashed:
                WebsiteStatusDot.Fill = stopActionBrush;
                WebsiteStatusLabel.Text = $"Website (localhost:{port}): Crashed";
                WebsiteStatusLabel.Foreground = stopActionBrush;
                WebsiteActionButton.Content = "Launch Website";
                WebsiteActionButton.BorderBrush = borderMutedBrush;
                WebsiteActionButton.IsEnabled = true;
                break;

            case ServerState.Stopped:
            default:
                WebsiteStatusDot.Fill = stoppedBrush;
                WebsiteStatusLabel.Text = $"Website (localhost:{port}): Stopped";
                WebsiteStatusLabel.Foreground = mutedBrush;
                WebsiteActionButton.Content = "Launch Website";
                WebsiteActionButton.BorderBrush = borderMutedBrush;
                WebsiteActionButton.IsEnabled = true;
                break;
        }
    }

    private void OnWebsiteActionClick(object sender, RoutedEventArgs e)
    {
        if (_websiteSupervisor == null) return;

        var isRunning = _websiteSupervisor.CurrentState == ServerState.Running;
        var prompt = isRunning
            ? "Enter your 6-digit PIN to stop Website"
            : "Enter your 6-digit PIN to launch Website";
        var title = isRunning ? "Stop Website" : "Launch Website";

        if (!PromptPin(prompt, title))
        {
            return;
        }

        if (isRunning)
        {
            _websiteSupervisor.StopWebsite();
        }
        else
        {
            var (success, error) = _websiteSupervisor.StartWebsite();
            if (!success && !string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "Website Startup Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OnOpenWebsiteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var port = _websiteSupervisor?.GetWebsitePort() ?? 3001;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://localhost:{port}",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch web browser: {ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://localhost:3000",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch web browser: {ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnServerActionClick(object sender, RoutedEventArgs e)
    {
        var isRunning = _supervisor.CurrentState == ServerState.Running;
        var prompt = isRunning ? "Enter your 6-digit PIN to stop server" : "Enter your 6-digit PIN to launch server";
        var title = isRunning ? "Stop ElectroERP" : "Launch ElectroERP";

        if (!PromptPin(prompt, title))
        {
            return;
        }

        if (isRunning)
        {
            _supervisor.StopServer();
        }
        else
        {
            var (success, error) = _supervisor.StartServer();
            if (!success && !string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OnRestartServerClick(object sender, RoutedEventArgs e)
    {
        if (!PromptPin("Enter your 6-digit PIN to restart server", "Restart ElectroERP"))
        {
            return;
        }

        var (success, error) = _supervisor.RestartServer();
        if (!success && !string.IsNullOrEmpty(error))
        {
            MessageBox.Show(error, "Restart Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnOnlineTunnelClick(object sender, RoutedEventArgs e)
    {
        var isOnline = _tunnelService.IsOnline;
        var prompt = isOnline
            ? $"Enter your 6-digit PIN to disconnect tunnel '{CloudflareTunnelService.TunnelName}'"
            : $"Enter your 6-digit PIN to launch tunnel '{CloudflareTunnelService.TunnelName}'";
        var title = isOnline ? "Stop Online Tunnel" : "Go Online";

        if (!PromptPin(prompt, title))
        {
            return;
        }

        if (isOnline)
        {
            _tunnelService.StopTunnel();
        }
        else
        {
            var (success, error) = _tunnelService.StartTunnel();
            if (!success && !string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "Cloudflare Tunnel Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
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
        ErrorCountLabel.Text = $"{recent.Count} errors today";

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
