using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// The page a chart is drawn in: the option written as script, the libraries named (HtmlAssets fills them in), the
// studio's theme picked up when the page is shown, and an error written on the page instead of a blank one.
public sealed partial class EChart
{
    private static readonly JsonSerializerOptions PageJson = new() { Encoder = JavaScriptEncoder.Default, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    // The series kinds and components only echarts-gl draws.
    private static readonly HashSet<string> GlSeries = new(StringComparer.Ordinal)
    {
        "surface", "bar3D", "scatter3D", "line3D", "lines3D", "map3D", "polygons3D", "scatterGL", "graphGL", "linesGL", "flowGL"
    };

    private static readonly string[] GlComponents = ["grid3D", "globe", "geo3D", "mapbox3D", "xAxis3D", "yAxis3D", "zAxis3D"];

    [GeneratedRegex("\"" + JsFunc.MarkerStart + "(?<code>[A-Za-z0-9_-]*)" + JsFunc.MarkerEnd + "\"")]
    private static partial Regex FunctionMarkerRegex();

    [GeneratedRegex(@"\b(grid3D|globe|geo3D|mapbox3D|xAxis3D)\b|type\s*:\s*['""](surface|bar3D|scatter3D|line3D|lines3D|map3D|polygons3D|scatterGL|graphGL|linesGL|flowGL)['""]")]
    private static partial Regex GlScriptRegex();

    [GeneratedRegex("</(script)", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptCloseRegex();

    /// <summary>True when the chart needs echarts-gl (a 3D or GL series or component).</summary>
    internal bool NeedsGl =>
        GlComponents.Any(Option.ContainsKey)
        || AllSeries().Any(s => TypeOf(s) is { } type && GlSeries.Contains(type))
        || (_script != null && GlScriptRegex().IsMatch(_script));

    internal string BuildPage()
    {
        var page = new StringBuilder(4096);
        var title = WebUtility.HtmlEncode(ChartTitle ?? "Interactive chart");
        var theme = _theme switch { EChartTheme.Light => "\"light\"", EChartTheme.Dark => "\"dark\"", _ => "null" };

        page.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"UTF-8\" />\n");
        page.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n");
        page.Append("<title>").Append(title).Append("</title>\n");
        page.Append("<style>html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif}");
        page.Append("#chart{position:absolute;inset:0;min-height:300px}");
        page.Append(".fry-error{position:absolute;inset:16px;margin:0;white-space:pre-wrap;color:#f48771;font:13px/1.5 Menlo,Consolas,monospace}</style>\n");
        page.Append(HtmlAssets.Reference("echarts.min.js")).Append('\n');
        if (NeedsGl) page.Append(HtmlAssets.Reference("echarts-gl.min.js")).Append('\n');
        page.Append("</head>\n<body>\n<div id=\"chart\"></div>\n<script>\n(function () {\n");
        page.Append("  var el = document.getElementById('chart');\n");
        page.Append("  function fail(message) { var p = document.createElement('pre'); p.className = 'fry-error'; p.textContent = 'The chart could not be drawn: ' + message; document.body.appendChild(p); }\n");
        page.Append("  if (!window.echarts) { fail('ECharts did not load.'); return; }\n");
        page.Append("  try {\n");
        page.Append("    var host = window.fryHost || {};\n");
        page.Append("    var theme = ").Append(theme).Append(" || host.theme || (window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');\n");
        page.Append("    document.documentElement.style.background = host.background || (theme === 'dark' ? '#1e1e1e' : '#ffffff');\n");
        page.Append("    var base = ").Append(ScriptOption()).Append(";\n");
        var (option, defaults) = Laid();
        page.Append("    var option = ").Append(ToScript(option)).Append(";\n");
        page.Append("    var defaults = ").Append(ToScript(defaults)).Append(";\n");
        page.Append("    for (var key in defaults) if (!(base && key in base) && !(key in option)) option[key] = defaults[key];\n");
        page.Append("    var chart = echarts.init(el, theme === 'dark' ? 'dark' : null, { renderer: '").Append(_svg ? "svg" : "canvas").Append("' });\n");
        page.Append("    if (base) chart.setOption(base);\n");
        page.Append("    chart.setOption(option);\n");
        page.Append("    window.__fryChart = chart;\n");
        page.Append("    if (window.ResizeObserver) new ResizeObserver(function () { chart.resize(); }).observe(el);\n");
        page.Append("    else window.addEventListener('resize', function () { chart.resize(); });\n");
        page.Append("  } catch (e) { fail(e && e.message ? e.message : String(e)); }\n");
        page.Append("})();\n</script>\n</body>\n</html>\n");
        return page.ToString();
    }

    // An option as script: JSON with each JsFunc written as its code, and nothing that would end the <script> early.
    private static string ToScript(JsonObject option)
    {
        var json = option.ToJsonString(PageJson);
        json = FunctionMarkerRegex().Replace(json, m => Guard(JsFunc.FromMarker(m.Groups["code"].Value)));
        return json;
    }

    // The JavaScript option of FromJs, run in its own function: an object, or code that sets `option`.
    private string ScriptOption()
    {
        if (_script == null) return "null";
        var code = Guard(_script.Trim());
        if (code.StartsWith('{')) return "(" + code + ")";
        return "(function () { var option; var myChart = { setOption: function (o) { option = o; } };\n" + code + "\n;return option; })()";
    }

    private static string Guard(string code) => ScriptCloseRegex().Replace(code, "<\\/$1").Replace("<!--", "<\\!--");

    // The option as drawn, and what it has unless it says otherwise: no background of its own, a legend where it helps,
    // room for the title, labels, legend and zoom slider, and the slider above a legend at the bottom.
    private (JsonObject Option, JsonObject Defaults) Laid()
    {
        var option = (JsonObject)Option.DeepClone();
        var defaults = new JsonObject { ["backgroundColor"] = "transparent" };
        var series = AllSeries().ToList();
        var radarItems = series.Where(s => TypeOf(s) == "radar").Sum(s => (s["data"] as JsonArray)?.Count ?? 0);
        var autoLegend = !option.ContainsKey("legend") && (series.Count > 1 || series.Any(s => TypeOf(s) is "pie" or "funnel") || radarItems > 1);
        if (autoLegend) defaults["legend"] = new JsonObject { ["type"] = "scroll", ["bottom"] = 0 };
        var legendBelow = autoLegend || (FirstObject("legend") is { } legend && legend["show"]?.GetValue<bool>() != false && legend.ContainsKey("bottom"));

        var slider = (option["dataZoom"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(z => z["type"]?.ToString() == "slider" && !z.ContainsKey("bottom"));
        if (slider != null && legendBelow && !_horizontal) slider["bottom"] = 30;

        if (IsCartesian)
        {
            var title = FirstObject("title");
            var top = title == null ? 32 : title.ContainsKey("subtext") ? 72 : 52;
            var bottom = 16 + (legendBelow ? 28 : 0) + (slider != null && !_horizontal ? 44 : 0) + (FirstObject("visualMap") != null ? 48 : 0);
            defaults["grid"] = new JsonObject { ["left"] = 16, ["right"] = slider != null && _horizontal ? 56 : 24, ["top"] = top, ["bottom"] = bottom, ["containLabel"] = true };
        }

        return (option, defaults);
    }
}
