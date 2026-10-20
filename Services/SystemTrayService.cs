using System.Drawing;
using System.Windows.Forms;
using ElectroERP.Manager.Models;

namespace ElectroERP.Manager.Services;

public class SystemTrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _serverActionItem;
    private readonly ToolStripMenuItem _exitItem;

    public event Action? OpenRequested;
    public event Action? LaunchRequested;
    public event Action? StopRequested;
    public event Action? ExitRequested;

    public SystemTrayService()
    {
        _openItem = new ToolStripMenuItem("Open ElectroERP Manager", null, (_, _) => OpenRequested?.Invoke())
        {
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };

        _serverActionItem = new ToolStripMenuItem("Launch Server", null, (_, _) =>
        {
            if (_serverActionItem?.Text == "Launch Server")
            {
                LaunchRequested?.Invoke();
            }
            else
            {
                StopRequested?.Invoke();
            }
        });

        _exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_openItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_serverActionItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_exitItem);

        Icon appIcon;
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "electroerp-manager-icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appIcon = new Icon(iconPath);
            }
            else
            {
                appIcon = Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "") ?? SystemIcons.Application;
            }
        }
        catch
        {
            appIcon = SystemIcons.Application;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "ElectroERP Manager",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void UpdateServerState(ServerState state)
    {
        switch (state)
        {
            case ServerState.Running:
                _notifyIcon.Text = "ElectroERP Manager (Running)";
                _serverActionItem.Text = "Stop Server";
                _serverActionItem.Enabled = true;
                break;

            case ServerState.Starting:
                _notifyIcon.Text = "ElectroERP Manager (Starting...)";
                _serverActionItem.Text = "Starting...";
                _serverActionItem.Enabled = false;
                break;

            case ServerState.Stopping:
                _notifyIcon.Text = "ElectroERP Manager (Stopping...)";
                _serverActionItem.Text = "Stopping...";
                _serverActionItem.Enabled = false;
                break;

            case ServerState.Crashed:
                _notifyIcon.Text = "ElectroERP Manager (Crashed)";
                _serverActionItem.Text = "Launch Server";
                _serverActionItem.Enabled = true;
                break;

            case ServerState.Stopped:
            default:
                _notifyIcon.Text = "ElectroERP Manager (Stopped)";
                _serverActionItem.Text = "Launch Server";
                _serverActionItem.Enabled = true;
                break;
        }
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
