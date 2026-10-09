using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// An interactive chart drawn by Apache ECharts (echarts-gl for 3D), put together one setting at a time and then shown:
/// <code>
/// EChart.Line(sales, "Sales").Title("Sales").XLabel("Month").YLabel("USD").Show();
/// EChart.Bar(regions, r => r.Region, r => r.Revenue).Horizontal().Show();
/// EChart.Surface((x, y) => Math.Sin(x) * Math.Cos(y)).ColorMap(ColorMapPreset.Plasma).Show();
/// EChart.FromJson(json).Title("From the gallery").Show();
/// </code>
/// The data reads as it does for <see cref="Charts"/>. Every setting writes into <see cref="Option"/>, the ECharts option
/// itself (echarts.apache.org/en/option.html); <see cref="Set"/>, <see cref="Merge(string)"/> and
/// <see cref="Configure"/> reach the rest. Returned as a cell's last value, a chart shows without <see cref="Show"/>.
/// </summary>
public sealed partial class EChart
{
    // The resolver is given, not left to the default: a host that turns reflection-based JSON off (a trimmed app, a
    // file-based program) still turns an object option into a tree.
    private static readonly JsonSerializerOptions NodeOptions = new()
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsFuncConverter(), new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private EChartTheme _theme = EChartTheme.Auto;
    private bool _svg;
    private bool _horizontal;
    private bool _stacked;
    private bool _smooth;
    private bool _valueLabels;

    // A JavaScript option (FromJs): applied first, then Option on top of it.
    private string? _script;

    private EChart(EChartType? kind, JsonObject? option = null)
    {
        Kind = kind;
        Option = option ?? new JsonObject();
    }

    /// <summary>The ECharts option the chart is drawn from. Read it, or change it with <see cref="Set"/> and <see cref="Configure"/>.</summary>
    public JsonObject Option { get; }

    /// <summary>The kind its factory made; null for a chart from a raw option.</summary>
    public EChartType? Kind { get; }

    /// <summary>The title's text, if it has one.</summary>
    public string? ChartTitle => FirstObject("title")?["text"] is JsonValue text && text.TryGetValue<string>(out var value) ? value : null;

    /// <summary>True once the chart was shown (a chart returned as a cell's last value after <c>Show()</c> isn't shown twice).</summary>
    public bool IsShown { get; private set; }

    // ── Raw options ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A chart from an ECharts option written as JSON; settings still apply on top of it.</summary>
    /// <exception cref="ArgumentException">The text isn't a JSON object; the message says where.</exception>
    public static EChart FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The ECharts option isn't valid JSON: {ex.Message} (an option copied from an ECharts example is JavaScript: use EChart.FromJs).", nameof(json), ex);
        }

        return node is JsonObject option
            ? new EChart(null, option)
            : throw new ArgumentException("An ECharts option is a JSON object: { \"series\": [...] }.", nameof(json));
    }

    /// <summary>A chart from an ECharts option as a C# object (an anonymous object, a dictionary…); names are written in camelCase.</summary>
    public static EChart FromOption(object option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return ToNode(option) is JsonObject tree
            ? new EChart(null, tree)
            : throw new ArgumentException($"An ECharts option is an object with named parts, not a {option.GetType().Name}.", nameof(option));
    }

    /// <summary>
    /// A chart from an option written in JavaScript, as ECharts' own examples are: an object (<c>{ xAxis: { type: 'category' } }</c>)
    /// or code that sets <c>option</c> (<c>option = { ... };</c>). It runs in the page; the settings apply on top of it.
    /// </summary>
    public static EChart FromJs(string javascript)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(javascript);
        return new EChart(null) { _script = javascript };
    }

    // ── Settings every chart has ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The title above the chart.</summary>
    public EChart Title(string title)
    {
        var component = Component("title");
        component["text"] = title;
        component["left"] ??= "center";
        return this;
    }

    /// <summary>A line under the title.</summary>
    public EChart Subtitle(string subtitle)
    {
        var component = Component("title");
        component["subtext"] = subtitle;
        component["left"] ??= "center";
        return this;
    }

    /// <summary>Shows or hides the legend (it shows by itself for several series, and for the slices of a pie).</summary>
    public EChart Legend(bool show = true)
    {
        var legend = Component("legend");
        legend["show"] = show;
        if (show) legend["type"] ??= "scroll";
        if (show && !legend.ContainsKey("top") && !legend.ContainsKey("left") && !legend.ContainsKey("right")) legend["bottom"] ??= 0;
        return this;
    }

    /// <summary>Which side of the chart the legend is on, and shows it.</summary>
    public EChart LegendAt(LegendPosition position)
    {
        var legend = Component("legend");
        foreach (var key in new[] { "top", "bottom", "left", "right", "orient" }) legend.Remove(key);
        legend["show"] = true;
        legend["type"] ??= "scroll";
        switch (position)
        {
            case LegendPosition.Top: legend["top"] = FirstObject("title") == null ? 0 : 40; break;
            case LegendPosition.Bottom: legend["bottom"] = 0; break;
            case LegendPosition.Left: legend["left"] = 0; legend["top"] = "middle"; legend["orient"] = "vertical"; break;
            case LegendPosition.Right: legend["right"] = 0; legend["top"] = "middle"; legend["orient"] = "vertical"; break;
        }

        return this;
    }

    /// <summary>What hovering shows: the item under the pointer, every series at that place, or nothing.</summary>
    public EChart Tooltip(EChartTooltip tooltip)
    {
        var component = Component("tooltip");
        component["show"] = tooltip != EChartTooltip.None;
        if (tooltip != EChartTooltip.None) component["trigger"] = tooltip == EChartTooltip.Axis ? "axis" : "item";
        return this;
    }

    /// <summary>The buttons in the corner: save as an image, see the data, undo zoom.</summary>
    public EChart Toolbox(bool show = true)
    {
        var features = new JsonObject
        {
            ["saveAsImage"] = new JsonObject(),
            ["dataView"] = new JsonObject { ["readOnly"] = true },
            ["restore"] = new JsonObject()
        };
        if (IsCartesian) features["dataZoom"] = new JsonObject();
        var toolbox = Component("toolbox");
        toolbox["show"] = show;
        toolbox["right"] ??= 16;
        toolbox["feature"] = features;
        return this;
    }

    /// <summary>The series colours, in order (CSS colours such as <c>"#4ec9b0"</c>).</summary>
    public EChart Colors(params string[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        Option["color"] = new JsonArray(colors.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());
        return this;
    }

    /// <summary>The colours from low to high values, for a chart coloured by value (a heatmap, a surface, 3D bars).</summary>
    public EChart ColorMap(ColorMapPreset preset) => ColorMap(Stops(preset));

    /// <summary>The colours from low to high values (CSS colours), for a chart coloured by value.</summary>
    public EChart ColorMap(params string[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length < 2) throw new ArgumentException("A colour map runs between at least two colours.", nameof(colors));
        var visualMap = FirstObject("visualMap") ?? throw new InvalidOperationException(
            "ColorMap colours a chart by value: a heatmap, a surface or 3D bars. Colors(...) sets the colours of the series.");
        visualMap["inRange"] = new JsonObject { ["color"] = new JsonArray(colors.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()) };
        return this;
    }

    /// <summary>The chart's colours; <see cref="EChartTheme.Auto"/> (the default) follows the studio.</summary>
    public EChart Theme(EChartTheme theme)
    {
        _theme = theme;
        return this;
    }

    /// <summary>Draws with SVG instead of a canvas: sharper when zoomed or printed, slower with many points. Not for 3D.</summary>
    public EChart Svg(bool svg = true)
    {
        _svg = svg;
        return this;
    }

    // ── Reaching any part of the option ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets one value anywhere in the option by its path: <c>.Set("series[0].label.show", true)</c>,
    /// <c>.Set("xAxis.axisLabel.rotate", 45)</c>. A path that names a list without an index sets every item of it
    /// (<c>"series.label.show"</c>); missing parts are made. The value can be a number, text, a <see cref="JsFunc"/>,
    /// or any object (written in camelCase).
    /// </summary>
    public EChart Set(string path, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var segments = EChartPath.Parse(path);
        EChartPath.Set(Option, segments, 0, value, ToNode);
        return this;
    }

    /// <summary>Merges an option written as JSON into this one: objects are merged part by part, anything else replaces.</summary>
    public EChart Merge(string json)
    {
        var other = FromJson(json).Option;
        DeepMerge(Option, other);
        return this;
    }

    /// <summary>Merges an option given as a C# object (an anonymous object, a dictionary…) into this one.</summary>
    public EChart Merge(object option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (option is string json) return Merge(json);
        DeepMerge(Option, FromOption(option).Option);
        return this;
    }

    /// <summary>Changes the option in code: <c>.Configure(o =&gt; o["animation"] = false)</c>.</summary>
    public EChart Configure(Action<JsonObject> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Option);
        return this;
    }

    // ── Showing it ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Shows the chart under the cell (or in Results); it can be shown again after more settings.</summary>
    public EChart Show()
    {
        IsShown = true;
        InteractiveDisplayContext.Emit(ToOutput());
        return this;
    }

    /// <summary>Same as <see cref="Show"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EChart Dump() => Show();

    /// <summary>The chart as a page that works on its own, the libraries written in: for a file or a browser.</summary>
    public string ToHtml() => HtmlAssets.Inline(BuildPage());

    /// <summary>Writes <see cref="ToHtml"/> to <paramref name="path"/> and returns the full path.</summary>
    public string SaveHtml(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        File.WriteAllText(full, ToHtml());
        return full;
    }

    /// <summary>The output that shows the chart: a small page that names the libraries rather than carrying them.</summary>
    internal RichCellOutput ToOutput() => new() { Kind = CellOutputKind.Html, HtmlContent = BuildPage() };

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static string[] Stops(ColorMapPreset preset) =>
        Enumerable.Range(0, 9).Select(i => ColorMapService.GetHexColor(preset, i / 8.0)).ToArray();

    // A component of the option (title, legend…): the object, or the first one of a list; made when missing.
    private JsonObject Component(string key)
    {
        var existing = FirstObject(key);
        if (existing != null) return existing;
        var made = new JsonObject();
        Option[key] = made;
        return made;
    }

    private JsonObject? FirstObject(string key) => Option[key] switch
    {
        JsonObject one => one,
        JsonArray many => many.OfType<JsonObject>().FirstOrDefault(),
        _ => null
    };

    private JsonArray SeriesList()
    {
        if (Option["series"] is JsonArray list) return list;
        var made = new JsonArray();
        if (Option["series"] is JsonObject single)
        {
            Option.Remove("series");
            made.Add(single);
        }

        Option["series"] = made;
        return made;
    }

    private IEnumerable<JsonObject> AllSeries() => Option["series"] switch
    {
        JsonArray list => list.OfType<JsonObject>(),
        JsonObject one => [one],
        _ => []
    };

    private static string? TypeOf(JsonObject series) => series["type"] is JsonValue v && v.TryGetValue<string>(out var t) ? t : null;

    // A C# value as a node of the option.
    internal static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node.Parent == null ? node : node.DeepClone(),
        JsFunc function => JsonValue.Create(function.ToMarker()),
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        double d => Number(d),
        float f => Number(f),
        Enum e => JsonValue.Create(JsonNamingPolicy.CamelCase.ConvertName(e.ToString())),
        _ => JsonSerializer.SerializeToNode(value, value.GetType(), NodeOptions)
    };

    // A number of the data: NaN and ∞ are a gap.
    internal static JsonNode? Number(double? value) => value is { } v && double.IsFinite(v) ? JsonValue.Create(v) : null;

    private static void DeepMerge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToList())
        {
            source.Remove(key);
            if (value is JsonObject incoming && target[key] is JsonObject existing) DeepMerge(existing, incoming);
            else target[key] = value;
        }
    }

    private sealed class JsFuncConverter : System.Text.Json.Serialization.JsonConverter<JsFunc>
    {
        public override JsFunc Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("A JsFunc is written into a chart's option, not read from JSON.");

        public override void Write(Utf8JsonWriter writer, JsFunc value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToMarker());
    }
}
