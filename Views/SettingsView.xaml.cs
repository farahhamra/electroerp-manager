using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElectroERP.Manager.Models;
using ElectroERP.Manager.Services;

namespace ElectroERP.Manager.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly PinAuthService _pinAuthService;
    private readonly StartupService _startupService;
    private readonly BackupService _backupService;
    private readonly ProcessSupervisorService? _supervisor;
    private AppConfig _config;

    public SettingsView(
        SettingsService settingsService,
        PinAuthService pinAuthService,
        StartupService startupService,
        BackupService? backupService = null,
        ProcessSupervisorService? supervisor = null)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _pinAuthService = pinAuthService;
        _startupService = startupService;
        _backupService = backupService ?? new BackupService(settingsService);
        _supervisor = supervisor;
        _config = _settingsService.LoadSettings();

        Loaded += (_, _) => PopulateSettings();
    }

    private void PopulateSettings()
    {
        _config = _settingsService.LoadSettings();

        StartupSwitch.IsChecked = _startupService.IsStartupEnabled();
        AutoRestartSwitch.IsChecked = _config.AutoRestartOnCrash;
        ServerPathBox.Text = _config.ServerDirectory;
        FrontendPathBox.Text = _config.FrontendDirectory;
        WebsitePathBox.Text = _config.WebsiteDirectory;

        UpdateBackupStatusUI();
    }

    private void UpdateBackupStatusUI()
    {
        var (hasBackup, lastDate, _) = _backupService.GetLatestBackupInfo();
        if (hasBackup && lastDate.HasValue)
        {
            BackupStatusLabel.Text = $"Last: {lastDate.Value:yyyy-MM-dd HH:mm}";
            BackupStatusLabel.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }
        else
        {
            BackupStatusLabel.Text = "No backup found";
            BackupStatusLabel.Foreground = (Brush)FindResource("ActionStopBrush");
        }
    }

    private void OnChangePinClick(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        bool changed = mainWindow != null
            ? mainWindow.ShowModalChangePin()
            : new ChangePinDialog(_pinAuthService).ShowDialog() == true;

        if (changed)
        {
            ShowNotice("PIN successfully changed.");
        }
    }

    private void OnBackupClick(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        bool authenticated = mainWindow != null
            ? mainWindow.ShowModalPin("Enter your 6-digit PIN to perform database backup", "Database Backup")
            : new PinDialog(_pinAuthService, "Enter your 6-digit PIN to perform database backup", "Database Backup").ShowDialog() == true;

        if (!authenticated) return;

        BackupButton.IsEnabled = false;
        BackupButton.Content = "Backing up...";

        try
        {
            var (success, msg, redirectUrl) = _backupService.CreateBackupOrGetRedirect(_supervisor!);
            if (success)
            {
                ShowNotice(msg);
                UpdateBackupStatusUI();
            }
            else if (!string.IsNullOrEmpty(redirectUrl))
            {
                ShowNotice("Opening ERP Backup in browser...");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = redirectUrl,
                    UseShellExecute = true
                });
            }
            else
            {
                ShowNotice(msg);
            }
        }
        catch (Exception ex)
        {
            ShowNotice($"Backup failed: {ex.Message}");
        }
        finally
        {
            BackupButton.IsEnabled = true;
            BackupButton.Content = "Backup Now";
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        var isEnabled = StartupSwitch.IsChecked == true;
        _startupService.SetStartup(isEnabled);
        _config.LaunchOnWindowsStartup = isEnabled;
        _settingsService.SaveSettings(_config);

        ShowNotice("Windows startup setting updated.");
    }

    private void OnAutoRestartToggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        _config.AutoRestartOnCrash = AutoRestartSwitch.IsChecked == true;
        _settingsService.SaveSettings(_config);

        ShowNotice("Crash recovery setting updated.");
    }

    private void OnServerPathLostFocus(object sender, RoutedEventArgs e)
    {
        var path = ServerPathBox.Text.Trim();
        if (path != _config.ServerDirectory)
        {
            _config.ServerDirectory = path;
            _settingsService.SaveSettings(_config);
            ShowNotice("Server path saved.");
            UpdateBackupStatusUI();
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select ElectroERP Backend Folder",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_config.ServerDirectory) ? _config.ServerDirectory : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ServerPathBox.Text = dialog.SelectedPath;
            _config.ServerDirectory = dialog.SelectedPath;
            _settingsService.SaveSettings(_config);
            ShowNotice("Server path updated.");
            UpdateBackupStatusUI();
        }
    }

    private void OnFrontendPathLostFocus(object sender, RoutedEventArgs e)
    {
        var path = FrontendPathBox.Text.Trim();
        if (path != _config.FrontendDirectory)
        {
            _config.FrontendDirectory = path;
            _settingsService.SaveSettings(_config);
            ShowNotice("Frontend path saved.");
        }
    }

    private void OnBrowseFrontendClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select ElectroERP Frontend Folder",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_config.FrontendDirectory) ? _config.FrontendDirectory : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FrontendPathBox.Text = dialog.SelectedPath;
            _config.FrontendDirectory = dialog.SelectedPath;
            _settingsService.SaveSettings(_config);
            ShowNotice("Frontend path updated.");
        }
    }

    private void OnWebsitePathLostFocus(object sender, RoutedEventArgs e)
    {
        var path = WebsitePathBox.Text.Trim();
        if (path != _config.WebsiteDirectory)
        {
            _config.WebsiteDirectory = path;
            _settingsService.SaveSettings(_config);
            ShowNotice("Website path saved.");
        }
    }

    private void OnBrowseWebsiteClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Premium Website Engine Folder (electroerp-website)",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_config.WebsiteDirectory) ? _config.WebsiteDirectory : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WebsitePathBox.Text = dialog.SelectedPath;
            _config.WebsiteDirectory = dialog.SelectedPath;
            _settingsService.SaveSettings(_config);
            ShowNotice("Website path updated.");
        }
    }

    private void ShowNotice(string message)
    {
        SaveNoticeLabel.Text = message;
        SaveNoticeLabel.Visibility = Visibility.Visible;

        Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() => SaveNoticeLabel.Visibility = Visibility.Collapsed);
        });
    }
}
