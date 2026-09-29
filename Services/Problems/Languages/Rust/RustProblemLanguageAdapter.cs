using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;

/// <summary>
/// Formats Blind 75 problems as Rust the way LeetCode has them: a <c>struct Solution</c> with an <c>impl</c> whose method has
/// LeetCode's Rust signature, and a harness that runs each of the problem's cases and prints a ✅ or ❌ line for it. The tests are the
/// catalog's own, translated from their C# calls (see <see cref="RustCallTranspiler"/>), so every language checks the same cases.
/// </summary>
public class RustProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static RustProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => LanguageIds.Rust;
    public string DisplayName => "Rust (Cargo)";
    public string DefaultFileExtension => ".rs";

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
            Title = $"{problem.Number:D2}. {problem.Title}.rs",
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
        var notebook = new NotebookDocumentItem { Title = $"{problem.Number}. {problem.Title} (Rust Notebook)" };

        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = source.Trim(),
            IsMarkdownPreviewMode = true
        });

        void RustCode(string source) => notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Language = LanguageId,
            Source = source.Trim()
        });

        Markdown($"# {problem.FullTitle}\n\n**Category:** `{problem.Category}` · **Difficulty:** `{problem.DifficultyBadgeText}` · **Acceptance:** `{problem.AcceptanceRateText}`\n\n{problem.DescriptionMarkdown.Trim()}");
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            Markdown($"## 🧠 How to think\n\n{problem.ThinkingProcessMarkdown.Trim()}");
        }

        // The definitions every later cell uses: the node types, and the harness that judges the tests.
        var setup = new StringBuilder();
        setup.AppendLine(RustProblemSupport.Uses).AppendLine();
        AppendSupport(setup, problem);
        setup.AppendLine(RustProblemSupport.Harness);
        Markdown("### 🧰 Setup\n\nRun this first: the node types LeetCode gives you and the harness that prints ✅/❌ for each test.");
        RustCode(setup.ToString());

        Markdown("### 💻 Solution\n\nImplement the solution below, then run the Tests cell.");
        RustCode(SolutionCode(problem));

        Markdown("### 🧪 Tests\n\nRun these to verify your implementation: `check` prints ✅ when the answer matches and ❌ with both values when it doesn't.");
        RustCode(BuildTestCode(problem));

        return notebook;
    }

    /// <summary>The tests as a notebook cell: the problem's helper functions, then a <c>check</c> statement for each case.</summary>
    public string BuildTestCode(IProblemItem problem)
    {
        var code = new StringBuilder();
        var helpers = RustProblemSupport.TestHelpers(problem.Number);
        if (helpers.Length > 0)
        {
            code.AppendLine(helpers);
            code.AppendLine();
        }

        var setup = RustProblemSupport.TestSetupStatements(problem.Number);
        if (setup.Length > 0) code.AppendLine(setup);

        foreach (var line in TestLines(problem, collectResult: false)) code.AppendLine(line);
        return code.ToString().TrimEnd();
    }

    private string BuildScriptCode(IProblemItem problem)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        sb.AppendLine("// Run (F5): builds with Cargo and prints a ✅/❌ line per test case.");
        sb.AppendLine("// The full statement and hints are in the Notes panel.");
        sb.AppendLine(RustProblemSupport.Prelude);
        sb.AppendLine();
        AppendSupport(sb, problem);
        sb.AppendLine(SolutionCode(problem));
        sb.AppendLine();
        sb.AppendLine(RustProblemSupport.Harness);
        sb.AppendLine();

        var helpers = RustProblemSupport.TestHelpers(problem.Number);
        if (helpers.Length > 0)
        {
            sb.AppendLine(helpers);
            sb.AppendLine();
        }

        sb.AppendLine("fn main() {");
        var setup = RustProblemSupport.TestSetupStatements(problem.Number);
        if (setup.Length > 0) sb.AppendLine("    " + setup);
        sb.AppendLine("    let mut all_passed = true;");
        foreach (var line in TestLines(problem, collectResult: true)) sb.AppendLine("    " + line);
        sb.AppendLine("    if !all_passed {");
        sb.AppendLine("        std::process::exit(1);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // One line per case. A call the translation can't read is a line that says so instead of a case, so the script still builds
    // and that case reads "not run" rather than passing or failing.
    private static IEnumerable<string> TestLines(IProblemItem problem, bool collectResult)
    {
        var method = RustProblemSignature.For(problem.SolutionCode, problem.Title, problem.Number);
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            if (!TryTranslate(test.Call, method, problem.Number, out var call, out var reason))
            {
                yield return $"// Not translated to Rust: {test.Call.Trim()} ({reason})";
                yield return $"println!(\"⚠️ {{}} → this case isn't available in Rust yet\", {RustValueLiterals.StringLiteral(test.Name)});";
                continue;
            }

            var check = $"check({RustValueLiterals.StringLiteral(test.Name)}, || {call}, {ExpectedLiteral(test.Expected)}, {(test.AnyOrder ? "true" : "false")})";
            yield return collectResult ? $"all_passed &= {check};" : $"{check};";
        }
    }

    internal static bool TryTranslate(string csharpCall, RustMethod method, int problemNumber, out string rust, out string reason)
    {
        try
        {
            rust = RustCallTranspiler.Translate(csharpCall, method, problemNumber);
            reason = string.Empty;
            return true;
        }
        catch (NotSupportedException ex)
        {
            rust = string.Empty;
            reason = ex.Message;
            return false;
        }
    }

    // What a case is expected to print, as a Rust string: raw when it has quotes, so it reads the way LeetCode shows it.
    private static string ExpectedLiteral(string expected) =>
        expected.Contains('"') || expected.Contains('\\') ? RustValueLiterals.RawString(expected) : RustValueLiterals.StringLiteral(expected);

    private static void AppendSupport(StringBuilder sb, IProblemItem problem)
    {
        var (list, tree, graph, board) = NeededSupport(problem);
        var cycle = problem.Number == RustProblemSignature.SharedNodeProblem;
        if (cycle) sb.AppendLine(RustProblemSupport.CycleListSupport).AppendLine();
        else if (list) sb.AppendLine(RustProblemSupport.ListSupport).AppendLine();
        if (tree) sb.AppendLine(RustProblemSupport.TreeSupport).AppendLine();
        if (graph) sb.AppendLine(RustProblemSupport.GraphSupport).AppendLine();
        if (board) sb.AppendLine(RustProblemSupport.BoardSupport).AppendLine();
    }

    private static (bool List, bool Tree, bool Graph, bool Board) NeededSupport(IProblemItem problem)
    {
        var calls = string.Join('\n', problem.Tests.Concat(problem.ExtraTests).Select(t => t.Call));
        var signature = RustProblemSignature.For(problem.SolutionCode, problem.Title, problem.Number).Signature;
        return (
            List: signature.Contains("ListNode", StringComparison.Ordinal) || calls.Contains("BuildList(", StringComparison.Ordinal),
            Tree: signature.Contains("TreeNode", StringComparison.Ordinal) || calls.Contains("BuildTree(", StringComparison.Ordinal),
            Graph: signature.Contains("RefCell<Node>", StringComparison.Ordinal) || calls.Contains("BuildGraph(", StringComparison.Ordinal),
            Board: calls.Contains("Board(", StringComparison.Ordinal) || calls.Contains("Grid(", StringComparison.Ordinal));
    }

    /// <summary>What the learner starts from: a curated solution when the catalog has one for Rust, otherwise LeetCode's empty method.</summary>
    internal static string SolutionCode(IProblemItem problem)
    {
        if (problem.LanguageImplementations.TryGetValue(LanguageIds.Rust, out var bundle) && !string.IsNullOrWhiteSpace(bundle.SolutionCode))
        {
            return bundle.SolutionCode.Trim();
        }

        return DesignStarter(problem.Number) ?? Starter(problem);
    }

    /// <summary>The empty <c>impl Solution</c> LeetCode gives for the problem, returning a value of the right type so it builds.</summary>
    internal static string Starter(IProblemItem problem)
    {
        var method = RustProblemSignature.For(problem.SolutionCode, problem.Title, problem.Number);
        var lines = new List<string>
        {
            "struct Solution;",
            string.Empty,
            "impl Solution {",
            $"    {method.Signature} {{",
            $"        // TODO: Implement solution for {problem.FullTitle}"
        };
        if (method.DefaultBody.Length > 0) lines.Add("        " + method.DefaultBody);
        lines.Add("    }");
        lines.Add("}");
        return string.Join('\n', lines);
    }

    // The problems that are a class with several methods, not one method: LeetCode's Rust starters for them.
    private static string? DesignStarter(int problemNumber) => problemNumber switch
    {
        208 => """
            struct Trie {
                // TODO: the fields you need
            }

            impl Trie {
                fn new() -> Self {
                    Trie {}
                }

                fn insert(&mut self, word: String) {}

                fn search(&self, word: String) -> bool {
                    false
                }

                fn starts_with(&self, prefix: String) -> bool {
                    false
                }
            }
            """,
        211 => """
            struct WordDictionary {
                // TODO: the fields you need
            }

            impl WordDictionary {
                fn new() -> Self {
                    WordDictionary {}
                }

                fn add_word(&mut self, word: String) {}

                // A '.' in the word matches any letter.
                fn search(&self, word: String) -> bool {
                    false
                }
            }
            """,
        295 => """
            struct MedianFinder {
                // TODO: the fields you need
            }

            impl MedianFinder {
                fn new() -> Self {
                    MedianFinder {}
                }

                fn add_num(&mut self, num: i32) {}

                fn find_median(&self) -> f64 {
                    0.0
                }
            }
            """,
        271 => """
            struct Codec {}

            impl Codec {
                fn new() -> Self {
                    Codec {}
                }

                // Encodes a list of strings to a single string.
                fn encode(&self, strs: Vec<String>) -> String {
                    String::new()
                }

                // Decodes a single string to a list of strings.
                fn decode(&self, s: String) -> Vec<String> {
                    Vec::new()
                }
            }
            """,
        297 => """
            struct Codec {}

            impl Codec {
                fn new() -> Self {
                    Codec {}
                }

                // Encodes a tree to a single string.
                fn serialize(&self, root: Option<Rc<RefCell<TreeNode>>>) -> String {
                    String::new()
                }

                // Decodes your encoded data to a tree.
                fn deserialize(&self, data: String) -> Option<Rc<RefCell<TreeNode>>> {
                    None
                }
            }
            """,
        _ => null
    };
}
