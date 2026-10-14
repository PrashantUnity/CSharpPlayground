using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>A path into an ECharts option, <c>series[0].label.show</c>, and setting the value it names.</summary>
internal static partial class EChartPath
{
    [GeneratedRegex(@"^(?<name>[A-Za-z_$][A-Za-z0-9_$]*)(\[(?<index>\d+)\])?$")]
    private static partial Regex SegmentRegex();

    public static List<(string Name, int? Index)> Parse(string path)
    {
        var segments = new List<(string, int?)>();
        foreach (var part in path.Split('.'))
        {
            var match = SegmentRegex().Match(part.Trim());
            if (!match.Success)
            {
                throw new ArgumentException($"'{part}' in '{path}' isn't a part of an option: write names and indexes, such as series[0].label.show.", nameof(path));
            }

            segments.Add((match.Groups["name"].Value, match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : null));
        }

        return segments;
    }

    public static void Set(JsonObject target, List<(string Name, int? Index)> path, int at, object? value, Func<object?, JsonNode?> toNode)
    {
        var (name, index) = path[at];
        var last = at == path.Count - 1;
        var current = target[name];

        if (index is not { } i)
        {
            if (last)
            {
                target[name] = toNode(value);
                return;
            }

            // A list without an index: the rest of the path is set on every item of it.
            if (current is JsonArray list)
            {
                var items = list.OfType<JsonObject>().ToList();
                if (items.Count == 0) throw new InvalidOperationException($"'{name}' has no items to set {string.Join('.', path.Skip(at + 1).Select(p => p.Name))} on.");
                foreach (var item in items) Set(item, path, at + 1, value, toNode);
                return;
            }

            if (current is not JsonObject child)
            {
                child = new JsonObject();
                target[name] = child;
            }

            Set(child, path, at + 1, value, toNode);
            return;
        }

        // An object where the path says [0] is that first item (xAxis is often one axis, not a list of them).
        if (current is JsonObject single && i == 0)
        {
            if (last) target[name] = toNode(value);
            else Set(single, path, at + 1, value, toNode);
            return;
        }

        if (current is not JsonArray array)
        {
            array = new JsonArray();
            target[name] = array;
        }

        if (i > array.Count) throw new ArgumentOutOfRangeException(nameof(path), $"'{name}' has {array.Count} item(s), so [{i}] is past its end.");
        if (i == array.Count) array.Add(last ? null : new JsonObject());
        if (last)
        {
            array[i] = toNode(value);
            return;
        }

        if (array[i] is not JsonObject element)
        {
            element = new JsonObject();
            array[i] = element;
        }

        Set(element, path, at + 1, value, toNode);
    }
}
