using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetArraysAndHashingProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_01_two_sum",
            Number = 1,
            Title = "Two Sum",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 52.9,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an array of integers `nums` and an integer `target`, return indices of the two numbers such that they add up to `target`.

            You may assume that each input would have **exactly one solution**, and you may not use the same element twice.

            ### Example 1:
            - **Input:** `nums = [2,7,11,15], target = 9`
            - **Output:** `[0,1]`

            ### Constraints:
            - `2 <= nums.Length <= 10^4`
            - `-10^9 <= nums[i] <= 10^9`
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int[] TwoSum(int[] nums, int target) 
                {
                    var map = new Dictionary<int, int>();
                    for (int i = 0; i < nums.Length; i++) 
                    {
                        int complement = target - nums[i];
                        if (map.TryGetValue(complement, out int idx)) 
                        {
                            return new[] { idx, i };
                        }
                        map[nums[i]] = i;
                    }
                    return Array.Empty<int>();
                }
            }

            var sol = new Solution();
            sol.TwoSum(new[] { 2, 7, 11, 15 }, 9).Dump("Two Sum (target=9)");
            """,
            SolutionCode = """
            public class Solution 
            {
                public int[] TwoSum(int[] nums, int target) 
                {
                    var map = new Dictionary<int, int>();
                    for (int i = 0; i < nums.Length; i++) 
                    {
                        int diff = target - nums[i];
                        if (map.TryGetValue(diff, out int index)) return new int[] { index, i };
                        map[nums[i]] = i;
                    }
                    return Array.Empty<int>();
                }
            }
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Example 1", Input = "[2,7,11,15], 9", ExpectedOutput = "[0,1]" },
                new() { Name = "Example 2", Input = "[3,2,4], 6", ExpectedOutput = "[1,2]" }
            }
        },
        new()
        {
            Id = "blind75_217_contains_duplicate",
            Number = 217,
            Title = "Contains Duplicate",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 61.7,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Sorting" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return `true` if any value appears **at least twice** in the array, and return `false` if every element is distinct.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public bool ContainsDuplicate(int[] nums) 
                {
                    var set = new HashSet<int>();
                    foreach (var n in nums)
                    {
                        if (!set.Add(n)) return true;
                    }
                    return false;
                }
            }

            var sol = new Solution();
            sol.ContainsDuplicate(new[] { 1, 2, 3, 1 }).Dump("ContainsDuplicate([1,2,3,1])");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Duplicates Present", Input = "[1,2,3,1]", ExpectedOutput = "True" },
                new() { Name = "All Distinct", Input = "[1,2,3,4]", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_242_valid_anagram",
            Number = 242,
            Title = "Valid Anagram",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 63.7,
            IsPremium = false,
            Tags = new List<string> { "Hash Table", "String", "Sorting" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given two strings `s` and `t`, return `true` if `t` is an anagram of `s`, and `false` otherwise.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public bool IsAnagram(string s, string t) 
                {
                    if (s.Length != t.Length) return false;
                    var counts = new int[26];
                    for (int i = 0; i < s.Length; i++)
                    {
                        counts[s[i] - 'a']++;
                        counts[t[i] - 'a']--;
                    }
                    foreach (var c in counts) if (c != 0) return false;
                    return true;
                }
            }

            var sol = new Solution();
            sol.IsAnagram("anagram", "nagaram").Dump("IsAnagram");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Valid", Input = "\"anagram\", \"nagaram\"", ExpectedOutput = "True" },
                new() { Name = "Invalid", Input = "\"rat\", \"car\"", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_49_group_anagrams",
            Number = 49,
            Title = "Group Anagrams",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 70.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "String", "Sorting" },
            TimeComplexity = "O(N * K log K)",
            SpaceComplexity = "O(N * K)",
            DescriptionMarkdown = """
            Given an array of strings `strs`, group **the anagrams** together. You can return the answer in **any order**.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class Solution 
            {
                public IList<IList<string>> GroupAnagrams(string[] strs) 
                {
                    var dict = new Dictionary<string, List<string>>();
                    foreach (var s in strs)
                    {
                        var key = new string(s.OrderBy(c => c).ToArray());
                        if (!dict.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            dict[key] = list;
                        }
                        list.Add(s);
                    }
                    return dict.Values.Cast<IList<string>>().ToList();
                }
            }

            var sol = new Solution();
            sol.GroupAnagrams(new[] { "eat","tea","tan","ate","nat","bat" }).Dump("Grouped");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Group 1", Input = "[\"eat\",\"tea\",\"tan\",\"ate\",\"nat\",\"bat\"]", ExpectedOutput = "3 Groups" }
            }
        },
        new()
        {
            Id = "blind75_347_top_k_frequent_elements",
            Number = 347,
            Title = "Top K Frequent Elements",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 64.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Bucket Sort", "Heap" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an integer array `nums` and an integer `k`, return the `k` most frequent elements. You may return the answer in **any order**.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class Solution 
            {
                public int[] TopKFrequent(int[] nums, int k) 
                {
                    return nums.GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .Take(k)
                        .Select(g => g.Key)
                        .ToArray();
                }
            }

            var sol = new Solution();
            sol.TopKFrequent(new[] { 1, 1, 1, 2, 2, 3 }, 2).Dump("Top 2 Frequent");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Top 2", Input = "[1,1,1,2,2,3], k=2", ExpectedOutput = "[1, 2]" }
            }
        },
        new()
        {
            Id = "blind75_238_product_of_array_except_self",
            Number = 238,
            Title = "Product of Array Except Self",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 65.3,
            IsPremium = false,
            Tags = new List<string> { "Array", "Prefix Sum" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1) auxiliary",
            DescriptionMarkdown = """
            Given an integer array `nums`, return an array `answer` such that `answer[i]` is equal to the product of all the elements of `nums` except `nums[i]`. Must run in `O(N)` without using division.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int[] ProductExceptSelf(int[] nums) 
                {
                    int n = nums.Length;
                    int[] res = new int[n];
                    res[0] = 1;
                    for (int i = 1; i < n; i++) res[i] = res[i - 1] * nums[i - 1];
                    int right = 1;
                    for (int i = n - 1; i >= 0; i--) 
                    {
                        res[i] *= right;
                        right *= nums[i];
                    }
                    return res;
                }
            }

            var sol = new Solution();
            sol.ProductExceptSelf(new[] { 1, 2, 3, 4 }).Dump("Product Except Self");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard", Input = "[1,2,3,4]", ExpectedOutput = "[24,12,8,6]" }
            }
        },
        new()
        {
            Id = "blind75_271_encode_and_decode_strings",
            Number = 271,
            Title = "Encode and Decode Strings",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 45.8,
            IsPremium = true,
            Tags = new List<string> { "Array", "String", "Design" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Design an algorithm to encode a list of strings to a string. The encoded string is then sent over the network and is decoded back to the original list of strings.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;
            using System.Text;

            public class Codec 
            {
                public string Encode(IList<string> strs) 
                {
                    var sb = new StringBuilder();
                    foreach (var s in strs) sb.Append(s.Length).Append('#').Append(s);
                    return sb.ToString();
                }

                public IList<string> Decode(string s) 
                {
                    var res = new List<string>();
                    int i = 0;
                    while (i < s.Length)
                    {
                        int hash = s.IndexOf('#', i);
                        int len = int.Parse(s.Substring(i, hash - i));
                        res.Add(s.Substring(hash + 1, len));
                        i = hash + 1 + len;
                    }
                    return res;
                }
            }

            var codec = new Codec();
            var encoded = codec.Encode(new[] { "lint", "code", "love", "you" });
            codec.Decode(encoded).Dump("Decoded Strings");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Four words", Input = "[\"lint\",\"code\",\"love\",\"you\"]", ExpectedOutput = "[\"lint\",\"code\",\"love\",\"you\"]" }
            }
        },
        new()
        {
            Id = "blind75_128_longest_consecutive_sequence",
            Number = 128,
            Title = "Longest Consecutive Sequence",
            Category = "Arrays & Hashing",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 52.3,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Union Find" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an unsorted array of integers `nums`, return the length of the longest consecutive elements sequence. Must run in `O(n)` time.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int LongestConsecutive(int[] nums) 
                {
                    var set = new HashSet<int>(nums);
                    int longest = 0;
                    foreach (var n in set)
                    {
                        if (!set.Contains(n - 1))
                        {
                            int curr = n;
                            int len = 1;
                            while (set.Contains(curr + 1))
                            {
                                curr++;
                                len++;
                            }
                            longest = Math.Max(longest, len);
                        }
                    }
                    return longest;
                }
            }

            var sol = new Solution();
            sol.LongestConsecutive(new[] { 100, 4, 200, 1, 3, 2 }).Dump("Longest Consecutive (expected: 4)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Example 1", Input = "[100,4,200,1,3,2]", ExpectedOutput = "4" },
                new() { Name = "Example 2", Input = "[0,3,7,2,5,8,4,6,0,1]", ExpectedOutput = "9" }
            }
        }
    };
}
