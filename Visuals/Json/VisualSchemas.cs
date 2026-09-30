using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

/// <summary>
/// The JSON Schema of each visual spec (draft 2020-12), generated from the spec types themselves, with their XML
/// documentation as the descriptions: one source for the C# API, the wire format and the docs. The checked-in copies
/// under <c>docs/visuals/schema</c> are kept current by a test; editors and other languages' tools can use them.
/// </summary>
public static class VisualSchemas
{
    public static string FileName(VisualFamily family) => family switch
    {
        VisualFamily.Chart => "chart.v1.json",
        VisualFamily.Plot3D => "plot3d.v1.json",
        _ => "visualizer.v1.json"
    };

    public static JsonObject Generate(VisualFamily family)
    {
        var specType = VisualJson.SpecType(family);
        var exporter = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = Describe
        };
        var generated = (JsonObject)VisualJson.Options.GetJsonSchemaAsNode(specType, exporter);

        var schema = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["title"] = specType.Name,
            ["x-mimeType"] = VisualMimeTypes.For(family)
        };
        foreach (var (name, value) in generated.ToList())
        {
            generated.Remove(name);
            schema[name] = value;
        }

        return schema;
    }

    private static JsonNode Describe(JsonSchemaExporterContext context, JsonNode schema)
    {
        var type = context.TypeInfo.Type;
        if (type == typeof(ScalarValue) || type == typeof(ScalarValue?))
        {
            schema = new JsonObject { ["type"] = new JsonArray("string", "number", "boolean", "null") };
        }
        else if (type == typeof(ElementRef))
        {
            schema = ElementRefSchema();
        }

        var member = context.PropertyInfo?.AttributeProvider as MemberInfo;
        var description = member != null
            ? Summary($"P:{member.DeclaringType!.FullName}.{member.Name}")
            : type.Namespace == typeof(VisualSpec).Namespace ? Summary($"T:{type.FullName}") : null;
        if (description != null && schema is JsonObject described && !described.ContainsKey("$ref"))
        {
            described["description"] = description;
        }

        return schema;
    }

    private static JsonObject ElementRefSchema() => new()
    {
        ["oneOf"] = new JsonArray(
            new JsonObject { ["type"] = "integer", ["description"] = "An item, by its index." },
            new JsonObject { ["type"] = "string", ["description"] = "A node, by its id." },
            new JsonObject
            {
                ["type"] = "array",
                ["description"] = "A grid cell: [row, column].",
                ["items"] = new JsonObject { ["type"] = "integer" },
                ["minItems"] = 2,
                ["maxItems"] = 2
            },
            new JsonObject
            {
                ["type"] = "object",
                ["description"] = "An edge, by its two ends.",
                ["properties"] = new JsonObject
                {
                    ["from"] = new JsonObject { ["type"] = "string" },
                    ["to"] = new JsonObject { ["type"] = "string" }
                },
                ["required"] = new JsonArray("from", "to"),
                ["additionalProperties"] = false
            })
    };

    // The member's <summary> as plain text, from the plugin's XML documentation file.
    private static string? Summary(string documentationId)
    {
        var xml = XmlDocumentationLookup.FindDocumentation(typeof(VisualSpec).Assembly.Location, documentationId);
        var summary = XmlDocumentationParser.Parse(xml)?.Summary;
        if (summary is not { Count: > 0 }) return null;

        var text = string.Concat(summary.Select(run => run.Text));
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
