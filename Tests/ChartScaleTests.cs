using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// What a chart's axes span and what its header says: a missing value is a gap that counts for nothing, a range the
/// chart fixes is kept, and the spec's settings reach the drawing.
/// </summary>
public class ChartScaleTests
{
    private static ChartOptions Chart(params double[] values) => new()
    {
        Series = { new ChartSeries { Points = values.Select((y, i) => new ChartDataPoint(i, y)).ToList() } }
    };

    // One missing value made every bound NaN, so the whole chart, every series of it, came out blank.
    [Fact]
    public void AMissingValue_DoesntCountTowardsTheRange()
    {
        var range = ChartDataRange.Of(Chart(120, 132.5, double.NaN, 160));

        Assert.Equal((0.0, 3.0), (range.MinX, range.MaxX));
        Assert.Equal((0.0, 175.0, 25.0), (range.MinY, range.MaxY, range.StepY));
    }

    // Ticks were the padded range cut in four (180.03, 351.45); they are round numbers, and the axis runs tick to tick.
    [Theory]
    [InlineData(160.0, 7, 25.0)]
    [InlineData(7.0, 8, 1.0)]
    [InlineData(99_999.0, 8, 20_000.0)]
    [InlineData(1.0, 7, 0.2)]
    [InlineData(0.0, 7, 1.0)]
    public void ATickStep_IsARoundNumber(double span, int steps, double step)
    {
        Assert.Equal(step, ChartTicks.Step(span, steps), 9);
    }

    [Fact]
    public void Ticks_AreTheStepsMultiples_EndsIncluded_WithoutMinusZero()
    {
        Assert.Equal([0, 25, 50, 75, 100, 125, 150, 175], ChartTicks.Between(0, 175, 25));

        var around = ChartTicks.Between(-1, 1, 0.5).ToList();
        Assert.Equal([-1, -0.5, 0, 0.5, 1], around);
        Assert.False(double.IsNegative(around[2]));
    }

    [Fact]
    public void AValueThatWouldSitOnTheFrame_GetsAStepOfRoom()
    {
        var range = ChartDataRange.Of(Chart(0, 50, 100));

        Assert.Equal((0.0, 120.0, 20.0), (range.MinY, range.MaxY, range.StepY));
    }

    [Fact]
    public void ARangeTheChartFixes_IsKeptAsItIs()
    {
        var chart = Chart(5, 10, 15);
        chart.YMin = -10;
        chart.YMax = 100;
        chart.XMax = 10;

        var range = ChartDataRange.Of(chart);

        Assert.Equal((-10.0, 100.0), (range.MinY, range.MaxY));
        Assert.Equal((0.0, 10.0), (range.MinX, range.MaxX));
    }

    // The same negative value everywhere gave a range from -4 to -6: upside down.
    [Fact]
    public void AConstantNegativeSeries_HasARangeTheRightWayUp()
    {
        var range = ChartDataRange.Of(Chart(-5, -5, -5));

        Assert.True(range.MinY < -5 && range.MaxY > -5, $"{range.MinY} to {range.MaxY}");
    }

    // Bars of negative values grew from a zero above the plot.
    [Fact]
    public void BarsOfNegativeValues_HaveZeroInTheirRange()
    {
        var range = ChartDataRange.Of(Chart(-10, -4, -2), zeroBaseline: true);

        Assert.True(range.MinY < -10 && range.MaxY >= 0, $"{range.MinY} to {range.MaxY}");
    }

    [Fact]
    public void AChartWithNothingToDraw_StillHasARange()
    {
        var range = ChartDataRange.Of(Chart(double.NaN, double.NaN));

        Assert.True(range.MaxX > range.MinX && range.MaxY > range.MinY);
    }

    [Fact]
    public void ALine_BreaksAtEveryGap()
    {
        var points = Chart(1, 2, double.NaN, 4, double.NaN, double.NaN, 7, 8, 9).Series[0].Points;

        Assert.Equal([(0, 2), (3, 1), (6, 3)], ChartPoints.Runs(points));
    }

    // The average was the average of each series' average, and N counted the gaps.
    [Fact]
    public void TheHeader_SumsUpTheValuesThatAreThere()
    {
        var chart = Chart(10, double.NaN, 20);
        chart.Series.Add(new ChartSeries { Points = { new ChartDataPoint(0, 90) } });

        var stats = ChartStatistics.Of(chart)!.Value;

        Assert.Equal((10.0, 90.0, 40.0, 3, 1), (stats.Min, stats.Max, stats.Average, stats.Count, stats.Missing));
        Assert.EndsWith("N: 3 • 1 missing", stats.ToString());
        Assert.Equal(15, chart.Series[0].AverageY);
    }

    [Fact]
    public void AChartWithoutValues_HasNoHeaderStatistics()
    {
        Assert.Null(ChartStatistics.Of(new ChartOptions()));
    }

    // A line through 100,000 values drew all of them on the UI thread, every frame. Four a pixel column draw the same line.
    [Fact]
    public void ALongLine_IsDrawnThroughAtMostFourValuesAPixelColumn_KeepingItsExtremes()
    {
        var points = Chart(Enumerable.Range(0, 100_000).Select(i => System.Math.Sin(i / 700.0) * 100 + (i == 54_321 ? 500 : 0)).ToArray()).Series[0].Points;

        var drawn = ChartPoints.ForLine(points, 0, points.Count, columns: 500, x => x / 100_000 * 500);

        Assert.InRange(drawn.Count, 500, 2000);
        Assert.Equal(drawn.Order(), drawn);
        Assert.Equal(drawn.Distinct().Count(), drawn.Count);
        Assert.Contains(0, drawn);
        Assert.Contains(99_999, drawn);
        Assert.Contains(54_321, drawn); // the spike
        var lowest = Enumerable.Range(0, points.Count).MinBy(i => points[i].Y);
        Assert.Contains(lowest, drawn);
    }

    [Fact]
    public void ALineThatFitsItsPixels_OrGoesBackOnItself_IsDrawnWhole()
    {
        var few = Chart(1, 2, 3).Series[0].Points;
        var loop = new ChartOptions { Series = { new ChartSeries { Points = Enumerable.Range(0, 5000).Select(i => new ChartDataPoint(System.Math.Cos(i / 100.0), System.Math.Sin(i / 100.0))).ToList() } } }.Series[0].Points;

        Assert.Equal(3, ChartPoints.ForLine(few, 0, 3, columns: 500, x => x).Count);
        Assert.Equal(5000, ChartPoints.ForLine(loop, 0, 5000, columns: 500, x => (x + 1) * 250).Count);
    }

    [Fact]
    public void ScatterPointsOnTopOfEachOther_AreDrawnOnce()
    {
        var points = new[] { new ChartDataPoint(0, 0), new ChartDataPoint(0.5, 0.5), new ChartDataPoint(10, 10), new ChartDataPoint(double.NaN, 1) };

        var drawn = ChartPoints.ForScatter(points, p => (p.X, p.Y));

        Assert.Equal([0, 2], drawn);
    }

    [Fact]
    public void MoreBarsThanPixels_ShowTheTallestOfEachColumn()
    {
        var points = Chart(Enumerable.Range(0, 1000).Select(i => i % 100 == 37 ? -500.0 : i % 7).ToArray()).Series[0].Points;

        var dense = ChartPoints.ForBars(points, slotWidth: 0.01);
        var roomy = ChartPoints.ForBars(Chart(1, double.NaN, 3).Series[0].Points, slotWidth: 20);

        Assert.Equal(10, dense.Count);
        Assert.All(dense, i => Assert.Equal(-500, points[i].Y));
        Assert.Equal([0, 2], roomy);
    }

    // A missing x was put at the value's index, so the value was drawn somewhere it doesn't belong.
    [Fact]
    public void AMissingXInASpec_IsAGap()
    {
        var spec = new ChartSpec { Series = { new ChartSeriesSpec { X = [0, null, 2], Y = [1, 2, 3] } } };

        var points = ChartRenderModelBuilder.Build(spec).Series[0].Points;

        Assert.True(double.IsNaN(points[1].X));
        Assert.Equal([(0, 1), (2, 1)], ChartPoints.Runs(points));
    }

    // showPoints: false still marked every value of a series of up to 40.
    [Theory]
    [InlineData(true, 100, true)]
    [InlineData(false, 10, false)]
    [InlineData(null, 40, true)]
    [InlineData(null, 41, false)]
    public void WhetherValuesAreMarked_IsWhatTheSpecSays_OrSuitsTheirNumber(bool? showPoints, int values, bool marked)
    {
        var spec = new ChartSpec { ShowPoints = showPoints, Series = { new ChartSeriesSpec { Y = Enumerable.Range(0, values).Select(v => (double?)v).ToList() } } };

        Assert.Equal(marked, ChartRenderModelBuilder.Build(spec).ShowPoints);
    }

    // A C# chart said "no legend, 560 × 280" whether or not anyone asked, so it looked unlike the same chart from Python.
    [Fact]
    public void AChartsDefaultSettings_AreLeftOutOfItsSpec()
    {
        var spec = ChartOptionsConverter.ToSpec(new ChartOptions { Series = { new ChartSeries(), new ChartSeries() } });

        Assert.Null(spec.Width);
        Assert.Null(spec.Height);
        Assert.Null(spec.Legend.Show);
        Assert.True(ChartRenderModelBuilder.Build(spec).ShowLegend);
    }
}
