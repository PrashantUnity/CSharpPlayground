using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

public partial class InteractiveChartControl : UserControl
{
    public static readonly StyledProperty<ChartOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractiveChartControl, ChartOptions?>(nameof(Options));

    public ChartOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    static InteractiveChartControl()
    {
        OptionsProperty.Changed.AddClassHandler<InteractiveChartControl>((x, _) => x.ApplyOptions());
    }

    public InteractiveChartControl()
    {
        try { InitializeComponent(); WireEvents(); }
        catch { /* Headless test runner without Avalonia platform rendering */ }
    }

    public InteractiveChartControl(ChartOptions options) : this()
    {
        Options = options;
        ApplyOptions();
    }

    /// <summary>A value was clicked (a point, bar or slice).</summary>
    public event EventHandler<ElementClickedEventArgs<ChartHitTestResult>>? ValueClicked;

    private void WireEvents()
    {
        if (this.FindControl<ChartCanvasControl>("CanvasControl") is { } clickable) clickable.ValueClicked += (_, e) => ValueClicked?.Invoke(this, e);

        var lineBtn = this.FindControl<Button>("LineTypeBtn");
        var areaBtn = this.FindControl<Button>("AreaTypeBtn");
        var barBtn = this.FindControl<Button>("BarTypeBtn");
        var scatterBtn = this.FindControl<Button>("ScatterTypeBtn");
        var pieBtn = this.FindControl<Button>("PieTypeBtn");
        var gridBtn = this.FindControl<Button>("GridToggleBtn");
        var copyBtn = this.FindControl<Button>("CopyCsvBtn");

        if (lineBtn != null) lineBtn.Click += (_, _) => SwitchType(ChartType.Line);
        if (areaBtn != null) areaBtn.Click += (_, _) => SwitchType(ChartType.Area);
        if (barBtn != null) barBtn.Click += (_, _) => SwitchType(ChartType.Bar);
        if (scatterBtn != null) scatterBtn.Click += (_, _) => SwitchType(ChartType.Scatter);
        if (pieBtn != null) pieBtn.Click += (_, _) => SwitchType(ChartType.Pie);

        if (gridBtn != null)
        {
            gridBtn.Click += (_, _) =>
            {
                if (Options != null)
                {
                    Options.ShowGrid = !Options.ShowGrid;
                    this.FindControl<ChartCanvasControl>("CanvasControl")?.InvalidateVisual();
                }
            };
        }

        if (copyBtn != null)
        {
            copyBtn.Click += async (_, _) =>
            {
                if (Options != null) await ChartExportService.CopyCsvToClipboardAsync(Options);
            };
        }
    }

    public void ApplyOptions()
    {
        var opts = Options;
        if (opts == null) return;

        var canvas = this.FindControl<ChartCanvasControl>("CanvasControl");
        if (canvas != null) { canvas.Options = opts; canvas.InvalidateVisual(); }

        // The height the chart asks for is the drawing's; the header and legend come on top of it.
        if (canvas != null && opts.Height > 0) canvas.Height = Math.Max(canvas.MinHeight, opts.Height);

        var titleBlock = this.FindControl<TextBlock>("ChartTitleText");
        if (titleBlock != null)
        {
            titleBlock.Text = string.IsNullOrWhiteSpace(opts.Title) ? $"{opts.Type} Chart" : opts.Title;
        }

        var subtitleBlock = this.FindControl<TextBlock>("ChartSubtitleText");
        if (subtitleBlock != null)
        {
            subtitleBlock.Text = opts.Subtitle;
            subtitleBlock.IsVisible = !string.IsNullOrWhiteSpace(opts.Subtitle);
        }

        var statsBorder = this.FindControl<Border>("StatsPillBorder");
        var statsBlock = this.FindControl<TextBlock>("StatsSummaryText");
        if (statsBlock != null && statsBorder != null)
        {
            var stats = ChartStatistics.Of(opts);
            statsBorder.IsVisible = opts.ShowStats && stats != null;
            if (stats != null) statsBlock.Text = stats.Value.ToString();
        }

        ApplyLegend(opts);
        UpdateIcon();
    }

    // The series legend under the drawing; a pie or donut lists its slices beside it instead.
    private void ApplyLegend(ChartOptions opts)
    {
        var panel = this.FindControl<ItemsControl>("SeriesLegendPanel");
        if (panel == null) return;

        panel.Items.Clear();
        panel.IsVisible = opts.ShowLegend && opts.Type is not (ChartType.Pie or ChartType.Donut);
        if (!panel.IsVisible) return;

        // Coloured as the renderers colour them: a series without a colour takes the palette's, counting drawn series.
        var drawn = 0;
        foreach (var series in opts.Series.Where(s => s.Points.Count > 0))
        {
            var color = ChartPaletteService.ParseColor(string.IsNullOrEmpty(series.Color) ? ChartPaletteService.GetSeriesColor(drawn) : series.Color);
            drawn++;
            panel.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Margin = new Thickness(0, 0, 14, 2),
                Children =
                {
                    new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = series.Name, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center }
                }
            });
        }
    }

    private void SwitchType(ChartType newType)
    {
        if (Options == null) return;
        Options.Type = newType;

        var titleBlock = this.FindControl<TextBlock>("ChartTitleText");
        if (titleBlock != null && string.IsNullOrWhiteSpace(Options.Title))
        {
            titleBlock.Text = $"{newType} Chart";
        }

        ApplyLegend(Options);
        UpdateIcon();
        this.FindControl<ChartCanvasControl>("CanvasControl")?.InvalidateVisual();
    }

    private void UpdateIcon()
    {
        var icon = this.FindControl<MaterialIcon>("HeaderChartIcon");
        if (icon == null || Options == null) return;

        icon.Kind = Options.Type switch
        {
            ChartType.Line => MaterialIconKind.ChartLine,
            ChartType.Area => MaterialIconKind.ChartAreaspline,
            ChartType.Bar or ChartType.Histogram => MaterialIconKind.ChartBar,
            ChartType.Scatter => MaterialIconKind.ChartScatterPlot,
            ChartType.Pie or ChartType.Donut => MaterialIconKind.ChartPie,
            _ => MaterialIconKind.ChartLine
        };
    }
}
