using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;

public partial class FryServerCellViewModel : ObservableObject
{
    private readonly IFryHttpServerEngine? _engine;

    public FryServerCellItem Model { get; }

    public string Id => Model.Id;

    [ObservableProperty]
    private FryServerCellType _type;

    [ObservableProperty]
    private string _title = "New Cell";

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _method = "GET";

    [ObservableProperty]
    private string _route = "/";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _source = string.Empty;

    // Test Harness inputs
    [ObservableProperty]
    private string _testPathParamsText = string.Empty;

    [ObservableProperty]
    private string _testQueryParamsText = string.Empty;

    [ObservableProperty]
    private string _testHeadersText = string.Empty;

    [ObservableProperty]
    private string _testBody = string.Empty;

    [ObservableProperty]
    private string _testBodyContentType = "application/json";

    // Telemetry & response display
    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private int? _lastStatusCode;

    [ObservableProperty]
    private string _lastResponseText = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasOutput;

    [ObservableProperty]
    private int _requestCount;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInputCollapsed;

    [ObservableProperty]
    private bool _isTestHarnessExpanded;

    // The badges' colours are theme resources (ServerMethod*/ServerType* in the palette and in every theme): the view
    // resolves these keys from itself (common:ThemeBrush), so they follow the active theme wherever the studio is hosted.
    private string MethodKey => Method.ToUpperInvariant() switch
    {
        "GET" => "ServerMethodGet",
        "POST" => "ServerMethodPost",
        "PUT" => "ServerMethodPut",
        "DELETE" => "ServerMethodDelete",
        "PATCH" => "ServerMethodPatch",
        "OPTIONS" or "HEAD" => "ServerMethodOptions",
        _ => "ServerMethodOther",
    };

    private string TypeKey => Type switch
    {
        FryServerCellType.Endpoint => MethodKey,
        FryServerCellType.Startup => "ServerTypeStartup",
        FryServerCellType.Middleware => "ServerTypeMiddleware",
        FryServerCellType.Scenario => "ServerTypeScenario",
        FryServerCellType.Background => "ServerTypeJob",
        FryServerCellType.Markdown => "ServerTypeDocs",
        _ => "ServerMethodOptions",
    };

    public string MethodBadgeBgKey => MethodKey + "BgBrush";
    public string MethodBadgeFgKey => MethodKey + "FgBrush";
    public string MethodBadgeBorderKey => MethodKey + "BorderBrush";
    public string TypeBadgeBgKey => TypeKey + "BgBrush";
    public string TypeBadgeFgKey => TypeKey + "FgBrush";
    public string TypeBadgeBorderKey => TypeKey + "BorderBrush";

    // A new method or type: every badge colour and the badge text (only two of them used to be announced, so the
    // badge kept its old background and border until the view was rebuilt).
    private void NotifyBadges()
    {
        OnPropertyChanged(nameof(MethodBadgeBgKey));
        OnPropertyChanged(nameof(MethodBadgeFgKey));
        OnPropertyChanged(nameof(MethodBadgeBorderKey));
        OnPropertyChanged(nameof(TypeBadgeBgKey));
        OnPropertyChanged(nameof(TypeBadgeFgKey));
        OnPropertyChanged(nameof(TypeBadgeBorderKey));
        OnPropertyChanged(nameof(TypeBadgeText));
    }

    public string TypeBadgeText => Type switch
    {
        FryServerCellType.Endpoint => Method.ToUpperInvariant(),
        FryServerCellType.Startup => "INIT",
        FryServerCellType.Middleware => "PIPE",
        FryServerCellType.Scenario => "TEST",
        FryServerCellType.Background => "JOB",
        FryServerCellType.Markdown => "DOCS",
        _ => "CELL"
    };

    public bool IsEndpoint => Type == FryServerCellType.Endpoint;
    public bool IsStartup => Type == FryServerCellType.Startup;
    public bool IsMiddleware => Type == FryServerCellType.Middleware;
    public bool IsScenario => Type == FryServerCellType.Scenario;
    public bool IsMarkdown => Type == FryServerCellType.Markdown;

    public string OutlineTooltip => IsEndpoint
        ? $"{Method} {Route}{(string.IsNullOrWhiteSpace(Title) ? "" : $" — {Title}")}"
        : $"{TypeBadgeText}: {Title}";

    public string EnableToggleTooltip => Enabled ? "Active (Click to disable)" : "Disabled (Click to enable)";

    [RelayCommand]
    public void ToggleInputCollapse() => IsInputCollapsed = !IsInputCollapsed;

    [RelayCommand]
    public void ToggleTestHarness() => IsTestHarnessExpanded = !IsTestHarnessExpanded;

    [RelayCommand]
    public void ClearOutput()
    {
        HasOutput = false;
        LastResponseText = string.Empty;
        LastStatusCode = null;
        Model.LastResponseText = null;
        Model.LastStatusCode = null;
    }

    [RelayCommand]
    public void SetMethod(string newMethod)
    {
        Method = newMethod;
    }

    public FryServerCellViewModel(FryServerCellItem model, IFryHttpServerEngine? engine = null)
    {
        Model = model;
        _engine = engine;

        _type = model.Type;
        _title = model.Title;
        _enabled = model.Enabled;
        _method = model.Method;
        _route = model.Route;
        _description = model.Description;
        _source = model.Source;
        _requestCount = model.RequestCount;

        // Initialize test harness fields from model
        _testBody = model.TestHarness.Body;
        _testBodyContentType = model.TestHarness.BodyContentType;
        _testPathParamsText = FormatParamDict(model.TestHarness.PathParams);
        _testQueryParamsText = FormatParamDict(model.TestHarness.QueryParams);
        _testHeadersText = FormatParamDict(model.TestHarness.Headers);

        _lastStatusCode = model.LastStatusCode;
        _lastResponseText = model.LastResponseText ?? string.Empty;
        _hasOutput = !string.IsNullOrEmpty(_lastResponseText) || _lastStatusCode.HasValue;
    }

    partial void OnTypeChanged(FryServerCellType value)
    {
        Model.Type = value;
        OnPropertyChanged(nameof(IsEndpoint));
        OnPropertyChanged(nameof(IsStartup));
        OnPropertyChanged(nameof(IsMiddleware));
        OnPropertyChanged(nameof(IsScenario));
        OnPropertyChanged(nameof(IsMarkdown));
        NotifyBadges();
    }

    partial void OnTitleChanged(string value) => Model.Title = value;
    partial void OnEnabledChanged(bool value)
    {
        Model.Enabled = value;
        _engine?.RefreshRoutes();
    }

    partial void OnMethodChanged(string value)
    {
        Model.Method = value;
        _engine?.RefreshRoutes();
        NotifyBadges();
    }

    partial void OnRouteChanged(string value)
    {
        Model.Route = value;
        _engine?.RefreshRoutes();
    }

    partial void OnDescriptionChanged(string value) => Model.Description = value;

    partial void OnSourceChanged(string value)
    {
        Model.Source = value;
        _engine?.InvalidateCellCompilationAsync(Model);
    }

    partial void OnTestBodyChanged(string value) => Model.TestHarness.Body = value;
    partial void OnTestBodyContentTypeChanged(string value) => Model.TestHarness.BodyContentType = value;

    partial void OnTestPathParamsTextChanged(string value) =>
        Model.TestHarness.PathParams = ParseParamDict(value);

    partial void OnTestQueryParamsTextChanged(string value) =>
        Model.TestHarness.QueryParams = ParseParamDict(value);

    partial void OnTestHeadersTextChanged(string value) =>
        Model.TestHarness.Headers = ParseParamDict(value);

    [RelayCommand]
    public async Task SendTestRequestAsync()
    {
        if (_engine == null) return;

        IsExecuting = true;
        HasError = false;
        ExecutionTimeText = "Running…";

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _engine.ExecuteLoopbackTestAsync(Model, Model.TestHarness).ConfigureAwait(false);
            sw.Stop();

            LastStatusCode = result.StatusCode;
            ExecutionTimeText = $"{sw.Elapsed.TotalMilliseconds:F1}ms";

            if (result is JsonResult jsonRes && jsonRes.Value != null)
            {
                LastResponseText = JsonSerializer.Serialize(jsonRes.Value, new JsonSerializerOptions { WriteIndented = true });
            }
            else if (result is TextResult txtRes)
            {
                LastResponseText = txtRes.Content;
            }
            else if (result is StatusCodeResult scRes && scRes.Error != null)
            {
                LastResponseText = JsonSerializer.Serialize(scRes.Error, new JsonSerializerOptions { WriteIndented = true });
                HasError = result.StatusCode >= 400;
            }
            else
            {
                LastResponseText = $"HTTP {result.StatusCode}";
            }

            Model.LastResponseText = LastResponseText;
            Model.LastStatusCode = LastStatusCode;
            HasOutput = true;
        }
        catch (Exception ex)
        {
            HasError = true;
            LastStatusCode = 500;
            LastResponseText = $"{{\"error\": \"{ex.Message}\"}}";
            ExecutionTimeText = "Failed";
            HasOutput = true;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void ToggleEnabled()
    {
        Enabled = !Enabled;
    }

    private static string FormatParamDict(System.Collections.Generic.Dictionary<string, string> dict) =>
        string.Join(", ", dict.Select(kv => $"{kv.Key}={kv.Value}"));

    private static System.Collections.Generic.Dictionary<string, string> ParseParamDict(string text)
    {
        var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return result;

        var tokens = text.Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var eq = token.IndexOf('=');
            if (eq > 0)
            {
                var k = token[..eq].Trim();
                var v = token[(eq + 1)..].Trim();
                if (!string.IsNullOrEmpty(k)) result[k] = v;
            }
            else if (!string.IsNullOrWhiteSpace(token))
            {
                result[token.Trim()] = string.Empty;
            }
        }
        return result;
    }
}
