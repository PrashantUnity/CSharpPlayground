using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public enum ServerLifecycleState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public interface IFryHttpServerEngine : IAsyncDisposable
{
    ServerLifecycleState State { get; }
    int BoundPort { get; }
    string BaseUrl { get; }
    int TotalRequestsServed { get; }
    IDictionary<string, object> SharedState { get; }
    IReadOnlyList<FryServerTrafficLogItem> TrafficLog { get; }

    event Action<ServerLifecycleState>? StateChanged;
    event Action<FryServerTrafficLogItem>? RequestProcessed;

    Task StartAsync(FryServerDocumentItem document, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(FryServerDocumentItem document, CancellationToken cancellationToken = default);

    Task<IServerResult> ExecuteLoopbackTestAsync(FryServerCellItem cell, FryServerTestHarnessItem testHarness, CancellationToken cancellationToken = default);
    Task InvalidateCellCompilationAsync(FryServerCellItem cell, CancellationToken cancellationToken = default);
    void RefreshRoutes();
}
