using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace ElectroERP.Manager.Services;

public class ErrorLogService
{
    private static readonly Regex ErrorPattern = new(
        @"(error|exception|fail|fatal|unhandled|uncaught|syntaxerror|typeerror|prismaclient|econnrefused|address already in use)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _logsDirectory;
    private readonly ConcurrentQueue<string> _recentErrors = new();
    private readonly object _fileLock = new();

    public event Action<string>? ErrorLogged;

    public ErrorLogService(string? customLogsDirectory = null)
    {
        var baseDir = AppContext.BaseDirectory;
        _logsDirectory = customLogsDirectory ?? Path.Combine(baseDir, "logs", "errors");

        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ErrorLogService] Failed to create logs directory: {ex.Message}");
        }
    }

    public IReadOnlyList<string> GetRecentErrors() => _recentErrors.ToArray();

    public void ProcessOutputLine(string? line, bool isStdErr)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // Determine if line represents an error
        bool isError = isStdErr || ErrorPattern.IsMatch(line) || line.TrimStart().StartsWith("at ");
        if (!isError)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var formattedLog = $"[{timestamp}] [ERROR] {line.Trim()}";

        // Store in memory ring buffer (limit to 50 items)
        _recentErrors.Enqueue(formattedLog);
        while (_recentErrors.Count > 50)
        {
            _recentErrors.TryDequeue(out _);
        }

        // Write to daily log file
        WriteToFile(formattedLog);

        // Notify subscribers
        ErrorLogged?.Invoke(formattedLog);
    }

    private void WriteToFile(string logEntry)
    {
        lock (_fileLock)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var filePath = Path.Combine(_logsDirectory, $"errors-{today}.log");

                File.AppendAllText(filePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ErrorLogService] Failed writing to file: {ex.Message}");
            }
        }
    }
}
