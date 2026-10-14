using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>The Go type a value from another language gets (<c>#!share --from csharp rows</c>), from its JSON: what Go would call it, and no more.</summary>
internal static class GoValueTypes
{
    public static string Infer(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TypeOf(document.RootElement);
        }
        catch (JsonException)
        {
            return "any";
        }
    }

    private static string TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.String => "string",
        JsonValueKind.Number => value.GetRawText().Any(c => c is '.' or 'e' or 'E') || !value.TryGetInt64(out _) ? "float64" : "int",
        JsonValueKind.Object => "map[string]any",
        JsonValueKind.Array => ArrayType(value),
        _ => "any"
    };

    // A list of one kind of thing is a slice of it (numbers: ints, or floats when any isn't whole); a mixed one is a slice of any.
    private static string ArrayType(JsonElement array)
    {
        var types = array.EnumerateArray().Select(TypeOf).Distinct().ToList();
        return types.Count switch
        {
            0 => "[]any",
            1 => "[]" + types[0],
            2 when types.Contains("int") && types.Contains("float64") => "[]float64",
            _ => "[]any"
        };
    }
}
