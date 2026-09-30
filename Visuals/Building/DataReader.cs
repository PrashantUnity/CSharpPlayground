using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>
/// How C# values read as the data of a visual, by the conventions every language follows (docs/visual-protocol.md): a
/// number is any numeric type (or a string that is one), a missing number (null, NaN, ∞) is a gap, a tuple's items are
/// its fields in order, and a record's fields are found by name, whatever its type. Every item is read by its own
/// type, so a list may mix them.
/// </summary>
internal static class DataReader
{
    private static readonly ConditionalWeakTable<Type, RecordShape> Shapes = new();

    /// <summary>Whether <paramref name="value"/> is a number: <paramref name="number"/> is null for a missing one (null, NaN or ∞).</summary>
    public static bool TryNumber(object? value, out double? number)
    {
        number = null;
        switch (value)
        {
            case null: return true;
            case double d: number = Finite(d); return true;
            case float f: number = Finite(f); return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case decimal m: number = (double)m; return true;
            case short s: number = s; return true;
            case byte b: number = b; return true;
            case sbyte sb: number = sb; return true;
            case ushort us: number = us; return true;
            case uint ui: number = ui; return true;
            case ulong ul: number = ul; return true;
            case Half h: number = Finite((double)h); return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                number = Finite(parsed);
                return true;
            default: return false;
        }
    }

    /// <summary>A number that is there (not missing, and a number at all).</summary>
    public static bool TryPresentNumber(object? value, out double number)
    {
        number = 0;
        if (!TryNumber(value, out var read) || read is not { } present) return false;
        number = present;
        return true;
    }

    public static double? Finite(double value) => double.IsFinite(value) ? value : null;

    /// <summary>
    /// A map's entries, read through its own enumerator: a generic dictionary enumerated as a plain sequence gives key-value
    /// pairs, not entries.
    /// </summary>
    public static IEnumerable<(object Key, object? Value)> Entries(IDictionary map)
    {
        var entries = map.GetEnumerator();
        while (entries.MoveNext()) yield return (entries.Key, entries.Value);
    }

    /// <summary>A sequence, but not text (a string is one value, not its characters).</summary>
    public static bool IsSequence(object? value) => value is IEnumerable and not string;

    /// <summary>A tuple's or pair's items in order: value tuples, <see cref="Tuple"/>s and key-value pairs.</summary>
    public static bool TryItems(object value, out object?[] items)
    {
        items = [];
        if (value is ITuple tuple)
        {
            items = new object?[tuple.Length];
            for (var i = 0; i < tuple.Length; i++) items[i] = tuple[i];
            return true;
        }

        var shape = ShapeOf(value.GetType());
        if (!shape.IsKeyValuePair) return false;
        items = [shape.Get(value, "Key"), shape.Get(value, "Value")];
        return true;
    }

    /// <summary>A record's member: the first of <paramref name="names"/> it has, ignoring case.</summary>
    public static bool TryMember(object value, IEnumerable<string> names, out string name, out object? member)
    {
        var shape = ShapeOf(value.GetType());
        foreach (var candidate in names)
        {
            if (shape.Has(candidate))
            {
                name = candidate;
                member = shape.Get(value, candidate);
                return true;
            }
        }

        name = string.Empty;
        member = null;
        return false;
    }

    /// <summary>A record's members, in declaration order, with their values.</summary>
    public static IEnumerable<(string Name, object? Value)> Members(object value)
    {
        var shape = ShapeOf(value.GetType());
        return shape.Names.Select(n => (n, shape.Get(value, n)));
    }

    /// <summary>Whether the value is a plain value (a number, text, a boolean…) rather than a record to look inside.</summary>
    public static bool IsScalar(object? value) =>
        value is null or string or bool or char or Enum or DateTime or DateTimeOffset or TimeSpan or Guid || TryNumber(value, out _);

    private static RecordShape ShapeOf(Type type) => Shapes.GetValue(type, t => new RecordShape(t));

    // A type's public instance properties and fields, found once per type.
    private sealed class RecordShape
    {
        private readonly Dictionary<string, Func<object, object?>> _getters = new(StringComparer.OrdinalIgnoreCase);

        public RecordShape(Type type)
        {
            IsKeyValuePair = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || !property.CanRead) continue;
                Add(property.Name, property.GetValue);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Add(field.Name, field.GetValue);
        }

        public bool IsKeyValuePair { get; }

        public List<string> Names { get; } = [];

        public bool Has(string name) => _getters.ContainsKey(name);

        public object? Get(object value, string name)
        {
            try
            {
                return _getters[name](value);
            }
            catch (TargetInvocationException)
            {
                return null; // a property that throws reads as missing
            }
        }

        private void Add(string name, Func<object, object?> getter)
        {
            if (!_getters.TryAdd(name, getter)) return;
            Names.Add(name);
        }
    }
}
