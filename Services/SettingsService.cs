using System.IO;
using System.Text.Json;
using ElectroERP.Manager.Models;

namespace ElectroERP.Manager.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _configFilePath;
    private readonly object _lock = new();

    public SettingsService(string? customPath = null)
    {
        var baseDir = AppContext.BaseDirectory;
        _configFilePath = customPath ?? Path.Combine(baseDir, "config.json");
    }

    public AppConfig LoadSettings()
    {
        lock (_lock)
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        EnsureDirectories(config);
                        return config;
                    }
                }
                catch
                {
                    // Fall back to default if corrupted
                }
            }

            var defaultConfig = CreateDefaultConfig();
            SaveSettings(defaultConfig);
            return defaultConfig;
        }
    }

    public void SaveSettings(AppConfig config)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsService] Failed to save config: {ex.Message}");
            }
        }
    }

    private AppConfig CreateDefaultConfig()
    {
        var config = new AppConfig
        {
            LaunchCommand = "node dist/index.js",
            LaunchOnWindowsStartup = false,
            AutoRestartOnCrash = true
        };

        EnsureDirectories(config);
        return config;
    }

    private void EnsureDirectories(AppConfig config)
    {
        EnsureServerDirectory(config);
        EnsureFrontendDirectory(config);
    }

    private void EnsureServerDirectory(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ServerDirectory) && Directory.Exists(config.ServerDirectory))
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        // Probe common relative locations
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "backend")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "backend")),
            Path.GetFullPath(Path.Combine(baseDir, "backend")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "backend")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "backend"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                config.ServerDirectory = candidate;
                return;
            }
        }

        // Fallback default
        config.ServerDirectory = Path.GetFullPath(Path.Combine(baseDir, "..", "backend"));
    }

    private void EnsureFrontendDirectory(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.FrontendDirectory) && Directory.Exists(config.FrontendDirectory))
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        // Probe common relative locations
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "frontend")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "frontend")),
            Path.GetFullPath(Path.Combine(baseDir, "frontend")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                config.FrontendDirectory = candidate;
                return;
            }
        }

        // Fallback default
        config.FrontendDirectory = Path.GetFullPath(Path.Combine(baseDir, "..", "frontend"));
    }
}
