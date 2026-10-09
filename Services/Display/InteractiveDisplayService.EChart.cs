namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public static partial class Display
{
    /// <summary>
    /// Shows <paramref name="data"/> as an interactive ECharts chart of <paramref name="kind"/>, and returns it (it can be
    /// changed and shown again). The same as <c>EChart.{kind}(data).Title(title).Show()</c>.
    /// </summary>
    public static global::PdfEditorApp.Plugins.CSharpEditor.Services.Display.EChart EChart(
        object data,
        string? title = null,
        EChartType kind = EChartType.Line,
        Action<global::PdfEditorApp.Plugins.CSharpEditor.Services.Display.EChart>? configure = null)
    {
        var chart = global::PdfEditorApp.Plugins.CSharpEditor.Services.Display.EChart.Of(data, kind);
        if (!string.IsNullOrEmpty(title)) chart.Title(title);
        configure?.Invoke(chart);
        return chart.Show();
    }
}
