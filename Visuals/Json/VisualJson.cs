using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// Visual specs to and from JSON, the same way for every language: camelCase names, enums as camelCase strings, nulls
/// and empty option groups left out. Reading is strict: an unknown field, a value of the wrong type or a missing required
/// field is an error that names where it is (<see cref="VisualSpecException"/>), never something silently dropped.
/// </summary>
public static class VisualJson
{
    /// <summary>The options every visual spec is written and read with.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(VisualSpec spec) => JsonSerializer.Serialize(spec, TypeInfo(spec.Family));

    public static byte[] SerializeToUtf8Bytes(VisualSpec spec) => JsonSerializer.SerializeToUtf8Bytes(spec, TypeInfo(spec.Family));

    public static JsonElement SerializeToElement(VisualSpec spec) => JsonSerializer.SerializeToElement(spec, TypeInfo(spec.Family));

    /// <summary>Reads a spec of <paramref name="family"/>; throws <see cref="VisualSpecException"/> when it isn't one.</summary>
    public static VisualSpec Deserialize(VisualFamily family, string json)
    {
        try
        {
            return (VisualSpec)(JsonSerializer.Deserialize(json, TypeInfo(family)) ?? throw NullSpec(family));
        }
        catch (JsonException ex)
        {
            throw new VisualSpecException(family, VisualJsonErrors.Describe(ex), ex);
        }
    }

    /// <inheritdoc cref="Deserialize(VisualFamily, string)"/>
    public static VisualSpec Deserialize(VisualFamily family, JsonElement json)
    {
        try
        {
            return (VisualSpec)(json.Deserialize(TypeInfo(family)) ?? throw NullSpec(family));
        }
        catch (JsonException ex)
        {
            throw new VisualSpecException(family, VisualJsonErrors.Describe(ex), ex);
        }
    }

    public static T Deserialize<T>(string json) where T : VisualSpec => (T)Deserialize(FamilyOf(typeof(T)), json);

    /// <summary>A deep copy, made by writing the spec out and reading it back: exactly what another process would get.</summary>
    public static T Clone<T>(T spec) where T : VisualSpec =>
        (T)(JsonSerializer.Deserialize(SerializeToUtf8Bytes(spec), TypeInfo(spec.Family)) ?? throw NullSpec(spec.Family));

    /// <summary>The metadata for one family's spec type.</summary>
    public static JsonTypeInfo TypeInfo(VisualFamily family) => Options.GetTypeInfo(SpecType(family));

    public static Type SpecType(VisualFamily family) => family switch
    {
        VisualFamily.Chart => typeof(ChartSpec),
        VisualFamily.Plot3D => typeof(Plot3DSpec),
        VisualFamily.Visualizer => typeof(VisualizerSpec),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
    };

    private static VisualFamily FamilyOf(Type type) =>
        type == typeof(ChartSpec) ? VisualFamily.Chart
        : type == typeof(Plot3DSpec) ? VisualFamily.Plot3D
        : type == typeof(VisualizerSpec) ? VisualFamily.Visualizer
        : throw new ArgumentOutOfRangeException(nameof(type), type, "not a visual spec type");

    private static VisualSpecException NullSpec(VisualFamily family) =>
        new(family, "the spec is null; send a JSON object");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,

            // Programs in other languages build these from maps that don't keep key order, so a shape's "type" can come last.
            AllowOutOfOrderMetadataProperties = true,

            // Specs are flat (nodes refer to each other by id), so real specs never nest deeply.
            MaxDepth = 64,
            TypeInfoResolver = VisualJsonContext.Default.WithAddedModifier(LeaveOutWhatSaysNothing)
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }

    // Left out, a value means its default; so an empty list, and an axis or legend with nothing set, aren't written:
    // {"kind": "line"} rather than {"kind": "line", "xAxis": {}, "series": []}. Every language then writes the same JSON.
    private static void LeaveOutWhatSaysNothing(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (typeof(IOptionalSpec).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = static (_, value) => value is IOptionalSpec { IsEmpty: false };
            }
            else if (property.PropertyType != typeof(string) && typeof(System.Collections.ICollection).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = static (_, value) => value is System.Collections.ICollection { Count: > 0 };
            }
        }
    }
}

/// <summary>A spec that can't be read, with what is wrong and where (a JSON path such as <c>$.series[0].y[3]</c>).</summary>
public sealed class VisualSpecException : Exception
{
    public VisualSpecException(VisualFamily family, string problem, Exception? inner = null)
        : base($"The {VisualJsonErrors.FamilyName(family)} spec isn't valid: {problem}", inner)
    {
        Family = family;
        Problem = problem;
    }

    public VisualFamily Family { get; }

    /// <summary>What is wrong, without the "isn't valid" preamble.</summary>
    public string Problem { get; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(ChartSpec))]
[JsonSerializable(typeof(Plot3DSpec))]
[JsonSerializable(typeof(VisualizerSpec))]
internal sealed partial class VisualJsonContext : JsonSerializerContext;
