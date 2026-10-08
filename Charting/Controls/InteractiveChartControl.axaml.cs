using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

public partial class InteractiveChartControl : UserControl
{
    public static readonly StyledProperty<ChartOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractiveChartControl, ChartOptions?>(nameof(Options));

    public ChartOptions? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public ChartViewState ViewState { get; set; } = new();

    private bool _isFullScreenView;

    /// <summary>True for the copy that fills the window (see <see cref="ChartFullScreenOverlay"/>): the chart takes all the room there is.</summary>
    public bool IsFullScreenView
    {
        get => _isFullScreenView;
        set
        {
            _isFullScreenView = value;
            ApplyViewMode();
        }
    }

    public event EventHandler? ExitFullScreenRequested;

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

            chrome.DefaultCanvasHeight = Options?.Height > 100 ? Options.Height : 260;
            chrome.SpecGetter = () => Options != null ? ChartOptionsConverter.ToSpec(Options) : null;
            chrome.DataCsvGetter = () => Options != null ? ChartExportService.ToCsv(Canvas?.GetEffectiveOptions() ?? Options) : null;
            chrome.ResetFitRequested += (_, _) => { ViewState.Reset(); Canvas?.InvalidateVisual(); RefreshView(); };
            chrome.FullscreenRequested += (_, _) => ToggleFullScreen();
        }
    }

    private void WireEvents()
    {
        if (Canvas is { } canvas)
        {
            canvas.ValueClicked += (_, e) => ValueClicked?.Invoke(this, e);
            canvas.ViewReset += (_, _) => RefreshView();
        }
        Bind("StackToggleBtn", ToggleStack);
        Bind("LineTypeBtn", () => SwitchType(ChartType.Line));
        Bind("AreaTypeBtn", () => SwitchType(ChartType.Area));
        Bind("BarTypeBtn", () => SwitchType(ChartType.Bar));
        Bind("ScatterTypeBtn", () => SwitchType(ChartType.Scatter));
        Bind("PieTypeBtn", () => SwitchType(ChartType.Pie));
        Bind("GridToggleBtn", () => { if (Options != null) { ViewState.OverrideShowGrid = !ViewState.EffectiveShowGrid(Options); Canvas?.InvalidateVisual(); } });
    }

    /// <summary>Fills the window with this chart, or leaves full screen when it already does.</summary>
    public void ToggleFullScreen()
    {
        if (IsFullScreenView) ExitFullScreenRequested?.Invoke(this, EventArgs.Empty);
        else ChartFullScreenOverlay.Open(this);
    }

    internal void RaiseValueClicked(ElementClickedEventArgs<ChartHitTestResult> e) => ValueClicked?.Invoke(this, e);

    private void ApplyViewMode()
    {
        VerticalAlignment = IsFullScreenView ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        if (Find<VisualChromeControl>("Chrome") is { } chrome)
        {
            chrome.VerticalAlignment = IsFullScreenView ? VerticalAlignment.Stretch : VerticalAlignment.Top;
            chrome.SetFullScreenState(IsFullScreenView);
        }
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && IsFullScreenView && e.Key == Avalonia.Input.Key.Escape)
        {
            ExitFullScreenRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
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
            canvas.Height = double.NaN;
            canvas.InvalidateVisual();
        }
        if (Find<VisualChromeControl>("Chrome") is { } chrome)
        {
            if (opts.Height > 0) chrome.DefaultCanvasHeight = opts.Height;
        }
        if (IsFullScreenView) ApplyViewMode(); // DefaultCanvasHeight puts the inline height back
        UpdateHeader();
        RefreshView();
    }

    private void SwitchType(ChartType newType)
    {
        if (Options == null) return;
        ViewState.OverrideType = newType;
        UpdateHeader();
        RefreshView();
        Canvas?.InvalidateVisual();
    }

    // What follows the view's own choices (kind, hidden series, stacking): the legend, and whether stacking can be toggled.
    private void RefreshView()
    {
        if (Options == null) return;
        ApplyLegend(Options);
        UpdateStackButton(Options);
    }

    private void ToggleStack()
    {
        if (Options == null) return;
        ViewState.OverrideStack = ViewState.EffectiveStack(Options) == ChartStack.None ? ChartStack.Stacked : ChartStack.None;
        Canvas?.InvalidateVisual();
    }

    // Several bar, line or area series can be piled up.
    private void UpdateStackButton(ChartOptions opts)
    {
        if (Find<Button>("StackToggleBtn") is not { } button) return;
        var kind = ViewState.EffectiveType(opts);
        var stackable = opts.Series.Count(s => s.Points.Count > 0 && (s.Kind ?? kind) is ChartType.Bar or ChartType.Line or ChartType.Area or ChartType.Histogram);
        button.IsVisible = stackable >= 2;
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
            ChartType.Bubble => MaterialIconKind.ChartBubble,
            ChartType.Radar => MaterialIconKind.Radar,
            ChartType.PolarArea => MaterialIconKind.ChartDonutVariant,
            _ => MaterialIconKind.ChartLine
        };
        Find<VisualChromeControl>("Chrome")?.SetHeader(title, opts.Subtitle, stats, opts.Notice, icon);
    }

    private void ApplyLegend(ChartOptions opts)
    {
        var panel = Find<ItemsControl>("SeriesLegendPanel");
        if (panel == null) return;
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyLegend(opts));
            }
            catch
            {
            }
            return;
        }
        panel.Items.Clear();
        var show = opts.ShowLegend && ViewState.EffectiveType(opts) is not (ChartType.Pie or ChartType.Donut or ChartType.PolarArea);
        panel.IsVisible = show;
        Find<VisualChromeControl>("Chrome")?.SetLegend(show ? panel : null, opts.LegendPosition);
        if (!show) return;

        // Down the side when the legend is beside the chart, along it otherwise.
        var beside = opts.LegendPosition is LegendPosition.Left or LegendPosition.Right;
        panel.ItemsPanel = beside
            ? new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 4 })
            : new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal });
        panel.Margin = opts.LegendPosition switch
        {
            LegendPosition.Top => new Thickness(0),
            LegendPosition.Bottom => new Thickness(0, 6, 0, 0),
            _ => new Thickness(0, 0, 0, 0)
        };

        for (var i = 0; i < opts.Series.Count; i++)
        {
            var series = opts.Series[i];
            if (series.Points.Count == 0) continue;

            var hidden = ViewState.HiddenSeries.Contains(i);
            var index = i;
            var color = ChartPaletteService.ParseColor(string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(i) : series.Color);
            var item = new Border
            {
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Opacity = hidden ? 0.4 : 1,
                Margin = new Thickness(0, 0, beside ? 0 : 14, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 5,
                    Children =
                    {
                        new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock
                        {
                            Text = series.Name, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center,
                            TextDecorations = hidden ? TextDecorations.Strikethrough : null
                        }
                    }
                }
            };
            ToolTip.SetTip(item, hidden ? "Click to show this series" : "Click to hide this series");

            // Clicking a series in the legend hides it (and the axes follow what is left), or brings it back.
            item.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left) return;
                if (!ViewState.HiddenSeries.Remove(index)) ViewState.HiddenSeries.Add(index);
                Canvas?.InvalidateVisual();
                ApplyLegend(opts);
            };
            panel.Items.Add(item);
        }
    }
}
