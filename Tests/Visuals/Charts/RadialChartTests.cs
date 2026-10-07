using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>Pie, donut (rings, start angle, sweep), polar area and radar: where they sit and what a pointer finds in them.</summary>
public class RadialChartTests
{
    private static readonly Rect Bounds = new(0, 0, 600, 400);

    private static ChartOptions Options(ChartType kind, Action<ChartSpec>? change = null, params ChartSeriesSpec[] series)
    {
        var spec = new ChartSpec { Kind = kind };
        spec.Series.AddRange(series);
        change?.Invoke(spec);
        return ChartRenderModelBuilder.Build(spec);
    }

    private static ChartSeriesSpec Slices(string name, params double?[] y) => new() { Name = name, Y = [.. y], Labels = y.Select((_, i) => (string?)$"s{i}").ToList() };

    [Fact]
    public void AFullCircle_IsCentredInItsBox_AtTheUsualSize()
    {
        var box = new Rect(0, 0, 400, 300);

        var (centre, radius) = RadialLayout.Fit(box, RadialLayout.Radians(0), 2 * Math.PI, 0);

        Assert.Equal(box.Center.X, centre.X, 6);
        Assert.Equal(box.Center.Y, centre.Y, 6);
        Assert.Equal(0.44 * 300, radius, 6);
    }

    [Fact]
    public void AHalfCircleGauge_SitsLowAndIsBiggerThanAWholeOne()
    {
        var box = new Rect(0, 0, 400, 300);
        var (_, whole) = RadialLayout.Fit(box, RadialLayout.Radians(0), 2 * Math.PI, 0.55);

        var (centre, radius) = RadialLayout.Fit(box, RadialLayout.Radians(-90), Math.PI, 0.55);

        Assert.True(radius > whole);
        Assert.True(centre.Y > box.Center.Y); // the open half has nothing in it
        Assert.True(centre.Y - radius >= box.Top - 1);
        Assert.True(centre.Y <= box.Bottom + 1);
    }

    [Fact]
    public void ARingEach_ForEverySeries_TheFirstOutermost()
    {
        var options = Options(ChartType.Donut, null, Slices("outer", 1, 1, 1, 1), Slices("inner", 2, 2));
        var renderer = new PieChartRenderer(isDonut: true);

        var near = renderer.HitTest(HitPoint(options, 0.95, angleFromTopDegrees: 10), Bounds, options);
        var far = renderer.HitTest(HitPoint(options, 0.62, angleFromTopDegrees: 10), Bounds, options);

        Assert.Equal("outer", near!.Series.Name);
        Assert.Equal("inner", far!.Series.Name);
    }

    // A point at a share of the outer radius and an angle (clockwise from 12 o'clock) of a donut that fills the chart.
    private static Point HitPoint(ChartOptions options, double shareOfRadius, double angleFromTopDegrees)
    {
        var area = new Rect(10, 10, Math.Max(50, Bounds.Width - Math.Min(160, Bounds.Width * 0.35) - 20), Math.Max(50, Bounds.Height - 20));
        var (centre, radius) = RadialLayout.Fit(area, RadialLayout.Radians(options.StartAngle), options.Sweep * Math.PI / 180, options.Type == ChartType.Donut ? options.Cutout : 0);
        return RadialLayout.PointAt(centre, radius * shareOfRadius, RadialLayout.Radians(options.StartAngle + angleFromTopDegrees));
    }

    [Fact]
    public void ASliceThatCrossesTheStart_IsFoundOnBothSides()
    {
        var options = Options(ChartType.Pie, s => s.StartAngle = 45, Slices("s", 1, 1, 2));
        var renderer = new PieChartRenderer();

        // The last slice (half the pie) starts at 180 degrees from the start and ends back at it.
        Assert.Equal("s2", Name(renderer.HitTest(HitPoint(options, 0.6, 200), Bounds, options)));
        Assert.Equal("s2", Name(renderer.HitTest(HitPoint(options, 0.6, 350), Bounds, options)));
        Assert.Equal("s0", Name(renderer.HitTest(HitPoint(options, 0.6, 10), Bounds, options)));
    }

    private static string? Name(ChartHitTestResult? hit) => hit?.Point.Label;

    [Fact]
    public void AHalfDonut_HasNoSliceInItsOpenHalf()
    {
        var options = Options(ChartType.Donut, s => (s.StartAngle, s.Sweep) = (-90, 180), Slices("s", 1, 1));
        var renderer = new PieChartRenderer(isDonut: true);

        Assert.NotNull(renderer.HitTest(HitPoint(options, 0.8, 45), Bounds, options));
        Assert.Null(renderer.HitTest(HitPoint(options, 0.8, 200), Bounds, options));
    }

    [Fact]
    public void AHiddenSeries_LosesItsRing()
    {
        var options = Options(ChartType.Donut, null, Slices("a", 1, 1), Slices("b", 1, 3));
        options.HiddenSeries = new HashSet<int> { 0 };
        var renderer = new PieChartRenderer(isDonut: true);

        var hit = renderer.HitTest(HitPoint(options, 0.9, 20), Bounds, options);

        Assert.Equal("b", hit!.Series.Name);
    }

    [Fact]
    public void APolarAreaSlice_ReachesAsFarAsItsValue()
    {
        var options = Options(ChartType.PolarArea, null, Slices("p", 4, 8));
        var renderer = new PolarAreaChartRenderer();
        var area = new Rect(10, 10, Bounds.Width - Math.Min(160, Bounds.Width * 0.35) - 20, Bounds.Height - 20);
        var (centre, radius) = RadialLayout.Fit(area, RadialLayout.Radians(0), 2 * Math.PI, 0);

        // Two slices of 180 degrees: the first on the right half, the second on the left. The scale ends at 10.
        var inFirst = RadialLayout.PointAt(centre, radius * 0.3, RadialLayout.Radians(90));
        var beyondFirst = RadialLayout.PointAt(centre, radius * 0.75, RadialLayout.Radians(90));
        var inSecond = RadialLayout.PointAt(centre, radius * 0.75, RadialLayout.Radians(270));

        Assert.Equal("s0", Name(renderer.HitTest(inFirst, Bounds, options)));
        Assert.Null(renderer.HitTest(beyondFirst, Bounds, options));
        Assert.Equal("s1", Name(renderer.HitTest(inSecond, Bounds, options)));
    }

    [Fact]
    public void ARadarHit_NamesTheSeriesAndTheSpoke()
    {
        var skills = new[] { "Speed", "Power", "Skill" };
        var options = Options(ChartType.Radar, null,
            new ChartSeriesSpec { Name = "Ada", Y = [10, 5, 5], Labels = [.. skills] },
            new ChartSeriesSpec { Name = "Bo", Y = [2, 2, 2], Labels = [.. skills] });
        var renderer = new RadarChartRenderer();

        // Up the first spoke, as far as Ada's 10 on the scale the chart draws (it leaves a step of room past the data).
        var top = RadarPoint(spoke: 0, spokes: 3, value: 10, values: [10, 5, 5, 2, 2, 2]);
        var hit = renderer.HitTest(top, Bounds, options);

        Assert.NotNull(hit);
        Assert.Equal("Ada", hit!.Series.Name);
        Assert.Contains("Speed", hit.DisplayText);
        Assert.Contains("10", hit.DisplayText);
    }

    [Fact]
    public void ARadarValueThatIsMissing_IsNotThere_ToPointAt()
    {
        var options = Options(ChartType.Radar, null, new ChartSeriesSpec { Name = "s", Y = [8, null, 8], Labels = ["a", "b", "c"] });
        var renderer = new RadarChartRenderer();

        // Nothing but the two values that exist can be hit, however close the pointer is to the empty spoke.
        for (var k = 0; k < 3; k++)
        {
            var onSpoke = RadarPoint(k, 3, 8, values: [8, 8]);
            var hit = renderer.HitTest(onSpoke, Bounds, options);
            Assert.Equal(k != 1, hit != null);
        }
    }

    // Where a value is on a spoke of a radar with these values (they decide the scale), as the renderer lays it out.
    private static Point RadarPoint(int spoke, int spokes, double value, double[] values)
    {
        var (min, max, _) = ChartDataRange.ValueAxis(values.Min(), values.Max(), null, null, false);
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 30;
        return RadialLayout.PointAt(Bounds.Center, radius * (value - min) / (max - min), RadialLayout.Radians(360.0 * spoke / spokes));
    }

    [Fact]
    public void TheFactoryGivesEachRoundKindItsRenderer()
    {
        Assert.IsType<PieChartRenderer>(ChartRendererFactory.GetRenderer(ChartType.Pie));
        Assert.IsType<PieChartRenderer>(ChartRendererFactory.GetRenderer(ChartType.Donut));
        Assert.IsType<PolarAreaChartRenderer>(ChartRendererFactory.GetRenderer(ChartType.PolarArea));
        Assert.IsType<RadarChartRenderer>(ChartRendererFactory.GetRenderer(ChartType.Radar));
    }
}
