using System.Net.Sockets;

namespace ElectroERP.Manager.Services;

public static class PortChecker
{
    public static async Task<bool> IsPortListeningAsync(string host, int port, int timeoutMs = 600)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
