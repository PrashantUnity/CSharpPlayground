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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an array of integers `nums` and an integer `target`, return the **indices** of the two numbers that add up to `target`.

            Every input has **exactly one** answer, and you may not use the same element twice. The two indices can be returned in any order.

            ### Example 1
            - **Input:** `nums = [2,7,11,15], target = 9`
            - **Output:** `[0,1]`
            - **Why:** `nums[0] + nums[1] = 2 + 7 = 9`.

            ### Example 2
            - **Input:** `nums = [3,2,4], target = 6`
            - **Output:** `[1,2]`
            - **Why:** `2 + 4 = 6`. `3 + 3` would also be 6, but it uses index 0 twice, which isn't allowed.

            ### Example 3
            - **Input:** `nums = [3,3], target = 6`
            - **Output:** `[0,1]`
            - **Why:** two *different* elements can hold the same value.

            ### Constraints
            - `2 <= nums.length <= 10^4`
            - `-10^9 <= nums[i] <= 10^9` and `-10^9 <= target <= 10^9`
            - Exactly one valid answer exists.

            **Follow-up:** can you beat `O(n²)` time?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** find two positions `i < j` with `nums[i] + nums[j] == target`, and return `[i, j]`.

            1. **Start with brute force.** Try every pair with two nested loops. It is correct but `O(n²)`: for 10⁴ numbers that is about 50 million sums.
            2. **Flip the question.** Standing on `nums[j]`, you don't need "some pair" any more. You need one specific number: `complement = target - nums[j]`. The question becomes *"have I already seen `complement`, and at which index?"*
            3. **Remember what you've seen.** A `Dictionary<int, int>` from value to index answers that in `O(1)`. Walk left to right and, for each number, **look up first, then insert**. Looking up before inserting is what stops a number from pairing with itself: in `[3,2,4]` with target 6, the map is still empty when you stand on the 3.
            4. **Walk Example 1.** `2` needs `7`; the map is `{}`, so store `{2:0}`. `7` needs `2`, which is in the map at index 0, so the answer is `[0,1]`.

            **Pattern to remember:** "find a pair with some property" usually means storing what you've already seen in a hash map, keyed by what a later element will look for.

            **Common mistakes:** returning the values instead of the indices; inserting before checking, which lets an element match itself; sorting first, which loses the original indices unless you carry them along.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: check every pair",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Two nested loops try every pair `i < j` and return the first one whose sum is `target`.",
                    BottleneckExplanation = "Nothing learned about earlier numbers is kept, so every new number is compared with all of them again.",
                    Code = """
                    int[] TwoSumBruteForce(int[] nums, int target)
                    {
                        for (int i = 0; i < nums.Length; i++)
                            for (int j = i + 1; j < nums.Length; j++)
                                if (nums[i] + nums[j] == target) return new[] { i, j };
                        return Array.Empty<int>();
                    }

                    Console.WriteLine(Judge.Format(TwoSumBruteForce(new[] { 2, 7, 11, 15 }, 9)));   // [0,1]
                    """
                },
                new()
                {
                    Name = "Sort, then walk two pointers inward",
                    Kind = ApproachKind.Greedy,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Sort (value, index) pairs by value. Put `left` on the smallest and `right` on the largest. If their sum is too small, only moving `left` right can raise it; if it's too big, only moving `right` left can lower it.",
                    GreedyChoiceProperty = "In sorted order, a sum that is too small can never be fixed by the current `left`, so it is safe to discard it (and symmetrically for `right`).",
                    Code = """
                    int[] TwoSumTwoPointers(int[] nums, int target)
                    {
                        var byValue = nums.Select((value, index) => (value, index)).OrderBy(p => p.value).ToArray();
                        int left = 0, right = byValue.Length - 1;
                        while (left < right)
                        {
                            int sum = byValue[left].value + byValue[right].value;
                            if (sum == target)
                                return new[] { Math.Min(byValue[left].index, byValue[right].index), Math.Max(byValue[left].index, byValue[right].index) };
                            if (sum < target) left++; else right--;
                        }
                        return Array.Empty<int>();
                    }

                    Console.WriteLine(Judge.Format(TwoSumTwoPointers(new[] { 3, 2, 4 }, 6)));   // [1,2]
                    """
                },
                new()
                {
                    Name = "One pass with a hash map",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "For each number, look its complement up in a dictionary of the numbers already seen (value → index); if it's there, you're done, otherwise add the current number. Each lookup and insert is O(1)."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[] TwoSum(int[] nums, int target)
                {
                    var seen = new Dictionary<int, int>();   // value -> index where it was seen
                    for (int i = 0; i < nums.Length; i++)
                    {
                        int complement = target - nums[i];
                        if (seen.TryGetValue(complement, out int j)) return new[] { j, i };
                        seen[nums[i]] = i;
                    }
                    return Array.Empty<int>();
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [2,7,11,15], target = 9", Expected = "[0,1]", Call = "sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)", AnyOrder = true },
                new() { Name = "Example 2", Input = "nums = [3,2,4], target = 6", Expected = "[1,2]", Call = "sol.TwoSum(new[] { 3, 2, 4 }, 6)", AnyOrder = true },
                new() { Name = "Example 3", Input = "nums = [3,3], target = 6", Expected = "[0,1]", Call = "sol.TwoSum(new[] { 3, 3 }, 6)", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Negative numbers", Input = "nums = [-3,4,3,90], target = 0", Expected = "[0,2]", Call = "sol.TwoSum(new[] { -3, 4, 3, 90 }, 0)", AnyOrder = true },
                new() { Name = "Pair at the very end", Input = "nums = [1,2,3,4,5], target = 9", Expected = "[3,4]", Call = "sol.TwoSum(new[] { 1, 2, 3, 4, 5 }, 9)", AnyOrder = true },
                new() { Name = "Zeros", Input = "nums = [0,4,3,0], target = 0", Expected = "[0,3]", Call = "sol.TwoSum(new[] { 0, 4, 3, 0 }, 0)", AnyOrder = true },
                new() { Name = "Extreme values", Input = "nums = [-1000000000,7,1000000000], target = 0", Expected = "[0,2]", Call = "sol.TwoSum(new[] { -1000000000, 7, 1000000000 }, 0)", AnyOrder = true }
            },
            StressTestCode = """
            // Random arrays can hold several valid pairs, so check that the answer is *a* valid pair.
            judge.Agree("Random arrays: always a valid pair",
                random =>
                {
                    var nums = Enumerable.Range(0, random.Next(2, 12)).Select(_ => random.Next(-20, 21)).ToArray();
                    int a = random.Next(nums.Length), b = (a + 1 + random.Next(nums.Length - 1)) % nums.Length;
                    return (nums, target: nums[a] + nums[b]);
                },
                input => true,
                input =>
                {
                    var answer = sol.TwoSum(input.nums, input.target);
                    return answer.Length == 2 && answer[0] != answer[1] && input.nums[answer[0]] + input.nums[answer[1]] == input.target;
                });
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            The one-pass hash map on `nums = [3,8,11,2,7]`, `target = 9`, a longer input than Example 1 so the map has time to fill.
            The `i` pointer walks the array and `complement` is the number it is looking for. The `seen` strip is the dictionary
            growing (each new entry outlined in lime). When the complement is already in `seen`, both indices light up.
            """,
            VisualizationCode = """
            var nums = new[] { 3, 8, 11, 2, 7 };
            int target = 9;

            var seen = new Dictionary<int, int>();   // value -> index
            int complement = 0;
            var tracker = VisualizerRecorder.CreateArray(nums, title: "1. Two Sum: one pass with a hash map");
            tracker.Watch(seen);
            tracker.Watch(() => complement);

            for (int i = 0; i < nums.Length; i++)
            {
                complement = target - nums[i];
                tracker.Step($"nums[{i}] = {nums[i]} needs {complement}. Is {complement} in seen?", pointers: new { i });
                if (seen.TryGetValue(complement, out int j))
                {
                    tracker.Step($"Yes: {complement} was seen at index {j}, and {nums[j]} + {nums[i]} = {target}. Answer [{j},{i}]", pointers: new { j, i }, highlight: new[] { j, i });
                    break;
                }
                seen[nums[i]] = i;
                tracker.Step($"No. Remember {nums[i]} at index {i} for the numbers still to come", pointers: new { i });
            }

            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return `true` if some value appears **at least twice**, and `false` if every element is distinct.

            ### Example 1
            - **Input:** `nums = [1,2,3,1]`
            - **Output:** `true`
            - **Why:** `1` appears at index 0 and again at index 3.

            ### Example 2
            - **Input:** `nums = [1,2,3,4]`
            - **Output:** `false`
            - **Why:** all four values are different.

            ### Example 3
            - **Input:** `nums = [1,1,1,3,3,4,3,2,4,2]`
            - **Output:** `true`
            - **Why:** several values repeat; finding any one repeat is enough.

            ### Constraints
            - `1 <= nums.length <= 10^5`
            - `-10^9 <= nums[i] <= 10^9`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** does any value show up twice? You only need a yes or no, not where or how often.

            1. **Brute force:** compare every pair `i < j`. That is `O(n²)`: about 5·10⁹ comparisons for 10⁵ numbers.
            2. **Sort first:** after sorting, equal values sit next to each other, so one pass comparing neighbours finds a repeat. `O(n log n)`, but it reorders (or copies) the array.
            3. **Remember what you've seen:** walk once, keeping a `HashSet<int>` of the values seen so far. If the current value is already in the set, you've found a duplicate and can stop; otherwise add it. Each lookup and insert is `O(1)` on average, so the pass is `O(n)`.
            4. **Walk Example 1:** `1` → set `{1}`; `2` → `{1,2}`; `3` → `{1,2,3}`; `1` is already in the set → `true`.

            **Pattern to remember:** "have I seen this before?" is a hash set. `HashSet.Add` returns `false` when the value was already there, so the check and the insert are one call.

            **Common mistakes:** comparing neighbours without sorting first; using a `List` with `Contains` (that lookup is `O(n)`, which brings back `O(n²)`); scanning to the end after the answer is already known.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: compare every pair",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Two nested loops compare each value with every value after it.",
                    BottleneckExplanation = "Nothing is remembered, so each new value is compared against all the earlier ones again.",
                    Code = """
                    bool ContainsDuplicateBruteForce(int[] nums)
                    {
                        for (int i = 0; i < nums.Length; i++)
                            for (int j = i + 1; j < nums.Length; j++)
                                if (nums[i] == nums[j]) return true;
                        return false;
                    }

                    Console.WriteLine(Judge.Format(ContainsDuplicateBruteForce(new[] { 1, 2, 3, 1 })));   // true
                    """
                },
                new()
                {
                    Name = "Sort, then compare neighbours",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n) for the sorted copy",
                    Intuition = "Sorting puts equal values side by side, so a duplicate exists exactly when two neighbours are equal. Good when memory is tight and sorting in place is allowed.",
                    Code = """
                    bool ContainsDuplicateBySorting(int[] nums)
                    {
                        var sorted = nums.OrderBy(n => n).ToArray();
                        for (int i = 1; i < sorted.Length; i++)
                            if (sorted[i] == sorted[i - 1]) return true;
                        return false;
                    }

                    Console.WriteLine(Judge.Format(ContainsDuplicateBySorting(new[] { 1, 2, 3, 4 })));   // false
                    """
                },
                new()
                {
                    Name = "One pass with a hash set",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Keep every value seen so far in a `HashSet<int>`; the first value that is already there proves a duplicate."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool ContainsDuplicate(int[] nums)
                {
                    var seen = new HashSet<int>();
                    foreach (int n in nums)
                    {
                        if (!seen.Add(n)) return true;   // Add returns false when n was already in the set
                    }
                    return false;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [1,2,3,1]", Expected = "true", Call = "sol.ContainsDuplicate(new[] { 1, 2, 3, 1 })" },
                new() { Name = "Example 2", Input = "nums = [1,2,3,4]", Expected = "false", Call = "sol.ContainsDuplicate(new[] { 1, 2, 3, 4 })" },
                new() { Name = "Example 3", Input = "nums = [1,1,1,3,3,4,3,2,4,2]", Expected = "true", Call = "sol.ContainsDuplicate(new[] { 1, 1, 1, 3, 3, 4, 3, 2, 4, 2 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single element", Input = "nums = [7]", Expected = "false", Call = "sol.ContainsDuplicate(new[] { 7 })" },
                new() { Name = "Repeat at the far ends", Input = "nums = [5,1,2,3,4,5]", Expected = "true", Call = "sol.ContainsDuplicate(new[] { 5, 1, 2, 3, 4, 5 })" },
                new() { Name = "Negatives and zero", Input = "nums = [-1,0,1,-1]", Expected = "true", Call = "sol.ContainsDuplicate(new[] { -1, 0, 1, -1 })" },
                new() { Name = "Extreme values", Input = "nums = [-1000000000,1000000000]", Expected = "false", Call = "sol.ContainsDuplicate(new[] { -1000000000, 1000000000 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs brute force",
                random => Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(-6, 7)).ToArray(),
                nums => ContainsDuplicateBruteForce(nums),
                nums => sol.ContainsDuplicate(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            One pass over `nums = [3,1,4,5,9,2,6,5]`. The `i` pointer walks the array and the `seen` strip under it grows
            with every new value (the index where it first appeared is kept only so both copies can be shown). The moment
            a value is already in `seen`, both of its positions light up and the answer is `true`.
            """,
            VisualizationCode = """
            var nums = new[] { 3, 1, 4, 5, 9, 2, 6, 5 };
            var seen = new Dictionary<int, int>();   // value -> index where it first appeared
            var tracker = VisualizerRecorder.CreateArray(nums, title: "217. Contains Duplicate: remember every value you pass");
            tracker.Watch(seen);

            bool duplicate = false;
            for (int i = 0; i < nums.Length && !duplicate; i++)
            {
                if (seen.TryGetValue(nums[i], out int first))
                {
                    duplicate = true;
                    tracker.Step($"{nums[i]} is already in seen (from index {first}): a duplicate, so the answer is true", pointers: new { first, i }, highlight: new[] { first, i });
                }
                else
                {
                    seen[nums[i]] = i;
                    tracker.Step($"{nums[i]} is new: add it to seen and move on", pointers: new { i });
                }
            }

            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1) (26 counters)",
            DescriptionMarkdown = """
            Given two strings `s` and `t`, return `true` if `t` is an **anagram** of `s`, and `false` otherwise.

            An anagram uses exactly the same letters, each exactly as many times, possibly in a different order: `"listen"` and `"silent"` are anagrams.

            ### Example 1
            - **Input:** `s = "anagram", t = "nagaram"`
            - **Output:** `true`
            - **Why:** both contain `a` three times and `n`, `g`, `r`, `m` once each.

            ### Example 2
            - **Input:** `s = "rat", t = "car"`
            - **Output:** `false`
            - **Why:** `s` has a `t` that `t` doesn't have, and `t` has a `c` that `s` doesn't.

            ### Example 3
            - **Input:** `s = "ab", t = "a"`
            - **Output:** `false`
            - **Why:** strings of different lengths can never be anagrams.

            ### Constraints
            - `1 <= s.length, t.length <= 5 * 10^4`
            - `s` and `t` consist of lowercase English letters.

            **Follow-up:** what if the inputs could contain any Unicode characters?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** do the two strings contain the same letters with the same counts? Order doesn't matter at all.

            1. **The obvious way:** sort both strings and compare them. Correct, but `O(n log n)`, and it does more work than needed: it puts the letters in order when you only need to count them.
            2. **Count instead.** Keep one counter per letter, `int[26]`, indexed by `c - 'a'`. Walk the strings side by side: each letter of `s` adds 1 to its counter, each letter of `t` subtracts 1. They are anagrams exactly when every counter ends at 0.
            3. **Check the lengths first.** Different lengths can't be anagrams, and equal lengths are what let one loop walk both strings.
            4. **Walk Example 2:** `"rat"` vs `"car"`: after the loop `r` = 1 − 1 = 0, `a` = 0, but `t` = +1 and `c` = −1, so the answer is `false`.

            **Pattern to remember:** "same letters?" or "same multiset?" means counting frequencies. With a small fixed alphabet use an array; for arbitrary characters (the follow-up) use a `Dictionary<char, int>`.

            **Common mistakes:** skipping the length check; comparing `char[]` arrays with `==` (that compares references, not contents); `c - 'a'` on uppercase or non-letter input, which indexes out of range.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Sort both strings and compare",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Two anagrams become the same string once their letters are sorted.",
                    BottleneckExplanation = "Sorting orders the letters, which is more than the question asks; counting them is linear.",
                    Code = """
                    bool IsAnagramBySorting(string s, string t) =>
                        s.Length == t.Length && s.OrderBy(c => c).SequenceEqual(t.OrderBy(c => c));

                    Console.WriteLine(Judge.Format(IsAnagramBySorting("anagram", "nagaram")));   // true
                    """
                },
                new()
                {
                    Name = "Count letters: +1 for s, −1 for t",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1) (26 counters)",
                    Intuition = "After a length check, one loop adds each letter of `s` to a counter and removes each letter of `t`; the strings are anagrams exactly when every counter is back to 0."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool IsAnagram(string s, string t)
                {
                    if (s.Length != t.Length) return false;

                    var counts = new int[26];
                    for (int i = 0; i < s.Length; i++)
                    {
                        counts[s[i] - 'a']++;   // s brings a letter
                        counts[t[i] - 'a']--;   // t takes one away
                    }

                    foreach (int count in counts)
                    {
                        if (count != 0) return false;
                    }
                    return true;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = """s = "anagram", t = "nagaram" """, Expected = "true", Call = """sol.IsAnagram("anagram", "nagaram")""" },
                new() { Name = "Example 2", Input = """s = "rat", t = "car" """, Expected = "false", Call = """sol.IsAnagram("rat", "car")""" },
                new() { Name = "Example 3", Input = """s = "ab", t = "a" """, Expected = "false", Call = """sol.IsAnagram("ab", "a")""" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "The same single letter", Input = """s = "a", t = "a" """, Expected = "true", Call = """sol.IsAnagram("a", "a")""" },
                new() { Name = "Same letters, different counts", Input = """s = "aacc", t = "ccac" """, Expected = "false", Call = """sol.IsAnagram("aacc", "ccac")""" },
                new() { Name = "listen and silent", Input = """s = "listen", t = "silent" """, Expected = "true", Call = """sol.IsAnagram("listen", "silent")""" },
                new() { Name = "One letter off", Input = """s = "abcd", t = "abce" """, Expected = "false", Call = """sol.IsAnagram("abcd", "abce")""" }
            },
            StressTestCode = """
            judge.Agree("Random strings vs sorting",
                random =>
                {
                    string Word(int length) => new string(Enumerable.Range(0, length).Select(_ => (char)('a' + random.Next(3))).ToArray());
                    int length = random.Next(1, 7);
                    return (s: Word(length), t: Word(random.Next(2) == 0 ? length : random.Next(1, 7)));
                },
                input => IsAnagramBySorting(input.s, input.t),
                input => sol.IsAnagram(input.s, input.t));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `s = "anagram"` on the top row and `t = "nagaram"` underneath, walked one column at a time. The `counts` strip
            shows each letter's counter: the top letter adds 1, the bottom letter takes 1 away (changed counters glow
            lime). When every counter is back to 0 the strings are anagrams.
            """,
            VisualizationCode = """
            string s = "anagram", t = "nagaram";
            var grid = MatrixTracker.Create(new[] { s, t }, title: "242. Valid Anagram: +1 for each letter of s, -1 for each letter of t",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default }, rowHeaders: new[] { "s", "t" });

            var counts = new SortedDictionary<char, int>();
            grid.Watch(counts);

            for (int i = 0; i < s.Length; i++)
            {
                counts[s[i]] = counts.GetValueOrDefault(s[i]) + 1;
                counts[t[i]] = counts.GetValueOrDefault(t[i]) - 1;
                grid.SetCell(0, i, state: GridCellState.Current);
                grid.SetCell(1, i, state: GridCellState.Current);
                grid.Snapshot(s[i] == t[i]
                    ? $"Column {i}: '{s[i]}' comes in and goes out again, so its count doesn't change"
                    : $"Column {i}: s brings a '{s[i]}' (+1), t takes a '{t[i]}' (-1)");
                grid.SetCell(0, i, state: GridCellState.Visited);
                grid.SetCell(1, i, state: GridCellState.Visited);
            }

            var leftover = counts.Where(c => c.Value != 0).Select(c => $"{c.Key}:{c.Value}").ToList();
            grid.Snapshot(leftover.Count == 0
                ? "Every count is back to 0: the same letters, the same number of times. Anagrams: true"
                : $"Counts left over ({string.Join(", ", leftover)}): not anagrams, false");
            Display.Visualizer(grid);
            """
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
            TimeComplexity = "O(n·k)",
            SpaceComplexity = "O(n·k)",
            DescriptionMarkdown = """
            Given an array of strings `strs`, group the **anagrams** together: words made of exactly the same letters, the same number of times each. Return the groups in any order; the words inside a group can be in any order too.

            ### Example 1
            - **Input:** `strs = ["eat","tea","tan","ate","nat","bat"]`
            - **Output:** `[["bat"],["nat","tan"],["ate","eat","tea"]]`
            - **Why:** `eat`, `tea` and `ate` all use one `a`, one `e` and one `t`; `tan` and `nat` use `a`, `n`, `t`; `bat` has no partner, so it is a group of one.

            ### Example 2
            - **Input:** `strs = [""]`
            - **Output:** `[[""]]`
            - **Why:** the empty string is a (one-word) group too.

            ### Example 3
            - **Input:** `strs = ["a"]`
            - **Output:** `[["a"]]`

            ### Constraints
            - `1 <= strs.length <= 10^4`
            - `0 <= strs[i].length <= 100`
            - `strs[i]` consists of lowercase English letters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** sort the words into buckets so that two words share a bucket exactly when they are anagrams.

            1. **Brute force:** for each word, compare it with one word from every existing group using an anagram check. With `n` words of length `k` that is `O(n²·k)`.
            2. **Give every word a key.** Find something that is *identical for anagrams and different otherwise*, then a `Dictionary<key, List<string>>` groups everything in one pass. Two keys work:
               - the letters sorted: `"eat"` → `"aet"` (`O(k log k)` per word);
               - the letter counts written out: `"eat"` → `"a1e1t1"` (`O(k)` per word, since the alphabet is fixed).
            3. **One pass:** compute the word's key, append the word to that key's list (creating the list the first time), and at the end return the dictionary's values.
            4. **Walk Example 1 with count keys:** `eat` → `a1e1t1` (new group), `tea` → `a1e1t1` (joins it), `tan` → `a1n1t1` (new), `ate` → joins the first, `nat` → joins the second, `bat` → `a1b1t1` (new). Three groups.

            **Pattern to remember:** to group things that are "equivalent", map each to a **canonical key** and group by it with a hash map.

            **Common mistakes:** using the word itself as the key; `chars.ToString()` instead of `new string(chars)` (the first gives `"System.Char[]"`); a count key without separators or letters, where `[1,11]` and `[11,1]` could collide.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Compare each word with every group",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²·k log k)",
                    SpaceComplexity = "O(n·k)",
                    Intuition = "Keep a list of groups; put each word in the first group whose first word is its anagram, or start a new group.",
                    BottleneckExplanation = "Every word is checked against every group, and each check sorts letters all over again.",
                    Code = """
                    List<List<string>> GroupByComparing(string[] strs)
                    {
                        var groups = new List<List<string>>();
                        foreach (var word in strs)
                        {
                            var home = groups.FirstOrDefault(g => g[0].Length == word.Length && g[0].OrderBy(c => c).SequenceEqual(word.OrderBy(c => c)));
                            if (home != null) home.Add(word);
                            else groups.Add(new List<string> { word });
                        }
                        return groups;
                    }

                    Console.WriteLine(Judge.Format(GroupByComparing(new[] { "eat", "tea", "tan", "ate", "nat", "bat" })));
                    """
                },
                new()
                {
                    Name = "Group by the sorted letters",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n·k log k)",
                    SpaceComplexity = "O(n·k)",
                    Intuition = "Sorting a word's letters gives the same string for all its anagrams (`\"tea\"` → `\"aet\"`), so it works as a dictionary key. The simplest correct answer.",
                    Code = """
                    List<List<string>> GroupBySortedKey(string[] strs) =>
                        strs.GroupBy(word => new string(word.OrderBy(c => c).ToArray()))
                            .Select(group => group.ToList())
                            .ToList();

                    Console.WriteLine(Judge.Format(GroupBySortedKey(new[] { "eat", "tea", "tan", "ate", "nat", "bat" })));
                    """
                },
                new()
                {
                    Name = "Group by the letter counts",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n·k)",
                    SpaceComplexity = "O(n·k)",
                    Intuition = "Count each word's letters and write the counts out (`\"eat\"` → `\"a1e1t1\"`). Building that key is linear in the word, so there is no sorting at all."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public IList<IList<string>> GroupAnagrams(string[] strs)
                {
                    var groups = new Dictionary<string, List<string>>();
                    foreach (var word in strs)
                    {
                        string key = Signature(word);
                        if (!groups.TryGetValue(key, out var group))
                        {
                            group = new List<string>();
                            groups[key] = group;
                        }
                        group.Add(word);
                    }
                    return groups.Values.ToList<IList<string>>();
                }

                // Letter counts as text, e.g. "eat" -> "a1e1t1": anagrams, and only anagrams, share a signature.
                private static string Signature(string word)
                {
                    var counts = new int[26];
                    foreach (char c in word) counts[c - 'a']++;

                    var key = new StringBuilder();
                    for (int i = 0; i < 26; i++)
                    {
                        if (counts[i] > 0) key.Append((char)('a' + i)).Append(counts[i]);
                    }
                    return key.ToString();
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = """strs = ["eat","tea","tan","ate","nat","bat"]""", Expected = """[["bat"],["nat","tan"],["ate","eat","tea"]]""", Call = """sol.GroupAnagrams(new[] { "eat", "tea", "tan", "ate", "nat", "bat" })""", AnyOrder = true },
                new() { Name = "Example 2", Input = """strs = [""]""", Expected = """[[""]]""", Call = """sol.GroupAnagrams(new[] { "" })""", AnyOrder = true },
                new() { Name = "Example 3", Input = """strs = ["a"]""", Expected = """[["a"]]""", Call = """sol.GroupAnagrams(new[] { "a" })""", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "No anagrams at all", Input = """strs = ["abc","def"]""", Expected = """[["abc"],["def"]]""", Call = """sol.GroupAnagrams(new[] { "abc", "def" })""", AnyOrder = true },
                new() { Name = "Everything in one group", Input = """strs = ["abc","bca","cab"]""", Expected = """[["abc","bca","cab"]]""", Call = """sol.GroupAnagrams(new[] { "abc", "bca", "cab" })""", AnyOrder = true },
                new() { Name = "Counts matter, not just letters", Input = """strs = ["aab","aba","abb"]""", Expected = """[["aab","aba"],["abb"]]""", Call = """sol.GroupAnagrams(new[] { "aab", "aba", "abb" })""", AnyOrder = true },
                new() { Name = "Repeated words", Input = """strs = ["x","x"]""", Expected = """[["x","x"]]""", Call = """sol.GroupAnagrams(new[] { "x", "x" })""", AnyOrder = true }
            },
            StressTestCode = """
            judge.Agree("Random words vs comparing every group",
                random => Enumerable.Range(0, random.Next(1, 9))
                    .Select(_ => new string(Enumerable.Range(0, random.Next(0, 4)).Select(_ => (char)('a' + random.Next(3))).ToArray()))
                    .ToArray(),
                strs => (object)GroupByComparing(strs),
                strs => sol.GroupAnagrams(strs),
                anyOrder: true);
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            The words of Example 1 in a row. For each word the `key` chip shows its letter counts (`"eat"` → `a1e1t1`) and
            the `groups` strip is the dictionary: a word either starts a new group or joins the one with the same key.
            Anagrams always land together because they always produce the same key.
            """,
            VisualizationCode = """
            var strs = new[] { "eat", "tea", "tan", "ate", "nat", "bat" };
            var groups = new Dictionary<string, List<string>>();
            string key = "";
            var tracker = VisualizerRecorder.CreateArray(strs, title: "49. Group Anagrams: the same letter counts, the same group");
            tracker.Watch(() => key);
            tracker.Watch(groups);

            for (int i = 0; i < strs.Length; i++)
            {
                var counts = new int[26];
                foreach (char c in strs[i]) counts[c - 'a']++;
                key = string.Concat(Enumerable.Range(0, 26).Where(k => counts[k] > 0).Select(k => $"{(char)('a' + k)}{counts[k]}"));

                if (groups.TryGetValue(key, out var group))
                {
                    string partners = string.Join(" and ", group.Select(w => $"\"{w}\""));
                    group.Add(strs[i]);
                    tracker.Step($"\"{strs[i]}\" has letter counts {key}, like {partners}: it joins that group", pointers: new { i });
                }
                else
                {
                    groups[key] = new List<string> { strs[i] };
                    tracker.Step($"\"{strs[i]}\" has letter counts {key}, a key not seen before: it starts a new group", pointers: new { i });
                }
            }

            tracker.Step($"Done: {groups.Count} groups, one per distinct key", highlight: Enumerable.Range(0, strs.Length));
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an integer array `nums` and an integer `k`, return the `k` values that appear **most often**. The answer can be in any order, and it is guaranteed to be unique.

            ### Example 1
            - **Input:** `nums = [1,1,1,2,2,3], k = 2`
            - **Output:** `[1,2]`
            - **Why:** `1` appears 3 times, `2` twice and `3` once, so the two most frequent are `1` and `2`.

            ### Example 2
            - **Input:** `nums = [1], k = 1`
            - **Output:** `[1]`

            ### Example 3
            - **Input:** `nums = [4,1,-1,2,-1,2,3], k = 2`
            - **Output:** `[-1,2]`
            - **Why:** `-1` and `2` appear twice each; every other value appears once.

            ### Constraints
            - `1 <= nums.length <= 10^5`
            - `-10^4 <= nums[i] <= 10^4`
            - `k` is between 1 and the number of distinct values, and the answer is unique.

            **Follow-up:** can you beat `O(n log n)`?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count how often each value appears, then report the `k` values with the highest counts.

            1. **Count first.** A `Dictionary<int, int>` from value to count takes one `O(n)` pass. That part is unavoidable.
            2. **Picking the top k.** Sorting the distinct values by count costs `O(m log m)` for `m` distinct values. A min-heap holding only the best `k` so far costs `O(m log k)`.
            3. **The insight: counts are small.** A value can appear at most `n` times, so make `n + 1` **buckets** where `buckets[f]` lists the values that appear exactly `f` times. Then walk the buckets from the highest `f` down, collecting values until you have `k`. Everything is linear: `O(n)`.
            4. **Walk Example 1:** counts `{1:3, 2:2, 3:1}` go into `buckets[3] = [1]`, `buckets[2] = [2]`, `buckets[1] = [3]`. Walking down from `f = 6`: 6, 5, 4 are empty, `f = 3` gives `1`, `f = 2` gives `2`, and that's `k = 2` values.

            **Pattern to remember:** "top k by frequency" means count, then either a size-k heap or, because frequencies are bounded by `n`, a bucket sort.

            **Common mistakes:** sorting the values instead of their counts; forgetting several values can share a bucket; sizing the bucket array `n` instead of `n + 1` (a value can appear `n` times).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Count, then sort by count",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Group equal values, sort the groups by size (largest first) and take the first `k` keys.",
                    BottleneckExplanation = "Sorting puts every distinct value in order, when only the top `k` are needed.",
                    Code = """
                    int[] TopKBySorting(int[] nums, int k) =>
                        nums.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(k).Select(g => g.Key).ToArray();

                    Console.WriteLine(Judge.Format(TopKBySorting(new[] { 1, 1, 1, 2, 2, 3 }, 2)));   // [1,2]
                    """
                },
                new()
                {
                    Name = "Keep a min-heap of the best k",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n log k)",
                    SpaceComplexity = "O(n)",
                    Intuition = "After counting, push each value into a min-heap keyed by its count and pop whenever the heap grows past `k`. The weakest of the current top k is always on top, so it is the one thrown out.",
                    Code = """
                    int[] TopKWithHeap(int[] nums, int k)
                    {
                        var count = new Dictionary<int, int>();
                        foreach (int n in nums) count[n] = count.GetValueOrDefault(n) + 1;

                        var heap = new PriorityQueue<int, int>();   // smallest count on top
                        foreach (var (value, frequency) in count)
                        {
                            heap.Enqueue(value, frequency);
                            if (heap.Count > k) heap.Dequeue();
                        }

                        var result = new int[heap.Count];
                        for (int i = result.Length - 1; i >= 0; i--) result[i] = heap.Dequeue();
                        return result;
                    }

                    Console.WriteLine(Judge.Format(TopKWithHeap(new[] { 4, 1, -1, 2, -1, 2, 3 }, 2)));   // [2,-1] or [-1,2]
                    """
                },
                new()
                {
                    Name = "Bucket sort by frequency",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Counts range from 1 to n, so drop each value into `buckets[count]` and read the buckets from the highest count down until `k` values are collected."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[] TopKFrequent(int[] nums, int k)
                {
                    var count = new Dictionary<int, int>();
                    foreach (int n in nums) count[n] = count.GetValueOrDefault(n) + 1;

                    // buckets[f] holds the values that appear exactly f times; f can be as large as nums.Length.
                    var buckets = new List<int>[nums.Length + 1];
                    foreach (var (value, frequency) in count)
                    {
                        (buckets[frequency] ??= new List<int>()).Add(value);
                    }

                    var result = new List<int>(k);
                    for (int f = buckets.Length - 1; f > 0 && result.Count < k; f--)
                    {
                        if (buckets[f] != null) result.AddRange(buckets[f]);
                    }
                    return result.Take(k).ToArray();
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [1,1,1,2,2,3], k = 2", Expected = "[1,2]", Call = "sol.TopKFrequent(new[] { 1, 1, 1, 2, 2, 3 }, 2)", AnyOrder = true },
                new() { Name = "Example 2", Input = "nums = [1], k = 1", Expected = "[1]", Call = "sol.TopKFrequent(new[] { 1 }, 1)", AnyOrder = true },
                new() { Name = "Example 3", Input = "nums = [4,1,-1,2,-1,2,3], k = 2", Expected = "[-1,2]", Call = "sol.TopKFrequent(new[] { 4, 1, -1, 2, -1, 2, 3 }, 2)", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One value repeated", Input = "nums = [5,5,5], k = 1", Expected = "[5]", Call = "sol.TopKFrequent(new[] { 5, 5, 5 }, 1)", AnyOrder = true },
                new() { Name = "k covers every value", Input = "nums = [3,1,2], k = 3", Expected = "[1,2,3]", Call = "sol.TopKFrequent(new[] { 3, 1, 2 }, 3)", AnyOrder = true },
                new() { Name = "Clear winners", Input = "nums = [1,2,2,3,3,3], k = 2", Expected = "[3,2]", Call = "sol.TopKFrequent(new[] { 1, 2, 2, 3, 3, 3 }, 2)", AnyOrder = true },
                new() { Name = "Negative values", Input = "nums = [-7,-7,0,1], k = 1", Expected = "[-7]", Call = "sol.TopKFrequent(new[] { -7, -7, 0, 1 }, 1)", AnyOrder = true }
            },
            StressTestCode = """
            // Every value gets its own count, so the answer is always unique.
            judge.Agree("Random arrays vs count-and-sort",
                random =>
                {
                    int distinct = random.Next(1, 6);
                    var nums = Enumerable.Range(0, distinct).SelectMany(v => Enumerable.Repeat(v * 3 - 5, v + 1)).OrderBy(_ => random.Next()).ToArray();
                    return (nums, k: random.Next(1, distinct + 1));
                },
                input => TopKBySorting(input.nums, input.k),
                input => sol.TopKFrequent(input.nums, input.k),
                anyOrder: true);
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Two parts on `nums = [4,1,4,2,4,1,3]`, `k = 2`. First the counting pass: `i` walks the array and the `count` map
            fills in. Then the buckets: cell `[f]` holds the values that appear exactly `f` times, and the `f` pointer walks
            down from the top bucket, dropping values into `result` until it holds `k` of them.
            """,
            VisualizationCode = """
            var nums = new[] { 4, 1, 4, 2, 4, 1, 3 };
            int k = 2;

            // Part 1: count every value.
            var count = new Dictionary<int, int>();
            var counting = VisualizerRecorder.CreateArray(nums, title: "347. Top K Frequent: 1) count each value");
            counting.Watch(count);
            for (int i = 0; i < nums.Length; i++)
            {
                count[nums[i]] = count.GetValueOrDefault(nums[i]) + 1;
                counting.Step($"{nums[i]} has now been seen {count[nums[i]]} time{(count[nums[i]] == 1 ? "" : "s")}", pointers: new { i });
            }
            Display.Visualizer(counting);

            // Part 2: bucket by frequency, then read from the most frequent down.
            var buckets = Enumerable.Range(0, nums.Length + 1).Select(_ => new List<int>()).ToArray();
            var result = new List<int>();
            var bucketing = VisualizerRecorder.CreateArray(buckets, title: "347. Top K Frequent: 2) bucket by frequency, read from the top");
            bucketing.Watch(result);
            foreach (var (value, frequency) in count)
            {
                buckets[frequency].Add(value);
                bucketing.Step($"{value} appears {frequency} time{(frequency == 1 ? "" : "s")}, so it goes into bucket [{frequency}]", highlight: new[] { frequency });
            }

            for (int f = buckets.Length - 1; f > 0 && result.Count < k; f--)
            {
                if (buckets[f].Count == 0)
                {
                    bucketing.Step($"Nothing appears {f} times: bucket [{f}] is empty", pointers: new { f });
                    continue;
                }
                result.AddRange(buckets[f].Take(k - result.Count));
                bucketing.Step($"Bucket [{f}] holds {string.Join(", ", buckets[f])}: take {(result.Count == k ? "what we need" : "all of it")}; result now has {result.Count} of {k}", pointers: new { f }, highlight: new[] { f });
            }

            bucketing.Step($"Done: the {k} most frequent values are {string.Join(", ", result)}");
            Display.Visualizer(bucketing);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1) extra (besides the answer)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return an array `answer` where `answer[i]` is the product of **every element except** `nums[i]`.

            You must run in `O(n)` time and **without using division**. Every product fits in a 32-bit integer.

            ### Example 1
            - **Input:** `nums = [1,2,3,4]`
            - **Output:** `[24,12,8,6]`
            - **Why:** `answer[0] = 2·3·4 = 24`, `answer[1] = 1·3·4 = 12`, `answer[2] = 1·2·4 = 8`, `answer[3] = 1·2·3 = 6`.

            ### Example 2
            - **Input:** `nums = [-1,1,0,-3,3]`
            - **Output:** `[0,0,9,0,0]`
            - **Why:** every product that includes the `0` is 0; only `answer[2]` leaves it out: `(-1)·1·(-3)·3 = 9`.

            ### Constraints
            - `2 <= nums.length <= 10^5`
            - `-30 <= nums[i] <= 30`
            - Every prefix and suffix product fits in a 32-bit integer.

            **Follow-up:** can you use only `O(1)` extra space? (The output array doesn't count.)
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** for each position, multiply everything *except* that position.

            1. **Brute force:** for each `i`, multiply all the other elements: `O(n²)`.
            2. **The tempting shortcut:** multiply everything once and divide by `nums[i]`. It's forbidden here, and it breaks on zeros anyway (you can't divide by 0).
            3. **Split the product in two.** Everything except `nums[i]` is *everything to its left* times *everything to its right*: `answer[i] = left[i] · right[i]`. Both are running products:
               - left to right: `left[0] = 1` (nothing on its left), then `left[i] = left[i-1] · nums[i-1]`;
               - right to left: `right[n-1] = 1`, then `right[i] = right[i+1] · nums[i+1]`.
            4. **Save the space:** write the left products straight into `answer`, then walk back from the right with one variable `suffix`, multiplying it in as you go.
            5. **Walk Example 1:** the left pass fills `answer = [1, 1, 2, 6]`. From the right: `suffix = 1`, so `answer[3] = 6`; `suffix = 4`, `answer[2] = 2·4 = 8`; `suffix = 12`, `answer[1] = 12`; `suffix = 24`, `answer[0] = 24`.

            **Pattern to remember:** "combine everything except position i" means prefix and suffix accumulations. The same idea gives range sums, trapping rain water and more.

            **Common mistakes:** starting the running products at 0 instead of 1; including `nums[i]` itself (update the running product *after* using it); reaching for division.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: multiply all the others",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1) extra",
                    Intuition = "For every index, loop over the whole array and multiply everything except that index.",
                    BottleneckExplanation = "Neighbouring answers share almost all of their factors, yet each one is recomputed from scratch.",
                    Code = """
                    int[] ProductExceptSelfBruteForce(int[] nums)
                    {
                        var answer = new int[nums.Length];
                        for (int i = 0; i < nums.Length; i++)
                        {
                            int product = 1;
                            for (int j = 0; j < nums.Length; j++)
                                if (j != i) product *= nums[j];
                            answer[i] = product;
                        }
                        return answer;
                    }

                    Console.WriteLine(Judge.Format(ProductExceptSelfBruteForce(new[] { 1, 2, 3, 4 })));   // [24,12,8,6]
                    """
                },
                new()
                {
                    Name = "Prefix and suffix product arrays",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "left[0] = 1,  left[i] = left[i-1] · nums[i-1]\nright[n-1] = 1,  right[i] = right[i+1] · nums[i+1]\nanswer[i] = left[i] · right[i]",
                    Intuition = "Build the product of everything before each index and the product of everything after it, then multiply the two arrays together.",
                    Code = """
                    int[] ProductExceptSelfWithArrays(int[] nums)
                    {
                        int n = nums.Length;
                        var left = new int[n];
                        var right = new int[n];
                        left[0] = 1;
                        for (int i = 1; i < n; i++) left[i] = left[i - 1] * nums[i - 1];
                        right[n - 1] = 1;
                        for (int i = n - 2; i >= 0; i--) right[i] = right[i + 1] * nums[i + 1];
                        return Enumerable.Range(0, n).Select(i => left[i] * right[i]).ToArray();
                    }

                    Console.WriteLine(Judge.Format(ProductExceptSelfWithArrays(new[] { -1, 1, 0, -3, 3 })));   // [0,0,9,0,0]
                    """
                },
                new()
                {
                    Name = "Left products in the answer, then one running suffix",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1) extra",
                    Intuition = "Fill `answer` with the left products in one pass, then walk back from the right keeping the suffix product in a single variable and multiplying it in."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int[] ProductExceptSelf(int[] nums)
                {
                    int n = nums.Length;
                    var answer = new int[n];

                    int prefix = 1;                      // product of everything left of i
                    for (int i = 0; i < n; i++)
                    {
                        answer[i] = prefix;
                        prefix *= nums[i];
                    }

                    int suffix = 1;                      // product of everything right of i
                    for (int i = n - 1; i >= 0; i--)
                    {
                        answer[i] *= suffix;
                        suffix *= nums[i];
                    }
                    return answer;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [1,2,3,4]", Expected = "[24,12,8,6]", Call = "sol.ProductExceptSelf(new[] { 1, 2, 3, 4 })" },
                new() { Name = "Example 2", Input = "nums = [-1,1,0,-3,3]", Expected = "[0,0,9,0,0]", Call = "sol.ProductExceptSelf(new[] { -1, 1, 0, -3, 3 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Two elements", Input = "nums = [5,7]", Expected = "[7,5]", Call = "sol.ProductExceptSelf(new[] { 5, 7 })" },
                new() { Name = "Two zeros", Input = "nums = [0,4,0]", Expected = "[0,0,0]", Call = "sol.ProductExceptSelf(new[] { 0, 4, 0 })" },
                new() { Name = "One zero", Input = "nums = [2,0,3]", Expected = "[0,6,0]", Call = "sol.ProductExceptSelf(new[] { 2, 0, 3 })" },
                new() { Name = "All negative", Input = "nums = [-1,-2,-3]", Expected = "[6,3,2]", Call = "sol.ProductExceptSelf(new[] { -1, -2, -3 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs brute force",
                random => Enumerable.Range(0, random.Next(2, 9)).Select(_ => random.Next(-4, 5)).ToArray(),
                nums => ProductExceptSelfBruteForce(nums),
                nums => sol.ProductExceptSelf(nums));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            `nums = [2,3,4,5]` on the top row. The left-to-right pass fills **left** (the product of everything before each
            index, whose factors light up); the right-to-left pass fills **right** and multiplies the two into **answer**.
            The solution keeps left in the answer array and right in the single `suffix` variable; the extra rows make both
            halves visible.
            """,
            VisualizationCode = """
            var nums = new[] { 2, 3, 4, 5 };
            int n = nums.Length;
            string[] Blank() => Enumerable.Repeat("", n).ToArray();

            var grid = MatrixTracker.Create(new[] { nums.Select(x => x.ToString()).ToArray(), Blank(), Blank(), Blank() },
                title: "238. Product Except Self: left products times right products",
                options: new MatrixParseOptions { StateClassifier = _ => GridCellState.Default },
                rowHeaders: new[] { "nums", "left", "right", "answer" });

            int prefix = 1, suffix = 1;
            var left = new int[n];
            grid.Watch(() => prefix);
            grid.Watch(() => suffix);

            void Mark(IEnumerable<int> factors, params (int Row, int Col)[] written)
            {
                for (int c = 0; c < n; c++) grid.SetCell(0, c, state: GridCellState.Default);
                foreach (int c in factors) grid.SetCell(0, c, state: GridCellState.Visited);
                foreach (var (r, c) in written) grid.SetCell(r, c, state: GridCellState.Current);
            }

            for (int i = 0; i < n; i++)
            {
                left[i] = prefix;
                grid.SetCell(1, i, val: prefix.ToString());
                Mark(Enumerable.Range(0, i), (1, i));
                grid.Snapshot(i == 0
                    ? "left[0] = 1: nothing is to its left (an empty product is 1)"
                    : $"left[{i}] = {string.Join("·", nums.Take(i))} = {prefix}: everything before index {i}");
                grid.SetCell(1, i, state: GridCellState.Default);
                prefix *= nums[i];
            }

            for (int i = n - 1; i >= 0; i--)
            {
                grid.SetCell(2, i, val: suffix.ToString());
                grid.SetCell(3, i, val: (left[i] * suffix).ToString());
                Mark(Enumerable.Range(i + 1, n - i - 1), (2, i), (3, i));
                grid.Snapshot(i == n - 1
                    ? $"right[{i}] = 1: nothing is to its right, so answer[{i}] = left × right = {left[i]} × 1 = {left[i]}"
                    : $"right[{i}] = {string.Join("·", nums.Skip(i + 1))} = {suffix}, so answer[{i}] = {left[i]} × {suffix} = {left[i] * suffix}");
                grid.SetCell(2, i, state: GridCellState.Default);
                grid.SetCell(3, i, state: GridCellState.Default);
                suffix *= nums[i];
            }

            Mark(Array.Empty<int>());
            grid.Snapshot($"Done: answer = [{string.Join(",", Enumerable.Range(0, n).Select(i => grid.Grid[3, i].DisplayValue))}], and no division was used");
            Display.Visualizer(grid);
            """
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
            TimeComplexity = "O(total characters)",
            SpaceComplexity = "O(total characters)",
            DescriptionMarkdown = """
            Design an algorithm that turns a **list of strings** into one string (to send it somewhere), and turns that string back into the original list.

            Implement a `Codec` class with `string Encode(IList<string> strs)` and `IList<string> Decode(string s)`, so that `Decode(Encode(strs))` returns exactly `strs`. The strings can contain **any** character — `#`, digits, commas, spaces — and may be empty.

            ### Example 1
            - **Input:** `strs = ["lint","code","love","you"]`
            - **Output:** `["lint","code","love","you"]` after encoding and decoding
            - **Why:** one valid encoding is `"4#lint4#code4#love3#you"`: each word is preceded by its length and a `#`.

            ### Example 2
            - **Input:** `strs = ["we","say",":","yes"]`
            - **Output:** `["we","say",":","yes"]`

            ### Example 3
            - **Input:** `strs = ["4#ab",""]`
            - **Output:** `["4#ab",""]`
            - **Why:** a word can look exactly like a length prefix, and a word can be empty. The encoding `"4#4#ab0#"` still decodes correctly, because the decoder takes the next 4 characters without looking inside them.

            ### Constraints
            - `0 <= strs.length < 200`
            - `0 <= strs[i].length < 200`
            - `strs[i]` contains any of the 256 valid ASCII characters.

            **Follow-up:** can you avoid relying on a delimiter that must never appear in the words?
            """,
            ThinkingProcessMarkdown = """
            **What makes it hard:** any separator you pick (a comma, `#`, `|`) can appear *inside* a word, so the decoder can't tell a separator from a letter. An empty list and a list holding one empty string must also stay different.

            1. **Joining with a separator fails:** `["a,b","c"]` joined by `,` is `"a,b,c"`, which splits back into three words.
            2. **Escaping works but is fiddly:** double every special character and remember to undo it.
            3. **Frame each word with its length.** Write `<length>#<word>` for every word. The decoder reads digits up to the first `#` to learn the length `L`, then takes exactly the next `L` characters **without looking at them**, whatever they are. Then it repeats from just after those characters.
            4. **Walk Example 3:** encoding `["4#ab", ""]` gives `"4#4#ab0#"`. Decoding: the digits before the first `#` say 4, so take `"4#ab"`; now at `0#`: length 0, so the next word is `""`. Done.

            **Pattern to remember:** length-prefixed framing, used by HTTP's Content-Length, network protocols and binary file formats. When content can be anything, say how long it is up front instead of marking where it ends.

            **Common mistakes:** searching for `#` from the start of the whole string instead of the current position; assuming lengths are one digit; losing empty words.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Join with a separator (and see it break)",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(total characters)",
                    SpaceComplexity = "O(total characters)",
                    Intuition = "`string.Join(\",\", strs)` to encode and `Split(',')` to decode. It is simple and fast, and it breaks as soon as a word contains a comma.",
                    BottleneckExplanation = "Any separator can appear inside a word, and an empty list can't be told apart from a list holding one empty string.",
                    Code = """
                    string EncodeWithComma(IList<string> strs) => string.Join(",", strs);
                    IList<string> DecodeWithComma(string s) => s.Split(',');

                    // The comma inside "a,b" is read as a separator, so three words come back instead of two:
                    Console.WriteLine(Judge.Format(DecodeWithComma(EncodeWithComma(new[] { "a,b", "c" }))));   // ["a","b","c"]
                    """
                },
                new()
                {
                    Name = "Length prefix: <length>#<word>",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(total characters)",
                    SpaceComplexity = "O(total characters)",
                    Intuition = "Write each word's length and a `#` before it. The decoder reads the length, then takes exactly that many characters without inspecting them, so words may contain anything."
                }
            },
            SolutionCode = """
            public class Codec
            {
                // Each word becomes "<length>#<word>", so the decoder never has to look inside a word.
                public string Encode(IList<string> strs)
                {
                    var encoded = new StringBuilder();
                    foreach (var word in strs) encoded.Append(word.Length).Append('#').Append(word);
                    return encoded.ToString();
                }

                public IList<string> Decode(string s)
                {
                    var words = new List<string>();
                    int i = 0;
                    while (i < s.Length)
                    {
                        int hash = s.IndexOf('#', i);             // the length is written before this '#'
                        int length = int.Parse(s[i..hash]);
                        words.Add(s.Substring(hash + 1, length));
                        i = hash + 1 + length;
                    }
                    return words;
                }
            }
            """,
            TestSetupCode = """
            // Decoding the encoding must give back exactly the same list.
            IList<string> RoundTrip(params string[] strs)
            {
                var codec = new Codec();
                return codec.Decode(codec.Encode(strs));
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = """strs = ["lint","code","love","you"]""", Expected = """["lint","code","love","you"]""", Call = """RoundTrip("lint", "code", "love", "you")""" },
                new() { Name = "Example 2", Input = """strs = ["we","say",":","yes"]""", Expected = """["we","say",":","yes"]""", Call = """RoundTrip("we", "say", ":", "yes")""" },
                new() { Name = "Example 3", Input = """strs = ["4#ab",""]""", Expected = """["4#ab",""]""", Call = """RoundTrip("4#ab", "")""" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "The encoded form", Input = """Encode(["lint","code"])""", Expected = "\"4#lint4#code\"", Call = """new Codec().Encode(new[] { "lint", "code" })""" },
                new() { Name = "An empty list", Input = "strs = []", Expected = "[]", Call = "RoundTrip()" },
                new() { Name = "One empty string", Input = """strs = [""]""", Expected = """[""]""", Call = """RoundTrip("")""" },
                new() { Name = "Digits, spaces and a long word", Input = """strs = ["12"," ","#","aaaaaaaaaaaa"]""", Expected = """["12"," ","#","aaaaaaaaaaaa"]""", Call = """RoundTrip("12", " ", "#", "aaaaaaaaaaaa")""" }
            },
            StressTestCode = """
            judge.Agree("Random lists of tricky words survive the round trip",
                random => Enumerable.Range(0, random.Next(0, 6))
                    .Select(_ => new string(Enumerable.Range(0, random.Next(0, 5)).Select(_ => "a#1, "[random.Next(5)]).ToArray()))
                    .ToArray(),
                strs => (IList<string>)strs,
                strs => RoundTrip(strs));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            `["we","4#ab","","yes"]` is encoded character by character: each word adds its length, a `#` and then itself
            (the new cells light up). Decoding walks the same cells: `i` reads the digits up to `hash`, which gives the
            length, and exactly that many characters are taken into `decoded`, even when they look like `4#` or are empty.
            """,
            VisualizationCode = """
            var words = new[] { "we", "4#ab", "", "yes" };
            var encoded = new List<char>();
            var tracker = VisualizerRecorder.CreateArray(encoded, title: "271. Encode and Decode Strings: length, '#', then the word");

            foreach (var word in words)
            {
                int start = encoded.Count;
                encoded.AddRange($"{word.Length}#{word}");
                tracker.Step(word.Length == 0
                    ? "encode \"\": an empty word still writes its length, 0#"
                    : $"encode \"{word}\": write its length {word.Length}, a '#', then the word as it is",
                    highlight: Enumerable.Range(start, encoded.Count - start));
            }

            var decoded = new List<string>();
            tracker.Watch(decoded);
            int i = 0;
            while (i < encoded.Count)
            {
                int hash = encoded.IndexOf('#', i);
                int length = int.Parse(new string(encoded.GetRange(i, hash - i).ToArray()));
                tracker.Step($"decode: the digits from i up to the next '#' say the next word has {length} character{(length == 1 ? "" : "s")}",
                    pointers: new { i, hash }, highlight: Enumerable.Range(i, hash - i + 1));

                var word = new string(encoded.GetRange(hash + 1, length).ToArray());
                decoded.Add(word);
                tracker.Step(length == 0
                    ? "decode: take 0 characters, so this word is empty"
                    : $"decode: take the next {length} characters without looking inside them: \"{word}\"",
                    pointers: new { hash }, highlight: Enumerable.Range(hash + 1, length));
                i = hash + 1 + length;
            }

            tracker.Step($"Decoded {decoded.Count} words, exactly the list we started with");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given an unsorted array of integers `nums`, return the length of the longest run of **consecutive values** (like 1, 2, 3, 4). The values can be anywhere in the array, in any order.

            Your algorithm must run in `O(n)` time.

            ### Example 1
            - **Input:** `nums = [100,4,200,1,3,2]`
            - **Output:** `4`
            - **Why:** `1, 2, 3, 4` are all present; `100` and `200` are runs of length 1.

            ### Example 2
            - **Input:** `nums = [0,3,7,2,5,8,4,6,0,1]`
            - **Output:** `9`
            - **Why:** every value from `0` to `8` is there (the second `0` doesn't make the run longer).

            ### Example 3
            - **Input:** `nums = [1,0,1,2]`
            - **Output:** `3`

            ### Constraints
            - `0 <= nums.length <= 10^5`
            - `-10^9 <= nums[i] <= 10^9`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** among all the values present, find the longest stretch `x, x+1, x+2, …` where every value is in the array.

            1. **Sorting:** sort, then scan for runs of `+1` steps, skipping duplicates. That's `O(n log n)`, and the problem asks for `O(n)`.
            2. **Membership in O(1):** put every value in a `HashSet<int>`. Now "is `x + 1` here?" is instant.
            3. **Only count from the start of a run.** A value `x` starts a run exactly when `x − 1` is **not** in the set. From each start, count up `x+1, x+2, …` while the set has them. Values in the middle of a run are skipped, because their run gets counted from its start.
            4. **Why that's O(n):** each value is looked at once as a possible start, and at most once more while some run passes through it. Without the start check, `[1,2,…,n]` would recount almost the whole run from every value: `O(n²)`.
            5. **Walk Example 1:** `100` starts a run (99 missing) of length 1; `4` is skipped (3 is present); `200` gives 1; `1` starts a run: 2, 3, 4 are present, 5 isn't, so the length is 4; `3` and `2` are skipped. The answer is 4.

            **Pattern to remember:** a hash set gives O(1) "is it there?"; then do the expensive work only from a *canonical starting point* (here, the smallest value of each run).

            **Common mistakes:** counting up from every value (quadratic); not handling duplicates or an empty array (answer 0); sorting when the problem demands `O(n)`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Count up from every value with a linear search",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n³)",
                    SpaceComplexity = "O(1)",
                    Intuition = "From each value `x`, keep checking whether `x + 1`, `x + 2`, … appear in the array by scanning it.",
                    BottleneckExplanation = "Every membership check scans the whole array, and runs are recounted from each of their values.",
                    Code = """
                    int LongestConsecutiveBruteForce(int[] nums)
                    {
                        int best = 0;
                        foreach (int start in nums)
                        {
                            int length = 1;
                            while (nums.Contains(start + length)) length++;
                            best = Math.Max(best, length);
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(LongestConsecutiveBruteForce(new[] { 100, 4, 200, 1, 3, 2 })));   // 4
                    """
                },
                new()
                {
                    Name = "Sort, then scan runs",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n log n)",
                    SpaceComplexity = "O(n) for the sorted copy",
                    Intuition = "After sorting, a run is a stretch where each value is one more than the previous; equal neighbours are skipped without breaking the run.",
                    Code = """
                    int LongestConsecutiveBySorting(int[] nums)
                    {
                        if (nums.Length == 0) return 0;
                        var sorted = nums.Distinct().OrderBy(n => n).ToArray();
                        int best = 1, current = 1;
                        for (int i = 1; i < sorted.Length; i++)
                        {
                            current = sorted[i] == sorted[i - 1] + 1 ? current + 1 : 1;
                            best = Math.Max(best, current);
                        }
                        return best;
                    }

                    Console.WriteLine(Judge.Format(LongestConsecutiveBySorting(new[] { 0, 3, 7, 2, 5, 8, 4, 6, 0, 1 })));   // 9
                    """
                },
                new()
                {
                    Name = "Hash set, counting only from the start of each run",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Put every value in a set. A value whose predecessor is missing starts a run; count up from there. Each value is visited at most twice."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int LongestConsecutive(int[] nums)
                {
                    var present = new HashSet<int>(nums);
                    int longest = 0;
                    foreach (int n in present)
                    {
                        if (present.Contains(n - 1)) continue;   // inside a run: it is counted from the run's start

                        int length = 1;
                        while (present.Contains(n + length)) length++;
                        longest = Math.Max(longest, length);
                    }
                    return longest;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [100,4,200,1,3,2]", Expected = "4", Call = "sol.LongestConsecutive(new[] { 100, 4, 200, 1, 3, 2 })" },
                new() { Name = "Example 2", Input = "nums = [0,3,7,2,5,8,4,6,0,1]", Expected = "9", Call = "sol.LongestConsecutive(new[] { 0, 3, 7, 2, 5, 8, 4, 6, 0, 1 })" },
                new() { Name = "Example 3", Input = "nums = [1,0,1,2]", Expected = "3", Call = "sol.LongestConsecutive(new[] { 1, 0, 1, 2 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Empty array", Input = "nums = []", Expected = "0", Call = "sol.LongestConsecutive(Array.Empty<int>())" },
                new() { Name = "A single value", Input = "nums = [7]", Expected = "1", Call = "sol.LongestConsecutive(new[] { 7 })" },
                new() { Name = "Negative run", Input = "nums = [-3,-1,-2,5]", Expected = "3", Call = "sol.LongestConsecutive(new[] { -3, -1, -2, 5 })" },
                new() { Name = "Two runs, the longer wins", Input = "nums = [10,5,12,3,11,4,6]", Expected = "4", Call = "sol.LongestConsecutive(new[] { 10, 5, 12, 3, 11, 4, 6 })" }
            },
            StressTestCode = """
            judge.Agree("Random arrays vs sorting",
                random => Enumerable.Range(0, random.Next(0, 12)).Select(_ => random.Next(-5, 12)).ToArray(),
                nums => LongestConsecutiveBySorting(nums),
                nums => sol.LongestConsecutive(nums));
            """,
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 1, `[100,4,200,1,3,2]`, with every value in the `present` set. The `i` pointer visits each value: if
            its predecessor is present it is skipped, because its run will be counted from the start. At a start, the run's
            cells light up one by one as it grows, and `longest` keeps the best length so far.
            """,
            VisualizationCode = """
            var nums = new[] { 100, 4, 200, 1, 3, 2 };
            var present = new HashSet<int>(nums);
            var indexOf = new Dictionary<int, int>();
            for (int k = 0; k < nums.Length; k++) indexOf.TryAdd(nums[k], k);

            int longest = 0;
            var tracker = VisualizerRecorder.CreateArray(nums, title: "128. Longest Consecutive Sequence: count only from the start of a run");
            tracker.Watch(present);
            tracker.Watch(() => longest);

            for (int i = 0; i < nums.Length; i++)
            {
                int n = nums[i];
                if (present.Contains(n - 1))
                {
                    tracker.Step($"{n}: {n - 1} is present, so {n} sits inside a run that starts lower. Skip it", pointers: new { i });
                    continue;
                }

                var run = new List<int> { indexOf[n] };
                tracker.Step($"{n}: {n - 1} is missing, so {n} starts a run", pointers: new { i }, highlight: run);
                while (present.Contains(n + run.Count))
                {
                    run.Add(indexOf[n + run.Count]);
                    tracker.Step($"{n + run.Count - 1} is present too: the run grows to length {run.Count}", pointers: new { i }, highlight: run);
                }

                longest = Math.Max(longest, run.Count);
                tracker.Step($"{n + run.Count} is missing: the run {n}..{n + run.Count - 1} has length {run.Count}; the longest so far is {longest}", pointers: new { i }, highlight: run);
            }

            Display.Visualizer(tracker);
            """
        },
    };
}
