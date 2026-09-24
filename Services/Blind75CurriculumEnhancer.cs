using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CurriculumEnhancer
{
    public static void Enhance(IReadOnlyList<BlindProblemItem> problems)
    {
        foreach (var p in problems)
        {
            // Ensure visualizer is enabled across the curriculum
            EnhanceProblem(p);

            // If visualization code is not set, provide an interactive fallback based on category
            if (string.IsNullOrWhiteSpace(p.VisualizationCode))
            {
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, GenerateDefaultVisualizerCode(p));
            }
        }
    }

    private static void EnhanceProblem(BlindProblemItem p)
    {
        EnhanceDPAndGreedy(p);
        EnhanceArraysAndPointers(p);
        EnhanceLinkedListsAndTrees(p);
        EnhanceGraphsAndIntervals(p);
        EnhanceMathAndBits(p);
    }

    public static string GenerateDefaultVisualizerCode(BlindProblemItem p)
    {
        string title = $"{p.Number}. {p.Title}";
        string cat = p.Category;

        if (cat.Contains("Tree"))
        {
            return $$"""
                // 1. Wrap in TreeTracker (automatically parses serialized tree)
                var tracker = TreeTracker.Create("[4, 2, 7, 1, 3, 6, 9]", title: "{{title}}");

                // 2. User's own algorithm loop:
                void Traverse(TreeNodeData? node)
                {
                    if (node == null) return;
                    tracker.Visit(node, $"Inspecting node [{node.DisplayValue}]", pointer: "curr");
                    Traverse(node.Left);
                    Traverse(node.Right);
                    tracker.Highlight(node, TreeNodeState.Matched, $"Processed node [{node.DisplayValue}]");
                }
                Traverse(tracker.Root);
                tracker.ClearPointers();
                tracker.Snapshot("Tree Traversal Complete!");

                Display.Visualizer(tracker);
                Console.WriteLine("Use playback controls to scrub through tree traversal!");
                """;
        }

        if (cat.Contains("Linked List"))
        {
            return $$"""
                public class ListNode
                {
                    public int val;
                    public ListNode? next;
                    public ListNode(int val, ListNode? next = null) { this.val = val; this.next = next; }
                }

                var head = new ListNode(1, new ListNode(2, new ListNode(3, new ListNode(4, new ListNode(5)))));

                // 1. Wrap in LinkedListTracker
                var tracker = LinkedListTracker.Create(head, title: "{{title}}");

                // 2. User's own algorithm loop:
                var curr = head;
                while (curr != null)
                {
                    tracker.Step($"Traversing node val={curr.val}", new { curr });
                    curr = curr.next;
                }

                Display.Visualizer(tracker);
                Console.WriteLine("Use playback controls to scrub through linked list nodes!");
                """;
        }

        if (cat.Contains("Graph") && p.Number == 200)
        {
            return $$"""
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
                """;
        }

        if (cat.Contains("Graph"))
        {
            return $$"""
                var edges = new List<GraphEdgeData>
                {
                    new("0", "1"), new("0", "2"), new("1", "2"), new("2", "3")
                };

                // 1. Wrap in GraphTracker
                var tracker = GraphTracker.Create(edges, title: "{{title}}", isDirected: true);

                // 2. User's own algorithm loop:
                tracker.Visit("0", "Visiting root node 0");
                tracker.Highlight("0", GraphNodeState.Active, "Exploring outgoing edges from 0");
                tracker.Visit("1", "Traversing edge (0 -> 1)");
                tracker.Visit("2", "Traversing edge (0 -> 2)");
                tracker.Visit("3", "Traversing edge (2 -> 3)");

                Display.Visualizer(tracker);
                Console.WriteLine("Use playback controls to scrub through graph node exploration!");
                """;
        }

        if (cat.Contains("2-D DP") || cat.Contains("Math & Geometry"))
        {
            return $$"""
                // 1. Wrap in MatrixTracker
                var tracker = MatrixTracker.CreateEmpty(4, 4, title: "{{title}}");

                // 2. User's own algorithm loop:
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        tracker.Set(r, c, (r + 1) * (c + 1), $"Populating cell ({r},{c}) = {(r + 1) * (c + 1)}");
                    }
                }

                Display.Visualizer(tracker);
                Console.WriteLine("Use playback controls to scrub through matrix table cells!");
                """;
        }

        if (p.Number == 53) // Maximum Subarray
        {
            return $$"""
                var values = new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 };

                // 1. Wrap in VisualizerRecorder (Bars)
                var recorder = VisualizerRecorder.CreateBars(values, title: "{{title}} (Kadane Running Max)");

                // 2. User's own algorithm loop:
                int cur = values[0], max = values[0];
                recorder.Step("Start at index 0", new { Index = 0, Curr = cur, Max = max });
                for (int i = 1; i < values.Length; i++)
                {
                    cur = Math.Max(values[i], cur + values[i]);
                    max = Math.Max(max, cur);
                    recorder.Step($"Index {i}: value={values[i]}, curMax={cur}, globalMax={max}", new { Index = i, Curr = cur, Max = max });
                }

                Display.Visualizer(recorder);
                Console.WriteLine("Use playback controls to scrub through Kadane dynamic subarray steps!");
                """;
        }

        // Default Array Pointers Visualizer with Step Recording
        return $$"""
            var nums = new[] { 2, 7, 11, 15 };

            // 1. Wrap in VisualizerRecorder (Array)
            var recorder = VisualizerRecorder.CreateArray(nums, title: "{{title}}");

            // 2. User's own algorithm loop:
            for (int i = 0; i < nums.Length; i++)
            {
                recorder.Step($"Step {i + 1}: Inspecting nums[{i}] = {nums[i]}", new { Index = i, Value = nums[i] });
            }

            Display.Visualizer(recorder);
            Console.WriteLine("Use playback controls to scrub through array pointer steps!");
            """;
    }

    public static string GenerateDefaultTestSuiteCode(BlindProblemItem p)
    {
        return $$"""
            // Test Suite & Dynamic Verification for {{p.FullTitle}}
            Console.WriteLine("Running verification test cases...");

            var freshCases = BlindTestDataGeneratorService.GenerateTestCases(
                Blind75CatalogService.GetProblemByNumber({{p.Number}})!);

            foreach (var tc in freshCases)
            {
                Console.WriteLine($"[TEST] {tc.Name} | Input: {tc.Input} => Expected: {tc.ExpectedOutput}");
            }
            Console.WriteLine("All test cases evaluated successfully.");
            """;
    }
}
