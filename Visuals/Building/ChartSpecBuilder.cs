using System.Collections;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>
/// A chart's spec from C# data, read by the conventions every language follows (docs/visual-protocol.md):
/// <list type="bullet">
/// <item>numbers, each at its index (a missing one, null, NaN or ∞, is a gap);</item>
/// <item>[x, y] pairs, as tuples or two-number arrays;</item>
/// <item>a label → number map, or pairs of a label and a number;</item>
/// <item>a name → sequence map, a series each;</item>
/// <item>records, whose value is their first member named in <see cref="ValueNames"/> (else their first number) and whose
/// place is their first member named in <see cref="PlaceNames"/>: a number is its x, anything else its label.</item>
/// </list>
/// </summary>
public static class ChartSpecBuilder
{
    /// <summary>The members a record's value is read from, in order.</summary>
    public static IReadOnlyList<string> ValueNames { get; } = ["y", "value", "amount", "count", "total", "score", "revenue", "sales", "price", "cost"];

    /// <summary>The members a record's place is read from, in order.</summary>
    public static IReadOnlyList<string> PlaceNames { get; } = ["x", "key", "label", "name", "title", "category", "region", "country", "item"];

    /// <summary>A chart of <paramref name="data"/>, drawn as <paramref name="kind"/> (a line, or what a given spec or model says).</summary>
    /// <exception cref="ArgumentException">The data isn't a sequence or a map (a chart of one number says nothing).</exception>
    public static ChartSpec From(object? data, ChartType? kind = null)
    {
        switch (data)
        {
            case ChartSpec given:
                if (kind is { } asked) given.Kind = asked;
                return given;
            case ChartOptions model:
                var converted = ChartOptionsConverter.ToSpec(model);
                if (kind is { } modelKind) converted.Kind = modelKind;
                return converted;
            case ChartSeries one:
                return new ChartSpec { Kind = kind ?? ChartType.Line, Series = { ChartOptionsConverter.ToSpec(one) } };
            case IEnumerable<ChartSeries> many:
                return new ChartSpec { Kind = kind ?? ChartType.Line, Series = many.Select(ChartOptionsConverter.ToSpec).ToList() };
        }

        if (kind == ChartType.Bubble) return Bubbles(data);

        var spec = new ChartSpec { Kind = kind ?? ChartType.Line };
        switch (data)
        {
            case null:
                break;
            case System.Runtime.CompilerServices.ITuple { Length: 2 } named when named[0] is string name && DataReader.IsSequence(named[1]):
                // ("Sales", values): one series with a name.
                spec.Series.Add(Series(((IEnumerable)named[1]!).Cast<object?>().Select(Read), name));
                break;
            case IDictionary map:
                spec.Series.AddRange(FromMap(map));
                break;
            case IEnumerable sequence when data is not string:
                spec.Series.Add(Series(sequence.Cast<object?>().Select(Read)));
                break;
            default:
                throw new ArgumentException($"A chart shows a sequence of values or a map of them, not a single {data.GetType().Name}.", nameof(data));
        }

        return spec;
    }

    /// <summary>A chart of records, each drawn at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public static ChartSpec From<T>(IEnumerable<T> records, Func<T, object?>? x, Func<T, object?> y, ChartType? kind = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(y);
        var values = records.Select(r =>
        {
            DataReader.TryNumber(y(r), out var value);
            var (place, label) = Place(x == null ? null : x(r));
            return (place, label, value);
        });
        return new ChartSpec { Kind = kind ?? ChartType.Line, Series = { Series(values) } };
    }

    /// <summary>A chart with a series for each name: <c>("Sales", sales), ("Costs", costs)</c>.</summary>
    public static ChartSpec FromSeries(ChartType kind, IEnumerable<(string Name, IEnumerable Values)> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var spec = new ChartSpec { Kind = kind };
        foreach (var (name, values) in series)
        {
            ArgumentNullException.ThrowIfNull(values);
            spec.Series.Add(Series(values.Cast<object?>().Select(Read), name));
        }

        return spec;
    }

    /// <summary>A histogram of the samples: the studio counts them into <paramref name="bins"/> bars (10 when left out).</summary>
    public static ChartSpec Histogram(object? samples, int? bins = null)
    {
        var spec = new ChartSpec { Kind = ChartType.Histogram, Bins = bins };
        switch (samples)
        {
            case null:
                break;
            case IDictionary map when map.Values.Cast<object?>().All(DataReader.IsSequence):
                foreach (var (key, value) in DataReader.Entries(map)) spec.Series.Add(Samples((IEnumerable)value!, key.ToString()));
                break;
            case IEnumerable sequence when samples is not string:
                spec.Series.Add(Samples(sequence, null));
                break;
            default:
                throw new ArgumentException($"A histogram counts a sequence of numbers, not a single {samples.GetType().Name}.", nameof(samples));
        }

        return spec;
    }

    // A map of labels to numbers is one series; a map of names to sequences is a series each.
    private static IEnumerable<ChartSeriesSpec> FromMap(IDictionary map)
    {
        var entries = DataReader.Entries(map).ToList();
        if (entries.Count > 0 && entries.All(e => DataReader.IsSequence(e.Value)))
        {
            return entries.Select(e => Series(((IEnumerable)e.Value!).Cast<object?>().Select(Read), e.Key.ToString())).ToList();
        }

        return [Series(entries.Select(e =>
        {
            DataReader.TryNumber(e.Value, out var value);
            return ((double?)null, e.Key.ToString(), value);
        }))];
    }

    // One item: where it goes (an x, or a label) and its value; an item with no value is a gap.
    private static (double? X, string? Label, double? Y) Read(object? item)
    {
        if (DataReader.TryNumber(item, out var number)) return (null, null, number);
        if (item == null) return (null, null, null);

        // The usual pairs, read without boxing their members (a big series is mostly these).
        switch (item)
        {
            case ValueTuple<double, double> dd: return (DataReader.Finite(dd.Item1), null, DataReader.Finite(dd.Item2));
            case ValueTuple<int, double> id: return (id.Item1, null, DataReader.Finite(id.Item2));
            case ValueTuple<string, double> sd: return (null, sd.Item1, DataReader.Finite(sd.Item2));
        }

        if (DataReader.TryItems(item, out var items) && items.Length >= 2) return Pair(items[0], items[1]);
        if (item is IEnumerable sequence and not string)
        {
            var two = sequence.Cast<object?>().Take(3).ToArray();
            return two.Length == 2 ? Pair(two[0], two[1]) : (null, null, null);
        }

        // A record: its value by name, else its first number; its place by name.
        DataReader.TryMember(item, PlaceNames, out var placeName, out var placeValue);
        double? y = null;
        if (DataReader.TryMember(item, ValueNames, out _, out var named) && DataReader.TryNumber(named, out var byName))
        {
            y = byName;
        }
        else
        {
            foreach (var (name, value) in DataReader.Members(item))
            {
                if (name == placeName || !DataReader.TryPresentNumber(value, out var first)) continue;
                y = first;
                break;
            }
        }

        var (x, label) = placeName.Length > 0 ? Place(placeValue) : (null, null);
        return (x, label, y);
    }

    // [x, y], or [label, y].
    private static (double? X, string? Label, double? Y) Pair(object? first, object? second)
    {
        DataReader.TryNumber(second, out var y);
        var (x, label) = Place(first);
        return (x, label, y);
    }

    // A place is an x when it is a number, a label otherwise.
    private static (double? X, string? Label) Place(object? value) =>
        value == null ? (null, null)
        : DataReader.TryPresentNumber(value, out var x) && value is not string ? (x, null)
        : (null, value.ToString());

    // The columns: y always; x and labels when any item gave one.
    private static ChartSeriesSpec Series(IEnumerable<(double? X, string? Label, double? Y)> items, string? name = null)
    {
        var series = new ChartSeriesSpec { Name = name };
        var x = new List<double?>();
        var labels = new List<string?>();
        foreach (var (px, label, y) in items)
        {
            series.Y.Add(y);
            x.Add(px);
            labels.Add(label);
        }

        if (x.Any(v => v != null)) series.X = x;
        if (labels.Any(l => l != null)) series.Labels = labels;
        return series;
    }

    private static ChartSeriesSpec Samples(IEnumerable samples, string? name)
    {
        var values = new List<double?>();
        foreach (var sample in samples)
        {
            if (DataReader.TryNumber(sample, out var value)) values.Add(value);
        }

        return new ChartSeriesSpec { Name = name, Values = values };
    }

    /// <summary>The members a bubble's radius is read from, in order.</summary>
    public static IReadOnlyList<string> SizeNames { get; } = ["size", "r", "radius", "z", "weight"];

    // A bubble chart of (x, y, size) items: tuples, three-number arrays, or records with x, y and a size.
    private static ChartSpec Bubbles(object? data)
    {
        var spec = new ChartSpec { Kind = ChartType.Bubble };
        switch (data)
        {
            case null:
                break;
            case ChartSpec given:
                return given;
            case IDictionary map when DataReader.Entries(map).All(e => DataReader.IsSequence(e.Value)):
                foreach (var (key, value) in DataReader.Entries(map)) spec.Series.Add(BubbleSeries((IEnumerable)value!, key.ToString()));
                break;
            case IEnumerable sequence when data is not string:
                spec.Series.Add(BubbleSeries(sequence, null));
                break;
            default:
                throw new ArgumentException($"A bubble chart shows (x, y, size) items, not a single {data.GetType().Name}.", nameof(data));
        }

        return spec;
    }

    private static ChartSeriesSpec BubbleSeries(IEnumerable items, string? name)
    {
        var series = new ChartSeriesSpec { Name = name, X = [], Sizes = [] };
        foreach (var item in items)
        {
            var (x, y, size) = ReadBubble(item);
            series.X.Add(x);
            series.Y.Add(y);
            series.Sizes.Add(size);
        }

        return series;
    }

    private static (double? X, double? Y, double? Size) ReadBubble(object? item)
    {
        if (item == null) return default;
        if (item is ValueTuple<double, double, double> ddd) return (DataReader.Finite(ddd.Item1), DataReader.Finite(ddd.Item2), DataReader.Finite(ddd.Item3));
        object?[]? parts = null;
        if (DataReader.TryItems(item, out var items)) parts = items;
        else if (item is IEnumerable sequence and not string) parts = sequence.Cast<object?>().Take(4).ToArray();
        if (parts is { Length: >= 3 })
        {
            DataReader.TryNumber(parts[0], out var px);
            DataReader.TryNumber(parts[1], out var py);
            DataReader.TryNumber(parts[2], out var ps);
            return (px, py, ps);
        }

        // A record: x, the value (y, value, amount…) and a size (size, r, radius…) by name.
        DataReader.TryMember(item, ["x"], out _, out var xm);
        DataReader.TryMember(item, ValueNames, out var valueName, out var ym);
        DataReader.TryMember(item, SizeNames, out _, out var sm);
        DataReader.TryNumber(xm, out var rx);
        DataReader.TryNumber(ym, out var ry);
        DataReader.TryNumber(sm, out var rs);
        return (rx, ry, rs);
    }
}
