using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CurriculumEnhancer
{
    private static void EnhanceGraphsAndIntervals(BlindProblemItem p)
    {
        switch (p.Number)
        {
            case 200: // Number of Islands
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Number of Islands
                    1. **Connected Component Count**: An island is an undirected connected component of '1's.
                    2. **Exploration & Sinking**: When you encounter an unvisited '1', increment the island count, then trigger BFS or DFS to sink ('0') all adjacent connected land cells.
                    3. **In-Place Modification**: Sinking the cell by setting `grid[r][c] = '0'` eliminates the need for an $O(M \times N)$ visited set!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Recursive DFS (In-Place Sinking)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(M * N)",
                    SpaceComplexity = "O(M * N) (Worst-case Call Stack)",
                    Intuition = "Sink connected land by recursing in 4 cardinal directions upon discovering '1'.",
                    Code = """
                    public int NumIslands(char[][] grid)
                    {
                        if (grid == null || grid.Length == 0) return 0;
                        int count = 0;
                        for (int r = 0; r < grid.Length; r++)
                        {
                            for (int c = 0; c < grid[0].Length; c++)
                            {
                                if (grid[r][c] == '1')
                                {
                                    count++;
                                    Sink(grid, r, c);
                                }
                            }
                        }
                        return count;
                    }

                    private void Sink(char[][] grid, int r, int c)
                    {
                        if (r < 0 || r >= grid.Length || c < 0 || c >= grid[0].Length || grid[r][c] != '1') return;
                        grid[r][c] = '0';
                        Sink(grid, r + 1, c);
                        Sink(grid, r - 1, c);
                        Sink(grid, r, c + 1);
                        Sink(grid, r, c - 1);
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Iterative BFS (Queue-based)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(M * N)",
                    SpaceComplexity = "O(min(M, N))",
                    Intuition = "Use a queue to spread outwards in concentric waves from each discovered land cell.",
                    Code = """
                    public int NumIslandsBFS(char[][] grid)
                    {
                        if (grid == null || grid.Length == 0) return 0;
                        int count = 0;
                        int rows = grid.Length, cols = grid[0].Length;
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                if (grid[r][c] == '1')
                                {
                                    count++;
                                    grid[r][c] = '0';
                                    var q = new Queue<(int r, int c)>();
                                    q.Enqueue((r, c));
                                    while (q.Count > 0)
                                    {
                                        var (currR, currC) = q.Dequeue();
                                        int[] dr = { 1, -1, 0, 0 }, dc = { 0, 0, 1, -1 };
                                        for (int i = 0; i < 4; i++)
                                        {
                                            int nr = currR + dr[i], nc = currC + dc[i];
                                            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && grid[nr][nc] == '1')
                                            {
                                                grid[nr][nc] = '0';
                                                q.Enqueue((nr, nc));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        return count;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var maze = new[] {
                        "11000",
                        "11000",
                        "00100",
                        "00011"
                    };

                    // 1. Wrap in MatrixTracker (automatically parses Start, Target, and Walls)
                    var tracker = MatrixTracker.Create(maze, title: "200. Number of Islands (BFS Pathfinder)");

                    // 2. User's own pathfinding loop:
                    var queue = new Queue<(int r, int c)>();
                    var visited = new bool[4, 5];
                    int islandCount = 0;

                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = 0; c < 5; c++)
                        {
                            if (maze[r][c] == '1' && !visited[r, c])
                            {
                                islandCount++;
                                queue.Enqueue((r, c));
                                visited[r, c] = true;
                                tracker.Snapshot($"Initialized BFS queue with Start ({r}, {c}) for Island #{islandCount}");

                                while (queue.Count > 0)
                                {
                                    var (currR, currC) = queue.Dequeue();
                                    tracker.Visit(currR, currC, $"Exploring ({currR}, {currC}) • Remaining in queue: {queue.Count}");

                                    foreach (var (nr, nc) in tracker.GetNeighbors(currR, currC, fourWay: true))
                                    {
                                        if (!visited[nr, nc] && maze[nr][nc] == '1')
                                        {
                                            visited[nr, nc] = true;
                                            queue.Enqueue((nr, nc));

                                            // Record discovery and predecessor back-pointer arrow:
                                            tracker.Enqueue(nr, nc, from: (currR, currC), note: $"Discovered ({nr}, {nc}) from ({currR}, {currC})");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Display.Visualizer(tracker);
                    Console.WriteLine($"BFS exploration complete: {islandCount} islands found. Use playback controls to scrub through steps!");
                    """);
                break;

            case 56: // Merge Intervals
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Merge Intervals
                    1. **Order Matters**: If intervals are scattered arbitrarily, finding overlapping intervals requires quadratic pairwise comparisons.
                    2. **Sorting by Start Time**: Once intervals are sorted by `start`, any overlapping intervals MUST be adjacent!
                    3. **Greedy Extension**: If `curr.start <= prev.end`, extend the current interval: `prev.end = Math.Max(prev.end, curr.end)`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Pairwise Comparison",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Iterate through all pairs and merge whenever intervals overlap until no merges remain.",
                    BottleneckExplanation = "Requires multiple passes and quadratic checks because intervals are unsorted.",
                    Code = """
                    // Concept: Graph component merging without sorting
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy Sorting (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N log N)",
                    SpaceComplexity = "O(N) (Sorted list)",
                    GreedyChoiceProperty = "Sorting by start time ensures only consecutive intervals can overlap.",
                    Intuition = "Sort by start time, then merge in a single linear pass.",
                    Code = """
                    public int[][] Merge(int[][] intervals)
                    {
                        if (intervals.Length <= 1) return intervals;
                        Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
                        var merged = new List<int[]>();
                        var current = intervals[0];
                        merged.Add(current);
                        foreach (var interval in intervals)
                        {
                            if (interval[0] <= current[1])
                            {
                                current[1] = Math.Max(current[1], interval[1]);
                            }
                            else
                            {
                                current = interval;
                                merged.Add(current);
                            }
                        }
                        return merged.ToArray();
                    }
                    """
                });
                break;

            case 435: // Non-overlapping Intervals
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Non-overlapping Intervals
                    1. **Complement Goal**: Finding the MINIMUM intervals to remove is equivalent to finding the MAXIMUM non-overlapping intervals (Interval Scheduling Theorem).
                    2. **Greedy Choice**: Sort by **end time**! Always pick the interval that finishes earliest, leaving maximum remaining time for subsequent intervals!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy Interval Scheduling (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N log N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Earliest deadline first leaves the maximum opportunity for subsequent intervals.",
                    Intuition = "Sort by end time. If the next interval starts before the current one ends, it must be removed.",
                    Code = """
                    public int EraseOverlapIntervals(int[][] intervals)
                    {
                        if (intervals.Length == 0) return 0;
                        Array.Sort(intervals, (a, b) => a[1].CompareTo(b[1]));
                        int count = 0;
                        int prevEnd = intervals[0][1];
                        for (int i = 1; i < intervals.Length; i++)
                        {
                            if (intervals[i][0] < prevEnd) count++;
                            else prevEnd = intervals[i][1];
                        }
                        return count;
                    }
                    """
                });
                break;
        }
    }

    private static void EnhanceMathAndBits(BlindProblemItem p)
    {
        switch (p.Number)
        {
            case 191: // Number of 1 Bits
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Number of 1 Bits (Hamming Weight)
                    1. **Bitwise Inspection**: Check each of the 32 bits with `n & 1`, shifting right.
                    2. **Brian Kernighan's Algorithm**: The operation `n & (n - 1)` always clears the **lowest set bit** of `n`!
                    3. **Speed Advantage**: Iterates only as many times as there are set bits, rather than fixed 32 loops.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive 32-Bit Loop",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(32) = O(1)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Iterate through all 32 bits, checking the lowest bit and shifting right.",
                    Code = """
                    public int HammingWeightLoop(uint n)
                    {
                        int count = 0;
                        for (int i = 0; i < 32; i++)
                        {
                            if ((n & 1) == 1) count++;
                            n >>= 1;
                        }
                        return count;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Brian Kernighan's Algorithm (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(Set Bits) <= O(32)",
                    SpaceComplexity = "O(1)",
                    Intuition = "n & (n - 1) flips the least significant 1-bit to 0 in one CPU cycle.",
                    Code = """
                    public int HammingWeight(uint n)
                    {
                        int count = 0;
                        while (n != 0)
                        {
                            n &= (n - 1);
                            count++;
                        }
                        return count;
                    }
                    """
                });
                break;
        }
    }
}
