using System.Diagnostics;

namespace ElectroERP.Manager.Services;

public static class ProcessManagerUtils
{
    public static void KillProcessTree(int processId)
    {
        try
        {
            using var killer = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /T /PID {processId}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            killer?.WaitForExit(3000);
        }
        catch
        {
            try { Process.GetProcessById(processId).Kill(); } catch { }
        }
    }

    public static void KillProcessOnPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano -p tcp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 && parts[1].EndsWith($":{port}"))
                {
                    var pidStr = parts[^1];
                    if (int.TryParse(pidStr, out var pid) && pid > 0)
                    {
                        KillProcessTree(pid);
                    }
                }
            }
        }
        catch { }
    }
}
