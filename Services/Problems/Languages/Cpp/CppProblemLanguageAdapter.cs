using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Cpp;

/// <summary>
/// Language adapter that formats algorithm and curriculum problems into runnable C++ classes,
/// interactive notebooks, and check() test assertions.
/// </summary>
public class CppProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static CppProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Cpp;
    public string DisplayName => "C++ (Clang / GCC / MSVC)";
    public string DefaultFileExtension => ".cpp";

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
            Title = $"{problem.Number:D2}. {problem.Title}.cpp",
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
            Title = $"{problem.Number}. {problem.Title} (C++ Notebook)"
        };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void CppCode(string source) => notebook.Cells.Add(new NotebookCellItem
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
            CppCode(GetCppLinkedListSupport());
        }
        else if (NeedsTreeSupport(problem))
        {
            Markdown("### 🧰 Setup\n\nTreeNode definition for binary tree operations.");
            CppCode(GetCppTreeSupport());
        }

        // 3. Solution cell
        Markdown("### 💻 Solution\n\nImplement the solution below.");
        CppCode(GetCppStarterOrSolution(problem));

        // 4. Tests cell
        Markdown("### 🧪 Tests\n\nRun these assertions to verify your implementation.");
        CppCode(BuildTestCode(problem));

        return notebook;
    }

    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine("Solution sol;");
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToCpp(test.Call);
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
        sb.AppendLine("// Run (F5): compiles with clang++/g++ and executes main().");
        sb.AppendLine("#include <iostream>");
        sb.AppendLine("#include <vector>");
        sb.AppendLine("#include <string>");
        sb.AppendLine("#include <unordered_map>");
        sb.AppendLine("#include <unordered_set>");
        sb.AppendLine("#include <algorithm>");
        sb.AppendLine("#include <queue>");
        sb.AppendLine("#include <stack>");
        sb.AppendLine("#include <sstream>");
        sb.AppendLine();
        sb.AppendLine("using namespace std;");
        sb.AppendLine();

        if (NeedsLinkedListSupport(problem))
        {
            sb.AppendLine(GetCppLinkedListSupport());
            sb.AppendLine();
        }
        else if (NeedsTreeSupport(problem))
        {
            sb.AppendLine(GetCppTreeSupport());
            sb.AppendLine();
        }

        sb.AppendLine("class Solution {");
        sb.AppendLine("public:");
        sb.AppendLine(Indent(GetCppSolutionBody(problem), 4));
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine(GetCppHarnessCode(problem));

        return sb.ToString();
    }

    private static string GetCppStarterOrSolution(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Cpp, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        var methodInfo = ProblemCodeTranspiler.ExtractMethodInfo(problem.SolutionCode, problem.Title);
        var methodName = char.ToLowerInvariant(methodInfo.Name[0]) + methodInfo.Name[1..];
        var cppRet = MapTypeToCpp(methodInfo.ReturnType);
        var cppParams = string.Join(", ", methodInfo.Parameters.Select(p => $"{MapTypeToCpp(p.Type)} {p.Name}"));

        return $$"""
        class Solution {
        public:
            {{cppRet}} {{methodName}}({{cppParams}}) {
                // TODO: Implement solution for {{problem.FullTitle}}
                {{GetDefaultReturn(cppRet)}}
            }
        };
        """;
    }

    private static string GetCppSolutionBody(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Cpp, out var bundle) &&
            !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            var code = bundle.SolutionCode.Trim();
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
        var cppRet = MapTypeToCpp(methodInfo.ReturnType);
        var cppParams = string.Join(", ", methodInfo.Parameters.Select(p => $"{MapTypeToCpp(p.Type)} {p.Name}"));

        return $$"""
        {{cppRet}} {{methodName}}({{cppParams}}) {
            // TODO: Implement solution for {{problem.FullTitle}}
            {{GetDefaultReturn(cppRet)}}
        }
        """;
    }

    private string GetCppHarnessCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
        // ── Test Verification Harness ──
        template<typename T>
        string formatVal(const T& val) {
            ostringstream oss;
            oss << val;
            return oss.str();
        }

        inline string formatVal(bool val) { return val ? "true" : "false"; }
        inline string formatVal(const string& val) { return val; }

        template<typename T>
        string formatVal(const vector<T>& vec) {
            ostringstream oss;
            oss << "[";
            for (size_t i = 0; i < vec.size(); ++i) {
                if (i > 0) oss << ",";
                oss << formatVal(vec[i]);
            }
            oss << "]";
            return oss.str();
        }

        inline string canonical(string s) {
            if (s.size() >= 2 && s.front() == '[' && s.back() == ']') {
                string inner = s.substr(1, s.size() - 2);
                vector<string> parts;
                string cur;
                for (char c : inner) {
                    if (c == ',') {
                        parts.push_back(cur);
                        cur.clear();
                    } else if (c != ' ') {
                        cur += c;
                    }
                }
                if (!cur.empty()) parts.push_back(cur);
                sort(parts.begin(), parts.end());
                ostringstream oss;
                oss << "[";
                for (size_t i = 0; i < parts.size(); ++i) {
                    if (i > 0) oss << ",";
                    oss << parts[i];
                }
                oss << "]";
                return oss.str();
            }
            return s;
        }

        template<typename T>
        bool check(const string& name, const T& actual, const string& expected, bool anyOrder = false) {
            string actualStr = formatVal(actual);
            bool passed = false;
            if (anyOrder && actualStr.front() == '[' && actualStr.back() == ']') {
                passed = (canonical(actualStr) == canonical(expected));
            } else {
                passed = (actualStr == expected);
            }

            string mark = passed ? "✅" : "❌";
            if (passed) {
                cout << mark << " " << name << " → " << actualStr << endl;
            } else {
                cout << mark << " " << name << " → got " << actualStr << " · expected " << expected << endl;
            }
            return passed;
        }
        """);

        sb.AppendLine("int main() {");
        sb.AppendLine("    Solution sol;");
        sb.AppendLine("    bool allPassed = true;");
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            var call = ProblemCodeTranspiler.TranspileCallToCpp(test.Call);
            var expected = Quote(test.Expected);
            var order = test.AnyOrder ? ", true" : ", false";
            sb.AppendLine($"    allPassed = check({Quote(test.Name)}, {call}, {expected}{order}) && allPassed;");
        }
        sb.AppendLine("    return allPassed ? 0 : 1;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool NeedsLinkedListSupport(IProblemItem problem) =>
        problem.Category.Equals("Linked List", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("ListNode", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsTreeSupport(IProblemItem problem) =>
        problem.Category.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        problem.SolutionCode.Contains("TreeNode", StringComparison.OrdinalIgnoreCase);

    private static string GetCppLinkedListSupport() =>
        """
        struct ListNode {
            int val;
            ListNode* next;
            ListNode() : val(0), next(nullptr) {}
            ListNode(int x) : val(x), next(nullptr) {}
            ListNode(int x, ListNode* next) : val(x), next(next) {}

            static ListNode* buildList(initializer_list<int> values) {
                ListNode dummy(0);
                ListNode* curr = &dummy;
                for (int v : values) {
                    curr->next = new ListNode(v);
                    curr = curr->next;
                }
                return dummy.next;
            }
        };

        inline string formatVal(ListNode* head) {
            ostringstream oss;
            oss << "[";
            while (head) {
                oss << head->val;
                if (head->next) oss << ",";
                head = head->next;
            }
            oss << "]";
            return oss.str();
        }
        """;

    private static string GetCppTreeSupport() =>
        """
        struct TreeNode {
            int val;
            TreeNode* left;
            TreeNode* right;
            TreeNode() : val(0), left(nullptr), right(nullptr) {}
            TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}
            TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}

            static TreeNode* buildTree(initializer_list<int> values) {
                vector<int> vals(values);
                if (vals.empty()) return nullptr;
                TreeNode* root = new TreeNode(vals[0]);
                queue<TreeNode*> q;
                q.push(root);
                size_t i = 1;
                while (i < vals.size() && !q.empty()) {
                    TreeNode* curr = q.front();
                    q.pop();
                    if (i < vals.size()) {
                        curr->left = new TreeNode(vals[i++]);
                        q.push(curr->left);
                    }
                    if (i < vals.size()) {
                        curr->right = new TreeNode(vals[i++]);
                        q.push(curr->right);
                    }
                }
                return root;
            }

            static int lcaValue(TreeNode* root, int p, int q) {
                if (!root) return -1;
                if (root->val == p || root->val == q) return root->val;
                int left = lcaValue(root->left, p, q);
                int right = lcaValue(root->right, p, q);
                if (left != -1 && right != -1) return root->val;
                return left != -1 ? left : right;
            }
        };

        inline string formatVal(TreeNode* root) {
            if (!root) return "null";
            return formatVal(root->val);
        }
        """;

    private static string MapTypeToCpp(string csharpType) => csharpType.Trim() switch
    {
        "int" => "int",
        "int[]" => "vector<int>",
        "int[][]" => "vector<vector<int>>",
        "string" => "string",
        "string[]" => "vector<string>",
        "bool" => "bool",
        "bool[]" => "vector<bool>",
        "double" => "double",
        "char" => "char",
        "char[]" => "vector<char>",
        "IList<int>" or "List<int>" => "vector<int>",
        "IList<string>" or "List<string>" => "vector<string>",
        "IList<IList<string>>" or "List<List<string>>" => "vector<vector<string>>",
        "IList<IList<int>>" or "List<List<int>>" => "vector<vector<int>>",
        "ListNode" => "ListNode*",
        "TreeNode" => "TreeNode*",
        _ => csharpType
    };

    private static string GetDefaultReturn(string cppType) => cppType switch
    {
        "int" or "long" or "short" or "char" => "return 0;",
        "double" or "float" => "return 0.0;",
        "bool" => "return false;",
        "void" => "",
        "string" => "return \"\";",
        _ when cppType.StartsWith("vector") => "return {};",
        _ when cppType.EndsWith("*") => "return nullptr;",
        _ => "return {};"
    };

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = text.Split('\n');
        return string.Join('\n', lines.Select(l => string.IsNullOrWhiteSpace(l) ? l : pad + l));
    }

    private static string Quote(string s) => $"\"{s.Replace("\"", "\\\"")}\"";
}
