using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>The new chart settings (combos, stacks, axes, scales, line styles, bubbles, radial kinds) read and write as JSON and are checked.</summary>
public class ChartSpecRulesTests
{
    private static ChartSeriesSpec Series(params double?[] y) => new() { Y = [.. y] };

    private static ChartSpec Chart(ChartType kind, params ChartSeriesSpec[] series)
    {
        var spec = new ChartSpec { Kind = kind };
        spec.Series.AddRange(series);
        return spec;
    }

    private static IReadOnlyList<VisualSpecIssue> Issues(ChartSpec spec) => VisualSpecValidator.Validate(spec);

    private static ChartSpec Rich() => new()
    {
        Kind = ChartType.Bar,
        Title = "Revenue and margin",
        Stack = ChartStack.Stacked,
        XAxis = { Title = "Month", Scale = AxisScale.Category },
        YAxis = { Title = "USD", SuggestedMin = 0, SuggestedMax = 100, Scale = AxisScale.Linear },
        Y2Axis = { Title = "%", Min = 0, Max = 100, Reverse = true },
        Legend = { Show = true, Position = LegendPosition.Right },
        Series =
        {
            new ChartSeriesSpec { Name = "North", Y = [4, 5, 6], Stack = "a", CornerRadius = 4 },
            new ChartSeriesSpec { Name = "South", Y = [3, 2, 1], Stack = "a", From = [0, 0, 0] },
            new ChartSeriesSpec
            {
                Name = "Margin", Kind = ChartType.Line, Axis = AxisSide.Right, Y = [10, 20, 15],
                Dash = LineDash.Dashed, Interpolation = LineInterpolation.Smooth, Tension = 0.6, Fill = true, PointStyle = PointShape.Star, PointRadius = 5
            },
            new ChartSeriesSpec { Name = "Steps", Kind = ChartType.Area, Y = [1, 2, 3], Step = LineStep.After, FillTo = 0, Stack = "b" },
            new ChartSeriesSpec { Name = "Segments", Kind = ChartType.Line, Y = [1, 2, 3], ColorSegments = true, Colors = ["#f00", null, "#0f0"] }
        }
    };

    [Fact]
    public void ARichComboSpec_IsValid_AndRoundTripsThroughJson()
    {
        var spec = Rich();
        Assert.Empty(Issues(spec));

        var json = VisualJson.Serialize(spec);
        var again = VisualJson.Deserialize<ChartSpec>(json);

        Assert.Equal(json, VisualJson.Serialize(again));
        Assert.Equal(AxisScale.Category, again.XAxis.Scale);
        Assert.Equal(LegendPosition.Right, again.Legend.Position);
        Assert.Equal(PointShape.Star, again.Series[2].PointStyle);
    }

    [Fact]
    public void TheNewSettings_AreWrittenInCamelCase_AndLeftOutWhenNotSet()
    {
        var json = VisualJson.Serialize(new ChartSpec
        {
            Kind = ChartType.PolarArea,
            Series = { new ChartSeriesSpec { Y = [1, 2] } },
            StartAngle = 90,
            Sweep = 180
        });

        Assert.Contains("\"kind\":\"polarArea\"", json);
        Assert.Contains("\"startAngle\":90", json);
        Assert.DoesNotContain("y2Axis", json);
        Assert.DoesNotContain("orientation", json);
        Assert.DoesNotContain("legend", json);

        var series = VisualJson.Serialize(new ChartSpec
        {
            Kind = ChartType.Line,
            Series = { new ChartSeriesSpec { Y = [1], ColorSegments = true, FillTo = 0, CornerRadius = 2, PointStyle = PointShape.Diamond } }
        });
        Assert.Contains("\"colorSegments\":true", series);
        Assert.Contains("\"fillTo\":0", series);
        Assert.Contains("\"pointStyle\":\"diamond\"", series);
    }

    [Fact]
    public void AnUnknownSettingIsStillAMistake_WithASuggestion()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<ChartSpec>("""{"kind":"line","series":[{"y":[1],"fillto":0,"dashes":"dashed"}]}"""));

        Assert.Contains("$.series[0]", error.Message);
    }

    public static TheoryData<string> BadCases() => new(Bad.Keys);

    private static readonly Dictionary<string, (Func<ChartSpec> Spec, string Path, string Says)> Bad = new()
    {
        ["horizontal line"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.Orientation = ChartOrientation.Horizontal), "$.orientation", "bar"),
        ["stack on scatter"] = (() => Chart(ChartType.Scatter, Series(1, 2)).With(s => s.Stack = ChartStack.Stacked), "$.stack", "stack"),
        ["angle on a line"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.StartAngle = 10), "$.startAngle", "pie"),
        ["sweep too big"] = (() => Chart(ChartType.Pie, Series(1, 2)).With(s => s.Sweep = 400), "$.sweep", "360"),
        ["cutout on a pie"] = (() => Chart(ChartType.Pie, Series(1, 2)).With(s => s.Cutout = 0.5), "$.cutout", "donut"),
        ["cutout too big"] = (() => Chart(ChartType.Donut, Series(1, 2)).With(s => s.Cutout = 1), "$.cutout", "0.95"),
        ["y2 with nothing on it"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.Y2Axis.Title = "right"), "$.y2Axis", "right axis"),
        ["scale on a pie"] = (() => Chart(ChartType.Pie, Series(1, 2)).With(s => s.XAxis.Scale = AxisScale.Log), "$.xAxis", "no axis"),
        ["two polar series"] = (() => Chart(ChartType.PolarArea, Series(1, 2), Series(3, 4)), "$.series", "one series"),
        ["kind on a pie"] = (() => Chart(ChartType.Pie, new ChartSeriesSpec { Y = [1], Kind = ChartType.Line }), "$.series[0].kind", "pie"),
        ["a pie kind in a combo"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1], Kind = ChartType.Pie }), "$.series[0].kind", "line, area, bar or scatter"),
        ["dash on a bar"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1], Dash = LineDash.Dashed }), "$.series[0].dash", "line, area or radar"),
        ["step on a bar"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1], Step = LineStep.After }), "$.series[0].step", "line or area"),
        ["tension without smooth"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], Tension = 0.3 }), "$.series[0].tension", "smooth"),
        ["tension out of range"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], Interpolation = LineInterpolation.Smooth, Tension = 2 }), "$.series[0].tension", "between 0 and 1"),
        ["step and smooth"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], Step = LineStep.Before, Interpolation = LineInterpolation.Monotone }), "$.series[0].step", "interpolation"),
        ["segments on a smooth line"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], ColorSegments = true, Interpolation = LineInterpolation.Smooth }), "$.series[0].colorSegments", "straight"),
        ["segments on a stepped line"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], ColorSegments = true, Step = LineStep.After }), "$.series[0].colorSegments", "straight"),
        ["fillTo missing series"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], FillTo = 5 }), "$.series[0].fillTo", "no series 5"),
        ["fillTo itself"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1, 2], FillTo = 0 }), "$.series[0].fillTo", "itself"),
        ["fillTo with fill off"] = (() => Chart(ChartType.Line, Series(1, 2), new ChartSeriesSpec { Y = [1, 2], FillTo = 0, Fill = false }), "$.series[1].fillTo", "fill is off"),
        ["markers on a bar"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1], PointStyle = PointShape.Star }), "$.series[0].pointStyle", "line, area, scatter, bubble or radar"),
        ["bubble without sizes"] = (() => Chart(ChartType.Bubble, Series(1, 2)), "$.series[0].sizes", "needs sizes"),
        ["bubble size zero"] = (() => Chart(ChartType.Bubble, new ChartSeriesSpec { Y = [1, 2], Sizes = [3, 0] }), "$.series[0].sizes[1]", "above 0"),
        ["bubble sizes length"] = (() => Chart(ChartType.Bubble, new ChartSeriesSpec { Y = [1, 2], Sizes = [3] }), "$.series[0].sizes", "has 1 values but y has 2"),
        ["sizes on a line"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1], Sizes = [3] }), "$.series[0].sizes", "bubble"),
        ["from on a line"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1], From = [0] }), "$.series[0].from", "bars"),
        ["from length"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1, 2], From = [0] }), "$.series[0].from", "has 1 values but y has 2"),
        ["corner on a line"] = (() => Chart(ChartType.Line, new ChartSeriesSpec { Y = [1], CornerRadius = 3 }), "$.series[0].cornerRadius", "bars"),
        ["corner negative"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1], CornerRadius = -1 }), "$.series[0].cornerRadius", "0 or more"),
        ["right axis on a pie"] = (() => Chart(ChartType.Pie, new ChartSeriesSpec { Y = [1], Axis = AxisSide.Right }), "$.series[0].axis", "no right axis"),
        ["series stack on an unstacked chart"] = (() => Chart(ChartType.Bar, new ChartSeriesSpec { Y = [1], Stack = "a" }), "$.series[0].stack", "isn't stacked"),
        ["percent with a negative"] = (() => Chart(ChartType.Bar, Series(1, -2)).With(s => s.Stack = ChartStack.Percent), "$.series[0].y[1]", "negative"),
        ["log y with zero"] = (() => Chart(ChartType.Line, Series(1, 0, 3)).With(s => s.YAxis.Scale = AxisScale.Log), "$.series[0].y[1]", "above 0"),
        ["log y2 uses the right axis"] = (() => Chart(ChartType.Line, Series(1, 2), new ChartSeriesSpec { Y = [1, -1], Axis = AxisSide.Right }).With(s => s.Y2Axis.Scale = AxisScale.Log), "$.series[1].y[1]", "above 0"),
        ["log x without x"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.XAxis.Scale = AxisScale.Log), "$.series[0].x", "log x axis"),
        ["time without x"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.XAxis.Scale = AxisScale.Time), "$.series[0].x", "Unix milliseconds"),
        ["time on y"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => s.YAxis.Scale = AxisScale.Time), "$.yAxis.scale", "linear or log"),
        ["suggested range reversed"] = (() => Chart(ChartType.Line, Series(1, 2)).With(s => (s.YAxis.SuggestedMin, s.YAxis.SuggestedMax) = (5, 5)), "$.yAxis", "suggestedMin must be below"),
        ["histogram series dash"] = (() => Chart(ChartType.Histogram, new ChartSeriesSpec { Values = [1, 2, 3], Dash = LineDash.Dotted }), "$.series[0].dash", "histogram"),
    };

    [Theory]
    [MemberData(nameof(BadCases))]
    public void AMistake_IsReportedAtItsPath_InPlainWords(string name)
    {
        var (spec, path, says) = Bad[name];

        var issues = Issues(spec());

        Assert.Contains(issues, i => i.Path == path && i.Message.Contains(says, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ADrawnSettingOn3DAxes_IsRefused()
    {
        var plot = new Plot3DSpec { Kind = Plot3DType.Scatter, Series = { new Plot3DSeriesSpec { X = [1], Y = [1], Z = [1] } } };
        plot.ZAxis.Scale = AxisScale.Log;

        Assert.Contains(VisualSpecValidator.Validate(plot), i => i.Path == "$.zAxis" && i.Message.Contains("2D charts"));
    }

    [Fact]
    public void EveryKindOfChart_CanBeASpecWithOneSeries()
    {
        foreach (var kind in Enum.GetValues<ChartType>())
        {
            var series = kind switch
            {
                ChartType.Histogram => new ChartSeriesSpec { Values = [1, 2, 3] },
                ChartType.Bubble => new ChartSeriesSpec { Y = [1, 2], Sizes = [4, 6] },
                _ => new ChartSeriesSpec { Y = [1, 2, 3] }
            };

            Assert.Empty(Issues(Chart(kind, series)));
        }
    }
}

internal static class ChartSpecTestExtensions
{
    // A spec after a change, in an expression.
    public static ChartSpec With(this ChartSpec spec, Action<ChartSpec> change)
    {
        change(spec);
        return spec;
    }
}
