using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public enum CellOutputTab
{
    Table,
    Console,
    Inspector,
    Image,
    Widget,
    Chart,
    Html,
    All
}

public partial class NotebookCellViewModel
{
    [ObservableProperty]
    private CellOutputTab _selectedOutputTab = CellOutputTab.Table;

    [ObservableProperty]
    private ChartOptions? _chartOptions;

    [ObservableProperty]
    private bool _hasChartOutput;

    [ObservableProperty]
    private string? _missingVariableName;

    [ObservableProperty]
    private bool _hasMissingVariableError;

    public string MissingVariableHintTitle =>
        !string.IsNullOrEmpty(MissingVariableName)
            ? $"Variable '{MissingVariableName}' not defined in active kernel"
            : "Missing symbol in active kernel";

    public bool HasTextOutput => !string.IsNullOrWhiteSpace(OutputText);

    public int AvailableOutputCount
    {
        get
        {
            int count = 0;
            if (HasTableOutput) count++;
            if (HasTextOutput) count++;
            if (HasInspectorOutput) count++;
            if (HasImageOutput) count++;
            if (HasChartOutput) count++;
            if (HasInteractiveControl || InteractiveControlPlaceholderVisible) count++;
            if (HasHtmlContent) count++;
            return count;
        }
    }

    public bool HasMultipleOutputKinds => AvailableOutputCount > 1;

    // Active visibility for content panels
    public bool IsTableTabActive => HasTableOutput && (SelectedOutputTab == CellOutputTab.Table || SelectedOutputTab == CellOutputTab.All);
    public bool IsConsoleTabActive => HasTextOutput && (SelectedOutputTab == CellOutputTab.Console || SelectedOutputTab == CellOutputTab.All);
    public bool IsInspectorTabActive => HasInspectorOutput && (SelectedOutputTab == CellOutputTab.Inspector || SelectedOutputTab == CellOutputTab.All);
    public bool IsImageTabActive => HasImageOutput && (SelectedOutputTab == CellOutputTab.Image || SelectedOutputTab == CellOutputTab.All);
    public bool IsWidgetTabActive => (HasInteractiveControl || InteractiveControlPlaceholderVisible) && (SelectedOutputTab == CellOutputTab.Widget || SelectedOutputTab == CellOutputTab.All);
    public bool IsChartTabActive => HasChartOutput && (SelectedOutputTab == CellOutputTab.Chart || SelectedOutputTab == CellOutputTab.All);
    public bool IsHtmlTabActive => HasHtmlContent && (SelectedOutputTab == CellOutputTab.Html || SelectedOutputTab == CellOutputTab.All);

    // Selected state for tab buttons
    public bool IsTableTabSelected => SelectedOutputTab == CellOutputTab.Table;
    public bool IsConsoleTabSelected => SelectedOutputTab == CellOutputTab.Console;
    public bool IsInspectorTabSelected => SelectedOutputTab == CellOutputTab.Inspector;
    public bool IsImageTabSelected => SelectedOutputTab == CellOutputTab.Image;
    public bool IsWidgetTabSelected => SelectedOutputTab == CellOutputTab.Widget;
    public bool IsChartTabSelected => SelectedOutputTab == CellOutputTab.Chart;
    public bool IsHtmlTabSelected => SelectedOutputTab == CellOutputTab.Html;
    public bool IsAllTabSelected => SelectedOutputTab == CellOutputTab.All;

    // Badges / summary metadata
    public string TableRowCountText => TableResult != null ? $"{TableResult.Rows.Count}" : string.Empty;

    public string ConsoleLineCountText
    {
        get
        {
            if (string.IsNullOrEmpty(OutputText)) return string.Empty;
            var lines = OutputText.Split('\n').Length;
            return lines <= 1 ? "1" : $"{lines}";
        }
    }

    public string SingleOutputTitle
    {
        get
        {
            if (HasChartOutput) return !string.IsNullOrWhiteSpace(ChartOptions?.Title) ? ChartOptions.Title : "Chart Output";
            if (HasTableOutput) return "Table Output";
            if (HasInspectorOutput) return "Object Inspector";
            if (HasImageOutput) return "Rendered Graphic";
            if (HasInteractiveControl || InteractiveControlPlaceholderVisible) return "Interactive Widget";
            if (HasHtmlContent) return "HTML Output";
            return "Console Output";
        }
    }

    public string SingleOutputIconKind
    {
        get
        {
            if (HasChartOutput) return "ChartLine";
            if (HasTableOutput) return "Table";
            if (HasInspectorOutput) return "CodeJson";
            if (HasImageOutput) return "ImageOutline";
            if (HasInteractiveControl || InteractiveControlPlaceholderVisible) return "ApplicationOutline";
            if (HasHtmlContent) return "LanguageHtml5";
            return "Console";
        }
    }

    public void SetChartOutput(ChartOptions chart)
    {
        ChartOptions = chart;
        HasChartOutput = true;
        HasOutput = true;
        SelectedOutputTab = CellOutputTab.Chart;
        NotifyOutputTabStateChanged();
    }

    partial void OnSelectedOutputTabChanged(CellOutputTab value)
    {
        NotifyOutputTabStateChanged();
    }

    [RelayCommand]
    public void SelectOutputTab(CellOutputTab tab)
    {
        SelectedOutputTab = tab;
    }

    [RelayCommand]
    public void SetOutputTab(string tabName)
    {
        if (Enum.TryParse<CellOutputTab>(tabName, true, out var tab))
        {
            SelectedOutputTab = tab;
        }
    }

    public void NotifyOutputTabStateChanged()
    {
        OnPropertyChanged(nameof(HasTextOutput));
        OnPropertyChanged(nameof(AvailableOutputCount));
        OnPropertyChanged(nameof(HasMultipleOutputKinds));
        OnPropertyChanged(nameof(IsTableTabActive));
        OnPropertyChanged(nameof(IsConsoleTabActive));
        OnPropertyChanged(nameof(IsInspectorTabActive));
        OnPropertyChanged(nameof(IsImageTabActive));
        OnPropertyChanged(nameof(IsWidgetTabActive));
        OnPropertyChanged(nameof(IsChartTabActive));
        OnPropertyChanged(nameof(IsHtmlTabActive));
        OnPropertyChanged(nameof(IsTableTabSelected));
        OnPropertyChanged(nameof(IsConsoleTabSelected));
        OnPropertyChanged(nameof(IsInspectorTabSelected));
        OnPropertyChanged(nameof(IsImageTabSelected));
        OnPropertyChanged(nameof(IsWidgetTabSelected));
        OnPropertyChanged(nameof(IsChartTabSelected));
        OnPropertyChanged(nameof(IsHtmlTabSelected));
        OnPropertyChanged(nameof(IsAllTabSelected));
        OnPropertyChanged(nameof(TableRowCountText));
        OnPropertyChanged(nameof(ConsoleLineCountText));
        OnPropertyChanged(nameof(SingleOutputTitle));
        OnPropertyChanged(nameof(SingleOutputIconKind));
        OnPropertyChanged(nameof(HasMissingVariableError));
        OnPropertyChanged(nameof(MissingVariableHintTitle));
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task RunCellAndAboveAsync()
    {
        if (_runCellsAboveAction != null)
        {
            await _runCellsAboveAction(this);
        }
    }
}
