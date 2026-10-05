using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
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

    #region Static Fallback Brushes
    private static readonly IBrush FallbackGetBg = new SolidColorBrush(Color.Parse("#162846"));
    private static readonly IBrush FallbackGetFg = new SolidColorBrush(Color.Parse("#60A5FA"));
    private static readonly IBrush FallbackGetBorder = new SolidColorBrush(Color.Parse("#2563EB"));

    private static readonly IBrush FallbackPostBg = new SolidColorBrush(Color.Parse("#123522"));
    private static readonly IBrush FallbackPostFg = new SolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush FallbackPostBorder = new SolidColorBrush(Color.Parse("#22C55E"));

    private static readonly IBrush FallbackPutBg = new SolidColorBrush(Color.Parse("#382A12"));
    private static readonly IBrush FallbackPutFg = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush FallbackPutBorder = new SolidColorBrush(Color.Parse("#D97706"));

    private static readonly IBrush FallbackDeleteBg = new SolidColorBrush(Color.Parse("#3A1519"));
    private static readonly IBrush FallbackDeleteFg = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush FallbackDeleteBorder = new SolidColorBrush(Color.Parse("#DC2626"));

    private static readonly IBrush FallbackPatchBg = new SolidColorBrush(Color.Parse("#2C1542"));
    private static readonly IBrush FallbackPatchFg = new SolidColorBrush(Color.Parse("#C084FC"));
    private static readonly IBrush FallbackPatchBorder = new SolidColorBrush(Color.Parse("#9333EA"));

    private static readonly IBrush FallbackOptionsBg = new SolidColorBrush(Color.Parse("#1E293B"));
    private static readonly IBrush FallbackOptionsFg = new SolidColorBrush(Color.Parse("#94A3B8"));
    private static readonly IBrush FallbackOptionsBorder = new SolidColorBrush(Color.Parse("#475569"));

    private static readonly IBrush FallbackOtherBg = new SolidColorBrush(Color.Parse("#133036"));
    private static readonly IBrush FallbackOtherFg = new SolidColorBrush(Color.Parse("#2DD4BF"));
    private static readonly IBrush FallbackOtherBorder = new SolidColorBrush(Color.Parse("#0D9488"));

    private static readonly IBrush FallbackStartupBg = new SolidColorBrush(Color.Parse("#2E1846"));
    private static readonly IBrush FallbackStartupFg = new SolidColorBrush(Color.Parse("#C084FC"));
    private static readonly IBrush FallbackStartupBorder = new SolidColorBrush(Color.Parse("#9333EA"));

    private static readonly IBrush FallbackMiddlewareBg = new SolidColorBrush(Color.Parse("#122E3B"));
    private static readonly IBrush FallbackMiddlewareFg = new SolidColorBrush(Color.Parse("#38BDF8"));
    private static readonly IBrush FallbackMiddlewareBorder = new SolidColorBrush(Color.Parse("#0284C7"));

    private static readonly IBrush FallbackScenarioBg = new SolidColorBrush(Color.Parse("#183522"));
    private static readonly IBrush FallbackScenarioFg = new SolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush FallbackScenarioBorder = new SolidColorBrush(Color.Parse("#22C55E"));

    private static readonly IBrush FallbackJobBg = new SolidColorBrush(Color.Parse("#382710"));
    private static readonly IBrush FallbackJobFg = new SolidColorBrush(Color.Parse("#FB923C"));
    private static readonly IBrush FallbackJobBorder = new SolidColorBrush(Color.Parse("#EA580C"));

    private static readonly IBrush FallbackDocsBg = new SolidColorBrush(Color.Parse("#3A2A12"));
    private static readonly IBrush FallbackDocsFg = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush FallbackDocsBorder = new SolidColorBrush(Color.Parse("#D97706"));

    private static readonly IBrush FallbackDefaultBg = FallbackOptionsBg;
    private static readonly IBrush FallbackDefaultFg = FallbackOptionsFg;
    private static readonly IBrush FallbackDefaultBorder = FallbackOptionsBorder;

    private static IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (Avalonia.Application.Current != null &&
            Avalonia.Application.Current.TryFindResource(resourceKey, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return fallback;
    }
    #endregion

    public IBrush MethodBadgeBrush => MethodBadgeFgBrush;
    public IBrush TypeBadgeBrush => TypeBadgeFgBrush;

    public IBrush MethodBadgeBgBrush => Method.ToUpperInvariant() switch
    {
        "GET" => ResolveBrush("ServerMethodGetBgBrush", FallbackGetBg),
        "POST" => ResolveBrush("ServerMethodPostBgBrush", FallbackPostBg),
        "PUT" => ResolveBrush("ServerMethodPutBgBrush", FallbackPutBg),
        "DELETE" => ResolveBrush("ServerMethodDeleteBgBrush", FallbackDeleteBg),
        "PATCH" => ResolveBrush("ServerMethodPatchBgBrush", FallbackPatchBg),
        "OPTIONS" or "HEAD" => ResolveBrush("ServerMethodOptionsBgBrush", FallbackOptionsBg),
        _ => ResolveBrush("ServerMethodOtherBgBrush", FallbackOtherBg)
    };

    public IBrush MethodBadgeFgBrush => Method.ToUpperInvariant() switch
    {
        "GET" => ResolveBrush("ServerMethodGetFgBrush", FallbackGetFg),
        "POST" => ResolveBrush("ServerMethodPostFgBrush", FallbackPostFg),
        "PUT" => ResolveBrush("ServerMethodPutFgBrush", FallbackPutFg),
        "DELETE" => ResolveBrush("ServerMethodDeleteFgBrush", FallbackDeleteFg),
        "PATCH" => ResolveBrush("ServerMethodPatchFgBrush", FallbackPatchFg),
        "OPTIONS" or "HEAD" => ResolveBrush("ServerMethodOptionsFgBrush", FallbackOptionsFg),
        _ => ResolveBrush("ServerMethodOtherFgBrush", FallbackOtherFg)
    };

    public IBrush MethodBadgeBorderBrush => Method.ToUpperInvariant() switch
    {
        "GET" => ResolveBrush("ServerMethodGetBorderBrush", FallbackGetBorder),
        "POST" => ResolveBrush("ServerMethodPostBorderBrush", FallbackPostBorder),
        "PUT" => ResolveBrush("ServerMethodPutBorderBrush", FallbackPutBorder),
        "DELETE" => ResolveBrush("ServerMethodDeleteBorderBrush", FallbackDeleteBorder),
        "PATCH" => ResolveBrush("ServerMethodPatchBorderBrush", FallbackPatchBorder),
        "OPTIONS" or "HEAD" => ResolveBrush("ServerMethodOptionsBorderBrush", FallbackOptionsBorder),
        _ => ResolveBrush("ServerMethodOtherBorderBrush", FallbackOtherBorder)
    };

    public IBrush TypeBadgeBgBrush => Type switch
    {
        FryServerCellType.Endpoint => MethodBadgeBgBrush,
        FryServerCellType.Startup => ResolveBrush("ServerTypeStartupBgBrush", FallbackStartupBg),
        FryServerCellType.Middleware => ResolveBrush("ServerTypeMiddlewareBgBrush", FallbackMiddlewareBg),
        FryServerCellType.Scenario => ResolveBrush("ServerTypeScenarioBgBrush", FallbackScenarioBg),
        FryServerCellType.Background => ResolveBrush("ServerTypeJobBgBrush", FallbackJobBg),
        FryServerCellType.Markdown => ResolveBrush("ServerTypeDocsBgBrush", FallbackDocsBg),
        _ => ResolveBrush("ServerMethodOptionsBgBrush", FallbackDefaultBg)
    };

    public IBrush TypeBadgeFgBrush => Type switch
    {
        FryServerCellType.Endpoint => MethodBadgeFgBrush,
        FryServerCellType.Startup => ResolveBrush("ServerTypeStartupFgBrush", FallbackStartupFg),
        FryServerCellType.Middleware => ResolveBrush("ServerTypeMiddlewareFgBrush", FallbackMiddlewareFg),
        FryServerCellType.Scenario => ResolveBrush("ServerTypeScenarioFgBrush", FallbackScenarioFg),
        FryServerCellType.Background => ResolveBrush("ServerTypeJobFgBrush", FallbackJobFg),
        FryServerCellType.Markdown => ResolveBrush("ServerTypeDocsFgBrush", FallbackDocsFg),
        _ => ResolveBrush("ServerMethodOptionsFgBrush", FallbackDefaultFg)
    };

    public IBrush TypeBadgeBorderBrush => Type switch
    {
        FryServerCellType.Endpoint => MethodBadgeBorderBrush,
        FryServerCellType.Startup => ResolveBrush("ServerTypeStartupBorderBrush", FallbackStartupBorder),
        FryServerCellType.Middleware => ResolveBrush("ServerTypeMiddlewareBorderBrush", FallbackMiddlewareBorder),
        FryServerCellType.Scenario => ResolveBrush("ServerTypeScenarioBorderBrush", FallbackScenarioBorder),
        FryServerCellType.Background => ResolveBrush("ServerTypeJobBorderBrush", FallbackJobBorder),
        FryServerCellType.Markdown => ResolveBrush("ServerTypeDocsBorderBrush", FallbackDocsBorder),
        _ => ResolveBrush("ServerMethodOptionsBorderBrush", FallbackDefaultBorder)
    };

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
        OnPropertyChanged(nameof(TypeBadgeBrush));
        OnPropertyChanged(nameof(TypeBadgeText));
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
        OnPropertyChanged(nameof(MethodBadgeBrush));
        OnPropertyChanged(nameof(TypeBadgeBrush));
        OnPropertyChanged(nameof(TypeBadgeText));
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
