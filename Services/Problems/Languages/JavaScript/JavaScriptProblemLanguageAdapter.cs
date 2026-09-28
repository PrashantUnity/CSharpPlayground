using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.JavaScript;

/// <summary>
/// Language adapter that formats algorithm and curriculum problems into runnable Node.js JavaScript scripts,
/// interactive polyglot notebooks, and check() test runners.
/// </summary>
public class JavaScriptProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static JavaScriptProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.JavaScript;
    public string DisplayName => "JavaScript (Node.js)";
    public string DefaultFileExtension => ".js";

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
            Title = $"{problem.Number:D2}. {problem.Title}.js",
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
            Title = $"{problem.Number}. {problem.Title} (JavaScript Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void JsCode(string source) => notebook.Cells.Add(new NotebookCellItem
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

        // 2. Setup (if ListNode/TreeNode needed)
        if (NeedsLinkedListSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nListNode definition for linked list operations.");
            JsCode(GetJavaScriptLinkedListSupport());
        }
        else if (NeedsTreeSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nTreeNode definition for binary tree operations.");
            JsCode(GetJavaScriptTreeSupport());
        }

        // 3. Solution cell
        Markdown("### 💻 Solution\n\nImplement the solution below.");
        JsCode(GetJavaScriptStarterOrSolution(problem));

        // 4. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        JsCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("// ── Verification Harness ──");
        code.AppendLine("""
        function check(name, actual, expected, anyOrder = false) {
            const expectedStr = typeof expected === 'string' ? expected : JSON.stringify(expected);
            if ((actual === null || actual === undefined) && (expectedStr === '[]' || expected === '[]')) {
                actual = [];
            }
            if (actual && typeof actual === 'object' && 'val' in actual && 'next' in actual && typeof listToValues === 'function') {
                actual = listToValues(actual);
            }
            const actualStr = JSON.stringify(actual);
            let passed = false;
            if (anyOrder && Array.isArray(actual)) {
                try {
                    const expArr = typeof expected === 'string' ? JSON.parse(expected) : expected;
                    if (Array.isArray(expArr)) {
                        const s1 = [...actual].sort();
                        const s2 = [...expArr].sort();
                        passed = JSON.stringify(s1) === JSON.stringify(s2);
                    }
                } catch {
                    passed = (actualStr === expectedStr);
                }
            } else {
                passed = (actualStr === expectedStr || actual == expected);
            }
            const mark = passed ? "✅" : "❌";
            console.log(passed ? `${mark} ${name} → ${actualStr}` : `${mark} ${name} → got ${actualStr} · expected ${expectedStr}`);
            return passed;
        }

        const sol = new Solution();
        """);

        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToJavaScript(test.Call);
            var expected = QuoteOrRaw(test.Expected);
            var order = test.AnyOrder ? ", true" : "";
            code.AppendLine($"check({Quote(test.Name)}, {call}, {expected}{order});");
        }

        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        code.AppendLine("// Run (F5): the tests print ✅/❌.");
        code.AppendLine();

        if (NeedsLinkedListSupport(problem))
        {
            code.AppendLine(GetJavaScriptLinkedListSupport());
            code.AppendLine();
        }
        else if (NeedsTreeSupport(problem))
        {
            code.AppendLine(GetJavaScriptTreeSupport());
            code.AppendLine();
        }

        code.AppendLine(GetJavaScriptStarterOrSolution(problem));
        code.AppendLine();
        code.AppendLine(BuildTestCode(problem));

        return code.ToString();
    }

    private static string GetJavaScriptStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.JavaScript, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        var methodInfo = ProblemCodeTranspiler.ExtractMethodInfo(problem.SolutionCode, problem.Title);
        var methodName = char.ToLowerInvariant(methodInfo.Name[0]) + methodInfo.Name[1..];
        var paramNames = string.Join(", ", methodInfo.Parameters.Select(p => p.Name));

        return $$"""
        class Solution {
            /**
             * Solution for {{problem.FullTitle}}
             */
            {{methodName}}({{paramNames}}) {
                // TODO: Implement solution
            }
        }
        """;
    }

    private static bool NeedsLinkedListSupport(IProblemItem problem) =>
        problem.Category.Equals("Linked List", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("ListNode", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsTreeSupport(IProblemItem problem) =>
        problem.Category.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("TreeNode", StringComparison.OrdinalIgnoreCase);

    private static string GetJavaScriptLinkedListSupport() =>
        """
        class ListNode {
            constructor(val = 0, next = null) {
                this.val = val;
                this.next = next;
            }
        }

        function buildList(...values) {
            let head = null;
            for (let i = values.length - 1; i >= 0; i--) {
                head = new ListNode(values[i], head);
            }
            return head;
        }

        function listToValues(head) {
            const res = [];
            let curr = head;
            while (curr) {
                res.push(curr.val);
                curr = curr.next;
            }
            return res;
        }
        """;

    private static string GetJavaScriptTreeSupport() =>
        """
        class TreeNode {
            constructor(val = 0, left = null, right = null) {
                this.val = val;
                this.left = left;
                this.right = right;
            }
        }

        function buildTree(...values) {
            if (!values.length || values[0] === null || values[0] === undefined) return null;
            const root = new TreeNode(values[0]);
            const queue = [root];
            let i = 1;
            while (i < values.length && queue.length) {
                const parent = queue.shift();
                if (i < values.length && values[i] !== null && values[i] !== undefined) {
                    parent.left = new TreeNode(values[i]);
                    queue.push(parent.left);
                }
                i++;
                if (i < values.length && values[i] !== null && values[i] !== undefined) {
                    parent.right = new TreeNode(values[i]);
                    queue.push(parent.right);
                }
                i++;
            }
            return root;
        }
        """;

    private static string Quote(string s) => $"\"{s.Replace("\"", "\\\"")}\"";

    private static string QuoteOrRaw(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "\"\"";
        s = s.Trim();
        if (s.StartsWith("[") || s.StartsWith("{") || s == "true" || s == "false" || s == "null" || double.TryParse(s, out _))
        {
            return Quote(s);
        }
        return Quote(s);
    }
}

/// <summary>Backward-compatible alias for JavaScriptProblemLanguageAdapter.</summary>
public sealed class JavaScriptBlindAdapter : JavaScriptProblemLanguageAdapter
{
    public static new JavaScriptBlindAdapter Instance { get; } = new();
}
