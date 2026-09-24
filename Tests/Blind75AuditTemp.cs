using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

// TEMPORARY audit: runs every Blind 75 script and notebook through the real kernel and writes a report.
public class Blind75AuditTemp
{
    private const string ReportPath = "/private/tmp/claude-501/-Users-codefrydev-Desktop-SourceCode-PDFCreator-Fonts-examples-CSharpEditorPlugin/a89c7681-53f1-42bf-adc1-a30033c3cd3a/scratchpad/blind75_audit.tsv";

    private static async Task<(bool Ok, string Error, int Steps, string Kinds, string Console)> RunAsync(NotebookExecutionKernel kernel, string code)
    {
        var rich = new List<RichCellOutput>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await kernel.ExecuteCellAsync(code, onRichOutput: r => rich.Add(r), ct: cts.Token);
        int steps = rich.Where(r => r.VisualizerSequence != null).Select(r => r.VisualizerSequence!.TotalSteps).DefaultIfEmpty(0).Max();
        string kinds = string.Join(",", rich.Select(r => r.Kind + (r.VisualizerOptions != null ? ":" + r.VisualizerOptions.Kind : "")));
        string error = result.Success ? "" : (result.WasCancelled ? "TIMEOUT" : (result.ErrorMessage ?? "").Split('\n')[0]);
        if (!result.Success && result.Diagnostics.Count > 0)
            error = string.Join(" | ", result.Diagnostics.Take(2).Select(d => $"L{d.Line}: {d.Message}"));
        return (result.Success, error.Replace('\t', ' '), steps, kinds, result.ConsoleOutput.Replace('\n', '⏎').Replace('\t', ' '));
    }

    [Fact]
    public async Task Audit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Num\tTitle\tCategory\tVisKind\tCustomVis\tScriptOk\tScriptError\tSteps\tKinds\tNotebookFails\tApproaches\tThinking\tTests\tDescLen\tConsole");
        foreach (var p in Blind75CatalogService.GetAllProblems())
        {
            bool custom = p.VisualizationCode.Trim() != Blind75CurriculumEnhancer.GenerateDefaultVisualizerCode(p).Trim();

            var script = Blind75CatalogService.ConvertToScript(p);
            var s = await RunAsync(new NotebookExecutionKernel(), script.Code);

            var nb = Blind75CatalogService.ConvertToNotebook(p);
            var kernel = new NotebookExecutionKernel();
            var fails = new List<string>();
            int cellIndex = 0;
            foreach (var cell in nb.Cells)
            {
                cellIndex++;
                if (cell.Type != CellType.Code) continue;
                var r = await RunAsync(kernel, cell.Source);
                if (!r.Ok) fails.Add($"#{cellIndex}: {r.Error}");
            }

            sb.AppendLine(string.Join("\t", p.Number, p.Title, p.Category, p.VisualizerKind, custom, s.Ok, s.Error, s.Steps, s.Kinds,
                string.Join(" ;; ", fails), p.Approaches.Count, !string.IsNullOrWhiteSpace(p.ThinkingProcessMarkdown), p.TestCases.Count,
                p.DescriptionMarkdown.Length, s.Console.Length > 160 ? s.Console[..160] : s.Console));
        }
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
