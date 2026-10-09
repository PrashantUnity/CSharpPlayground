using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// How the data of the charts <see cref="Charts"/> has no kind for reads: grids, prices, trees, links and 3D categories,
/// by the same conventions (<see cref="DataReader"/>): numbers of any type, a missing number is a gap, tuples by position,
/// records by member name.
/// </summary>
internal static class EChartData
{
    private static readonly string[] NameMembers = ["name", "label", "title", "key", "id"];
    private static readonly string[] ChildMembers = ["children", "items", "nodes"];
    private static readonly string[] FromMembers = ["source", "from", "start", "parent"];
    private static readonly string[] ToMembers = ["target", "to", "end", "child"];
    private static readonly string[] AmountMembers = ["value", "weight", "amount", "count", "flow", "size"];
    private static readonly string[] DateMembers = ["date", "time", "day", "period", "x", "label"];

    /// <summary>True for a 2D array or a sequence of sequences of numbers: a grid.</summary>
    public static bool IsGrid(object data) =>
        data is Array { Rank: 2 }
        || data is IEnumerable rows and not string and not IDictionary
           && rows.Cast<object?>().FirstOrDefault() is IEnumerable first and not string
           && first.Cast<object?>().All(v => DataReader.TryNumber(v, out _));

    /// <summary>The rows of a grid of numbers (a missing or unreadable number is null); a short row stays short.</summary>
    public static List<List<double?>> Grid(object values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        if (values is Array { Rank: 2 } grid)
        {
            var rows = new List<List<double?>>(grid.GetLength(0));
            for (var r = 0; r < grid.GetLength(0); r++)
            {
                var row = new List<double?>(grid.GetLength(1));
                for (var c = 0; c < grid.GetLength(1); c++) row.Add(DataReader.TryNumber(grid.GetValue(r, c), out var v) ? v : null);
                rows.Add(row);
            }

            return rows;
        }

        if (values is IEnumerable sequence and not string and not IDictionary)
        {
            var rows = new List<List<double?>>();
            foreach (var row in sequence)
            {
                if (row is not IEnumerable cells || row is string) throw new ArgumentException($"A grid is rows of numbers; a row here is a {row?.GetType().Name ?? "null"}.", parameter);
                rows.Add(cells.Cast<object?>().Select(v => DataReader.TryNumber(v, out var n) ? n : null).ToList());
            }

            return rows;
        }

        throw new ArgumentException($"A grid is a double[,] or rows of numbers, not a {values.GetType().Name}.", parameter);
    }

    /// <summary>Open, close, low and high of each item, with its date when it has one.</summary>
    public static List<(string? Date, double Open, double Close, double Low, double High)> Candles(object data, string parameter)
    {
        if (data is not IEnumerable items || data is string or IDictionary)
        {
            throw new ArgumentException($"Candlesticks are a sequence of [open, close, low, high] items or records, not a {data?.GetType().Name ?? "null"}.", parameter);
        }

        var candles = new List<(string?, double, double, double, double)>();
        foreach (var item in items)
        {
            if (item == null) continue;
            var parts = Parts(item);
            if (parts != null && parts.Length >= 4)
            {
                var hasDate = parts.Length >= 5 || !DataReader.TryPresentNumber(parts[0], out _);
                var at = hasDate ? 1 : 0;
                candles.Add((hasDate ? DateText(parts[0]) : null, Num(parts[at]), Num(parts[at + 1]), Num(parts[at + 2]), Num(parts[at + 3])));
                continue;
            }

            if (DataReader.TryMember(item, ["open"], out _, out var open) && DataReader.TryMember(item, ["close"], out _, out var close)
                && DataReader.TryMember(item, ["low"], out _, out var low) && DataReader.TryMember(item, ["high"], out _, out var high))
            {
                var date = DataReader.TryMember(item, DateMembers, out _, out var d) ? DateText(d) : null;
                candles.Add((date, Num(open), Num(close), Num(low), Num(high)));
                continue;
            }

            throw new ArgumentException($"A candle is [open, close, low, high] or a record with Open, Close, Low and High; {item.GetType().Name} is neither.", parameter);
        }

        return candles;
    }

    /// <summary>A date as a label: 2026-10-09, with the time when there is one.</summary>
    public static string DateText(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateTimeOffset o => o.TimeOfDay == TimeSpan.Zero ? o.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : o.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// The nodes of a tree: a map (name → number, or name → what is inside), or records with a name, a value and
    /// children, nested as deep as they go.
    /// </summary>
    public static JsonArray Tree(object? data, string parameter)
    {
        var nodes = new JsonArray();
        switch (data)
        {
            case null:
                throw new ArgumentNullException(parameter);
            case IDictionary map:
                foreach (var (key, value) in DataReader.Entries(map)) nodes.Add(Node(key.ToString() ?? string.Empty, value, parameter));
                break;
            case IEnumerable items and not string:
                foreach (var item in items)
                {
                    if (item != null) nodes.Add(Record(item, parameter));
                }

                break;
            default:
                if (DataReader.IsScalar(data)) throw new ArgumentException($"A tree is a map or records with names and values, not a single {data.GetType().Name}.", parameter);
                nodes.Add(Record(data, parameter));
                break;
        }

        return nodes;
    }

    private static JsonObject Node(string name, object? value, string parameter)
    {
        var node = new JsonObject { ["name"] = name };
        if (DataReader.TryPresentNumber(value, out var number)) node["value"] = number;
        else if (value is IDictionary or IEnumerable and not string) node["children"] = Tree(value, parameter);
        else if (value != null && !DataReader.IsScalar(value)) return Record(value, parameter, name);
        return node;
    }

    private static JsonObject Record(object item, string parameter, string? fallbackName = null)
    {
        if (DataReader.TryItems(item, out var pair) && pair.Length >= 2) return Node(pair[0]?.ToString() ?? string.Empty, pair[1], parameter);
        var name = DataReader.TryMember(item, NameMembers, out _, out var n) ? n?.ToString() : fallbackName;
        var node = new JsonObject { ["name"] = name ?? item.ToString() };
        if (DataReader.TryMember(item, ChartSpecBuilder.ValueNames, out _, out var v) && DataReader.TryPresentNumber(v, out var value)) node["value"] = value;
        if (DataReader.TryMember(item, ChildMembers, out _, out var children) && children is IEnumerable and not string) node["children"] = Tree(children, parameter);
        return node;
    }

    /// <summary>
    /// Links between named nodes: (from, to) or (from, to, amount) tuples or arrays, records with source/from, target/to
    /// and value members, or an adjacency map (node → the nodes it links to, or node → { node: amount }).
    /// </summary>
    public static List<(string From, string To, double? Value)> Links(object data, string parameter)
    {
        var links = new List<(string, string, double?)>();
        switch (data)
        {
            case null:
                throw new ArgumentNullException(parameter);
            case IDictionary adjacency:
                foreach (var (key, value) in DataReader.Entries(adjacency))
                {
                    var from = key.ToString() ?? string.Empty;
                    switch (value)
                    {
                        case IDictionary weighted:
                            foreach (var (to, amount) in DataReader.Entries(weighted)) links.Add((from, to.ToString() ?? string.Empty, DataReader.TryNumber(amount, out var a) ? a : null));
                            break;
                        case IEnumerable targets and not string:
                            foreach (var to in targets) if (to != null) links.Add((from, to.ToString() ?? string.Empty, null));
                            break;
                        case null:
                            break;
                        default:
                            links.Add((from, value.ToString() ?? string.Empty, null));
                            break;
                    }
                }

                return links;
            case IEnumerable items and not string:
                foreach (var item in items)
                {
                    if (item == null) continue;
                    var parts = Parts(item);
                    if (parts is { Length: >= 2 })
                    {
                        links.Add((Text(parts[0]), Text(parts[1]), parts.Length >= 3 && DataReader.TryNumber(parts[2], out var w) ? w : null));
                        continue;
                    }

                    if (DataReader.TryMember(item, FromMembers, out _, out var source) && DataReader.TryMember(item, ToMembers, out _, out var target))
                    {
                        var amount = DataReader.TryMember(item, AmountMembers, out _, out var m) && DataReader.TryNumber(m, out var x) ? x : null;
                        links.Add((Text(source), Text(target), amount));
                        continue;
                    }

                    throw new ArgumentException($"A link is (from, to, amount) or a record with Source and Target; {item.GetType().Name} is neither.", parameter);
                }

                return links;
            default:
                throw new ArgumentException($"Links are a sequence of (from, to, amount) or an adjacency map, not a {data.GetType().Name}.", parameter);
        }
    }

    /// <summary>Every node the links name, in the order they first appear; an adjacency map's own nodes first, linked or not.</summary>
    public static List<string> NodeNames(IEnumerable<(string From, string To, double? Value)> links, object? data = null)
    {
        var seen = new HashSet<string>();
        var names = new List<string>();
        if (data is IDictionary adjacency)
        {
            foreach (var (key, _) in DataReader.Entries(adjacency))
            {
                var name = key.ToString() ?? string.Empty;
                if (seen.Add(name)) names.Add(name);
            }
        }

        foreach (var (from, to, _) in links)
        {
            if (seen.Add(from)) names.Add(from);
            if (seen.Add(to)) names.Add(to);
        }

        return names;
    }

    /// <summary>(x, y, height) items: tuples, arrays, or records with x, y and z members (or their first three).</summary>
    public static List<(object? X, object? Y, double? Z)> Triplets(object data, string parameter)
    {
        if (data is not IEnumerable items || data is string or IDictionary)
        {
            throw new ArgumentException($"3D bars are a grid of heights or (x, y, height) items, not a {data?.GetType().Name ?? "null"}.", parameter);
        }

        var triplets = new List<(object?, object?, double?)>();
        foreach (var item in items)
        {
            if (item == null) continue;
            var parts = Parts(item);
            if (parts is not { Length: >= 3 })
            {
                if (DataReader.TryMember(item, ["x"], out _, out var x) && DataReader.TryMember(item, ["y"], out _, out var y) && DataReader.TryMember(item, ["z", "value", "height"], out _, out var z))
                {
                    parts = [x, y, z];
                }
                else
                {
                    var members = DataReader.Members(item).Take(3).Select(m => m.Value).ToArray();
                    parts = members.Length == 3 ? members : throw new ArgumentException($"A 3D bar is (x, y, height); {item.GetType().Name} has no three values.", parameter);
                }
            }

            triplets.Add((parts[0], parts[1], DataReader.TryNumber(parts[2], out var h) ? h : null));
        }

        return triplets;
    }

    // A tuple's items, or an array's or list's (not a record's).
    private static object?[]? Parts(object item)
    {
        if (DataReader.TryItems(item, out var items)) return items;
        return item is IEnumerable sequence and not string and not IDictionary ? sequence.Cast<object?>().Take(6).ToArray() : null;
    }

    private static double Num(object? value) => DataReader.TryPresentNumber(value, out var n) ? n : double.NaN;

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// An axis of 3D bars: categories in order when there are labels or any place is text (a number then picks a
    /// category by position), numbers along a value axis otherwise.
    /// </summary>
    public sealed class Categories
    {
        private readonly List<string>? _names;
        private readonly Dictionary<string, int> _index = new();

        public Categories(IEnumerable<string>? labels, IEnumerable<object?> places)
        {
            var given = labels?.ToList();
            var all = places.ToList();
            if (given == null && all.All(p => p == null || (DataReader.TryPresentNumber(p, out _) && p is not string))) return;

            _names = given ?? [];
            for (var i = 0; i < _names.Count; i++) _index.TryAdd(_names[i], i);
            if (given != null) return;
            foreach (var place in all)
            {
                if (place != null && !(DataReader.TryPresentNumber(place, out _) && place is not string)) Find(Text(place));
            }
        }

        public int Count => _names?.Count ?? 0;

        public JsonNode? Place(object? place)
        {
            if (_names == null) return DataReader.TryPresentNumber(place, out var value) ? value : null;
            if (place != null && place is not string && DataReader.TryPresentNumber(place, out var position)) return (int)position;
            return Find(Text(place));
        }

        public JsonObject Axis(string name) => _names == null
            ? new JsonObject { ["type"] = "value", ["name"] = name }
            : new JsonObject { ["type"] = "category", ["name"] = name, ["data"] = new JsonArray(_names.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray()) };

        private int Find(string name)
        {
            if (_index.TryGetValue(name, out var at)) return at;
            _names!.Add(name);
            _index[name] = _names.Count - 1;
            return _names.Count - 1;
        }
    }
}
