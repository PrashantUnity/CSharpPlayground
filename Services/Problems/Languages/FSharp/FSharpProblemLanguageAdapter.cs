using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.FSharp;

/// <summary>
/// Language adapter that formats curriculum and algorithm problems into runnable F# script files (.fsx),
/// interactive polyglot notebooks, and check() test assertions.
/// </summary>
public class FSharpProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static FSharpProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.FSharp;
    public string DisplayName => "F# (dotnet fsi)";
    public string DefaultFileExtension => ".fsx";

    public ScriptDocumentItem BuildScript(IProblemItem problem)
    {
        var notes = new StringBuilder();
        notes.Append($"# {problem.FullTitle}\n\n**{problem.DifficultyBadgeText}** · {problem.Category}\n\n{problem.DescriptionMarkdown.Trim()}");
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            notes.Append($"\n\n## How to think\n\n{problem.ThinkingProcessMarkdown.Trim()}");
        }

        return new ScriptDocumentItem
        {
            Title = $"{problem.Number:D2}. {problem.Title}.fsx",
            LanguageId = LanguageId,
            Code = BuildScriptCode(problem),
            Notes = notes.ToString(),
            TestCases = problem.TestCases.Select(t => new TestCaseItem
            {
                Name = t.Name,
                Input = t.Input,
                ExpectedOutput = t.ExpectedOutput
            }).ToList()
        };
    }

    public NotebookDocumentItem BuildNotebook(IProblemItem problem)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title} (F# Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void FSharpCode(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Language = LanguageId,
            Source = source.Trim()
        });

        // 1. Problem Statement
        Markdown($"# {problem.FullTitle}\n\n**Category:** `{problem.Category}` · **Difficulty:** `{problem.DifficultyBadgeText}` · **Acceptance:** `{problem.AcceptanceRateText}`\n\n{problem.DescriptionMarkdown.Trim()}");
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            Markdown($"## 🧠 How to think\n\n{problem.ThinkingProcessMarkdown.Trim()}");
        }

        // 2. Solution cell
        Markdown("### 💻 Solution\n\nImplement the functional F# solution below.");
        FSharpCode(GetFSharpStarterOrSolution(problem));

        // 3. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        FSharpCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("// ── Verification Harness ──");
        code.AppendLine("""
            let check (name: string) (actual: obj) (expected: obj) =
                let actStr = sprintf "%A" actual
                let expStr = sprintf "%A" expected
                if actStr = expStr then
                    printfn "✅ %s → %s" name actStr
                else
                    printfn "❌ %s → got %s · expected %s" name actStr expStr
            """);

        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            code.AppendLine($"check {Quote(test.Name)} {Quote(test.Expected)} {Quote(test.Expected)}");
        }
        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        sb.AppendLine("// Run (F5): executes with F# Interactive (dotnet fsi).");
        sb.AppendLine("open System");
        sb.AppendLine();

        sb.AppendLine(GetFSharpStarterOrSolution(problem));
        sb.AppendLine();

        sb.AppendLine("""
            let check (name: string) (actual: obj) (expected: obj) =
                let actStr = sprintf "%A" actual
                let expStr = sprintf "%A" expected
                if actStr = expStr then
                    printfn "✅ %s → %s" name actStr
                else
                    printfn "❌ %s → got %s · expected %s" name actStr expStr
            """);
        sb.AppendLine();

        foreach (var test in problem.Tests)
        {
            sb.AppendLine($"check {Quote(test.Name)} {Quote(test.Expected)} {Quote(test.Expected)}");
        }

        return sb.ToString();
    }

    private static string GetFSharpStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.FSharp, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        var funcName = ToCamelCase(problem.Title);
        return $$"""
            let {{funcName}} (nums: int list) : int =
                // TODO: implement {{problem.Title}} in F#
                0
            """;
    }

    private static string ToCamelCase(string title)
    {
        var words = title.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "solve";
        var sb = new StringBuilder(words[0].ToLowerInvariant());
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                sb.Append(char.ToUpperInvariant(words[i][0]));
                if (words[i].Length > 1) sb.Append(words[i][1..].ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    private static string Quote(string text) => "\"" + text.Replace("\"", "\\\"") + "\"";
}
