using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>The kinds of chart <see cref="EChart"/> draws: each is one of its factories (<c>EChart.Line</c>, <c>EChart.Bar3D</c>…).</summary>
public enum EChartType
{
    Line,
    Area,
    Bar,
    Scatter,
    Pie,
    Donut,
    Funnel,
    Radar,
    Heatmap,
    Candlestick,
    Gauge,
    Treemap,
    Sunburst,
    Sankey,
    Graph,

    /// <summary>A 3D surface (WebGL).</summary>
    Surface,

    /// <summary>3D bars standing on a floor (WebGL).</summary>
    Bar3D,

    /// <summary>Points in 3D (WebGL).</summary>
    Scatter3D
}

/// <summary>The colours an <see cref="EChart"/> is drawn in.</summary>
public enum EChartTheme
{
    /// <summary>The studio's theme (the system's, in a browser).</summary>
    Auto,
    Light,
    Dark
}

/// <summary>What hovering shows.</summary>
public enum EChartTooltip
{
    /// <summary>The value under the pointer.</summary>
    Item,

    /// <summary>Every series' value at the place under the pointer.</summary>
    Axis,

    /// <summary>Nothing.</summary>
    None
}

/// <summary>How a 3D surface or bar is lit.</summary>
public enum SurfaceShading
{
    /// <summary>Flat colour, no light: the cheapest.</summary>
    Color,

    /// <summary>Lit from one side, so the shape shows.</summary>
    Lambert,

    /// <summary>Physically based: shine and soft shadows.</summary>
    Realistic
}

/// <summary>
/// A JavaScript function placed in a chart's option where ECharts takes one (a formatter, a colour callback…):
/// <c>.Set("tooltip.formatter", JsFunc.From("p => p.name + ': ' + p.value"))</c>. It is written into the page as code,
/// not as text.
/// </summary>
public sealed class JsFunc
{
    internal const string MarkerStart = "__fry_js__:";
    internal const string MarkerEnd = ":__fry_js__";

    public JsFunc(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>The function's JavaScript source.</summary>
    public string Code { get; }

    public static JsFunc From(string code) => new(code);

    // In the option tree a function is a marked string, so it survives being copied, merged and serialized like any other
    // value; the page writer turns the marker back into code.
    internal string ToMarker() => MarkerStart + Convert.ToBase64String(Encoding.UTF8.GetBytes(Code)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + MarkerEnd;

    internal static string FromMarker(string encoded)
    {
        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    public override string ToString() => Code;
}
