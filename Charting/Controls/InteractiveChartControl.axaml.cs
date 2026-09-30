using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

public partial class InteractiveChartControl : UserControl
{
    public static readonly StyledProperty<ChartOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractiveChartControl, ChartOptions?>(nameof(Options));

    public ChartOptions? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public ChartViewState ViewState { get; set; } = new();

    private T? Find<T>(string name) where T : Control { try { return this.FindControl<T>(name); } catch { return null; } }
    private ChartCanvasControl? Canvas => Find<ChartCanvasControl>("CanvasControl");

    static InteractiveChartControl() =>
        OptionsProperty.Changed.AddClassHandler<InteractiveChartControl>((x, _) => x.ApplyOptions());

    public InteractiveChartControl() { try { InitializeComponent(); SetupChrome(); WireEvents(); } catch { } }
    public InteractiveChartControl(ChartOptions options) : this() { Options = options; ApplyOptions(); }

    public event EventHandler<ElementClickedEventArgs<ChartHitTestResult>>? ValueClicked;

    private void SetupChrome()
    {
        var chrome = Find<VisualChromeControl>("Chrome");
        var tools = Find<StackPanel>("KindToolsPanel");
        var canvas = Canvas;
        var legend = Find<ItemsControl>("SeriesLegendPanel");

        if (chrome != null)
        {
            if (tools != null) { tools.IsVisible = true; chrome.SetKindTools(tools); }
            if (canvas != null) { canvas.IsVisible = true; chrome.SetCanvasContent(canvas); }
            if (legend != null) chrome.SetFooter(legend);

            chrome.SpecGetter = () => Options != null ? ChartOptionsConverter.ToSpec(Options) : null;
            chrome.DataCsvGetter = () => Options != null ? ChartExportService.ToCsv(Canvas?.GetEffectiveOptions() ?? Options) : null;
            chrome.ResetFitRequested += (_, _) => { ViewState.Reset(); Canvas?.InvalidateVisual(); };
        }
    }

    private void WireEvents()
    {
        if (Canvas is { } canvas) canvas.ValueClicked += (_, e) => ValueClicked?.Invoke(this, e);
        Bind("LineTypeBtn", () => SwitchType(ChartType.Line));
        Bind("AreaTypeBtn", () => SwitchType(ChartType.Area));
        Bind("BarTypeBtn", () => SwitchType(ChartType.Bar));
        Bind("ScatterTypeBtn", () => SwitchType(ChartType.Scatter));
        Bind("PieTypeBtn", () => SwitchType(ChartType.Pie));
        Bind("GridToggleBtn", () => { if (Options != null) { ViewState.OverrideShowGrid = !ViewState.EffectiveShowGrid(Options); Canvas?.InvalidateVisual(); } });
    }

    private void Bind(string name, Action act) { if (Find<Button>(name) is { } b) b.Click += (_, _) => act(); }

    public void ApplyOptions()
    {
        var opts = Options;
        if (opts == null) return;
        var canvas = Canvas;
        if (canvas != null)
        {
            canvas.Options = opts;
            canvas.ViewState = ViewState;
            if (opts.Height > 0) canvas.Height = Math.Max(canvas.MinHeight, opts.Height);
            canvas.InvalidateVisual();
        }
        UpdateHeader();
        ApplyLegend(opts);
    }

    private void SwitchType(ChartType newType)
    {
        if (Options == null) return;
        ViewState.OverrideType = newType;
        UpdateHeader();
        ApplyLegend(Options);
        Canvas?.InvalidateVisual();
    }

    private void UpdateHeader()
    {
        var opts = Options;
        if (opts == null) return;
        var effectiveType = ViewState.EffectiveType(opts);
        var title = string.IsNullOrWhiteSpace(opts.Title) ? $"{effectiveType} Chart" : opts.Title;
        var stats = opts.ShowStats ? ChartStatistics.Of(opts)?.ToString() : null;
        var icon = effectiveType switch
        {
            ChartType.Line => MaterialIconKind.ChartLine,
            ChartType.Area => MaterialIconKind.ChartAreaspline,
            ChartType.Bar or ChartType.Histogram => MaterialIconKind.ChartBar,
            ChartType.Scatter => MaterialIconKind.ChartScatterPlot,
            ChartType.Pie or ChartType.Donut => MaterialIconKind.ChartPie,
            _ => MaterialIconKind.ChartLine
        };
        Find<VisualChromeControl>("Chrome")?.SetHeader(title, opts.Subtitle, stats, opts.Notice, icon);
    }

    private void ApplyLegend(ChartOptions opts)
    {
        var panel = Find<ItemsControl>("SeriesLegendPanel");
        if (panel == null) return;
        panel.Items.Clear();
        var show = opts.ShowLegend && ViewState.EffectiveType(opts) is not (ChartType.Pie or ChartType.Donut);
        panel.IsVisible = show;
        Find<VisualChromeControl>("Chrome")?.SetFooter(show ? panel : null);
        if (!show) return;

        int drawn = 0;
        foreach (var series in opts.Series.Where(s => s.Points.Count > 0))
        {
            var color = ChartPaletteService.ParseColor(string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(drawn++) : series.Color);
            panel.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 0, 14, 2),
                Children = {
                    new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = series.Name, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center }
                }
            });
        }
    }
}
