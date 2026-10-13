namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public static partial class DisplayExtensions
{
    /// <summary>The data as an <see cref="EChart"/> of <paramref name="kind"/>, to set up and show: <c>sales.ToEChart(EChartType.Bar).Title("Sales").Show()</c>.</summary>
    public static EChart ToEChart<T>(this T data, EChartType kind = EChartType.Line) =>
        EChart.Of(data ?? throw new ArgumentNullException(nameof(data)), kind);

    /// <summary>Records as an <see cref="EChart"/>: each at <paramref name="x"/> with the value <paramref name="y"/>.</summary>
    public static EChart ToEChart<T>(this IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, EChartType kind = EChartType.Line) =>
        EChart.Of(records, x, y, kind);

    /// <summary>Records as a 3D <see cref="EChart"/> (bars or points) at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static EChart ToEChart<T>(this IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, Func<T, object?> z, EChartType kind = EChartType.Bar3D) =>
        EChart.Of(records, x, y, z, kind);

    /// <summary>Shows the data as an <see cref="EChart"/> and returns the data, so a query can go on: <c>sales.DumpEChart("Sales", EChartType.Bar)</c>.</summary>
    public static T DumpEChart<T>(this T data, string? title = null, EChartType kind = EChartType.Line, Action<EChart>? configure = null)
    {
        Show(data.ToEChart(kind), title, configure);
        return data;
    }

    /// <summary>Shows records as an <see cref="EChart"/> (each at <paramref name="x"/> with the value <paramref name="y"/>) and returns them.</summary>
    public static IEnumerable<T> DumpEChart<T>(this IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? title = null, EChartType kind = EChartType.Line, Action<EChart>? configure = null)
    {
        var list = records as IReadOnlyCollection<T> ?? records.ToList();
        Show(list.ToEChart(x, y, kind), title, configure);
        return list;
    }

    /// <summary>Shows records as a 3D <see cref="EChart"/> (bars or points) and returns them.</summary>
    public static IEnumerable<T> DumpEChart<T>(this IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, Func<T, object?> z, string? title = null, EChartType kind = EChartType.Bar3D, Action<EChart>? configure = null)
    {
        var list = records as IReadOnlyCollection<T> ?? records.ToList();
        Show(list.ToEChart(x, y, z, kind), title, configure);
        return list;
    }

    private static void Show(EChart chart, string? title, Action<EChart>? configure)
    {
        if (!string.IsNullOrEmpty(title)) chart.Title(title);
        configure?.Invoke(chart);
        chart.Show();
    }
}
