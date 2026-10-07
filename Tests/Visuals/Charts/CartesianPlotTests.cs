using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers.Layers;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>How a cartesian chart is laid out: axes, slots, lanes, piles and what a pointer finds.</summary>
public class CartesianPlotTests
{
    private static readonly Rect Bounds = new(0, 0, 600, 300);

    private static ChartSeriesSpec S(string name, params double?[] y) => new() { Name = name, Y = [.. y] };

    private static CartesianPlot Plot(ChartSpec spec) => CartesianPlot.Create(ChartRenderModelBuilder.Build(spec), Bounds);

    private static ChartSpec Spec(ChartType kind, params ChartSeriesSpec[] series)
    {
        var spec = new ChartSpec { Kind = kind };
        spec.Series.AddRange(series);
        return spec;
    }

    [Fact]
    public void ALineChart_SpreadsItsValuesOverTheirX_WhileBarsGetASlotEach()
    {
        var line = Plot(Spec(ChartType.Line, S("a", 1, 2, 3)));
        var bars = Plot(Spec(ChartType.Bar, S("a", 1, 2, 3)));

        Assert.IsType<LinearAxis>(line.Index);
        Assert.Equal(line.Area.Left, line.IndexPx(0), 6);
        Assert.Equal(line.Area.Right, line.IndexPx(2), 6);

        Assert.IsType<CategoryAxis>(bars.Index);
        Assert.Equal(bars.Area.Width / 3, bars.SlotLength, 6);
        Assert.Equal(bars.Area.Left + bars.SlotLength / 2, bars.IndexPx(0), 6);
    }

    [Fact]
    public void AnyBarSeries_MakesTheIndexAxisSlots_ForTheLinesAlongsideIt()
    {
        var combo = Spec(ChartType.Line, S("a", 1, 2, 3), new ChartSeriesSpec { Name = "bars", Kind = ChartType.Bar, Y = [3, 2, 1] });

        var plot = Plot(combo);

        Assert.IsType<CategoryAxis>(plot.Index);
        Assert.Equal(plot.IndexPx(1), plot.At(plot.IndexValue(plot.Series[0], 1), 2, AxisSide.Left).X, 6);
    }

    [Fact]
    public void SideBySideBars_TakeOneLaneEach_AndAStackedPileTakesOne()
    {
        var grouped = Plot(Spec(ChartType.Bar, S("a", 1, 2), S("b", 3, 4), S("c", 5, 6)));
        var stacked = Plot(Spec(ChartType.Bar, S("a", 1, 2), S("b", 3, 4), S("c", 5, 6)).With(s => s.Stack = ChartStack.Stacked));
        var groups = Plot(Spec(ChartType.Bar, S("a", 1, 2), S("b", 3, 4), S("c", 5, 6)).With(s =>
        {
            s.Stack = ChartStack.Stacked;
            s.Series[0].Stack = s.Series[1].Stack = "x";
            s.Series[2].Stack = "y";
        }));

        Assert.Equal(3, grouped.LaneCount);
        Assert.Equal([0, 1, 2], grouped.Series.Select(l => l.Lane));
        Assert.Equal(1, stacked.LaneCount);
        Assert.Equal(2, groups.LaneCount);
        Assert.Equal([0, 0, 1], groups.Series.Select(l => l.Lane));
    }

    [Fact]
    public void AStack_StartsEachSeriesWhereTheOneUnderItEnds_AndGapsAddNothing()
    {
        var plot = Plot(Spec(ChartType.Bar, S("a", 1, 2, 3), S("b", 4, null, 6), S("c", -1, 5, -2)).With(s => s.Stack = ChartStack.Stacked));

        Assert.Equal([0.0, 0, 0], plot.Series[0].Base!);
        Assert.Equal([1.0, 2, 3], plot.Series[0].Top!);
        Assert.Equal([1.0, 2, 3], plot.Series[1].Base!);
        Assert.Equal([5.0, 2, 9], plot.Series[1].Top!);
        // Negative values pile downwards from the baseline, not onto the positive ones.
        Assert.Equal([0.0, 2, 0], plot.Series[2].Base!);
        Assert.Equal([-1.0, 7, -2], plot.Series[2].Top!);
        Assert.True(plot.Left.Max >= 9 && plot.Left.Min <= -2);
    }

    [Fact]
    public void APercentStack_ScalesEveryPlaceTo100()
    {
        var plot = Plot(Spec(ChartType.Bar, S("a", 1, 3), S("b", 3, 1)).With(s => s.Stack = ChartStack.Percent));

        Assert.Equal([25.0, 75], plot.Series[0].Top!.Select(v => Math.Round(v, 6)));
        Assert.Equal([100.0, 100], plot.Series[1].Top!.Select(v => Math.Round(v, 6)));
        Assert.Equal(100, plot.Left.Max, 6);
    }

    [Fact]
    public void BarsAndLinesStackSeparately_AndSeriesOnAnotherAxisDoNotStackWithThem()
    {
        var plot = Plot(Spec(ChartType.Bar, S("a", 1, 2), new ChartSeriesSpec { Name = "line", Kind = ChartType.Line, Y = [5, 5] }, new ChartSeriesSpec { Name = "right", Axis = AxisSide.Right, Y = [100, 200] }, S("b", 3, 4))
            .With(s => s.Stack = ChartStack.Stacked));

        Assert.Equal([1.0, 2], plot.Series[0].Top!);
        Assert.Equal([5.0, 5], plot.Series[1].Top!);
        Assert.Equal([100.0, 200], plot.Series[2].Top!);
        Assert.Equal([4.0, 6], plot.Series[3].Top!);
    }

    [Fact]
    public void ASecondAxis_HasItsOwnRange_AndPlacesItsSeriesOnIt()
    {
        var plot = Plot(Spec(ChartType.Line, S("small", 1, 2, 3), new ChartSeriesSpec { Name = "big", Axis = AxisSide.Right, Y = [1000, 2000, 3000] })
            .With(s => s.Y2Axis.Title = "Big"));

        Assert.NotNull(plot.Right);
        Assert.True(plot.Left.Max < 10);
        Assert.True(plot.Right!.Max >= 3000);
        // The same pixel height on each axis is a different value.
        Assert.Equal(plot.ValuePx(3, AxisSide.Left), plot.ValuePx(3000, AxisSide.Right), 0);
        Assert.True(plot.Area.Right < Bounds.Right - 40);
    }

    [Fact]
    public void AHiddenSeries_IsNotDrawn_AndDoesNotCountTowardsTheAxes()
    {
        var spec = Spec(ChartType.Line, S("small", 1, 2, 3), S("big", 1000, 2000, 3000));
        var options = ChartRenderModelBuilder.Build(spec);
        options.HiddenSeries = new HashSet<int> { 1 };

        var plot = CartesianPlot.Create(options, Bounds);

        Assert.Single(plot.Series);
        Assert.Equal(0, plot.Series[0].Index);
        Assert.True(plot.Left.Max < 10);
    }

    [Fact]
    public void ALogAxis_PutsEachPowerOfTenTheSameDistanceApart()
    {
        var plot = Plot(Spec(ChartType.Line, S("a", 1, 10, 100, 1000)).With(s => s.YAxis.Scale = AxisScale.Log));

        Assert.IsType<LogAxis>(plot.Left);
        var step = plot.ValuePx(10, AxisSide.Left) - plot.ValuePx(100, AxisSide.Left);
        Assert.Equal(step, plot.ValuePx(100, AxisSide.Left) - plot.ValuePx(1000, AxisSide.Left), 6);
        Assert.Equal([1.0, 10, 100, 1000], plot.Left.Ticks(7).Select(t => t.Value));
    }

    [Fact]
    public void ADecadeRange_IsRoundedOutToPowersOfTen()
    {
        Assert.Equal((1.0, 1000.0), LogAxis.Decades(3, 700));
        Assert.Equal((10.0, 100.0), LogAxis.Decades(10, 100));
        Assert.Equal((1.0, 10.0), LogAxis.Decades(-5, 4));
    }

    [Fact]
    public void ATimeAxis_PutsTicksOnCalendarUnits()
    {
        var day = 86_400_000.0;
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var week = TimeTicks.Between(start, start + 6 * day, 8).ToList();
        var year = TimeTicks.Between(start, start + 700 * day, 8).ToList();
        var months = TimeTicks.Between(start, start + 200 * day, 8).ToList();

        Assert.All(week, t => Assert.Equal(0, (t.Value - start) % day));
        Assert.Equal("1 Jan", week[0].Label);
        Assert.All(months, t => Assert.StartsWith("1", TimeTicks.ToDate(t.Value).Day.ToString()));
        Assert.Contains(months, t => t.Label == "Feb 2026");
        Assert.All(year, t => Assert.Equal(1, TimeTicks.ToDate(t.Value).Day));
        Assert.True(year.Count <= 9);
    }

    [Fact]
    public void ATimeScale_UsesTheXAsMilliseconds()
    {
        var start = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var series = new ChartSeriesSpec { Y = [1, 2, 3], X = [start, start + 86_400_000.0, start + 3 * 86_400_000.0] };
        var plot = Plot(Spec(ChartType.Line, series).With(s => s.XAxis.Scale = AxisScale.Time));

        Assert.IsType<TimeAxis>(plot.Index);
        Assert.True(plot.IndexPx(start + 86_400_000.0) < plot.IndexPx(start + 3 * 86_400_000.0));
        Assert.Equal((plot.IndexPx(start) + plot.IndexPx(start + 3 * 86_400_000.0)) / 2, plot.IndexPx(start + 1.5 * 86_400_000.0), 3);
    }

    [Fact]
    public void AHorizontalBarChart_TurnsThePlot_CategoriesDownTheLeftAndValuesAlongTheBottom()
    {
        var plot = Plot(Spec(ChartType.Bar, S("a", 1, 2, 3)).With(s => s.Orientation = ChartOrientation.Horizontal));

        Assert.True(plot.Horizontal);
        Assert.True(plot.IndexPx(0) < plot.IndexPx(2));
        Assert.Equal(plot.Area.Left, plot.ValuePx(0, AxisSide.Left), 6);
        Assert.True(plot.ValuePx(3, AxisSide.Left) > plot.ValuePx(1, AxisSide.Left));
        var at = plot.At(1, 2, AxisSide.Left);
        Assert.Equal(plot.ValuePx(2, AxisSide.Left), at.X, 6);
        Assert.Equal(plot.IndexPx(1), at.Y, 6);
    }

    [Fact]
    public void AReversedAxis_RunsTheOtherWay()
    {
        var plot = Plot(Spec(ChartType.Line, S("a", 1, 2, 3)).With(s => (s.XAxis.Reverse, s.YAxis.Reverse) = (true, true)));

        Assert.True(plot.IndexPx(0) > plot.IndexPx(2));
        Assert.True(plot.ValuePx(1, AxisSide.Left) < plot.ValuePx(3, AxisSide.Left));
    }

    [Fact]
    public void ASuggestedRange_WidensTheAxis_ButAFixedOneWins()
    {
        var suggested = Plot(Spec(ChartType.Line, S("a", 40, 50, 60)).With(s => (s.YAxis.SuggestedMin, s.YAxis.SuggestedMax) = (0, 200)));
        var fixedRange = Plot(Spec(ChartType.Line, S("a", 40, 50, 60)).With(s => (s.YAxis.Min, s.YAxis.Max, s.YAxis.SuggestedMax) = (30, 70, 200)));

        Assert.True(suggested.Left.Max >= 200);
        Assert.Equal((30, 70), (fixedRange.Left.Min, fixedRange.Left.Max));
    }

    [Fact]
    public void TheValueAxisOfBars_AlwaysHasZero_AndAnAreaFillsDownToIt()
    {
        var bars = Plot(Spec(ChartType.Bar, S("a", 50, 60)));
        var line = Plot(Spec(ChartType.Line, S("a", 50, 60)));

        Assert.Equal(0, bars.Left.Min);
        Assert.Equal(0, bars.Baseline(AxisSide.Left));
        Assert.Equal(0, line.Left.Min); // all-positive values start from zero, as before
    }

    [Fact]
    public void AMultiSeriesHit_ListsEverySeriesAtThePlace()
    {
        var options = ChartRenderModelBuilder.Build(Spec(ChartType.Line, new ChartSeriesSpec { Name = "A", Y = [1, 5, 3], Labels = ["x", "y", "z"] }, S("B", 2, 4, 6)));
        var plot = CartesianPlot.Create(options, Bounds);
        var spot = plot.At(1, 5, AxisSide.Left);

        var hit = new CartesianChartRenderer().HitTest(spot, Bounds, options);

        Assert.NotNull(hit);
        Assert.Equal("y", hit!.Header);
        Assert.Equal(2, hit.Rows!.Count);
        Assert.Contains(hit.Rows, r => r.Text == "A: 5");
        Assert.Contains(hit.Rows, r => r.Text == "B: 4");
    }

    [Fact]
    public void ASingleSeriesHit_IsAPlainValue()
    {
        var options = ChartRenderModelBuilder.Build(Spec(ChartType.Line, S("A", 1, 5, 3)));
        var plot = CartesianPlot.Create(options, Bounds);

        var hit = new CartesianChartRenderer().HitTest(plot.At(1, 5, AxisSide.Left), Bounds, options);

        Assert.NotNull(hit);
        Assert.Null(hit!.Rows);
        Assert.Equal(5, hit.Point.Y);
    }

    [Fact]
    public void ABarHit_FindsTheBarOfTheLanePointedAt()
    {
        var options = ChartRenderModelBuilder.Build(Spec(ChartType.Bar, S("A", 4, 8), S("B", 6, 2)));
        var plot = CartesianPlot.Create(options, Bounds);
        var rect = BarLayer.BarRect(plot, plot.Series[1], 1)!.Value;

        var hit = new CartesianChartRenderer().HitTest(new Point(rect.Center.X, plot.Area.Bottom - 2), Bounds, options);

        Assert.NotNull(hit);
        Assert.Equal("B", hit!.Series.Name);
        Assert.Equal(2, hit.Point.Y);
    }

    [Fact]
    public void ALongLine_IsHitWithoutLookingAtEveryValue()
    {
        var y = Enumerable.Range(0, 50_000).Select(i => (double?)Math.Sin(i / 500.0)).ToArray();
        var options = ChartRenderModelBuilder.Build(Spec(ChartType.Line, new ChartSeriesSpec { Y = [.. y] }));
        var plot = CartesianPlot.Create(options, Bounds);
        var spot = plot.At(25_000, y[25_000]!.Value, AxisSide.Left);

        var hit = new CartesianChartRenderer().HitTest(spot, Bounds, options);

        Assert.NotNull(hit);
        Assert.InRange(hit!.Point.X, 24_000, 26_000);
    }

    [Fact]
    public void TheBiggestBubble_FitsInsideThePlot_OnEveryEdge()
    {
        var bubbles = new ChartSeriesSpec { X = [0, 10], Y = [0, 10], Sizes = [20, 30] };
        var plot = Plot(Spec(ChartType.Bubble, bubbles));

        var small = plot.At(0, 0, AxisSide.Left);
        var big = plot.At(10, 10, AxisSide.Left);

        Assert.True(small.X - 20 >= plot.Area.Left - 0.5 && small.Y + 20 <= plot.Area.Bottom + 0.5);
        Assert.True(big.X + 30 <= plot.Area.Right + 0.5 && big.Y - 30 >= plot.Area.Top - 0.5);
    }

    [Fact]
    public void ARangeTheChartFixes_IsNotPaddedForBubbles()
    {
        var bubbles = new ChartSeriesSpec { X = [0, 10], Y = [0, 10], Sizes = [20, 30] };
        var plot = Plot(Spec(ChartType.Bubble, bubbles).With(s => (s.XAxis.Min, s.XAxis.Max) = (0, 10)));

        Assert.Equal((0, 10), (plot.Index.Min, plot.Index.Max));
    }

    [Fact]
    public void ABubbleChartOfManyValues_IsThinnedToACellEach_ButAShortOneIsDrawnWhole()
    {
        ChartSpec Cloud(int n)
        {
            var random = new Random(7);
            var cloud = new ChartSeriesSpec { X = [], Y = [], Sizes = [] };
            for (var i = 0; i < n; i++)
            {
                cloud.X!.Add(random.NextDouble() * 100);
                cloud.Y.Add(random.NextDouble() * 100);
                cloud.Sizes!.Add(2 + i % 9);
            }

            return Spec(ChartType.Bubble, cloud);
        }

        var few = Plot(Cloud(500));
        var many = Plot(Cloud(100_000));

        Assert.Equal(500, PointLayer.Bubbles(few, few.Series[0]).Count);

        var drawn = PointLayer.Bubbles(many, many.Series[0]);
        var cells = many.Area.Width * many.Area.Height / (8 * 8) + 1;
        Assert.True(drawn.Count <= 2 * cells + 100, $"{drawn.Count} bubbles for a plot of about {cells:F0} cells");
        Assert.True(drawn.Count < 100_000 / 4);

        // The biggest come first, so the small ones show on top.
        var sizes = drawn.Select(i => PointLayer.Radius(many.Series[0].Series, i)).ToList();
        Assert.Equal(sizes.OrderByDescending(r => r), sizes);
    }

    [Fact]
    public void ACategoryAxis_NamesItsPlacesWhenAskedFor_AndStillNamesEachOne()
    {
        var spec = Spec(ChartType.Bar, new ChartSeriesSpec { Y = [1, 2, 3], Labels = ["a", null, "c"] });
        var axis = (CategoryAxis)Plot(spec).Index;

        Assert.Equal(["a", "1", "c"], axis.Labels);
        Assert.Equal(3, axis.Labels.Count);
    }
}
