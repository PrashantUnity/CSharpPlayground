using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Visual specs as JSON: every fixture (the JSON every language must write) reads and writes back unchanged, and a spec
/// that can't be read says what is wrong and where, in words for whoever wrote it.
/// </summary>
public class VisualJsonTests
{
    private static readonly string FixtureFolder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Visuals");

    internal static IReadOnlyList<string> FixtureFiles() =>
        Directory.GetFiles(FixtureFolder, "*.json").Select(path => Path.GetFileName(path)).Order().ToList();

    public static TheoryData<string> Fixtures() => new(FixtureFiles());

    internal static VisualFamily FamilyOfFixture(string file) =>
        file.StartsWith("chart-", StringComparison.Ordinal) ? VisualFamily.Chart
        : file.StartsWith("plot3d-", StringComparison.Ordinal) ? VisualFamily.Plot3D
        : VisualFamily.Visualizer;

    internal static VisualSpec ReadFixture(string file) =>
        VisualJson.Deserialize(FamilyOfFixture(file), File.ReadAllText(Path.Combine(FixtureFolder, file)));

    [Fact]
    public void ThereAreFixturesForEveryKind()
    {
        var files = FixtureFiles();

        Assert.Contains(files, f => f.StartsWith("chart-"));
        Assert.Contains(files, f => f.StartsWith("plot3d-"));
        foreach (var kind in Enum.GetValues<VisualizerKind>())
        {
            Assert.Contains(files, f => FamilyOfFixture(f) == VisualFamily.Visualizer && ((VisualizerSpec)ReadFixture(f)).Kind == kind);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void AFixture_ReadsAndWritesBackTheSameJson(string file)
    {
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureFolder, file)));

        var written = JsonNode.Parse(VisualJson.Serialize(ReadFixture(file)));

        Assert.True(JsonNode.DeepEquals(expected, written), $"{file} came back as {written?.ToJsonString()}");
    }

    [Fact]
    public void WhatSaysNothing_IsLeftOut()
    {
        var json = VisualJson.Serialize(new ChartSpec { Kind = ChartType.Bar });

        Assert.Equal("""{"kind":"bar"}""", json);
    }

    [Fact]
    public void EnumsAreCamelCaseText()
    {
        var spec = new Plot3DSpec { Kind = Plot3DType.Graph3D, ColorMap = ColorMapPreset.CoolWarm };

        var json = VisualJson.Serialize(spec);

        Assert.Contains("\"kind\":\"graph\"", json);
        Assert.Contains("\"colorMap\":\"coolWarm\"", json);
    }

    [Fact]
    public void AnUnknownField_IsAnError_ThatNamesItAndGuessesTheMeaning()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<ChartSpec>("""{"kind": "line", "titel": "Sales"}"""));

        Assert.Contains("unknown field \"titel\"", error.Message);
        Assert.Contains("did you mean \"title\"?", error.Message);
        Assert.StartsWith("The chart spec isn't valid:", error.Message);
    }

    [Fact]
    public void AValueOfTheWrongType_SaysWhereAndWhatWasExpected()
    {
        var error = Assert.Throws<VisualSpecException>(() =>
            VisualJson.Deserialize<ChartSpec>("""{"kind": "line", "series": [{"y": [1, "two"]}]}"""));

        Assert.Contains("$.series[0].y[1]", error.Problem);
        Assert.Contains("must be a number or null", error.Problem);
    }

    [Fact]
    public void AnUnknownKind_ListsTheKnownOnes()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<ChartSpec>("""{"kind": "spline"}"""));

        Assert.Contains("$.kind", error.Problem);
        Assert.Contains("\"line\"", error.Problem);
        Assert.Contains("\"histogram\"", error.Problem);
    }

    [Fact]
    public void AMissingRequiredField_IsNamed()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<VisualizerSpec>(
            """{"kind": "graph", "state": {"graph": {"nodes": [{"label": "no id"}]}}}"""));

        Assert.Contains("missing \"id\"", error.Problem);
    }

    [Fact]
    public void NullWhereAValueIsNeeded_IsAnError()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<ChartSpec>("""{"kind": "line", "xAxis": null}"""));

        Assert.Contains("$.xAxis", error.Problem);
        Assert.Contains("can't be null", error.Problem);
    }

    [Fact]
    public void BrokenJson_IsAnError_NotACrash()
    {
        var error = Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<ChartSpec>("""{"kind": """));

        Assert.StartsWith("The chart spec isn't valid:", error.Message);
    }

    [Fact]
    public void Elements_AreNamedInFourShortForms()
    {
        var spec = VisualJson.Deserialize<VisualizerSpec>("""
            {"kind": "graph", "state": {"graph": {"nodes": [{"id": "a"}, {"id": "b"}]}},
             "steps": [{"highlight": [3, "a", [2, 1], {"to": "b", "from": "a"}]}]}
            """);

        var highlight = spec.Steps[0].Highlight!;
        Assert.Equal(new[] { ElementRef.Item(3), ElementRef.Node("a"), ElementRef.Cell(2, 1), ElementRef.Edge("a", "b") }, highlight);
        Assert.Contains("\"highlight\":[3,\"a\",[2,1],{\"from\":\"a\",\"to\":\"b\"}]", VisualJson.Serialize(spec));
    }

    [Theory]
    [InlineData("""{"from": "a"}""")]
    [InlineData("""[1, 2, 3]""")]
    [InlineData("""true""")]
    [InlineData("""{"from": "a", "to": "b", "weight": 2}""")]
    public void AnElementInAnotherForm_IsAnError(string element)
    {
        Assert.Throws<VisualSpecException>(() => VisualJson.Deserialize<VisualizerSpec>(
            $$$"""{"kind": "matrix", "state": {"grid": {"values": [[1]]}}, "steps": [{"highlight": [{{{element}}}]}]}"""));
    }

    [Fact]
    public void Values_ReadTheSameWhateverLanguageSentThem()
    {
        Assert.Equal("∞", ScalarValue.FromInteger(int.MaxValue).ToString());
        Assert.Equal("-∞", ScalarValue.FromInteger(long.MinValue).ToString());
        Assert.Equal("1.5", ScalarValue.FromReal(1.5).ToString());
        Assert.Equal("true", ScalarValue.FromBoolean(true).ToString());
        Assert.Equal("null", ScalarValue.Null.ToString());
        Assert.Equal("a", ScalarValue.From('a').ToString());
        Assert.Equal("a", ((ScalarValue)'a').ToString());
        Assert.Equal(ScalarKind.Integer, ScalarValue.From((byte)7).Kind);

        // JSON has no infinity: it travels as the text it is shown as.
        Assert.Equal(ScalarValue.FromText("∞"), ScalarValue.FromReal(double.PositiveInfinity));
        Assert.Equal(ScalarValue.FromText("NaN"), ScalarValue.From(double.NaN));
    }

    [Fact]
    public void Clone_IsADeepCopy()
    {
        var original = (ChartSpec)ReadFixture("chart-line.json");

        var copy = VisualJson.Clone(original);
        copy.Series[0].Y[0] = -1;
        copy.XAxis.Title = "changed";

        Assert.Equal(120, original.Series[0].Y[0]);
        Assert.Equal("Month", original.XAxis.Title);
    }
}
