using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetRecursionTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "recursion_tree_fibonacci_memo",
            Title = "509. Fibonacci Recursion Tree (Naive vs Memo)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "See every recursive call as a tree, the live call stack, and how a memo cuts whole subtrees.",
            IconKind = MaterialIconKind.FamilyTree,
            AccentColor = "#14b8a6",
            AccentBackground = "#062e2a",
            AccentBorder = "#0d9488",
            CategoryBadge = "Recursion • Memoization",
            Tags = new List<string> { "Recursion", "Memoization", "Dynamic Programming", "RecursionTracker" },
            Notes = @"# Fibonacci Recursion Tree

Each call becomes a node under the call that made it.
- The path from **main** to the glowing node is the call stack; teal calls are waiting for a result.
- Returned calls show their value; green calls were answered from the memo without recursing.
- Set `useMemo = false` to watch the naive version recompute the same subproblems (15 calls instead of 9).",
            InitialCode = @"bool useMemo = true;   // flip to false to see the naive tree
var calls = RecursionTracker.Create(useMemo ? ""fib(5) with a memo"" : ""fib(5) without a memo"");
var memo = new Dictionary<int, long>();
if (useMemo) calls.Watch(memo);

long Fib(int n)
{
    using var call = calls.Enter($""fib({n})"");
    if (useMemo && memo.TryGetValue(n, out var cached)) return call.Memo(cached);
    if (n < 2) return call.Return(n);

    long result = Fib(n - 1) + Fib(n - 2);
    if (useMemo) memo[n] = result;
    return call.Return(result);
}

Console.WriteLine($""fib(5) = {Fib(5)} using {calls.CallCount} calls"");
Display.Visualizer(calls);"
        }
    };
}
