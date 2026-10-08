using System.IO;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary><c>visualizer n ...</c>: runs Blind 75 scripts and saves steps of the step-by-step visualizers they display.</summary>
internal static class VisualizerSnapshots
{
    public static void Frames(Options options)
    {
        bool quiet = options.Flag("quiet");
        if (options.Value("template") is { } templateQuery)
        {
            var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t =>
                t.Id.Contains(templateQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Title.Contains(templateQuery, StringComparison.OrdinalIgnoreCase));
            if (template is null)
                throw new ArgumentException($"No template found matching '{templateQuery}'.");

            RenderTemplate(template, options, quiet);
            return;
        }

        foreach (int number in options.Problems())
        {
            var problem = Blind75CatalogService.GetProblemByNumber(number)!;
            var outputs = new List<RichCellOutput>();
            var kernel = new NotebookExecutionKernel();
            string code = Blind75CatalogService.ConvertToScript(problem).Code;

            // The script runs on a worker thread, as it does in the studio, while this thread keeps the UI queue moving.
            var result = Snapshot.Wait(Task.Run(() => kernel.ExecuteCellAsync(code, onRichOutput: outputs.Add)));
            Console.WriteLine($"==== {problem.FullTitle}: {(result.Success ? "ran" : "FAILED " + result.ErrorMessage)}");
            Console.WriteLine(result.ConsoleOutput.Trim());

            // Each visualizer is drawn from its spec, as the studio's views draw it.
            int k = 0;
            var visualizers = outputs
                .Where(o => o.Visual?.Spec is VisualizerSpec)
                .Select(o => VisualizerRenderModelBuilder.Build((VisualizerSpec)o.Visual!.Spec));
            foreach (var visualizer in visualizers)
            {
                k++;
                var sequence = visualizer.Sequence;
                int total = sequence?.TotalSteps ?? 1;
                Console.WriteLine($"-- visualizer {k}: {visualizer.Kind}, {total} steps, \"{visualizer.Title}\"");
                if (sequence != null && !quiet)
                {
                    for (int i = 0; i < total; i++)
                    {
                        Console.WriteLine($"   {i,3} [Ln {sequence.Steps[i].SourceLine,3}] {sequence.Steps[i].Description}");
                    }
                }

                foreach (int step in Steps(options, total))
                {
                    sequence?.SeekStep(step);
                    var content = new ScrollViewer { Content = new Border { Padding = new Thickness(10), Child = new InteractiveVisualizerControl(visualizer) } };
                    var window = Snapshot.Show(content, options.Int("width", 900), options.Int("height", 640));
                    Snapshot.Save(window, $"p{number}_v{k}_s{step}");
                    window.Close();
                }

                if (options.Flag("gif") && sequence != null && sequence.HasSteps)
                {
                    var gifOptions = new VisualizerGifExportOptions
                    {
                        Width = options.Int("width", 640),
                        Height = options.Int("height", 360),
                        StepDelay = options.Int("delay", 0) > 0 ? TimeSpan.FromMilliseconds(options.Int("delay", 300)) : null,
                        IncludeBanner = !options.Flag("no-banner")
                    };
                    string gifName = $"p{number}_v{k}.gif";
                    string gifPath = Path.Combine(Snapshot.OutputFolder, gifName);
                    var bytes = Snapshot.Wait(VisualizerGifExportService.ExportToGifBytesAsync(visualizer, gifOptions));
                    File.WriteAllBytes(gifPath, bytes);
                    Console.WriteLine($"   Saved GIF -> {gifPath} ({bytes.Length / 1024} KB)");
                }
            }
        }
    }

    private static void RenderTemplate(PdfEditorApp.Plugins.CSharpEditor.Models.CodeTemplate template, Options options, bool quiet)
    {
        var outputs = new List<RichCellOutput>();
        var kernel = new NotebookExecutionKernel();
        var codeSnippets = template.Cells != null && template.Cells.Count > 0
            ? template.Cells.Where(c => c.Type == CellType.Code).Select(c => c.Source).ToList()
            : new List<string> { template.InitialCode };

        Console.WriteLine($"==== Template: {template.Title} ({codeSnippets.Count} code cells)");
        foreach (var code in codeSnippets)
        {
            var result = Snapshot.Wait(Task.Run(() => kernel.ExecuteCellAsync(code, onRichOutput: outputs.Add)));
            if (!result.Success)
            {
                Console.WriteLine($"Cell failed: {result.ErrorMessage}");
            }
            if (!string.IsNullOrWhiteSpace(result.ConsoleOutput))
            {
                Console.WriteLine(result.ConsoleOutput.Trim());
            }
        }

        int k = 0;
        var visualizers = outputs
            .Where(o => o.Visual?.Spec is VisualizerSpec)
            .Select(o => VisualizerRenderModelBuilder.Build((VisualizerSpec)o.Visual!.Spec));
        int targetViz = options.Int("viz", options.Int("visualizer", 0));
        foreach (var visualizer in visualizers)
        {
            k++;
            if (targetViz > 0 && k != targetViz) continue;
            var sequence = visualizer.Sequence;
            int total = sequence?.TotalSteps ?? 1;
            Console.WriteLine($"-- visualizer {k}: {visualizer.Kind}, {total} steps, \"{visualizer.Title}\"");
            if (sequence != null && !quiet)
            {
                for (int i = 0; i < total; i++)
                {
                    Console.WriteLine($"   {i,3} [Ln {sequence.Steps[i].SourceLine,3}] {sequence.Steps[i].Description}");
                }
            }

            foreach (int step in Steps(options, total))
            {
                sequence?.SeekStep(step);
                var content = new ScrollViewer { Content = new Border { Padding = new Thickness(10), Child = new InteractiveVisualizerControl(visualizer) } };
                var window = Snapshot.Show(content, options.Int("width", 900), options.Int("height", 640));
                string name = options.Value("name") is { } customName && !options.List("steps").Any()
                    ? customName
                    : $"{template.Id}_v{k}_s{step}";
                Snapshot.Save(window, name);
                window.Close();
            }

            if (options.Flag("gif") && sequence != null && sequence.HasSteps)
            {
                var gifOptions = new VisualizerGifExportOptions
                {
                    Width = options.Int("width", 640),
                    Height = options.Int("height", 360),
                    StepDelay = options.Int("delay", 0) > 0 ? TimeSpan.FromMilliseconds(options.Int("delay", 300)) : null,
                    IncludeBanner = !options.Flag("no-banner")
                };
                string gifName = options.Value("name") is { } customName && !options.List("steps").Any()
                    ? (customName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? customName : $"{customName}.gif")
                    : $"{template.Id}_v{k}.gif";
                string gifPath = Path.Combine(Snapshot.OutputFolder, gifName);
                var bytes = Snapshot.Wait(VisualizerGifExportService.ExportToGifBytesAsync(visualizer, gifOptions));
                File.WriteAllBytes(gifPath, bytes);
                Console.WriteLine($"   Saved GIF -> {gifPath} ({bytes.Length / 1024} KB)");
            }
        }
    }

    // --steps 0,5,-1 (negative counts from the end); by default the first, a third of the way, two thirds and the last.
    private static SortedSet<int> Steps(Options options, int total)
    {
        var steps = new SortedSet<int>();
        var requested = options.List("steps").ToList();
        if (requested.Count == 0)
        {
            steps.UnionWith(new[] { 0, total / 3, 2 * total / 3, total - 1 });
            return steps;
        }
        foreach (string text in requested)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
            {
                throw new ArgumentException($"--steps wants whole numbers, like 0,5,-1; not '{text}'.");
            }
            steps.Add(Math.Clamp(step < 0 ? total + step : step, 0, total - 1));
        }
        return steps;
    }
}
