using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetDynamicProgramming1DProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_70_climbing_stairs",
            Number = 70,
            Title = "Climbing Stairs",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 54.0,
            IsPremium = false,
            Tags = new List<string> { "Math", "Dynamic Programming", "Memoization" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are climbing a staircase of `n` steps. Each move climbs either **1 or 2** steps. In how many distinct ways can you reach the top?

            ### Example 1
            - **Input:** `n = 2`
            - **Output:** `2`
            - **Why:** `1 + 1` or `2`.

            ### Example 2
            - **Input:** `n = 3`
            - **Output:** `3`
            - **Why:** `1 + 1 + 1`, `1 + 2` or `2 + 1`.

            ### Constraints
            - `1 <= n <= 45`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count the sequences of 1s and 2s that add up to `n`.

            1. **Look at the last move.** Any way to reach step `n` ends with a 1-step (from step `n - 1`) or a 2-step (from step `n - 2`). Those two groups don't overlap, so `ways(n) = ways(n - 1) + ways(n - 2)`: the Fibonacci numbers.
            2. **Plain recursion is exponential:** `ways(5)` calls `ways(4)` and `ways(3)`, and `ways(4)` calls `ways(3)` again, and so on: about `2^n` calls for 45 stairs.
            3. **Each value only needs computing once.** Remember them (memoization), or fill a table from the bottom up: `ways[0] = 1`, `ways[1] = 1`, then each entry is the sum of the two before it. `O(n)`.
            4. **Only the last two entries are ever read,** so two variables are enough: `O(1)` memory.
            5. **Walk n = 5:** 1, 1, 2, 3, 5, **8**.

            **Pattern to remember:** "how many ways" + "the last step has a few options" → add up the counts of the smaller problems. This is the simplest dynamic programming recurrence.

            **Common mistakes:** plain recursion without memo (times out at n ≈ 40); an off-by-one start (`ways[0]` is 1: there is one way to be at the bottom); overflow for larger variants (45 still fits in `int`).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Plain recursion",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "ways(n) = ways(n − 1) + ways(n − 2), with ways(1) = 1 and ways(2) = 2, straight from the definition.",
                    BottleneckExplanation = "The same stairs are recomputed again and again: ways(3) is computed twice for n = 5, and the count doubles with every stair.",
                    Code = """
                    int ClimbRecursively(int n) => n <= 2 ? n : ClimbRecursively(n - 1) + ClimbRecursively(n - 2);

                    Console.WriteLine(Judge.Format(ClimbRecursively(10)));   // 89
                    """
                },
                new()
                {
                    Name = "Bottom-up table",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "ways[0] = 1, ways[1] = 1\nways[i] = ways[i − 1] + ways[i − 2]",
                    Intuition = "Fill the answers for 0, 1, 2 … n stairs in order; each one is the sum of the two before it, so every stair is computed once.",
                    Code = """
                    int ClimbWithTable(int n)
                    {
                        var ways = new int[n + 1];
                        ways[0] = 1;   // one way to be at the bottom: don't move
                        ways[1] = 1;
                        for (int i = 2; i <= n; i++) ways[i] = ways[i - 1] + ways[i - 2];
                        return ways[n];
                    }

                    Console.WriteLine(Judge.Format(ClimbWithTable(5)));   // 8
                    """
                },
                new()
                {
                    Name = "Keep only the last two counts",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "The table only ever reads its last two entries, so slide two variables up the stairs."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int ClimbStairs(int n)
                {
                    int twoBelow = 1, oneBelow = 1;   // ways to reach step i - 2 and step i - 1 (starting at steps 0 and 1)
                    for (int i = 2; i <= n; i++)
                        (twoBelow, oneBelow) = (oneBelow, oneBelow + twoBelow);   // the last move was 1 or 2 steps
                    return oneBelow;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 2", Expected = "2", Call = "sol.ClimbStairs(2)" },
                new() { Name = "Example 2", Input = "n = 3", Expected = "3", Call = "sol.ClimbStairs(3)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One stair", Input = "n = 1", Expected = "1", Call = "sol.ClimbStairs(1)" },
                new() { Name = "Five stairs", Input = "n = 5", Expected = "8", Call = "sol.ClimbStairs(5)" },
                new() { Name = "Ten stairs", Input = "n = 10", Expected = "89", Call = "sol.ClimbStairs(10)" },
                new() { Name = "The largest n", Input = "n = 45", Expected = "1836311903", Call = "sol.ClimbStairs(45)" }
            },
            StressTestCode = """
            judge.Agree("Every n up to 25 vs the table", random => random.Next(1, 26), n => ClimbWithTable(n), n => sol.ClimbStairs(n));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Two players. First the plain recursion for 5 stairs: the call tree shows `ways(3)` and `ways(2)` being
            computed more than once (`computed` counts them). Then the table for 6 stairs: each entry is the sum of the
            two below it, computed exactly once. The solution keeps only those two entries.
            """,
            VisualizationCode = """
            // 1. Plain recursion: the same stairs are recomputed
            var calls = RecursionTracker.Create("70. Climbing Stairs: plain recursion repeats work");
            var computed = new SortedDictionary<int, int>();   // how many times each ways(n) was computed
            calls.Watch(computed);
            int Ways(int n)
            {
                using var call = calls.Enter($"ways({n})");
                computed[n] = computed.GetValueOrDefault(n) + 1;
                if (n <= 2) return call.Return(n);
                return call.Return(Ways(n - 1) + Ways(n - 2));
            }
            Ways(5);
            Display.Visualizer(calls);

            // 2. Bottom-up: every stair once
            int stairs = 6;
            var ways = new int[stairs + 1];
            var tracker = VisualizerRecorder.CreateArray(ways, title: "70. Climbing Stairs: each stair from the two below it");
            ways[0] = 1;
            ways[1] = 1;
            tracker.Step("ways[0] = 1 (one way to stay at the bottom) and ways[1] = 1 (a single 1-step)", highlight: new[] { 0, 1 });
            for (int i = 2; i <= stairs; i++)
            {
                ways[i] = ways[i - 1] + ways[i - 2];
                tracker.Step($"The last move to stair {i} is a 1-step from {i - 1} or a 2-step from {i - 2}: ways[{i}] = {ways[i - 1]} + {ways[i - 2]} = {ways[i]}",
                    pointers: new { i }, highlight: new[] { i - 2, i - 1 });
            }
            tracker.Step($"{ways[stairs]} ways to climb {stairs} stairs, each stair computed once");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_198_house_robber",
            Number = 198,
            Title = "House Robber",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 51.5,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Houses along a street hold `nums[i]` money each. Robbing two **neighbouring** houses sets off the alarm. Return the most money you can take without robbing two neighbours.

            ### Example 1
            - **Input:** `nums = [1,2,3,1]`
            - **Output:** `4`
            - **Why:** rob houses 0 and 2: `1 + 3`.

            ### Example 2
            - **Input:** `nums = [2,7,9,3,1]`
            - **Output:** `12`
            - **Why:** rob houses 0, 2 and 4: `2 + 9 + 1`.

            ### Constraints
            - `1 <= nums.length <= 100`
            - `0 <= nums[i] <= 400`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** pick houses, no two adjacent, maximising the total.

            1. **Greedy fails:** always grabbing the richest house can block two good neighbours (`[2,3,2]` → take both 2s, not the 3… wait, that's 4 vs 3; and `[3,4,3]` → 6 beats 4).
            2. **Decide house by house.** Standing at house `i`, there are two options:
               - **rob it:** `nums[i]` plus the best you could do up to house `i - 2` (house `i - 1` is off limits);
               - **skip it:** the best you could do up to house `i - 1`.
            3. **So** `best[i] = max(best[i - 1], best[i - 2] + nums[i])`. Fill it left to right: `O(n)`.
            4. **Only two numbers are needed** at any time: the best up to `i - 1` and up to `i - 2`.
            5. **Walk Example 2:** best = 2, 7, 11 (2 + 9), 11 (skip 3), 12 (11 + 1).

            **Pattern to remember:** "choose or skip each item, with a restriction on neighbours" → a DP over the prefix where each state looks back one or two steps.

            **Common mistakes:** greedily taking every other house (odd or even positions: `[2,1,1,2]` needs the two ends); forgetting the single-house case; mixing up "best up to i" with "best that robs i".
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try robbing or skipping every house",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "From house i, either rob it and jump to i + 2, or skip it and move to i + 1; take the better of the two totals.",
                    BottleneckExplanation = "The best total from each house onward is recomputed along many paths; storing it once makes it linear.",
                    Code = """
                    int RobRecursively(int[] nums, int i = 0) =>
                        i >= nums.Length ? 0 : Math.Max(nums[i] + RobRecursively(nums, i + 2), RobRecursively(nums, i + 1));

                    Console.WriteLine(Judge.Format(RobRecursively(new[] { 2, 7, 9, 3, 1 })));   // 12
                    """
                },
                new()
                {
                    Name = "A table of the best total so far",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "best[0] = nums[0], best[1] = max(nums[0], nums[1])\nbest[i] = max(best[i − 1], best[i − 2] + nums[i])",
                    Intuition = "best[i] is the most money from houses 0..i: either house i is skipped (best[i − 1]) or robbed on top of best[i − 2].",
                    Code = """
                    int RobWithTable(int[] nums)
                    {
                        var best = new int[nums.Length];
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int skip = i >= 1 ? best[i - 1] : 0;
                            int rob = nums[i] + (i >= 2 ? best[i - 2] : 0);
                            best[i] = Math.Max(skip, rob);
                        }
                        return best[^1];
                    }

                    Console.WriteLine(Judge.Format(RobWithTable(new[] { 1, 2, 3, 1 })));   // 4
                    """
                },
                new()
                {
                    Name = "Two running totals",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Keep the best total up to the previous house and up to the one before it; each house updates them in one line."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int Rob(int[] nums)
                {
                    int skipLast = 0, best = 0;   // the best total up to house i - 2, and up to house i - 1
                    foreach (int money in nums)
                        (skipLast, best) = (best, Math.Max(best, skipLast + money));   // skip this house, or rob it on top of i - 2
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [1,2,3,1]", Expected = "4", Call = "sol.Rob(new[] { 1, 2, 3, 1 })" },
                new() { Name = "Example 2", Input = "nums = [2,7,9,3,1]", Expected = "12", Call = "sol.Rob(new[] { 2, 7, 9, 3, 1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One house", Input = "nums = [5]", Expected = "5", Call = "sol.Rob(new[] { 5 })" },
                new() { Name = "Not every other house", Input = "nums = [2,1,1,2]", Expected = "4", Call = "sol.Rob(new[] { 2, 1, 1, 2 })" },
                new() { Name = "Empty houses", Input = "nums = [0,0,0]", Expected = "0", Call = "sol.Rob(new[] { 0, 0, 0 })" },
                new() { Name = "The middle beats both ends", Input = "nums = [1,3,1]", Expected = "3", Call = "sol.Rob(new[] { 1, 3, 1 })" }
            },
            StressTestCode = """
            judge.Agree("Random streets vs trying every choice",
                random => Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(0, 20)).ToArray(),
                nums => RobRecursively(nums),
                nums => sol.Rob(nums));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 2. The top row is the money in each house and the bottom row the best total up to that house. Each
            step compares robbing the house (its money plus the best two houses back) with skipping it (the best one
            house back). At the end the houses actually robbed are traced back and marked.
            """,
            VisualizationCode = """
            var nums = new[] { 2, 7, 9, 3, 1 };
            int n = nums.Length;
            var grid = MatrixTracker.Create(new[] { nums.Select(x => x.ToString()).ToArray(), Enumerable.Repeat("", n).ToArray() },
                title: "198. House Robber: rob this house, or keep the best without it",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "money", "best so far" });

            int skipLast = 0, best = 0;
            var bestUpTo = new int[n];
            grid.Watch(() => skipLast);
            grid.Watch(() => best);
            for (int i = 0; i < n; i++)
            {
                int rob = skipLast + nums[i], skip = best;
                (skipLast, best) = (best, Math.Max(best, rob));
                bestUpTo[i] = best;
                grid.SetCell(1, i, val: best.ToString(), state: GridCellState.Current);
                grid.SetCell(0, i, state: GridCellState.Visited);
                grid.Snapshot(rob > skip
                    ? $"House {i}: robbing gives {nums[i]} + {rob - nums[i]} (best two houses back) = {rob}, more than skipping ({skip}): best = {best}"
                    : $"House {i}: robbing gives {rob}, skipping keeps {skip}: best stays {best}");
                grid.SetCell(1, i, state: GridCellState.Default);
                grid.SetCell(0, i, state: GridCellState.Default);
            }

            // Trace back which houses were robbed: a best that changed at house i means house i was taken.
            var robbed = new List<(int, int)>();
            for (int i = n - 1; i >= 0; )
            {
                if (i == 0 || bestUpTo[i] != bestUpTo[i - 1]) { robbed.Add((0, i)); i -= 2; }
                else i--;
            }
            grid.MarkPath(robbed, $"The best total is {best}: rob houses {string.Join(", ", robbed.Select(c => c.Item2).OrderBy(x => x))}", color: "#15803d");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_213_house_robber_ii",
            Number = 213,
            Title = "House Robber II",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 44.5,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Same as House Robber (problem 198), but the houses stand in a **circle**: the first and the last house are neighbours too. Return the most money you can take without robbing two neighbours.

            ### Example 1
            - **Input:** `nums = [2,3,2]`
            - **Output:** `3`
            - **Why:** houses 0 and 2 are neighbours on the circle, so you can't take both 2s.

            ### Example 2
            - **Input:** `nums = [1,2,3,1]`
            - **Output:** `4`
            - **Why:** `1 + 3` (houses 0 and 2).

            ### Example 3
            - **Input:** `nums = [1,2,3]`
            - **Output:** `3`

            ### Constraints
            - `1 <= nums.length <= 100`
            - `0 <= nums[i] <= 1000`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** House Robber, except the first and last houses can't both be robbed.

            1. **Break the circle.** Any valid plan leaves out the first house or leaves out the last house (it can't take both). So the answer is the better of:
               - House Robber on houses `0 .. n-2` (the last house is out), and
               - House Robber on houses `1 .. n-1` (the first house is out).
            2. **Each is the ordinary line problem,** solved with the two running totals from problem 198. Two passes: still `O(n)`.
            3. **Edge case:** one house → just take it (both ranges would be empty).
            4. **Walk `[2,7,9,3,1]`:** without the last: 2 + 9 = 11. Without the first: 7 + 3 = 10, or 9 + 1 = 10. Answer **11**, although the straight-line answer 12 (2 + 9 + 1) would rob two neighbours on the circle.

            **Pattern to remember:** a circular constraint often splits into two linear cases ("first excluded" / "last excluded"); solve each with the known method.

            **Common mistakes:** solving the line problem and subtracting something; forgetting `n = 1`; running both ranges over the whole array.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try every set of houses",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n · n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Each bitmask is a choice of houses; keep it only if no two chosen houses are neighbours around the circle, and take the richest.",
                    BottleneckExplanation = "The number of subsets doubles with every house; the two linear DP passes need one look at each house.",
                    Code = """
                    int RobCircleBruteForce(int[] nums)
                    {
                        int n = nums.Length, best = 0;
                        for (int mask = 0; mask < 1 << n; mask++)
                        {
                            bool ok = n == 1 || Enumerable.Range(0, n).All(i => (mask >> i & 1) == 0 || (mask >> ((i + 1) % n) & 1) == 0);
                            if (ok) best = Math.Max(best, Enumerable.Range(0, n).Where(i => (mask >> i & 1) == 1).Sum(i => nums[i]));
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(RobCircleBruteForce(new[] { 2, 3, 2 })));   // 3
                    """
                },
                new()
                {
                    Name = "Two straight-line House Robbers",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Solve the line problem once without the last house and once without the first; the better answer respects the circle."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int Rob(int[] nums)
                {
                    if (nums.Length == 1) return nums[0];
                    return Math.Max(RobLine(nums, 0, nums.Length - 2), RobLine(nums, 1, nums.Length - 1));   // never both ends
                }

                // Problem 198 on nums[from..to].
                private int RobLine(int[] nums, int from, int to)
                {
                    int skipLast = 0, best = 0;
                    for (int i = from; i <= to; i++)
                        (skipLast, best) = (best, Math.Max(best, skipLast + nums[i]));
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [2,3,2]", Expected = "3", Call = "sol.Rob(new[] { 2, 3, 2 })" },
                new() { Name = "Example 2", Input = "nums = [1,2,3,1]", Expected = "4", Call = "sol.Rob(new[] { 1, 2, 3, 1 })" },
                new() { Name = "Example 3", Input = "nums = [1,2,3]", Expected = "3", Call = "sol.Rob(new[] { 1, 2, 3 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One house", Input = "nums = [5]", Expected = "5", Call = "sol.Rob(new[] { 5 })" },
                new() { Name = "Two neighbours", Input = "nums = [1,1]", Expected = "1", Call = "sol.Rob(new[] { 1, 1 })" },
                new() { Name = "The ends touch", Input = "nums = [200,3,140,20,10]", Expected = "340", Call = "sol.Rob(new[] { 200, 3, 140, 20, 10 })" },
                new() { Name = "The line answer is illegal", Input = "nums = [2,7,9,3,1]", Expected = "11", Call = "sol.Rob(new[] { 2, 7, 9, 3, 1 })" }
            },
            StressTestCode = """
            judge.Agree("Random circles vs trying every set",
                random => Enumerable.Range(0, random.Next(1, 11)).Select(_ => random.Next(0, 20)).ToArray(),
                nums => RobCircleBruteForce(nums),
                nums => sol.Rob(nums));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `[2,7,9,3,1]` in a circle. The middle row runs House Robber without the last house, the bottom row without
            the first house; each cell is the best total so far. The straight-line answer 12 would rob houses 0 and 4,
            which are neighbours on the circle, so the answer is the better of the two rows' ends: 11.
            """,
            VisualizationCode = """
            var nums = new[] { 2, 7, 9, 3, 1 };
            int n = nums.Length;
            string[] Blank() => Enumerable.Repeat("", n).ToArray();
            var grid = MatrixTracker.Create(new[] { nums.Select(x => x.ToString()).ToArray(), Blank(), Blank() },
                title: "213. House Robber II: break the circle into two lines",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "money", "without last", "without first" });

            int RobLine(int from, int to, int row, string name)
            {
                int skipLast = 0, best = 0;
                for (int i = from; i <= to; i++)
                {
                    int rob = skipLast + nums[i];
                    (skipLast, best) = (best, Math.Max(best, rob));
                    grid.SetCell(row, i, val: best.ToString(), state: GridCellState.Current);
                    grid.Snapshot($"{name}, house {i}: rob it ({rob}) or skip it ({skipLast}): best so far {best}");
                    grid.SetCell(row, i, state: GridCellState.Default);
                }
                return best;
            }

            grid.SetCell(0, n - 1, state: GridCellState.Wall);
            grid.Snapshot($"Plan 1 leaves out the last house ({nums[n - 1]}), so houses 0 and {n - 1} can't both be robbed");
            int first = RobLine(0, n - 2, 1, "Without the last");
            grid.SetCell(0, n - 1, state: GridCellState.Default);
            grid.SetCell(0, 0, state: GridCellState.Wall);
            grid.Snapshot($"Plan 2 leaves out the first house ({nums[0]})");
            int second = RobLine(1, n - 1, 2, "Without the first");
            grid.SetCell(0, 0, state: GridCellState.Default);
            grid.SetCell(first >= second ? 1 : 2, first >= second ? n - 2 : n - 1, state: GridCellState.Path);
            grid.Snapshot($"The answer is the better plan: max({first}, {second}) = {Math.Max(first, second)}");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_05_longest_palindromic_substring",
            Number = 5,
            Title = "Longest Palindromic Substring",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 34.7,
            IsPremium = false,
            Tags = new List<string> { "Two Pointers", "String", "Dynamic Programming" },
            TimeComplexity = "O(n²)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a string `s`, return its longest **substring** (consecutive characters) that is a **palindrome**, i.e. reads the same forwards and backwards.

            ### Example 1
            - **Input:** `s = "babad"`
            - **Output:** `"bab"`
            - **Why:** `"aba"` is just as long; either is accepted, and this solution returns the one that starts first.

            ### Example 2
            - **Input:** `s = "cbbd"`
            - **Output:** `"bb"`

            ### Constraints
            - `1 <= s.length <= 1000`
            - `s` consists of digits and English letters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** among all substrings that mirror themselves, find the longest.

            1. **Brute force:** check every substring: `O(n²)` substrings × `O(n)` to check each = `O(n³)`.
            2. **A palindrome mirrors around its centre.** Pick a centre and grow outwards while the two ends match; the moment they differ, no longer palindrome can have that centre.
            3. **Centres come in two kinds:** on a letter (odd lengths like `"aba"`) and between two letters (even lengths like `"abba"`). That's `2n − 1` centres, each growing at most `n/2` steps: `O(n²)` time, `O(1)` memory.
            4. **The DP view:** `s[i..j]` is a palindrome when `s[i] == s[j]` and `s[i+1..j-1]` is one; filling that table is also `O(n²)` but needs `O(n²)` memory. Expanding from centres computes the same facts without storing them.
            5. **Walk `"babad"`:** the centre at index 1 (`a`) grows to `"bab"`; the centre at index 2 grows to `"aba"`, not longer; nothing reaches 4.

            **Pattern to remember:** palindrome problems → expand around centres (both kinds). Manacher's algorithm does it in `O(n)`, but interviews rarely require it.

            **Common mistakes:** forgetting even-length centres (`"cbbd"`); off-by-one when computing the start after the loop overshoots; building substrings inside the loop.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Check every substring",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n³)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Try all start and end positions (only ones longer than the best so far) and test each substring against its reverse.",
                    BottleneckExplanation = "Every check re-reads the whole substring, although growing from a centre reuses the part already known to match.",
                    Code = """
                    string LongestPalindromeBruteForce(string s)
                    {
                        string best = s[..1];
                        for (int i = 0; i < s.Length; i++)
                            for (int j = i + best.Length; j < s.Length; j++)   // only longer candidates can win
                            {
                                string candidate = s.Substring(i, j - i + 1);
                                if (candidate.SequenceEqual(candidate.Reverse())) best = candidate;
                            }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(LongestPalindromeBruteForce("babad")));   // "bab"
                    """
                },
                new()
                {
                    Name = "Table: is s[i..j] a palindrome?",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n²)",
                    RecurrenceRelation = "pal[i][j] = s[i] == s[j] && (j − i < 2 || pal[i + 1][j − 1])",
                    Intuition = "Fill the table by length: a substring is a palindrome when its ends match and its inside (shorter, already known) is one.",
                    Code = """
                    string LongestPalindromeDp(string s)
                    {
                        int n = s.Length, bestStart = 0, bestLength = 1;
                        var isPalindrome = new bool[n, n];
                        for (int length = 1; length <= n; length++)
                            for (int i = 0; i + length - 1 < n; i++)
                            {
                                int j = i + length - 1;
                                isPalindrome[i, j] = s[i] == s[j] && (length <= 2 || isPalindrome[i + 1, j - 1]);
                                if (isPalindrome[i, j] && length > bestLength) (bestStart, bestLength) = (i, length);
                            }
                        return s.Substring(bestStart, bestLength);
                    }

                    Console.WriteLine(Judge.Format(LongestPalindromeDp("cbbd")));   // "bb"
                    """
                },
                new()
                {
                    Name = "Expand around every centre",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "For each of the 2n − 1 centres (letters and gaps), grow outwards while the ends match and keep the longest window found."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public string LongestPalindrome(string s)
                {
                    int bestStart = 0, bestLength = 1;
                    for (int center = 0; center < s.Length; center++)
                    {
                        foreach (int right in new[] { center, center + 1 })   // odd length (on a letter), even length (between two)
                        {
                            int l = center, r = right;
                            while (l >= 0 && r < s.Length && s[l] == s[r]) { l--; r++; }   // grow while the ends match
                            if (r - l - 1 > bestLength) (bestStart, bestLength) = (l + 1, r - l - 1);
                        }
                    }
                    return s.Substring(bestStart, bestLength);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"babad\"", Expected = "\"bab\"", Call = "sol.LongestPalindrome(\"babad\")" },
                new() { Name = "Example 2", Input = "s = \"cbbd\"", Expected = "\"bb\"", Call = "sol.LongestPalindrome(\"cbbd\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One letter", Input = "s = \"a\"", Expected = "\"a\"", Call = "sol.LongestPalindrome(\"a\")" },
                new() { Name = "No repeats", Input = "s = \"ac\"", Expected = "\"a\"", Call = "sol.LongestPalindrome(\"ac\")" },
                new() { Name = "The whole string", Input = "s = \"racecar\"", Expected = "\"racecar\"", Call = "sol.LongestPalindrome(\"racecar\")" },
                new() { Name = "An even one inside", Input = "s = \"forgeeksskeegfor\"", Expected = "\"geeksskeeg\"", Call = "sol.LongestPalindrome(\"forgeeksskeegfor\")" },
                new() { Name = "All the same letter", Input = "s = \"aaaa\"", Expected = "\"aaaa\"", Call = "sol.LongestPalindrome(\"aaaa\")" }
            },
            StressTestCode = """
            judge.Agree("Random strings vs checking every substring",
                random => new string(Enumerable.Range(0, random.Next(1, 12)).Select(_ => "ab"[random.Next(2)]).ToArray()),
                s => LongestPalindromeBruteForce(s),
                s => sol.LongestPalindrome(s));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `"xabacabay"`. Each step is one centre, on a letter or between two, and highlights the palindrome it grows to
            between `l` and `r`; `best` keeps the longest. The centre on `c` grows three letters each way to `"abacaba"`.
            """,
            VisualizationCode = """
            var s = "xabacabay";
            var tracker = VisualizerRecorder.CreateArray(s, title: "5. Longest Palindromic Substring: grow from every centre");
            int bestStart = 0, bestLength = 1;
            string best = s[..1];
            tracker.Watch(() => best);

            for (int center = 0; center < s.Length; center++)
            {
                foreach (int right in new[] { center, center + 1 })
                {
                    int l = center, r = right;
                    while (l >= 0 && r < s.Length && s[l] == s[r]) { l--; r++; }
                    int length = r - l - 1;
                    string where = right == center ? $"on '{s[center]}' (index {center})" : $"between index {center} and {center + 1}";
                    if (length == 0)
                    {
                        if (right < s.Length) tracker.Step($"Centre {where}: '{s[center]}' and '{s[right]}' differ, so no even palindrome here", pointers: new { l = center, r = right });
                        continue;
                    }
                    bool record = length > bestLength;
                    if (record)
                    {
                        (bestStart, bestLength) = (l + 1, length);
                        best = s.Substring(bestStart, bestLength);
                    }
                    tracker.Step($"Centre {where} grows to \"{s.Substring(l + 1, length)}\" ({length}){(record ? ", the longest so far" : "")}",
                        pointers: new { l = l + 1, r = r - 1 }, highlight: Enumerable.Range(l + 1, length));
                }
            }

            tracker.Step($"Every centre has been tried: \"{best}\"", highlight: Enumerable.Range(bestStart, bestLength));
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_647_palindromic_substrings",
            Number = 647,
            Title = "Palindromic Substrings",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 72.1,
            IsPremium = false,
            Tags = new List<string> { "Two Pointers", "String", "Dynamic Programming" },
            TimeComplexity = "O(n²)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a string `s`, return how many of its **substrings** are palindromes. Substrings at different positions count separately, even if they contain the same letters.

            ### Example 1
            - **Input:** `s = "abc"`
            - **Output:** `3`
            - **Why:** `"a"`, `"b"`, `"c"`.

            ### Example 2
            - **Input:** `s = "aaa"`
            - **Output:** `6`
            - **Why:** three `"a"`, two `"aa"` and one `"aaa"`.

            ### Constraints
            - `1 <= s.length <= 1000`
            - `s` consists of lowercase English letters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count the pairs `(i, j)` for which `s[i..j]` reads the same both ways.

            1. **Brute force:** test all `O(n²)` substrings, each in `O(n)`: `O(n³)`.
            2. **Count while expanding from centres** (as in problem 5). From a centre, every step outwards where the ends still match is **one more palindrome**: `"b"`, then `"aba"`, then `"cabac"` … and the first mismatch ends that centre.
            3. **Both kinds of centre:** on a letter (odd lengths) and between two letters (even lengths). `2n − 1` centres, `O(n²)` in total, `O(1)` memory.
            4. **Walk `"aaa"`:** centre on each letter: `"a"` ×3, plus `"aaa"` from the middle. Between letters: `"aa"` ×2. Total **6**.

            **Pattern to remember:** when every successful step of an expansion is itself an answer, count the steps instead of only keeping the best.

            **Common mistakes:** counting distinct palindromes instead of positions; forgetting even-length centres; double counting by visiting a centre twice.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Test every substring",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n³)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Enumerate all start/end pairs and compare each substring with its reverse.",
                    BottleneckExplanation = "Neighbouring substrings are checked from scratch; expanding from a centre reuses the part already known to match.",
                    Code = """
                    int CountPalindromesBruteForce(string s)
                    {
                        int count = 0;
                        for (int i = 0; i < s.Length; i++)
                            for (int j = i; j < s.Length; j++)
                            {
                                string candidate = s.Substring(i, j - i + 1);
                                if (candidate.SequenceEqual(candidate.Reverse())) count++;
                            }
                        return count;
                    }

                    Console.WriteLine(Judge.Format(CountPalindromesBruteForce("aaa")));   // 6
                    """
                },
                new()
                {
                    Name = "Table of palindromes by length",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n²)",
                    RecurrenceRelation = "pal[i][j] = s[i] == s[j] && (j − i < 2 || pal[i + 1][j − 1])\nanswer = number of true entries",
                    Intuition = "Fill pal[i][j] for growing lengths from the already-known inside, and count the true entries.",
                    Code = """
                    int CountPalindromesDp(string s)
                    {
                        int n = s.Length, count = 0;
                        var isPalindrome = new bool[n, n];
                        for (int length = 1; length <= n; length++)
                            for (int i = 0; i + length - 1 < n; i++)
                            {
                                int j = i + length - 1;
                                isPalindrome[i, j] = s[i] == s[j] && (length <= 2 || isPalindrome[i + 1, j - 1]);
                                if (isPalindrome[i, j]) count++;
                            }
                        return count;
                    }

                    Console.WriteLine(Judge.Format(CountPalindromesDp("abc")));   // 3
                    """
                },
                new()
                {
                    Name = "Count while expanding around every centre",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Every successful outward step from a centre is one more palindromic substring."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int CountSubstrings(string s)
                {
                    int count = 0;
                    for (int center = 0; center < s.Length; center++)
                        foreach (int right in new[] { center, center + 1 })   // on a letter, and between two letters
                            for (int l = center, r = right; l >= 0 && r < s.Length && s[l] == s[r]; l--, r++)
                                count++;                                       // s[l..r] is one more palindrome
                    return count;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"abc\"", Expected = "3", Call = "sol.CountSubstrings(\"abc\")" },
                new() { Name = "Example 2", Input = "s = \"aaa\"", Expected = "6", Call = "sol.CountSubstrings(\"aaa\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One letter", Input = "s = \"a\"", Expected = "1", Call = "sol.CountSubstrings(\"a\")" },
                new() { Name = "An even palindrome", Input = "s = \"abba\"", Expected = "6", Call = "sol.CountSubstrings(\"abba\")" },
                new() { Name = "An odd palindrome", Input = "s = \"aba\"", Expected = "4", Call = "sol.CountSubstrings(\"aba\")" },
                new() { Name = "Nested even ones", Input = "s = \"abccba\"", Expected = "9", Call = "sol.CountSubstrings(\"abccba\")" }
            },
            StressTestCode = """
            judge.Agree("Random strings vs testing every substring",
                random => new string(Enumerable.Range(0, random.Next(1, 12)).Select(_ => "ab"[random.Next(2)]).ToArray()),
                s => CountPalindromesBruteForce(s),
                s => sol.CountSubstrings(s));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `"aabaa"`. Every step is one palindrome found while growing from a centre (highlighted between `l` and `r`),
            and `count` goes up by one each time: 5 single letters, `"aa"` twice, `"aba"` and `"aabaa"`, 9 in total.
            """,
            VisualizationCode = """
            var s = "aabaa";
            var tracker = VisualizerRecorder.CreateArray(s, title: "647. Palindromic Substrings: every step outwards is one more");
            int count = 0;
            tracker.Watch(() => count);

            for (int center = 0; center < s.Length; center++)
                foreach (int right in new[] { center, center + 1 })
                    for (int l = center, r = right; l >= 0 && r < s.Length && s[l] == s[r]; l--, r++)
                    {
                        count++;
                        string kind = right == center ? $"centred on index {center}" : $"centred between {center} and {center + 1}";
                        tracker.Step($"\"{s.Substring(l, r - l + 1)}\" ({kind}) reads the same both ways: count = {count}",
                            pointers: new { l, r }, highlight: Enumerable.Range(l, r - l + 1));
                    }

            tracker.Step($"Every centre has stopped growing: {count} palindromic substrings");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_91_decode_ways",
            Number = 91,
            Title = "Decode Ways",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 35.7,
            IsPremium = false,
            Tags = new List<string> { "String", "Dynamic Programming" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Letters are encoded as numbers: `A → 1`, `B → 2`, …, `Z → 26`. Given a string of digits `s`, return how many ways it can be **decoded** back into letters.

            A piece must be a number from 1 to 26 written without a leading zero: `"06"` is not `F`.

            ### Example 1
            - **Input:** `s = "12"`
            - **Output:** `2`
            - **Why:** `"AB"` (1 2) or `"L"` (12).

            ### Example 2
            - **Input:** `s = "226"`
            - **Output:** `3`
            - **Why:** `"BZ"` (2 26), `"VF"` (22 6), `"BBF"` (2 2 6).

            ### Example 3
            - **Input:** `s = "06"`
            - **Output:** `0`
            - **Why:** a piece can't start with 0, and `0` alone is not a letter.

            ### Constraints
            - `1 <= s.length <= 100`
            - `s` contains only digits and may contain leading zeros.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** in how many ways can the digit string be cut into pieces from `1` to `26`?

            1. **Look at the last piece.** A decoding of the first `i` digits ends with either
               - a **one-digit** letter `s[i-1]`, allowed unless it's `'0'` → then the rest is any decoding of the first `i - 1` digits, or
               - a **two-digit** letter `s[i-2..i-1]`, allowed when it's between `10` and `26` → the rest is a decoding of the first `i - 2` digits.
            2. **So** `ways[i] = (s[i-1] != '0' ? ways[i-1] : 0) + (10 <= two digits <= 26 ? ways[i-2] : 0)`, with `ways[0] = 1` (the empty prefix has one decoding: nothing).
            3. **Zeros are what make it tricky:** a `0` can only be the second digit of `10` or `20`. `"100"` has no decoding at all.
            4. **Only the last two entries matter,** so two variables do: `O(n)` time, `O(1)` memory.
            5. **Walk `"226"`:** ways = 1 (empty), 1 (`2`), 2 (`2 2`, `22`), 3 (`… 6` from 2 ways, plus `26` from 1 way).

            **Pattern to remember:** "count the ways to split a sequence" → DP over prefixes where the last piece has a few possible sizes (like Climbing Stairs, with validity checks).

            **Common mistakes:** counting `'0'` as a letter; accepting `"05"` as a two-digit piece; forgetting that `27`–`99` are not letters; starting with `ways[0] = 0`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try one or two digits, recursively",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "From position i, take one digit (if it isn't 0) or two digits (if they form 10–26) and count the ways to decode the rest.",
                    BottleneckExplanation = "The rest from each position is recounted along many paths; a table computes each position once.",
                    Code = """
                    int DecodeRecursively(string s, int i = 0)
                    {
                        if (i == s.Length) return 1;          // decoded everything
                        if (s[i] == '0') return 0;             // no letter starts with 0
                        int ways = DecodeRecursively(s, i + 1);
                        if (i + 1 < s.Length && int.Parse(s.Substring(i, 2)) <= 26) ways += DecodeRecursively(s, i + 2);
                        return ways;
                    }

                    Console.WriteLine(Judge.Format(DecodeRecursively("226")));   // 3
                    """
                },
                new()
                {
                    Name = "Table over prefixes",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "ways[0] = 1\nways[i] = (s[i−1] ≠ '0' ? ways[i−1] : 0) + (10 ≤ s[i−2..i−1] ≤ 26 ? ways[i−2] : 0)",
                    Intuition = "ways[i] counts the decodings of the first i digits: the last letter used one digit or two.",
                    Code = """
                    int DecodeWithTable(string s)
                    {
                        var ways = new int[s.Length + 1];
                        ways[0] = 1;
                        for (int i = 1; i <= s.Length; i++)
                        {
                            if (s[i - 1] != '0') ways[i] += ways[i - 1];
                            if (i >= 2 && s[i - 2] != '0' && int.Parse(s.Substring(i - 2, 2)) <= 26) ways[i] += ways[i - 2];
                        }
                        return ways[s.Length];
                    }

                    Console.WriteLine(Judge.Format(DecodeWithTable("11106")));   // 2
                    """
                },
                new()
                {
                    Name = "Two running counts",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Keep the counts for the prefixes one and two digits shorter; each new digit combines them according to the one- and two-digit rules."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int NumDecodings(string s)
                {
                    int twoBack = 1, oneBack = s[0] == '0' ? 0 : 1;   // decodings of the first 0 and the first 1 digits
                    for (int i = 2; i <= s.Length; i++)
                    {
                        int ways = 0;
                        if (s[i - 1] != '0') ways += oneBack;                             // last letter uses one digit (1-9)
                        int pair = (s[i - 2] - '0') * 10 + (s[i - 1] - '0');
                        if (pair >= 10 && pair <= 26) ways += twoBack;                    // last letter uses two digits (10-26)
                        (twoBack, oneBack) = (oneBack, ways);
                    }
                    return oneBack;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"12\"", Expected = "2", Call = "sol.NumDecodings(\"12\")" },
                new() { Name = "Example 2", Input = "s = \"226\"", Expected = "3", Call = "sol.NumDecodings(\"226\")" },
                new() { Name = "Example 3", Input = "s = \"06\"", Expected = "0", Call = "sol.NumDecodings(\"06\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Just a zero", Input = "s = \"0\"", Expected = "0", Call = "sol.NumDecodings(\"0\")" },
                new() { Name = "Ten is one letter", Input = "s = \"10\"", Expected = "1", Call = "sol.NumDecodings(\"10\")" },
                new() { Name = "27 is too big", Input = "s = \"27\"", Expected = "1", Call = "sol.NumDecodings(\"27\")" },
                new() { Name = "A zero in the middle", Input = "s = \"11106\"", Expected = "2", Call = "sol.NumDecodings(\"11106\")" },
                new() { Name = "Two zeros in a row", Input = "s = \"100\"", Expected = "0", Call = "sol.NumDecodings(\"100\")" }
            },
            StressTestCode = """
            judge.Agree("Random digit strings vs trying every split",
                random => new string(Enumerable.Range(0, random.Next(1, 11)).Select(_ => "01267"[random.Next(5)]).ToArray()),
                s => DecodeRecursively(s),
                s => sol.NumDecodings(s));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `"11106"`. The bottom row counts the decodings of each prefix (the first column is the empty prefix). Each
            step checks whether the newest digit can stand alone and whether the last two digits form 10–26, and adds the
            counts they point back to. The `0` can't stand alone, so only `10` keeps the count alive.
            """,
            VisualizationCode = """
            var s = "11106";
            int n = s.Length;
            var grid = MatrixTracker.Create(new[] { new[] { "∅" }.Concat(s.Select(c => c.ToString())).ToArray(), Enumerable.Repeat("", n + 1).ToArray() },
                title: "91. Decode Ways: the last letter uses one digit or two",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "digits", "ways" });
            var ways = new int[n + 1];
            ways[0] = 1;
            grid.SetCell(1, 0, val: "1");
            grid.Snapshot("ways[0] = 1: the empty prefix has exactly one decoding, nothing at all");

            for (int i = 1; i <= n; i++)
            {
                var parts = new List<string>();
                if (s[i - 1] != '0')
                {
                    ways[i] += ways[i - 1];
                    parts.Add($"'{s[i - 1]}' alone is a letter (+{ways[i - 1]})");
                }
                else parts.Add("'0' alone is not a letter");
                if (i >= 2)
                {
                    int pair = (s[i - 2] - '0') * 10 + (s[i - 1] - '0');
                    if (pair >= 10 && pair <= 26)
                    {
                        ways[i] += ways[i - 2];
                        parts.Add($"\"{s[i - 2]}{s[i - 1]}\" = {pair} is a letter too (+{ways[i - 2]})");
                    }
                    else parts.Add($"\"{s[i - 2]}{s[i - 1]}\" is not 10–26");
                }

                grid.SetCell(1, i, val: ways[i].ToString(), state: GridCellState.Current);
                grid.SetCell(0, i, state: GridCellState.Visited);
                if (i >= 2) grid.SetCell(0, i - 1, state: GridCellState.Visited);
                grid.Snapshot($"\"{s[..i]}\": {string.Join("; ", parts)}. ways[{i}] = {ways[i]}");
                grid.SetCell(1, i, state: GridCellState.Default);
                grid.SetCell(0, i, state: GridCellState.Default);
                if (i >= 2) grid.SetCell(0, i - 1, state: GridCellState.Default);
            }

            grid.SetCell(1, n, state: GridCellState.Path);
            grid.Snapshot($"\"{s}\" can be decoded in {ways[n]} ways");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_322_coin_change",
            Number = 322,
            Title = "Coin Change",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 45.2,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming", "BFS" },
            TimeComplexity = "O(amount · coins)",
            SpaceComplexity = "O(amount)",
            DescriptionMarkdown = """
            You have coins of the values in `coins` (as many of each as you like) and a target `amount`. Return the **fewest coins** that add up to `amount`, or `-1` if it can't be made.

            ### Example 1
            - **Input:** `coins = [1,2,5], amount = 11`
            - **Output:** `3`
            - **Why:** `5 + 5 + 1`.

            ### Example 2
            - **Input:** `coins = [2], amount = 3`
            - **Output:** `-1`

            ### Example 3
            - **Input:** `coins = [1], amount = 0`
            - **Output:** `0`

            ### Constraints
            - `1 <= coins.length <= 12`
            - `1 <= coins[i] <= 2^31 - 1`
            - `0 <= amount <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** make `amount` out of the coin values using as few coins as possible.

            1. **Greedy is tempting and wrong.** "Always take the biggest coin that fits" works for real-world coins, but with `[1,3,4]` and 6 it takes `4 + 1 + 1` (3 coins) while `3 + 3` needs only 2.
            2. **Think about the last coin.** If the last coin used is `c`, the rest is the best way to make `amount - c`. So `fewest(a) = 1 + min over coins c ≤ a of fewest(a - c)`, with `fewest(0) = 0`.
            3. **Plain recursion repeats itself** (`fewest(7)` is reached from 8, 9, 12 …). Memoize it, or fill a table `fewest[0..amount]` from small to large: each entry looks at one smaller entry per coin. `O(amount · coins)`.
            4. **Unreachable amounts** stay "infinite"; if `fewest[amount]` is still infinite at the end, answer `-1`.
            5. **Walk `[1,3,4]`, 6:** fewest = 0, 1, 2, 1 (3), 1 (4), 2 (4+1), **2** (3+3).

            **Pattern to remember:** "minimum number of items to reach a total, unlimited reuse" = unbounded knapsack: `dp[a] = min(dp[a - item] + 1)`. When a greedy choice might be wrong, check it on a counterexample before trusting it.

            **Common mistakes:** trusting greedy; adding 1 to "infinity" (overflow) instead of skipping unreachable amounts; forgetting `amount = 0` → 0.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try every last coin, recursively",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(coins^(amount / smallest coin))",
                    SpaceComplexity = "O(amount) call stack",
                    Intuition = "The fewest coins for a is 1 + the fewest for a − c, minimised over every coin c that fits.",
                    BottleneckExplanation = "The same smaller amounts are solved again and again along different coin orders: exponential.",
                    Code = """
                    int CoinChangeRecursively(int[] coins, int amount)
                    {
                        if (amount == 0) return 0;
                        int best = -1;
                        foreach (int coin in coins)
                        {
                            if (coin > amount) continue;
                            int rest = CoinChangeRecursively(coins, amount - coin);
                            if (rest >= 0 && (best < 0 || rest + 1 < best)) best = rest + 1;
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(CoinChangeRecursively(new[] { 1, 2, 5 }, 11)));   // 3
                    """
                },
                new()
                {
                    Name = "Greedy: biggest coin first (wrong!)",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(coins log coins)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Does not hold for arbitrary coins: the biggest coin can force extra small ones ([1,3,4] for 6 gives 4 + 1 + 1).",
                    Intuition = "Take as many of the biggest coin as fit, then the next biggest, and so on.",
                    BottleneckExplanation = "It is not always correct: with [1,3,4] and 6 it answers 3 (4 + 1 + 1) instead of 2 (3 + 3).",
                    Code = """
                    int CoinChangeGreedy(int[] coins, int amount)
                    {
                        int count = 0;
                        foreach (int coin in coins.OrderByDescending(c => c))
                        {
                            count += amount / coin;   // as many of the biggest coin as fit
                            amount %= coin;
                        }
                        return amount == 0 ? count : -1;
                    }

                    Console.WriteLine(Judge.Format(CoinChangeGreedy(new[] { 1, 3, 4 }, 6)));   // 3, but 3 + 3 needs only 2
                    """
                },
                new()
                {
                    Name = "Memoized recursion",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(amount · coins)",
                    SpaceComplexity = "O(amount)",
                    RecurrenceRelation = "fewest(0) = 0\nfewest(a) = 1 + min over coins c ≤ a of fewest(a − c)   (unreachable if every option is)",
                    Intuition = "The recursive definition, but each amount is solved once and remembered.",
                    Code = """
                    int CoinChangeMemo(int[] coins, int amount)
                    {
                        var memo = new Dictionary<int, int>();
                        int Fewest(int left)
                        {
                            if (left == 0) return 0;
                            if (memo.TryGetValue(left, out int known)) return known;
                            int best = -1;
                            foreach (int coin in coins)
                            {
                                if (coin > left) continue;
                                int rest = Fewest(left - coin);
                                if (rest >= 0 && (best < 0 || rest + 1 < best)) best = rest + 1;
                            }
                            return memo[left] = best;
                        }
                        return Fewest(amount);
                    }

                    Console.WriteLine(Judge.Format(CoinChangeMemo(new[] { 1, 3, 4 }, 6)));   // 2
                    """
                },
                new()
                {
                    Name = "Bottom-up table of fewest coins",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(amount · coins)",
                    SpaceComplexity = "O(amount)",
                    Intuition = "Fill fewest[a] for a = 1 … amount; each entry tries every coin as the last one and uses the already-known smaller entry."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int CoinChange(int[] coins, int amount)
                {
                    var fewest = new int[amount + 1];
                    Array.Fill(fewest, int.MaxValue);   // "can't be made yet"
                    fewest[0] = 0;                      // zero coins make 0
                    for (int a = 1; a <= amount; a++)
                        foreach (int coin in coins)
                            if (coin <= a && fewest[a - coin] != int.MaxValue)
                                fewest[a] = Math.Min(fewest[a], fewest[a - coin] + 1);   // `coin` is the last coin used
                    return fewest[amount] == int.MaxValue ? -1 : fewest[amount];
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "coins = [1,2,5], amount = 11", Expected = "3", Call = "sol.CoinChange(new[] { 1, 2, 5 }, 11)" },
                new() { Name = "Example 2", Input = "coins = [2], amount = 3", Expected = "-1", Call = "sol.CoinChange(new[] { 2 }, 3)" },
                new() { Name = "Example 3", Input = "coins = [1], amount = 0", Expected = "0", Call = "sol.CoinChange(new[] { 1 }, 0)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Greedy would be wrong", Input = "coins = [1,3,4], amount = 6", Expected = "2", Call = "sol.CoinChange(new[] { 1, 3, 4 }, 6)" },
                new() { Name = "Too small for any coin", Input = "coins = [5,10], amount = 3", Expected = "-1", Call = "sol.CoinChange(new[] { 5, 10 }, 3)" },
                new() { Name = "Two of one coin", Input = "coins = [2], amount = 4", Expected = "2", Call = "sol.CoinChange(new[] { 2 }, 4)" },
                new() { Name = "A big amount", Input = "coins = [186,419,83,408], amount = 6249", Expected = "20", Call = "sol.CoinChange(new[] { 186, 419, 83, 408 }, 6249)" }
            },
            StressTestCode = """
            judge.Agree("Random coins and amounts vs memoized recursion",
                random => (coins: Enumerable.Range(1, 12).OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToArray(), amount: random.Next(0, 40)),
                input => CoinChangeMemo(input.coins, input.amount),
                input => sol.CoinChange(input.coins, input.amount));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `coins = [1,3,4]`, `amount = 6`, the case where greedy fails. Cell `a` holds the fewest coins that make `a`
            (∞ = can't be made yet). For each amount, every coin is tried as the last coin: `fewest[a − coin] + 1`, and
            the smaller count wins. The table ends at 2 (3 + 3), where greedy would have used 4 + 1 + 1.
            """,
            VisualizationCode = """
            int[] coins = { 1, 3, 4 };
            int amount = 6;
            var fewest = new int[amount + 1];
            Array.Fill(fewest, int.MaxValue);
            fewest[0] = 0;
            var lastCoin = new int[amount + 1];
            var tracker = VisualizerRecorder.CreateArray(fewest, title: "322. Coin Change: the fewest coins for every amount");
            tracker.Step("fewest[0] = 0; every other amount starts as ∞ (not made yet)", highlight: new[] { 0 });

            for (int a = 1; a <= amount; a++)
                foreach (int coin in coins)
                {
                    if (coin > a || fewest[a - coin] == int.MaxValue) continue;
                    int candidate = fewest[a - coin] + 1;
                    bool better = candidate < fewest[a];
                    if (better) (fewest[a], lastCoin[a]) = (candidate, coin);
                    tracker.Step(better
                            ? $"Amount {a} with a {coin} last: fewest[{a - coin}] + 1 = {candidate}, the best so far"
                            : $"Amount {a} with a {coin} last: fewest[{a - coin}] + 1 = {candidate}, no better than {fewest[a]}",
                        pointers: new { a, rest = a - coin }, highlight: new[] { a - coin, a });
                }

            var used = new List<int>();
            for (int a = amount; a > 0; a -= lastCoin[a]) used.Add(lastCoin[a]);
            tracker.Step($"fewest[{amount}] = {fewest[amount]}: {string.Join(" + ", used)}. Greedy (biggest coin first) would take 4 + 1 + 1 = 3 coins", highlight: new[] { amount });
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_152_maximum_product_subarray",
            Number = 152,
            Title = "Maximum Product Subarray",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 37.1,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `nums`, find a non-empty **subarray** (consecutive elements) with the **largest product**, and return that product.

            ### Example 1
            - **Input:** `nums = [2,3,-2,4]`
            - **Output:** `6`
            - **Why:** `[2,3]`; including the -2 makes the product negative.

            ### Example 2
            - **Input:** `nums = [-2,0,-1]`
            - **Output:** `0`
            - **Why:** `[-2,-1]` isn't a subarray (the 0 sits between them), and every other product is at most 0.

            ### Constraints
            - `1 <= nums.length <= 2 * 10^4`
            - `-10 <= nums[i] <= 10`
            - The product of any subarray fits in a 32-bit integer.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** like Maximum Subarray (problem 53), but multiplying, which makes signs matter.

            1. **Brute force:** every subarray's product: `O(n²)`.
            2. **Kadane's idea needs a twist.** For sums, the best subarray ending at `i` extends the best one ending at `i - 1` or restarts. For products, a **negative** number turns the *smallest* (most negative) product into the *largest*: `-12 × -1 = 12`.
            3. **So track both extremes** ending at each position: `maxHere` and `minHere`. With the next number `x`, the new extremes are among `x` (restart), `x · maxHere` and `x · minHere`.
            4. **Zeros reset everything:** the only subarray ending at a zero that can win is the zero itself, and the next number restarts cleanly.
            5. **Walk `[2,3,-2,4,-1]`:** (max, min) = (2,2), (6,3), (-2,-12), (4,-48), then `-1 × -48 = 48` → **48**.

            **Pattern to remember:** when a transition can flip the order (multiplying by negatives), keep both the best and the worst state.

            **Common mistakes:** tracking only the maximum; updating `maxHere` before using its old value for `minHere` (use a tuple or a temporary); starting `best` at 0 (the answer can be negative, e.g. `[-2]`).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Every subarray's product",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "For each start, multiply forward one element at a time and keep the largest product seen.",
                    BottleneckExplanation = "Each start recomputes products the previous start already knew; tracking the extremes ending at each position is linear.",
                    Code = """
                    int MaxProductBruteForce(int[] nums)
                    {
                        int best = nums[0];
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int product = 1;
                            for (int j = i; j < nums.Length; j++)
                            {
                                product *= nums[j];
                                best = Math.Max(best, product);
                            }
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(MaxProductBruteForce(new[] { 2, 3, -2, 4 })));   // 6
                    """
                },
                new()
                {
                    Name = "Largest and smallest product ending here",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    RecurrenceRelation = "maxHere[i] = max(x, x · maxHere[i−1], x · minHere[i−1])\nminHere[i] = min(x, x · maxHere[i−1], x · minHere[i−1])\nanswer = max over i of maxHere[i]",
                    Intuition = "Keep the extremes of the products ending at each position; a negative number swaps their roles, so both are needed."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MaxProduct(int[] nums)
                {
                    int best = nums[0], maxHere = nums[0], minHere = nums[0];   // extremes of the products ending here
                    for (int i = 1; i < nums.Length; i++)
                    {
                        int x = nums[i];
                        (maxHere, minHere) = (Math.Max(x, Math.Max(x * maxHere, x * minHere)),   // a negative x turns the
                                              Math.Min(x, Math.Min(x * maxHere, x * minHere)));  // smallest into the largest
                        best = Math.Max(best, maxHere);
                    }
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [2,3,-2,4]", Expected = "6", Call = "sol.MaxProduct(new[] { 2, 3, -2, 4 })" },
                new() { Name = "Example 2", Input = "nums = [-2,0,-1]", Expected = "0", Call = "sol.MaxProduct(new[] { -2, 0, -1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One negative number", Input = "nums = [-2]", Expected = "-2", Call = "sol.MaxProduct(new[] { -2 })" },
                new() { Name = "Two negatives cancel", Input = "nums = [-2,3,-4]", Expected = "24", Call = "sol.MaxProduct(new[] { -2, 3, -4 })" },
                new() { Name = "After a zero", Input = "nums = [0,2]", Expected = "2", Call = "sol.MaxProduct(new[] { 0, 2 })" },
                new() { Name = "Skip the first negative", Input = "nums = [2,-5,-2,-4,3]", Expected = "24", Call = "sol.MaxProduct(new[] { 2, -5, -2, -4, 3 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs every subarray",
                random => Enumerable.Range(0, random.Next(1, 10)).Select(_ => random.Next(-4, 5)).ToArray(),
                nums => MaxProductBruteForce(nums),
                nums => sol.MaxProduct(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `[2,3,-2,4,-1]`. At each index, `maxHere` and `minHere` are the largest and smallest products of a subarray
            ending there. The -2 makes the smallest product -12, and the -1 at the end flips -48 into 48, the answer.
            """,
            VisualizationCode = """
            var nums = new[] { 2, 3, -2, 4, -1 };
            var tracker = VisualizerRecorder.CreateArray(nums, title: "152. Maximum Product Subarray: keep the largest and the smallest");
            int best = nums[0], maxHere = nums[0], minHere = nums[0];
            tracker.Watch(() => maxHere);
            tracker.Watch(() => minHere);
            tracker.Watch(() => best);
            tracker.Step($"Start at {nums[0]}: the only subarray ending here is [{nums[0]}]", pointers: new { i = 0 });

            for (int i = 1; i < nums.Length; i++)
            {
                int x = nums[i];
                int fromMax = x * maxHere, fromMin = x * minHere;
                (maxHere, minHere) = (Math.Max(x, Math.Max(fromMax, fromMin)), Math.Min(x, Math.Min(fromMax, fromMin)));
                bool record = maxHere > best;
                best = Math.Max(best, maxHere);
                tracker.Step($"x = {x}: candidates {x} (restart), {x}·max = {fromMax}, {x}·min = {fromMin}. New max {maxHere}, new min {minHere}{(record ? ", a new best" : "")}",
                    pointers: new { i });
            }

            tracker.Step($"The largest product of any subarray is {best}");
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_139_word_break",
            Number = 139,
            Title = "Word Break",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 47.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "String", "Dynamic Programming", "Trie", "Memoization" },
            TimeComplexity = "O(n² · L) with L the longest word, for the substring checks",
            SpaceComplexity = "O(n + total dictionary size)",
            DescriptionMarkdown = """
            Given a string `s` and a dictionary `wordDict`, return `true` if `s` can be split into a sequence of one or more dictionary words (words may be reused).

            ### Example 1
            - **Input:** `s = "leetcode", wordDict = ["leet","code"]`
            - **Output:** `true`

            ### Example 2
            - **Input:** `s = "applepenapple", wordDict = ["apple","pen"]`
            - **Output:** `true`
            - **Why:** `apple pen apple`; reusing `apple` is fine.

            ### Example 3
            - **Input:** `s = "catsandog", wordDict = ["cats","dog","sand","and","cat"]`
            - **Output:** `false`
            - **Why:** `cats and` and `cat sand` both leave `og`, which isn't a word.

            ### Constraints
            - `1 <= s.length <= 300`
            - `1 <= wordDict.length <= 1000`, `1 <= wordDict[i].length <= 20`, all words distinct.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** can the string be cut into pieces that are all dictionary words?

            1. **Try the first word, then solve the rest:** that recursion is exponential for inputs like `"aaaaaaaaab"` with words `a, aa, aaa …`, because the same suffix is re-solved over and over.
            2. **Ask a smaller question per prefix:** `canSplit[i]` = can the first `i` letters be split into words? The empty prefix can: `canSplit[0] = true`.
            3. **The last word decides it:** the first `i` letters split exactly when some `j < i` has `canSplit[j]` true **and** `s[j..i]` is a word.
            4. **Fill it left to right** and answer `canSplit[n]`. With a hash set of words that's `O(n²)` checks (you can limit `j` to the longest word's length).
            5. **Walk Example 3:** `cat` → `canSplit[3]`, `cats` → `[4]`, `cat|sand` and `cats|and` → `[7]`. For `[9]` the last word would be `og`, `g` or `dog`, but `dog` would need `canSplit[6]`, which is false → `false`.

            **Pattern to remember:** "can the sequence be split into valid pieces?" → a boolean DP over prefixes where each state looks back at every possible last piece.

            **Common mistakes:** a greedy longest-word-first split (`cars` with `car, ca, rs` needs `ca|rs`); recursion without memo; forgetting `canSplit[0] = true`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try every first word, recursively",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "If some prefix is a word and the rest can be split, the whole string can be split.",
                    BottleneckExplanation = "The same suffix is re-solved for every way of reaching it; remembering each prefix's answer makes it polynomial.",
                    Code = """
                    bool WordBreakRecursively(string s, IList<string> wordDict) =>
                        s.Length == 0 || wordDict.Any(word => s.StartsWith(word) && WordBreakRecursively(s[word.Length..], wordDict));

                    Console.WriteLine(Judge.Format(WordBreakRecursively("catsandog", new[] { "cats", "dog", "sand", "and", "cat" })));   // false
                    """
                },
                new()
                {
                    Name = "Which prefixes can be split? (bottom-up)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n² · L)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "canSplit[0] = true\ncanSplit[i] = any j < i with canSplit[j] and s[j..i] in the dictionary",
                    Intuition = "Fill canSplit for longer and longer prefixes: a prefix splits when a shorter one does and the letters in between form a word."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool WordBreak(string s, IList<string> wordDict)
                {
                    var words = new HashSet<string>(wordDict);
                    var canSplit = new bool[s.Length + 1];   // canSplit[i]: the first i letters split into words
                    canSplit[0] = true;                       // the empty prefix needs no words
                    for (int i = 1; i <= s.Length; i++)
                        for (int j = 0; j < i && !canSplit[i]; j++)
                            canSplit[i] = canSplit[j] && words.Contains(s[j..i]);   // split up to j, then one word s[j..i]
                    return canSplit[s.Length];
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"leetcode\", wordDict = [\"leet\",\"code\"]", Expected = "true", Call = "sol.WordBreak(\"leetcode\", new[] { \"leet\", \"code\" })" },
                new() { Name = "Example 2", Input = "s = \"applepenapple\", wordDict = [\"apple\",\"pen\"]", Expected = "true", Call = "sol.WordBreak(\"applepenapple\", new[] { \"apple\", \"pen\" })" },
                new() { Name = "Example 3", Input = "s = \"catsandog\", wordDict = [\"cats\",\"dog\",\"sand\",\"and\",\"cat\"]", Expected = "false", Call = "sol.WordBreak(\"catsandog\", new[] { \"cats\", \"dog\", \"sand\", \"and\", \"cat\" })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One word", Input = "s = \"a\", wordDict = [\"a\"]", Expected = "true", Call = "sol.WordBreak(\"a\", new[] { \"a\" })" },
                new() { Name = "A letter left over", Input = "s = \"ab\", wordDict = [\"a\"]", Expected = "false", Call = "sol.WordBreak(\"ab\", new[] { \"a\" })" },
                new() { Name = "Mixing word lengths", Input = "s = \"aaaaaaa\", wordDict = [\"aaaa\",\"aaa\"]", Expected = "true", Call = "sol.WordBreak(\"aaaaaaa\", new[] { \"aaaa\", \"aaa\" })" },
                new() { Name = "Longest word first fails", Input = "s = \"cars\", wordDict = [\"car\",\"ca\",\"rs\"]", Expected = "true", Call = "sol.WordBreak(\"cars\", new[] { \"car\", \"ca\", \"rs\" })" }
            },
            StressTestCode = """
            judge.Agree("Random strings and dictionaries vs recursion",
                random =>
                {
                    string Letters(int length) => new string(Enumerable.Range(0, length).Select(_ => "ab"[random.Next(2)]).ToArray());
                    return (s: Letters(random.Next(1, 10)), words: Enumerable.Range(0, random.Next(1, 4)).Select(_ => Letters(random.Next(1, 4))).Distinct().ToArray());
                },
                input => WordBreakRecursively(input.s, input.words),
                input => sol.WordBreak(input.s, input.words));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 3, `"catsandog"`. The bottom row marks which prefixes can be split into words (the first column is the
            empty prefix). A prefix gets a ✓ when a shorter ✓ prefix is followed by a dictionary word, highlighted in the
            top row. The last prefix never finds such a word (`og` and `g` aren't words, and `dog` would need a ✓ before
            it), so the answer is false.
            """,
            VisualizationCode = """
            var s = "catsandog";
            var words = new HashSet<string> { "cats", "dog", "sand", "and", "cat" };
            int n = s.Length;
            var grid = MatrixTracker.Create(new[] { new[] { "∅" }.Concat(s.Select(c => c.ToString())).ToArray(), Enumerable.Repeat("", n + 1).ToArray() },
                title: "139. Word Break: which prefixes split into words?",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "letters", "can split" });
            var canSplit = new bool[n + 1];
            canSplit[0] = true;
            grid.SetCell(1, 0, val: "✓");
            grid.Snapshot("The empty prefix can always be split (into no words at all)");

            for (int i = 1; i <= n; i++)
            {
                for (int j = 0; j < i && !canSplit[i]; j++)
                    canSplit[i] = canSplit[j] && words.Contains(s[j..i]);
                int start = Enumerable.Range(0, i).FirstOrDefault(j => canSplit[j] && words.Contains(s[j..i]), -1);

                grid.SetCell(1, i, val: canSplit[i] ? "✓" : "✗", state: GridCellState.Current);
                if (start >= 0) for (int c = start + 1; c <= i; c++) grid.SetCell(0, c, state: GridCellState.Path);
                grid.Snapshot(canSplit[i] && start == 0
                    ? $"\"{s[..i]}\" is itself a word, so ✓"
                    : canSplit[i]
                    ? $"\"{s[..i]}\": \"{s[..start]}\" splits, and \"{s[start..i]}\" is a word, so ✓"
                    : $"\"{s[..i]}\": no split point j has a ✓ prefix followed by a word, so ✗");
                grid.SetCell(1, i, state: GridCellState.Default);
                for (int c = 1; c <= i; c++) grid.SetCell(0, c, state: GridCellState.Default);
            }

            grid.Snapshot($"canSplit[{n}] is {(canSplit[n] ? "true" : "false")}: \"{s}\" {(canSplit[n] ? "can" : "cannot")} be split into dictionary words");
            Display.Visualizer(grid);
            """
        },
        new()
        {
            Id = "blind75_300_longest_increasing_subsequence",
            Number = 300,
            Title = "Longest Increasing Subsequence",
            Category = "1-D DP",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 55.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Binary Search", "Dynamic Programming" },
            TimeComplexity = "O(n log n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return the length of the longest **strictly increasing subsequence**: numbers picked in their original order (not necessarily next to each other), each bigger than the one before.

            ### Example 1
            - **Input:** `nums = [10,9,2,5,3,7,101,18]`
            - **Output:** `4`
            - **Why:** `[2,3,7,101]` (or `[2,5,7,18]`).

            ### Example 2
            - **Input:** `nums = [0,1,0,3,2,3]`
            - **Output:** `4`

            ### Example 3
            - **Input:** `nums = [7,7,7,7,7,7,7]`
            - **Output:** `1`
            - **Why:** equal numbers don't count as increasing.

            ### Constraints
            - `1 <= nums.length <= 2500`
            - `-10^4 <= nums[i] <= 10^4`
            - **Follow-up:** can you do it in `O(n log n)`?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** pick as many numbers as possible, left to right, each bigger than the last.

            1. **DP over "ends here":** `longest[i]` = the longest increasing subsequence that **ends** at `nums[i]`. It extends the best earlier one with a smaller value: `longest[i] = 1 + max(longest[j])` over `j < i` with `nums[j] < nums[i]`. That's `O(n²)`.
            2. **Faster: remember only the best tails.** Let `tails[k]` be the smallest number that can end an increasing subsequence of length `k + 1`. Smaller tails are better: they leave more room for what follows. `tails` is always sorted.
            3. **For each number `x`:** if it's bigger than every tail, it extends the longest subsequence: append it. Otherwise it gives some length a smaller tail: replace the **first tail ≥ x**. Find that spot with binary search: `O(log n)` per number.
            4. **The answer is `tails.Count`.** (`tails` itself isn't necessarily a real subsequence; only its length is meaningful.)
            5. **Walk Example 1:** 10 → [10]; 9 → [9]; 2 → [2]; 5 → [2,5]; 3 → [2,3]; 7 → [2,3,7]; 101 → [2,3,7,101]; 18 → [2,3,7,18]. Length **4**.

            **Pattern to remember:** "longest increasing subsequence" → patience sorting with binary search; many "longest chain" problems (Russian dolls, box stacking) reduce to it.

            **Common mistakes:** confusing subsequence with subarray; using `<=` (then equal values extend the sequence); replacing the first tail **greater** than `x` instead of the first **≥ x** (breaks with duplicates).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Take or skip every number",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^n)",
                    SpaceComplexity = "O(n) call stack",
                    Intuition = "Walk the array remembering the last number taken; each number is either taken (if bigger) or skipped.",
                    BottleneckExplanation = "Every subset of positions is a possible subsequence, so the choices double with each number.",
                    Code = """
                    int LisBruteForce(int[] nums, int i = 0, int last = int.MinValue)
                    {
                        if (i == nums.Length) return 0;
                        int skip = LisBruteForce(nums, i + 1, last);
                        int take = nums[i] > last ? 1 + LisBruteForce(nums, i + 1, nums[i]) : 0;
                        return Math.Max(skip, take);
                    }

                    Console.WriteLine(Judge.Format(LisBruteForce(new[] { 10, 9, 2, 5, 3, 7, 101, 18 })));   // 4
                    """
                },
                new()
                {
                    Name = "Longest subsequence ending at each index",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "longest[i] = 1 + max(longest[j]) over j < i with nums[j] < nums[i]   (1 if there is none)\nanswer = max(longest)",
                    Intuition = "Each number extends the longest increasing subsequence that ends at a smaller number before it.",
                    Code = """
                    int LisQuadratic(int[] nums)
                    {
                        var longest = new int[nums.Length];
                        for (int i = 0; i < nums.Length; i++)
                        {
                            longest[i] = 1;
                            for (int j = 0; j < i; j++)
                                if (nums[j] < nums[i]) longest[i] = Math.Max(longest[i], longest[j] + 1);
                        }
                        return longest.Max();
                    }

                    Console.WriteLine(Judge.Format(LisQuadratic(new[] { 0, 1, 0, 3, 2, 3 })));   // 4
                    """
                },
                new()
                {
                    Name = "Smallest tails + binary search (patience sorting)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Keep, for each length, the smallest possible tail. Each number either extends the longest length or lowers the first tail that is not smaller than it."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int LengthOfLIS(int[] nums)
                {
                    var tails = new List<int>();   // tails[k]: the smallest tail of an increasing subsequence of length k + 1
                    foreach (int x in nums)
                    {
                        int k = tails.BinarySearch(x);
                        if (k < 0) k = ~k;                    // the first tail >= x
                        if (k == tails.Count) tails.Add(x);    // x extends the longest subsequence
                        else tails[k] = x;                     // x is a smaller tail for length k + 1
                    }
                    return tails.Count;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [10,9,2,5,3,7,101,18]", Expected = "4", Call = "sol.LengthOfLIS(new[] { 10, 9, 2, 5, 3, 7, 101, 18 })" },
                new() { Name = "Example 2", Input = "nums = [0,1,0,3,2,3]", Expected = "4", Call = "sol.LengthOfLIS(new[] { 0, 1, 0, 3, 2, 3 })" },
                new() { Name = "Example 3", Input = "nums = [7,7,7,7,7,7,7]", Expected = "1", Call = "sol.LengthOfLIS(new[] { 7, 7, 7, 7, 7, 7, 7 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One number", Input = "nums = [5]", Expected = "1", Call = "sol.LengthOfLIS(new[] { 5 })" },
                new() { Name = "Already increasing", Input = "nums = [1,2,3,4]", Expected = "4", Call = "sol.LengthOfLIS(new[] { 1, 2, 3, 4 })" },
                new() { Name = "Decreasing", Input = "nums = [4,3,2,1]", Expected = "1", Call = "sol.LengthOfLIS(new[] { 4, 3, 2, 1 })" },
                new() { Name = "A later, lower start wins", Input = "nums = [3,5,6,2,5,4,19,5,6,7,12]", Expected = "6", Call = "sol.LengthOfLIS(new[] { 3, 5, 6, 2, 5, 4, 19, 5, 6, 7, 12 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs the O(n²) table",
                random => Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(-5, 10)).ToArray(),
                nums => LisQuadratic(nums),
                nums => sol.LengthOfLIS(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 1. `i` walks the numbers and `tails` (underneath) holds the smallest possible tail for each length.
            A number bigger than every tail makes the longest subsequence one longer; otherwise binary search finds the
            first tail that is not smaller and the number replaces it.
            """,
            VisualizationCode = """
            var nums = new[] { 10, 9, 2, 5, 3, 7, 101, 18 };
            var tracker = VisualizerRecorder.CreateArray(nums, title: "300. Longest Increasing Subsequence: the smallest tail for every length");
            var tails = new List<int>();
            tracker.Watch(tails);

            for (int i = 0; i < nums.Length; i++)
            {
                int x = nums[i];
                int k = tails.BinarySearch(x);
                if (k < 0) k = ~k;
                if (k == tails.Count)
                {
                    tails.Add(x);
                    tracker.Step($"{x} is bigger than every tail: it extends the longest subsequence to length {tails.Count}", pointers: new { i });
                }
                else
                {
                    int old = tails[k];
                    tails[k] = x;
                    tracker.Step($"{x}: binary search finds the first tail ≥ {x} is {old} (length {k + 1}); {x} is a smaller tail for that length, so replace it", pointers: new { i });
                }
            }

            tracker.Step($"The longest increasing subsequence has length {tails.Count}");
            Display.Visualizer(tracker);
            """
        }
    };
}
