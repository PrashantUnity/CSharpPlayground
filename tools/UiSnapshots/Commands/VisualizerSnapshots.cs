using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary><c>visualizer n ...</c>: runs Blind 75 scripts and saves steps of the step-by-step visualizers they display.</summary>
internal static class VisualizerSnapshots
{
    public static void Frames(Options options)
    {
        bool quiet = options.Flag("quiet");
        foreach (int number in options.Problems())
        {
            var problem = Blind75CatalogService.GetProblemByNumber(number)!;
            var outputs = new List<RichCellOutput>();
            var kernel = new NotebookExecutionKernel();
            string code = Blind75CatalogService.ConvertToScript(problem).Code;

            // The kernel builds the visualizer controls through Dispatcher.UIThread.Invoke, so the script runs on a worker
            // thread while this thread keeps the UI queue moving; running it here would block on itself.
            var result = Snapshot.Wait(Task.Run(() => kernel.ExecuteCellAsync(code, onRichOutput: outputs.Add)));
            Console.WriteLine($"==== {problem.FullTitle}: {(result.Success ? "ran" : "FAILED " + result.ErrorMessage)}");
            Console.WriteLine(result.ConsoleOutput.Trim());

            int k = 0;
            foreach (var visualizer in outputs.Where(o => o.Kind == CellOutputKind.Visualizer).Select(o => o.VisualizerOptions).OfType<VisualizerOptions>())
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
