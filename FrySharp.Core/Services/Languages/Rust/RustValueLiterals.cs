using System.Globalization;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// A value another language shared (<c>#!share --from csharp nums</c>) as Rust source: JSON becomes the Rust type that fits it
/// best, <c>[1,2,3]</c> a <c>Vec&lt;i64&gt;</c>, <c>"x"</c> a <c>String</c>, an object or a mixed list a <c>fry::Json</c>.
/// </summary>
internal static class RustValueLiterals
{
    private const string JsonType = "fry::Json";

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "else", "enum", "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop",
        "match", "mod", "move", "mut", "pub", "ref", "return", "static", "struct", "trait", "true", "type", "unsafe", "use",
        "where", "while", "async", "await", "dyn", "abstract", "become", "box", "do", "final", "macro", "override", "priv",
        "typeof", "unsized", "virtual", "yield", "try", "gen"
    };

    // These can't be raw identifiers.
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "self", "Self", "super", "crate", "_" };

    /// <summary>
    /// The variable's name as Rust source (a keyword becomes a raw identifier), or a <see cref="KernelValueException"/> saying why the
    /// name can't be a Rust variable.
    /// </summary>
    public static string VariableName(string name)
    {
        var valid = !string.IsNullOrEmpty(name) && (name[0] == '_' || char.IsLetter(name[0])) && name.All(c => c == '_' || char.IsLetterOrDigit(c));
        if (!valid || Reserved.Contains(name))
        {
            throw new KernelValueException($"'{name}' isn't a name Rust can use for a variable: use letters, digits and underscores, starting with a letter (rename it with --as).");
        }

        return Keywords.Contains(name) ? "r#" + name : name;
    }

    /// <summary>The Rust type a JSON value is declared with.</summary>
    public static string TypeOf(string json)
    {
        using var document = Parse(json);
        return TypeOf(document.RootElement);
    }

    /// <summary><c>let name: Type = value;</c> for a JSON value.</summary>
    public static string Declare(string name, string json)
    {
        using var document = Parse(json);
        var type = TypeOf(document.RootElement);
        return $"let {VariableName(name)}: {type} = {Expression(document.RootElement, type)};";
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException ex)
        {
            throw new KernelValueException($"The shared value isn't valid JSON: {ex.Message}");
        }
    }

    private static string TypeOf(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True or JsonValueKind.False:
                return "bool";
            case JsonValueKind.String:
                return "String";
            case JsonValueKind.Number:
                return IsInteger(element) ? (element.TryGetInt64(out _) ? "i64" : element.TryGetUInt64(out _) ? "u64" : "f64") : "f64";
            case JsonValueKind.Null:
                return "Option<f64>";
            case JsonValueKind.Array:
                {
                    var items = element.EnumerateArray().ToList();
                    if (items.Count == 0) return "Vec<i64>";
                    string? unified = null;
                    foreach (var item in items)
                    {
                        var type = TypeOf(item);
                        unified = unified == null ? type : Unify(unified, type);
                        if (unified == null) return JsonType;
                    }

                    return $"Vec<{unified}>";
                }
            default:
                return JsonType;
        }
    }

    // A list of whole numbers and decimals is a list of decimals; a list of lists is a list of the lists' one type.
    private static string? Unify(string a, string b)
    {
        if (a == b) return a;
        if ((a, b) is ("i64", "f64") or ("f64", "i64") or ("u64", "f64") or ("f64", "u64") or ("i64", "u64") or ("u64", "i64")) return "f64";
        if (a.StartsWith("Vec<", StringComparison.Ordinal) && b.StartsWith("Vec<", StringComparison.Ordinal))
        {
            var inner = Unify(a[4..^1], b[4..^1]);
            return inner == null ? null : $"Vec<{inner}>";
        }

        return null;
    }

    private static string Expression(JsonElement element, string type)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
                return "None";
            case JsonValueKind.String:
                return "String::from(" + StringLiteral(element.GetString() ?? string.Empty) + ")";
            case JsonValueKind.Number:
                return NumberLiteral(element, type);
            case JsonValueKind.Array when type.StartsWith("Vec<", StringComparison.Ordinal):
                {
                    var inner = type[4..^1];
                    var items = element.EnumerateArray().Select(item => Expression(item, inner)).ToList();
                    return items.Count == 0 ? "Vec::new()" : "vec![" + string.Join(", ", items) + "]";
                }
            default:
                return $"{JsonType}::parse({RawString(element.GetRawText())})";
        }
    }

    private static bool IsInteger(JsonElement element)
    {
        var raw = element.GetRawText();
        return raw.IndexOfAny(['.', 'e', 'E']) < 0;
    }

    private static string NumberLiteral(JsonElement element, string type)
    {
        if (type == "f64")
        {
            var number = element.GetDouble();
            var text = number.ToString("R", CultureInfo.InvariantCulture);
            return text.Contains('.') || text.Contains('E') || text.Contains('e') ? text : text + ".0";
        }

        return element.GetRawText();
    }

    /// <summary>A Rust string literal for <paramref name="text"/>.</summary>
    public static string StringLiteral(string text)
    {
        var literal = new StringBuilder("\"");
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': literal.Append("\\\""); break;
                case '\\': literal.Append("\\\\"); break;
                case '\n': literal.Append("\\n"); break;
                case '\r': literal.Append("\\r"); break;
                case '\t': literal.Append("\\t"); break;
                case '\0': literal.Append("\\0"); break;
                case var control when char.IsControl(control): literal.Append("\\u{").Append(((int)control).ToString("x")).Append('}'); break;
                default: literal.Append(c); break;
            }
        }

        return literal.Append('"').ToString();
    }

    /// <summary>A raw string literal (<c>r#"…"#</c>) with as many <c>#</c> as the text needs.</summary>
    public static string RawString(string text)
    {
        var hashes = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '"') continue;
            var run = 0;
            while (i + 1 + run < text.Length && text[i + 1 + run] == '#') run++;
            hashes = Math.Max(hashes, run + 1);
        }

        var fence = new string('#', hashes);
        return "r" + fence + "\"" + text + "\"" + fence;
    }
}
