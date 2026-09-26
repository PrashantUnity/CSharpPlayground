using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// Values crossing between the C# kernel and another language's for <c>#!share</c>, as JSON: a C# value as JSON, a
/// JSON value as the C# type that fits it best (<c>[1,2,3]</c> is an <c>int[]</c>, <c>{"a": 1}</c> a
/// <c>Dictionary&lt;string, object&gt;</c>), and the name it's declared under.
/// </summary>
public static class KernelValueSharing
{
    /// <summary>Beyond this much JSON a value isn't worth copying between kernels (a file would serve better).</summary>
    public const int MaxJsonLength = 32 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            IncludeFields = true,
            MaxDepth = 64,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        // NaN and infinities aren't JSON; Python's side sends them as null too.
        options.Converters.Add(new FiniteDoubleConverter());
        options.Converters.Add(new FiniteSingleConverter());
        return options;
    }

    /// <summary>A C# value as JSON. Throws <see cref="KernelValueException"/> for what JSON can't carry (a control, an image, a delegate…).</summary>
    public static string ToJson(object? value, Type? declaredType, string name)
    {
        if (value != null && Unshareable(value.GetType()) is { } kind)
        {
            throw new KernelValueException($"'{name}' is {kind}, which can't be shared: only data (numbers, text, lists, dictionaries, records…) can.");
        }

        try
        {
            var shareable = value is DataTable table ? Records(table) : value;
            var json = JsonSerializer.Serialize(shareable, shareable?.GetType() ?? declaredType ?? typeof(object), JsonOptions);
            return json.Length <= MaxJsonLength
                ? json
                : throw new KernelValueException($"'{name}' is too big to share ({json.Length / (1024 * 1024)} MB of JSON). Save it to a file and read that instead.");
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException or ArgumentException)
        {
            throw new KernelValueException($"'{name}' ({Friendly(value?.GetType() ?? declaredType)}) can't be shared as JSON: {ex.Message}");
        }
    }

    /// <summary>Reads <paramref name="json"/> as a <paramref name="type"/>; false (with why) when it doesn't fit.</summary>
    public static bool TryFromJson(string json, Type type, out object? value, out string? problem)
    {
        try
        {
            value = JsonSerializer.Deserialize(json, type, JsonOptions);
            problem = null;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            value = null;
            problem = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// The C# type that fits a JSON value best, and the value as it: whole numbers are <c>int</c> (or <c>long</c>), others
    /// <c>double</c>; lists of one kind are arrays (<c>int[]</c>, <c>double[][]</c>, <c>string[]</c>…); other lists are
    /// <c>List&lt;object&gt;</c> and objects <c>Dictionary&lt;string, object&gt;</c>.
    /// </summary>
    public static (Type Type, object? Value) Infer(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException ex)
        {
            throw new KernelValueException($"The shared value isn't valid JSON: {ex.Message}");
        }

        using (document)
        {
            return Infer(document.RootElement);
        }
    }

    private static (Type Type, object? Value) Infer(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return Number(element);
            case JsonValueKind.String:
                return (typeof(string), element.GetString());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return (typeof(bool), element.GetBoolean());
            case JsonValueKind.Array:
                return Array(element);
            case JsonValueKind.Object:
                return (typeof(Dictionary<string, object>), Loose(element));
            default:
                return (typeof(object), null);
        }
    }

    private static (Type Type, object? Value) Number(JsonElement element)
    {
        var raw = element.GetRawText();
        var whole = raw.IndexOfAny(['.', 'e', 'E']) < 0;
        if (whole && element.TryGetInt32(out var i)) return (typeof(int), i);
        if (whole && element.TryGetInt64(out var l)) return (typeof(long), l);
        return (typeof(double), element.GetDouble());
    }

    private static (Type Type, object? Value) Array(JsonElement array)
    {
        var items = array.EnumerateArray().Select(Infer).ToList();
        if (items.Count > 0)
        {
            var types = items.Select(item => item.Type).Distinct().ToList();
            if (types.Count == 1 && types[0] != typeof(object) && types[0] != typeof(Dictionary<string, object>) && !IsList(types[0]))
            {
                return ArrayOf(types[0], items.Select(item => item.Value));
            }

            // Numbers of mixed kinds (1 and 2.5) are all doubles; whole numbers too big for int are all longs.
            if (types.All(t => t == typeof(int) || t == typeof(long) || t == typeof(double)))
            {
                var widest = types.Contains(typeof(double)) ? typeof(double) : typeof(long);
                return ArrayOf(widest, items.Select(item => Convert.ChangeType(item.Value, widest, CultureInfo.InvariantCulture)));
            }

            // Lists of lists of one kind: int[][], double[][], string[][]…
            if (types.All(t => t.IsArray && t.GetElementType() is { } e && (e.IsPrimitive || e == typeof(string))))
            {
                var elements = types.Select(t => t.GetElementType()!).Distinct().ToList();
                var inner = elements.Count == 1 ? elements[0]
                    : elements.All(e => e == typeof(int) || e == typeof(long) || e == typeof(double)) ? (elements.Contains(typeof(double)) ? typeof(double) : typeof(long))
                    : null;
                if (inner != null)
                {
                    var rows = items.Select(item => ArrayOf(inner, ((System.Array)item.Value!).Cast<object?>()
                        .Select(v => inner == typeof(string) ? v : Convert.ChangeType(v, inner, CultureInfo.InvariantCulture))).Value).ToList();
                    return ArrayOf(inner.MakeArrayType(), rows);
                }
            }
        }

        return (typeof(List<object>), Loose(array));
    }

    private static bool IsList(Type type) => type == typeof(List<object>);

    private static (Type Type, object? Value) ArrayOf(Type elementType, IEnumerable<object?> values)
    {
        var list = values.ToList();
        var array = System.Array.CreateInstance(elementType, list.Count);
        for (var i = 0; i < list.Count; i++) array.SetValue(list[i], i);
        return (elementType.MakeArrayType(), array);
    }

    // Inside a List<object> or Dictionary<string, object>: numbers, text, true/false, null, and more lists and dictionaries.
    private static object? Loose(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => Number(element).Value,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(Loose).ToList(),
        JsonValueKind.Object => LooseObject(element),
        _ => null
    };

    // A key that appears twice keeps its last value, as JSON readers do.
    private static Dictionary<string, object?> LooseObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject()) dictionary[property.Name] = Loose(property.Value);
        return dictionary;
    }

    /// <summary>
    /// <paramref name="name"/> as a C# identifier (<c>@class</c> for a keyword). Throws <see cref="KernelValueException"/>
    /// for a name C# can't have, e.g. <c>my-list</c>.
    /// </summary>
    public static string Identifier(string name)
    {
        var bare = name.StartsWith('@') ? name[1..] : name;
        if (!SyntaxFacts.IsValidIdentifier(bare))
        {
            throw new KernelValueException($"'{name}' isn't a valid C# name. Use #!share ... --as <name> to give it one, e.g. --as {Suggest(bare)}.");
        }

        // @ makes any keyword a name (@class, @await), and the variable is still called "class".
        return SyntaxFacts.GetKeywordKind(bare) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(bare) != SyntaxKind.None
            ? "@" + bare
            : bare;
    }

    private static string Suggest(string name)
    {
        var letters = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        return letters.Length == 0 ? "value" : char.IsDigit(letters[0]) ? "value_" + letters : letters;
    }

    /// <summary>A type's name as generated code writes it: <c>global::System.Int32[]</c>, <c>global::System.Collections.Generic.List&lt;global::System.Object&gt;</c>.</summary>
    public static string TypeName(Type type)
    {
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
        if (!type.IsGenericType) return "global::" + type.FullName;

        var definition = type.GetGenericTypeDefinition().FullName!;
        return "global::" + definition[..definition.IndexOf('`')] + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
    }

    private static List<Dictionary<string, object?>> Records(DataTable table) =>
        table.Rows.Cast<DataRow>()
            .Select(row => table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName, c => row[c] is DBNull ? null : row[c]))
            .ToList();

    // What can't cross as data, named for the message.
    private static string? Unshareable(Type type)
    {
        if (typeof(Delegate).IsAssignableFrom(type)) return "a function";
        if (typeof(Stream).IsAssignableFrom(type)) return "a stream";
        if (typeof(Task).IsAssignableFrom(type)) return "a task (await it first)";
        if (typeof(Type).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type) || typeof(Assembly).IsAssignableFrom(type)) return "a type or member";
        if (typeof(Avalonia.Visual).IsAssignableFrom(type)) return "a control";
        if (typeof(Avalonia.Media.IImage).IsAssignableFrom(type)) return "an image";
        if (type == typeof(IntPtr) || type == typeof(UIntPtr) || typeof(System.Runtime.InteropServices.SafeHandle).IsAssignableFrom(type)) return "a handle";
        return null;
    }

    private static string Friendly(Type? type) => type == null ? "null" : DumpTableBuilder.GetFriendlyTypeName(type);

    private sealed class FiniteDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsFinite(value)) writer.WriteNumberValue(value);
            else writer.WriteNullValue();
        }

        public override bool HandleNull => true;
    }

    private sealed class FiniteSingleConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? float.NaN : reader.GetSingle();

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            if (float.IsFinite(value)) writer.WriteNumberValue(value);
            else writer.WriteNullValue();
        }

        public override bool HandleNull => true;
    }
}
