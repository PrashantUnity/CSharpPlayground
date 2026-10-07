using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Every chart and 3D helper takes the same named settings (axis labels, size, legend), so a chart needs no
/// <c>configure</c> lambda for the usual things; the older call shapes still draw what they drew.
/// </summary>
public class FriendlyVisualSettingsTests
{
    private static readonly int[] Sales = [4, 9, 6, 12];

    public static TheoryData<string> AxisChartKinds() => new("Line", "Area", "Bar", "Scatter", "Histogram");

    private static DisplayHandle<ChartSpec> Chart(string kind) => kind switch
    {
        "Line" => Display.LineChart(Sales, "t", xLabel: "Month", yLabel: "USD", width: 600, height: 300, legend: true),
        "Area" => Display.AreaChart(Sales, "t", xLabel: "Month", yLabel: "USD", width: 600, height: 300, legend: true),
        "Bar" => Display.BarChart(Sales, "t", xLabel: "Month", yLabel: "USD", width: 600, height: 300, legend: true),
        "Scatter" => Display.ScatterChart(new[] { (1.0, 2.0), (2.0, 5.0) }, "t", xLabel: "Month", yLabel: "USD", width: 600, height: 300, legend: true),
        "Histogram" => Display.Histogram(new[] { 1.0, 2, 2, 3, 3, 3 }, "t", xLabel: "Month", yLabel: "USD", width: 600, height: 300, legend: true),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [Theory]
    [MemberData(nameof(AxisChartKinds))]
    public void AChartHelper_TakesAxisLabelsSizeAndLegend(string kind)
    {
        var spec = Assert.IsType<ChartSpec>(Chart(kind).Spec);

        Assert.Equal("Month", spec.XAxis.Title);
        Assert.Equal("USD", spec.YAxis.Title);
        Assert.Equal(600, spec.Width);
        Assert.Equal(300, spec.Height);
        Assert.True(spec.Legend.Show);
    }

    [Fact]
    public void PieAndDonut_TakeSizeAndLegend()
    {
        var slices = new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 };
        var pie = Assert.IsType<ChartSpec>(Display.PieChart(slices, "p", width: 320, height: 320, legend: false).Spec);
        var donut = Assert.IsType<ChartSpec>(Display.DonutChart(slices, "d", width: 280, legend: false).Spec);

        Assert.Equal((320, 320, false), (pie.Width, pie.Height, pie.Legend.Show));
        Assert.Equal((280, false), (donut.Width, donut.Legend.Show));
    }

    [Fact]
    public void ChartSettingsLeftOut_AreTheStudiosDefaults()
    {
        var spec = Assert.IsType<ChartSpec>(Display.LineChart(Sales).Spec);

        Assert.Null(spec.XAxis.Title);
        Assert.Null(spec.YAxis.Title);
        Assert.Null(spec.Width);
        Assert.Null(spec.Legend.Show);
    }

    [Fact]
    public void TheRecordsOverload_TakesTheSameSettings()
    {
        var spec = Assert.IsType<ChartSpec>(Display.Chart(
            new[] { new { Month = "Jan", Revenue = 3 }, new { Month = "Feb", Revenue = 5 } },
            r => r.Month, r => r.Revenue, "Revenue", xLabel: "Month", yLabel: "USD", width: 500).Spec);

        Assert.Equal(("Month", "USD", 500), (spec.XAxis.Title, spec.YAxis.Title, spec.Width));
    }

    [Fact]
    public void TheExtensionForms_PassTheSettingsOn()
    {
        var line = Assert.IsType<ChartSpec>(Shown(() => Sales.DisplayLineChart("t", xLabel: "x", yLabel: "y", height: 240)));
        var scatter = Assert.IsType<Plot3DSpec>(Shown(() => new[] { (1.0, 2.0, 3.0), (2.0, 3.0, 4.0) }.DisplayScatter3D("t", xLabel: "a", yLabel: "b", zLabel: "c", width: 500)));

        Assert.Equal(("x", "y", 240), (line.XAxis.Title, line.YAxis.Title, line.Height));
        Assert.Equal(("a", "b", "c", 500), (scatter.XAxis.Title, scatter.YAxis.Title, scatter.ZAxis.Title, scatter.Width));
    }

    // The extension forms hand the data back, so the visual they showed is what the display scope was given.
    private static VisualSpec? Shown(Action show)
    {
        RichCellOutput? emitted = null;
        using (InteractiveDisplayContext.EnterScope(output => emitted = output)) show();
        return emitted?.Visual?.Spec;
    }

    private static readonly (double, double, double)[] Points = [(1, 2, 3), (4, 5, 6), (2, 3, 1)];

    public static TheoryData<string> PointPlotKinds() => new("Scatter", "Trajectory", "VoxelBar");

    [Theory]
    [MemberData(nameof(PointPlotKinds))]
    public void APointPlotHelper_TakesAxisLabelsAndSize(string kind)
    {
        var handle = kind switch
        {
            "Scatter" => Display.Scatter3D(Points, "t", xLabel: "X", yLabel: "Y", zLabel: "Height", width: 640, height: 400),
            "Trajectory" => Display.Trajectory3D(Points, "t", xLabel: "X", yLabel: "Y", zLabel: "Height", width: 640, height: 400),
            _ => Display.VoxelBar3D(Points, "t", xLabel: "X", yLabel: "Y", zLabel: "Height", width: 640, height: 400)
        };
        var spec = Assert.IsType<Plot3DSpec>(handle.Spec);

        Assert.Equal(("X", "Y", "Height"), (spec.XAxis.Title, spec.YAxis.Title, spec.ZAxis.Title));
        Assert.Equal((640, 400), (spec.Width, spec.Height));
    }

    [Fact]
    public void ASurface_TakesAxisLabelsAndSize_FromEveryShapeOfTheCall()
    {
        var grid = new double[,] { { 1, 2 }, { 3, 4 } };
        var specs = new[]
        {
            Display.Surface3D(grid, "t", xLabel: "X", yLabel: "Y", zLabel: "Z", width: 500).Spec,
            Display.Surface3D((x, y) => x * y, (-1, 1), (-1, 1), xLabel: "X", yLabel: "Y", zLabel: "Z", width: 500).Spec,
            Display.Surface3D((x, y) => x * y, minX: -1, maxX: 1, xLabel: "X", yLabel: "Y", zLabel: "Z", width: 500).Spec,
            Display.Plot3D((x, y) => x * y, xLabel: "X", yLabel: "Y", zLabel: "Z", width: 500).Spec
        };

        Assert.All(specs, s =>
        {
            Assert.Equal(("X", "Y", "Z", 500), (s!.XAxis.Title, s.YAxis.Title, s.ZAxis.Title, s.Width));
        });
    }

    [Fact]
    public void ASurfaceWithoutRanges_CoversMinusFiveToFive_AtThirtyByThirty()
    {
        var spec = Assert.IsType<Plot3DSpec>(Display.Surface3D((x, y) => x + y).Spec);

        Assert.NotNull(spec.Surface);
        Assert.Equal((-5, 5, -5, 5), (spec.Surface.X.Min, spec.Surface.X.Max, spec.Surface.Y.Min, spec.Surface.Y.Max));
        Assert.Equal(30, spec.Surface.Z.Count);
        Assert.Equal(30, spec.Surface.Z[0].Count);
    }

    [Fact]
    public void ASurfaceWithTupleRanges_UsesThemAndTheResolution()
    {
        var spec = Assert.IsType<Plot3DSpec>(Display.Surface3D((x, y) => x * y, xRange: (-2, 2), yRange: (0, 1), resolution: 12, wireframe: true).Spec);

        Assert.Equal(Plot3DType.Wireframe, spec.Kind);
        Assert.Equal((-2, 2, 0, 1), (spec.Surface!.X.Min, spec.Surface.X.Max, spec.Surface.Y.Min, spec.Surface.Y.Max));
        Assert.Equal((12, 12), (spec.Surface.Z.Count, spec.Surface.Z[0].Count));
    }

    [Fact]
    public void TheOlderSurfaceShapeWithMinAndMax_StillDrawsTheSame()
    {
        var older = Assert.IsType<Plot3DSpec>(Display.Surface3D((x, y) => x * y, minX: -2, maxX: 2, minY: 0, maxY: 1, resX: 12, resY: 9).Spec);
        var newer = Assert.IsType<Plot3DSpec>(Display.Surface3D((x, y) => x * y, (-2, 2), (0, 1), resolution: 12).Spec);

        Assert.Equal((9, 12), (older.Surface!.Z.Count, older.Surface.Z[0].Count));
        Assert.Equal(newer.Surface!.X.Min, older.Surface.X.Min);
        Assert.Equal(newer.Surface.Y.Max, older.Surface.Y.Max);
    }

    [Fact]
    public void ScriptsThatUseTheNewSettings_Compile()
    {
        const string code = """
            Display.LineChart(new[] { 1, 3, 2 }, "Sales", xLabel: "Month", yLabel: "USD", width: 600, height: 300);
            Display.BarChart(new Dictionary<string, int> { ["Mon"] = 4, ["Tue"] = 7 }, legend: false);
            Display.Surface3D((x, y) => Math.Sin(x) * Math.Cos(y), (-5, 5), (-5, 5), title: "Wave", zLabel: "Height");
            Display.Surface3D((x, y) => x * y);
            new[] { (1.0, 2.0, 3.0) }.DisplayScatter3D("Points", xLabel: "a", yLabel: "b", zLabel: "c");
            """;

        var (success, _, diagnostics) = new RoslynCompilerService().CompileToAssembly(code, ExecutionLanguageMode.Statements);

        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
    }
}
