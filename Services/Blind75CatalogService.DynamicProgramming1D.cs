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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are climbing a staircase. It takes `n` steps to reach the top. Each time you can either climb `1` or `2` steps. In how many distinct ways can you climb to the top?
            """,
            StarterCode = """
            public class Solution 
            {
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
            }

            var sol = new Solution();
            sol.ClimbStairs(5).Dump("ClimbStairs 5 (expected: 8)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "n=2", Input = "2", ExpectedOutput = "2" },
                new() { Name = "n=3", Input = "3", ExpectedOutput = "3" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are a professional robber planning to rob houses along a street. Each house has a certain amount of money stashed. Adjacent houses have security systems connected and **will automatically contact the police if two adjacent houses were broken into on the same night**.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int Rob(int[] nums) 
                {
                    int rob1 = 0, rob2 = 0;
                    foreach (var n in nums)
                    {
                        int temp = Math.Max(n + rob1, rob2);
                        rob1 = rob2;
                        rob2 = temp;
                    }
                    return rob2;
                }
            }

            var sol = new Solution();
            sol.Rob(new[] { 1, 2, 3, 1 }).Dump("Rob (expected: 4)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,3,1]", Input = "[1,2,3,1]", ExpectedOutput = "4" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            All houses at this place are **arranged in a circle**. That means the first house is the neighbor of the last one.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int Rob(int[] nums) 
                {
                    if (nums.Length == 1) return nums[0];
                    return Math.Max(RobRange(nums, 0, nums.Length - 2), RobRange(nums, 1, nums.Length - 1));
                }

                private int RobRange(int[] nums, int start, int end)
                {
                    int rob1 = 0, rob2 = 0;
                    for (int i = start; i <= end; i++)
                    {
                        int temp = Math.Max(nums[i] + rob1, rob2);
                        rob1 = rob2;
                        rob2 = temp;
                    }
                    return rob2;
                }
            }

            var sol = new Solution();
            sol.Rob(new[] { 2, 3, 2 }).Dump("Rob circular (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[2,3,2]", Input = "[2,3,2]", ExpectedOutput = "3" }
            }
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
            TimeComplexity = "O(N^2)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a string `s`, return the longest palindromic substring in `s`.
            """,
            StarterCode = """
            public class Solution 
            {
                public string LongestPalindrome(string s) 
                {
                    if (string.IsNullOrEmpty(s)) return "";
                    int start = 0, maxLen = 0;

                    void Expand(int l, int r)
                    {
                        while (l >= 0 && r < s.Length && s[l] == s[r])
                        {
                            if (r - l + 1 > maxLen) { start = l; maxLen = r - l + 1; }
                            l--; r++;
                        }
                    }

                    for (int i = 0; i < s.Length; i++)
                    {
                        Expand(i, i);
                        Expand(i, i + 1);
                    }
                    return s.Substring(start, maxLen);
                }
            }

            var sol = new Solution();
            sol.LongestPalindrome("babad").Dump("LongestPalindrome babad");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "babad", Input = "\"babad\"", ExpectedOutput = "bab" }
            }
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
            TimeComplexity = "O(N^2)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given a string `s`, return *the number of **palindromic substrings** in it*.
            """,
            StarterCode = """
            public class Solution 
            {
                public int CountSubstrings(string s) 
                {
                    int count = 0;
                    void Expand(int l, int r)
                    {
                        while (l >= 0 && r < s.Length && s[l] == s[r])
                        {
                            count++;
                            l--; r++;
                        }
                    }
                    for (int i = 0; i < s.Length; i++)
                    {
                        Expand(i, i);
                        Expand(i, i + 1);
                    }
                    return count;
                }
            }

            var sol = new Solution();
            sol.CountSubstrings("aaa").Dump("CountSubstrings aaa (expected: 6)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "aaa", Input = "\"aaa\"", ExpectedOutput = "6" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            A message containing letters from A-Z can be encoded into numbers using the mapping 'A' -> 1, 'B' -> 2, ... 'Z' -> 26.
            Given a string `s` containing only digits, return *the **number of ways** to decode it*.
            """,
            StarterCode = """
            public class Solution 
            {
                public int NumDecodings(string s) 
                {
                    if (string.IsNullOrEmpty(s) || s[0] == '0') return 0;
                    int dp1 = 1, dp2 = 1;
                    for (int i = 1; i < s.Length; i++)
                    {
                        int current = 0;
                        if (s[i] != '0') current += dp1;
                        int twoDigit = (s[i - 1] - '0') * 10 + (s[i] - '0');
                        if (twoDigit >= 10 && twoDigit <= 26) current += dp2;
                        dp2 = dp1;
                        dp1 = current;
                    }
                    return dp1;
                }
            }

            var sol = new Solution();
            sol.NumDecodings("226").Dump("NumDecodings 226 (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "226", Input = "\"226\"", ExpectedOutput = "3" }
            }
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
            TimeComplexity = "O(amount * coins)",
            SpaceComplexity = "O(amount)",
            DescriptionMarkdown = """
            You are given an integer array `coins` representing coins of different denominations and an integer `amount` representing a total amount of money.
            Return *the fewest number of coins that you need to make up that amount*. If impossible, return `-1`.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int CoinChange(int[] coins, int amount) 
                {
                    var dp = new int[amount + 1];
                    Array.Fill(dp, amount + 1);
                    dp[0] = 0;
                    for (int i = 1; i <= amount; i++)
                    {
                        foreach (var c in coins)
                        {
                            if (i - c >= 0) dp[i] = Math.Min(dp[i], 1 + dp[i - c]);
                        }
                    }
                    return dp[amount] > amount ? -1 : dp[amount];
                }
            }

            var sol = new Solution();
            sol.CoinChange(new[] { 1, 2, 5 }, 11).Dump("CoinChange [1,2,5], 11 (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[1,2,5], 11", Input = "[1,2,5], 11", ExpectedOutput = "3" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `nums`, find a subarray that has the largest product, and return *the product*.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int MaxProduct(int[] nums) 
                {
                    int res = nums[0];
                    int curMin = 1, curMax = 1;
                    foreach (var n in nums)
                    {
                        int temp = curMax * n;
                        curMax = Math.Max(n, Math.Max(temp, curMin * n));
                        curMin = Math.Min(n, Math.Min(temp, curMin * n));
                        res = Math.Max(res, curMax);
                    }
                    return res;
                }
            }

            var sol = new Solution();
            sol.MaxProduct(new[] { 2, 3, -2, 4 }).Dump("MaxProduct (expected: 6)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[2,3,-2,4]", Input = "[2,3,-2,4]", ExpectedOutput = "6" }
            }
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
            TimeComplexity = "O(N^2 * M)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given a string `s` and a dictionary of strings `wordDict`, return `true` if `s` can be segmented into a space-separated sequence of one or more dictionary words.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public bool WordBreak(string s, IList<string> wordDict) 
                {
                    var dp = new bool[s.Length + 1];
                    dp[s.Length] = true;
                    for (int i = s.Length - 1; i >= 0; i--)
                    {
                        foreach (var w in wordDict)
                        {
                            if (i + w.Length <= s.Length && s.Substring(i, w.Length) == w)
                            {
                                dp[i] = dp[i + w.Length];
                                if (dp[i]) break;
                            }
                        }
                    }
                    return dp[0];
                }
            }

            var sol = new Solution();
            sol.WordBreak("leetcode", new[] { "leet", "code" }).Dump("WordBreak (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "leetcode", Input = "\"leetcode\", [\"leet\",\"code\"]", ExpectedOutput = "True" }
            }
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
            TimeComplexity = "O(N log N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return the length of the longest strictly increasing subsequence.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int LengthOfLIS(int[] nums) 
                {
                    var tails = new List<int>();
                    foreach (var x in nums)
                    {
                        int idx = tails.BinarySearch(x);
                        if (idx < 0) idx = ~idx;
                        if (idx == tails.Count) tails.Add(x);
                        else tails[idx] = x;
                    }
                    return tails.Count;
                }
            }

            var sol = new Solution();
            sol.LengthOfLIS(new[] { 10, 9, 2, 5, 3, 7, 101, 18 }).Dump("LengthOfLIS (expected: 4)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard array", Input = "[10,9,2,5,3,7,101,18]", ExpectedOutput = "4" }
            }
        }
    };
}
