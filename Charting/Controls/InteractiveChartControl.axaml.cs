using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

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

    private void WireEvents()
    {
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

        var titleBlock = this.FindControl<TextBlock>("ChartTitleText");
        if (titleBlock != null)
        {
            titleBlock.Text = string.IsNullOrWhiteSpace(opts.Title) ? $"{opts.Type} Chart" : opts.Title;
        }

        var statsBorder = this.FindControl<Border>("StatsPillBorder");
        var statsBlock = this.FindControl<TextBlock>("StatsSummaryText");
        if (statsBlock != null && statsBorder != null)
        {
            statsBorder.IsVisible = opts.ShowStats && opts.Series.Count > 0;
            if (statsBorder.IsVisible)
            {
                var total = opts.Series.Sum(s => s.Points.Count);
                statsBlock.Text = $"Min: {opts.Series.Min(s => s.MinY):0.##} • Max: {opts.Series.Max(s => s.MaxY):0.##} • Avg: {opts.Series.Average(s => s.AvgY):0.##} • N: {total}";
            }
        }

        UpdateIcon();
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
