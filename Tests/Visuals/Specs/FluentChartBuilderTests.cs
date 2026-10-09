using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>The fluent builders (<c>Charts.Line(sales).Title("Sales").Show()</c>) draw what the same call on Display draws.</summary>
public class FluentChartBuilderTests
{
    private static readonly int[] Sales = [4, 9, 6, 12];
    private static readonly Dictionary<string, double> Slices = new() { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
    private static readonly (double, double, double)[] Points = [(1, 2, 3), (4, 5, 6), (2, 3, 1)];

    private static string Json(VisualSpec? spec) => VisualJson.Serialize(spec!);

    public static TheoryData<string> ChartKinds() => new("Line", "Area", "Bar", "Scatter", "Pie", "Donut", "Histogram");

    [Theory]
    [MemberData(nameof(ChartKinds))]
    public void AChartBuilder_ShowsWhatTheSameDisplayCallShows(string kind)
    {
        var xy = new[] { (1.0, 2.0), (2.0, 5.0), (3.0, 4.0) };
        var (built, displayed) = kind switch
        {
            "Line" => (Charts.Line(Sales).Title("t").XLabel("x").YLabel("y").Size(600, 300).Show(), Display.LineChart(Sales, "t", xLabel: "x", yLabel: "y", width: 600, height: 300)),
            "Area" => (Charts.Area(Sales).Title("t").Show(), Display.AreaChart(Sales, "t")),
            "Bar" => (Charts.Bar(Slices).Title("t").Color("#4ec9b0").Show(), Display.BarChart(Slices, "t", "#4ec9b0")),
            "Scatter" => (Charts.Scatter(xy).Title("t").Show(), Display.ScatterChart(xy, "t")),
            "Pie" => (Charts.Pie(Slices).Title("t").Legend(false).Show(), Display.PieChart(Slices, "t", legend: false)),
            "Donut" => (Charts.Donut(Slices).Title("t").Show(), Display.DonutChart(Slices, "t")),
            _ => (Charts.Histogram(new[] { 1.0, 2, 2, 3, 3, 3 }).Title("t").Bins(4).Show(), Display.Histogram(new[] { 1.0, 2, 2, 3, 3, 3 }, "t", bins: 4))
        };

        Assert.Equal(Json(displayed.Spec), Json(built.Spec));
    }

    public static TheoryData<string> PlotKinds() => new("Scatter3D", "Trajectory3D", "VoxelBar3D", "Surface", "SurfaceGrid", "Wireframe");

    [Theory]
    [MemberData(nameof(PlotKinds))]
    public void APlot3DBuilder_ShowsWhatTheSameDisplayCallShows(string kind)
    {
        var grid = new double[,] { { 1, 2, 3 }, { 3, 2, 1 }, { 2, 4, 2 } };
        Func<double, double, double> wave = (x, y) => Math.Sin(x) * Math.Cos(y);
        var (built, displayed) = kind switch
        {
            "Scatter3D" => (Charts.Scatter3D(Points).Title("t").XLabel("a").ZLabel("c").Show(), Display.Scatter3D(Points, "t", xLabel: "a", zLabel: "c")),
            "Trajectory3D" => (Charts.Trajectory3D(Points).Title("t").Show(), Display.Trajectory3D(Points, "t")),
            "VoxelBar3D" => (Charts.VoxelBar3D(Points).Title("t").Show(), Display.VoxelBar3D(Points, "t")),
            "Surface" => (Charts.Surface(wave, (-3, 3), (-2, 2), 20).Title("t").ColorMap(ColorMapPreset.Plasma).Show(), Display.Surface3D(wave, (-3, 3), (-2, 2), 20, "t", ColorMapPreset.Plasma)),
            "SurfaceGrid" => (Charts.Surface(grid).Title("t").Show(), Display.Surface3D(grid, "t")),
            _ => (Charts.Wireframe(wave).Title("t").Show(), Display.Surface3D(wave, title: "t", wireframe: true))
        };

        Assert.Equal(Json(displayed.Spec), Json(built.Spec));
    }

    [Fact]
    public void ASurfaceBuilder_DefaultsToMinusFiveToFive_AndWireframeSwitchesTheKind()
    {
        var spec = Charts.Surface((x, y) => x + y).Wireframe().Show().Spec!;

        Assert.Equal(Plot3DType.Wireframe, spec.Kind);
        Assert.Equal((-5, 5, 30), (spec.Surface!.X.Min, spec.Surface.X.Max, spec.Surface.Z.Count));
    }

    [Fact]
    public void Series_AddsNamedSeries_AndNamesTheFirstOne()
    {
        var spec = Charts.Bar(new[] { 1, 2, 3 }, "2024").Series("2025", new[] { 2, 3, 5 }, "#ff8800").Show().Spec!;

        Assert.Equal(["2024", "2025"], spec.Series.Select(s => s.Name));
        Assert.Equal("#ff8800", spec.Series[1].Color);
    }

    [Fact]
    public void ARecordsBuilder_ReadsTheXAndYOfEachRecord()
    {
        var spec = Charts.Bar(new[] { new { Month = "Jan", Total = 3 }, new { Month = "Feb", Total = 5 } }, r => r.Month, r => r.Total, "Totals").Show().Spec!;

        Assert.Equal("Totals", spec.Series[0].Name);
        Assert.Equal([3.0, 5.0], spec.Series[0].Y);
        Assert.Equal(["Jan", "Feb"], spec.Series[0].Labels);
    }

    [Fact]
    public void Colors_ColoursTheValuesOfTheFirstSeries()
    {
        var spec = Charts.Bar(Slices).Colors("#111111", null, "#333333").Show().Spec!;

        Assert.Equal(["#111111", null, "#333333"], spec.Series[0].Colors);
    }

    [Fact]
    public void RecordsOfPoints_TakeTheirLabels()
    {
        var spec = Charts.Scatter3D(new[] { new { X = 1.0, Y = 2.0, Z = 3.0, Name = "a" }, new { X = 4.0, Y = 5.0, Z = 6.0, Name = "b" } }, p => p.X, p => p.Y, p => p.Z, p => p.Name).Show().Spec!;

        Assert.Equal(["a", "b"], spec.Series[0].Labels);
    }

    [Fact]
    public void Labels_LabelTheLastSeriesOfPoints()
    {
        var spec = Charts.Scatter3D(Points).Labels(["a", "b", "c"]).Show().Spec!;

        Assert.Equal(["a", "b", "c"], spec.Series[0].Labels);
    }

    [Fact]
    public void Configure_RunsLast_SoItCanChangeAnything()
    {
        var spec = Charts.Line(Sales).Configure(s => s.Title = "from configure").Title("from the setting").Show().Spec!;

        Assert.Equal("from configure", spec.Title);
    }

    [Fact]
    public void AShownBuilder_CannotBeChanged_ButItsHandleCanUpdateIt()
    {
        var builder = Charts.Line(Sales);
        var handle = builder.Show();

        var error = Assert.Throws<InvalidOperationException>(() => builder.Title("late"));
        Assert.Contains("Update", error.Message);

        handle.Update(s => s.Title = "updated");
        Assert.Equal("updated", handle.Spec!.Title);
    }

    [Fact]
    public void AnUnusableBuilder_SaysWhatIsWrong_WhenItIsShown()
    {
        RichCellOutput? emitted = null;
        DisplayHandle<Plot3DSpec> handle;
        using (InteractiveDisplayContext.EnterScope(output => emitted = output)) handle = Charts.Scatter3D(Points).Labels(["only one"]).Show();

        Assert.False(handle.IsShown);
        Assert.Equal(CellOutputKind.Error, emitted!.Kind);
        Assert.Contains("abel", emitted.Text);
    }

    [Fact]
    public void Mistakes_AreSaidAtTheCallThatMadeThem()
    {
        Assert.Throws<ArgumentException>(() => Charts.Line(42));
        Assert.Throws<InvalidOperationException>(() => Charts.Scatter3D(Points).Wireframe());
    }

    [Fact]
    public void ABuilderReturnedFromACell_IsShownLikeAnyVisual()
    {
        Assert.True(VisualOutputs.TryFromModel(Charts.Line(Sales).Title("returned"), out var output));
        Assert.Equal("returned", output!.Visual!.Spec.Title);
        Assert.Equal(CellOutputKind.Chart, output.Kind);
    }

    [Fact]
    public void DisplayShow_TakesABuilder()
    {
        var handle = Display.Show(Charts.Surface((x, y) => x * y).Title("via Display.Show"));

        Assert.Equal("via Display.Show", handle.Spec!.Title);
    }

    [Fact]
    public void AScriptThatUsesTheBuilders_Compiles()
    {
        const string code = """
            Charts.Line(new[] { 1, 3, 2 }).Title("Sales").XLabel("Month").YLabel("USD").Size(640, 320).Show();
            Charts.Bar(new Dictionary<string, int> { ["Mon"] = 4 }).Colors("#4ec9b0").Show();
            Charts.Surface((x, y) => Math.Sin(x) * Math.Cos(y), (-5, 5), (-5, 5)).ColorMap(ColorMapPreset.Plasma).Show();
            Charts.Scatter3D(new[] { (1.0, 2.0, 3.0) }).Labels(new[] { "a" }).Show();
            var handle = Charts.Pie(new Dictionary<string, int> { ["a"] = 1 }).Show();
            handle.Update(s => s.Title = "again");
            """;

        var (success, _, diagnostics) = new RoslynCompilerService().CompileToAssembly(code, ExecutionLanguageMode.Statements);

        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
    }
}
