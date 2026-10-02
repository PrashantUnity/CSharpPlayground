using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary>
/// <c>visuals [word ...]</c>: draws specs through <see cref="VisualOutputView"/>, the view behind every chart, 3D plot and
/// visualizer output, reached the way a program's display reaches it: the spec fixtures in Tests/Fixtures/Visuals (all
/// of them, or those whose names contain one of the words), or any spec or display bundle given with <c>--file</c>.
/// </summary>
internal static class VisualSnapshots
{
    public static void Render(Options options)
    {
        int width = options.Int("width", 900);
        int height = options.Int("height", 600);
        foreach (var (name, output) in Outputs(options))
        {
            if (output.Visual == null)
            {
                Console.Error.WriteLine($"{name}: {output.Text ?? "not a visual"}");
                continue;
            }

            var view = new VisualOutputView { Output = output.Visual };
            var window = Snapshot.Show(new ScrollViewer { Content = new Border { Padding = new Thickness(10), Child = view } }, width, height);
            if (!Snapshot.WaitFor(() => !view.IsPreparing, TimeSpan.FromSeconds(30)))
            {
                Console.Error.WriteLine($"{name}: still preparing after 30 s");
            }

            Snapshot.Save(window, options.Value("name") ?? $"visual_{name}");
            window.Close();
        }
    }

    private static IEnumerable<(string Name, RichCellOutput Output)> Outputs(Options options)
    {
        if (options.Value("file") is { } file)
        {
            yield return (Path.GetFileNameWithoutExtension(file), Read(file));
            yield break;
        }

        var words = Enumerable.Range(0, 16).Select(options.Positional).OfType<string>().ToList();
        var fixtures = Directory.GetFiles(FixtureFolder(), "*.json")
            .Where(path => words.Count == 0 || words.Any(w => Path.GetFileName(path).Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToList();
        if (fixtures.Count == 0) throw new ArgumentException($"No fixture in {FixtureFolder()} matches {string.Join(", ", words)}.");

        foreach (var fixture in fixtures)
        {
            yield return (Path.GetFileNameWithoutExtension(fixture), Read(fixture));
        }
    }

    // A display bundle ({"application/vnd.fry.chart.v1+json": {...}}) goes through the kernels' MIME mapper; a bare
    // spec is named for its family (chart-*.json, plot3d-*.json, visualizer-*.json), as the fixtures are.
    private static RichCellOutput Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any(p => p.Name.StartsWith("application/", StringComparison.Ordinal)))
        {
            using var metadata = JsonDocument.Parse("{}");
            return MimeOutputMapper.Map(root, metadata.RootElement).Rich ?? VisualOutputs.Error("the bundle has no visual in it");
        }

        var name = Path.GetFileName(path);
        VisualFamily? family = name.StartsWith("chart", StringComparison.OrdinalIgnoreCase) ? VisualFamily.Chart
            : name.StartsWith("plot3d", StringComparison.OrdinalIgnoreCase) ? VisualFamily.Plot3D
            : name.StartsWith("visualizer", StringComparison.OrdinalIgnoreCase) ? VisualFamily.Visualizer
            : null;
        return family is { } known
            ? VisualOutputs.FromJson(known, root)
            : VisualOutputs.Error("name a bare spec chart-….json, plot3d-….json or visualizer-….json, or wrap it in a display bundle");
    }

    private static string FixtureFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            var fixtures = Path.Combine(folder.FullName, "Tests", "Fixtures", "Visuals");
            if (File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.slnx")) && Directory.Exists(fixtures)) return fixtures;
        }

        throw new ArgumentException("Tests/Fixtures/Visuals wasn't found above the tool's folder; give a spec with --file.");
    }
}
