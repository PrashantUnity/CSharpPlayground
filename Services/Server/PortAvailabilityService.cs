using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public class PortAvailabilityService : IPortAvailabilityService
{
    public Task<PortStatusResult> CheckPortStatusAsync(int port, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            if (port is < 1 or > 65535)
            {
                return new PortStatusResult(port, PortState.Invalid, Message: $"Port {port} is out of valid range (1-65535).");
            }

            var isAvailable = IsPortAvailableInternal(port);
            if (isAvailable)
            {
                return new PortStatusResult(port, PortState.Available, Message: $"Port {port} is available.");
            }

            var suggested = await FindNextAvailablePortAsync(port + 1, 50, cancellationToken).ConfigureAwait(false);
            var message = suggested.HasValue
                ? $"Port {port} is currently in use. Suggested available port: {suggested.Value}."
                : $"Port {port} is currently in use.";

            return new PortStatusResult(port, PortState.InUse, suggested, message);
        }, cancellationToken);
    }

    public Task<int?> FindNextAvailablePortAsync(int startingPort, int maxScan = 50, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var start = Math.Clamp(startingPort, 1, 65535);
            var end = Math.Min(start + maxScan, 65535);

            for (var p = start; p <= end; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsPortAvailableInternal(p))
                {
                    return (int?)p;
                }
            }

            return null;
        }, cancellationToken);
    }

    private static bool IsPortAvailableInternal(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
            if (tcpListeners.Any(endpoint => endpoint.Port == port))
            {
                return false;
            }

            // Perform direct socket bind check on Loopback
            using var listener = new TcpListener(IPAddress.Loopback, port);
            if (OperatingSystem.IsWindows())
            {
                listener.ExclusiveAddressUse = true;
            }
            else
            {
                listener.ExclusiveAddressUse = false;
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}
