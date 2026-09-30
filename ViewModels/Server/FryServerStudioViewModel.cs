using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;

public partial class FryServerStudioViewModel : ObservableObject, IDisposable
{
    private readonly IFryHttpServerEngine _engine;
    private readonly IPortAvailabilityService _portService;
    private readonly IScriptStorageService? _storageService;
    private readonly Action? _backToHubAction;
    private readonly Action? _backToHomeAction;

    [ObservableProperty]
    private GridLength _sideBarGridLength = new(260);

    [ObservableProperty]
    private GridLength _bottomDeckGridLength = new(180);

    public int EndpointCount => Cells.Count(c => c.IsEndpoint);
    public int MiddlewareCount => Cells.Count(c => c.Type == FryServerCellType.Middleware);
    public int StartupCount => Cells.Count(c => c.Type == FryServerCellType.Startup);
    public int ScenarioCount => Cells.Count(c => c.Type == FryServerCellType.Scenario);
    public double AverageLatencyMs => TrafficLog.Count > 0 ? TrafficLog.Average(t => t.ElapsedMilliseconds) : 0;

    [ObservableProperty]
    private FryServerDocumentItem _document;

    [ObservableProperty]
    private string _documentTitle = "API Server";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private FryServerCellViewModel? _activeCell;

    public ObservableCollection<FryServerCellViewModel> Cells { get; } = new();
    public ObservableCollection<FryServerTrafficLogItem> TrafficLog { get; } = new();

    // 5-Zone VS Code Layout properties
    [ObservableProperty]
    private int _selectedActivityBarIndex = 0; // 0 = Endpoints Outline, 1 = Traffic Logs, 2 = Explorer

    [ObservableProperty]
    private bool _isSideBarVisible = true;

    [ObservableProperty]
    private string _sideBarTitle = "OUTLINE";

    public bool IsEndpointsOutlineActive => SelectedActivityBarIndex == 0;
    public bool IsTrafficActive => SelectedActivityBarIndex == 1;
    public bool IsExplorerActive => SelectedActivityBarIndex == 2;

    [RelayCommand]
    public void SelectActivityBarItem(string? indexStr)
    {
        if (int.TryParse(indexStr, out var idx))
        {
            SelectActivityBarItem(idx);
        }
    }

    public void SelectActivityBarItem(int index)
    {
        if (SelectedActivityBarIndex == index && IsSideBarVisible)
        {
            IsSideBarVisible = false;
            return;
        }
        SelectedActivityBarIndex = index;
        IsSideBarVisible = true;
        SideBarTitle = index switch
        {
            0 => "OUTLINE",
            1 => "TRAFFIC LOGS",
            2 => "EXPLORER",
            _ => "SIDEBAR"
        };
        OnPropertyChanged(nameof(IsEndpointsOutlineActive));
        OnPropertyChanged(nameof(IsTrafficActive));
        OnPropertyChanged(nameof(IsExplorerActive));
    }

    [ObservableProperty]
    private bool _isBottomPanelVisible = true;

    [ObservableProperty]
    private int _selectedBottomPanelIndex = 0; // 0 = Traffic Log, 1 = Output, 2 = Problems

    [ObservableProperty]
    private double _editorFontSize = 13.0;

    public string BreadcrumbFolder => "Workspace";
    public string BreadcrumbDocument => $"{DocumentTitle}.fryserver";

    public string ActiveCellBadgeText => ActiveCell != null
        ? (ActiveCell.IsEndpoint ? $"{ActiveCell.Method} {ActiveCell.Route}" : $"{ActiveCell.TypeBadgeText}: {ActiveCell.Title}")
        : "Overview";

    public string ActiveCellTypeIcon => ActiveCell?.Type switch
    {
        FryServerCellType.Endpoint => "Api",
        FryServerCellType.Startup => "DatabasePlus",
        FryServerCellType.Middleware => "FilterVariant",
        FryServerCellType.Scenario => "TestTube",
        FryServerCellType.Background => "ClockOutline",
        FryServerCellType.Markdown => "FileDocumentOutline",
        _ => "ServerNetwork"
    };

    public IBrush ActiveCellTypeColor => ActiveCell?.TypeBadgeBrush ?? SolidColorBrush.Parse("#2F81F7");

    partial void OnActiveCellChanged(FryServerCellViewModel? value)
    {
        OnPropertyChanged(nameof(ActiveCellBadgeText));
        OnPropertyChanged(nameof(ActiveCellTypeIcon));
        OnPropertyChanged(nameof(ActiveCellTypeColor));
    }

    partial void OnDocumentTitleChanged(string value)
    {
        OnPropertyChanged(nameof(BreadcrumbDocument));
    }

    public IFryHttpServerEngine Engine => _engine;

    public FryServerStudioViewModel(
        FryServerDocumentItem? document = null,
        string? filePath = null,
        IFryHttpServerEngine? engine = null,
        IPortAvailabilityService? portService = null,
        IScriptStorageService? storageService = null,
        Action? backToHubAction = null,
        Action? backToHomeAction = null)
    {
        _portService = portService ?? new PortAvailabilityService();
        _engine = engine ?? new FryHttpListenerServerEngine(_portService);
        _storageService = storageService;
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;

        var doc = document ?? CreateDefaultDocument();
        _document = doc;
        _filePath = filePath ?? string.Empty;
        _documentTitle = string.IsNullOrWhiteSpace(doc.Title) ? "API Server" : doc.Title;

        _portInput = doc.ServerConfig.Port;
        _hostInput = doc.ServerConfig.Host;
        _apiPrefixInput = doc.ServerConfig.ApiPrefix;
        _corsEnabled = doc.ServerConfig.EnableCors;

        _engine.StateChanged += OnServerStateChanged;
        _engine.RequestProcessed += OnRequestProcessed;

        PopulateCells();
        _ = CheckPortAvailabilityAsync();
    }

    private void PopulateCells()
    {
        Cells.Clear();
        foreach (var cellItem in Document.Cells)
        {
            var cellVm = new FryServerCellViewModel(cellItem, _engine);
            Cells.Add(cellVm);
        }

        ActiveCell = Cells.FirstOrDefault();
        NotifyCellCounts();
    }

    public void NotifyCellCounts()
    {
        OnPropertyChanged(nameof(EndpointCount));
        OnPropertyChanged(nameof(MiddlewareCount));
        OnPropertyChanged(nameof(StartupCount));
        OnPropertyChanged(nameof(ScenarioCount));
    }

    private static FryServerDocumentItem CreateDefaultDocument()
    {
        return new FryServerDocumentItem
        {
            Title = "API Server",
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = 5000,
                ApiPrefix = "/api",
                EnableCors = true
            },
            Cells = new()
            {
                new FryServerCellItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = FryServerCellType.Endpoint,
                    Title = "Get User Profile",
                    Method = "GET",
                    Route = "/users/{id}",
                    Source = @"
                        var user = new { id, name = ""Alice Vance"", role = ""Engineer"" };
                        return Ok(user);
                    ",
                    TestHarness = new FryServerTestHarnessItem
                    {
                        PathParams = new() { ["id"] = "123" }
                    }
                }
            }
        };
    }

    [RelayCommand]
    public void SelectCell(FryServerCellViewModel? cell)
    {
        if (cell == null) return;
        foreach (var c in Cells) c.IsSelected = false;
        cell.IsSelected = true;
        ActiveCell = cell;
    }

    [RelayCommand]
    public void ToggleSideBar() => IsSideBarVisible = !IsSideBarVisible;

    [RelayCommand]
    public void ToggleBottomPanel() => IsBottomPanelVisible = !IsBottomPanelVisible;

    public void LoadDocument(FryServerDocumentItem document, string? filePath = null)
    {
        _ = _engine.StopAsync();
        Document = document;
        FilePath = filePath ?? string.Empty;
        DocumentTitle = string.IsNullOrWhiteSpace(document.Title) ? "API Server" : document.Title;

        PortInput = document.ServerConfig.Port;
        HostInput = document.ServerConfig.Host;
        ApiPrefixInput = document.ServerConfig.ApiPrefix;
        CorsEnabled = document.ServerConfig.EnableCors;

        PopulateCells();
        _ = CheckPortAvailabilityAsync();
    }

    [RelayCommand]
    public async Task SaveDocumentAsync()
    {
        if (_storageService == null || Document == null) return;
        await _storageService.SaveServerDocumentAsync(Document);
    }

    [RelayCommand]
    public void ReturnToHub() => _backToHubAction?.Invoke();

    [RelayCommand]
    public void ReturnToHome() => _backToHomeAction?.Invoke();

    public void Dispose()
    {
        _engine.StateChanged -= OnServerStateChanged;
        _engine.RequestProcessed -= OnRequestProcessed;
        _ = _engine.StopAsync();
    }
}
