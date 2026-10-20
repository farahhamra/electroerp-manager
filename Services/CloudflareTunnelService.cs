using System.Diagnostics;
using System.IO;

namespace ElectroERP.Manager.Services;

public class CloudflareTunnelService : IDisposable
{
    public const string TunnelName = "electroerp-prod";

    private readonly ErrorLogService _errorLogService;
    private readonly JobObject _jobObject = new();
    private readonly object _lock = new();
    private Process? _tunnelProcess;
    private bool _isManualStop;

    public bool IsOnline { get; private set; }
    public event Action<bool>? OnlineStateChanged;

    public CloudflareTunnelService(ErrorLogService errorLogService)
    {
        _errorLogService = errorLogService;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTunnel();
    }

    public (bool Success, string? Message) StartTunnel()
    {
        lock (_lock)
        {
            if (IsOnline && _tunnelProcess != null && !_tunnelProcess.HasExited)
            {
                return (false, $"Cloudflare tunnel '{TunnelName}' is already online.");
            }

            try
            {
                _isManualStop = false;
                var executable = ResolveCloudflaredExecutable();

                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"tunnel run {TunnelName}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _tunnelProcess = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                _tunnelProcess.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        _errorLogService.ProcessOutputLine($"[Tunnel] {e.Data}", false);
                    }
                };

                _tunnelProcess.ErrorDataReceived += (_, e) =>
                {
                    // cloudflared writes normal INF logs to stderr; pass isStdErr=false so only real error patterns are flagged
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        _errorLogService.ProcessOutputLine($"[Tunnel] {e.Data}", false);
                    }
                };

                _tunnelProcess.Exited += (_, _) => OnTunnelProcessExited();

                if (!_tunnelProcess.Start())
                {
                    SetOnlineState(false);
                    return (false, $"Failed to start Cloudflare tunnel '{TunnelName}'.");
                }

                try { _jobObject.AssignProcess(_tunnelProcess.Handle); } catch { }

                _tunnelProcess.BeginOutputReadLine();
                _tunnelProcess.BeginErrorReadLine();

                SetOnlineState(true);
                return (true, null);
            }
            catch (Exception ex)
            {
                SetOnlineState(false);
                var msg = $"Failed to launch cloudflared tunnel '{TunnelName}': {ex.Message}";
                _errorLogService.ProcessOutputLine($"[Tunnel] {msg}", true);
                return (false, msg);
            }
        }
    }

    public (bool Success, string? Message) StopTunnel()
    {
        lock (_lock)
        {
            _isManualStop = true;

            try
            {
                if (_tunnelProcess != null && !_tunnelProcess.HasExited)
                {
                    ProcessManagerUtils.KillProcessTree(_tunnelProcess.Id);
                }
            }
            catch (Exception ex)
            {
                _errorLogService.ProcessOutputLine($"[Tunnel] Error stopping tunnel: {ex.Message}", true);
            }
            finally
            {
                _tunnelProcess = null;
                SetOnlineState(false);
            }

            return (true, null);
        }
    }

    private void OnTunnelProcessExited()
    {
        lock (_lock)
        {
            int exitCode = -1;
            try { exitCode = _tunnelProcess?.ExitCode ?? -1; } catch { }

            _tunnelProcess = null;
            var wasManual = _isManualStop;
            SetOnlineState(false);

            if (!wasManual)
            {
                _errorLogService.ProcessOutputLine(
                    $"[Tunnel] Cloudflare tunnel '{TunnelName}' exited unexpectedly with code {exitCode}.",
                    true);
            }
        }
    }

    private static string ResolveCloudflaredExecutable()
    {
        string[] candidatePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cloudflared", "cloudflared.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "cloudflared", "cloudflared.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "cloudflared.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cloudflared", "cloudflared.exe")
        ];

        foreach (var path in candidatePaths)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return "cloudflared";
    }

    private void SetOnlineState(bool isOnline)
    {
        IsOnline = isOnline;
        OnlineStateChanged?.Invoke(isOnline);
    }

    public void Dispose()
    {
        StopTunnel();
        _jobObject.Dispose();
        GC.SuppressFinalize(this);
    }
}
