using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// The object inspector a kernel in another language sends as <see cref="RichOutputMime.InspectorMime"/>, read back into
/// the studio's <see cref="ObjectInspectorNode"/> (the writer is <see cref="RichOutputMime.ToBundle"/>). It is how Java,
/// Python and JavaScript show an object the way C# does: <c>{"title": "People", "circular": false, "properties":
/// [{"name": "Name", "value": "Code", "isString": true}, {"name": "Another", "child": {…}}]}</c>.
/// </summary>
/// <remarks>
/// A row is a scalar (<c>value</c>, with <c>isNull</c> / <c>isString</c> telling how to colour it) or a nested object
/// (<c>child</c>). Which nodes start open follows the C# inspector: the top one, and the ones just below it unless they
/// close a cycle (<c>circular</c>). A node nested deeper than <see cref="MaxDepth"/> is shown by its title only.
/// </remarks>
public static class InspectorBundle
{
    /// <summary>Levels read; a deeper object keeps its title and loses its rows (the same limit the writer applies).</summary>
    public const int MaxDepth = 6;

    public static ObjectInspectorNode Read(JsonElement json) => ReadNode(json, 0);

    private static ObjectInspectorNode ReadNode(JsonElement json, int depth)
    {
        var title = Text(json, "title") is { Length: > 0 } named ? named : "object";
        var circular = Flag(json, "circular");
        var node = new ObjectInspectorNode(title, isExpanded: depth == 0 || (!circular && depth < 2), isExpandable: true)
        {
            IsCircularReference = circular
        };

        if (depth >= MaxDepth || !json.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Array)
        {
            return node;
        }

        foreach (var property in properties.EnumerateArray())
        {
            if (property.ValueKind != JsonValueKind.Object) continue;
            node.Properties.Add(ReadRow(property, depth + 1));
        }
        return node;
    }

    private static ObjectInspectorPropertyRow ReadRow(JsonElement property, int depth)
    {
        var row = new ObjectInspectorPropertyRow { Name = Text(property, "name") ?? string.Empty };

        if (property.TryGetProperty("child", out var child) && child.ValueKind == JsonValueKind.Object)
        {
            row.IsComplexChild = true;
            row.ChildNode = ReadNode(child, depth);
            return row;
        }

        if (Flag(property, "isNull"))
        {
            row.IsNull = true;
            row.SimpleValueText = "<null>";
            return row;
        }

        row.IsString = Flag(property, "isString");
        row.SimpleValueText = property.TryGetProperty("value", out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
            : string.Empty;
        return row;
    }

    private static string? Text(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
