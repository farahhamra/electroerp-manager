using System.Diagnostics;
using System.IO;
using ElectroERP.Manager.Models;

namespace ElectroERP.Manager.Services;

public class WebsiteSupervisorService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ErrorLogService _errorLogService;
    private readonly JobObject _jobObject = new();
    private Process? _websiteProcess;
    private bool _isManualStop;
    private readonly object _stateLock = new();

    public ServerState CurrentState { get; private set; } = ServerState.Stopped;
    public event Action<ServerState>? StateChanged;

    public WebsiteSupervisorService(SettingsService settingsService, ErrorLogService errorLogService)
    {
        _settingsService = settingsService;
        _errorLogService = errorLogService;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopWebsiteProcess();
    }

    public int GetWebsitePort()
    {
        var config = _settingsService.LoadSettings();
        return config.WebsitePort > 0 ? config.WebsitePort : 3001;
    }

    public (bool Success, string? Message) StartWebsite()
    {
        lock (_stateLock)
        {
            if (CurrentState == ServerState.Running || CurrentState == ServerState.Starting)
            {
                return (false, "Website is already running.");
            }

            var config = _settingsService.LoadSettings();
            var websiteDir = Path.GetFullPath(config.WebsiteDirectory);
            var port = config.WebsitePort > 0 ? config.WebsitePort : 3001;

            if (!Directory.Exists(websiteDir))
            {
                var msg = $"Website directory not found: {websiteDir}";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            var viteBinPath = Path.Combine(websiteDir, "node_modules", "vite", "bin", "vite.js");
            if (!File.Exists(viteBinPath))
            {
                var msg = $"Vite binary not found at '{viteBinPath}'. Please run 'npm install' in the electroerp-website directory.";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            try
            {
                SetState(ServerState.Starting);
                _isManualStop = false;

                ProcessManagerUtils.KillProcessOnPort(port);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"\"{viteBinPath}\" --port {port} --host localhost --strictPort",
                    WorkingDirectory = websiteDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _websiteProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _websiteProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Website] {e.Data}", false);
                };
                _websiteProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Website] {e.Data}", true);
                };
                _websiteProcess.Exited += (_, _) => OnWebsiteProcessExited();

                if (!_websiteProcess.Start())
                {
                    SetState(ServerState.Stopped);
                    return (false, "Failed to launch Website process.");
                }

                try { _jobObject.AssignProcess(_websiteProcess.Handle); } catch { }

                _websiteProcess.BeginOutputReadLine();
                _websiteProcess.BeginErrorReadLine();

                WaitForWebsiteReadyAsync(port);

                return (true, null);
            }
            catch (Exception ex)
            {
                StopWebsiteProcess();
                SetState(ServerState.Stopped);
                var err = $"Failed to start Website: {ex.Message}";
                _errorLogService.ProcessOutputLine(err, true);
                return (false, err);
            }
        }
    }

    public (bool Success, string? Message) StopWebsite()
    {
        lock (_stateLock)
        {
            if (CurrentState == ServerState.Stopped || CurrentState == ServerState.Stopping)
            {
                return (false, "Website is not running.");
            }

            SetState(ServerState.Stopping);
            _isManualStop = true;

            try
            {
                StopWebsiteProcess();
            }
            catch (Exception ex)
            {
                _errorLogService.ProcessOutputLine($"Error stopping Website process: {ex.Message}", true);
            }
            finally
            {
                SetState(ServerState.Stopped);
            }

            return (true, null);
        }
    }

    private void OnWebsiteProcessExited()
    {
        lock (_stateLock)
        {
            if (_isManualStop)
            {
                SetState(ServerState.Stopped);
                return;
            }

            if (CurrentState == ServerState.Stopped || CurrentState == ServerState.Stopping)
            {
                return;
            }

            var exitCode = -1;
            try { exitCode = _websiteProcess?.ExitCode ?? -1; } catch { }
            _errorLogService.ProcessOutputLine($"[Website] Process exited unexpectedly with code {exitCode}.", true);
            SetState(ServerState.Crashed);
            StopWebsiteProcess();
        }
    }

    private void StopWebsiteProcess()
    {
        var port = GetWebsitePort();
        if (_websiteProcess != null && !_websiteProcess.HasExited)
        {
            try { ProcessManagerUtils.KillProcessTree(_websiteProcess.Id); } catch { }
            _websiteProcess = null;
        }

        ProcessManagerUtils.KillProcessOnPort(port);
    }

    private void WaitForWebsiteReadyAsync(int port)
    {
        _ = Task.Run(async () =>
        {
            var startedAt = DateTime.UtcNow;
            var maxWait = TimeSpan.FromSeconds(25);

            while (DateTime.UtcNow - startedAt < maxWait)
            {
                if (_isManualStop) return;
                if (_websiteProcess == null || _websiteProcess.HasExited) return;

                var ok = await PortChecker.IsPortListeningAsync("127.0.0.1", port);
                if (ok)
                {
                    lock (_stateLock)
                    {
                        if (!_isManualStop && CurrentState == ServerState.Starting)
                        {
                            SetState(ServerState.Running);
                        }
                    }
                    return;
                }

                await Task.Delay(350);
            }

            lock (_stateLock)
            {
                if (!_isManualStop && CurrentState == ServerState.Starting)
                {
                    _errorLogService.ProcessOutputLine($"[Website] Startup timeout: port {port} did not become reachable.", true);
                    SetState(ServerState.Crashed);
                    StopWebsiteProcess();
                }
            }
        });
    }

    private void SetState(ServerState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        StopWebsiteProcess();
        _jobObject.Dispose();
        GC.SuppressFinalize(this);
    }
}
