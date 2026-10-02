namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>Small valid specs of each family, for tests about outputs rather than about what is drawn.</summary>
internal static class SampleSpecs
{
    public static ChartSpec Chart(string title = "Chart") => new()
    {
        Title = title,
        Series = { new ChartSeriesSpec { Name = "values", Y = [1, 4, 9] } }
    };

    public static Plot3DSpec Plot3D(string title = "3D plot") => new()
    {
        Title = title,
        Series = { new Plot3DSeriesSpec { X = [0, 1], Y = [0, 1], Z = [0, 2] } }
    };

    public static VisualizerSpec Visualizer(string title = "Visualizer") => new()
    {
        Title = title,
        Kind = VisualizerKind.ArrayPointers,
        State = { Array = new ArrayState { Values = [1, 2, 3] } }
    };

    public static VisualSpec Of(VisualFamily family, string title) => family switch
    {
        VisualFamily.Chart => Chart(title),
        VisualFamily.Plot3D => Plot3D(title),
        _ => Visualizer(title)
    };
}
