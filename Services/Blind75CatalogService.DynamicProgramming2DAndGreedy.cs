using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetDynamicProgrammingProblems()
    {
        var list = new List<BlindProblemItem>(GetDynamicProgramming1DProblems());
        list.AddRange(GetDynamicProgramming2DAndGreedyProblems());
        return list;
    }

    private static IEnumerable<BlindProblemItem> GetDynamicProgramming2DAndGreedyProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_62_unique_paths",
            Number = 62,
            Title = "Unique Paths",
            Category = "2-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 64.3,
            IsPremium = false,
            Tags = new List<string> { "Math", "Dynamic Programming", "Combinatorics" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            A robot stands in the top-left cell of an `m × n` grid and wants to reach the bottom-right cell. It can only move **right** or **down**. How many different paths are there?

            ### Example 1
            - **Input:** `m = 3, n = 7`
            - **Output:** `28`

            ### Example 2
            - **Input:** `m = 3, n = 2`
            - **Output:** `3`
            - **Why:** right-down-down, down-right-down, down-down-right.

            ### Constraints
            - `1 <= m, n <= 100`
            - The answer is at most `2 · 10^9`.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count the monotone paths (only right and down) across the grid.

            1. **Look at a cell's last move.** The robot enters cell `(r, c)` either from above or from the left. So `paths[r][c] = paths[r-1][c] + paths[r][c-1]`.
            2. **The edges are easy:** along the top row or the left column there's only one way (keep going right, or keep going down), so they're all 1.
            3. **Fill the table row by row:** each cell needs the one above and the one to its left, both already filled. `O(m · n)` time.
            4. **One row is enough:** when you go along a row, `row[c]` still holds the value from above and `row[c-1]` already holds the new value from the left, so `row[c] += row[c-1]`. `O(n)` memory.
            5. **The counting view:** every path is `m - 1` downs and `n - 1` rights in some order, so the answer is `C(m + n − 2, m − 1)`. For 3 × 7 that's `C(8, 2) = 28`.

            **Pattern to remember:** grid path counting / minimum path sums → a 2-D table where each cell combines its top and left neighbours, often compressible to one row.

            **Common mistakes:** plain recursion (exponential); initialising the first row or column to 0; overflow in the combinatorics formula when multiplying before dividing.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try both moves, recursively",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^(m+n))",
                    SpaceComplexity = "O(m + n) call stack",
                    Intuition = "Paths from a cell = paths after moving down + paths after moving right; one path once you're on the last row or column.",
                    BottleneckExplanation = "Every cell is reached along many different routes and recounted each time.",
                    Code = """
                    int UniquePathsRecursively(int m, int n) => m == 1 || n == 1 ? 1 : UniquePathsRecursively(m - 1, n) + UniquePathsRecursively(m, n - 1);

                    Console.WriteLine(Judge.Format(UniquePathsRecursively(3, 7)));   // 28
                    """
                },
                new()
                {
                    Name = "Full table of path counts",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m · n)",
                    RecurrenceRelation = "paths[0][c] = paths[r][0] = 1\npaths[r][c] = paths[r − 1][c] + paths[r][c − 1]",
                    Intuition = "Each cell is reached from above or from the left, so its count is the sum of those two.",
                    Code = """
                    int UniquePathsTable(int m, int n)
                    {
                        var paths = new int[m, n];
                        for (int r = 0; r < m; r++)
                            for (int c = 0; c < n; c++)
                                paths[r, c] = r == 0 || c == 0 ? 1 : paths[r - 1, c] + paths[r, c - 1];
                        return paths[m - 1, n - 1];
                    }

                    Console.WriteLine(Judge.Format(UniquePathsTable(3, 2)));   // 3
                    """
                },
                new()
                {
                    Name = "Count the orders of downs and rights",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(min(m, n))",
                    SpaceComplexity = "O(1)",
                    Intuition = "A path is a sequence of m − 1 downs and n − 1 rights; choosing where the downs go gives C(m + n − 2, m − 1).",
                    Code = """
                    int UniquePathsByCounting(int m, int n)
                    {
                        long paths = 1;
                        for (int k = 1; k <= m - 1; k++) paths = paths * (n - 1 + k) / k;   // stays a whole number at every step
                        return (int)paths;
                    }

                    Console.WriteLine(Judge.Format(UniquePathsByCounting(10, 10)));   // 48620
                    """
                },
                new()
                {
                    Name = "One row, updated in place",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Keep one row of counts: row[c] (from above) += row[c − 1] (from the left), one row at a time."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int UniquePaths(int m, int n)
                {
                    var row = new int[n];
                    Array.Fill(row, 1);                // the top row: only one way along it
                    for (int r = 1; r < m; r++)
                        for (int c = 1; c < n; c++)
                            row[c] += row[c - 1];      // from above (the old row[c]) + from the left (the new row[c - 1])
                    return row[n - 1];
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "m = 3, n = 7", Expected = "28", Call = "sol.UniquePaths(3, 7)" },
                new() { Name = "Example 2", Input = "m = 3, n = 2", Expected = "3", Call = "sol.UniquePaths(3, 2)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single cell", Input = "m = 1, n = 1", Expected = "1", Call = "sol.UniquePaths(1, 1)" },
                new() { Name = "A single row", Input = "m = 1, n = 5", Expected = "1", Call = "sol.UniquePaths(1, 5)" },
                new() { Name = "A square", Input = "m = 3, n = 3", Expected = "6", Call = "sol.UniquePaths(3, 3)" },
                new() { Name = "A large grid", Input = "m = 23, n = 12", Expected = "193536720", Call = "sol.UniquePaths(23, 12)" }
            },
            StressTestCode = """
            judge.Agree("Random grids vs the full table",
                random => (m: random.Next(1, 15), n: random.Next(1, 15)),
                size => UniquePathsTable(size.m, size.n),
                size => sol.UniquePaths(size.m, size.n));
            judge.Agree("Random grids vs counting orders",
                random => (m: random.Next(1, 15), n: random.Next(1, 15)),
                size => UniquePathsByCounting(size.m, size.n),
                size => sol.UniquePaths(size.m, size.n));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1's 3 × 7 grid. The top row and the left column are 1 (only one way along an edge). Every other cell
            adds the cell above it and the cell to its left (both highlighted), and the bottom-right corner ends at 28.
            """,
            VisualizationCode = """
            int m = 3, n = 7;
            var paths = new int[m, n];
            var grid = MatrixTracker.CreateEmpty(m, n, "62. Unique Paths: each cell = from above + from the left");
            for (int c = 0; c < n; c++) { paths[0, c] = 1; grid.SetCell(0, c, val: "1"); }
            for (int r = 0; r < m; r++) { paths[r, 0] = 1; grid.SetCell(r, 0, val: "1"); }
            grid.Snapshot("The top row and the left column are all 1: along an edge there is only one way to go");

            for (int r = 1; r < m; r++)
                for (int c = 1; c < n; c++)
                {
                    paths[r, c] = paths[r - 1, c] + paths[r, c - 1];
                    grid.SetCell(r, c, val: paths[r, c].ToString(), state: GridCellState.Current);
                    grid.SetCell(r - 1, c, state: GridCellState.Visited);
                    grid.SetCell(r, c - 1, state: GridCellState.Visited);
                    grid.Snapshot($"({r},{c}) is entered from above ({paths[r - 1, c]}) or from the left ({paths[r, c - 1]}): {paths[r, c]} paths");
                    grid.SetCell(r, c, state: GridCellState.Default);
                    grid.SetCell(r - 1, c, state: GridCellState.Default);
                    grid.SetCell(r, c - 1, state: GridCellState.Default);
                }

            grid.SetCell(m - 1, n - 1, state: GridCellState.Path);
            grid.Snapshot($"{paths[m - 1, n - 1]} paths reach the bottom-right corner (C({m + n - 2}, {m - 1}) = {paths[m - 1, n - 1]})");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_1143_longest_common_subsequence",
            Number = 1143,
            Title = "Longest Common Subsequence",
            Category = "2-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 59.5,
            IsPremium = false,
            Tags = new List<string> { "String", "Dynamic Programming" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(m · n) (two rows are enough)",
            DescriptionMarkdown = """
            Given two strings `text1` and `text2`, return the length of their **longest common subsequence**: the longest string you can get from both by deleting some characters (possibly none) without changing the order of the rest. Return `0` if they share nothing.

            ### Example 1
            - **Input:** `text1 = "abcde", text2 = "ace"`
            - **Output:** `3`
            - **Why:** `"ace"` is a subsequence of both.

            ### Example 2
            - **Input:** `text1 = "abc", text2 = "abc"`
            - **Output:** `3`

            ### Example 3
            - **Input:** `text1 = "abc", text2 = "def"`
            - **Output:** `0`

            ### Constraints
            - `1 <= text1.length, text2.length <= 1000`
            - Both consist of lowercase English letters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** line up as many equal letters as possible, in order, in both strings.

            1. **Compare the last letters** of the two prefixes `text1[..i]` and `text2[..j]`:
               - **equal:** that letter can end the common subsequence, so `lcs(i, j) = lcs(i-1, j-1) + 1`;
               - **different:** at least one of them isn't used, so `lcs(i, j) = max(lcs(i-1, j), lcs(i, j-1))`.
            2. **Empty prefixes share nothing:** row 0 and column 0 are 0.
            3. **Fill a `(m+1) × (n+1)` table** row by row; each cell needs its left, top and top-left neighbours. `O(m · n)` time; only the previous row is ever read, so two rows suffice.
            4. **Walk Example 1:** matches `a/a`, `c/c`, `e/e` each add 1 to their top-left neighbour; every other cell copies the bigger of top and left. The corner is **3**.

            **Pattern to remember:** two sequences → a 2-D table over their prefixes (edit distance, LCS, interleaving strings, …): match → diagonal + 1, otherwise the best of skipping one side.

            **Common mistakes:** confusing it with the longest common *substring* (consecutive letters); off-by-one between the table index and the string index; plain recursion without memo.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Recursion on the last letters",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^(m+n))",
                    SpaceComplexity = "O(m + n) call stack",
                    Intuition = "Equal last letters → 1 + the answer without both; otherwise the better of dropping one letter from either string.",
                    BottleneckExplanation = "The same pair of prefixes is reached through many different drop orders and solved again each time.",
                    Code = """
                    int LcsRecursively(string a, string b, int i, int j)
                    {
                        if (i == 0 || j == 0) return 0;
                        if (a[i - 1] == b[j - 1]) return 1 + LcsRecursively(a, b, i - 1, j - 1);
                        return Math.Max(LcsRecursively(a, b, i - 1, j), LcsRecursively(a, b, i, j - 1));
                    }

                    Console.WriteLine(Judge.Format(LcsRecursively("abcde", "ace", 5, 3)));   // 3
                    """
                },
                new()
                {
                    Name = "Table over both prefixes",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m · n)",
                    RecurrenceRelation = "lcs[i][0] = lcs[0][j] = 0\nlcs[i][j] = text1[i−1] == text2[j−1] ? lcs[i−1][j−1] + 1 : max(lcs[i−1][j], lcs[i][j−1])",
                    Intuition = "Fill lcs[i][j] for every pair of prefixes: a matching last letter extends the diagonal; otherwise take the better neighbour."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int LongestCommonSubsequence(string text1, string text2)
                {
                    int m = text1.Length, n = text2.Length;
                    var lcs = new int[m + 1, n + 1];                 // row 0 and column 0: an empty prefix shares nothing
                    for (int i = 1; i <= m; i++)
                        for (int j = 1; j <= n; j++)
                            lcs[i, j] = text1[i - 1] == text2[j - 1]
                                ? lcs[i - 1, j - 1] + 1                    // both last letters join the subsequence
                                : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);  // drop the last letter of one of them
                    return lcs[m, n];
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "text1 = \"abcde\", text2 = \"ace\"", Expected = "3", Call = "sol.LongestCommonSubsequence(\"abcde\", \"ace\")" },
                new() { Name = "Example 2", Input = "text1 = \"abc\", text2 = \"abc\"", Expected = "3", Call = "sol.LongestCommonSubsequence(\"abc\", \"abc\")" },
                new() { Name = "Example 3", Input = "text1 = \"abc\", text2 = \"def\"", Expected = "0", Call = "sol.LongestCommonSubsequence(\"abc\", \"def\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One letter each", Input = "text1 = \"a\", text2 = \"a\"", Expected = "1", Call = "sol.LongestCommonSubsequence(\"a\", \"a\")" },
                new() { Name = "Order matters", Input = "text1 = \"ab\", text2 = \"ba\"", Expected = "1", Call = "sol.LongestCommonSubsequence(\"ab\", \"ba\")" },
                new() { Name = "Only one shared letter", Input = "text1 = \"bsbininm\", text2 = \"jmjkbkjkv\"", Expected = "1", Call = "sol.LongestCommonSubsequence(\"bsbininm\", \"jmjkbkjkv\")" },
                new() { Name = "Letters spread apart", Input = "text1 = \"oxcpqrsvwf\", text2 = \"shmtulqrypy\"", Expected = "2", Call = "sol.LongestCommonSubsequence(\"oxcpqrsvwf\", \"shmtulqrypy\")" }
            },
            StressTestCode = """
            judge.Agree("Random strings vs recursion",
                random =>
                {
                    string Letters() => new string(Enumerable.Range(0, random.Next(1, 7)).Select(_ => "abc"[random.Next(3)]).ToArray());
                    return (a: Letters(), b: Letters());
                },
                input => LcsRecursively(input.a, input.b, input.a.Length, input.b.Length),
                input => sol.LongestCommonSubsequence(input.a, input.b));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1: rows are the prefixes of `abcde`, columns the prefixes of `ace` (∅ is the empty prefix). A cell whose
            two letters match takes its top-left neighbour + 1 (green); otherwise it copies the bigger of the cells above
            and to the left. The bottom-right corner is the answer, 3.
            """,
            VisualizationCode = """
            string text1 = "abcde", text2 = "ace";
            int m = text1.Length, n = text2.Length;
            var lcs = new int[m + 1, n + 1];
            var grid = MatrixTracker.CreateEmpty(m + 1, n + 1, "1143. Longest Common Subsequence: match → diagonal + 1",
                rowHeaders: new[] { "∅" }.Concat(text1.Select(c => c.ToString())),
                columnHeaders: new[] { "∅" }.Concat(text2.Select(c => c.ToString())));
            for (int i = 0; i <= m; i++) grid.SetCell(i, 0, val: "0");
            for (int j = 0; j <= n; j++) grid.SetCell(0, j, val: "0");
            grid.Snapshot("An empty prefix shares nothing: row ∅ and column ∅ are 0");

            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                {
                    bool match = text1[i - 1] == text2[j - 1];
                    lcs[i, j] = match ? lcs[i - 1, j - 1] + 1 : Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                    grid.SetCell(i, j, val: lcs[i, j].ToString(), state: GridCellState.Current, color: match ? "#15803d" : null);
                    var from = match ? new[] { (i - 1, j - 1) } : new[] { (i - 1, j), (i, j - 1) };
                    foreach (var (r, c) in from) grid.SetCell(r, c, state: GridCellState.Visited);
                    grid.Snapshot(match
                        ? $"'{text1[i - 1]}' = '{text2[j - 1]}': both can end the subsequence, so the top-left {lcs[i - 1, j - 1]} + 1 = {lcs[i, j]}"
                        : $"'{text1[i - 1]}' ≠ '{text2[j - 1]}': drop one of them and keep the better of above ({lcs[i - 1, j]}) and left ({lcs[i, j - 1]}): {lcs[i, j]}");
                    grid.SetCell(i, j, state: GridCellState.Default);
                    foreach (var (r, c) in from) grid.SetCell(r, c, state: GridCellState.Default);
                }

            grid.SetCell(m, n, state: GridCellState.Path);
            grid.Snapshot($"The corner holds the answer: the longest common subsequence has length {lcs[m, n]} (\"ace\")");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_53_maximum_subarray",
            Number = 53,
            Title = "Maximum Subarray",
            Category = "Greedy",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 50.8,
            IsPremium = false,
            Tags = new List<string> { "Array", "Divide and Conquer", "Dynamic Programming" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `nums`, find the non-empty **subarray** (consecutive elements) with the **largest sum** and return that sum.

            ### Example 1
            - **Input:** `nums = [-2,1,-3,4,-1,2,1,-5,4]`
            - **Output:** `6`
            - **Why:** `[4,-1,2,1]` sums to 6.

            ### Example 2
            - **Input:** `nums = [1]`
            - **Output:** `1`

            ### Example 3
            - **Input:** `nums = [5,4,-1,7,8]`
            - **Output:** `23`
            - **Why:** the whole array; the -1 is worth crossing to reach 7 and 8.

            ### Constraints
            - `1 <= nums.length <= 10^5`
            - `-10^4 <= nums[i] <= 10^4`
            - **Follow-up:** try the divide-and-conquer approach too.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** which run of consecutive numbers has the biggest total?

            1. **Brute force:** every start and end, with a running sum: `O(n²)`.
            2. **Kadane's idea: decide at each number whether to keep the run going.** Let `current` be the best sum of a subarray that **ends at** the current number. When you reach `x`, either extend the previous run (`current + x`) or start a new run at `x`.
            3. **Start fresh exactly when the old run is negative:** a negative prefix only drags any future sum down, so `current = max(x, current + x)`.
            4. **The answer is the best `current` ever seen** (the best subarray ends somewhere). One pass, `O(1)` memory.
            5. **Walk Example 1:** current = -2, 1 (restart), -2, 4 (restart), 3, 5, **6**, 1, 5. Best **6**.

            **Pattern to remember:** "best contiguous segment" → Kadane: best ending here = max(extend, restart). Maximum product subarray (152) and best time to buy and sell (121) are cousins.

            **Common mistakes:** starting `best` at 0 (an all-negative array must return its largest element); resetting to 0 instead of to `x`; mixing up subarrays with subsequences.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Every start with a running sum",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "For each start, extend the end one element at a time, keeping the running sum and the largest sum seen.",
                    BottleneckExplanation = "Each start recomputes sums the previous start already knew; Kadane keeps the best run ending here and never looks back.",
                    Code = """
                    int MaxSubArrayBruteForce(int[] nums)
                    {
                        int best = nums[0];
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int sum = 0;
                            for (int j = i; j < nums.Length; j++)
                            {
                                sum += nums[j];
                                best = Math.Max(best, sum);
                            }
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(MaxSubArrayBruteForce(new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 })));   // 6
                    """
                },
                new()
                {
                    Name = "Divide and conquer",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(log n)",
                    Intuition = "The best subarray lies in the left half, in the right half, or crosses the middle; the crossing one is the best suffix of the left plus the best prefix of the right.",
                    Code = """
                    int MaxSubArrayDivide(int[] nums, int lo, int hi)
                    {
                        if (lo == hi) return nums[lo];
                        int mid = (lo + hi) / 2;
                        int leftBest = int.MinValue, rightBest = int.MinValue;
                        for (int i = mid, sum = 0; i >= lo; i--) leftBest = Math.Max(leftBest, sum += nums[i]);        // best ending at mid
                        for (int i = mid + 1, sum = 0; i <= hi; i++) rightBest = Math.Max(rightBest, sum += nums[i]);  // best starting at mid + 1
                        return Math.Max(leftBest + rightBest, Math.Max(MaxSubArrayDivide(nums, lo, mid), MaxSubArrayDivide(nums, mid + 1, hi)));
                    }

                    var example = new[] { 5, 4, -1, 7, 8 };
                    Console.WriteLine(Judge.Format(MaxSubArrayDivide(example, 0, example.Length - 1)));   // 23
                    """
                },
                new()
                {
                    Name = "Kadane: extend the run or restart",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    RecurrenceRelation = "current[i] = max(nums[i], current[i − 1] + nums[i])\nanswer = max over i of current[i]",
                    GreedyChoiceProperty = "A run with a negative sum can never help what follows, so dropping it is always safe.",
                    Intuition = "Track the best sum of a subarray ending at the current element; restart whenever carrying the old run would hurt."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MaxSubArray(int[] nums)
                {
                    int best = nums[0], current = nums[0];   // current: the best sum of a subarray ending here
                    for (int i = 1; i < nums.Length; i++)
                    {
                        current = Math.Max(nums[i], current + nums[i]);   // extend the run, or restart if it only drags us down
                        best = Math.Max(best, current);
                    }
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [-2,1,-3,4,-1,2,1,-5,4]", Expected = "6", Call = "sol.MaxSubArray(new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 })" },
                new() { Name = "Example 2", Input = "nums = [1]", Expected = "1", Call = "sol.MaxSubArray(new[] { 1 })" },
                new() { Name = "Example 3", Input = "nums = [5,4,-1,7,8]", Expected = "23", Call = "sol.MaxSubArray(new[] { 5, 4, -1, 7, 8 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "All negative", Input = "nums = [-3,-1,-2]", Expected = "-1", Call = "sol.MaxSubArray(new[] { -3, -1, -2 })" },
                new() { Name = "One negative number", Input = "nums = [-1]", Expected = "-1", Call = "sol.MaxSubArray(new[] { -1 })" },
                new() { Name = "Ties", Input = "nums = [1,-1,1]", Expected = "1", Call = "sol.MaxSubArray(new[] { 1, -1, 1 })" },
                new() { Name = "Restart after a deep drop", Input = "nums = [8,-19,5,-4,20]", Expected = "21", Call = "sol.MaxSubArray(new[] { 8, -19, 5, -4, 20 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs every start",
                random => Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(-10, 11)).ToArray(),
                nums => MaxSubArrayBruteForce(nums),
                nums => sol.MaxSubArray(nums));
            judge.Agree("Random arrays vs divide and conquer",
                random => Enumerable.Range(0, random.Next(1, 30)).Select(_ => random.Next(-10, 11)).ToArray(),
                nums => MaxSubArrayDivide(nums, 0, nums.Length - 1),
                nums => sol.MaxSubArray(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 1. The highlighted cells are the current run, the best subarray ending at `i`. When the run's sum would
            be smaller than the number on its own, the run restarts there. `best` remembers the record: `[4,-1,2,1]` = 6.
            """,
            VisualizationCode = """
            var nums = new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 };
            var tracker = VisualizerRecorder.CreateArray(nums, title: "53. Maximum Subarray: extend the run or start again");
            int best = nums[0], current = nums[0], start = 0, bestStart = 0, bestEnd = 0;
            tracker.Watch(() => current);
            tracker.Watch(() => best);
            tracker.Step($"The first run is just [{nums[0]}]", pointers: new { i = 0 }, highlight: new[] { 0 });

            for (int i = 1; i < nums.Length; i++)
            {
                bool restart = nums[i] > current + nums[i];
                string why = restart
                    ? $"{nums[i]} alone beats {current} + {nums[i]} = {current + nums[i]}: the old run only drags it down, so restart at {nums[i]}"
                    : $"{current} + {nums[i]} = {current + nums[i]} is at least {nums[i]} alone: extend the run";
                current = Math.Max(nums[i], current + nums[i]);
                if (restart) start = i;
                bool record = current > best;
                if (record) (best, bestStart, bestEnd) = (current, start, i);
                tracker.Step($"{why}{(record ? $". {current} is a new best" : "")}", pointers: new { i }, highlight: Enumerable.Range(start, i - start + 1));
            }

            tracker.Step($"The best subarray is [{string.Join(",", nums[bestStart..(bestEnd + 1)])}] with sum {best}", highlight: Enumerable.Range(bestStart, bestEnd - bestStart + 1));
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_55_jump_game",
            Number = 55,
            Title = "Jump Game",
            Category = "Greedy",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 41.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming", "Greedy" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You start at index 0 of an array `nums`, where `nums[i]` is the **longest** jump you can make from index `i` (any shorter jump is fine too). Return `true` if you can reach the last index.

            ### Example 1
            - **Input:** `nums = [2,3,1,1,4]`
            - **Output:** `true`
            - **Why:** jump 1 step to index 1, then 3 steps to the end.

            ### Example 2
            - **Input:** `nums = [3,2,1,0,4]`
            - **Output:** `false`
            - **Why:** every route lands on index 3, whose jump length is 0.

            ### Constraints
            - `1 <= nums.length <= 10^4`
            - `0 <= nums[i] <= 10^5`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** is the last index reachable, if from `i` you may land anywhere in `i+1 .. i+nums[i]`?

            1. **Trying every jump** is a search over paths: exponential in the worst case.
            2. **DP from the back:** index `i` is "good" if some index it can land on is good; the last index is good. `O(n²)` in the worst case.
            3. **Greedy: track the farthest index you can reach.** Walk left to right. Every index up to `farthest` is reachable (you can always take a shorter jump), so at each reachable `i`, update `farthest = max(farthest, i + nums[i])`.
            4. **If `i` ever passes `farthest`,** you're standing on an index nothing can reach: `false`. If `farthest` reaches the last index: `true`.
            5. **Walk Example 2:** farthest = 3, 3, 3, 3 (the 0 adds nothing), and index 4 is beyond 3 → `false`.

            **Another greedy:** walk from the end keeping a "goal": any index that can jump onto the goal becomes the new goal; the answer is whether the goal reaches index 0.

            **Pattern to remember:** reachability over a line with ranges → keep the frontier (farthest reach) instead of exploring paths. Jump Game II counts the jumps with the same frontier idea.

            **Common mistakes:** always taking the longest jump (it can land on a 0 you could have jumped over); forgetting that an array of length 1 is already solved.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try every jump, recursively",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "From i, try every jump length from nums[i] down to 1 and see whether any route reaches the end.",
                    BottleneckExplanation = "Indices are revisited from many different starting points; nothing is remembered.",
                    Code = """
                    bool CanJumpRecursively(int[] nums, int i = 0)
                    {
                        if (i >= nums.Length - 1) return true;
                        for (int step = nums[i]; step >= 1; step--)
                            if (CanJumpRecursively(nums, i + step)) return true;
                        return false;
                    }

                    Console.WriteLine(Judge.Format(CanJumpRecursively(new[] { 2, 3, 1, 1, 4 })));   // true
                    """
                },
                new()
                {
                    Name = "Which indices can reach the end? (DP from the back)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "good[n − 1] = true\ngood[i] = any good[i + s] for 1 ≤ s ≤ nums[i]",
                    Intuition = "An index is good when one of its landing spots is good; fill that from the end and read good[0].",
                    Code = """
                    bool CanJumpDp(int[] nums)
                    {
                        var good = new bool[nums.Length];
                        good[^1] = true;
                        for (int i = nums.Length - 2; i >= 0; i--)
                            for (int step = 1; step <= nums[i] && !good[i]; step++)
                                good[i] = i + step < nums.Length && good[i + step];
                        return good[0];
                    }

                    Console.WriteLine(Judge.Format(CanJumpDp(new[] { 3, 2, 1, 0, 4 })));   // false
                    """
                },
                new()
                {
                    Name = "Greedy from the back: move the goal",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "If index i can jump onto any good index, it can jump onto the leftmost one known, so remembering only that goal is enough.",
                    Intuition = "Walk from the end; any index that can jump onto the current goal becomes the new goal.",
                    Code = """
                    bool CanJumpBackwards(int[] nums)
                    {
                        int goal = nums.Length - 1;                 // the leftmost index known to reach the end
                        for (int i = nums.Length - 2; i >= 0; i--)
                            if (i + nums[i] >= goal) goal = i;      // i can land on the goal, so i becomes the goal
                        return goal == 0;
                    }

                    Console.WriteLine(Judge.Format(CanJumpBackwards(new[] { 2, 3, 1, 1, 4 })));   // true
                    """
                },
                new()
                {
                    Name = "Greedy: farthest reach so far",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Everything up to the farthest reach is reachable, so the farthest reach is all you need to remember.",
                    Intuition = "Sweep left to right keeping the farthest reachable index; standing beyond it means failure, reaching the end means success."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool CanJump(int[] nums)
                {
                    int farthest = 0;                                   // every index up to here is reachable
                    for (int i = 0; i < nums.Length; i++)
                    {
                        if (i > farthest) return false;                 // nothing reaches i, so nothing after it either
                        farthest = Math.Max(farthest, i + nums[i]);
                        if (farthest >= nums.Length - 1) return true;   // the last index is within reach
                    }
                    return true;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [2,3,1,1,4]", Expected = "true", Call = "sol.CanJump(new[] { 2, 3, 1, 1, 4 })" },
                new() { Name = "Example 2", Input = "nums = [3,2,1,0,4]", Expected = "false", Call = "sol.CanJump(new[] { 3, 2, 1, 0, 4 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Already at the end", Input = "nums = [0]", Expected = "true", Call = "sol.CanJump(new[] { 0 })" },
                new() { Name = "Stuck at the start", Input = "nums = [0,1]", Expected = "false", Call = "sol.CanJump(new[] { 0, 1 })" },
                new() { Name = "Jump over the zeros", Input = "nums = [2,0,0]", Expected = "true", Call = "sol.CanJump(new[] { 2, 0, 0 })" },
                new() { Name = "A zero blocks the way", Input = "nums = [1,1,0,1]", Expected = "false", Call = "sol.CanJump(new[] { 1, 1, 0, 1 })" },
                new() { Name = "The longest jump isn't best", Input = "nums = [3,0,8,2,0,0,1]", Expected = "true", Call = "sol.CanJump(new[] { 3, 0, 8, 2, 0, 0, 1 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs the DP table",
                random => Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(0, 4)).ToArray(),
                nums => CanJumpDp(nums),
                nums => sol.CanJump(nums));
            judge.Agree("Random arrays vs moving the goal",
                random => Enumerable.Range(0, random.Next(1, 30)).Select(_ => random.Next(0, 4)).ToArray(),
                nums => CanJumpBackwards(nums),
                nums => sol.CanJump(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 2, `[3,2,1,0,4]`. The highlighted cells are everything reachable so far, up to `farthest`. Each index
            within reach can push `farthest` further; here every jump stops at index 3 (the 0), so when `i` reaches 4 it
            is outside the reachable zone and the answer is false.
            """,
            VisualizationCode = """
            var nums = new[] { 3, 2, 1, 0, 4 };
            var tracker = VisualizerRecorder.CreateArray(nums, title: "55. Jump Game: how far can we get?");
            int farthest = 0;
            tracker.Watch(() => farthest);
            bool reachable = true;

            for (int i = 0; i < nums.Length; i++)
            {
                if (i > farthest)
                {
                    reachable = false;
                    tracker.Step($"Index {i} is beyond the farthest reachable index {farthest}: nothing can land here, so the answer is false",
                        pointers: new { i }, highlight: Enumerable.Range(0, farthest + 1));
                    break;
                }
                int before = farthest;
                farthest = Math.Max(farthest, i + nums[i]);
                tracker.Step(farthest > before
                        ? $"From {i} a jump of up to {nums[i]} reaches index {i + nums[i]}: farthest is now {farthest}"
                        : $"From {i} a jump of up to {nums[i]} reaches only {i + nums[i]}: farthest stays {farthest}",
                    pointers: new { i }, highlight: Enumerable.Range(0, Math.Min(farthest, nums.Length - 1) + 1));
                if (farthest >= nums.Length - 1)
                {
                    tracker.Step("The last index is within reach: true", highlight: Enumerable.Range(0, nums.Length));
                    break;
                }
            }

            if (!reachable) tracker.Step("Every route runs into the 0 at index 3, which jumps nowhere");
            Display.Visualizer(tracker);
            """
        }
    };
}
