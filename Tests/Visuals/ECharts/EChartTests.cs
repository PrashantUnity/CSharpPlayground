using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>EChart: the option each factory and setting writes, and the page a chart is drawn in.</summary>
public class EChartTests
{
    private static readonly double[] Sales = [12, 30, 22, 41];

    private static JsonObject Series(EChart chart, int index = 0) => (JsonObject)((JsonArray)chart.Option["series"]!)[index]!;

    private static string Text(JsonNode? node) => node!.GetValue<string>();

    private static RichCellOutput Shown(Func<EChart> make)
    {
        RichCellOutput? output = null;
        using (InteractiveDisplayContext.EnterScope(o => output ??= o)) make().Show();
        return output!;
    }

    // ── What each factory makes ─────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, Func<EChart>, string> Factories() => new()
    {
        { "line", () => EChart.Line(Sales), "line" },
        { "area", () => EChart.Area(Sales), "line" },
        { "bar", () => EChart.Bar(Sales), "bar" },
        { "scatter", () => EChart.Scatter(new[] { (1.0, 2.0), (3.0, 4.0) }), "scatter" },
        { "pie", () => EChart.Pie(new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 }), "pie" },
        { "donut", () => EChart.Donut(new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 }), "pie" },
        { "funnel", () => EChart.Funnel(new Dictionary<string, double> { ["seen"] = 100, ["bought"] = 20 }), "funnel" },
        { "radar", () => EChart.Radar(new Dictionary<string, double> { ["speed"] = 3, ["power"] = 7 }), "radar" },
        { "heatmap", () => EChart.Heatmap(new double[,] { { 1, 2 }, { 3, 4 } }), "heatmap" },
        { "candlestick", () => EChart.Candlestick(new[] { new[] { 10.0, 12, 9, 13 }, new[] { 12.0, 11, 10, 12.5 } }), "candlestick" },
        { "gauge", () => EChart.Gauge(72), "gauge" },
        { "treemap", () => EChart.Treemap(new Dictionary<string, object> { ["src"] = new Dictionary<string, double> { ["a.cs"] = 3 }, ["docs"] = 2 }), "treemap" },
        { "sunburst", () => EChart.Sunburst(new Dictionary<string, double> { ["a"] = 1, ["b"] = 2 }), "sunburst" },
        { "sankey", () => EChart.Sankey(new[] { ("Visit", "Cart", 40.0), ("Cart", "Buy", 12.0) }), "sankey" },
        { "graph", () => EChart.Graph(new Dictionary<string, string[]> { ["a"] = ["b", "c"], ["b"] = ["c"], ["d"] = [] }), "graph" },
        { "surface", () => EChart.Surface((x, y) => x * y, resolution: 5), "surface" },
        { "bar3D", () => EChart.Bar3D(new double[,] { { 1, 2 }, { 3, 4 } }), "bar3D" },
        { "scatter3D", () => EChart.Scatter3D(new[] { (1.0, 2.0, 3.0), (4.0, 5.0, 6.0) }), "scatter3D" }
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void EveryFactory_MakesASeriesOfItsKind_AndAPageThatShowsIt(string name, Func<EChart> make, string type)
    {
        var chart = make();

        Assert.True(Text(Series(chart)["type"]) == type, name);
        var html = chart.ToOutput().HtmlContent!;
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("chart.setOption(option)", html);
    }

    [Fact]
    public void TheSameData_ReadsTheSameAsCharts()
    {
        var data = new Dictionary<string, double> { ["North"] = 4, ["South"] = 9, ["East"] = 2 };

        var spec = Charts.Bar(data).Spec.Series[0];
        var chart = EChart.Bar(data);

        Assert.Equal(spec.Labels, ((JsonArray)chart.Option["xAxis"]!["data"]!).Select(n => (string?)Text(n)));
        Assert.Equal(spec.Y, ((JsonArray)Series(chart)["data"]!).Select(n => (double?)n!.GetValue<double>()));
    }

    [Fact]
    public void Records_AreReadByTheirSelectors()
    {
        var sales = new[] { new { Region = "EU", Revenue = 10 }, new { Region = "US", Revenue = 20 } };

        var chart = EChart.Bar(sales, s => s.Region, s => s.Revenue, "Revenue");

        Assert.Equal(["EU", "US"], ((JsonArray)chart.Option["xAxis"]!["data"]!).Select(Text));
        Assert.Equal("Revenue", Text(Series(chart)["name"]));
    }

    [Fact]
    public void ASeriesAdded_LinesUpWithTheCategories_AndCanBeAnotherKind()
    {
        var chart = EChart.Bar(new Dictionary<string, double> { ["Q1"] = 1, ["Q2"] = 2 }, "Revenue")
            .Series("Profit", new Dictionary<string, double> { ["Q2"] = 5, ["Q3"] = 6 }, EChartType.Line);

        Assert.Equal(["Q1", "Q2", "Q3"], ((JsonArray)chart.Option["xAxis"]!["data"]!).Select(Text));
        var profit = Series(chart, 1);
        Assert.Equal("line", Text(profit["type"]));
        Assert.Equal([null, 5.0, 6.0], ((JsonArray)profit["data"]!).Select(n => n?.GetValue<double>()));
    }

    [Fact]
    public void Horizontal_PutsTheCategoriesDownTheSide()
    {
        var chart = EChart.Bar(new Dictionary<string, double> { ["a"] = 1 }).Horizontal().Stacked();

        Assert.Equal("value", Text(chart.Option["xAxis"]!["type"]));
        Assert.Equal("category", Text(chart.Option["yAxis"]!["type"]));
        Assert.Equal("total", Text(Series(chart)["stack"]));
    }

    [Fact]
    public void AHeatmap_HasTheAxesAndTheColourScaleECharts_Needs()
    {
        var chart = EChart.Heatmap(new double[,] { { 1, 2 }, { 3, double.NaN } }, ["a", "b"], ["top", "bottom"]);

        Assert.Equal(["a", "b"], ((JsonArray)chart.Option["xAxis"]!["data"]!).Select(Text));
        Assert.Equal(["top", "bottom"], ((JsonArray)chart.Option["yAxis"]!["data"]!).Select(Text));
        Assert.Equal(1, chart.Option["visualMap"]!["min"]!.GetValue<double>());
        Assert.Equal(3, chart.Option["visualMap"]!["max"]!.GetValue<double>());
        var cells = (JsonArray)Series(chart)["data"]!;
        Assert.Equal(3, cells.Count); // the NaN cell is empty
        Assert.Equal("[1,0,2]", cells[1]!.ToJsonString());
    }

    [Fact]
    public void ARadar_HasItsSpokes_EachToARoundNumberAboveItsLargestValue()
    {
        var chart = EChart.Radar(new Dictionary<string, double> { ["speed"] = 3, ["power"] = 42 });

        var spokes = (JsonArray)chart.Option["radar"]!["indicator"]!;
        Assert.Equal(["speed", "power"], spokes.Select(s => Text(s!["name"])));
        Assert.Equal([5.0, 50.0], spokes.Select(s => s!["max"]!.GetValue<double>()));
    }

    // ── 3D ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ASurface_IsRowsOfY_AlongX_WithItsShapeSaid_AndAHoleWhereTheFunctionIsNaN()
    {
        var chart = EChart.Surface((x, y) => x < 0 ? double.NaN : x + y, (-1, 1), (0, 2), resolution: 3);

        var surface = Series(chart);
        Assert.Equal("[3,3]", surface["dataShape"]!.ToJsonString());
        var points = ((JsonArray)surface["data"]!).Select(p => (JsonArray)p!).ToList();
        Assert.Equal(9, points.Count);
        Assert.Equal(["[-1,0,null]", "[0,0,0]", "[1,0,1]"], points.Take(3).Select(p => p.ToJsonString()));
        Assert.Equal(2, chart.Option["visualMap"]!["dimension"]!.GetValue<int>());
        Assert.False(chart.Option["grid3D"]!["viewControl"]!["autoRotate"]!.GetValue<bool>());
    }

    [Fact]
    public void AParametricSurface_IsDrawnAsOne_WithUAndVInEachPoint()
    {
        var chart = EChart.ParametricSurface(
            (u, v) => Math.Cos(u) * Math.Cos(v), (u, v) => Math.Sin(u) * Math.Cos(v), (u, v) => Math.Sin(v),
            (0, Math.PI), (0, 1), resolution: 4);

        var surface = Series(chart);
        Assert.True(surface["parametric"]!.GetValue<bool>());
        Assert.Equal("[4,4]", surface["dataShape"]!.ToJsonString());
        var first = (JsonArray)((JsonArray)surface["data"]!)[1]!;
        Assert.Equal(5, first.Count);
        Assert.Equal(Math.PI / 3, first[3]!.GetValue<double>(), 6); // u runs along each row
        Assert.Equal(0, first[4]!.GetValue<double>());
    }

    [Fact]
    public void Bar3D_KeepsTheCategoriesInTheOrderGiven_NotTheOrderSeen()
    {
        var hours = new[] { "12a", "1a", "2a" };
        var activity = new[] { (Hour: "2a", Day: "Mon", Count: 3), (Hour: "12a", Day: "Tue", Count: 1) };

        var chart = EChart.Bar3D(activity, a => a.Hour, a => a.Day, a => a.Count, xLabels: hours);

        Assert.Equal(hours, ((JsonArray)chart.Option["xAxis3D"]!["data"]!).Select(Text));
        Assert.Equal(["Mon", "Tue"], ((JsonArray)chart.Option["yAxis3D"]!["data"]!).Select(Text));
        Assert.Equal("[2,0,3]", ((JsonArray)Series(chart)["data"]!)[0]!.ToJsonString());
    }

    [Fact]
    public void Settings3D_ReachTheSurface()
    {
        var chart = EChart.Surface((x, y) => x * y, resolution: 4)
            .Wireframe().Shading(SurfaceShading.Realistic).AutoRotate(speed: 5).ColorMap(ColorMapPreset.Plasma).ZLabel("height");

        var surface = Series(chart);
        Assert.True(surface["wireframe"]!["show"]!.GetValue<bool>());
        Assert.Equal("realistic", Text(surface["shading"]));
        Assert.Equal(5, chart.Option["grid3D"]!["viewControl"]!["autoRotateSpeed"]!.GetValue<double>());
        Assert.Equal(9, ((JsonArray)chart.Option["visualMap"]!["inRange"]!["color"]!).Count);
        Assert.Equal("height", Text(chart.Option["zAxis3D"]!["name"]));
    }

    // ── The page ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AJsFunc_IsWrittenAsCode_WhereverItIs()
    {
        var set = EChart.Line(Sales).Set("tooltip.formatter", JsFunc.From("p => p.name + ': ' + p.value")).ToOutput().HtmlContent!;
        var inAnObject = EChart.FromOption(new { series = new[] { new { type = "pie", label = new { formatter = JsFunc.From("function (p) { return p.name; }") } } } })
            .ToOutput().HtmlContent!;

        Assert.Contains("\"formatter\":p => p.name + ': ' + p.value", set);
        Assert.Contains("\"formatter\":function (p) { return p.name; }", inAnObject);
        Assert.DoesNotContain("__fry_js__", set + inAnObject);
    }

    [Fact]
    public void TextThatClosesAScript_StaysInsideTheChartsScript()
    {
        var html = EChart.Line(Sales).Title("</script><script>alert(1)</script>")
            .Set("tooltip.formatter", JsFunc.From("p => '</script>'")).ToOutput().HtmlContent!;

        Assert.DoesNotContain("<script>alert(1)", html);
        Assert.Contains("<title>&lt;/script&gt;&lt;script&gt;alert(1)&lt;/script&gt;</title>", html);
        Assert.Contains("p => '<\\/script>'", html);
        Assert.Equal(2, html.Split("</script>").Length - 1); // the library reference and the chart's script only
    }

    [Fact]
    public void BadJson_IsRefused_WithWhereItWentWrong()
    {
        var error = Assert.Throws<ArgumentException>(() => EChart.FromJson("{ \"series\": [ { \"type\": 'pie' } ] }"));

        Assert.Contains("LineNumber", error.Message);
        Assert.Contains("EChart.FromJs", error.Message);
        Assert.Throws<ArgumentException>(() => EChart.FromJson("[1, 2]"));
    }

    [Fact]
    public void ARawOption_TakesSettingsOnTopOfIt()
    {
        var fromJson = EChart.FromJson("""{ "series": [{ "type": "pie", "data": [1, 2] }] }""").Title("Raw");
        var fromObject = EChart.FromOption(new { series = new[] { new { type = "pie", data = new[] { 1, 2 } } } }).Title("Object");

        Assert.Equal("Raw", fromJson.ChartTitle);
        Assert.Equal("pie", Text(Series(fromJson)["type"]));
        Assert.Equal("Object", fromObject.ChartTitle);
        Assert.Equal("pie", Text(Series(fromObject)["type"]));
    }

    [Fact]
    public void AJavaScriptOption_RunsInThePage_AndTheSettingsGoOnTop()
    {
        var html = EChart.FromJs("option = { xAxis: { type: 'category' }, yAxis: {}, series: [{ type: 'bar', data: [1, 2] }] };")
            .Title("From an example").ToOutput().HtmlContent!;

        Assert.Contains("series: [{ type: 'bar', data: [1, 2] }]", html);
        Assert.Contains("if (base) chart.setOption(base);", html);
        Assert.Contains("\"title\":{\"text\":\"From an example\"", html);
    }

    [Fact]
    public void Echarts_Gl_IsOnlyLoaded_ForWhatNeedsIt()
    {
        static bool Gl(EChart chart) => chart.ToOutput().HtmlContent!.Contains("echarts-gl.min.js");

        Assert.False(Gl(EChart.Line(Sales).Title("Q3 3D sales on the surface")));
        Assert.False(Gl(EChart.FromJson("""{ "title": { "text": "3D globe surface" }, "series": [{ "type": "line", "data": [1] }] }""")));
        Assert.True(Gl(EChart.Surface((x, y) => x, resolution: 3)));
        Assert.True(Gl(EChart.FromJson("""{ "grid3D": {}, "series": [{ "type": "bar3D", "data": [] }] }""")));
        Assert.True(Gl(EChart.FromJs("option = { series: [{ type: 'scatter3D', data: [] }] };")));
    }

    [Fact]
    public void TheOutput_NamesTheLibraries_AndTheExportCarriesThem()
    {
        var chart = EChart.Surface((x, y) => Math.Sin(x) * Math.Cos(y));

        var output = chart.ToOutput().HtmlContent!;
        var page = chart.ToHtml();

        Assert.True(output.Length < 200_000, $"{output.Length} characters");
        Assert.Contains("data-fry-asset=\"echarts.min.js\"", output);
        Assert.Contains("data-fry-asset=\"echarts-gl.min.js\"", output);
        Assert.DoesNotContain("data-fry-asset", page);
        Assert.True(page.Length > output.Length + 1_000_000, "the export has the libraries in it");
    }

    [Fact]
    public void TheTheme_FollowsTheStudio_UnlessOneIsChosen()
    {
        var auto = EChart.Line(Sales).ToOutput().HtmlContent!;
        var dark = EChart.Line(Sales).Theme(EChartTheme.Dark).ToOutput().HtmlContent!;

        Assert.Contains("var theme = null || host.theme", auto);
        Assert.Contains("var theme = \"dark\" || host.theme", dark);

        var shown = HtmlAssets.ForDisplay(auto, dark: false, background: "#F3F3F3");
        Assert.Contains("<head><script>window.fryHost={theme:\"light\",background:\"#F3F3F3\"};</script>", shown);
        Assert.DoesNotContain("data-fry-asset", shown);
    }

    [Fact]
    public void AZoomSlider_SitsAboveALegendAtTheBottom()
    {
        var withLegend = EChart.Bar(("a", new[] { 1.0, 2 }), ("b", new[] { 3.0, 4 })).Zoom().ToOutput().HtmlContent!;
        var alone = EChart.Bar(Sales).Zoom().ToOutput().HtmlContent!;

        Assert.Contains("{\"type\":\"slider\",\"xAxisIndex\":0,\"bottom\":30}", withLegend);
        Assert.Contains("{\"type\":\"slider\",\"xAxisIndex\":0}", alone);
    }

    // ── Settings that reach any part of the option ────────────────────────────────────────────────────────────────

    [Fact]
    public void Set_FollowsThePath_IntoListsAndObjects()
    {
        var chart = EChart.Line(("a", Sales), ("b", Sales))
            .Set("series.label.show", true)
            .Set("xAxis[0].axisLabel.rotate", 45)
            .Set("series[1].name", "renamed");

        Assert.All(((JsonArray)chart.Option["series"]!).Select(s => s!["label"]!["show"]!.GetValue<bool>()), Assert.True);
        Assert.Equal(45, chart.Option["xAxis"]!["axisLabel"]!["rotate"]!.GetValue<int>());
        Assert.Equal("renamed", Text(Series(chart, 1)["name"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set("series[5].name", "x"));
        Assert.Throws<ArgumentException>(() => chart.Set("series..name", "x"));
    }

    [Fact]
    public void Merge_MergesObjects_AndReplacesTheRest()
    {
        var chart = EChart.Line(Sales).Title("Kept").Merge("""{ "title": { "subtext": "added" }, "animation": false }""");

        Assert.Equal("Kept", chart.ChartTitle);
        Assert.Equal("added", Text(chart.Option["title"]!["subtext"]));
        Assert.False(chart.Option["animation"]!.GetValue<bool>());
    }

    [Fact]
    public void ASettingAChartCantHave_SaysWhatItIsFor()
    {
        var pie = EChart.Pie(new Dictionary<string, double> { ["a"] = 1 });

        Assert.Contains("ColorMap", Assert.Throws<InvalidOperationException>(() => EChart.Line(Sales).ColorMap(ColorMapPreset.Viridis)).Message);
        Assert.Contains("no x axis", Assert.Throws<InvalidOperationException>(() => pie.XLabel("x")).Message);
        Assert.Contains("3D", Assert.Throws<InvalidOperationException>(() => pie.AutoRotate()).Message);
        Assert.Contains("axes", Assert.Throws<InvalidOperationException>(() => pie.Horizontal()).Message);
    }

    [Fact]
    public void Labels_NameTheSlicesOfAPie_AndThePlacesOfALine()
    {
        var pie = EChart.Pie(new[] { 1.0, 2.0 }).Labels(["left", "right"]);
        var line = EChart.Line(Sales).Labels(["Q1", "Q2", "Q3", "Q4"]);

        Assert.Equal(["left", "right"], ((JsonArray)Series(pie)["data"]!).Select(d => Text(d!["name"])));
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], ((JsonArray)line.Option["xAxis"]!["data"]!).Select(Text));
    }

    // ── Showing ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Show_EmitsAnHtmlOutput_AndTheExtensionsReturnTheirData()
    {
        var output = Shown(() => EChart.Line(Sales).Title("Shown"));
        Assert.Equal(CellOutputKind.Html, output.Kind);
        Assert.Contains("<title>Shown</title>", output.HtmlContent);

        var outputs = new List<RichCellOutput>();
        var records = new[] { new { Day = "Mon", Steps = 4 }, new { Day = "Tue", Steps = 7 } };
        using (InteractiveDisplayContext.EnterScope(outputs.Add))
        {
            Assert.Same(Sales, Sales.DumpEChart("Sales", EChartType.Bar));
            Assert.Equal(2, records.DumpEChart(r => r.Day, r => r.Steps, "Steps").Count());
            Display.EChart(Sales, "Display", EChartType.Area);
        }

        Assert.Equal(3, outputs.Count);
        Assert.Contains("<title>Display</title>", outputs[2].HtmlContent);
    }

    [Fact]
    public void TheWrongKindForSelectors_SaysWhichFactoryToUse()
    {
        var records = new[] { new { A = 1, B = 2 } };

        var error = Assert.Throws<ArgumentException>(() => records.ToEChart(r => r.A, r => r.B, EChartType.Heatmap));

        Assert.Contains("EChart.Heatmap", error.Message);
    }

    [Theory]
    [InlineData("echart_3d_surface_webgl")]
    [InlineData("echart_interactive_dashboard")]
    public async Task TheEChartTemplates_RunAndDrawAChart(string id)
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == id);
        var outputs = new List<RichCellOutput>();

        var result = await Task.Run(() => new NotebookExecutionKernel().ExecuteCellAsync(template.InitialCode, onRichOutput: outputs.Add));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains(outputs, o => o.Kind == CellOutputKind.Html && o.HtmlContent!.Contains("data-fry-asset=\"echarts.min.js\""));
    }

    [Fact]
    public async Task AChart_ReturnedAsACellsLastValue_IsShownOnce()
    {
        var kernel = new NotebookExecutionKernel();
        var returned = new List<RichCellOutput>();
        var shownThenReturned = new List<RichCellOutput>();

        await Task.Run(() => kernel.ExecuteCellAsync("EChart.Line(new[] { 1, 2, 3 }).Title(\"returned\")", onRichOutput: returned.Add));
        await Task.Run(() => kernel.ExecuteCellAsync("EChart.Line(new[] { 1, 2, 3 }).Show()", onRichOutput: shownThenReturned.Add));

        Assert.Contains("<title>returned</title>", Assert.Single(returned).HtmlContent);
        Assert.Single(shownThenReturned);
    }
}
