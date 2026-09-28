using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Java;

/// <summary>
/// Language adapter that formats algorithm and curriculum problems into runnable Java classes,
/// JShell polyglot notebooks, and check() test assertions.
/// </summary>
public class JavaProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static JavaProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Java;
    public string DisplayName => "Java (OpenJDK)";
    public string DefaultFileExtension => ".java";

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
            Title = $"{problem.Number:D2}. {problem.Title}.java",
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
            Title = $"{problem.Number}. {problem.Title} (Java Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void JavaCode(string source) => notebook.Cells.Add(new NotebookCellItem
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

        // 2. Setup
        if (NeedsLinkedListSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nListNode definition for linked list operations.");
            JavaCode(GetJavaLinkedListSupport());
        }
        else if (NeedsTreeSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nTreeNode definition for binary tree operations.");
            JavaCode(GetJavaTreeSupport());
        }

        // 3. Solution cell
        Markdown("### 💻 Solution\n\nImplement the solution below.");
        JavaCode(GetJavaStarterOrSolution(problem, isNotebook: true));

        // 4. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        JavaCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("Solution sol = new Solution();");
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToJava(test.Call);
            var expected = Quote(test.Expected);
            var order = test.AnyOrder ? ", true" : ", false";
            code.AppendLine($"check({Quote(test.Name)}, {call}, {expected}{order});");
        }
        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        sb.AppendLine("// Run (F5): compiles with javac and executes main().");
        sb.AppendLine("import java.util.*;");
        sb.AppendLine();

        if (NeedsLinkedListSupport(problem))
        {
            sb.AppendLine(GetJavaLinkedListSupport());
            sb.AppendLine();
        }
        else if (NeedsTreeSupport(problem))
        {
            sb.AppendLine(GetJavaTreeSupport());
            sb.AppendLine();
        }

        sb.AppendLine("public class Solution {");
        sb.AppendLine();
        sb.AppendLine(Indent(GetJavaSolutionBody(problem), 4));
        sb.AppendLine();
        sb.AppendLine(Indent(GetJavaHarnessCode(problem), 4));
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetJavaStarterOrSolution(IProblemItem problem, bool isNotebook)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Java, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        var methodInfo = ProblemCodeTranspiler.ExtractMethodInfo(problem.SolutionCode, problem.Title);
        var methodName = char.ToLowerInvariant(methodInfo.Name[0]) + methodInfo.Name[1..];
        var javaRet = MapTypeToJava(methodInfo.ReturnType);
        var javaParams = string.Join(", ", methodInfo.Parameters.Select(p => $"{MapTypeToJava(p.Type)} {p.Name}"));

        return $$"""
        public class Solution {
            public {{javaRet}} {{methodName}}({{javaParams}}) {
                // TODO: Implement solution for {{problem.FullTitle}}
                {{GetDefaultReturn(javaRet)}}
            }
        }
        """;
    }

    private static string GetJavaSolutionBody(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Java, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            var code = bundle.SolutionCode.Trim();
            // If the bundle already wrapped in class Solution, unwrap body
            if (code.Contains("class Solution"))
            {
                int firstBrace = code.IndexOf('{');
                int lastBrace = code.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    return code.Substring(firstBrace + 1, lastBrace - firstBrace - 1).Trim();
                }
            }
            return code;
        }

        var methodInfo = ProblemCodeTranspiler.ExtractMethodInfo(problem.SolutionCode, problem.Title);
        var methodName = char.ToLowerInvariant(methodInfo.Name[0]) + methodInfo.Name[1..];
        var javaRet = MapTypeToJava(methodInfo.ReturnType);
        var javaParams = string.Join(", ", methodInfo.Parameters.Select(p => $"{MapTypeToJava(p.Type)} {p.Name}"));

        return $$"""
        public {{javaRet}} {{methodName}}({{javaParams}}) {
            // TODO: Implement solution for {{problem.FullTitle}}
            {{GetDefaultReturn(javaRet)}}
        }
        """;
    }

    private string GetJavaHarnessCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
        // ── Test Verification Harness ──
        public static boolean check(String name, Object actual, String expected, boolean anyOrder) {
            String actualStr = format(actual);
            if (actual == null && "[]".equals(expected)) {
                actualStr = "[]";
            }
            boolean passed;
            if (anyOrder && actualStr.startsWith("[") && actualStr.endsWith("]")) {
                passed = canonical(actualStr).equals(canonical(expected));
            } else {
                passed = actualStr.equals(expected);
            }
            String mark = passed ? "✅" : "❌";
            if (passed) {
                System.out.println(mark + " " + name + " → " + actualStr);
            } else {
                System.out.println(mark + " " + name + " → got " + actualStr + " · expected " + expected);
            }
            return passed;
        }

        private static String canonical(String s) {
            if (s == null) return "";
            s = s.trim();
            if (s.startsWith("[") && s.endsWith("]")) {
                String inner = s.substring(1, s.length() - 1);
                String[] parts = inner.split(",");
                for (int i = 0; i < parts.length; i++) parts[i] = parts[i].trim();
                Arrays.sort(parts);
                return Arrays.toString(parts);
            }
            return s;
        }

        private static String format(Object obj) {
            if (obj == null) return "null";
            if (obj instanceof int[] a) return Arrays.toString(a);
            if (obj instanceof long[] a) return Arrays.toString(a);
            if (obj instanceof boolean[] a) return Arrays.toString(a);
            if (obj instanceof Object[] a) return Arrays.deepToString(a);
            if (obj instanceof Collection<?> c) return c.toString().replace(", ", ",");
            return String.valueOf(obj);
        }
        """);

        sb.AppendLine("public static void main(String[] args) {");
        sb.AppendLine("    Solution sol = new Solution();");
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToJava(test.Call);
            var expected = Quote(test.Expected);
            var order = test.AnyOrder ? ", true" : ", false";
            sb.AppendLine($"    check({Quote(test.Name)}, {call}, {expected}{order});");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool NeedsLinkedListSupport(IProblemItem problem) =>
        problem.Category.Equals("Linked List", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("ListNode", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsTreeSupport(IProblemItem problem) =>
        problem.Category.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("TreeNode", StringComparison.OrdinalIgnoreCase);

    private static string GetJavaLinkedListSupport() =>
        """
        class ListNode {
            public int val;
            public ListNode next;
            public ListNode(int val) { this.val = val; }
            public ListNode(int val, ListNode next) { this.val = val; this.next = next; }

            public static ListNode buildList(int... values) {
                ListNode head = null;
                for (int i = values.length - 1; i >= 0; i--) {
                    head = new ListNode(values[i], head);
                }
                return head;
            }

            @Override
            public String toString() {
                StringBuilder sb = new StringBuilder("[");
                ListNode curr = this;
                while (curr != null) {
                    sb.append(curr.val);
                    if (curr.next != null) sb.append(",");
                    curr = curr.next;
                }
                return sb.append("]").toString();
            }
        }
        """;

    private static string GetJavaTreeSupport() =>
        """
        class TreeNode {
            public int val;
            public TreeNode left;
            public TreeNode right;
            public TreeNode(int val) { this.val = val; }
            public TreeNode(int val, TreeNode left, TreeNode right) {
                this.val = val;
                this.left = left;
                this.right = right;
            }

            public static TreeNode buildTree(Integer... values) {
                if (values == null || values.length == 0 || values[0] == null) return null;
                TreeNode root = new TreeNode(values[0]);
                Queue<TreeNode> queue = new LinkedList<>();
                queue.offer(root);
                int i = 1;
                while (i < values.length && !queue.isEmpty()) {
                    TreeNode parent = queue.poll();
                    if (i < values.length && values[i] != null) {
                        parent.left = new TreeNode(values[i]);
                        queue.offer(parent.left);
                    }
                    i++;
                    if (i < values.length && values[i] != null) {
                        parent.right = new TreeNode(values[i]);
                        queue.offer(parent.right);
                    }
                    i++;
                }
                return root;
            }
        }
        """;

    private static string MapTypeToJava(string csharpType) => csharpType.Trim() switch
    {
        "int" => "int",
        "int[]" => "int[]",
        "int[][]" => "int[][]",
        "string" => "String",
        "string[]" => "String[]",
        "bool" => "boolean",
        "bool[]" => "boolean[]",
        "double" => "double",
        "char" => "char",
        "char[]" => "char[]",
        "IList<int>" or "List<int>" => "List<Integer>",
        "IList<string>" or "List<string>" => "List<String>",
        "IList<IList<string>>" or "List<List<string>>" => "List<List<String>>",
        "IList<IList<int>>" or "List<List<int>>" => "List<List<Integer>>",
        "ListNode" => "ListNode",
        "TreeNode" => "TreeNode",
        _ => csharpType
    };

    private static string GetDefaultReturn(string javaType) => javaType switch
    {
        "int" or "long" or "short" or "byte" => "return 0;",
        "double" or "float" => "return 0.0;",
        "boolean" => "return false;",
        "void" => "",
        "int[]" => "return new int[0];",
        "int[][]" => "return new int[0][0];",
        "String" => "return \"\";",
        _ => "return null;"
    };

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Split('\n');
        return string.Join('\n', lines.Select(l => string.IsNullOrWhiteSpace(l) ? l : pad + l));
    }

    private static string Quote(string s) => $"\"{s.Replace("\"", "\\\"")}\"";
}

/// <summary>Backward-compatible alias for JavaProblemLanguageAdapter.</summary>
public sealed class JavaBlindAdapter : JavaProblemLanguageAdapter
{
    public static new JavaBlindAdapter Instance { get; } = new();
}
