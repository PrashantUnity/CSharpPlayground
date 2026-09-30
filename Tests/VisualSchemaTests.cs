using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The schemas in docs/visuals/schema are generated from the spec types; this keeps them current. After changing a spec
/// type, run the tests once with FRY_UPDATE_SCHEMAS=1 to rewrite them, and check the diff in.
/// </summary>
public class VisualSchemaTests
{
    // Files for people to read: "+" and "'" as themselves, not + and ' (they are never embedded in HTML).
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Theory]
    [InlineData(VisualFamily.Chart)]
    [InlineData(VisualFamily.Plot3D)]
    [InlineData(VisualFamily.Visualizer)]
    public void TheCheckedInSchema_IsTheOneTheSpecTypesDescribe(VisualFamily family)
    {
        var generated = VisualSchemas.Generate(family).ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
        var path = Path.Combine(RepositoryPaths.Root, "docs", "visuals", "schema", VisualSchemas.FileName(family));

        if (Environment.GetEnvironmentVariable("FRY_UPDATE_SCHEMAS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing: run the tests with FRY_UPDATE_SCHEMAS=1 to write it.");
        Assert.True(File.ReadAllText(path).ReplaceLineEndings("\n") == generated,
            $"{path} is out of date: run the tests with FRY_UPDATE_SCHEMAS=1 and check the diff in.");
    }

    [Fact]
    public void TheSchemas_CarryTheDocumentationAndTheWireRules()
    {
        var chart = VisualSchemas.Generate(VisualFamily.Chart);
        var json = chart.ToJsonString();

        Assert.Equal("application/vnd.fry.chart.v1+json", (string?)chart["x-mimeType"]);
        Assert.Contains("A 2D chart", (string?)chart["description"]);
        Assert.Contains("\"histogram\"", json);                       // enums as the JSON spells them
        Assert.Contains("How many bars a histogram groups", json);      // member docs become descriptions
        Assert.Equal("false", chart["additionalProperties"]?.ToJsonString()); // unknown fields are errors

        var visualizer = VisualSchemas.Generate(VisualFamily.Visualizer).ToJsonString();
        Assert.Contains("A grid cell: [row, column].", visualizer);     // element references
        Assert.Contains("\"required\":[\"id\"]", visualizer.Replace(" ", ""));
    }
}
