using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>The things a visual tells its program about (the <c>events</c> of a <c>subscribe</c> message).</summary>
public static class VisualEventKinds
{
    /// <summary>A click on an element: a chart's value, a node, an edge, a cell, an item.</summary>
    public const string Click = "click";

    /// <summary>The elements picked with Ctrl- or Shift-click changed.</summary>
    public const string Select = "select";

    /// <summary>A step visualizer moved to another step.</summary>
    public const string Step = "step";

    public static IReadOnlyList<string> All { get; } = [Click, Select, Step];

    public static bool IsKnown(string? kind) => kind is Click or Select or Step;
}

/// <summary>
/// What was clicked or picked, named as the spec names it: a chart's value by series and index (with its x, y, label
/// and id), a node or an item, an edge by its ends, a cell by row and column.
/// </summary>
public sealed record VisualEventTarget
{
    public int? Series { get; init; }
    public int? Index { get; init; }
    public string? Id { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public string? Label { get; init; }
    public string? Node { get; init; }
    public (string From, string To)? Edge { get; init; }
    public (int Row, int Col)? Cell { get; init; }
    public int? Item { get; init; }

    internal void Write(Utf8JsonWriter json)
    {
        json.WriteStartObject();
        if (Series is { } series) json.WriteNumber("series", series);
        if (Index is { } index) json.WriteNumber("index", index);
        if (Id != null) json.WriteString("id", Id);
        if (X is { } x && double.IsFinite(x)) json.WriteNumber("x", x);
        if (Y is { } y && double.IsFinite(y)) json.WriteNumber("y", y);
        if (Z is { } z && double.IsFinite(z)) json.WriteNumber("z", z);
        if (Label != null) json.WriteString("label", Label);
        if (Node != null) json.WriteString("node", Node);
        if (Edge is { } edge)
        {
            json.WriteStartArray("edge");
            json.WriteStringValue(edge.From);
            json.WriteStringValue(edge.To);
            json.WriteEndArray();
        }

        if (Cell is { } cell)
        {
            json.WriteStartArray("cell");
            json.WriteNumberValue(cell.Row);
            json.WriteNumberValue(cell.Col);
            json.WriteEndArray();
        }

        if (Item is { } item) json.WriteNumber("item", item);
        json.WriteEndObject();
    }
}

/// <summary>
/// Something that happened to a shown visual: <c>click</c> (with its <see cref="Target"/>), <c>select</c> (with the
/// <see cref="Targets"/> picked) or <c>step</c> (with the step's <see cref="Index"/>). The same event reaches a program in
/// any language, as the <c>event</c> of an <c>event</c> message.
/// </summary>
public sealed record VisualEvent(string Kind)
{
    public VisualEventTarget? Target { get; init; }
    public IReadOnlyList<VisualEventTarget>? Targets { get; init; }
    public int? Index { get; init; }

    /// <summary>The keys held: ctrl, shift, alt, meta.</summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    public static VisualEvent Click(VisualEventTarget target, IReadOnlyList<string>? modifiers = null) =>
        new(VisualEventKinds.Click) { Target = target, Modifiers = modifiers ?? [] };

    public static VisualEvent Select(IReadOnlyList<VisualEventTarget> targets) => new(VisualEventKinds.Select) { Targets = targets };

    public static VisualEvent Step(int index) => new(VisualEventKinds.Step) { Index = index };

    /// <summary>The event as a program reads it: <c>{"event": "click", "target": {...}, "modifiers": [...]}</c>.</summary>
    public void Write(Utf8JsonWriter json)
    {
        json.WriteStartObject();
        json.WriteString("event", Kind);
        if (Target != null)
        {
            json.WritePropertyName("target");
            Target.Write(json);
        }

        if (Targets != null)
        {
            json.WriteStartArray("targets");
            foreach (var target in Targets) target.Write(json);
            json.WriteEndArray();
        }

        if (Index is { } index) json.WriteNumber("index", index);
        if (Modifiers.Count > 0)
        {
            json.WriteStartArray("modifiers");
            foreach (var modifier in Modifiers) json.WriteStringValue(modifier);
            json.WriteEndArray();
        }

        json.WriteEndObject();
    }

    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer)) Write(json);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// The event as the message a program reads, one line of UTF-8 JSON ending in a newline, over a kernel's input or the
    /// event socket alike: <c>{"type":"event","id":…,"display_id":…,"event":{…}}</c>.
    /// </summary>
    public byte[] ToMessageLine(string displayId, string id)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("type", "event");
            json.WriteString("id", id);
            json.WriteString("display_id", displayId);
            json.WritePropertyName("event");
            Write(json);
            json.WriteEndObject();
        }

        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }
}

/// <summary>Where a visual's events go: a program's kernel or connection, or a C# callback.</summary>
public interface IVisualEventSink
{
    void Deliver(string displayId, VisualEvent visualEvent);
}
