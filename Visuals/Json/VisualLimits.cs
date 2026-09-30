namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// How much of a visual the studio draws. Past a limit it draws the first part and says so in the visual's header
/// ("showing the first 200,000 of 350,000 values"); a limit is never silent.
/// </summary>
public static class VisualLimits
{
    /// <summary>Values across all series of a chart.</summary>
    public const int MaxChartValues = 200_000;

    /// <summary>Points across all series of a 3D plot.</summary>
    public const int MaxPlot3DPoints = 100_000;

    /// <summary>Heights in a surface (500 × 500).</summary>
    public const int MaxSurfaceCells = 250_000;

    public const int MaxGraphNodes = 5_000;
    public const int MaxGraphEdges = 20_000;

    /// <summary>Cells in a grid (200 × 200).</summary>
    public const int MaxGridCells = 40_000;

    public const int MaxTreeNodes = 5_000;
    public const int MaxListNodes = 2_000;
    public const int MaxArrayItems = 10_000;
    public const int MaxCanvasShapes = 20_000;

    /// <summary>Steps of one visualizer.</summary>
    public const int MaxSteps = 5_000;

    /// <summary>The largest spec read from JSON; a program sending more gets an error in its output.</summary>
    public const int MaxPayloadBytes = 32 * 1024 * 1024;

    /// <summary>Visual outputs one notebook cell keeps.</summary>
    public const int MaxVisualsPerCell = 50;
}
