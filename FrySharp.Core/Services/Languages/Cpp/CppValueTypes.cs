using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>The C++ type a value from another language gets (<c>#!share --from csharp rows</c>), from its JSON: what C++ would call it, and no more.</summary>
internal static class CppValueTypes
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
            return "fry::Json";
        }
    }

    private static string TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.String => "std::string",
        JsonValueKind.Number => value.GetRawText().Any(c => c is '.' or 'e' or 'E') || !value.TryGetInt64(out _) ? "double" : "long long",
        JsonValueKind.Array => ArrayType(value),
        _ => "fry::Json" // an object, or null: C++ has no type for "anything", so the value stays JSON the cell can read (j["name"].as_string())
    };

    // A list of one kind of thing is a vector of it (numbers: whole, or double when any isn't); a mixed one stays JSON.
    private static string ArrayType(JsonElement array)
    {
        var types = array.EnumerateArray().Select(TypeOf).Distinct().ToList();
        return types.Count switch
        {
            0 => "std::vector<long long>",
            1 when types[0] == "fry::Json" => "fry::Json",
            1 => "std::vector<" + types[0] + ">",
            2 when types.Contains("long long") && types.Contains("double") => "std::vector<double>",
            _ => "fry::Json"
        };
    }
}
