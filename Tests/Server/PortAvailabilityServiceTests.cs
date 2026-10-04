using System.Net;
using System.Net.Sockets;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;
using Xunit;

namespace CSharpEditorPlugin.Tests.Server;

public class PortAvailabilityServiceTests
{
    private readonly PortAvailabilityService _service = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public async Task CheckPortStatus_InvalidPorts_ReturnsInvalidState(int invalidPort)
    {
        var result = await _service.CheckPortStatusAsync(invalidPort);

        Assert.Equal(PortState.Invalid, result.State);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task CheckPortStatus_AvailablePort_ReturnsAvailableState()
    {
        // Find a free port using an ephemeral bind
        int freePort;
        using (var temp = new TcpListener(IPAddress.Loopback, 0))
        {
            temp.Start();
            freePort = ((IPEndPoint)temp.LocalEndpoint).Port;
            temp.Stop();
        }

        PortStatusResult result = await _service.CheckPortStatusAsync(freePort);
        for (int i = 0; i < 10 && result.State != PortState.Available; i++)
        {
            await Task.Delay(50);
            result = await _service.CheckPortStatusAsync(freePort);
        }

        Assert.Equal(PortState.Available, result.State);
        Assert.Equal(freePort, result.Port);
    }

    [Fact]
    public async Task CheckPortStatus_OccupiedPort_ReturnsInUseAndSuggestsNextPort()
    {
        // Occupy an ephemeral port
        using var occupiedListener = new TcpListener(IPAddress.Loopback, 0);
        occupiedListener.Start();
        var occupiedPort = ((IPEndPoint)occupiedListener.LocalEndpoint).Port;

        var result = await _service.CheckPortStatusAsync(occupiedPort);

        Assert.Equal(PortState.InUse, result.State);
        Assert.Equal(occupiedPort, result.Port);
        Assert.NotNull(result.SuggestedPort);
        Assert.NotEqual(occupiedPort, result.SuggestedPort.Value);
        Assert.True(result.SuggestedPort.Value > occupiedPort);
    }

    [Fact]
    public async Task FindNextAvailablePort_ReturnsValidPortGreaterThanStart()
    {
        var nextPort = await _service.FindNextAvailablePortAsync(30000, 100);

        Assert.NotNull(nextPort);
        Assert.InRange(nextPort.Value, 30000, 30100);
    }
}
