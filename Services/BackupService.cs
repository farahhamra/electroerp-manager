using System.Diagnostics;
using System.IO;

namespace ElectroERP.Manager.Services;

public class BackupService
{
    private readonly SettingsService _settingsService;

    public BackupService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string GetBackupDirectory()
    {
        var config = _settingsService.LoadSettings();
        var serverDir = Path.GetFullPath(config.ServerDirectory);
        var backupDir = Path.GetFullPath(Path.Combine(serverDir, "..", "private", "backup"));
        if (!Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }
        return backupDir;
    }

    public (bool HasBackup, DateTime? LastBackupDate, string? FileName) GetLatestBackupInfo()
    {
        try
        {
            var dir = GetBackupDirectory();
            var dirInfo = new DirectoryInfo(dir);
            var files = dirInfo.GetFiles("*.*")
                .Where(f => f.Extension.Equals(".etx", StringComparison.OrdinalIgnoreCase)
                         || f.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase)
                         || f.Extension.Equals(".bak", StringComparison.OrdinalIgnoreCase)
                         || f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            if (files.Count > 0)
            {
                var latest = files[0];
                return (true, latest.LastWriteTime, latest.Name);
            }
        }
        catch
        {
            // Ignore access errors
        }

        return (false, null, null);
    }

    public (bool Success, string Message, string? RedirectUrl) CreateBackupOrGetRedirect(ProcessSupervisorService supervisor)
    {
        try
        {
            var dir = GetBackupDirectory();
            var config = _settingsService.LoadSettings();
            var serverDir = Path.GetFullPath(config.ServerDirectory);
            var envPath = Path.Combine(serverDir, ".env");

            var envVars = ParseEnvFile(envPath);
            envVars.TryGetValue("DB_HOST", out var host);
            envVars.TryGetValue("DB_USER", out var user);
            envVars.TryGetValue("DB_PASSWORD", out var pass);
            envVars.TryGetValue("DB_NAME", out var dbName);
            envVars.TryGetValue("DB_PORT", out var portStr);

            host ??= "localhost";
            user ??= "root";
            pass ??= "";
            dbName ??= "electroerp-db";
            if (!int.TryParse(portStr, out var port)) port = 3306;

            var mysqldumpPath = FindMysqldump();
            if (!string.IsNullOrEmpty(mysqldumpPath))
            {
                var count = Directory.GetFiles(dir, "*.*").Length;
                var fileName = $"backup-{DateTime.Now:yyyyMMdd_HHmmss}-{(count + 1):D3}.etx";
                var targetPath = Path.Combine(dir, fileName);

                var startInfo = new ProcessStartInfo
                {
                    FileName = mysqldumpPath,
                    Arguments = $"-h {host} -P {port} -u {user} --password=\"{pass}\" {dbName} --no-create-info --skip-triggers --complete-insert --result-file=\"{targetPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var proc = Process.Start(startInfo);
                proc?.WaitForExit(30000);

                if (proc?.ExitCode == 0 && File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                {
                    return (true, $"Database backup created: {fileName}", null);
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to web app redirect
            Debug.WriteLine($"[BackupService] mysqldump failed: {ex.Message}");
        }

        const string webBackupUrl = "http://localhost:3000/core/batch-io";
        return (false, "Opening web app database backup...", webBackupUrl);
    }

    private static string? FindMysqldump()
    {
        var candidates = new[]
        {
            "mysqldump",
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 5.7\bin\mysqldump.exe",
            @"C:\xampp\mysql\bin\mysqldump.exe"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo { FileName = "mysqldump", Arguments = "--version", CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(1500);
            if (p?.ExitCode == 0) return "mysqldump";
        }
        catch { }

        return null;
    }

    private static Dictionary<string, string> ParseEnvFile(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return dict;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = line.Substring(0, eqIndex).Trim();
                var val = line.Substring(eqIndex + 1).Trim().Trim('\'', '"');
                dict[key] = val;
            }
        }

        return dict;
    }
}
