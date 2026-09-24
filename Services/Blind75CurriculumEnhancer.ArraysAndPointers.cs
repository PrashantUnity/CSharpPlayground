using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CurriculumEnhancer
{
    private static void EnhanceArraysAndPointers(BlindProblemItem p)
    {
        switch (p.Number)
        {
            case 1: // Two Sum
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Two Sum
                    1. **Complement Equation**: `x + y = target` => `y = target - x`.
                    2. **Lookup Bottleneck**: In the brute-force approach, searching for `y` takes $O(N)$ time.
                    3. **State Trade-off**: Trade space for time using a Hash Map (`Dictionary<int, int>`) storing `{ value: index }` for $O(1)$ complement lookups.
                    4. **Single-Pass Trick**: You can insert elements as you iterate, avoiding duplicate self-matching.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Brute Force (Nested Loops)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Check all pairs (i, j) with two nested loops until nums[i] + nums[j] == target.",
                    BottleneckExplanation = "Checks N*(N-1)/2 pairs without remembering previous numbers.",
                    Code = """
                    public int[] TwoSumNaive(int[] nums, int target)
                    {
                        for (int i = 0; i < nums.Length; i++)
                            for (int j = i + 1; j < nums.Length; j++)
                                if (nums[i] + nums[j] == target) return new[] { i, j };
                        return Array.Empty<int>();
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Two Pointers with Sorting",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(N log N)",
                    SpaceComplexity = "O(N) (Index pairs)",
                    Intuition = "Sort elements and use converging left and right pointers. Increment left if sum < target, decrement right if sum > target.",
                    GreedyChoiceProperty = "Sorted array guarantees monotonic sum changes when moving pointers.",
                    Code = """
                    public int[] TwoSumTwoPointers(int[] nums, int target)
                    {
                        var indexed = nums.Select((val, idx) => (val, idx)).OrderBy(x => x.val).ToArray();
                        int l = 0, r = indexed.Length - 1;
                        while (l < r)
                        {
                            int sum = indexed[l].val + indexed[r].val;
                            if (sum == target) return new[] { indexed[l].idx, indexed[r].idx };
                            if (sum < target) l++; else r--;
                        }
                        return Array.Empty<int>();
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Optimal One-Pass Hash Map",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Store each number in a dictionary. Look up the complement 'target - nums[i]' in O(1) time.",
                    Code = """
                    public int[] TwoSum(int[] nums, int target)
                    {
                        var map = new Dictionary<int, int>();
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int complement = target - nums[i];
                            if (map.TryGetValue(complement, out int idx)) return new[] { idx, i };
                            map[nums[i]] = i;
                        }
                        return Array.Empty<int>();
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var nums = new[] { 2, 7, 11, 15 };
                    int target = 9;

                    // 1. Wrap in VisualizerRecorder (Array)
                    var recorder = VisualizerRecorder.CreateArray(nums, title: "1. Two Sum (Step Recording)");

                    // 2. User's own algorithm loop:
                    var map = new Dictionary<int, int>();
                    for (int i = 0; i < nums.Length; i++)
                    {
                        int complement = target - nums[i];
                        recorder.Step($"Check nums[{i}]={nums[i]}, complement={complement}", new { Index = i, Value = nums[i], Complement = complement });
                        if (map.TryGetValue(complement, out int idx))
                        {
                            recorder.Step($"Found match at index {idx} and {i} ({nums[idx]} + {nums[i]} = {target})!", new { Match1 = idx, Match2 = i });
                            break;
                        }
                        map[nums[i]] = i;
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through Two Sum complement lookups!");
                    """);
                break;

            case 11: // Container With Most Water
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Container With Most Water
                    1. **Area Equation**: `Area = (right - left) * Math.Min(height[left], height[right])`.
                    2. **Greedy Reduction**: Start with the widest possible container (`left = 0`, `right = length - 1`).
                    3. **Which Pointer to Move?**: The area is constrained by the **shorter line**. Moving the taller line inward can only decrease width without any chance of increasing the constraining height. Therefore, greedily move the shorter line!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Exhaustive (All Pairs)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Calculate water trapped for every possible pair of lines.",
                    BottleneckExplanation = "Tests all combinations even when moving a taller line is provably worse.",
                    Code = """
                    public int MaxAreaNaive(int[] height)
                    {
                        int maxWater = 0;
                        for (int i = 0; i < height.Length; i++)
                            for (int j = i + 1; j < height.Length; j++)
                                maxWater = Math.Max(maxWater, (j - i) * Math.Min(height[i], height[j]));
                        return maxWater;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy Two Pointers (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Always advance the pointer pointing to the shorter vertical bar, preserving maximum potential height.",
                    Intuition = "Inward converging two pointers starting at opposite ends.",
                    Code = """
                    public int MaxArea(int[] height)
                    {
                        int maxArea = 0, l = 0, r = height.Length - 1;
                        while (l < r)
                        {
                            int area = (r - l) * Math.Min(height[l], height[r]);
                            maxArea = Math.Max(maxArea, area);
                            if (height[l] < height[r]) l++; else r--;
                        }
                        return maxArea;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var heights = new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 };

                    // 1. Wrap in VisualizerRecorder (Bars)
                    var recorder = VisualizerRecorder.CreateBars(heights, title: "11. Container With Most Water (Two Pointers)");

                    // 2. User's own algorithm loop:
                    int l = 0, r = heights.Length - 1, maxArea = 0;
                    while (l < r)
                    {
                        int area = (r - l) * Math.Min(heights[l], heights[r]);
                        maxArea = Math.Max(maxArea, area);
                        recorder.Step($"L={l} ({heights[l]}), R={r} ({heights[r]}), Area={area}, MaxArea={maxArea}", new { Left = l, Right = r, Area = area, MaxArea = maxArea });
                        if (heights[l] < heights[r]) l++; else r--;
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through converging pointer bounds!");
                    """);
                break;

            case 121: // Best Time to Buy and Sell Stock
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Best Time to Buy and Sell Stock
                    1. **Order Constraint**: You must buy before you sell (`i < j`).
                    2. **Greedy State Tracking**: As you sweep day by day, what is the best price to buy? It is simply the **lowest price seen so far**!
                    3. **Max Profit Formula**: At day `i`, `profit = prices[i] - minPriceSoFar`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Double Loop",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Check profit for every combination of buy day i and sell day j.",
                    BottleneckExplanation = "Recalculates min price across all preceding days redundantly.",
                    Code = """
                    public int MaxProfitNaive(int[] prices)
                    {
                        int max = 0;
                        for (int i = 0; i < prices.Length; i++)
                            for (int j = i + 1; j < prices.Length; j++)
                                max = Math.Max(max, prices[j] - prices[i]);
                        return max;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Dynamic Programming (State Array)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    RecurrenceRelation = "minPrices[i] = Math.Min(minPrices[i-1], prices[i])",
                    Intuition = "Maintain prefix minimums array, then compute max difference.",
                    Code = """
                    public int MaxProfitDP(int[] prices)
                    {
                        if (prices.Length == 0) return 0;
                        int[] minPrices = new int[prices.Length];
                        minPrices[0] = prices[0];
                        for (int i = 1; i < prices.Length; i++)
                            minPrices[i] = Math.Min(minPrices[i - 1], prices[i]);

                        int maxProfit = 0;
                        for (int i = 0; i < prices.Length; i++)
                            maxProfit = Math.Max(maxProfit, prices[i] - minPrices[i]);
                        return maxProfit;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy One-Pass (Optimal)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Always remember the lowest buy price seen so far.",
                    Intuition = "Single linear scan updating minPrice and maxProfit in lockstep.",
                    Code = """
                    public int MaxProfit(int[] prices)
                    {
                        int minPrice = int.MaxValue;
                        int maxProfit = 0;
                        foreach (int p in prices)
                        {
                            if (p < minPrice) minPrice = p;
                            else if (p - minPrice > maxProfit) maxProfit = p - minPrice;
                        }
                        return maxProfit;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var prices = new[] { 7, 1, 5, 3, 6, 4 };

                    // 1. Wrap in VisualizerRecorder (Bars)
                    var recorder = VisualizerRecorder.CreateBars(prices, title: "121. Best Time to Buy and Sell Stock (One-Pass)");

                    // 2. User's own algorithm loop:
                    int minPrice = int.MaxValue, maxProfit = 0;
                    for (int i = 0; i < prices.Length; i++)
                    {
                        if (prices[i] < minPrice)
                        {
                            minPrice = prices[i];
                            recorder.Step($"Day {i}: new min buy price = {minPrice}", new { Day = i, Price = prices[i], MinPrice = minPrice, MaxProfit = maxProfit });
                        }
                        else if (prices[i] - minPrice > maxProfit)
                        {
                            maxProfit = prices[i] - minPrice;
                            recorder.Step($"Day {i}: sell for profit = {maxProfit}", new { Day = i, Price = prices[i], MinPrice = minPrice, MaxProfit = maxProfit });
                        }
                        else
                        {
                            recorder.Step($"Day {i}: price {prices[i]}, current profit {prices[i] - minPrice}", new { Day = i, Price = prices[i], MinPrice = minPrice, MaxProfit = maxProfit });
                        }
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through daily price progression!");
                    """);
                break;
        }
    }
}
