using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetDynamicProgrammingTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "leetcode_72_edit_distance",
            Title = "72. Edit Distance (Interactive Multi-Approach Notebook)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Interactive notebook containing 4 distinct approaches (Naive Recursion, Memoization, 2D Matrix Tabulation, Rolling Rows DP), each with its own live visualizer.",
            IconKind = MaterialIconKind.NotebookOutline,
            AccentColor = "#a855f7",
            AccentBackground = "#2e1065",
            AccentBorder = "#7e22ce",
            CategoryBadge = "LeetCode 72 • Hard",
            Tags = new List<string> { "Dynamic Programming", "Edit Distance", "Levenshtein", "Notebook", "Visualizer", "MatrixTracker", "RecursionTracker" },
            Notes = @"# 72. Edit Distance (Levenshtein Distance)

Given two strings `word1` and `word2`, return *the minimum number of operations required to convert `word1` to `word2`*.

### Allowed operations (cost = 1 each):
1. **Insert** a character
2. **Delete** a character
3. **Replace** a character
- **Match**: cost = 0

---

### Notebook Structure (Each Approach Has Its Own Visualization):
1. **Approach 1 (Naive Recursion)**: `RecursionTracker` call tree showing the exponential 3-way branching.
2. **Approach 2 (Memoized Top-Down DP)**: `RecursionTracker` with memoization watch, showing pruned subtrees and green memo hits.
3. **Approach 3 (2D Tabulation DP & Path Backtracking)**: Interactive `MatrixTracker` table with dependency arrows, match diagonals, and illuminated optimal path.
4. **Approach 4 (Space-Optimized DP)**: `MatrixTracker` 2-row rolling buffer visualizer demonstrating O(min(m, n)) space reduction.",
            Cells = new List<NotebookCellItem>
            {
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"# 📓 72. Edit Distance (Levenshtein Distance)
### Masterclass Interactive Notebook

**Problem Statement:**
Given two strings `word1` and `word2`, find the minimum number of edits (Insert, Delete, Replace) to convert `word1` into `word2`.

Each approach in this notebook has **its own standalone visualizer** so you can see:
- The exponential recursive branching tree
- How memoization prunes duplicate subtrees
- The full 2D Dynamic Programming matrix with dependency arrows and traceback
- How space optimization reduces memory to just two rolling rows."
                },
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"---
## Approach 1: Naive Recursion & Call Tree Exploration
At each mismatch, we branch into 3 recursive choices:
1. **Insert**: `solve(i, j - 1)`
2. **Delete**: `solve(i - 1, j)`
3. **Replace**: `solve(i - 1, j - 1)`

Run the cell below to inspect the **live recursive call tree** using `RecursionTracker`."
                },
                new()
                {
                    Type = CellType.Code,
                    Source = @"// Approach 1: Naive Recursive Call Tree Visualization
string a = ""cat"";
string b = ""cut"";

var naiveTracker = RecursionTracker.Create($""Naive Recursion: '{a}' ➔ '{b}'"");

int NaiveEditDistance(int i, int j, string op = ""start"")
{
    string label = op == ""start"" ? $""{a}➔{b}"" : $""{op}({i},{j})"";
    using var call = naiveTracker.Enter(label);
    if (i == 0) return call.Return(j);
    if (j == 0) return call.Return(i);

    if (a[i - 1] == b[j - 1])
    {
        return call.Return(NaiveEditDistance(i - 1, j - 1, ""match""));
    }

    int del = NaiveEditDistance(i - 1, j, ""del"");
    int ins = NaiveEditDistance(i, j - 1, ""ins"");
    int rep = NaiveEditDistance(i - 1, j - 1, ""rep"");

    int ans = 1 + Math.Min(del, Math.Min(ins, rep));
    return call.Return(ans);
}

int dist1 = NaiveEditDistance(a.Length, b.Length);
Console.WriteLine($""[Approach 1 - Naive] \""{a}\"" ➔ \""{b}\"": Distance = {dist1} edits."");
Console.WriteLine($""Explored {naiveTracker.CallCount} recursive calls (notice the 3-way exponential branching for mismatches)."");
Display.Visualizer(naiveTracker);"
                },
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"---
## Approach 2: Top-Down Dynamic Programming (Memoization)
Notice how many identical calls `solve(i, j)` were spawned in Approach 1.
By caching results in a `memo` dictionary, any previously solved subproblem is immediately returned with zero recomputation.

Run the cell below to watch `RecursionTracker` show **green memo hits** pruning entire branches!"
                },
                new()
                {
                    Type = CellType.Code,
                    Source = @"// Approach 2: Memoized Recursion with Pruned Branches
string w1 = ""horse"";
string w2 = ""ros"";

var memoTracker = RecursionTracker.Create($""Memoized DP: '{w1}' ➔ '{w2}'"");
var memo = new Dictionary<(int, int), int>();
memoTracker.Watch(memo);

int MemoEditDistance(int i, int j, string op = ""root"")
{
    string label = op == ""root"" ? $""{w1}➔{w2}"" : $""{op}({i},{j})"";
    if (label.Length > 12) label = $""{op}({i},{j})"";
    using var call = memoTracker.Enter(label);
    if (memo.TryGetValue((i, j), out int cached))
    {
        return call.Memo(cached); // Green memo hit! Prunes the entire subtree
    }

    if (i == 0) return call.Return(j);
    if (j == 0) return call.Return(i);

    int result;
    if (w1[i - 1] == w2[j - 1])
    {
        result = MemoEditDistance(i - 1, j - 1, ""match"");
    }
    else
    {
        int del = MemoEditDistance(i - 1, j, ""del"");
        int ins = MemoEditDistance(i, j - 1, ""ins"");
        int rep = MemoEditDistance(i - 1, j - 1, ""rep"");
        result = 1 + Math.Min(del, Math.Min(ins, rep));
    }

    memo[(i, j)] = result;
    return call.Return(result);
}

int dist2 = MemoEditDistance(w1.Length, w2.Length);
Console.WriteLine($""[Approach 2 - Memoized] \""{w1}\"" ➔ \""{w2}\"": Distance = {dist2}"");
Console.WriteLine($""Total calls: {memoTracker.CallCount} (Memoization saved over 70% of redundant calls!)"");
Display.Visualizer(memoTracker);"
                },
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"---
## Approach 3: Bottom-Up 2D Tabulation DP & Path Backtracking
We construct an `(m + 1) × (n + 1)` table:
- **Diagonal $\nwarrow$ with cost 0**: Characters match (`word1[i-1] == word2[j-1]`).
- **Predecessor Arrows**: Mismatches show directed arrows pointing to the optimal choice:
  - $\nwarrow$ Amber arrow for **Replace**
  - $\uparrow$ Red arrow for **Delete**
  - $\leftarrow$ Blue arrow for **Insert**
- **Optimal Path**: Traced backwards from `(m, n)` to `(0, 0)` and highlighted in glowing emerald green."
                },
                new()
                {
                    Type = CellType.Code,
                    Source = @"// Approach 3: 2D Matrix Tabulation with Dependency Arrows & Backtracking
string word1 = ""horse"";
string word2 = ""ros"";
int m = word1.Length;
int n = word2.Length;
int[,] dp = new int[m + 1, n + 1];

var matrixTracker = MatrixTracker.CreateEmpty(
    m + 1, n + 1,
    title: $""Approach 3: Levenshtein 2D DP Matrix (\""{word1}\"" ➔ \""{word2}\"")"",
    rowHeaders: new[] { ""∅"" }.Concat(word1.Select(c => c.ToString())),
    columnHeaders: new[] { ""∅"" }.Concat(word2.Select(c => c.ToString()))
);

// Base cases: empty prefix conversions
for (int i = 0; i <= m; i++)
{
    dp[i, 0] = i;
    matrixTracker.SetCell(i, 0, val: i.ToString(), color: i == 0 ? ""#64748b"" : ""#ef4444"", subLabel: i == 0 ? ""start"" : ""del"");
}
for (int j = 1; j <= n; j++)
{
    dp[0, j] = j;
    matrixTracker.SetCell(0, j, val: j.ToString(), color: ""#3b82f6"", subLabel: ""ins"");
}
matrixTracker.Snapshot(
    ""Base Cases: row ∅ requires pure insertions (ins: blue), col ∅ requires pure deletions (del: red)"",
    new { Row_0 = ""Insert characters from word2"", Col_0 = ""Delete characters from word1"" }
);

for (int i = 1; i <= m; i++)
{
    for (int j = 1; j <= n; j++)
    {
        char c1 = word1[i - 1];
        char c2 = word2[j - 1];
        bool match = c1 == c2;

        if (match)
        {
            dp[i, j] = dp[i - 1, j - 1];
            matrixTracker.SetCell(i, j, val: dp[i, j].ToString(), state: GridCellState.Current, color: ""#10b981"", subLabel: ""match"");
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Visited);
            matrixTracker.PointTo(i, j, i - 1, j - 1, label: ""0"", color: ""#10b981"");
            matrixTracker.Snapshot(
                $""'{c1}' == '{c2}' [Match]: No edit needed. Inherit diagonal dp[{i-1},{j-1}] = {dp[i, j]}"",
                new { Action = ""Match"", Characters = $""'{c1}' == '{c2}'"", Cost = dp[i, j], Rule = $""dp[{i},{j}] = dp[{i-1},{j-1}]"" }
            );
            matrixTracker.SetCell(i, j, state: GridCellState.Default, color: ""#10b981"", subLabel: ""match"");
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Default);
        }
        else
        {
            int del = dp[i - 1, j];
            int ins = dp[i, j - 1];
            int rep = dp[i - 1, j - 1];
            int minCost = Math.Min(del, Math.Min(ins, rep));
            dp[i, j] = 1 + minCost;

            string op = (minCost == rep) ? ""rep"" : (minCost == del) ? ""del"" : ""ins"";
            string color = (minCost == rep) ? ""#f59e0b"" : (minCost == del) ? ""#ef4444"" : ""#3b82f6"";
            string fullAction = (minCost == rep) ? $""Replace '{c1}'➔'{c2}'"" : (minCost == del) ? $""Delete '{c1}'"" : $""Insert '{c2}'"";

            matrixTracker.SetCell(i, j, val: dp[i, j].ToString(), state: GridCellState.Current, color: color, subLabel: op);
            matrixTracker.SetCell(i - 1, j, state: GridCellState.Visited);
            matrixTracker.SetCell(i, j - 1, state: GridCellState.Visited);
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Visited);

            if (minCost == rep) matrixTracker.PointTo(i, j, i - 1, j - 1, label: ""rep"", color: ""#f59e0b"");
            else if (minCost == del) matrixTracker.PointTo(i, j, i - 1, j, label: ""del"", color: ""#ef4444"");
            else matrixTracker.PointTo(i, j, i, j - 1, label: ""ins"", color: ""#3b82f6"");

            matrixTracker.Snapshot(
                $""'{c1}' ≠ '{c2}' [{fullAction}]: 1 + min(del:{del}, ins:{ins}, rep:{rep}) = {dp[i, j]}"",
                new { Action = fullAction, Choice = op, Formula = $""1 + min(del:{del}, ins:{ins}, rep:{rep}) = {dp[i, j]}"", Cost = dp[i, j] }
            );
            matrixTracker.SetCell(i, j, state: GridCellState.Default, color: color, subLabel: op);
            matrixTracker.SetCell(i - 1, j, state: GridCellState.Default);
            matrixTracker.SetCell(i, j - 1, state: GridCellState.Default);
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Default);
        }
    }
}

// Reconstruct optimal path and operations
var path = new List<(int Row, int Col)>();
var steps = new List<string>();
int r = m, c = n;
path.Add((r, c));

while (r > 0 || c > 0)
{
    if (r > 0 && c > 0 && word1[r - 1] == word2[c - 1])
    {
        steps.Add($""Match '{word1[r - 1]}' == '{word2[c - 1]}' (cost 0)"");
        r--; c--;
    }
    else if (r > 0 && c > 0 && dp[r, c] == dp[r - 1, c - 1] + 1)
    {
        steps.Add($""Replace '{word1[r - 1]}' ➔ '{word2[c - 1]}' (cost 1)"");
        r--; c--;
    }
    else if (r > 0 && dp[r, c] == dp[r - 1, c] + 1)
    {
        steps.Add($""Delete '{word1[r - 1]}' (cost 1)"");
        r--;
    }
    else
    {
        steps.Add($""Insert '{word2[c - 1]}' (cost 1)"");
        c--;
    }
    path.Add((r, c));
}

path.Reverse();
steps.Reverse();

matrixTracker.MarkPath(path, $""Optimal Path: {dp[m, n]} operations ({string.Join("" ➔ "", steps)})"");
Display.Visualizer(matrixTracker);

Console.WriteLine($""Transformation Sequence ({steps.Count} steps):"");
for (int k = 0; k < steps.Count; k++)
{
    Console.WriteLine($""  {k + 1}. {steps[k]}"");
}
Console.WriteLine($""Levenshtein Edit Distance: {dp[m, n]}"");"
                },
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"---
## Approach 4: Space-Optimized Rolling Rows DP (O(N) Space)
Because each row only references the immediate previous row, we don't need to retain the entire (m + 1) × (n + 1) matrix in memory.
We can solve Edit Distance using only **two rolling rows** (`prev` and `curr`).

Run the cell below to watch the 2-row buffer compute the exact same result while capping memory to O(min(m, n))."
                },
                new()
                {
                    Type = CellType.Code,
                    Source = @"// Approach 4: Space-Optimized 2-Row Rolling Buffer Visualization
string s1 = ""horse"";
string s2 = ""ros"";
int m4 = s1.Length;
int n4 = s2.Length;

var rollingTracker = MatrixTracker.CreateEmpty(
    2, n4 + 1,
    title: $""Approach 4: Rolling 2-Row Buffer (O(N) Space)"",
    rowHeaders: new[] { ""prev (row i-1)"", ""curr (row i)"" },
    columnHeaders: new[] { ""∅"" }.Concat(s2.Select(c => c.ToString()))
);

int[] prev = new int[n4 + 1];
int[] curr = new int[n4 + 1];

for (int j = 0; j <= n4; j++)
{
    prev[j] = j;
    rollingTracker.SetCell(0, j, val: j.ToString(), color: j == 0 ? ""#64748b"" : ""#3b82f6"", subLabel: j == 0 ? ""start"" : ""ins"");
}
rollingTracker.Snapshot(""Initial Row: prev holds base cases for converting empty ∅ to prefixes of 'ros'"");

for (int i = 1; i <= m4; i++)
{
    curr[0] = i;
    rollingTracker.SetCell(1, 0, val: i.ToString(), state: GridCellState.Visited, color: ""#ef4444"", subLabel: ""del"");

    for (int j = 1; j <= n4; j++)
    {
        bool match = s1[i - 1] == s2[j - 1];
        string op;
        string color;
        if (match)
        {
            curr[j] = prev[j - 1];
            op = ""match"";
            color = ""#10b981"";
        }
        else
        {
            int rep = prev[j - 1];
            int del = prev[j];
            int ins = curr[j - 1];
            int minCost = Math.Min(rep, Math.Min(del, ins));
            curr[j] = 1 + minCost;
            op = (minCost == rep) ? ""rep"" : (minCost == del) ? ""del"" : ""ins"";
            color = (minCost == rep) ? ""#f59e0b"" : (minCost == del) ? ""#ef4444"" : ""#3b82f6"";
        }
        rollingTracker.SetCell(1, j, val: curr[j].ToString(), state: GridCellState.Current, color: color, subLabel: op);
    }

    rollingTracker.Snapshot(
        $""Computed Row {i} ('{s1[i - 1]}'): values [{string.Join("", "", curr)}]"",
        new { Row = i, Character = s1[i - 1], Buffer = string.Join("", "", curr) }
    );
    Array.Copy(curr, prev, n4 + 1);
    for (int j = 0; j <= n4; j++)
    {
        rollingTracker.SetCell(0, j, val: prev[j].ToString(), state: GridCellState.Default);
        rollingTracker.SetCell(1, j, val: """", state: GridCellState.Default, subLabel: """");
    }
}

rollingTracker.SetCell(0, n4, state: GridCellState.Path, color: ""#10b981"", subLabel: ""res"");
rollingTracker.Snapshot($""Final Edit Distance: {prev[n4]} computed using only 2 rows of memory!"");
Display.Visualizer(rollingTracker);

Console.WriteLine($""[Approach 4 - Rolling Rows] Memory footprint: 2 rows × {n4 + 1} cells = {2 * (n4 + 1)} integers."");
Console.WriteLine($""Final Edit Distance: {prev[n4]}"");"
                },
                new()
                {
                    Type = CellType.Markdown,
                    IsMarkdownPreviewMode = true,
                    Source = @"---
## Summary & Complexity Comparison

| Approach | Time Complexity | Space Complexity | Visualizer Used |
| :--- | :---: | :---: | :--- |
| **1. Naive Recursion** | O(3^min(m, n)) | O(m + n) stack | `RecursionTracker` (shows branching tree) |
| **2. Memoization (Top-Down)** | O(m × n) | O(m × n) | `RecursionTracker` + memo (shows pruned calls) |
| **3. 2D Tabulation (Bottom-Up)** | O(m × n) | O(m × n) | `MatrixTracker` (interactive grid & arrows) |
| **4. Rolling Rows (Optimized)** | O(m × n) | O(min(m, n)) | `MatrixTracker` (2-row rolling buffer) |

### Key Takeaway for Interviews:
- **State Definition**: `dp[i, j]` is the minimum edits to convert prefix `word1[0..i-1]` to prefix `word2[0..j-1]`.
- **Match**: `dp[i, j] = dp[i-1, j-1]` (cost 0).
- **Mismatch**: `1 + min(delete: dp[i-1, j], insert: dp[i, j-1], replace: dp[i-1, j-1])`."
                }
            },
            InitialCode = @"// LeetCode 72: Edit Distance (Levenshtein Distance)
string word1 = ""horse"", word2 = ""ros"";
int m = word1.Length, n = word2.Length;
int[,] dp = new int[m + 1, n + 1];

var matrixTracker = MatrixTracker.CreateEmpty(m + 1, n + 1,
    title: $""72. Edit Distance: \""{word1}\"" ➔ \""{word2}\"""",
    rowHeaders: new[] { ""∅"" }.Concat(word1.Select(c => c.ToString())),
    columnHeaders: new[] { ""∅"" }.Concat(word2.Select(c => c.ToString())));

for (int i = 0; i <= m; i++) { dp[i, 0] = i; matrixTracker.SetCell(i, 0, val: i.ToString(), color: i == 0 ? ""#64748b"" : ""#ef4444"", subLabel: i == 0 ? ""start"" : ""del""); }
for (int j = 1; j <= n; j++) { dp[0, j] = j; matrixTracker.SetCell(0, j, val: j.ToString(), color: ""#3b82f6"", subLabel: ""ins""); }
matrixTracker.Snapshot(""Base Cases: row ∅ pure insertions (blue), col ∅ pure deletions (red)"");

for (int i = 1; i <= m; i++)
{
    for (int j = 1; j <= n; j++)
    {
        char c1 = word1[i - 1];
        char c2 = word2[j - 1];
        bool match = c1 == c2;

        if (match)
        {
            dp[i, j] = dp[i - 1, j - 1];
            matrixTracker.SetCell(i, j, val: dp[i, j].ToString(), state: GridCellState.Current, color: ""#10b981"", subLabel: ""match"");
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Visited);
            matrixTracker.PointTo(i, j, i - 1, j - 1, label: ""0"", color: ""#10b981"");
            matrixTracker.Snapshot($""'{c1}' == '{c2}' [Match]: No edit needed. Inherit diagonal dp[{i-1},{j-1}] = {dp[i, j]}"");
            matrixTracker.SetCell(i, j, state: GridCellState.Default, color: ""#10b981"", subLabel: ""match"");
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Default);
        }
        else
        {
            int del = dp[i - 1, j], ins = dp[i, j - 1], rep = dp[i - 1, j - 1];
            int minCost = Math.Min(del, Math.Min(ins, rep));
            dp[i, j] = 1 + minCost;
            string op = (minCost == rep) ? ""rep"" : (minCost == del) ? ""del"" : ""ins"";
            string color = (minCost == rep) ? ""#f59e0b"" : (minCost == del) ? ""#ef4444"" : ""#3b82f6"";
            string fullAction = (minCost == rep) ? $""Replace '{c1}'➔'{c2}'"" : (minCost == del) ? $""Delete '{c1}'"" : $""Insert '{c2}'"";

            matrixTracker.SetCell(i, j, val: dp[i, j].ToString(), state: GridCellState.Current, color: color, subLabel: op);
            matrixTracker.SetCell(i - 1, j, state: GridCellState.Visited);
            matrixTracker.SetCell(i, j - 1, state: GridCellState.Visited);
            matrixTracker.SetCell(i - 1, j - 1, state: GridCellState.Visited);

            if (minCost == rep) matrixTracker.PointTo(i, j, i - 1, j - 1, label: ""rep"", color: ""#f59e0b"");
            else if (minCost == del) matrixTracker.PointTo(i, j, i - 1, j, label: ""del"", color: ""#ef4444"");
            else matrixTracker.PointTo(i, j, i, j - 1, label: ""ins"", color: ""#3b82f6"");

            matrixTracker.Snapshot($""'{c1}' ≠ '{c2}' [{fullAction}]: 1 + min(del:{del}, ins:{ins}, rep:{rep}) = {dp[i, j]}"");
            matrixTracker.SetCell(i, j, state: GridCellState.Default, color: color, subLabel: op);
        }
    }
}

var path = new List<(int Row, int Col)>();
var steps = new List<string>();
int r = m, c = n;
path.Add((r, c));

while (r > 0 || c > 0)
{
    if (r > 0 && c > 0 && word1[r - 1] == word2[c - 1]) { steps.Add($""Match '{word1[r - 1]}' == '{word2[c - 1]}' (cost 0)""); r--; c--; }
    else if (r > 0 && c > 0 && dp[r, c] == dp[r - 1, c - 1] + 1) { steps.Add($""Replace '{word1[r - 1]}' ➔ '{word2[c - 1]}' (cost 1)""); r--; c--; }
    else if (r > 0 && dp[r, c] == dp[r - 1, c] + 1) { steps.Add($""Delete '{word1[r - 1]}' (cost 1)""); r--; }
    else { steps.Add($""Insert '{word2[c - 1]}' (cost 1)""); c--; }
    path.Add((r, c));
}

path.Reverse(); steps.Reverse();
matrixTracker.MarkPath(path, $""Optimal Path: {dp[m, n]} operations ({string.Join("" ➔ "", steps)})"");
Display.Visualizer(matrixTracker);

Console.WriteLine($""Transformation Sequence ({steps.Count} steps):"");
for (int k = 0; k < steps.Count; k++) Console.WriteLine($""  {k + 1}. {steps[k]}"");
Console.WriteLine($""Levenshtein Edit Distance: {dp[m, n]}"");",
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Case 1", Input = "word1 = \"horse\", word2 = \"ros\"", ExpectedOutput = "3" },
                new() { Name = "Case 2", Input = "word1 = \"intention\", word2 = \"execution\"", ExpectedOutput = "5" },
                new() { Name = "Case 3", Input = "word1 = \"cat\", word2 = \"cut\"", ExpectedOutput = "1" }
            }
        }
    };
}
