using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CurriculumEnhancer
{
    private static void EnhanceDPAndGreedy(BlindProblemItem p)
    {
        switch (p.Number)
        {
            case 70: // Climbing Stairs
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Climbing Stairs
                    1. **Subproblem Identification**: To reach step `n`, your final move was either 1 step (from `n-1`) or 2 steps (from `n-2`).
                    2. **Invariant / Recurrence**: The distinct ways to step `n` is exactly `ways(n) = ways(n-1) + ways(n-2)`.
                    3. **Base Cases**: `ways(1) = 1`, `ways(2) = 2`.
                    4. **Space Optimization**: Because each calculation only requires the previous two numbers, you never need to store an entire array; two variables suffice!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive / Brute Force (Recursive Tree)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^N)",
                    SpaceComplexity = "O(N) (Recursion Stack)",
                    Intuition = "Directly implement the recursive definition: Climb(n-1) + Climb(n-2). Generates an exponential binary call tree.",
                    BottleneckExplanation = "Severe redundant recalculation. For example, Climb(n-2) is computed independently in multiple branches.",
                    Code = """
                    public int ClimbStairsNaive(int n)
                    {
                        if (n <= 2) return n;
                        return ClimbStairsNaive(n - 1) + ClimbStairsNaive(n - 2);
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Dynamic Programming (Bottom-Up Tabulation)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    RecurrenceRelation = "dp[i] = dp[i-1] + dp[i-2]",
                    Intuition = "Allocate an array of size n+1 and build the answers sequentially from 1 to n.",
                    Code = """
                    public int ClimbStairsDP(int n)
                    {
                        if (n <= 2) return n;
                        int[] dp = new int[n + 1];
                        dp[1] = 1; dp[2] = 2;
                        for (int i = 3; i <= n; i++) dp[i] = dp[i - 1] + dp[i - 2];
                        return dp[n];
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Optimal State-Reduction (Fibonacci Memory)",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Maintain only two variables 'prev' and 'curr', swapping values on each step.",
                    Code = """
                    public int ClimbStairs(int n)
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
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    int n = 5;
                    var dp = new int[n + 1];

                    // 1. Wrap in VisualizerRecorder (Array)
                    var recorder = VisualizerRecorder.CreateArray(dp, title: "70. Climbing Stairs (DP Step Recording)");

                    // 2. User's own algorithm loop:
                    dp[1] = 1; dp[2] = 2;
                    recorder.Step("Base cases: dp[1]=1, dp[2]=2", new { step = 2, dp1 = 1, dp2 = 2 });
                    for (int i = 3; i <= n; i++)
                    {
                        dp[i] = dp[i - 1] + dp[i - 2];
                        recorder.Step($"Step {i}: dp[{i}] = dp[{i-1}] ({dp[i-1]}) + dp[{i-2}] ({dp[i-2]}) = {dp[i]} ways", new { step = i, ways = dp[i] });
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through Fibonacci DP transitions!");
                    """);
                break;

            case 198: // House Robber
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: House Robber
                    1. **Decision at House `i`**: Either rob house `i` (cannot rob `i-1`, but can take loot up to `i-2`) OR skip house `i` (loot is max loot up to `i-1`).
                    2. **Recurrence**: `dp[i] = max(dp[i-1], dp[i-2] + nums[i])`.
                    3. **Space Reduction**: Notice that `dp[i]` only depends on `dp[i-1]` and `dp[i-2]`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Exhaustive Search",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Try all valid subsets of non-adjacent houses recursively.",
                    BottleneckExplanation = "Explores the same sub-indices repeatedly without caching previous results.",
                    Code = """
                    public int RobNaive(int[] nums, int i = 0)
                    {
                        if (i >= nums.Length) return 0;
                        return Math.Max(nums[i] + RobNaive(nums, i + 2), RobNaive(nums, i + 1));
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Dynamic Programming (Tabulation)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(N)",
                    RecurrenceRelation = "dp[i] = Math.Max(dp[i-1], dp[i-2] + nums[i])",
                    Intuition = "Store the maximum loot obtainable up to each house index.",
                    Code = """
                    public int RobDP(int[] nums)
                    {
                        if (nums.Length == 0) return 0;
                        if (nums.Length == 1) return nums[0];
                        int[] dp = new int[nums.Length];
                        dp[0] = nums[0];
                        dp[1] = Math.Max(nums[0], nums[1]);
                        for (int i = 2; i < nums.Length; i++)
                            dp[i] = Math.Max(dp[i - 1], dp[i - 2] + nums[i]);
                        return dp[^1];
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Optimal O(1) Space Kadane-Style",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Keep track of 'robPrevious' and 'robTwoBack' variables as you sweep through houses.",
                    Code = """
                    public int Rob(int[] nums)
                    {
                        int rob1 = 0, rob2 = 0;
                        foreach (int n in nums)
                        {
                            int newRob = Math.Max(rob1 + n, rob2);
                            rob1 = rob2;
                            rob2 = newRob;
                        }
                        return rob2;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var houses = new[] { 2, 7, 9, 3, 1 };

                    // 1. Wrap in VisualizerRecorder (Bars)
                    var recorder = VisualizerRecorder.CreateBars(houses, title: "198. House Robber (Step Recording)");

                    // 2. User's own algorithm loop:
                    int rob1 = 0, rob2 = 0;
                    recorder.Step("Start at house 0", new { rob1 = 0, rob2 = 0 });
                    for (int i = 0; i < houses.Length; i++)
                    {
                        int newRob = Math.Max(rob1 + houses[i], rob2);
                        rob1 = rob2;
                        rob2 = newRob;
                        recorder.Step($"House {i} (${houses[i]}): max loot = ${rob2}", new { House = i, Wealth = houses[i], MaxLoot = rob2 });
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through House Robber decisions!");
                    """);
                break;

            case 322: // Coin Change
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Coin Change
                    1. **Unbounded Knapsack Variation**: You have infinite coins of each denomination.
                    2. **Why Greedy Fails**: Picking the largest coin first does NOT guarantee minimum total coins!
                       - Example: `coins = [1, 3, 4]`, `amount = 6`.
                       - Greedy picks: `4 + 1 + 1` = 3 coins.
                       - Optimal DP picks: `3 + 3` = 2 coins!
                    3. **DP Recurrence**: For every coin `c`, `dp[amount] = min(dp[amount], dp[amount - c] + 1)`.
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Recursion (Exhaustive)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(S^N)",
                    SpaceComplexity = "O(Amount)",
                    Intuition = "Branch on every coin denomination for each remaining amount.",
                    BottleneckExplanation = "Exponential branch explosion with overlapping amount states.",
                    Code = """
                    public int CoinChangeNaive(int[] coins, int amount)
                    {
                        if (amount == 0) return 0;
                        if (amount < 0) return int.MaxValue;
                        int minCoins = int.MaxValue;
                        foreach (int c in coins)
                        {
                            int res = CoinChangeNaive(coins, amount - c);
                            if (res != int.MaxValue) minCoins = Math.Min(minCoins, res + 1);
                        }
                        return minCoins;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy Paradigm (Why It Fails Demonstration)",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(N log N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Fails for arbitrary coin sets. Only provably optimal for canonical systems (e.g., standard US currency).",
                    Intuition = "Greedily picking the largest coin can leave an inefficient remainder (e.g. [1, 3, 4], amount 6 gives 4+1+1=3 instead of 3+3=2).",
                    Code = """
                    // Demonstration: Greedy coin picker (can produce sub-optimal answers)
                    public int CoinChangeGreedyDemo(int[] coins, int amount)
                    {
                        Array.Sort(coins);
                        int count = 0;
                        for (int i = coins.Length - 1; i >= 0; i--)
                        {
                            count += amount / coins[i];
                            amount %= coins[i];
                        }
                        return amount == 0 ? count : -1;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Dynamic Programming (Bottom-Up Tabulation)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(Amount * Coins.Length)",
                    SpaceComplexity = "O(Amount)",
                    RecurrenceRelation = "dp[i] = Math.Min(dp[i], dp[i - coin] + 1)",
                    Intuition = "Fill a 1D DP table up to 'amount' initialized to infinity, with dp[0] = 0.",
                    Code = """
                    public int CoinChange(int[] coins, int amount)
                    {
                        var dp = new int[amount + 1];
                        Array.Fill(dp, amount + 1);
                        dp[0] = 0;
                        for (int i = 1; i <= amount; i++)
                        {
                            foreach (int c in coins)
                            {
                                if (i - c >= 0) dp[i] = Math.Min(dp[i], dp[i - c] + 1);
                            }
                        }
                        return dp[amount] > amount ? -1 : dp[amount];
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var coins = new[] { 1, 3, 4 };
                    int amount = 6;
                    var dp = new int[amount + 1];
                    Array.Fill(dp, amount + 1);
                    dp[0] = 0;

                    // 1. Wrap in VisualizerRecorder (Array)
                    var recorder = VisualizerRecorder.CreateArray(dp, title: "322. Coin Change (Step Recording)");

                    // 2. User's own algorithm loop:
                    recorder.Step("Initialized DP table with inf, dp[0]=0", new { amount, coins });
                    for (int i = 1; i <= amount; i++)
                    {
                        foreach (int c in coins)
                        {
                            if (i - c >= 0 && dp[i - c] + 1 < dp[i])
                            {
                                dp[i] = dp[i - c] + 1;
                                recorder.Step($"Amount {i} using coin {c}: dp[{i}] = {dp[i]}", new { target = i, coin = c, minCoins = dp[i] });
                            }
                        }
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through Coin Change subproblems!");
                    """);
                break;

            case 53: // Maximum Subarray
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Maximum Subarray (Kadane)
                    1. **Contiguity Invariant**: Any contiguous subarray ends at some index `i`.
                    2. **Greedy Choice**: When standing at index `i`, should you append `nums[i]` to the previous subarray sum, or start fresh from `nums[i]`?
                    3. **Decision Rule**: `currentSum = Math.Max(nums[i], currentSum + nums[i])`. If `currentSum < 0`, it hurts any future subarray, so greedily discard it!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Brute Force (All Subarrays)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Iterate through every starting index i and every ending index j, keeping a running sum.",
                    BottleneckExplanation = "Checks all N*(N+1)/2 subarrays without leveraging optimal substructure.",
                    Code = """
                    public int MaxSubArrayNaive(int[] nums)
                    {
                        int maxSum = int.MinValue;
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int sum = 0;
                            for (int j = i; j < nums.Length; j++)
                            {
                                sum += nums[j];
                                maxSum = Math.Max(maxSum, sum);
                            }
                        }
                        return maxSum;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy / DP Kadane's Algorithm",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Discard negative running prefixes immediately; they can never increase subsequent sums.",
                    RecurrenceRelation = "dp[i] = Math.Max(nums[i], dp[i-1] + nums[i])",
                    Intuition = "Linear scan keeping track of max ending here and global maximum.",
                    Code = """
                    public int MaxSubArray(int[] nums)
                    {
                        int maxSoFar = nums[0];
                        int currentMax = nums[0];
                        for (int i = 1; i < nums.Length; i++)
                        {
                            currentMax = Math.Max(nums[i], currentMax + nums[i]);
                            maxSoFar = Math.Max(maxSoFar, currentMax);
                        }
                        return maxSoFar;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var nums = new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 };

                    // 1. Wrap in VisualizerRecorder (Bars)
                    var recorder = VisualizerRecorder.CreateBars(nums, title: "53. Maximum Subarray (Kadane Step Recording)");

                    // 2. User's own algorithm loop:
                    int maxSoFar = nums[0], currentMax = nums[0];
                    recorder.Step("Start at index 0", new { Index = 0, Curr = currentMax, Max = maxSoFar });
                    for (int i = 1; i < nums.Length; i++)
                    {
                        currentMax = Math.Max(nums[i], currentMax + nums[i]);
                        maxSoFar = Math.Max(maxSoFar, currentMax);
                        recorder.Step($"Index {i}: nums[{i}] = {nums[i]}, currentMax = {currentMax}, maxSoFar = {maxSoFar}", new { Index = i, Curr = currentMax, Max = maxSoFar });
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through Kadane dynamic subarray steps!");
                    """);
                break;

            case 55: // Jump Game
                p.GetType().GetProperty(nameof(p.ThinkingProcessMarkdown))?.SetValue(p, """
                    ### 🧠 How to Think: Jump Game
                    1. **Forward Greedy Reach**: At any index `i`, the furthest you can ever reach is `i + nums[i]`.
                    2. **Invariant**: If current index `i` exceeds `maxReach`, you are stuck in a dead-end and can never proceed.
                    3. **Goal Reached**: If `maxReach >= nums.Length - 1`, return true immediately!
                    """);
                p.Approaches.Clear();
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Naive Backtracking (Recursive)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(2^N)",
                    SpaceComplexity = "O(N)",
                    Intuition = "Try every jump length from 1 to nums[curr] recursively.",
                    BottleneckExplanation = "Severe redundant branch exploration.",
                    Code = """
                    public bool CanJumpNaive(int[] nums, int pos = 0)
                    {
                        if (pos >= nums.Length - 1) return true;
                        int furthest = Math.Min(pos + nums[pos], nums.Length - 1);
                        for (int next = furthest; next > pos; next--)
                            if (CanJumpNaive(nums, next)) return true;
                        return false;
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Dynamic Programming (Bottom-Up Reachability)",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(N^2)",
                    SpaceComplexity = "O(N)",
                    RecurrenceRelation = "dp[i] = exists j <= i + nums[i] where dp[j] == true",
                    Intuition = "Work backwards from end or forwards with a boolean reachability array.",
                    Code = """
                    public bool CanJumpDP(int[] nums)
                    {
                        bool[] canReach = new bool[nums.Length];
                        canReach[0] = true;
                        for (int i = 0; i < nums.Length; i++)
                        {
                            if (!canReach[i]) continue;
                            for (int j = 1; j <= nums[i] && i + j < nums.Length; j++)
                                canReach[i + j] = true;
                        }
                        return canReach[^1];
                    }
                    """
                });
                p.Approaches.Add(new ProblemApproachItem
                {
                    Name = "Greedy Max Reachability",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(N)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "Always extend the global horizon maxReach = Math.Max(maxReach, i + nums[i]).",
                    Intuition = "One pass through the array maintaining the furthest reachable index.",
                    Code = """
                    public bool CanJump(int[] nums)
                    {
                        int maxReach = 0;
                        for (int i = 0; i < nums.Length; i++)
                        {
                            if (i > maxReach) return false;
                            maxReach = Math.Max(maxReach, i + nums[i]);
                            if (maxReach >= nums.Length - 1) return true;
                        }
                        return true;
                    }
                    """
                });
                p.GetType().GetProperty(nameof(p.VisualizationCode))?.SetValue(p, """
                    var nums = new[] { 2, 3, 1, 1, 4 };

                    // 1. Wrap in VisualizerRecorder (Array)
                    var recorder = VisualizerRecorder.CreateArray(nums, title: "55. Jump Game (Horizon Progression)");

                    // 2. User's own algorithm loop:
                    int maxReach = 0;
                    recorder.Step("Start at index 0", new { i = 0, maxReach });
                    for (int i = 0; i < nums.Length; i++)
                    {
                        if (i > maxReach)
                        {
                            recorder.Step($"Stuck at index {i}!", new { i, maxReach, Status = "Failed" });
                            break;
                        }
                        maxReach = Math.Max(maxReach, i + nums[i]);
                        recorder.Step($"Index {i}: can jump {nums[i]} steps. Horizon extended to {maxReach}", new { i, jump = nums[i], maxReach });
                        if (maxReach >= nums.Length - 1)
                        {
                            recorder.Step($"Goal reached! maxReach ({maxReach}) >= end", new { i, maxReach, Status = "Success" });
                            break;
                        }
                    }

                    Display.Visualizer(recorder);
                    Console.WriteLine("Use playback controls to scrub through greedy jump reach!");
                    """);
                break;
        }
    }
}
