using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetIntervalProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_57_insert_interval",
            Number = 57,
            Title = "Insert Interval",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 41.5,
            IsPremium = false,
            Tags = new List<string> { "Array" },
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n) for the result",
            DescriptionMarkdown = """
            You are given `intervals`, a list of **non-overlapping** intervals `[start_i, end_i]` sorted by start, and one more interval `newInterval = [start, end]`.

            Insert `newInterval` so that the list is still sorted by start and has no overlapping intervals, merging wherever it overlaps. Return the resulting list (building a new list is fine).

            ### Example 1
            - **Input:** `intervals = [[1,3],[6,9]], newInterval = [2,5]`
            - **Output:** `[[1,5],[6,9]]`
            - **Why:** `[2,5]` overlaps `[1,3]`, so together they become `[1,5]`; `[6,9]` starts after 5 and stays.

            ### Example 2
            - **Input:** `intervals = [[1,2],[3,5],[6,7],[8,10],[12,16]], newInterval = [4,8]`
            - **Output:** `[[1,2],[3,10],[12,16]]`
            - **Why:** `[4,8]` overlaps `[3,5]`, `[6,7]` and `[8,10]` (touching at 8 counts), so all four become `[3,10]`.

            ### Constraints
            - `0 <= intervals.length <= 10^4`
            - `0 <= start_i <= end_i <= 10^5`, and `intervals` is sorted by `start_i` with no overlaps.
            - `0 <= start <= end <= 10^5`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** drop one more bar onto a sorted, gap-separated number line and glue it to whatever it touches.

            1. **The lazy way:** append `newInterval` and run Merge Intervals (problem 56): sort, then sweep. Correct, but sorting costs `O(n log n)` although the list is already sorted.
            2. **Use the order you're given.** Compared with `newInterval`, every existing interval falls into exactly one of three groups, and they appear **in this order**:
               - **entirely before** it: it ends before the new one starts (`end_i < start`). Copy it unchanged.
               - **overlapping** it: it starts no later than the new one ends (`start_i <= end`). Absorb it: `start = min(start, start_i)`, `end = max(end, end_i)`.
               - **entirely after** it: everything left. Add the grown interval once, then copy the rest.
            3. **One pass, three loops,** and every interval is looked at once: `O(n)`.
            4. **Touching counts as overlapping,** so the tests are `<` for "before" and `<=` for "overlaps".
            5. **Walk Example 2:** `[1,2]` ends before 4: copy. `[3,5]` starts at 3 ≤ 8: absorb → `[3,8]`. `[6,7]` → still `[3,8]`. `[8,10]` starts at 8 ≤ 8 → `[3,10]`. `[12,16]` starts after 10: add `[3,10]`, then copy `[12,16]`.

            **Pattern to remember:** with sorted, disjoint intervals, a new interval splits the list into before / overlapping / after, and the middle group collapses into one interval.

            **Common mistakes:** comparing with the *original* `end` after it has grown (always use the updated one); forgetting to add the merged interval when nothing overlaps; mixing up `<` and `<=` at touching points.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Append it and merge everything again",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Add `newInterval` to the list, sort by start and merge overlapping neighbours exactly like problem 56.",
                    BottleneckExplanation = "It re-sorts a list that is already sorted, so it pays `O(n log n)` for what a single `O(n)` pass can do.",
                    Code = """
                    int[][] InsertByMergingAgain(int[][] intervals, int[] newInterval)
                    {
                        var all = intervals.Append(newInterval).OrderBy(i => i[0]).ToList();
                        var merged = new List<int[]>();
                        foreach (var interval in all)
                        {
                            if (merged.Count > 0 && interval[0] <= merged[^1][1]) merged[^1][1] = Math.Max(merged[^1][1], interval[1]);
                            else merged.Add(new[] { interval[0], interval[1] });
                        }
                        return merged.ToArray();
                    }

                    Console.WriteLine(Judge.Format(InsertByMergingAgain(new[] { new[] { 1, 3 }, new[] { 6, 9 } }, new[] { 2, 5 })));   // [[1,5],[6,9]]
                    """
                },
                new()
                {
                    Name = "One pass: before, overlapping, after",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n) for the result",
                    Intuition = "Copy the intervals that end before the new one starts, absorb every interval that starts before it ends (growing it), add it, then copy the rest."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[][] Insert(int[][] intervals, int[] newInterval)
                {
                    var result = new List<int[]>();
                    int start = newInterval[0], end = newInterval[1], i = 0;

                    while (i < intervals.Length && intervals[i][1] < start) result.Add(intervals[i++]);   // entirely before

                    while (i < intervals.Length && intervals[i][0] <= end)                              // overlapping: absorb it
                    {
                        start = Math.Min(start, intervals[i][0]);
                        end = Math.Max(end, intervals[i][1]);
                        i++;
                    }
                    result.Add(new[] { start, end });

                    while (i < intervals.Length) result.Add(intervals[i++]);                              // entirely after
                    return result.ToArray();
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "intervals = [[1,3],[6,9]], newInterval = [2,5]", Expected = "[[1,5],[6,9]]", Call = "sol.Insert(new[] { new[] { 1, 3 }, new[] { 6, 9 } }, new[] { 2, 5 })" },
                new() { Name = "Example 2", Input = "intervals = [[1,2],[3,5],[6,7],[8,10],[12,16]], newInterval = [4,8]", Expected = "[[1,2],[3,10],[12,16]]", Call = "sol.Insert(new[] { new[] { 1, 2 }, new[] { 3, 5 }, new[] { 6, 7 }, new[] { 8, 10 }, new[] { 12, 16 } }, new[] { 4, 8 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Empty list", Input = "intervals = [], newInterval = [5,7]", Expected = "[[5,7]]", Call = "sol.Insert(new int[0][], new[] { 5, 7 })" },
                new() { Name = "Goes first", Input = "intervals = [[3,5]], newInterval = [1,2]", Expected = "[[1,2],[3,5]]", Call = "sol.Insert(new[] { new[] { 3, 5 } }, new[] { 1, 2 })" },
                new() { Name = "Goes last", Input = "intervals = [[1,2]], newInterval = [3,4]", Expected = "[[1,2],[3,4]]", Call = "sol.Insert(new[] { new[] { 1, 2 } }, new[] { 3, 4 })" },
                new() { Name = "Touching counts", Input = "intervals = [[1,5]], newInterval = [5,7]", Expected = "[[1,7]]", Call = "sol.Insert(new[] { new[] { 1, 5 } }, new[] { 5, 7 })" },
                new() { Name = "Swallows everything", Input = "intervals = [[2,3],[5,6]], newInterval = [1,10]", Expected = "[[1,10]]", Call = "sol.Insert(new[] { new[] { 2, 3 }, new[] { 5, 6 } }, new[] { 1, 10 })" },
                new() { Name = "Already covered", Input = "intervals = [[1,10]], newInterval = [3,4]", Expected = "[[1,10]]", Call = "sol.Insert(new[] { new[] { 1, 10 } }, new[] { 3, 4 })" }
            },
            StressTestCode = """
            judge.Agree("Random inputs vs merging again",
                random =>
                {
                    var intervals = new List<int[]>();
                    int at = random.Next(0, 3);
                    for (int k = random.Next(0, 6); k > 0; k--)
                    {
                        int length = random.Next(0, 3);
                        intervals.Add(new[] { at, at + length });
                        at += length + random.Next(1, 4);
                    }
                    int start = random.Next(0, at + 2);
                    return (intervals: intervals.ToArray(), newInterval: new[] { start, start + random.Next(0, 5) });
                },
                input => InsertByMergingAgain(input.intervals, input.newInterval),
                input => sol.Insert(input.intervals, input.newInterval));
            """,
            VisualizerKind = "Canvas",
            VisualizationDescription = """
            Example 2 on a timeline. The purple **new** row is `newInterval`, growing as it absorbs overlapping intervals
            (teal). The dashed pink line marks the edge being compared: first where the new interval starts, then where it
            ends. The green **result** lane fills up with the copied intervals and the merged one.
            """,
            VisualizationCode = """
            var intervals = new[] { new[] { 1, 2 }, new[] { 3, 5 }, new[] { 6, 7 }, new[] { 8, 10 }, new[] { 12, 16 } };
            var newInterval = new[] { 4, 8 };
            var tracker = IntervalTracker.Create(intervals, title: "57. Insert Interval: before, overlapping, after");

            var result = new List<int[]>();
            var absorbed = new List<int>();
            int start = newInterval[0], end = newInterval[1], i = 0;
            tracker.Step($"newInterval [{start},{end}] (purple) has to fit into the sorted list", pending: new[] { start, end }, result: result);

            while (i < intervals.Length && intervals[i][1] < start)
            {
                result.Add(intervals[i]);
                tracker.Step($"[{intervals[i][0]},{intervals[i][1]}] ends at {intervals[i][1]}, before the new interval starts at {start}: copy it",
                    current: i, done: Enumerable.Range(0, i), pending: new[] { start, end }, result: result, marker: start, markerLabel: $"new starts {start}");
                i++;
            }

            while (i < intervals.Length && intervals[i][0] <= end)
            {
                int oldEnd = end;
                start = Math.Min(start, intervals[i][0]);
                end = Math.Max(end, intervals[i][1]);
                absorbed.Add(i);
                tracker.Step($"[{intervals[i][0]},{intervals[i][1]}] starts at {intervals[i][0]} ≤ {oldEnd}, where the new interval ends: absorb it, giving [{start},{end}]",
                    current: i, active: absorbed, done: Enumerable.Range(0, i), pending: new[] { start, end }, result: result, marker: oldEnd, markerLabel: $"new ends {oldEnd}");
                i++;
            }

            result.Add(new[] { start, end });
            tracker.Step(i < intervals.Length
                    ? $"[{intervals[i][0]},{intervals[i][1]}] starts after {end}: nothing more overlaps, so add the grown interval [{start},{end}]"
                    : $"Nothing more overlaps: add the grown interval [{start},{end}]",
                active: absorbed, done: Enumerable.Range(0, i), result: result, marker: end, markerLabel: $"new ends {end}");

            while (i < intervals.Length)
            {
                result.Add(intervals[i]);
                tracker.Step($"[{intervals[i][0]},{intervals[i][1]}] is entirely after: copy it", current: i, done: Enumerable.Range(0, i), result: result);
                i++;
            }

            tracker.Step($"Done: {Judge.Format(result)}", done: Enumerable.Range(0, intervals.Length), result: result);
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_56_merge_intervals",
            Number = 56,
            Title = "Merge Intervals",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 50.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Sorting" },
            TimeComplexity = "O(n log n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            You are given an array of `intervals` where `intervals[i] = [start_i, end_i]`. Merge every group of **overlapping** intervals into one, and return the non-overlapping intervals that cover exactly the same ranges as the input.

            Two intervals overlap when they share at least one point, so `[1,4]` and `[4,5]` overlap (they touch at 4).

            ### Example 1
            - **Input:** `intervals = [[1,3],[2,6],[8,10],[15,18]]`
            - **Output:** `[[1,6],[8,10],[15,18]]`
            - **Why:** `[1,3]` and `[2,6]` overlap on `2..3`, so they merge into `[1,6]`. The other two touch nothing.

            ### Example 2
            - **Input:** `intervals = [[1,4],[4,5]]`
            - **Output:** `[[1,5]]`
            - **Why:** they touch at 4, which counts as overlapping.

            ### Example 3
            - **Input:** `intervals = [[4,7],[1,4]]`
            - **Output:** `[[1,7]]`
            - **Why:** the input is not sorted; `[1,4]` comes first on the number line and touches `[4,7]`.

            ### Constraints
            - `1 <= intervals.length <= 10^4`
            - `intervals[i].length == 2`
            - `0 <= start_i <= end_i <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Picture it on a number line.** Each interval is a bar. Merging means gluing together bars that touch or overlap, and reporting the glued pieces.

            1. **When do two bars overlap?** With `a = [a0,a1]` starting no later than `b = [b0,b1]`, they overlap exactly when `b0 <= a1`: the second one starts before the first one ends. The merged bar is `[a0, max(a1, b1)]`. Take the **max**, because `b` can sit completely inside `a` (`[1,10]` and `[2,3]`).
            2. **The trouble with unsorted input.** In `[[8,10],[1,3],[2,6]]`, the 1 and the 2 are far apart in the array, so you'd have to compare every pair, and merging can create new overlaps, so you'd compare again.
            3. **Sort by start, and the problem becomes one sweep.** After sorting, an interval can only overlap the group you are currently building, because every earlier group ended before something that started even earlier. So keep one "current group":
               - if the next interval starts **at or before** the group's end, extend the group's end;
               - otherwise the group is final: save it and start a new group with this interval.
            4. **Walk Example 1.** `[1,3]` opens a group. `[2,6]` starts at 2 ≤ 3, so the group becomes `[1,6]`. `[8,10]` starts at 8 > 6, so `[1,6]` is final and `[8,10]` opens a group. `[15,18]` starts at 15 > 10, so it opens another. The answer is `[[1,6],[8,10],[15,18]]`.

            **Pattern to remember:** most interval problems begin with "sort by start (or by end)"; after that, a single pass comparing each interval with the last one you kept is enough.

            **Common mistakes:** using `<` instead of `<=` (touching intervals must merge); setting the end to the new interval's end instead of the max; forgetting to add the last group after the loop; editing the input arrays while you still need them.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Merge overlapping pairs until nothing changes",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n³) worst case",
                    SpaceComplexity = "O(n)",
                    Intuition = "Keep a list of intervals. Look for any two that overlap; if you find a pair, replace both with their union and start looking again. When no pair overlaps, you are done.",
                    BottleneckExplanation = "Without an order you cannot tell which intervals might still overlap, so every merge sends you back to comparing all pairs.",
                    Code = """
                    int[][] MergeUntilStable(int[][] intervals)
                    {
                        var list = intervals.Select(i => new[] { i[0], i[1] }).ToList();
                        bool merged = true;
                        while (merged)
                        {
                            merged = false;
                            for (int a = 0; a < list.Count && !merged; a++)
                                for (int b = a + 1; b < list.Count && !merged; b++)
                                    if (list[a][0] <= list[b][1] && list[b][0] <= list[a][1])
                                    {
                                        list[a] = new[] { Math.Min(list[a][0], list[b][0]), Math.Max(list[a][1], list[b][1]) };
                                        list.RemoveAt(b);
                                        merged = true;
                                    }
                        }
                        return list.OrderBy(i => i[0]).ToArray();
                    }

                    Console.WriteLine(Judge.Format(MergeUntilStable(new[] { new[] { 1, 3 }, new[] { 2, 6 }, new[] { 8, 10 }, new[] { 15, 18 } })));   // [[1,6],[8,10],[15,18]]
                    """
                },
                new()
                {
                    Name = "Sort by start, then sweep once",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Sort by start time. Walk the intervals keeping the last merged group: extend its end when the next interval starts at or before it, otherwise start a new group. Sorting dominates the cost; the sweep is O(n)."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[][] Merge(int[][] intervals)
                {
                    var sorted = intervals.OrderBy(i => i[0]).ToArray();
                    var merged = new List<int[]>();
                    foreach (var interval in sorted)
                    {
                        if (merged.Count > 0 && interval[0] <= merged[^1][1])
                        {
                            merged[^1][1] = Math.Max(merged[^1][1], interval[1]);   // overlaps: extend the current group
                        }
                        else
                        {
                            merged.Add(new[] { interval[0], interval[1] });          // a gap: start a new group
                        }
                    }
                    return merged.ToArray();
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "intervals = [[1,3],[2,6],[8,10],[15,18]]", Expected = "[[1,6],[8,10],[15,18]]", Call = "sol.Merge(new[] { new[] { 1, 3 }, new[] { 2, 6 }, new[] { 8, 10 }, new[] { 15, 18 } })" },
                new() { Name = "Example 2", Input = "intervals = [[1,4],[4,5]]", Expected = "[[1,5]]", Call = "sol.Merge(new[] { new[] { 1, 4 }, new[] { 4, 5 } })" },
                new() { Name = "Example 3", Input = "intervals = [[4,7],[1,4]]", Expected = "[[1,7]]", Call = "sol.Merge(new[] { new[] { 4, 7 }, new[] { 1, 4 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One interval", Input = "intervals = [[5,5]]", Expected = "[[5,5]]", Call = "sol.Merge(new[] { new[] { 5, 5 } })" },
                new() { Name = "Nothing overlaps", Input = "intervals = [[1,2],[3,4],[5,6]]", Expected = "[[1,2],[3,4],[5,6]]", Call = "sol.Merge(new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 } })" },
                new() { Name = "Nested inside a longer one", Input = "intervals = [[1,10],[2,3],[4,5]]", Expected = "[[1,10]]", Call = "sol.Merge(new[] { new[] { 1, 10 }, new[] { 2, 3 }, new[] { 4, 5 } })" },
                new() { Name = "A chain that merges into one", Input = "intervals = [[1,4],[2,5],[4,7],[6,9]]", Expected = "[[1,9]]", Call = "sol.Merge(new[] { new[] { 1, 4 }, new[] { 2, 5 }, new[] { 4, 7 }, new[] { 6, 9 } })" }
            },
            StressTestCode = """
            judge.Agree("Random intervals vs merge-until-stable",
                random => Enumerable.Range(0, random.Next(1, 9)).Select(_ => { int start = random.Next(0, 20); return new[] { start, start + random.Next(0, 6) }; }).ToArray(),
                intervals => MergeUntilStable(intervals),
                intervals => sol.Merge(intervals));
            """,
            VisualizerKind = "Canvas",
            VisualizationDescription = """
            A timeline for `[[8,10],[1,3],[15,18],[2,6],[9,11]]`, unsorted on purpose. After sorting, the amber row is the
            interval being examined and the dashed pink line marks where the current group ends: an interval starting at
            or before that line overlaps. The green **result** lane is the answer so far; a group that just grew or
            appeared is outlined in lime.
            """,
            VisualizationCode = """
            var intervals = new[] { new[] { 8, 10 }, new[] { 1, 3 }, new[] { 15, 18 }, new[] { 2, 6 }, new[] { 9, 11 } };
            var tracker = IntervalTracker.Create(intervals, title: "56. Merge Intervals: sort, then sweep");

            Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
            tracker.Step("Sort by start: intervals that can overlap now sit next to each other");

            var merged = new List<int[]>();
            for (int i = 0; i < intervals.Length; i++)
            {
                var interval = intervals[i];
                if (merged.Count > 0 && interval[0] <= merged[^1][1])
                {
                    int end = merged[^1][1];
                    merged[^1][1] = Math.Max(end, interval[1]);
                    tracker.Step($"[{interval[0]},{interval[1]}] starts at {interval[0]} ≤ {end}, the group's end: overlap, so the group becomes [{merged[^1][0]},{merged[^1][1]}]",
                        current: i, done: Enumerable.Range(0, i), result: merged, marker: merged[^1][1], markerLabel: $"end {merged[^1][1]}");
                }
                else
                {
                    merged.Add(new[] { interval[0], interval[1] });
                    tracker.Step(merged.Count == 1
                            ? $"[{interval[0]},{interval[1]}] opens the first group"
                            : $"[{interval[0]},{interval[1]}] starts after {merged[^2][1]}: the last group is final, start a new one",
                        current: i, done: Enumerable.Range(0, i), result: merged, marker: merged[^1][1], markerLabel: $"end {merged[^1][1]}");
                }
            }

            tracker.Step($"Done: {merged.Count} merged intervals cover everything", done: Enumerable.Range(0, intervals.Length), result: merged);
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_435_non_overlapping_intervals",
            Number = 435,
            Title = "Non-overlapping Intervals",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 54.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming", "Greedy", "Sorting" },
            TimeComplexity = "O(n log n)",
            SpaceComplexity = "O(n) for the sorted copy",
            DescriptionMarkdown = """
            Given an array of `intervals` where `intervals[i] = [start_i, end_i]`, return the **minimum number of intervals to remove** so that the remaining intervals don't overlap.

            Intervals that only touch, like `[1,2]` and `[2,3]`, do **not** overlap.

            ### Example 1
            - **Input:** `intervals = [[1,2],[2,3],[3,4],[1,3]]`
            - **Output:** `1`
            - **Why:** remove `[1,3]`; `[1,2]`, `[2,3]` and `[3,4]` only touch.

            ### Example 2
            - **Input:** `intervals = [[1,2],[1,2],[1,2]]`
            - **Output:** `2`
            - **Why:** the three copies overlap each other, so only one can stay.

            ### Example 3
            - **Input:** `intervals = [[1,2],[2,3]]`
            - **Output:** `0`

            ### Constraints
            - `1 <= intervals.length <= 10^5`
            - `-5 * 10^4 <= start_i < end_i <= 5 * 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** removing as few as possible is the same as **keeping as many as possible** without overlaps. The answer is `n − (most intervals we can keep)`.

            1. **Brute force:** try every subset and keep the largest one without overlaps: `O(2ⁿ)`, only usable for tiny inputs.
            2. **Dynamic programming:** sort by start; `keep[i]` = the most intervals we can keep ending with interval `i` = `1 + max(keep[j])` over earlier `j` that end by the time `i` starts. `O(n²)`, like longest increasing subsequence.
            3. **Greedy: always keep the interval that ends first.** Sort by **end**. Keep the first one; then keep each next interval that starts at or after the last kept end, and remove the others.
            4. **Why the earliest end is safe:** whatever the best answer keeps first, swapping it for the interval that ends earliest can't create an overlap (it ends even sooner), so there is a best answer that starts with the earliest end. Repeat the argument for the rest. Sorting by *start* fails: `[1,100]` starts first but blocks everything.
            5. **Walk Example 1:** by end: `[1,2]`, `[2,3]`, `[1,3]`, `[3,4]`. Keep `[1,2]`; `[2,3]` starts at 2 ≥ 2, keep; `[1,3]` starts at 1 < 3, remove; `[3,4]` starts at 3 ≥ 3, keep. Removed **1**.

            **Pattern to remember:** "the most non-overlapping intervals" (activity selection) is greedy by earliest **end**. It is the same problem as the minimum arrows to burst balloons.

            **Common mistakes:** sorting by start and keeping the first one; treating touching intervals as overlapping (`>=`, not `>`); returning how many you kept instead of how many you removed.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Try every subset",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2ⁿ · n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Each bitmask is a choice of intervals to keep. Sort the kept ones by start and check neighbours; the largest valid subset tells how few to remove.",
                    BottleneckExplanation = "The number of subsets doubles with every interval: 20 intervals already means a million subsets.",
                    Code = """
                    int EraseOverlapIntervalsBruteForce(int[][] intervals)
                    {
                        int n = intervals.Length, best = 0;
                        for (int mask = 0; mask < 1 << n; mask++)                 // every subset of intervals to keep
                        {
                            var kept = Enumerable.Range(0, n).Where(i => (mask >> i & 1) == 1).Select(i => intervals[i]).OrderBy(i => i[0]).ToList();
                            bool valid = true;
                            for (int k = 1; k < kept.Count && valid; k++) valid = kept[k - 1][1] <= kept[k][0];
                            if (valid) best = Math.Max(best, kept.Count);
                        }
                        return n - best;
                    }

                    Console.WriteLine(Judge.Format(EraseOverlapIntervalsBruteForce(new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 1, 3 } })));   // 1
                    """
                },
                new()
                {
                    Name = "Dynamic programming over intervals sorted by start",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "keep[i] = 1 + max(keep[j]) over j < i with end_j ≤ start_i (or 1 if there is none)\nanswer = n − max(keep)",
                    Intuition = "Like longest increasing subsequence: the best chain ending with interval `i` extends the best chain that finishes before `i` starts.",
                    Code = """
                    int EraseOverlapIntervalsDp(int[][] intervals)
                    {
                        var sorted = intervals.OrderBy(i => i[0]).ToArray();
                        var keep = new int[sorted.Length];
                        for (int i = 0; i < sorted.Length; i++)
                        {
                            keep[i] = 1;
                            for (int j = 0; j < i; j++)
                                if (sorted[j][1] <= sorted[i][0]) keep[i] = Math.Max(keep[i], keep[j] + 1);
                        }
                        return sorted.Length - keep.Max();
                    }

                    Console.WriteLine(Judge.Format(EraseOverlapIntervalsDp(new[] { new[] { 1, 2 }, new[] { 1, 2 }, new[] { 1, 2 } })));   // 2
                    """
                },
                new()
                {
                    Name = "Greedy: keep whatever ends first",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    GreedyChoiceProperty = "Among intervals that fit, the one ending earliest leaves the most room, so some optimal answer always keeps it.",
                    Intuition = "Sort by end. Keep an interval when it starts at or after the last kept end; otherwise it overlaps and ends later, so remove it."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int EraseOverlapIntervals(int[][] intervals)
                {
                    var byEnd = intervals.OrderBy(i => i[1]).ToArray();
                    int removed = 0, lastEnd = int.MinValue;
                    foreach (var interval in byEnd)
                    {
                        if (interval[0] >= lastEnd) lastEnd = interval[1];   // fits after the last kept one: keep it
                        else removed++;                                      // overlaps it and ends later: remove it
                    }
                    return removed;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "intervals = [[1,2],[2,3],[3,4],[1,3]]", Expected = "1", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 1, 3 } })" },
                new() { Name = "Example 2", Input = "intervals = [[1,2],[1,2],[1,2]]", Expected = "2", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 2 }, new[] { 1, 2 }, new[] { 1, 2 } })" },
                new() { Name = "Example 3", Input = "intervals = [[1,2],[2,3]]", Expected = "0", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 2 }, new[] { 2, 3 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One interval", Input = "intervals = [[1,2]]", Expected = "0", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 2 } })" },
                new() { Name = "Starting first is a trap", Input = "intervals = [[1,100],[11,22],[1,11],[2,12]]", Expected = "2", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 100 }, new[] { 11, 22 }, new[] { 1, 11 }, new[] { 2, 12 } })" },
                new() { Name = "Negative values", Input = "intervals = [[-3,-1],[-2,0],[0,2]]", Expected = "1", Call = "sol.EraseOverlapIntervals(new[] { new[] { -3, -1 }, new[] { -2, 0 }, new[] { 0, 2 } })" },
                new() { Name = "Already disjoint", Input = "intervals = [[1,2],[3,4],[5,6]]", Expected = "0", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 } })" },
                new() { Name = "One long interval blocks many", Input = "intervals = [[1,10],[2,3],[4,5],[6,7]]", Expected = "1", Call = "sol.EraseOverlapIntervals(new[] { new[] { 1, 10 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6, 7 } })" }
            },
            StressTestCode = """
            judge.Agree("Random intervals vs trying every subset",
                random => Enumerable.Range(0, random.Next(1, 9)).Select(_ => { int start = random.Next(-5, 10); return new[] { start, start + random.Next(1, 6) }; }).ToArray(),
                intervals => EraseOverlapIntervalsBruteForce(intervals),
                intervals => sol.EraseOverlapIntervals(intervals));
            judge.Agree("Random intervals vs the DP",
                random => Enumerable.Range(0, random.Next(1, 30)).Select(_ => { int start = random.Next(-20, 40); return new[] { start, start + random.Next(1, 12) }; }).ToArray(),
                intervals => EraseOverlapIntervalsDp(intervals),
                intervals => sol.EraseOverlapIntervals(intervals));
            """,
            VisualizerKind = "Canvas",
            VisualizationDescription = """
            `[[1,10],[2,3],[4,6],[5,7],[8,9]]` sorted by end. The dashed pink line is where the last kept interval ends:
            an interval starting at or after it is kept (green **result** lane), one starting before it overlaps and is
            removed (red ✕). Notice `[1,10]`, which starts first, comes last and gets removed.
            """,
            VisualizationCode = """
            var intervals = new[] { new[] { 1, 10 }, new[] { 2, 3 }, new[] { 4, 6 }, new[] { 5, 7 }, new[] { 8, 9 } };
            var tracker = IntervalTracker.Create(intervals, title: "435. Non-overlapping Intervals: keep whatever ends first");
            int removedCount = 0;
            tracker.Watch(() => removedCount);

            Array.Sort(intervals, (a, b) => a[1].CompareTo(b[1]));
            var kept = new List<int[]>();
            var removed = new List<int>();
            tracker.Step("Sort by end: the interval that ends first leaves the most room for the rest", result: kept);

            int lastEnd = int.MinValue;
            for (int i = 0; i < intervals.Length; i++)
            {
                var interval = intervals[i];
                double? line = kept.Count == 0 ? null : lastEnd;
                if (interval[0] >= lastEnd)
                {
                    kept.Add(interval);
                    tracker.Step(kept.Count == 1
                            ? $"[{interval[0]},{interval[1]}] ends first: keep it"
                            : $"[{interval[0]},{interval[1]}] starts at {interval[0]} ≥ {lastEnd}, where the last kept interval ends: keep it",
                        current: i, done: Enumerable.Range(0, i), removed: removed, result: kept, marker: line, markerLabel: $"kept until {lastEnd}");
                    lastEnd = interval[1];
                }
                else
                {
                    removed.Add(i);
                    removedCount++;
                    tracker.Step($"[{interval[0]},{interval[1]}] starts at {interval[0]} < {lastEnd}: it overlaps the last kept interval and ends later, so remove it",
                        current: i, done: Enumerable.Range(0, i), removed: removed, result: kept, marker: line, markerLabel: $"kept until {lastEnd}");
                }
            }

            tracker.Step($"Done: remove {removedCount}, keeping {kept.Count} intervals that don't overlap", done: Enumerable.Range(0, intervals.Length), removed: removed, result: kept);
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_252_meeting_rooms",
            Number = 252,
            Title = "Meeting Rooms",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 60.5,
            IsPremium = true,
            Tags = new List<string> { "Array", "Sorting" },
            TimeComplexity = "O(n log n)",
            SpaceComplexity = "O(n) for the sorted copy",
            DescriptionMarkdown = """
            Given an array of meeting times `intervals` where `intervals[i] = [start_i, end_i]`, decide whether one person could attend **all** of them, i.e. no two meetings overlap.

            A meeting that ends at 10 and one that starts at 10 don't overlap: you can walk straight from one to the next.

            ### Example 1
            - **Input:** `intervals = [[0,30],[5,10],[15,20]]`
            - **Output:** `false`
            - **Why:** `[5,10]` starts while `[0,30]` is still running.

            ### Example 2
            - **Input:** `intervals = [[7,10],[2,4]]`
            - **Output:** `true`

            ### Constraints
            - `0 <= intervals.length <= 10^4`
            - `0 <= start_i < end_i <= 10^6`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** does any pair of meetings overlap?

            1. **Brute force:** check every pair. Two meetings `a` and `b` overlap when each starts before the other ends: `a.start < b.end && b.start < a.end`. That's `O(n²)`.
            2. **Sort by start time.** Now a meeting can only clash with the one right **before** it: if it doesn't start before the previous meeting ends, it starts after every earlier meeting ended too (they all ended no later, or they would have clashed already).
            3. **One pass after sorting:** if `sorted[i].start < sorted[i-1].end`, return `false`. Otherwise `true`.
            4. **Touching is fine:** use `<`, not `<=`, so `[1,5]` and `[5,10]` pass.
            5. **Walk Example 1:** sorted `[0,30]`, `[5,10]`, `[15,20]`; 5 < 30 → `false` straight away.

            **Pattern to remember:** after sorting by start, overlap questions only need neighbours. This is the warm-up for Meeting Rooms II (problem 253).

            **Common mistakes:** forgetting to sort (the input is in any order); using `<=` (back-to-back meetings are allowed); comparing with the first meeting instead of the previous one.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Compare every pair",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Two meetings clash when each starts before the other ends. Check all pairs.",
                    BottleneckExplanation = "Most pairs are far apart in time; once sorted, only neighbours can clash.",
                    Code = """
                    bool CanAttendByComparingPairs(int[][] intervals)
                    {
                        for (int i = 0; i < intervals.Length; i++)
                            for (int j = i + 1; j < intervals.Length; j++)
                                if (intervals[i][0] < intervals[j][1] && intervals[j][0] < intervals[i][1]) return false;   // they overlap
                        return true;
                    }

                    Console.WriteLine(Judge.Format(CanAttendByComparingPairs(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } })));   // false
                    """
                },
                new()
                {
                    Name = "Sort by start, compare neighbours",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "After sorting by start, the meetings are compatible exactly when each one starts no earlier than the previous one ends."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool CanAttendMeetings(int[][] intervals)
                {
                    var sorted = intervals.OrderBy(i => i[0]).ToArray();
                    for (int i = 1; i < sorted.Length; i++)
                        if (sorted[i][0] < sorted[i - 1][1]) return false;   // starts before the previous meeting ends
                    return true;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "intervals = [[0,30],[5,10],[15,20]]", Expected = "false", Call = "sol.CanAttendMeetings(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } })" },
                new() { Name = "Example 2", Input = "intervals = [[7,10],[2,4]]", Expected = "true", Call = "sol.CanAttendMeetings(new[] { new[] { 7, 10 }, new[] { 2, 4 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "No meetings", Input = "intervals = []", Expected = "true", Call = "sol.CanAttendMeetings(new int[0][])" },
                new() { Name = "One meeting", Input = "intervals = [[1,2]]", Expected = "true", Call = "sol.CanAttendMeetings(new[] { new[] { 1, 2 } })" },
                new() { Name = "Back to back is fine", Input = "intervals = [[5,10],[1,5]]", Expected = "true", Call = "sol.CanAttendMeetings(new[] { new[] { 5, 10 }, new[] { 1, 5 } })" },
                new() { Name = "Clash hidden by the order", Input = "intervals = [[5,8],[1,6]]", Expected = "false", Call = "sol.CanAttendMeetings(new[] { new[] { 5, 8 }, new[] { 1, 6 } })" },
                new() { Name = "One inside another", Input = "intervals = [[1,10],[2,3]]", Expected = "false", Call = "sol.CanAttendMeetings(new[] { new[] { 1, 10 }, new[] { 2, 3 } })" }
            },
            StressTestCode = """
            judge.Agree("Random meetings vs comparing every pair",
                random => Enumerable.Range(0, random.Next(0, 6)).Select(_ => { int start = random.Next(0, 30); return new[] { start, start + random.Next(1, 6) }; }).ToArray(),
                intervals => CanAttendByComparingPairs(intervals),
                intervals => sol.CanAttendMeetings(intervals));
            """,
            VisualizerKind = "Canvas",
            VisualizationDescription = """
            `[[9,10],[1,3],[4,6],[5,8]]` on a timeline, sorted by start. Each meeting is compared with the one before it:
            the dashed pink line is where the previous meeting ends. `[5,8]` starts at 5, before `[4,6]` ends at 6, so the
            two clashing meetings turn red and the answer is false.
            """,
            VisualizationCode = """
            var intervals = new[] { new[] { 9, 10 }, new[] { 1, 3 }, new[] { 4, 6 }, new[] { 5, 8 } };
            var tracker = IntervalTracker.Create(intervals, title: "252. Meeting Rooms: after sorting, only neighbours can clash");

            Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
            tracker.Step("Sort by start time: a meeting can now only clash with the one right before it");

            bool canAttend = true;
            for (int i = 1; i < intervals.Length; i++)
            {
                var previous = intervals[i - 1];
                var meeting = intervals[i];
                if (meeting[0] < previous[1])
                {
                    canAttend = false;
                    tracker.Step($"[{meeting[0]},{meeting[1]}] starts at {meeting[0]}, before [{previous[0]},{previous[1]}] ends at {previous[1]}: they clash, so the answer is false",
                        current: i, removed: new[] { i - 1, i }, done: Enumerable.Range(0, i - 1), marker: previous[1], markerLabel: $"ends {previous[1]}");
                    break;
                }
                tracker.Step($"[{meeting[0]},{meeting[1]}] starts at {meeting[0]} ≥ {previous[1]}, when [{previous[0]},{previous[1]}] ends: no clash",
                    current: i, active: new[] { i - 1 }, done: Enumerable.Range(0, i - 1), marker: previous[1], markerLabel: $"ends {previous[1]}");
            }

            if (canAttend) tracker.Step("No neighbours clash: one person can attend every meeting, true", done: Enumerable.Range(0, intervals.Length));
            Display.Visualizer(tracker);
            """
        },
        new()
        {
            Id = "blind75_253_meeting_rooms_ii",
            Number = 253,
            Title = "Meeting Rooms II",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 53.0,
            IsPremium = true,
            Tags = new List<string> { "Array", "Two Pointers", "Greedy", "Sorting", "Heap (Priority Queue)" },
            TimeComplexity = "O(n log n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an array of meeting times `intervals` where `intervals[i] = [start_i, end_i]`, return the **minimum number of conference rooms** needed so that every meeting has a room.

            A room freed at time 10 can host a meeting that starts at 10.

            ### Example 1
            - **Input:** `intervals = [[0,30],[5,10],[15,20]]`
            - **Output:** `2`
            - **Why:** `[0,30]` needs a room the whole time; `[5,10]` and later `[15,20]` can share a second room.

            ### Example 2
            - **Input:** `intervals = [[7,10],[2,4]]`
            - **Output:** `1`

            ### Constraints
            - `1 <= intervals.length <= 10^4`
            - `0 <= start_i < end_i <= 10^6`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** the number of rooms needed is the **largest number of meetings running at the same moment**.

            1. **Count directly:** the number of running meetings only goes up when a meeting starts, so check each start time and count the meetings running then: `O(n²)`.
            2. **Hand out rooms in start order.** Sort meetings by start. When a meeting starts, reuse a room if some room's meeting has already ended; otherwise open a new room.
            3. **Which room to check?** The one that frees up **earliest**. Keep every room's end time in a **min-heap**: if the smallest end time is `<=` the new start, that room is free, so pop it; then push the new meeting's end time. The heap's size is the number of rooms opened.
            4. **Why popping one is enough:** we only need to know whether *some* room is free. Pushing and popping one each time means the heap never shrinks below the most rooms ever needed, so its final size is the answer.
            5. **Walk Example 1:** `[0,30]` → heap `{30}`. `[5,10]`: 30 > 5, no free room → `{10, 30}`. `[15,20]`: 10 ≤ 15, reuse that room → `{20, 30}`. Two rooms.

            **Another way:** sort all start times and all end times separately and sweep them with two pointers: a start before the next end needs one more room, an end frees one.

            **Common mistakes:** freeing a room only when `end < start` (back-to-back meetings share a room, so use `<=`); forgetting to sort by start; returning the number of overlaps instead of the peak.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Count the meetings running at each start",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "The number of running meetings only rises when one starts, so the peak happens at some start time. Count, for each start `t`, the meetings with `start <= t < end`.",
                    BottleneckExplanation = "For every meeting it rescans all the others, instead of keeping the running meetings up to date as time moves forward.",
                    Code = """
                    int MinMeetingRoomsByCounting(int[][] intervals)
                    {
                        int best = 0;
                        foreach (var meeting in intervals)
                        {
                            int t = meeting[0];
                            best = Math.Max(best, intervals.Count(m => m[0] <= t && t < m[1]));   // running at time t
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(MinMeetingRoomsByCounting(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } })));   // 2
                    """
                },
                new()
                {
                    Name = "Sweep the sorted starts and ends",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Sort start times and end times separately. Walk the starts; every end time that is `<=` the current start frees a room first, then the meeting takes one. The peak is the answer.",
                    Code = """
                    int MinMeetingRoomsBySweep(int[][] intervals)
                    {
                        var starts = intervals.Select(i => i[0]).OrderBy(x => x).ToArray();
                        var ends = intervals.Select(i => i[1]).OrderBy(x => x).ToArray();
                        int rooms = 0, best = 0, e = 0;
                        foreach (int start in starts)
                        {
                            while (ends[e] <= start) { e++; rooms--; }   // meetings over by now give their rooms back
                            rooms++;
                            best = Math.Max(best, rooms);
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(MinMeetingRoomsBySweep(new[] { new[] { 7, 10 }, new[] { 2, 4 } })));   // 1
                    """
                },
                new()
                {
                    Name = "Min-heap of the rooms' end times",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Sort by start. For each meeting, reuse the room that frees up earliest if it is free by now (pop it), then push this meeting's end time. The heap's size is the number of rooms."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MinMeetingRooms(int[][] intervals)
                {
                    var byStart = intervals.OrderBy(i => i[0]).ToArray();
                    var endTimes = new PriorityQueue<int, int>();   // when each room frees up, earliest first
                    foreach (var meeting in byStart)
                    {
                        if (endTimes.Count > 0 && endTimes.Peek() <= meeting[0]) endTimes.Dequeue();   // that room is free again
                        endTimes.Enqueue(meeting[1], meeting[1]);
                    }
                    return endTimes.Count;   // a room is only added when every room is busy
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "intervals = [[0,30],[5,10],[15,20]]", Expected = "2", Call = "sol.MinMeetingRooms(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } })" },
                new() { Name = "Example 2", Input = "intervals = [[7,10],[2,4]]", Expected = "1", Call = "sol.MinMeetingRooms(new[] { new[] { 7, 10 }, new[] { 2, 4 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One meeting", Input = "intervals = [[1,5]]", Expected = "1", Call = "sol.MinMeetingRooms(new[] { new[] { 1, 5 } })" },
                new() { Name = "Back to back share a room", Input = "intervals = [[1,5],[5,10],[10,15]]", Expected = "1", Call = "sol.MinMeetingRooms(new[] { new[] { 1, 5 }, new[] { 5, 10 }, new[] { 10, 15 } })" },
                new() { Name = "All at once", Input = "intervals = [[1,4],[2,5],[3,6]]", Expected = "3", Call = "sol.MinMeetingRooms(new[] { new[] { 1, 4 }, new[] { 2, 5 }, new[] { 3, 6 } })" },
                new() { Name = "Rooms get reused", Input = "intervals = [[1,10],[2,7],[3,19],[8,12],[10,20],[11,30]]", Expected = "4", Call = "sol.MinMeetingRooms(new[] { new[] { 1, 10 }, new[] { 2, 7 }, new[] { 3, 19 }, new[] { 8, 12 }, new[] { 10, 20 }, new[] { 11, 30 } })" }
            },
            StressTestCode = """
            judge.Agree("Random meetings vs counting",
                random => Enumerable.Range(0, random.Next(1, 10)).Select(_ => { int start = random.Next(0, 20); return new[] { start, start + random.Next(1, 8) }; }).ToArray(),
                intervals => MinMeetingRoomsByCounting(intervals),
                intervals => sol.MinMeetingRooms(intervals));
            judge.Agree("Random meetings vs the two-pointer sweep",
                random => Enumerable.Range(0, random.Next(1, 30)).Select(_ => { int start = random.Next(0, 50); return new[] { start, start + random.Next(1, 15) }; }).ToArray(),
                intervals => MinMeetingRoomsBySweep(intervals),
                intervals => sol.MinMeetingRooms(intervals));
            """,
            VisualizerKind = "Canvas",
            VisualizationDescription = """
            `[[1,10],[2,7],[3,19],[8,12],[10,20],[11,30]]` sorted by start. The dashed line is the current start time;
            `endTimes` under the timeline is the heap of rooms ordered by when they free up, earliest first. A meeting reuses
            the earliest room when it is free by then, otherwise it opens a new one; teal meetings are still running.
            """,
            VisualizationCode = """
            var intervals = new[] { new[] { 1, 10 }, new[] { 2, 7 }, new[] { 3, 19 }, new[] { 8, 12 }, new[] { 10, 20 }, new[] { 11, 30 } };
            var tracker = IntervalTracker.Create(intervals, title: "253. Meeting Rooms II: a heap of room end times");
            var endTimes = new PriorityQueue<string, int>();   // rooms by the time they free up (the solution only needs the times)
            tracker.Watch(endTimes);

            Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
            tracker.Step("Sort by start: hand out rooms in the order meetings begin");

            for (int i = 0; i < intervals.Length; i++)
            {
                var meeting = intervals[i];
                string room, why;
                if (endTimes.TryPeek(out _, out int earliest) && earliest <= meeting[0])
                {
                    room = endTimes.Dequeue();
                    why = $"{room} has been free since {earliest}, so reuse it";
                }
                else
                {
                    room = $"room {endTimes.Count + 1}";
                    why = endTimes.Count == 0 ? "no rooms yet: open room 1" : $"every room is busy (the earliest frees at {earliest}): open {room}";
                }
                endTimes.Enqueue(room, meeting[1]);

                var running = Enumerable.Range(0, i).Where(j => intervals[j][1] > meeting[0]).ToList();
                tracker.Step($"[{meeting[0]},{meeting[1]}] starts at {meeting[0]}: {why}, busy until {meeting[1]}. Rooms: {endTimes.Count}",
                    current: i, active: running, done: Enumerable.Range(0, i).Except(running), marker: meeting[0], markerLabel: $"t = {meeting[0]}");
            }

            tracker.Step($"Done: {endTimes.Count} rooms are enough, the most meetings that ever ran at once", done: Enumerable.Range(0, intervals.Length));
            Display.Visualizer(tracker);
            """
        }
    };
}
