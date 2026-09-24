using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetTwoPointersAndSlidingWindowProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_125_valid_palindrome",
            Number = 125,
            Title = "Valid Palindrome",
            Category = "Two Pointers",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 48.5,
            IsPremium = false,
            Tags = new List<string> { "Two Pointers", "String" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            A phrase is a **palindrome** if, after converting all uppercase letters into lowercase letters and removing all non-alphanumeric characters, it reads the same forward and backward.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public bool IsPalindrome(string s) 
                {
                    int l = 0, r = s.Length - 1;
                    while (l < r)
                    {
                        while (l < r && !char.IsLetterOrDigit(s[l])) l++;
                        while (l < r && !char.IsLetterOrDigit(s[r])) r--;
                        if (char.ToLower(s[l]) != char.ToLower(s[r])) return false;
                        l++;
                        r--;
                    }
                    return true;
                }
            }

            var sol = new Solution();
            sol.IsPalindrome("A man, a plan, a canal: Panama").Dump("Is Palindrome (Expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Valid phrase", Input = "\"A man, a plan, a canal: Panama\"", ExpectedOutput = "True" },
                new() { Name = "Race a car", Input = "\"race a car\"", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_15_3sum",
            Number = 15,
            Title = "3Sum",
            Category = "Two Pointers",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 35.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Two Pointers", "Sorting" },
            TimeComplexity = "O(N^2)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return all the triplets `[nums[i], nums[j], nums[k]]` such that `i != j`, `i != k`, and `j != k`, and `nums[i] + nums[j] + nums[k] == 0`. Notice that the solution set must not contain duplicate triplets.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public IList<IList<int>> ThreeSum(int[] nums) 
                {
                    Array.Sort(nums);
                    var res = new List<IList<int>>();
                    for (int i = 0; i < nums.Length - 2; i++)
                    {
                        if (i > 0 && nums[i] == nums[i - 1]) continue;
                        int l = i + 1, r = nums.Length - 1;
                        while (l < r)
                        {
                            int sum = nums[i] + nums[l] + nums[r];
                            if (sum == 0)
                            {
                                res.Add(new List<int> { nums[i], nums[l], nums[r] });
                                while (l < r && nums[l] == nums[l + 1]) l++;
                                while (l < r && nums[r] == nums[r - 1]) r--;
                                l++;
                                r--;
                            }
                            else if (sum < 0) l++;
                            else r--;
                        }
                    }
                    return res;
                }
            }

            var sol = new Solution();
            sol.ThreeSum(new[] { -1, 0, 1, 2, -1, -4 }).Dump("3Sum");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard", Input = "[-1,0,1,2,-1,-4]", ExpectedOutput = "[[-1,-1,2],[-1,0,1]]" }
            }
        },
        new()
        {
            Id = "blind75_11_container_with_most_water",
            Number = 11,
            Title = "Container With Most Water",
            Category = "Two Pointers",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 56.4,
            IsPremium = false,
            Tags = new List<string> { "Array", "Two Pointers", "Greedy" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an integer array `height` of length `n`, find two lines that together with the x-axis form a container, such that the container contains the most water. Return the maximum amount of water a container can store.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int MaxArea(int[] height) 
                {
                    int l = 0, r = height.Length - 1;
                    int maxArea = 0;
                    while (l < r)
                    {
                        int area = Math.Min(height[l], height[r]) * (r - l);
                        maxArea = Math.Max(maxArea, area);
                        if (height[l] < height[r]) l++;
                        else r--;
                    }
                    return maxArea;
                }
            }

            var sol = new Solution();
            sol.MaxArea(new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 }).Dump("Max Area (expected: 49)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Example 1", Input = "[1,8,6,2,5,4,8,3,7]", ExpectedOutput = "49" }
            }
        },
        new()
        {
            Id = "blind75_121_best_time_to_buy_and_sell_stock",
            Number = 121,
            Title = "Best Time to Buy and Sell Stock",
            Category = "Sliding Window",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 54.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming", "Sliding Window" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You want to maximize your profit by choosing a single day to buy one stock and choosing a different day in the future to sell that stock. Return the maximum profit you can achieve from this transaction.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int MaxProfit(int[] prices) 
                {
                    int minPrice = int.MaxValue;
                    int maxProfit = 0;
                    foreach (var p in prices)
                    {
                        minPrice = Math.Min(minPrice, p);
                        maxProfit = Math.Max(maxProfit, p - minPrice);
                    }
                    return maxProfit;
                }
            }

            var sol = new Solution();
            sol.MaxProfit(new[] { 7, 1, 5, 3, 6, 4 }).Dump("Max Profit (expected: 5)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard", Input = "[7,1,5,3,6,4]", ExpectedOutput = "5" }
            }
        },
        new()
        {
            Id = "blind75_03_longest_substring_without_repeating_characters",
            Number = 3,
            Title = "Longest Substring Without Repeating Characters",
            Category = "Sliding Window",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 35.8,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "String", "Sliding Window" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(min(M, N))",
            DescriptionMarkdown = """
            Given a string `s`, find the length of the **longest substring** without duplicate characters.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int LengthOfLongestSubstring(string s) 
                {
                    var map = new Dictionary<char, int>();
                    int maxLen = 0, l = 0;
                    for (int r = 0; r < s.Length; r++)
                    {
                        if (map.TryGetValue(s[r], out int prevIdx) && prevIdx >= l)
                        {
                            l = prevIdx + 1;
                        }
                        map[s[r]] = r;
                        maxLen = Math.Max(maxLen, r - l + 1);
                    }
                    return maxLen;
                }
            }

            var sol = new Solution();
            sol.LengthOfLongestSubstring("abcabcbb").Dump("LengthOfLongestSubstring (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "abcabcbb", Input = "\"abcabcbb\"", ExpectedOutput = "3" },
                new() { Name = "bbbbb", Input = "\"bbbbb\"", ExpectedOutput = "1" }
            }
        },
        new()
        {
            Id = "blind75_424_longest_repeating_character_replacement",
            Number = 424,
            Title = "Longest Repeating Character Replacement",
            Category = "Sliding Window",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 55.7,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "String", "Sliding Window" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given a string `s` and an integer `k`. You can choose any character of the string and change it to any other uppercase English character. Return the length of the longest substring containing the same letter you can get after performing the above operations at most `k` times.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int CharacterReplacement(string s, int k) 
                {
                    var counts = new int[26];
                    int maxCount = 0, maxLen = 0, l = 0;
                    for (int r = 0; r < s.Length; r++)
                    {
                        counts[s[r] - 'A']++;
                        maxCount = Math.Max(maxCount, counts[s[r] - 'A']);
                        while ((r - l + 1) - maxCount > k)
                        {
                            counts[s[l] - 'A']--;
                            l++;
                        }
                        maxLen = Math.Max(maxLen, r - l + 1);
                    }
                    return maxLen;
                }
            }

            var sol = new Solution();
            sol.CharacterReplacement("ABAB", 2).Dump("CharacterReplacement (expected: 4)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "ABAB, k=2", Input = "\"ABAB\", 2", ExpectedOutput = "4" }
            }
        },
        new()
        {
            Id = "blind75_76_minimum_window_substring",
            Number = 76,
            Title = "Minimum Window Substring",
            Category = "Sliding Window",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 43.5,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "String", "Sliding Window" },
            TimeComplexity = "O(M + N)",
            SpaceComplexity = "O(M + N)",
            DescriptionMarkdown = """
            Given two strings `s` and `t` of lengths `m` and `n` respectively, return the **minimum window substring** of `s` such that every character in `t` (including duplicates) is included in the window.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public string MinWindow(string s, string t) 
                {
                    if (s.Length < t.Length) return "";
                    var tCount = new Dictionary<char, int>();
                    foreach (var c in t) tCount[c] = tCount.GetValueOrDefault(c) + 1;
                    var window = new Dictionary<char, int>();
                    int have = 0, need = tCount.Count;
                    int l = 0, minLen = int.MaxValue, resL = -1;

                    for (int r = 0; r < s.Length; r++)
                    {
                        char c = s[r];
                        window[c] = window.GetValueOrDefault(c) + 1;
                        if (tCount.ContainsKey(c) && window[c] == tCount[c]) have++;

                        while (have == need)
                        {
                            if (r - l + 1 < minLen)
                            {
                                minLen = r - l + 1;
                                resL = l;
                            }
                            window[s[l]]--;
                            if (tCount.ContainsKey(s[l]) && window[s[l]] < tCount[s[l]]) have--;
                            l++;
                        }
                    }
                    return minLen == int.MaxValue ? "" : s.Substring(resL, minLen);
                }
            }

            var sol = new Solution();
            sol.MinWindow("ADOBECODEBANC", "ABC").Dump("MinWindow (expected: BANC)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "ADOBECODEBANC", Input = "\"ADOBECODEBANC\", \"ABC\"", ExpectedOutput = "BANC" }
            }
        },
        new()
        {
            Id = "blind75_20_valid_parentheses",
            Number = 20,
            Title = "Valid Parentheses",
            Category = "Stack",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 41.0,
            IsPremium = false,
            Tags = new List<string> { "String", "Stack" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given a string `s` containing just the characters `'('`, `')'`, `'{'`, `'}'`, `'['` and `']'`, determine if the input string is valid.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public bool IsValid(string s) 
                {
                    var stack = new Stack<char>();
                    foreach (var c in s)
                    {
                        if (c == '(') stack.Push(')');
                        else if (c == '{') stack.Push('}');
                        else if (c == '[') stack.Push(']');
                        else if (stack.Count == 0 || stack.Pop() != c) return false;
                    }
                    return stack.Count == 0;
                }
            }

            var sol = new Solution();
            sol.IsValid("()[]{}").Dump("IsValid (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "()[]{}", Input = "\"()[]{}\"", ExpectedOutput = "True" },
                new() { Name = "(]", Input = "\"(]\"", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_153_find_minimum_in_rotated_sorted_array",
            Number = 153,
            Title = "Find Minimum in Rotated Sorted Array",
            Category = "Binary Search",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 50.4,
            IsPremium = false,
            Tags = new List<string> { "Array", "Binary Search" },
            TimeComplexity = "O(log N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the sorted rotated array `nums` of unique elements, return the minimum element of this array. You must write an algorithm that runs in `O(log n)` time.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int FindMin(int[] nums) 
                {
                    int l = 0, r = nums.Length - 1;
                    while (l < r)
                    {
                        int mid = l + (r - l) / 2;
                        if (nums[mid] > nums[r]) l = mid + 1;
                        else r = mid;
                    }
                    return nums[l];
                }
            }

            var sol = new Solution();
            sol.FindMin(new[] { 3, 4, 5, 1, 2 }).Dump("FindMin (expected: 1)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[3,4,5,1,2]", Input = "[3,4,5,1,2]", ExpectedOutput = "1" }
            }
        },
        new()
        {
            Id = "blind75_33_search_in_rotated_sorted_array",
            Number = 33,
            Title = "Search in Rotated Sorted Array",
            Category = "Binary Search",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 40.7,
            IsPremium = false,
            Tags = new List<string> { "Array", "Binary Search" },
            TimeComplexity = "O(log N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given the array `nums` after the possible rotation and an integer `target`, return the index of `target` if it is in `nums`, or `-1` if it is not in `nums`.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int Search(int[] nums, int target) 
                {
                    int l = 0, r = nums.Length - 1;
                    while (l <= r)
                    {
                        int mid = l + (r - l) / 2;
                        if (nums[mid] == target) return mid;
                        if (nums[l] <= nums[mid])
                        {
                            if (target >= nums[l] && target < nums[mid]) r = mid - 1;
                            else l = mid + 1;
                        }
                        else
                        {
                            if (target > nums[mid] && target <= nums[r]) l = mid + 1;
                            else r = mid - 1;
                        }
                    }
                    return -1;
                }
            }

            var sol = new Solution();
            sol.Search(new[] { 4, 5, 6, 7, 0, 1, 2 }, 0).Dump("Search 0 (expected: 4)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Target present", Input = "[4,5,6,7,0,1,2], 0", ExpectedOutput = "4" }
            }
        }
    };
}
