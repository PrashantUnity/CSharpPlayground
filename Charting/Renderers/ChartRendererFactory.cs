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
            ChartType.Pie => new PieChartRenderer(isDonut: false),
            ChartType.Donut => new PieChartRenderer(isDonut: true),
            ChartType.PolarArea => new PolarAreaChartRenderer(),
            ChartType.Radar => new RadarChartRenderer(),
            _ => new CartesianChartRenderer()
        });
    }
}
