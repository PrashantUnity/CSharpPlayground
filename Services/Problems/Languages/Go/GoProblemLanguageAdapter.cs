using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Go;

/// <summary>
/// Language adapter that formats curriculum and algorithm problems into runnable Go source files,
/// interactive polyglot notebooks, and check() test assertions.
/// </summary>
public class GoProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static GoProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Go;
    public string DisplayName => "Go (gc toolchain)";
    public string DefaultFileExtension => ".go";

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
            Title = $"{problem.Number:D2}. {problem.Title}.go",
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
            Title = $"{problem.Number}. {problem.Title} (Go Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void GoCode(string source) => notebook.Cells.Add(new NotebookCellItem
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
        Markdown("### 💻 Solution\n\nImplement the solution in Go below.");
        GoCode(GetGoStarterOrSolution(problem));

        // 3. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        GoCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("// ── Verification Harness ──");
        code.AppendLine("""
            func check(name string, actual, expected interface{}) {
            	actStr := fmt.Sprintf("%v", actual)
            	expStr := fmt.Sprintf("%v", expected)
            	if actStr == expStr {
            		fmt.Printf("✅ %s → %s\n", name, actStr)
            	} else {
            		fmt.Printf("❌ %s → got %s · expected %s\n", name, actStr, expStr)
            	}
            }
            """);

        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            code.AppendLine($"check({Quote(test.Name)}, {test.Call}, {Quote(test.Expected)})");
        }
        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        sb.AppendLine("// Run (F5): compiles with the Go toolchain (go build) and runs.");
        sb.AppendLine("package main");
        sb.AppendLine();
        sb.AppendLine("import (");
        sb.AppendLine("\t\"fmt\"");
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine(GetGoStarterOrSolution(problem));
        sb.AppendLine();

        sb.AppendLine("func check(name string, actual, expected interface{}) {");
        sb.AppendLine("\tactStr := fmt.Sprintf(\"%v\", actual)");
        sb.AppendLine("\texpStr := fmt.Sprintf(\"%v\", expected)");
        sb.AppendLine("\tif actStr == expStr {");
        sb.AppendLine("\t\tfmt.Printf(\"✅ %s → %s\\n\", name, actStr)");
        sb.AppendLine("\t} else {");
        sb.AppendLine("\t\tfmt.Printf(\"❌ %s → got %s · expected %s\\n\", name, actStr, expStr)");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("func main() {");
        foreach (var test in problem.Tests)
        {
            sb.AppendLine($"\tcheck({Quote(test.Name)}, \"{test.Expected}\", {Quote(test.Expected)})");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetGoStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Go, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        // Generic starter function
        var funcName = ToCamelCase(problem.Title);
        return $$"""
            func {{funcName}}(nums []int) int {
            	// TODO: implement {{problem.Title}}
            	return 0
            }
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
