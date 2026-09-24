using System;
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
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            There is a robot on an `m x n` grid. The robot is initially located at the top-left corner. The robot tries to move to the bottom-right corner. Return the number of possible unique paths.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int UniquePaths(int m, int n) 
                {
                    var row = new int[n];
                    Array.Fill(row, 1);
                    for (int i = 0; i < m - 1; i++)
                    {
                        var newRow = new int[n];
                        newRow[0] = 1;
                        for (int j = 1; j < n; j++) newRow[j] = newRow[j - 1] + row[j];
                        row = newRow;
                    }
                    return row[n - 1];
                }
            }

            var sol = new Solution();
            sol.UniquePaths(3, 7).Dump("UniquePaths 3x7 (expected: 28)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3x7", Input = "3, 7", ExpectedOutput = "28" }
            }
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
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(M * N)",
            DescriptionMarkdown = """
            Given two strings `text1` and `text2`, return the length of their longest common subsequence. If there is no common subsequence, return `0`.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int LongestCommonSubsequence(string text1, string text2) 
                {
                    int m = text1.Length, n = text2.Length;
                    var dp = new int[m + 1, n + 1];
                    for (int i = m - 1; i >= 0; i--)
                    {
                        for (int j = n - 1; j >= 0; j--)
                        {
                            if (text1[i] == text2[j]) dp[i, j] = 1 + dp[i + 1, j + 1];
                            else dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                        }
                    }
                    return dp[0, 0];
                }
            }

            var sol = new Solution();
            sol.LongestCommonSubsequence("abcde", "ace").Dump("LCS (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "abcde and ace", Input = "\"abcde\", \"ace\"", ExpectedOutput = "3" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `nums`, find the subarray with the largest sum, and return *its sum*.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int MaxSubArray(int[] nums) 
                {
                    int maxSoFar = nums[0], maxEndingHere = nums[0];
                    for (int i = 1; i < nums.Length; i++)
                    {
                        maxEndingHere = Math.Max(nums[i], maxEndingHere + nums[i]);
                        maxSoFar = Math.Max(maxSoFar, maxEndingHere);
                    }
                    return maxSoFar;
                }
            }

            var sol = new Solution();
            sol.MaxSubArray(new[] { -2, 1, -3, 4, -1, 2, 1, -5, 4 }).Dump("MaxSubArray (expected: 6)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Kadane array", Input = "[-2,1,-3,4,-1,2,1,-5,4]", ExpectedOutput = "6" }
            }
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
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given an integer array `nums`. You are initially positioned at the array's **first index**. Return `true` if you can reach the last index, or `false` otherwise.
            """,
            StarterCode = """
            public class Solution 
            {
                public bool CanJump(int[] nums) 
                {
                    int goal = nums.Length - 1;
                    for (int i = nums.Length - 2; i >= 0; i--)
                    {
                        if (i + nums[i] >= goal) goal = i;
                    }
                    return goal == 0;
                }
            }

            var sol = new Solution();
            sol.CanJump(new[] { 2, 3, 1, 1, 4 }).Dump("CanJump (expected: True)");
            sol.CanJump(new[] { 3, 2, 1, 0, 4 }).Dump("CanJump (expected: False)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Reachable", Input = "[2,3,1,1,4]", ExpectedOutput = "True" },
                new() { Name = "Blocked", Input = "[3,2,1,0,4]", ExpectedOutput = "False" }
            }
        }
    };
}
