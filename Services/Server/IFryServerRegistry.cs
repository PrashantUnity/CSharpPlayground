using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

/// <summary>
/// Represents an actively running or registered server instance in the workspace.
/// </summary>
public class FryRunningServerItem : ObservableObject
{
    public string ServerId { get; init; } = string.Empty;
    public string DocumentTitle { get; set; } = "API Server";
    public string FilePath { get; set; } = string.Empty;
    public int BoundPort { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public IFryHttpServerEngine Engine { get; init; } = null!;
    public FryServerDocumentItem Document { get; init; } = null!;

    public int TotalRequests => Engine?.TotalRequestsServed ?? 0;
    public ServerLifecycleState State => Engine?.State ?? ServerLifecycleState.Stopped;
    public bool IsRunning => State == ServerLifecycleState.Running;

    public int EndpointCount => Document?.Cells?.Count(c => c.Enabled && c.Type == FryServerCellType.Endpoint) ?? 0;

    public string DisplayLocation => string.IsNullOrWhiteSpace(FilePath) ? "In-Memory Session" : FilePath;

    public string FormattedUptime
    {
        get
        {
            var span = DateTime.UtcNow - StartedAt;
            if (span.TotalSeconds < 60) return "Just started";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m uptime";
            return $"{(int)span.TotalHours}h {span.Minutes}m uptime";
        }
    }

    public void NotifyMetricsChanged()
    {
        OnPropertyChanged(nameof(TotalRequests));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(FormattedUptime));
        OnPropertyChanged(nameof(EndpointCount));
    }
}

/// <summary>
/// Central registry and lifecycle coordinator for active HTTP servers running within the studio.
/// </summary>
public interface IFryServerRegistry
{
    IReadOnlyList<FryRunningServerItem> RunningServers { get; }
    int RunningCount { get; }
    event Action? RunningServersChanged;

    void RegisterServer(FryRunningServerItem item);
    void UnregisterServer(string serverId);
    FryRunningServerItem? GetServer(string serverId);
    FryRunningServerItem? GetServerByPort(int port);
    Task StopServerAsync(string serverId);
    Task StopAllAsync();
}
