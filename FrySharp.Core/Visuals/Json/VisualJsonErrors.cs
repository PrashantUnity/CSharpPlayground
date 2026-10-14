using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// Turns System.Text.Json's messages, written for .NET developers, into ones for whoever wrote the JSON in any language:
/// <c>$.series[0].y[3]: must be a number or null</c> rather than <c>could not be converted to System.Nullable`1[System.Double]</c>.
/// </summary>
internal static partial class VisualJsonErrors
{
    public static string FamilyName(VisualFamily family) => family switch
    {
        VisualFamily.Chart => "chart",
        VisualFamily.Plot3D => "3D plot",
        _ => "visualizer"
    };

    public static string Describe(JsonException ex)
    {
        var path = string.IsNullOrEmpty(ex.Path) ? "$" : ex.Path;
        var message = PositionSuffix().Replace(ex.Message, string.Empty).Trim();

        if (UnknownProperty().Match(message) is { Success: true } unknown)
        {
            var name = unknown.Groups["name"].Value;
            var suggestion = Closest(name, JsonNamesOf(unknown.Groups["type"].Value));
            return $"{path}: unknown field \"{name}\"" + (suggestion != null ? $" (did you mean \"{suggestion}\"?)" : string.Empty);
        }

        if (MissingRequired().Match(message) is { Success: true } missing)
        {
            return $"{path}: missing {missing.Groups["names"].Value.Replace("'", "\"")}";
        }

        if (NotConvertible().Match(message) is { Success: true } conversion)
        {
            return $"{path}: must be {Expected(conversion.Groups["type"].Value)}";
        }

        if (NullNotAllowed().IsMatch(message))
        {
            return $"{path}: can't be null";
        }

        return $"{path}: {message.TrimEnd('.')}";
    }

    private static string Expected(string typeName)
    {
        var nullable = typeName.StartsWith("System.Nullable`1[", StringComparison.Ordinal);
        var inner = nullable ? typeName["System.Nullable`1[".Length..^1] : typeName;
        var expected = inner switch
        {
            "System.Double" or "System.Single" or "System.Decimal" => "a number",
            "System.Int32" or "System.Int64" => "a whole number",
            "System.Boolean" => "true or false",
            "System.String" => "a string",
            _ when inner.StartsWith("System.Collections.Generic.List`1", StringComparison.Ordinal) => "a list",
            _ when inner.StartsWith("System.Collections.Generic.Dictionary`2", StringComparison.Ordinal) => "an object of names and texts",
            _ => SpecTypeDescription(inner)
        };
        return nullable ? expected + " or null" : expected;
    }

    private static string SpecTypeDescription(string typeName)
    {
        var type = typeof(VisualSpec).Assembly.GetType(typeName);
        if (type is { IsEnum: true })
        {
            return "one of " + string.Join(", ", EnumJsonNames(type).Select(n => $"\"{n}\""));
        }

        return "an object";
    }

    /// <summary>An enum's values as the JSON spells them.</summary>
    public static IEnumerable<string> EnumJsonNames(Type enumType) =>
        enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(JsonName);

    /// <summary>One enum value as the JSON spells it (<c>Plot3DType.Graph3D</c> is <c>"graph"</c>).</summary>
    public static string JsonName(Enum value) =>
        value.GetType().GetField(value.ToString()) is { } field ? JsonName(field) : JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string JsonName(FieldInfo field) =>
        field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(field.Name);

    private static IEnumerable<string> JsonNamesOf(string typeName)
    {
        var type = typeof(VisualSpec).Assembly.GetType(typeName);
        return type == null ? [] : VisualJson.Options.GetTypeInfo(type).Properties.Select(p => p.Name);
    }

    // The field the writer most likely meant: the known name fewest edits away, if it is close (one edit for a short
    // name, two or a third of the letters for a longer one).
    private static string? Closest(string name, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = EditDistance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        var allowed = name.Length <= 3 ? 1 : Math.Max(2, name.Length / 3);
        return bestDistance <= allowed ? best : null;
    }

    // Edits between two names, a swap of neighbouring letters counting as one ("titel" is one edit from "title").
    private static int EditDistance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }

    [GeneratedRegex(@"\s*Path: .*$", RegexOptions.Singleline)]
    private static partial Regex PositionSuffix();

    [GeneratedRegex(@"The JSON property '(?<name>.+?)' could not be mapped to any \.NET member contained in type '(?<type>.+?)'")]
    private static partial Regex UnknownProperty();

    [GeneratedRegex(@"missing required properties including: (?<names>.+?)\.?$")]
    private static partial Regex MissingRequired();

    [GeneratedRegex(@"could not be converted to (?<type>[^\s]+?)\.?$")]
    private static partial Regex NotConvertible();

    // RespectNullableAnnotations: "The property or field 'title' on type '…' doesn't allow setting null values."
    [GeneratedRegex(@"allow (getting |setting )?null values", RegexOptions.IgnoreCase)]
    private static partial Regex NullNotAllowed();
}
