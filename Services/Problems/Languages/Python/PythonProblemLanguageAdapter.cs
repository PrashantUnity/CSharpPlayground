using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Python;

/// <summary>
/// Language adapter that formats algorithm and curriculum problems into runnable Python scripts,
/// Jupyter/polyglot notebooks, and check() test assertions.
/// </summary>
public class PythonProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static PythonProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Python;
    public string DisplayName => "Python 3.12";
    public string DefaultFileExtension => ".py";

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
            Title = $"{problem.Number:D2}. {problem.Title}.py",
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
            Title = $"{problem.Number}. {problem.Title} (Python Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void PythonCode(string source) => notebook.Cells.Add(new NotebookCellItem
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

        // 2. Setup (if python support code exists or common structures)
        if (NeedsLinkedListSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nListNode definition for linked list operations.");
            PythonCode(GetPythonLinkedListSupport());
        }
        else if (NeedsTreeSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nTreeNode definition for binary tree operations.");
            PythonCode(GetPythonTreeSupport());
        }

        // 3. Solution cell
        Markdown("### 💻 Solution\n\nImplement the solution below.");
        PythonCode(GetPythonStarterOrSolution(problem));

        // 4. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        PythonCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("# ── Verification Harness ──");
        code.AppendLine("""
        import json

        def check(name, actual, expected, any_order=False):
            expected_str = expected if isinstance(expected, str) else json.dumps(expected)
            if actual is None and expected_str == '[]':
                actual = []
            if hasattr(actual, 'val') and hasattr(actual, 'next') and 'listToValues' in globals():
                actual = listToValues(actual)
            actual_str = json.dumps(actual) if isinstance(actual, (list, dict, tuple)) else str(actual).lower() if isinstance(actual, bool) else str(actual)
            passed = False
            if any_order and isinstance(actual, list):
                try:
                    exp_list = json.loads(expected_str) if isinstance(expected_str, str) else expected_str
                    if isinstance(exp_list, list):
                        passed = sorted(str(x) for x in actual) == sorted(str(x) for x in exp_list)
                    else:
                        passed = actual_str == expected_str
                except Exception:
                    passed = (actual_str == expected_str)
            else:
                passed = (actual_str == expected_str.lower() if isinstance(actual, bool) else actual_str == expected_str)

            status = "✅" if passed else "❌"
            if passed:
                print(f"{status} {name} → {actual_str}")
            else:
                print(f"{status} {name} → got {actual_str} · expected {expected_str}")
            return passed

        sol = Solution()
        """);

        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToPython(test.Call);
            var expected = Quote(test.Expected);
            var orderArg = test.AnyOrder ? ", any_order=True" : "";
            code.AppendLine($"check({Quote(test.Name)}, {call}, {expected}{orderArg})");
        }

        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine($"# {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        code.AppendLine("# Run (F5): the tests print ✅/❌.");
        code.AppendLine();

        if (NeedsLinkedListSupport(problem))
        {
            code.AppendLine(GetPythonLinkedListSupport());
            code.AppendLine();
        }
        else if (NeedsTreeSupport(problem))
        {
            code.AppendLine(GetPythonTreeSupport());
            code.AppendLine();
        }

        code.AppendLine(GetPythonStarterOrSolution(problem));
        code.AppendLine();
        code.AppendLine(BuildTestCode(problem));

        return code.ToString();
    }

    private static string GetPythonStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Python, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        var methodInfo = ProblemCodeTranspiler.ExtractMethodInfo(problem.SolutionCode, problem.Title);
        var methodName = char.ToLowerInvariant(methodInfo.Name[0]) + methodInfo.Name[1..];
        var paramNames = methodInfo.Parameters.Count > 0
            ? ", " + string.Join(", ", methodInfo.Parameters.Select(p => $"{p.Name}: {MapTypeToPython(p.Type)}"))
            : "";

        var retType = MapTypeToPython(methodInfo.ReturnType);

        return $$"""
        class Solution:
            def {{methodName}}(self{{paramNames}}) -> {{retType}}:
                # TODO: Implement solution for {{problem.FullTitle}}
                pass
        """;
    }

    private static bool NeedsLinkedListSupport(IProblemItem problem) =>
        problem.Category.Equals("Linked List", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("ListNode", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsTreeSupport(IProblemItem problem) =>
        problem.Category.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("TreeNode", StringComparison.OrdinalIgnoreCase);

    private static string GetPythonLinkedListSupport() =>
        """
        class ListNode:
            def __init__(self, val=0, next=None):
                self.val = val
                self.next = next

        def buildList(*values):
            head = None
            for v in reversed(values):
                head = ListNode(v, head)
            return head

        def listToValues(node):
            res = []
            while node:
                res.append(node.val)
                node = node.next
            return res
        """;

    private static string GetPythonTreeSupport() =>
        """
        class TreeNode:
            def __init__(self, val=0, left=None, right=None):
                self.val = val
                self.left = left
                self.right = right

        def buildTree(*values):
            if not values or values[0] is None:
                return None
            root = TreeNode(values[0])
            from collections import deque
            queue = deque([root])
            i = 1
            while i < len(values):
                parent = queue.popleft()
                if i < len(values) and values[i] is not None:
                    parent.left = TreeNode(values[i])
                    queue.append(parent.left)
                i += 1
                if i < len(values) and values[i] is not None:
                    parent.right = TreeNode(values[i])
                    queue.append(parent.right)
                i += 1
            return root
        """;

    private static string MapTypeToPython(string csharpType) => csharpType.Trim() switch
    {
        "int" => "int",
        "int[]" => "list[int]",
        "int[][]" => "list[list[int]]",
        "string" => "str",
        "string[]" => "list[str]",
        "bool" => "bool",
        "double" => "float",
        "void" => "None",
        "IList<int>" or "List<int>" => "list[int]",
        "IList<string>" or "List<string>" => "list[str]",
        "IList<IList<string>>" or "List<List<string>>" => "list[list[str]]",
        "IList<IList<int>>" or "List<List<int>>" => "list[list[int]]",
        "ListNode" => "Optional[ListNode]",
        "TreeNode" => "Optional[TreeNode]",
        _ => "Any"
    };

    private static string Quote(string s) => $"\"{s.Replace("\"", "\\\"")}\"";
}

/// <summary>Backward-compatible alias for PythonProblemLanguageAdapter.</summary>
public sealed class PythonBlindAdapter : PythonProblemLanguageAdapter
{
    public static new PythonBlindAdapter Instance { get; } = new();
}
