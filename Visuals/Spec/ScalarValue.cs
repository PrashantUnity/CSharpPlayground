using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>What a <see cref="ScalarValue"/> holds.</summary>
public enum ScalarKind
{
    Null,
    Boolean,
    Integer,
    Real,
    Text
}

/// <summary>
/// A value shown inside a cell, node or item: a JSON number, string, boolean or null. The studio formats it, so a value
/// reads the same whichever language sent it: invariant numbers, lowercase booleans, ∞ for the usual "infinity"
/// sentinels (the largest 32- and 64-bit integers). Anything else is sent as text.
/// </summary>
[JsonConverter(typeof(ScalarValueJsonConverter))]
public readonly struct ScalarValue : IEquatable<ScalarValue>
{
    private ScalarValue(ScalarKind kind, long integer = 0, double real = 0, string? text = null, bool boolean = false)
    {
        Kind = kind;
        Integer = integer;
        Real = real;
        Text = text;
        Boolean = boolean;
    }

    public ScalarKind Kind { get; }
    public long Integer { get; }
    public double Real { get; }
    public string? Text { get; }
    public bool Boolean { get; }

    public static ScalarValue Null => default;
    public static ScalarValue FromInteger(long value) => new(ScalarKind.Integer, integer: value);
    public static ScalarValue FromText(string? value) => value == null ? Null : new(ScalarKind.Text, text: value);
    public static ScalarValue FromBoolean(bool value) => new(ScalarKind.Boolean, boolean: value);

    /// <summary>A real number; infinity and NaN, which JSON cannot carry, become the text they are shown as.</summary>
    public static ScalarValue FromReal(double value)
    {
        if (double.IsPositiveInfinity(value)) return FromText("∞");
        if (double.IsNegativeInfinity(value)) return FromText("-∞");
        if (double.IsNaN(value)) return FromText("NaN");
        return new ScalarValue(ScalarKind.Real, real: value);
    }

    /// <summary>A number as a number, a string, char or enum as text, a boolean as itself, anything else by <paramref name="format"/>.</summary>
    public static ScalarValue From(object? value, Func<object, string>? format = null) => value switch
    {
        null => Null,
        ScalarValue scalar => scalar,
        bool b => FromBoolean(b),
        string s => FromText(s),
        char c => FromText(c.ToString()),
        sbyte or byte or short or ushort or int or uint or long => FromInteger(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        ulong u => u <= long.MaxValue ? FromInteger((long)u) : FromReal(u),
        float f => FromReal(f),
        double d => FromReal(d),
        decimal m => FromReal((double)m),
        Enum e => FromText(e.ToString()),
        _ => FromText(format != null ? format(value) : value.ToString())
    };

    public bool IsNull => Kind == ScalarKind.Null;

    /// <summary>The value as a number, when it is one.</summary>
    public double? AsNumber() => Kind switch
    {
        ScalarKind.Integer => Integer,
        ScalarKind.Real => Real,
        _ => null
    };

    /// <summary>How the value reads in a visual.</summary>
    public override string ToString() => Kind switch
    {
        ScalarKind.Null => "null",
        ScalarKind.Boolean => Boolean ? "true" : "false",
        ScalarKind.Integer => Integer switch
        {
            int.MaxValue or long.MaxValue => "∞",
            int.MinValue or long.MinValue => "-∞",
            _ => Integer.ToString(CultureInfo.InvariantCulture)
        },
        ScalarKind.Real => Real.ToString("G", CultureInfo.InvariantCulture),
        _ => Text ?? string.Empty
    };

    /// <summary>Whether the value reads as <paramref name="text"/>: <see cref="ToString"/>, compared without making the string.</summary>
    internal bool ReadsAs(string? text)
    {
        if (text == null) return false;
        switch (Kind)
        {
            case ScalarKind.Null:
                return text == "null";
            case ScalarKind.Boolean:
                return text == (Boolean ? "true" : "false");
            case ScalarKind.Text:
                return text == (Text ?? string.Empty);
            case ScalarKind.Integer when Integer is not (int.MaxValue or long.MaxValue or int.MinValue or long.MinValue):
                Span<char> digits = stackalloc char[20];
                return Integer.TryFormat(digits, out var written, default, CultureInfo.InvariantCulture) && text.AsSpan().SequenceEqual(digits[..written]);
            default:
                return text == ToString();
        }
    }

    public bool Equals(ScalarValue other) =>
        Kind == other.Kind && Integer == other.Integer && Real.Equals(other.Real) && Text == other.Text && Boolean == other.Boolean;

    public override bool Equals(object? obj) => obj is ScalarValue other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, Integer, Real, Text, Boolean);
    public static bool operator ==(ScalarValue left, ScalarValue right) => left.Equals(right);
    public static bool operator !=(ScalarValue left, ScalarValue right) => !left.Equals(right);

    public static implicit operator ScalarValue(long value) => FromInteger(value);
    public static implicit operator ScalarValue(double value) => FromReal(value);
    public static implicit operator ScalarValue(bool value) => FromBoolean(value);

    // Explicit, because the literal null converts to a string: were this implicit, `changed ? value : null` would be a
    // null value (ScalarValue.Null) rather than no value at all.
    public static explicit operator ScalarValue(string? value) => FromText(value);

    // Without this a char would take the long conversion and show as its code ('a' as 97).
    public static implicit operator ScalarValue(char value) => FromText(value.ToString());
}

/// <summary>Reads and writes a <see cref="ScalarValue"/> as the JSON scalar it is.</summary>
public sealed class ScalarValueJsonConverter : JsonConverter<ScalarValue>
{
    public override bool HandleNull => true;

    public override ScalarValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Null => ScalarValue.Null,
        JsonTokenType.True => ScalarValue.FromBoolean(true),
        JsonTokenType.False => ScalarValue.FromBoolean(false),
        JsonTokenType.String => ScalarValue.FromText(reader.GetString()),
        JsonTokenType.Number when reader.TryGetInt64(out var whole) => ScalarValue.FromInteger(whole),
        JsonTokenType.Number => ScalarValue.FromReal(reader.GetDouble()),
        _ => throw new JsonException("expected a number, string, boolean or null")
    };

    public override void Write(Utf8JsonWriter writer, ScalarValue value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ScalarKind.Null: writer.WriteNullValue(); break;
            case ScalarKind.Boolean: writer.WriteBooleanValue(value.Boolean); break;
            case ScalarKind.Integer: writer.WriteNumberValue(value.Integer); break;
            case ScalarKind.Real: writer.WriteNumberValue(value.Real); break;
            default: writer.WriteStringValue(value.Text); break;
        }
    }
}
