using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Display
{
    public static InteractiveChartControl Chart(
        object data,
        string? title = null,
        string? color = null,
        string? chartType = null,
        double width = 560,
        double height = 280,
        bool showGrid = true,
        bool showPoints = true,
        bool showStats = true)
    {
        ChartOptions options;

        if (data is ChartOptions existingOpts)
        {
            options = existingOpts;
        }
        else
        {
            options = ChartDataParser.Parse(data, title, color, chartType);
        }

        if (!string.IsNullOrEmpty(title)) options.Title = title;
        if (!string.IsNullOrEmpty(color)) options.PrimaryColor = color;
        if (!string.IsNullOrEmpty(chartType) && Enum.TryParse<ChartType>(chartType, true, out var ct))
        {
            options.Type = ct;
        }

        options.Width = width;
        options.Height = height;
        options.ShowGrid = showGrid;
        options.ShowPoints = showPoints;
        options.ShowStats = showStats;

        InteractiveChartControl? chartControl = null;
        try
        {
            chartControl = new InteractiveChartControl(options);
        }
        catch
        {
            // Headless test runner without Avalonia platform rendering interface
        }

        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Chart,
            InteractiveControl = chartControl,
            ChartOptions = options
        });

        return chartControl!;
    }

    public static InteractiveChartControl LineChart(
        object data,
        string? title = null,
        string? color = null,
        bool showPoints = true)
    {
        return Chart(data, title, color, "Line", showPoints: showPoints);
    }

    public static InteractiveChartControl AreaChart(
        object data,
        string? title = null,
        string? color = null)
    {
        return Chart(data, title, color, "Area");
    }

    public static InteractiveChartControl BarChart(
        object data,
        string? title = null,
        string? color = null)
    {
        return Chart(data, title, color, "Bar");
    }

    public static InteractiveChartControl ScatterChart(
        object data,
        string? title = null,
        string? color = null)
    {
        return Chart(data, title, color, "Scatter");
    }

    public static InteractiveChartControl PieChart(
        object data,
        string? title = null)
    {
        return Chart(data, title, null, "Pie", showGrid: false);
    }

    public static InteractiveChartControl DonutChart(
        object data,
        string? title = null)
    {
        return Chart(data, title, null, "Donut", showGrid: false);
    }
}

public static partial class DisplayExtensions
{
    public static T Chart<T>(
        this T data,
        string? title = null,
        string? color = null,
        string? chartType = null)
    {
        if (data != null)
        {
            Display.Chart(data, title, color, chartType);
        }
        return data;
    }

    public static T LineChart<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.LineChart(data, title, color);
        }
        return data;
    }

    public static T AreaChart<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.AreaChart(data, title, color);
        }
        return data;
    }

    public static T BarChart<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.BarChart(data, title, color);
        }
        return data;
    }

    public static T ScatterChart<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.ScatterChart(data, title, color);
        }
        return data;
    }

    public static T PieChart<T>(
        this T data,
        string? title = null)
    {
        if (data != null)
        {
            Display.PieChart(data, title);
        }
        return data;
    }

    public static T DonutChart<T>(
        this T data,
        string? title = null)
    {
        if (data != null)
        {
            Display.DonutChart(data, title);
        }
        return data;
    }
}
