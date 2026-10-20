using System.Diagnostics;
using System.IO;
using ElectroERP.Manager.Models;

namespace ElectroERP.Manager.Services;

public class ProcessSupervisorService : IDisposable
{
    private const int MaxCrashRetries = 5;
    private static readonly int[] CrashRetryDelaysSeconds = [0, 5, 5, 5, 7];

    private readonly SettingsService _settingsService;
    private readonly ErrorLogService _errorLogService;
    private readonly JobObject _jobObject = new();
    private Process? _backendProcess;
    private Process? _frontendProcess;
    private bool _isManualStop;
    private int _crashRetryCount;
    private readonly object _stateLock = new();

    public ServerState CurrentState { get; private set; } = ServerState.Stopped;
    public event Action<ServerState>? StateChanged;

    public ProcessSupervisorService(SettingsService settingsService, ErrorLogService errorLogService)
    {
        _settingsService = settingsService;
        _errorLogService = errorLogService;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAllProcesses();
    }

    public (bool Success, string? Message) StartServer() => StartServerInternal(isAutoRetry: false);

    private (bool Success, string? Message) StartServerInternal(bool isAutoRetry)
    {
        lock (_stateLock)
        {
            if (CurrentState == ServerState.Running || CurrentState == ServerState.Starting)
            {
                return (false, "Application is already running.");
            }

            if (!isAutoRetry)
            {
                _crashRetryCount = 0;
            }

            var config = _settingsService.LoadSettings();
            var serverDir = Path.GetFullPath(config.ServerDirectory);
            var frontendDir = Path.GetFullPath(config.FrontendDirectory);

            // 1. Validate Backend
            if (!Directory.Exists(serverDir))
            {
                var msg = $"Backend directory not found: {serverDir}";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            var backendBuildFile = Path.Combine(serverDir, "dist", "index.js");
            if (!File.Exists(backendBuildFile))
            {
                var msg = $"Backend build entry point not found at '{backendBuildFile}'. Please run 'npm run build' in the backend directory.";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            // 2. Validate Frontend
            if (!Directory.Exists(frontendDir))
            {
                var msg = $"Frontend directory not found: {frontendDir}";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            var frontendBuildDir = Path.Combine(frontendDir, ".next");
            if (!Directory.Exists(frontendBuildDir))
            {
                var msg = $"Frontend production build not found at '{frontendBuildDir}'. Please run 'npm run build' in the frontend directory.";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            var nextBinPath = Path.Combine(frontendDir, "node_modules", "next", "dist", "bin", "next");
            if (!File.Exists(nextBinPath))
            {
                var msg = $"Next.js binary not found at '{nextBinPath}'. Please run 'npm install' in the frontend directory.";
                _errorLogService.ProcessOutputLine(msg, true);
                return (false, msg);
            }

            try
            {
                SetState(ServerState.Starting);
                _isManualStop = false;

                // Ensure ports are clear of any lingering processes
                ProcessManagerUtils.KillProcessOnPort(5000);
                ProcessManagerUtils.KillProcessOnPort(3000);

                // Launch Backend Process
                var backendInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "dist/index.js",
                    WorkingDirectory = serverDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                backendInfo.EnvironmentVariables["NODE_ENV"] = "production";

                _backendProcess = new Process { StartInfo = backendInfo, EnableRaisingEvents = true };
                _backendProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Backend] {e.Data}", false);
                };
                _backendProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Backend] {e.Data}", true);
                };
                _backendProcess.Exited += (s, e) => OnChildProcessExited("Backend", _backendProcess);

                if (!_backendProcess.Start())
                {
                    SetState(ServerState.Stopped);
                    return (false, "Failed to launch Backend process.");
                }

                try { _jobObject.AssignProcess(_backendProcess.Handle); } catch { }

                _backendProcess.BeginOutputReadLine();
                _backendProcess.BeginErrorReadLine();

                // Launch Frontend Process (node node_modules/next/dist/bin/next start)
                var frontendInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"\"{nextBinPath}\" start",
                    WorkingDirectory = frontendDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                frontendInfo.EnvironmentVariables["NODE_ENV"] = "production";

                _frontendProcess = new Process { StartInfo = frontendInfo, EnableRaisingEvents = true };
                _frontendProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Frontend] {e.Data}", false);
                };
                _frontendProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) _errorLogService.ProcessOutputLine($"[Frontend] {e.Data}", true);
                };
                _frontendProcess.Exited += (s, e) => OnChildProcessExited("Frontend", _frontendProcess);

                if (!_frontendProcess.Start())
                {
                    // Clean up backend
                    ProcessManagerUtils.KillProcessTree(_backendProcess.Id);
                    SetState(ServerState.Stopped);
                    return (false, "Failed to launch Frontend process.");
                }

                try { _jobObject.AssignProcess(_frontendProcess.Handle); } catch { }

                _frontendProcess.BeginOutputReadLine();
                _frontendProcess.BeginErrorReadLine();

                // State remains Starting until BOTH ports 5000 (Backend) and 3000 (Frontend) respond
                WaitForBothServicesReadyAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                StopAllProcesses();
                SetState(ServerState.Stopped);
                var err = $"Failed to start application: {ex.Message}";
                _errorLogService.ProcessOutputLine(err, true);
                return (false, err);
            }
        }
    }

    public (bool Success, string? Message) StopServer()
    {
        lock (_stateLock)
        {
            if (CurrentState == ServerState.Stopped || CurrentState == ServerState.Stopping)
            {
                return (false, "Application is not running.");
            }

            SetState(ServerState.Stopping);
            _isManualStop = true;
            _crashRetryCount = 0;

            try
            {
                StopAllProcesses();
            }
            catch (Exception ex)
            {
                _errorLogService.ProcessOutputLine($"Error stopping processes: {ex.Message}", true);
            }
            finally
            {
                SetState(ServerState.Stopped);
            }

            return (true, null);
        }
    }

    public (bool Success, string? Message) RestartServer()
    {
        lock (_stateLock)
        {
            if (CurrentState == ServerState.Running || CurrentState == ServerState.Starting)
            {
                StopServer();
            }

            return StartServerInternal(isAutoRetry: false);
        }
    }

    private void OnChildProcessExited(string serviceName, Process? process)
    {
        lock (_stateLock)
        {
            var exitCode = -1;
            try { exitCode = process?.ExitCode ?? -1; } catch { }

            if (_isManualStop)
            {
                var backendStopped = _backendProcess == null || _backendProcess.HasExited;
                var frontendStopped = _frontendProcess == null || _frontendProcess.HasExited;
                if (backendStopped && frontendStopped)
                {
                    SetState(ServerState.Stopped);
                }
                return;
            }

            // Ignore sibling exit triggered by StopAllProcesses() during crash cleanup
            if (CurrentState == ServerState.Crashed || CurrentState == ServerState.Stopped || CurrentState == ServerState.Stopping)
            {
                return;
            }

            _errorLogService.ProcessOutputLine($"[Supervisor] {serviceName} process crashed with exit code {exitCode}.", true);
            HandleCrashAndScheduleRecovery();
        }
    }

    private void HandleCrashAndScheduleRecovery()
    {
        SetState(ServerState.Crashed);
        StopAllProcesses();

        var config = _settingsService.LoadSettings();
        if (!config.AutoRestartOnCrash)
        {
            return;
        }

        if (_crashRetryCount >= MaxCrashRetries)
        {
            _errorLogService.ProcessOutputLine(
                $"[Supervisor] Auto-recovery limit reached ({MaxCrashRetries}/{MaxCrashRetries} tries failed). Manual launch required.",
                true);
            return;
        }

        int delaySeconds = CrashRetryDelaysSeconds[_crashRetryCount];
        _crashRetryCount++;
        int attemptNumber = _crashRetryCount;
        int delayMs = delaySeconds > 0 ? delaySeconds * 1000 : 250;

        var scheduleMsg = delaySeconds == 0
            ? $"[Supervisor] Auto-recovery try {attemptNumber}/{MaxCrashRetries} starting now..."
            : $"[Supervisor] Auto-recovery try {attemptNumber}/{MaxCrashRetries} scheduled in {delaySeconds}s...";
        _errorLogService.ProcessOutputLine(scheduleMsg, true);

        Task.Delay(delayMs).ContinueWith(_ =>
        {
            lock (_stateLock)
            {
                if (!_isManualStop && CurrentState == ServerState.Crashed)
                {
                    StartServerInternal(isAutoRetry: true);
                }
            }
        });
    }

    private void StopAllProcesses()
    {
        if (_backendProcess != null && !_backendProcess.HasExited)
        {
            try { ProcessManagerUtils.KillProcessTree(_backendProcess.Id); } catch { }
            _backendProcess = null;
        }

        if (_frontendProcess != null && !_frontendProcess.HasExited)
        {
            try { ProcessManagerUtils.KillProcessTree(_frontendProcess.Id); } catch { }
            _frontendProcess = null;
        }

        ProcessManagerUtils.KillProcessOnPort(5000);
        ProcessManagerUtils.KillProcessOnPort(3000);
    }

    private void WaitForBothServicesReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            var startedAt = DateTime.UtcNow;
            var maxWait = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - startedAt < maxWait)
            {
                if (_isManualStop) return;

                if ((_backendProcess == null || _backendProcess.HasExited) ||
                    (_frontendProcess == null || _frontendProcess.HasExited))
                {
                    return;
                }

                var backendOk = await PortChecker.IsPortListeningAsync("127.0.0.1", 5000);
                var frontendOk = await PortChecker.IsPortListeningAsync("127.0.0.1", 3000);

                if (backendOk && frontendOk)
                {
                    lock (_stateLock)
                    {
                        if (!_isManualStop && CurrentState == ServerState.Starting)
                        {
                            SetState(ServerState.Running);
                            _ = ResetCrashCountAfterStableRunAsync();
                        }
                    }
                    return;
                }

                await Task.Delay(400);
            }

            lock (_stateLock)
            {
                if (!_isManualStop && CurrentState == ServerState.Starting)
                {
                    _errorLogService.ProcessOutputLine("[Supervisor] Startup timeout: services did not both become reachable on ports 5000 and 3000.", true);
                    HandleCrashAndScheduleRecovery();
                }
            }
        });
    }

    private async Task ResetCrashCountAfterStableRunAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(60));
        lock (_stateLock)
        {
            if (!_isManualStop && CurrentState == ServerState.Running)
            {
                _crashRetryCount = 0;
            }
        }
    }

    private void SetState(ServerState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        StopAllProcesses();
        _jobObject.Dispose();
        GC.SuppressFinalize(this);
    }
}
