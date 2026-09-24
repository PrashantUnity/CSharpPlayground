using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static class BlindTestDataGeneratorService
{
    private static readonly Random _rng = new();

    public static List<TestCaseItem> GenerateTestCases(BlindProblemItem problem, int count = 3)
    {
        if (problem == null) return new List<TestCaseItem>();

        var list = new List<TestCaseItem>();

        switch (problem.Number)
        {
            case 1: // Two Sum
                list.Add(GenerateTwoSumCase("Edge: Negative Values", new[] { -10, -3, 2, 8, 14 }, 5));
                list.Add(GenerateTwoSumCase("Stress: Randomized", GenerateRandomArray(6, -20, 20), null));
                list.Add(GenerateTwoSumCase("Edge: Adjacent Pair", new[] { 100, 200, 300, 400 }, 700));
                break;

            case 217: // Contains Duplicate
                list.Add(new TestCaseItem { Name = "Edge: All Unique", Input = "[1, 2, 3, 4, 5, 6, 7]", ExpectedOutput = "false" });
                list.Add(new TestCaseItem { Name = "Edge: Duplicates at Ends", Input = "[99, 1, 2, 3, 4, 99]", ExpectedOutput = "true" });
                list.Add(new TestCaseItem { Name = "Stress: Large Clustered", Input = JsonSerializer.Serialize(GenerateRandomArray(10, 1, 5)), ExpectedOutput = "true" });
                break;

            case 242: // Valid Anagram
                list.Add(new TestCaseItem { Name = "Edge: Exact Match", Input = "\"algorithm\", \"algorithm\"", ExpectedOutput = "true" });
                list.Add(new TestCaseItem { Name = "Edge: Length Mismatch", Input = "\"rat\", \"car\"", ExpectedOutput = "false" });
                list.Add(new TestCaseItem { Name = "Stress: Scrambled Words", Input = "\"listen\", \"silent\"", ExpectedOutput = "true" });
                break;

            case 121: // Best Time to Buy and Sell Stock
                list.Add(new TestCaseItem { Name = "Edge: Monotonically Decreasing", Input = "[10, 9, 7, 5, 2, 1]", ExpectedOutput = "0" });
                list.Add(new TestCaseItem { Name = "Edge: Peak on Final Day", Input = "[2, 4, 1, 10]", ExpectedOutput = "9" });
                list.Add(new TestCaseItem { Name = "Stress: Volatile Market", Input = JsonSerializer.Serialize(GenerateRandomArray(8, 1, 30)), ExpectedOutput = "Evaluated" });
                break;

            case 53: // Maximum Subarray
                list.Add(new TestCaseItem { Name = "Edge: All Negatives", Input = "[-5, -1, -8, -4]", ExpectedOutput = "-1" });
                list.Add(new TestCaseItem { Name = "Edge: Single Element", Input = "[42]", ExpectedOutput = "42" });
                list.Add(new TestCaseItem { Name = "Stress: Alternating Fluctuations", Input = "[5, 4, -1, 7, 8]", ExpectedOutput = "23" });
                break;

            case 55: // Jump Game
                list.Add(new TestCaseItem { Name = "Edge: Zero Trap", Input = "[3, 2, 1, 0, 4]", ExpectedOutput = "false" });
                list.Add(new TestCaseItem { Name = "Edge: Minimum Length", Input = "[0]", ExpectedOutput = "true" });
                list.Add(new TestCaseItem { Name = "Stress: Direct Jump", Input = "[5, 0, 0, 0, 0, 1]", ExpectedOutput = "true" });
                break;

            case 70: // Climbing Stairs
                int n1 = _rng.Next(6, 12);
                int n2 = _rng.Next(13, 20);
                list.Add(new TestCaseItem { Name = $"Random: n={n1}", Input = $"{n1}", ExpectedOutput = $"{ClimbStairs(n1)}" });
                list.Add(new TestCaseItem { Name = $"Random: n={n2}", Input = $"{n2}", ExpectedOutput = $"{ClimbStairs(n2)}" });
                list.Add(new TestCaseItem { Name = "Edge: Boundary n=1", Input = "1", ExpectedOutput = "1" });
                break;

            case 198: // House Robber
                list.Add(new TestCaseItem { Name = "Edge: Two Houses", Input = "[2, 50]", ExpectedOutput = "50" });
                list.Add(new TestCaseItem { Name = "Edge: Identical Wealth", Input = "[5, 5, 5, 5, 5]", ExpectedOutput = "15" });
                list.Add(new TestCaseItem { Name = "Stress: High Value Outlier", Input = "[1, 100, 1, 1, 100]", ExpectedOutput = "200" });
                break;

            case 322: // Coin Change
                list.Add(new TestCaseItem { Name = "Edge: Impossible Amount", Input = "[2], 3", ExpectedOutput = "-1" });
                list.Add(new TestCaseItem { Name = "Edge: Zero Target", Input = "[1, 2, 5], 0", ExpectedOutput = "0" });
                list.Add(new TestCaseItem { Name = "Tricky: Greedy Trap", Input = "[1, 3, 4], 6", ExpectedOutput = "2" });
                break;

            case 206: // Reverse Linked List
                list.Add(new TestCaseItem { Name = "Edge: Single Node", Input = "[42]", ExpectedOutput = "[42]" });
                list.Add(new TestCaseItem { Name = "Edge: Empty List", Input = "[]", ExpectedOutput = "[]" });
                list.Add(new TestCaseItem { Name = "Stress: Sequence 1..8", Input = "[1, 2, 3, 4, 5, 6, 7, 8]", ExpectedOutput = "[8, 7, 6, 5, 4, 3, 2, 1]" });
                break;

            case 226: // Invert Binary Tree
                list.Add(new TestCaseItem { Name = "Edge: Single Root", Input = "[1]", ExpectedOutput = "[1]" });
                list.Add(new TestCaseItem { Name = "Edge: Skewed Left", Input = "[1, 2, null, 3]", ExpectedOutput = "[1, null, 2, null, 3]" });
                list.Add(new TestCaseItem { Name = "Standard: Balanced BST", Input = "[4, 2, 7, 1, 3, 6, 9]", ExpectedOutput = "[4, 7, 2, 9, 6, 3, 1]" });
                break;

            case 200: // Number of Islands
                list.Add(new TestCaseItem { Name = "Edge: All Water", Input = "[[\"0\",\"0\"],[\"0\",\"0\"]]", ExpectedOutput = "0" });
                list.Add(new TestCaseItem { Name = "Edge: Solid Continent", Input = "[[\"1\",\"1\"],[\"1\",\"1\"]]", ExpectedOutput = "1" });
                list.Add(new TestCaseItem { Name = "Stress: Checkerboard", Input = "[[\"1\",\"0\",\"1\"],[\"0\",\"1\",\"0\"],[\"1\",\"0\",\"1\"]]", ExpectedOutput = "5" });
                break;

            case 56: // Merge Intervals
                list.Add(new TestCaseItem { Name = "Edge: No Overlaps", Input = "[[1,2],[3,4],[5,6]]", ExpectedOutput = "[[1,2],[3,4],[5,6]]" });
                list.Add(new TestCaseItem { Name = "Edge: Fully Enclosed", Input = "[[1,10],[2,5],[3,7]]", ExpectedOutput = "[[1,10]]" });
                list.Add(new TestCaseItem { Name = "Stress: Chain Merge", Input = "[[1,4],[2,5],[4,7],[6,9]]", ExpectedOutput = "[[1,9]]" });
                break;

            default:
                // Category-based generative fallback
                list.AddRange(GenerateFallbackCasesForCategory(problem));
                break;
        }

        // Fill remaining up to count if needed
        while (list.Count < count)
        {
            list.Add(new TestCaseItem
            {
                Name = $"Generated Case #{list.Count + 1}",
                Input = $"Sample input for {problem.Title}",
                ExpectedOutput = "Valid evaluation"
            });
        }

        return list;
    }

    private static TestCaseItem GenerateTwoSumCase(string name, int[] nums, int? targetOverride)
    {
        int i1 = 0, i2 = 1;
        if (nums.Length >= 2)
        {
            i1 = _rng.Next(0, nums.Length - 1);
            i2 = _rng.Next(i1 + 1, nums.Length);
        }
        int target = targetOverride ?? (nums[i1] + nums[i2]);
        string input = $"{JsonSerializer.Serialize(nums)}, {target}";
        string expected = targetOverride.HasValue
            ? FindTwoSumSolution(nums, targetOverride.Value)
            : $"[{i1},{i2}]";

        return new TestCaseItem { Name = name, Input = input, ExpectedOutput = expected };
    }

    private static string FindTwoSumSolution(int[] nums, int target)
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++)
        {
            int diff = target - nums[i];
            if (map.TryGetValue(diff, out int idx)) return $"[{idx},{i}]";
            map[nums[i]] = i;
        }
        return "[]";
    }

    private static int ClimbStairs(int n)
    {
        if (n <= 2) return n;
        int a = 1, b = 2;
        for (int i = 3; i <= n; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    private static int[] GenerateRandomArray(int length, int min, int max)
    {
        var arr = new int[length];
        for (int i = 0; i < length; i++) arr[i] = _rng.Next(min, max + 1);
        return arr;
    }

    private static IEnumerable<TestCaseItem> GenerateFallbackCasesForCategory(BlindProblemItem p)
    {
        var list = new List<TestCaseItem>();
        string cat = p.Category;

        if (cat.Contains("Tree"))
        {
            list.Add(new TestCaseItem { Name = "Edge: Leaf Node", Input = "[10]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Random: Skewed Tree", Input = "[5, 4, null, 3, null, 2]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Standard: Complete Binary Tree", Input = "[10, 5, 15, 3, 7, 12, 18]", ExpectedOutput = "Evaluated" });
        }
        else if (cat.Contains("Linked List"))
        {
            list.Add(new TestCaseItem { Name = "Edge: Single Element", Input = "[100]", ExpectedOutput = "[100]" });
            list.Add(new TestCaseItem { Name = "Standard: Even Count List", Input = "[2, 4, 6, 8]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Standard: Odd Count List", Input = "[1, 3, 5, 7, 9]", ExpectedOutput = "Evaluated" });
        }
        else if (cat.Contains("Graph"))
        {
            list.Add(new TestCaseItem { Name = "Edge: Disconnected Nodes", Input = "n=4, edges=[[0,1],[2,3]]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Stress: Cyclic Directed", Input = "n=3, edges=[[0,1],[1,2],[2,0]]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Standard: Star Topology", Input = "n=5, edges=[[0,1],[0,2],[0,3],[0,4]]", ExpectedOutput = "Evaluated" });
        }
        else if (cat.Contains("DP") || cat.Contains("Greedy"))
        {
            list.Add(new TestCaseItem { Name = "Edge: Minimum State", Input = "[1]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Edge: Zero Boundary", Input = "0", ExpectedOutput = "0" });
            list.Add(new TestCaseItem { Name = "Stress: Alternating Signs", Input = "[-2, 5, -1, 8, -4]", ExpectedOutput = "Evaluated" });
        }
        else if (cat.Contains("Interval"))
        {
            list.Add(new TestCaseItem { Name = "Edge: Single Interval", Input = "[[1, 5]]", ExpectedOutput = "[[1, 5]]" });
            list.Add(new TestCaseItem { Name = "Edge: Touching Boundaries", Input = "[[1, 2], [2, 3], [3, 4]]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Stress: Nested Intervals", Input = "[[1, 10], [2, 3], [4, 7]]", ExpectedOutput = "Evaluated" });
        }
        else
        {
            list.Add(new TestCaseItem { Name = "Edge: Boundary Values", Input = "[-1000, 0, 1000]", ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Random: Small Array", Input = JsonSerializer.Serialize(GenerateRandomArray(5, 1, 50)), ExpectedOutput = "Evaluated" });
            list.Add(new TestCaseItem { Name = "Random: Large Array", Input = JsonSerializer.Serialize(GenerateRandomArray(10, -50, 50)), ExpectedOutput = "Evaluated" });
        }

        return list;
    }
}
