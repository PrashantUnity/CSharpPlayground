using System.Text.Json;
using System.Text.Json.Nodes;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The specs the canonical calls make, from C# (the reference): <c>docs/visuals/conformance/&lt;case&gt;.json</c>. Every
/// language's SDK makes the same calls on the same data in its real conformance test and must send equal specs. Run the
/// tests once with FRY_UPDATE_SCHEMAS=1 to rewrite the files after a deliberate change, and check the diff in.
/// </summary>
public class VisualConformanceFixturesTests
{
    public sealed record TreeNode(int Val, TreeNode? Left = null, TreeNode? Right = null);

    public sealed class ListNode(int val, ListNode? next = null)
    {
        public int Val { get; } = val;
        public ListNode? Next { get; } = next;
    }

    /// <summary>Each case's name and the C# call; the other languages' tests make the same call on the same data.</summary>
    public static readonly (string Name, Action Call)[] Cases =
    [
        ("chart-numbers", () => Display.LineChart(new double?[] { 3, 1, null, 4 }, "Numbers")),
        ("chart-pairs", () => Display.ScatterChart(new[] { new[] { 1.0, 2 }, new[] { 2.0, 4.5 } }, "Pairs")),
        ("chart-labels", () => Display.BarChart(new Dictionary<string, int> { ["Mon"] = 3, ["Tue"] = 5 }, "Days")),
        ("chart-series", () => Display.Chart(new Dictionary<string, int[]> { ["a"] = [1, 2], ["b"] = [3, 4] }, "Series")),
        ("chart-records", () => Display.Chart(new[] { new { name = "Jan", value = 10 }, new { name = "Feb", value = 12 } }, "Records")),
        ("chart-histogram", () => Display.Histogram(new[] { 1, 2, 2, 3, 3, 3 }, "Samples", bins: 3)),
        ("chart-pie", () => Display.PieChart(new Dictionary<string, int> { ["A"] = 1, ["B"] = 2 }, "Share")),
        ("plot3d-points", () => Display.Scatter3D(new[] { (1, 2, 3), (4, 5, 6) }, "Points")),
        ("plot3d-surface", () => Display.Surface3D(new double[,] { { 1, 2 }, { 3, 4 } }, "Heights")),
        ("plot3d-graph", () => Display.Graph3D(new Dictionary<string, string[]> { ["a"] = ["b", "c"], ["b"] = ["c"] }, "Links")),
        ("visualizer-matrix", () => Display.Matrix(new[] { new[] { 1, 0 }, new[] { 0, 1 } }, "Grid")),
        ("visualizer-array", () => Display.Array(new[] { 1, 3, 5 }, new Dictionary<string, int> { ["i"] = 0, ["j"] = 2 }, "Two pointers")),
        ("visualizer-tree", () => Display.Tree(new TreeNode(2, new TreeNode(1), new TreeNode(3)), "Tree")),
        ("visualizer-tree-level-order", () => Display.Tree(new int?[] { 1, 2, 3, null, 4 }, "Level order")),
        ("visualizer-graph", () => Display.Graph(new Dictionary<string, string[]> { ["a"] = ["b"], ["b"] = ["c"] }, "Graph")),
        ("visualizer-linked-list", () => Display.LinkedList(new ListNode(1, new ListNode(2, new ListNode(3))), "List")),
        ("visualizer-bars", () => Display.Bars(new[] { 3, 1, 2 }, "Bars")),
        ("visualizer-islands", () => Display.Islands(new[] { new[] { 1, 0 }, new[] { 1, 1 } }, "Islands")),
    ];

    public static IEnumerable<object[]> CaseNames() => Cases.Select(c => new object[] { c.Name });

    public static string PathOf(string name) => Path.Combine(RepositoryPaths.Root, "docs", "visuals", "conformance", name + ".json");

    /// <summary>What a call sends, as a fixture holds it: the MIME type and the spec.</summary>
    public static string Fixture(Action call)
    {
        VisualOutput? output = null;
        using (InteractiveDisplayContext.EnterScope(o => output ??= o.Visual)) call();
        var visual = output ?? throw new InvalidOperationException("The call showed no visual.");
        var fixture = new JsonObject
        {
            ["mime"] = visual.MimeType,
            ["spec"] = JsonNode.Parse(VisualJson.SerializeToUtf8Bytes(visual.Spec))
        };
        return fixture.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 2 }).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// How a spec of <paramref name="mime"/> draws: another language's spec conforms when it draws as the fixture does
    /// (what the host works out, it may leave out). Throws when the spec isn't valid.
    /// </summary>
    public static string DrawnAs(string mime, JsonElement spec)
    {
        Assert.True(VisualMimeTypes.TryGetFamily(mime, out var family), $"{mime} isn't a visual MIME type");
        var parsed = VisualJson.Deserialize(family, spec);
        var errors = VisualSpecValidator.Validate(parsed);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        return VisualDrawing.Prepare(parsed).Model switch
        {
            PdfEditorApp.Plugins.CSharpEditor.Charting.Models.ChartOptions chart => VisualFingerprints.Of(chart),
            PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models.Plot3DOptions plot => VisualFingerprints.Of(plot),
            PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models.VisualizerOptions visualizer => VisualFingerprints.Of(visualizer),
            var other => throw new InvalidOperationException($"{mime} drew as {other?.GetType().Name ?? "nothing"}")
        };
    }

    /// <summary>The fixture of <paramref name="name"/>: its MIME type, and how it draws.</summary>
    public static (string Mime, string Drawn) Expected(string name)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(PathOf(name)));
        var mime = fixture.RootElement.GetProperty("mime").GetString()!;
        return (mime, DrawnAs(mime, fixture.RootElement.GetProperty("spec")));
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void TheCheckedInFixture_IsWhatTheCSharpCallSends(string name)
    {
        var generated = Fixture(Cases.Single(c => c.Name == name).Call);
        var path = PathOf(name);
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
}
