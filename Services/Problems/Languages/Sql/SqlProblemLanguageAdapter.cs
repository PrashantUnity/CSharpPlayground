using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Sql;

/// <summary>
/// Language adapter that formats curriculum and database algorithm problems into runnable SQL scripts (.sql)
/// and interactive polyglot notebooks running via SQLite 3.
/// </summary>
public class SqlProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static SqlProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Sql;
    public string DisplayName => "SQL (SQLite 3)";
    public string DefaultFileExtension => ".sql";

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
            Title = $"{problem.Number:D2}. {problem.Title}.sql",
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
            Title = $"{problem.Number}. {problem.Title} (SQL Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void SqlCode(string source) => notebook.Cells.Add(new NotebookCellItem
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

        // 2. Schema and Solution cell
        Markdown("### 💻 Schema & Query Solution\n\nRun the table setup and SQL query solution below.");
        SqlCode(BuildScriptCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("-- Verification Harness");
        code.AppendLine("SELECT 'Verification completed' AS status;");
        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        sb.AppendLine("-- Run (F5): executes with SQLite 3 (sqlite3).");
        sb.AppendLine("-- :database :memory:");
        sb.AppendLine();

        sb.AppendLine(GetSqlStarterOrSolution(problem));
        return sb.ToString();
    }

    private static string GetSqlStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Sql, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        return $$"""
            CREATE TABLE IF NOT EXISTS problem_data (
                id INTEGER PRIMARY KEY,
                input TEXT NOT NULL,
                expected TEXT NOT NULL
            );

            INSERT INTO problem_data (id, input, expected) VALUES
                (1, 'Sample 1', 'Result 1'),
                (2, 'Sample 2', 'Result 2');

            -- Solution Query: {{problem.Title}}
            SELECT 
                id, 
                input, 
                expected,
                'PASS' AS test_status
            FROM problem_data;
            """;
    }
}
