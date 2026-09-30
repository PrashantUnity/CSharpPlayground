using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;

public partial class FryServerStudioViewModel
{
    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private bool _isServerStarting;

    [ObservableProperty]
    private string _serverStatusText = "Stopped";

    [ObservableProperty]
    private int _boundPort = 5000;

    [ObservableProperty]
    private string _baseUrl = "http://localhost:5000/api";

    [ObservableProperty]
    private string _totalRequestsText = "0 requests";

    // Server configuration fields
    [ObservableProperty]
    private int _portInput = 5000;

    [ObservableProperty]
    private string _hostInput = "localhost";

    [ObservableProperty]
    private string _apiPrefixInput = "/api";

    [ObservableProperty]
    private bool _corsEnabled = true;

    // Port availability & conflict detection
    [ObservableProperty]
    private bool _isPortAvailable = true;

    [ObservableProperty]
    private bool _hasPortConflict;

    public bool ShowPortConflict => !IsServerRunning && HasPortConflict;

    partial void OnHasPortConflictChanged(bool value) => OnPropertyChanged(nameof(ShowPortConflict));
    partial void OnIsServerRunningChanged(bool value) => OnPropertyChanged(nameof(ShowPortConflict));

    [ObservableProperty]
    private int? _suggestedPort;

    [ObservableProperty]
    private string _portAvailabilityMessage = string.Empty;

    partial void OnPortInputChanged(int value)
    {
        Document.ServerConfig.Port = value;
        _ = CheckPortAvailabilityAsync();
    }

    partial void OnHostInputChanged(string value) => Document.ServerConfig.Host = value;
    partial void OnApiPrefixInputChanged(string value) => Document.ServerConfig.ApiPrefix = value;
    partial void OnCorsEnabledChanged(bool value) => Document.ServerConfig.EnableCors = value;

    [RelayCommand]
    public async Task CheckPortAvailabilityAsync()
    {
        var result = await _portService.CheckPortStatusAsync(PortInput).ConfigureAwait(false);

        Dispatcher.UIThread.Post(() =>
        {
            IsPortAvailable = result.State == PortState.Available;
            HasPortConflict = result.State == PortState.InUse;
            SuggestedPort = result.SuggestedPort;
            PortAvailabilityMessage = result.Message ?? string.Empty;
        });
    }

    [RelayCommand]
    public void ApplySuggestedPort()
    {
        if (SuggestedPort.HasValue)
        {
            PortInput = SuggestedPort.Value;
        }
    }

    [RelayCommand]
    public async Task StartServerAsync()
    {
        if (IsServerRunning || IsServerStarting) return;

        IsServerStarting = true;
        ServerStatusText = "Starting…";

        try
        {
            Document.ServerConfig.Port = PortInput;
            Document.ServerConfig.Host = HostInput;
            Document.ServerConfig.ApiPrefix = ApiPrefixInput;
            Document.ServerConfig.EnableCors = CorsEnabled;

            await _engine.StartAsync(Document).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                BoundPort = _engine.BoundPort;
                BaseUrl = _engine.BaseUrl;
                IsServerRunning = true;
                IsServerStarting = false;
                ServerStatusText = $"Listening on :{_engine.BoundPort}";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsServerRunning = false;
                IsServerStarting = false;
                ServerStatusText = $"Error: {ex.Message}";
            });
        }
    }

    [RelayCommand]
    public async Task StopServerAsync()
    {
        ServerStatusText = "Stopping…";
        await _engine.StopAsync().ConfigureAwait(false);

        Dispatcher.UIThread.Post(() =>
        {
            IsServerRunning = false;
            IsServerStarting = false;
            ServerStatusText = "Stopped";
        });
    }

    [RelayCommand]
    public async Task RestartServerAsync()
    {
        await StopServerAsync().ConfigureAwait(false);
        await StartServerAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    public void ClearTrafficLog()
    {
        TrafficLog.Clear();
    }

    [RelayCommand]
    public async Task CopyBaseUrlAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(BaseUrl).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ExportMinimalApiAsync()
    {
        var code = FryServerAspNetCoreExporter.ExportToMinimalApiProgramCs(Document);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(code).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ExportOpenApiAsync()
    {
        var json = FryServerOpenApiExporter.GenerateOpenApiJson(Document);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(json).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ExportCurlAsync()
    {
        var sb = new StringBuilder();
        foreach (var cell in Document.Cells.Where(c => c.Enabled && c.Type == FryServerCellType.Endpoint))
        {
            sb.AppendLine(FryServerCurlGenerator.GenerateCurlCommand(Document.ServerConfig, cell));
        }
        var curl = sb.ToString();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(curl).ConfigureAwait(false);
        }
    }

    private void OnServerStateChanged(ServerLifecycleState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsServerRunning = state == ServerLifecycleState.Running;
            IsServerStarting = state == ServerLifecycleState.Starting;

            ServerStatusText = state switch
            {
                ServerLifecycleState.Running => $"Listening on :{_engine.BoundPort}",
                ServerLifecycleState.Starting => "Starting…",
                ServerLifecycleState.Stopping => "Stopping…",
                ServerLifecycleState.Faulted => "Faulted",
                _ => "Stopped"
            };

            if (state == ServerLifecycleState.Running)
            {
                BoundPort = _engine.BoundPort;
                BaseUrl = _engine.BaseUrl;
            }
        });
    }

    private void OnRequestProcessed(FryServerTrafficLogItem logItem)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TrafficLog.Insert(0, logItem);
            if (TrafficLog.Count > 300)
            {
                TrafficLog.RemoveAt(TrafficLog.Count - 1);
            }

            TotalRequestsText = $"{_engine.TotalRequestsServed} requests";
        });
    }
}
