namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

/// <summary>
/// The chart colours every host starts from. A series without its own colour is drawn in the theme's colour for its
/// index, which only the host's renderer knows (the desktop reads the active theme, the browser its CSS), so the render
/// model leaves it empty and these are the fallback.
/// </summary>
public static class ChartPalette
{
    public const string DefaultCyan = "#4ec9b0";

    /// <summary>Series colours when the theme has none, in order.</summary>
    public static IReadOnlyList<string> ExpressivePalette { get; } =
    [
        "#4ec9b0", // Cyan / Teal
        "#60a5fa", // Blue
        "#f59e0b", // Amber
        "#f43f5e", // Rose
        "#10b981", // Emerald
        "#a855f7", // Purple
        "#38bdf8", // Sky
        "#fb923c"  // Orange
    ];

    /// <summary>The fallback colour of series <paramref name="seriesIndex"/> (0-based).</summary>
    public static string DefaultSeriesColor(int seriesIndex) =>
        ExpressivePalette[Math.Max(seriesIndex, 0) % ExpressivePalette.Count];
}
