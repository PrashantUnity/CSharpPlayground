using System.Diagnostics;
using System.Text.Json;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Every 2D EChart drawn by the real ECharts the studio ships, run by Node in its server-side mode: the page's own script
/// runs, the option is accepted, and the chart comes out as SVG with something in it. (3D needs WebGL, which Node hasn't.)
/// </summary>
[Collection(RealJavaScriptCollection.Name)]
public class EChartRealLibraryTests
{
    // Loads ECharts with no DOM (so it draws server-side), then runs each page's chart script with the little DOM it touches.
    private const string Harness = """
        const fs = require('fs'), vm = require('vm');
        const [library, ...pages] = process.argv.slice(2);
        const code = fs.readFileSync(library, 'utf8');
        for (const page of pages) {
          const html = fs.readFileSync(page, 'utf8');
          const script = html.slice(html.lastIndexOf('<script>\n(function () {') + 8, html.lastIndexOf('</script>'));
          const errors = [];
          const document = { getElementById: () => ({ style: {} }), createElement: () => ({}), body: { appendChild: e => errors.push(e.textContent) }, documentElement: { style: {} } };
          const context = vm.createContext({ console, setTimeout, clearTimeout });
          const result = { page, ok: false };
          try {
            vm.runInContext(code, context);
            const echarts = context.echarts, init = echarts.init;
            let chart;
            echarts.init = (el, theme) => (chart = init(null, theme, { renderer: 'svg', ssr: true, width: 800, height: 500 }));
            vm.runInContext('(function (document, window, echarts) {' + script + '\n})', context)(document, { echarts, addEventListener() {} }, echarts);
            if (errors.length) throw new Error(errors.join('; '));
            const option = chart.getOption();
            const svg = chart.renderToSVGString();
            Object.assign(result, { ok: true, series: option.series.map(s => s.type), shapes: (svg.match(/<(path|rect|circle|text)/g) || []).length,
              formatter: typeof (option.tooltip && option.tooltip[0] && option.tooltip[0].formatter) });
          } catch (e) { result.error = String(e && e.message || e); }
          console.log(JSON.stringify(result));
        }
        process.exit(0);
        """;

    [JavaScriptFact]
    public async Task Every2DChart_IsAcceptedAndDrawnByTheRealECharts()
    {
        var charts = EChartTests.Factories().Select(row => ((string)row[0], ((Func<EChart>)row[1])(), (string)row[2]))
            .Where(c => !c.Item2.NeedsGl)
            .ToList();
        charts.Add(("with a function", EChart.Line(new[] { 1.0, 2, 3 }).Set("tooltip.formatter", JsFunc.From("p => p.name")).Zoom().Toolbox().ValueLabels(), "line"));
        charts.Add(("from JavaScript", EChart.FromJs("option = { xAxis: { type: 'category', data: ['a', 'b'] }, yAxis: {}, series: [{ type: 'bar', data: [3, 4] }] };").Title("JS"), "bar"));
        charts.Add(("horizontal, stacked", EChart.Bar(("a", new[] { 1.0, 2 }), ("b", new[] { 3.0, 4 })).Horizontal().Stacked().Legend(), "bar"));

        var results = await DrawAsync(charts.Select(c => c.Item2.ToOutput().HtmlContent!).ToList());

        for (var i = 0; i < charts.Count; i++)
        {
            var (name, _, type) = charts[i];
            Assert.True(results[i].GetProperty("ok").GetBoolean(), $"{name}: {results[i]}");
            Assert.Equal(type, results[i].GetProperty("series")[0].GetString());
            Assert.True(results[i].GetProperty("shapes").GetInt32() > 3, $"{name} drew next to nothing: {results[i]}");
        }

        Assert.Equal("function", results[charts.FindIndex(c => c.Item1 == "with a function")].GetProperty("formatter").GetString());
    }

    [JavaScriptFact]
    public async Task EveryEChartDocsSample_DrawsWithTheRealECharts()
    {
        var category = DocumentationService.Instance.Categories.Single(c => c.Id == "echart_visual_computing");
        var pages = new List<(string Sample, string Html)>();
        foreach (var snippet in category.Articles.SelectMany(a => a.CodeSnippets))
        {
            var outputs = new List<RichCellOutput>();
            var result = await Task.Run(() => new NotebookExecutionKernel().ExecuteCellAsync(snippet.Code, onRichOutput: outputs.Add));
            Assert.True(result.Success, $"{snippet.Id}: {result.ErrorMessage}");
            var charts = outputs.Where(o => o.Kind == CellOutputKind.Html).Select(o => o.HtmlContent!).ToList();
            Assert.True(charts.Count > 0, $"{snippet.Id} drew nothing");
            pages.AddRange(charts.Where(html => !html.Contains("echarts-gl.min.js")).Select(html => (snippet.Id, html)));
        }

        var drawn = await DrawAsync(pages.Select(p => p.Html).ToList());

        for (var i = 0; i < pages.Count; i++)
        {
            Assert.True(drawn[i].GetProperty("ok").GetBoolean(), $"{pages[i].Sample}: {drawn[i]}");
            Assert.True(drawn[i].GetProperty("shapes").GetInt32() > 3, $"{pages[i].Sample} drew next to nothing: {drawn[i]}");
        }
    }

    // Each page drawn by Node with the ECharts the plugin ships, in order.
    private static async Task<List<JsonElement>> DrawAsync(IReadOnlyList<string> pages)
    {
        var folder = Directory.CreateTempSubdirectory("echart-real-").FullName;
        try
        {
            var library = Path.Combine(folder, "echarts.min.js");
            await using (var resource = typeof(EChart).Assembly.GetManifestResourceStream("ECharts.echarts.min.js")!)
            await using (var file = File.Create(library))
            {
                await resource.CopyToAsync(file);
            }

            var harness = Path.Combine(folder, "draw.js");
            await File.WriteAllTextAsync(harness, Harness);
            var files = new List<string>();
            foreach (var html in pages)
            {
                var page = Path.Combine(folder, $"{files.Count}.html");
                await File.WriteAllTextAsync(page, html);
                files.Add(page);
            }

            var start = new ProcessStartInfo(TestJavaScript.Require().ExecutablePath) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { harness, library }.Concat(files)) start.ArgumentList.Add(argument);
            using var node = Process.Start(start)!;
            var output = node.StandardOutput.ReadToEndAsync();
            var errors = node.StandardError.ReadToEndAsync();
            await node.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));

            var results = (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line).RootElement).ToList();
            Assert.True(results.Count == pages.Count, $"Node drew {results.Count} of {pages.Count} pages: {await errors}");
            return results;
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
    }
}
