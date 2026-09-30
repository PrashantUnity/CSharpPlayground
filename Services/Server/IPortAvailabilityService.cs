using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public enum PortState
{
    Available,
    InUse,
    Invalid
}

public record PortStatusResult(int Port, PortState State, int? SuggestedPort = null, string? Message = null);

public interface IPortAvailabilityService
{
    /// <summary>
    /// Checks whether the specified port is available on the local machine.
    /// </summary>
    Task<PortStatusResult> CheckPortStatusAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the next available port starting from <paramref name="startingPort"/>.
    /// </summary>
    Task<int?> FindNextAvailablePortAsync(int startingPort, int maxScan = 50, CancellationToken cancellationToken = default);
}
