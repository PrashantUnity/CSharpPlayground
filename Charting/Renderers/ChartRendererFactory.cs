using System;
using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public static class ChartRendererFactory
{
    private static readonly ConcurrentDictionary<ChartType, IChartRenderer> Cache = new();

    public static IChartRenderer GetRenderer(ChartType type)
    {
        return Cache.GetOrAdd(type, t => t switch
        {
            ChartType.Line => new LineChartRenderer(isAreaMode: false),
            ChartType.Area => new LineChartRenderer(isAreaMode: true),
            ChartType.Bar => new BarChartRenderer(),
            ChartType.Histogram => new BarChartRenderer(),
            ChartType.Scatter => new ScatterChartRenderer(),
            ChartType.Pie => new PieChartRenderer(isDonut: false),
            ChartType.Donut => new PieChartRenderer(isDonut: true),
            _ => new LineChartRenderer()
        });
    }
}
