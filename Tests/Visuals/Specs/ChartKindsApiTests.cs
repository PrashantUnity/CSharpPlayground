using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>The helpers and builders for the newer chart kinds and options draw what their specs say.</summary>
public class ChartKindsApiTests
{
    private static readonly double[] A = [4, 9, 6, 12];
    private static readonly double[] B = [6, 8, 3, 14];
    private static string Json(VisualSpec? spec) => VisualJson.Serialize(spec!);
    private static ChartSpec Spec(DisplayHandle<ChartSpec> handle) => handle.Spec!;

    [Fact]
    public void SeveralNamedSeries_DrawAsOneLineEach()
    {
        var spec = Spec(Display.LineChart(("Sales", A), ("Costs", B)));

        Assert.Equal(ChartType.Line, spec.Kind);
        Assert.Equal(["Sales", "Costs"], spec.Series.Select(s => s.Name));
        Assert.Equal([4.0, 9, 6, 12], spec.Series[0].Y);
        Assert.Empty(VisualSpecValidator.Validate(spec));
    }

    [Fact]
    public void ASingleNamedSeries_IsAccepted_ThoughATupleIsOneArgument()
    {
        var spec = Spec(Display.LineChart(("Sales", A)));

        Assert.Equal("Sales", spec.Series.Single().Name);
        Assert.Equal(4, spec.Series[0].Y.Count);
    }

    [Fact]
    public void Labels_NameThePlacesOfEverySeries()
    {
        var spec = Spec(Display.BarChart(A, labels: ["Q1", "Q2", "Q3", "Q4"]));

        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], spec.Series[0].Labels);
    }

    [Fact]
    public void StackedAndHorizontalBars_SetTheirSpecFields()
    {
        var stacked = Spec(Display.StackedBarChart(new Dictionary<string, double[]> { ["a"] = A, ["b"] = B }));
        var percent = Spec(Display.StackedBarChart(new Dictionary<string, double[]> { ["a"] = A, ["b"] = B }, percent: true));
        var horizontal = Spec(Display.HorizontalBarChart(A, labels: ["w", "x", "y", "z"]));
        var both = Spec(Display.BarChart(new Dictionary<string, double[]> { ["a"] = A, ["b"] = B }, stack: ChartStack.Stacked, horizontal: true));

        Assert.Equal(ChartStack.Stacked, stacked.Stack);
        Assert.Equal(ChartStack.Percent, percent.Stack);
        Assert.Equal(ChartOrientation.Horizontal, horizontal.Orientation);
        Assert.Equal((ChartStack.Stacked, ChartOrientation.Horizontal), (both.Stack!.Value, both.Orientation!.Value));
        Assert.All(new[] { stacked, percent, horizontal, both }, s => Assert.Empty(VisualSpecValidator.Validate(s)));
    }

    [Fact]
    public void ABubbleChart_ReadsTuplesArraysAndRecords()
    {
        var tuples = Spec(Display.BubbleChart(new[] { (1.0, 2.0, 5.0), (2.0, 4.0, 9.0) }));
        var arrays = Spec(Display.BubbleChart(new[] { new[] { 1.0, 2.0, 5.0 }, new[] { 2.0, 4.0, 9.0 } }));
        var records = Spec(Display.BubbleChart(new[] { new { X = 1, Y = 2, Size = 5 }, new { X = 2, Y = 4, Size = 9 } }));
        var shortName = Spec(Display.BubbleChart(new[] { new { X = 1, Y = 2, R = 5 }, new { X = 2, Y = 4, R = 9 } }));
        var radius = Spec(Display.BubbleChart(new[] { new { x = 1, value = 2, radius = 5 }, new { x = 2, value = 4, radius = 9 } }));

        foreach (var spec in new[] { tuples, arrays, records, shortName, radius })
        {
            Assert.Equal(ChartType.Bubble, spec.Kind);
            Assert.Equal([1.0, 2], spec.Series[0].X);
            Assert.Equal([2.0, 4], spec.Series[0].Y);
            Assert.Equal([5.0, 9], spec.Series[0].Sizes);
            Assert.Empty(VisualSpecValidator.Validate(spec));
        }
    }

    [Fact]
    public void ANamedGroupOfBubbles_IsASeriesEach()
    {
        var spec = Spec(Display.BubbleChart(new Dictionary<string, (double, double, double)[]>
        {
            ["A"] = [(1, 2, 5), (2, 3, 6)],
            ["B"] = [(3, 1, 4)]
        }));

        Assert.Equal(["A", "B"], spec.Series.Select(s => s.Name));
        Assert.Equal([4.0], spec.Series[1].Sizes);
    }

    [Fact]
    public void ARadarChart_NamesItsSpokes()
    {
        var spec = Spec(Display.RadarChart(new Dictionary<string, double[]> { ["Ada"] = [8, 6, 9], ["Bo"] = [5, 9, 6] }, labels: ["Speed", "Power", "Skill"]));

        Assert.Equal(ChartType.Radar, spec.Kind);
        Assert.Equal(["Speed", "Power", "Skill"], spec.Series[0].Labels);
        Assert.Equal(["Speed", "Power", "Skill"], spec.Series[1].Labels);
        Assert.Empty(VisualSpecValidator.Validate(spec));
    }

    [Fact]
    public void AGauge_IsAHalfDonutStartingAtTheLeft()
    {
        var spec = Spec(Display.DonutChart(new Dictionary<string, double> { ["Done"] = 70, ["Left"] = 30 }, gauge: true));

        Assert.Equal((-90.0, 180.0), (spec.StartAngle!.Value, spec.Sweep!.Value));
        Assert.Empty(VisualSpecValidator.Validate(spec));
    }

    [Fact]
    public void APolarAreaChart_IsAValidSpec()
    {
        var spec = Spec(Display.PolarAreaChart(new Dictionary<string, double> { ["a"] = 3, ["b"] = 5 }));

        Assert.Equal(ChartType.PolarArea, spec.Kind);
        Assert.Empty(VisualSpecValidator.Validate(spec));
    }

    [Fact]
    public void SeriesOptions_SetTheFieldsTheirNamesSay()
    {
        var builder = Charts.Line(A, "Sales")
            .Series("Costs", B, s => s.Kind(ChartType.Bar).OnRightAxis().Rounded(5).StackGroup("x"))
            .Series("Trend", A, s => s.Dashed().Smooth(0.6).Points(PointShape.Star, 6).Fill().LineWidth(3))
            .Series("Steps", B, s => s.Step(LineStep.Middle).Dotted().Color("#ff0000").ColorSegments().Colors("#111", "#222", "#333", "#444"));
        var spec = builder.Spec;

        Assert.Equal((ChartType.Bar, AxisSide.Right, 5.0, "x"), (spec.Series[1].Kind!.Value, spec.Series[1].Axis!.Value, spec.Series[1].CornerRadius!.Value, spec.Series[1].Stack));
        Assert.Equal((LineDash.Dashed, LineInterpolation.Smooth, 0.6, PointShape.Star, 6.0, true, 3.0),
            (spec.Series[2].Dash!.Value, spec.Series[2].Interpolation!.Value, spec.Series[2].Tension!.Value, spec.Series[2].PointStyle!.Value, spec.Series[2].PointRadius!.Value, spec.Series[2].Fill!.Value, spec.Series[2].LineWidth!.Value));
        Assert.Equal((LineStep.Middle, LineDash.Dotted, "#ff0000", true), (spec.Series[3].Step!.Value, spec.Series[3].Dash!.Value, spec.Series[3].Color, spec.Series[3].ColorSegments!.Value));
    }

    [Fact]
    public void ABuilderWithEveryChartLevelSetting_IsAValidSpec_AndReadsBack()
    {
        var spec = Charts.Bar(("2025", A), ("2026", B))
            .Stacked()
            .RightAxis("Margin %", 0, 100)
            .Style(s => s.Rounded(4))
            .SuggestedY(0, 50)
            .LegendAt(LegendPosition.Right)
            .Labels(["Q1", "Q2", "Q3", "Q4"])
            .Series("Margin", new double[] { 20, 30, 25, 40 }, s => s.Kind(ChartType.Line).OnRightAxis().Monotone())
            .Spec;

        Assert.Empty(VisualSpecValidator.Validate(spec));
        Assert.Equal(Json(spec), Json(VisualJson.Deserialize<ChartSpec>(Json(spec))));
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], spec.Series[0].Labels);
        Assert.Equal((0, 100), (spec.Y2Axis.Min, spec.Y2Axis.Max));
    }

    [Fact]
    public void TheScalesAndAnglesSettings_GoOnTheirAxes()
    {
        var log = Charts.Line(new double[] { 1, 10, 100 }).LogY().Spec;
        var logX = Charts.Line(new double[] { 1, 10, 100 }).LogX().ReverseY().Spec;
        var time = Charts.Line(A).TimeX().ReverseX().Spec;
        var round = Charts.Donut(new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 }).Angles(-90, 180).Cutout(0.7).Spec;

        Assert.Equal(AxisScale.Log, log.YAxis.Scale);
        Assert.Equal((AxisScale.Log, true), (logX.XAxis.Scale!.Value, logX.YAxis.Reverse!.Value));
        Assert.Equal((AxisScale.Time, true), (time.XAxis.Scale!.Value, time.XAxis.Reverse!.Value));
        Assert.Equal((-90.0, 180.0, 0.7), (round.StartAngle!.Value, round.Sweep!.Value, round.Cutout!.Value));
    }

    [Fact]
    public void TheGaugeBuilder_ShowsValueOutOfMax()
    {
        var spec = Charts.Gauge(65, 100, "Disk").Spec;

        Assert.Equal([65.0, 35], spec.Series[0].Y);
        Assert.Equal("Disk", spec.Title);
        Assert.Empty(VisualSpecValidator.Validate(spec));
        Assert.Equal([100.0, 0], Charts.Gauge(150, 100).Spec.Series[0].Y); // clamped, never a negative slice
    }

    [Fact]
    public void StyleBeforeAnySeries_Says()
    {
        var empty = Charts.Line(new double[] { 1, 2 });
        empty.Spec.Series.Clear();

        Assert.Throws<InvalidOperationException>(() => empty.Style(s => s.Dashed()));
    }

    [Fact]
    public void ABuilderAndTheMatchingDisplayCall_ShowTheSameChart()
    {
        var data = new Dictionary<string, double[]> { ["a"] = A, ["b"] = B };

        Assert.Equal(Json(Spec(Display.StackedBarChart(data, "t", xLabel: "x", yLabel: "y"))), Json(Charts.Bar(data).Title("t").XLabel("x").YLabel("y").Stacked().Spec));
        Assert.Equal(Json(Spec(Display.RadarChart(data, "r", labels: ["a", "b", "c", "d"]))), Json(Charts.Radar(data, ["a", "b", "c", "d"]).Title("r").Spec));
    }

    [Fact]
    public void AScriptThatUsesTheNewChartApi_Compiles()
    {
        const string code = """
            Display.LineChart(("Sales", new double[] { 1, 3, 2 }), ("Costs", new double[] { 2, 2, 3 }));
            Display.StackedBarChart(new Dictionary<string, double[]> { ["a"] = new double[] { 1, 2 }, ["b"] = new double[] { 3, 1 } }, labels: new[] { "x", "y" });
            Display.BubbleChart(new[] { (1.0, 2.0, 8.0), (2.0, 3.0, 12.0) }, "Bubbles");
            Display.RadarChart(new Dictionary<string, double[]> { ["Ada"] = new double[] { 5, 6, 7 } }, labels: new[] { "a", "b", "c" });
            Display.DonutChart(new Dictionary<string, double> { ["done"] = 70, ["left"] = 30 }, gauge: true);
            Charts.Bar(("2025", new double[] { 4, 9 }), ("2026", new double[] { 6, 8 }))
                .Stacked().RightAxis("%", 0, 100).LegendAt(LegendPosition.Top)
                .Series("Margin", new double[] { 20, 30 }, s => s.Kind(ChartType.Line).OnRightAxis().Dashed().Step())
                .Show();
            Charts.Gauge(65, 100, "Disk").Show();
            Charts.Bubble(new[] { (1.0, 2.0, 8.0) }).Show();
            Charts.PolarArea(new Dictionary<string, double> { ["a"] = 3 }).Show();
            new[] { (1.0, 2.0, 8.0) }.DisplayBubbleChart();
            """;

        var (success, _, diagnostics) = new RoslynCompilerService().CompileToAssembly(code, ExecutionLanguageMode.Statements);

        Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void PairsOfTheUsualTypes_AreReadLikeAnyOther_AGapWhereAValueIsNotANumber()
    {
        var fast = Spec(Display.LineChart(new[] { (1.0, 2.0), (2.0, double.NaN), (3.0, 4.0) }));
        var slow = Spec(Display.LineChart(new[] { (1m, 2.0), (2m, double.NaN), (3m, 4.0) }));

        Assert.Equal(slow.Series[0].X, fast.Series[0].X);
        Assert.Equal(slow.Series[0].Y, fast.Series[0].Y);
        Assert.Equal([2.0, null, 4.0], fast.Series[0].Y);

        var labelled = Spec(Display.BarChart(new[] { ("Mon", 3.0), ("Tue", double.PositiveInfinity) }));
        Assert.Equal(["Mon", "Tue"], labelled.Series[0].Labels);
        Assert.Equal([3.0, null], labelled.Series[0].Y);

        var bubbles = Spec(Display.BubbleChart(new[] { (1.0, 2.0, 8.0), (2.0, double.NaN, 12.0) }));
        Assert.Equal([8.0, 12.0], bubbles.Series[0].Sizes);
        Assert.Equal([2.0, null], bubbles.Series[0].Y);
    }
}
