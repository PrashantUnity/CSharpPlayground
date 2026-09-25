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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            A phrase is a **palindrome** if, after turning every uppercase letter into lowercase and removing everything that isn't a letter or a digit, it reads the same forward and backward.

            Given a string `s`, return `true` if it is a palindrome, or `false` otherwise.

            ### Example 1
            - **Input:** `s = "A man, a plan, a canal: Panama"`
            - **Output:** `true`
            - **Why:** keeping only letters and digits, in lowercase, gives `"amanaplanacanalpanama"`, which is the same reversed.

            ### Example 2
            - **Input:** `s = "race a car"`
            - **Output:** `false`
            - **Why:** `"raceacar"` reversed is `"racaecar"`.

            ### Example 3
            - **Input:** `s = " "`
            - **Output:** `true`
            - **Why:** nothing is left after removing the space, and an empty string reads the same both ways.

            ### Constraints
            - `1 <= s.length <= 2 * 10^5`
            - `s` consists only of printable ASCII characters.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** ignore case and anything that isn't a letter or digit; does what's left mirror itself?

            1. **The direct way:** build the cleaned-up string, reverse it, and compare. That's `O(n)` time, but it needs `O(n)` extra space for the copies.
            2. **Compare from both ends instead.** Put `left` at the start and `right` at the end. A palindrome's first character must equal its last, its second must equal its second-to-last, and so on, so move the two pointers toward each other comparing as you go.
            3. **Skip what doesn't count.** If `s[left]` isn't a letter or digit, step `left` forward; likewise step `right` back. Only compare two characters that both count, using lowercase.
            4. **Stop early:** the first mismatch means `false`. If the pointers meet or cross without one, it's `true`.
            5. **Walk Example 2:** `r`/`r` match, `a`/`a` match, `c`/`c` match, then `e` vs `a` → mismatch → `false`.

            **Pattern to remember:** mirrored comparisons are two pointers converging from both ends, with no copies.

            **Common mistakes:** forgetting that digits count (`"0P"` is **not** a palindrome); comparing without lowercasing; letting the skip loops run past each other (always keep `left < right`).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Clean up, reverse and compare",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Keep only letters and digits, in lowercase, then check whether that string equals its reverse.",
                    BottleneckExplanation = "It is linear, but it builds two extra copies of the string when two indices would do.",
                    Code = """
                    bool IsPalindromeByReversing(string s)
                    {
                        var cleaned = new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                        return cleaned.SequenceEqual(cleaned.Reverse());
                    }

                    Show(IsPalindromeByReversing("A man, a plan, a canal: Panama"));   // true
                    """
                },
                new()
                {
                    Name = "Two pointers from both ends",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Walk `left` forward and `right` backward, skipping anything that isn't a letter or digit and comparing the rest in lowercase. The first mismatch answers false."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool IsPalindrome(string s)
                {
                    int left = 0, right = s.Length - 1;
                    while (left < right)
                    {
                        if (!char.IsLetterOrDigit(s[left])) { left++; continue; }
                        if (!char.IsLetterOrDigit(s[right])) { right--; continue; }
                        if (char.ToLowerInvariant(s[left]) != char.ToLowerInvariant(s[right])) return false;
                        left++;
                        right--;
                    }
                    return true;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"A man, a plan, a canal: Panama\"", Expected = "true", Call = "sol.IsPalindrome(\"A man, a plan, a canal: Panama\")" },
                new() { Name = "Example 2", Input = "s = \"race a car\"", Expected = "false", Call = "sol.IsPalindrome(\"race a car\")" },
                new() { Name = "Example 3", Input = "s = \" \"", Expected = "true", Call = "sol.IsPalindrome(\" \")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Digits count too", Input = "s = \"0P\"", Expected = "false", Call = "sol.IsPalindrome(\"0P\")" },
                new() { Name = "Only punctuation", Input = "s = \".,!\"", Expected = "true", Call = "sol.IsPalindrome(\".,!\")" },
                new() { Name = "Mixed case", Input = "s = \"Madam, I'm Adam\"", Expected = "true", Call = "sol.IsPalindrome(\"Madam, I'm Adam\")" },
                new() { Name = "Underscore is skipped", Input = "s = \"ab_a\"", Expected = "true", Call = "sol.IsPalindrome(\"ab_a\")" }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            The characters of `"No 'x' in Nixon"`. `left` and `right` start at the two ends: spaces and apostrophes are
            skipped (the step says why), letters are compared in lowercase, and each matched pair turns green as the
            pointers close in. They meet without a mismatch, so it is a palindrome.
            """,
            VisualizationCode = """
            var s = "No 'x' in Nixon";
            var tracker = VisualizerRecorder.CreateArray(s, title: "125. Valid Palindrome: compare from both ends");
            var cells = tracker.Options.ArrayData!.Items;

            int left = 0, right = s.Length - 1;
            bool palindrome = true;
            while (left < right)
            {
                if (!char.IsLetterOrDigit(s[left]))
                {
                    tracker.Step($"s[{left}] is '{s[left]}', not a letter or digit: skip it", pointers: new { left, right });
                    left++;
                    continue;
                }
                if (!char.IsLetterOrDigit(s[right]))
                {
                    tracker.Step($"s[{right}] is '{s[right]}', not a letter or digit: skip it", pointers: new { left, right });
                    right--;
                    continue;
                }

                bool same = char.ToLowerInvariant(s[left]) == char.ToLowerInvariant(s[right]);
                tracker.Step(same
                    ? $"'{s[left]}' and '{s[right]}' are the same letter (ignoring case): both pointers move inward"
                    : $"'{s[left]}' and '{s[right]}' differ: not a palindrome", pointers: new { left, right }, highlight: new[] { left, right });
                if (!same)
                {
                    palindrome = false;
                    break;
                }
                cells[left].Color = cells[right].Color = "#14532d";
                left++;
                right--;
            }

            tracker.Step(palindrome ? "The pointers met without a mismatch: it reads the same both ways, true" : "Answer: false");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n²)",
            SpaceComplexity = "O(1) extra (besides sorting and the output)",
            DescriptionMarkdown = """
            Given an integer array `nums`, return every **distinct** triplet `[nums[i], nums[j], nums[k]]` with `i`, `j`, `k` all different and `nums[i] + nums[j] + nums[k] == 0`.

            The answer must not contain the same triplet twice. Triplets and their elements may be listed in any order.

            ### Example 1
            - **Input:** `nums = [-1,0,1,2,-1,-4]`
            - **Output:** `[[-1,-1,2],[-1,0,1]]`
            - **Why:** `(-1) + (-1) + 2 = 0` and `(-1) + 0 + 1 = 0`. The triplet `[-1,0,1]` can be built with either of the two `-1`s, but it is listed once.

            ### Example 2
            - **Input:** `nums = [0,1,1]`
            - **Output:** `[]`
            - **Why:** the only possible triplet sums to 2.

            ### Example 3
            - **Input:** `nums = [0,0,0]`
            - **Output:** `[[0,0,0]]`

            ### Constraints
            - `3 <= nums.length <= 3000`
            - `-10^5 <= nums[i] <= 10^5`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** find every set of three values (by position) that sums to 0, reporting each set of values once.

            1. **Brute force:** three nested loops over `i < j < k` is `O(n³)`, about 4.5·10⁹ checks for 3000 numbers, and duplicates still have to be filtered out.
            2. **Sort first.** Sorting makes two things easy: duplicates sit side by side (so they can be skipped), and it turns the inner search into the **Two Sum II** trick.
            3. **Fix one number, two-pointer the rest.** For each `i`, look for two values after it that sum to `-nums[i]`: put `lo` right after `i` and `hi` at the end. If the sum is too small, only moving `lo` right can raise it; if too big, only moving `hi` left can lower it; if it's exactly 0, record the triplet and move both.
            4. **Skipping duplicates:** skip an `i` whose value equals the previous `i`'s (it would find the same triplets), and after a match move `lo` past equal values. Once `nums[i] > 0`, stop: three numbers ≥ that can't reach 0.
            5. **Walk Example 1** (sorted `[-4,-1,-1,0,1,2]`): `i = -4` needs 4 from the rest; no pair reaches it. `i = -1`: `lo = -1`, `hi = 2` → sum 0, record `[-1,-1,2]`; then `0 + 1` → `[-1,0,1]`. The next `-1` is skipped as a duplicate.

            **Pattern to remember:** k-sum problems are "sort, fix k−2 numbers, then two pointers" (`O(n^(k-1))`).

            **Common mistakes:** deduplicating afterwards with sets instead of skipping (slow and messy); forgetting to skip duplicates of `i` or of `lo`; stopping `lo < hi` too early after a match.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: every triple, deduplicated",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n³)",
                    SpaceComplexity = "O(answer)",
                    Intuition = "Try every `i < j < k`; sort each zero-sum triple so equal triples look the same, and keep them in a set.",
                    BottleneckExplanation = "Checking all triples is cubic, and most of them are hopeless: the sorted order could rule them out in bulk.",
                    Code = """
                    List<List<int>> ThreeSumBruteForce(int[] nums)
                    {
                        var seen = new HashSet<string>();
                        var result = new List<List<int>>();
                        for (int i = 0; i < nums.Length; i++)
                            for (int j = i + 1; j < nums.Length; j++)
                                for (int k = j + 1; k < nums.Length; k++)
                                {
                                    if (nums[i] + nums[j] + nums[k] != 0) continue;
                                    var triple = new[] { nums[i], nums[j], nums[k] }.OrderBy(x => x).ToList();
                                    if (seen.Add(string.Join(",", triple))) result.Add(triple);
                                }
                        return result;
                    }

                    Show(ThreeSumBruteForce(new[] { -1, 0, 1, 2, -1, -4 }));   // [[-1,0,1],[-1,-1,2]]
                    """
                },
                new()
                {
                    Name = "Sort, fix one number, two pointers for the other two",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1) extra",
                    Intuition = "After sorting, each `nums[i]` turns the rest into a two-pointer search for `-nums[i]`. Skipping repeated values for `i` and after each match keeps triplets unique without any set."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public IList<IList<int>> ThreeSum(int[] nums)
                {
                    Array.Sort(nums);
                    var result = new List<IList<int>>();
                    for (int i = 0; i < nums.Length - 2 && nums[i] <= 0; i++)
                    {
                        if (i > 0 && nums[i] == nums[i - 1]) continue;          // same first number: same triplets

                        int lo = i + 1, hi = nums.Length - 1;
                        while (lo < hi)
                        {
                            int sum = nums[i] + nums[lo] + nums[hi];
                            if (sum < 0) lo++;
                            else if (sum > 0) hi--;
                            else
                            {
                                result.Add(new[] { nums[i], nums[lo], nums[hi] });
                                lo++;
                                hi--;
                                while (lo < hi && nums[lo] == nums[lo - 1]) lo++;   // skip repeats of the second number
                            }
                        }
                    }
                    return result;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [-1,0,1,2,-1,-4]", Expected = "[[-1,-1,2],[-1,0,1]]", Call = "sol.ThreeSum(new[] { -1, 0, 1, 2, -1, -4 })", AnyOrder = true },
                new() { Name = "Example 2", Input = "nums = [0,1,1]", Expected = "[]", Call = "sol.ThreeSum(new[] { 0, 1, 1 })", AnyOrder = true },
                new() { Name = "Example 3", Input = "nums = [0,0,0]", Expected = "[[0,0,0]]", Call = "sol.ThreeSum(new[] { 0, 0, 0 })", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Four zeros, one triplet", Input = "nums = [0,0,0,0]", Expected = "[[0,0,0]]", Call = "sol.ThreeSum(new[] { 0, 0, 0, 0 })", AnyOrder = true },
                new() { Name = "All positive", Input = "nums = [1,2,3]", Expected = "[]", Call = "sol.ThreeSum(new[] { 1, 2, 3 })", AnyOrder = true },
                new() { Name = "Duplicates everywhere", Input = "nums = [-2,0,0,2,2]", Expected = "[[-2,0,2]]", Call = "sol.ThreeSum(new[] { -2, 0, 0, 2, 2 })", AnyOrder = true },
                new() { Name = "Two triplets", Input = "nums = [-2,-1,0,1,2]", Expected = "[[-2,0,2],[-1,0,1]]", Call = "sol.ThreeSum(new[] { -2, -1, 0, 1, 2 })", AnyOrder = true }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 1 sorted to `[-4,-1,-1,0,1,2]`. For each `i`, `lo` and `hi` squeeze toward each other: the `sum` chip
            says whether the triple is too small (move `lo` right), too big (move `hi` left) or zero (record it in
            `triplets`). Watch the second `-1` get skipped because it would only find the same triplets again.
            """,
            VisualizationCode = """
            var nums = new[] { -1, 0, 1, 2, -1, -4 };
            var triplets = new List<string>();
            int sum = 0;
            var tracker = VisualizerRecorder.CreateArray(nums, title: "15. 3Sum: sort, fix i, then two pointers");
            tracker.Watch(() => sum);
            tracker.Watch(triplets);

            Array.Sort(nums);
            tracker.Step("Sort: equal values sit together, and the two-pointer search becomes possible");

            for (int i = 0; i < nums.Length - 2 && nums[i] <= 0; i++)
            {
                if (i > 0 && nums[i] == nums[i - 1])
                {
                    tracker.Step($"nums[{i}] = {nums[i]} equals the previous first number: every triplet it could find is already known, skip it", pointers: new { i });
                    continue;
                }

                int lo = i + 1, hi = nums.Length - 1;
                while (lo < hi)
                {
                    sum = nums[i] + nums[lo] + nums[hi];
                    if (sum < 0)
                    {
                        tracker.Step($"{nums[i]} + {nums[lo]} + {nums[hi]} = {sum} is too small: move lo right to a bigger number", pointers: new { i, lo, hi });
                        lo++;
                    }
                    else if (sum > 0)
                    {
                        tracker.Step($"{nums[i]} + {nums[lo]} + {nums[hi]} = {sum} is too big: move hi left to a smaller number", pointers: new { i, lo, hi });
                        hi--;
                    }
                    else
                    {
                        triplets.Add($"[{nums[i]},{nums[lo]},{nums[hi]}]");
                        tracker.Step($"{nums[i]} + {nums[lo]} + {nums[hi]} = 0: record it, then move both pointers", pointers: new { i, lo, hi }, highlight: new[] { i, lo, hi });
                        lo++;
                        hi--;
                        while (lo < hi && nums[lo] == nums[lo - 1]) lo++;
                    }
                }
            }

            tracker.Step($"Done: {triplets.Count} distinct triplets");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given `n` vertical lines: line `i` stands at position `i` with height `height[i]`. Pick **two** lines that, together with the x-axis, form a container, and return the **most water** it can hold.

            The water level can't be higher than the shorter of the two lines, so a container between lines `i` and `j` holds `(j − i) · min(height[i], height[j])`. You may not tilt the container.

            ### Example 1
            - **Input:** `height = [1,8,6,2,5,4,8,3,7]`
            - **Output:** `49`
            - **Why:** the lines at positions 1 (height 8) and 8 (height 7) are 7 apart, and the water can rise to 7: `7 · 7 = 49`.

            ### Example 2
            - **Input:** `height = [1,1]`
            - **Output:** `1`

            ### Constraints
            - `2 <= height.length <= 10^5`
            - `0 <= height[i] <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** maximise `width × shorter height` over all pairs of lines.

            1. **Brute force:** try every pair: `O(n²)`, about 5·10⁹ pairs for 10⁵ lines.
            2. **Start as wide as possible.** Put `left` at the first line and `right` at the last. That container has the maximum width; every other container is narrower, so it can only win by being taller.
            3. **Which line to give up?** The water level is set by the *shorter* line. If you moved the taller line inward, the width shrinks and the level still can't rise above the shorter line, so the area can only get worse. So the shorter line is useless for every narrower container: **move the shorter one** inward and keep the best area seen.
            4. **Walk Example 1:** `1` vs `7` (width 8) holds 8; move the 1. `8` vs `7` (width 7) holds 49; move the 7. `8` vs `3` holds 18; move the 3. … Nothing beats 49.

            **Pattern to remember:** two pointers from both ends with a greedy rule for which side to move, justified by "the side you drop can't be part of a better answer".

            **Common mistakes:** moving the taller line; using the taller height instead of the shorter; forgetting that equal heights can move either pointer (both are safe).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: every pair of lines",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Compute `(j − i) · min(height[i], height[j])` for every pair and keep the largest.",
                    BottleneckExplanation = "It keeps trying pairs that include a short line even after a wider container with that line has been measured, which is provably pointless.",
                    Code = """
                    int MaxAreaBruteForce(int[] height)
                    {
                        int best = 0;
                        for (int i = 0; i < height.Length; i++)
                            for (int j = i + 1; j < height.Length; j++)
                                best = Math.Max(best, (j - i) * Math.Min(height[i], height[j]));
                        return best;
                    }

                    Show(MaxAreaBruteForce(new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 }));   // 49
                    """
                },
                new()
                {
                    Name = "Two pointers: always move the shorter line",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "The shorter line can't form a bigger container with any line closer in, so dropping it loses nothing.",
                    Intuition = "Start with the widest container and repeatedly move the pointer at the shorter line inward, keeping the best area. Each line is dropped once, so it is linear."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MaxArea(int[] height)
                {
                    int left = 0, right = height.Length - 1, best = 0;
                    while (left < right)
                    {
                        best = Math.Max(best, (right - left) * Math.Min(height[left], height[right]));
                        if (height[left] < height[right]) left++;    // the shorter line can't do better with anything closer
                        else right--;
                    }
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "height = [1,8,6,2,5,4,8,3,7]", Expected = "49", Call = "sol.MaxArea(new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 })" },
                new() { Name = "Example 2", Input = "height = [1,1]", Expected = "1", Call = "sol.MaxArea(new[] { 1, 1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Tall ends win", Input = "height = [4,3,2,1,4]", Expected = "16", Call = "sol.MaxArea(new[] { 4, 3, 2, 1, 4 })" },
                new() { Name = "Short middle", Input = "height = [1,2,1]", Expected = "2", Call = "sol.MaxArea(new[] { 1, 2, 1 })" },
                new() { Name = "No height", Input = "height = [0,0]", Expected = "0", Call = "sol.MaxArea(new[] { 0, 0 })" },
                new() { Name = "Best pair is not the widest", Input = "height = [1,2,4,3]", Expected = "4", Call = "sol.MaxArea(new[] { 1, 2, 4, 3 })" }
            },
            VisualizerKind = "Bars",
            VisualizationDescription = """
            The lines of Example 1 as bars. The shaded block between `left` and `right` is the water the current
            container holds (its area is in the step text and the `area` chip); `best` keeps the record. Each step drops
            the shorter wall, and you can see why: the water could never rise above it anyway.
            """,
            VisualizationCode = """
            var height = new[] { 1, 8, 6, 2, 5, 4, 8, 3, 7 };
            var tracker = VisualizerRecorder.CreateBars(height, title: "11. Container With Most Water: move the shorter wall");
            int left = 0, right = height.Length - 1, area = 0, best = 0;
            tracker.Watch(() => area);
            tracker.Watch(() => best);

            while (left < right)
            {
                int level = Math.Min(height[left], height[right]);
                area = (right - left) * level;
                bool record = area > best;
                best = Math.Max(best, area);
                string drop = height[left] < height[right] ? "left" : "right";
                tracker.Step($"Walls {height[left]} and {height[right]}, {right - left} apart: water rises to {level}, area {area}{(record ? " (new best)" : "")}. The {drop} wall is shorter, so move it",
                    pointers: new { left, right }, shade: new BarShade(left, right, level, $"area {area}"));
                if (height[left] < height[right]) left++;
                else right--;
            }

            tracker.Step($"The pointers met: the most water is {best}");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given `prices`, where `prices[i]` is a stock's price on day `i`. Choose **one** day to buy and a **later** day to sell, and return the largest profit you can make. If no profit is possible, return `0`.

            ### Example 1
            - **Input:** `prices = [7,1,5,3,6,4]`
            - **Output:** `5`
            - **Why:** buy on day 1 at price 1 and sell on day 4 at price 6: `6 − 1 = 5`. Selling on day 0 at 7 isn't allowed, because you must buy before you sell.

            ### Example 2
            - **Input:** `prices = [7,6,4,3,1]`
            - **Output:** `0`
            - **Why:** the price only falls, so every trade loses money; doing nothing earns 0.

            ### Constraints
            - `1 <= prices.length <= 10^5`
            - `0 <= prices[i] <= 10^4`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** maximise `prices[sell] − prices[buy]` with `buy < sell`, or 0.

            1. **Brute force:** try every buy day with every later sell day: `O(n²)`.
            2. **Fix the sell day.** If you sell on day `i`, the best buy is simply the **cheapest price before day `i`**. So the question per day is: "what's the lowest price I've seen so far?"
            3. **One pass:** keep `minPrice` (the cheapest day so far) and `maxProfit`. For each price, first try selling today (`price − minPrice`), then update `minPrice` if today is cheaper. Order doesn't matter much here, because selling on the day you buy earns 0.
            4. **Walk Example 1:** 7 → min 7. 1 → min 1. 5 → profit 4. 3 → profit 2. 6 → profit 5 (best). 4 → profit 3. The answer is 5.

            **Pattern to remember:** "best pair with i < j" usually needs only a running summary of everything before `j` (here, the minimum). This is the simplest dynamic-programming / sliding-window idea.

            **Common mistakes:** taking the global minimum even when it comes *after* the maximum (you can't sell before buying); returning a negative number instead of 0.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: every buy day with every later sell day",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Two nested loops compute `prices[j] − prices[i]` for every `i < j`.",
                    BottleneckExplanation = "For each sell day it re-scans all earlier days to find the cheapest one, which one running minimum already knows.",
                    Code = """
                    int MaxProfitBruteForce(int[] prices)
                    {
                        int best = 0;
                        for (int i = 0; i < prices.Length; i++)
                            for (int j = i + 1; j < prices.Length; j++)
                                best = Math.Max(best, prices[j] - prices[i]);
                        return best;
                    }

                    Show(MaxProfitBruteForce(new[] { 7, 1, 5, 3, 6, 4 }));   // 5
                    """
                },
                new()
                {
                    Name = "Prefix minimum array",
                    Kind = ApproachKind.DynamicProgramming,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    RecurrenceRelation = "minBefore[0] = prices[0]\nminBefore[i] = min(minBefore[i-1], prices[i])\nanswer = max over i of prices[i] − minBefore[i]",
                    Intuition = "First record the cheapest price up to each day, then the best profit for selling on day `i` is `prices[i] − minBefore[i]`.",
                    Code = """
                    int MaxProfitWithPrefixMin(int[] prices)
                    {
                        var minBefore = new int[prices.Length];
                        minBefore[0] = prices[0];
                        for (int i = 1; i < prices.Length; i++) minBefore[i] = Math.Min(minBefore[i - 1], prices[i]);
                        return Enumerable.Range(0, prices.Length).Max(i => prices[i] - minBefore[i]);
                    }

                    Show(MaxProfitWithPrefixMin(new[] { 7, 6, 4, 3, 1 }));   // 0
                    """
                },
                new()
                {
                    Name = "One pass: cheapest day so far",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    GreedyChoiceProperty = "For any sell day, buying at the lowest earlier price is never worse than buying at any other earlier price.",
                    Intuition = "Keep the minimum price so far and the best profit; each day, try selling at today's price, then lower the minimum if today is cheaper."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int MaxProfit(int[] prices)
                {
                    int minPrice = int.MaxValue, maxProfit = 0;
                    foreach (int price in prices)
                    {
                        maxProfit = Math.Max(maxProfit, price - minPrice);   // sell today at the best buy seen so far
                        minPrice = Math.Min(minPrice, price);
                    }
                    return maxProfit;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "prices = [7,1,5,3,6,4]", Expected = "5", Call = "sol.MaxProfit(new[] { 7, 1, 5, 3, 6, 4 })" },
                new() { Name = "Example 2", Input = "prices = [7,6,4,3,1]", Expected = "0", Call = "sol.MaxProfit(new[] { 7, 6, 4, 3, 1 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A single day", Input = "prices = [5]", Expected = "0", Call = "sol.MaxProfit(new[] { 5 })" },
                new() { Name = "The lowest price comes last", Input = "prices = [2,4,1]", Expected = "2", Call = "sol.MaxProfit(new[] { 2, 4, 1 })" },
                new() { Name = "A new low later on", Input = "prices = [3,2,6,5,0,3]", Expected = "4", Call = "sol.MaxProfit(new[] { 3, 2, 6, 5, 0, 3 })" },
                new() { Name = "Two days, rising", Input = "prices = [1,2]", Expected = "1", Call = "sol.MaxProfit(new[] { 1, 2 })" }
            },
            VisualizerKind = "Bars",
            VisualizationDescription = """
            Example 1's prices as bars. `buy` marks the cheapest day so far and `day` walks forward. The shaded band runs
            from the buy day to today, between the price paid (dashed line) and today's price, so its height is the
            profit of selling today. `minPrice`, `profit` and `maxProfit` update as you go.
            """,
            VisualizationCode = """
            var prices = new[] { 7, 1, 5, 3, 6, 4 };
            var tracker = VisualizerRecorder.CreateBars(prices, title: "121. Best Time to Buy and Sell Stock: remember the cheapest day");
            int buy = 0, minPrice = prices[0], profit = 0, maxProfit = 0;
            tracker.Watch(() => minPrice);
            tracker.Watch(() => profit);
            tracker.Watch(() => maxProfit);
            tracker.Step($"Day 0: the only price so far is {prices[0]}, so it is the cheapest", pointers: new { buy, day = 0 });

            for (int day = 1; day < prices.Length; day++)
            {
                profit = prices[day] - minPrice;
                if (prices[day] < minPrice)
                {
                    minPrice = prices[day];
                    buy = day;
                    tracker.Step($"Day {day}: {prices[day]} is cheaper than anything before, so it becomes the best day to buy", pointers: new { buy, day });
                    continue;
                }

                bool record = profit > maxProfit;
                maxProfit = Math.Max(maxProfit, profit);
                tracker.Step($"Day {day}: selling at {prices[day]} after buying at {minPrice} earns {profit}{(record ? ", the best so far" : "")}",
                    pointers: new { buy, day }, shade: new BarShade(buy, day, prices[day], $"profit {profit}", Floor: minPrice));
            }

            tracker.Step($"Done: the best trade earns {maxProfit}");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(min(n, alphabet))",
            DescriptionMarkdown = """
            Given a string `s`, return the length of the longest **substring** (a run of consecutive characters) in which no character appears twice.

            ### Example 1
            - **Input:** `s = "abcabcbb"`
            - **Output:** `3`
            - **Why:** `"abc"` has no repeats; every run of 4 consecutive characters repeats a letter.

            ### Example 2
            - **Input:** `s = "bbbbb"`
            - **Output:** `1`
            - **Why:** any two neighbours are both `b`, so the best is `"b"`.

            ### Example 3
            - **Input:** `s = "pwwkew"`
            - **Output:** `3`
            - **Why:** `"wke"` works. `"pwke"` has no repeats either, but it skips a character, so it is not a substring.

            ### Constraints
            - `0 <= s.length <= 5 * 10^4`
            - `s` consists of English letters, digits, symbols and spaces.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** find the longest window `s[left..right]` whose characters are all different.

            1. **Brute force:** from every start, extend until a character repeats. That is `O(n²)`, and after each repeat it starts over from the next position, re-reading characters it already knew were distinct.
            2. **Slide a window instead.** Grow the window one character at a time on the right. As long as the new character isn't already inside, the window stays valid and just gets longer.
            3. **When the new character repeats,** the window must lose the earlier copy, and everything before it. Rather than removing characters one by one, remember where each character was **last seen**: if `s[right]` was last seen at index `last` inside the window, jump `left` to `last + 1`.
            4. **Only copies inside the window count.** An old copy before `left` is no problem, so only jump when `last >= left` (otherwise `left` would move backwards). This is what makes `"abba"` come out as 2.
            5. **Walk `"tmmzuxt"`:** `t`, `m` grow the window; the second `m` jumps `left` to 2; `z`, `u`, `x` grow it to `"mzux"`; the final `t` was last seen at 0, before the window, so it just joins: `"mzuxt"`, length **5**.

            **Pattern to remember:** "longest window with a property" is expand-right, fix-left. A last-seen map lets `left` jump instead of crawl.

            **Common mistakes:** jumping `left` backwards for a stale copy; confusing substrings with subsequences; forgetting that the empty string gives 0.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: grow from every start",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(min(n, alphabet))",
                    Intuition = "For each start, add characters to a set until one is already there; the set size is the longest run from that start.",
                    BottleneckExplanation = "After a repeat it restarts one position later and re-reads the same characters, although the window only needed to lose its left part.",
                    Code = """
                    int LengthOfLongestSubstringBruteForce(string s)
                    {
                        int best = 0;
                        for (int start = 0; start < s.Length; start++)
                        {
                            var seen = new HashSet<char>();
                            int end = start;
                            while (end < s.Length && seen.Add(s[end])) end++;
                            best = Math.Max(best, end - start);
                        }
                        return best;
                    }

                    Show(LengthOfLongestSubstringBruteForce("abcabcbb"));   // 3
                    """
                },
                new()
                {
                    Name = "Sliding window with a set, shrinking one step at a time",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(min(n, alphabet))",
                    Intuition = "Keep the window's characters in a set. Before adding `s[right]`, remove characters from the left until it isn't in the set. Each character enters and leaves once, so it is linear.",
                    Code = """
                    int LengthWithShrinkingWindow(string s)
                    {
                        var window = new HashSet<char>();
                        int left = 0, best = 0;
                        for (int right = 0; right < s.Length; right++)
                        {
                            while (window.Contains(s[right])) window.Remove(s[left++]);
                            window.Add(s[right]);
                            best = Math.Max(best, right - left + 1);
                        }
                        return best;
                    }

                    Show(LengthWithShrinkingWindow("pwwkew"));   // 3
                    """
                },
                new()
                {
                    Name = "Sliding window that jumps with a last-seen map",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(min(n, alphabet))",
                    Intuition = "Remember the last index of every character. When `s[right]` was last seen inside the window, move `left` straight past that copy, then measure the window."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int LengthOfLongestSubstring(string s)
                {
                    var lastSeen = new Dictionary<char, int>();
                    int left = 0, best = 0;
                    for (int right = 0; right < s.Length; right++)
                    {
                        if (lastSeen.TryGetValue(s[right], out int last) && last >= left)
                            left = last + 1;                     // jump just past the copy inside the window
                        lastSeen[s[right]] = right;
                        best = Math.Max(best, right - left + 1);
                    }
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"abcabcbb\"", Expected = "3", Call = "sol.LengthOfLongestSubstring(\"abcabcbb\")" },
                new() { Name = "Example 2", Input = "s = \"bbbbb\"", Expected = "1", Call = "sol.LengthOfLongestSubstring(\"bbbbb\")" },
                new() { Name = "Example 3", Input = "s = \"pwwkew\"", Expected = "3", Call = "sol.LengthOfLongestSubstring(\"pwwkew\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Empty string", Input = "s = \"\"", Expected = "0", Call = "sol.LengthOfLongestSubstring(\"\")" },
                new() { Name = "A single space", Input = "s = \" \"", Expected = "1", Call = "sol.LengthOfLongestSubstring(\" \")" },
                new() { Name = "Repeat in the middle", Input = "s = \"dvdf\"", Expected = "3", Call = "sol.LengthOfLongestSubstring(\"dvdf\")" },
                new() { Name = "Stale copy before the window", Input = "s = \"abba\"", Expected = "2", Call = "sol.LengthOfLongestSubstring(\"abba\")" },
                new() { Name = "Old copy is ignored", Input = "s = \"tmmzuxt\"", Expected = "5", Call = "sol.LengthOfLongestSubstring(\"tmmzuxt\")" }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            The characters of `"tmmzuxt"`. The highlighted cells are the current window between `left` and `right`;
            `lastSeen` shows where each character last appeared. The second `m` makes `left` jump past the first one, and
            the final `t` is allowed in because its earlier copy is already outside the window.
            """,
            VisualizationCode = """
            var s = "tmmzuxt";
            var lastSeen = new Dictionary<char, int>();
            int left = 0, best = 0, bestLeft = 0;
            var tracker = VisualizerRecorder.CreateArray(s, title: "3. Longest Substring Without Repeating Characters: a window that jumps");
            tracker.Watch(lastSeen);
            tracker.Watch(() => best);

            for (int right = 0; right < s.Length; right++)
            {
                string why;
                if (lastSeen.TryGetValue(s[right], out int last) && last >= left)
                {
                    left = last + 1;
                    why = $"'{s[right]}' is already in the window at index {last}: jump left to {left}, just past it";
                }
                else if (lastSeen.ContainsKey(s[right]))
                {
                    why = $"'{s[right]}' was seen at index {last}, but that is before the window, so it doesn't count";
                }
                else
                {
                    why = $"'{s[right]}' is new, so the window grows";
                }

                lastSeen[s[right]] = right;
                int length = right - left + 1;
                bool record = length > best;
                if (record) (best, bestLeft) = (length, left);
                tracker.Step($"{why}. Window \"{s.Substring(left, length)}\" has length {length}{(record ? " (new best)" : "")}",
                    pointers: new { left, right }, highlight: Enumerable.Range(left, length));
            }

            tracker.Step($"Done: \"{s.Substring(bestLeft, best)}\" is the longest window without a repeat, length {best}", highlight: Enumerable.Range(bestLeft, best));
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(1) (at most 26 letters)",
            DescriptionMarkdown = """
            You are given a string `s` of uppercase English letters and an integer `k`. You may pick any character of `s` and change it to any other uppercase letter, at most `k` times in total.

            Return the length of the longest substring that can consist of **one repeated letter** after those changes.

            ### Example 1
            - **Input:** `s = "ABAB", k = 2`
            - **Output:** `4`
            - **Why:** change both `A`s to `B` (or both `B`s to `A`) to get `"BBBB"`.

            ### Example 2
            - **Input:** `s = "AABABBA", k = 1`
            - **Output:** `4`
            - **Why:** change the `A` at index 3 to `B`: `"AABBBBA"` contains `"BBBB"`. No window of 5 can be fixed with one change.

            ### Constraints
            - `1 <= s.length <= 10^5`
            - `s` consists of only uppercase English letters.
            - `0 <= k <= s.length`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** which window can become a single letter using at most `k` changes, and how long can it be?

            1. **Cost of fixing one window:** keep its most common letter and change everything else. So a window needs `length − (count of its most common letter)` changes, and it is fixable when that is `≤ k`.
            2. **Brute force:** check every window: `O(n²)` windows (counts can be updated as the end moves).
            3. **Slide a window:** extend `right` one letter at a time and update the letter counts. If the window now needs more than `k` changes, drop letters from the left until it doesn't. Every letter enters and leaves once.
            4. **Why shrinking is safe:** if `s[left..right]` needs too many changes, any longer window containing it does too, so `left` never has to go back.
            5. **Walk Example 2** (`k = 1`): `"AABA"` needs 1 change → length 4. Adding `B` gives `"AABAB"`: 5 − 3 = 2 changes, too many, so drop `A`s until `"BAB"` is fixable again. The best stays **4**.

            **Pattern to remember:** "longest window that is still OK" = expand right, shrink left while broken, and find a cheap formula for "broken" (here `length − maxCount > k`).

            **Going further:** the maximum count never needs to go down (a window can only beat the record when some letter's count beats the old maximum), which removes the recount. Recounting 26 letters is still linear, and easier to trust.

            **Common mistakes:** counting changes as "letters different from `s[left]`" instead of from the most common letter; forgetting to decrement counts when the left side moves.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: every start, grow while fixable",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(1)",
                    Intuition = "For each start, extend the end with running letter counts; the window is fixable while `length − top count ≤ k`.",
                    BottleneckExplanation = "The window starting one position later overlaps the previous one almost entirely, yet it is rebuilt from scratch.",
                    Code = """
                    int CharacterReplacementBruteForce(string s, int k)
                    {
                        int best = 0;
                        for (int start = 0; start < s.Length; start++)
                        {
                            var count = new int[26];
                            int top = 0;
                            for (int end = start; end < s.Length; end++)
                            {
                                top = Math.Max(top, ++count[s[end] - 'A']);
                                if (end - start + 1 - top <= k) best = Math.Max(best, end - start + 1);
                            }
                        }
                        return best;
                    }

                    Show(CharacterReplacementBruteForce("AABABBA", 1));   // 4
                    """
                },
                new()
                {
                    Name = "Sliding window: shrink while it needs more than k changes",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Count the letters in the window. It needs `length − most common count` changes; while that exceeds `k`, drop the leftmost letter. The largest fixable window is the answer."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int CharacterReplacement(string s, int k)
                {
                    var count = new Dictionary<char, int>();
                    int left = 0, best = 0;
                    for (int right = 0; right < s.Length; right++)
                    {
                        count[s[right]] = count.GetValueOrDefault(s[right]) + 1;

                        // Keep the most common letter and change the rest.
                        while (right - left + 1 - count.Values.Max() > k)
                        {
                            count[s[left]]--;
                            left++;
                        }
                        best = Math.Max(best, right - left + 1);
                    }
                    return best;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"ABAB\", k = 2", Expected = "4", Call = "sol.CharacterReplacement(\"ABAB\", 2)" },
                new() { Name = "Example 2", Input = "s = \"AABABBA\", k = 1", Expected = "4", Call = "sol.CharacterReplacement(\"AABABBA\", 1)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Already one letter", Input = "s = \"AAAA\", k = 0", Expected = "4", Call = "sol.CharacterReplacement(\"AAAA\", 0)" },
                new() { Name = "All different", Input = "s = \"ABCDE\", k = 1", Expected = "2", Call = "sol.CharacterReplacement(\"ABCDE\", 1)" },
                new() { Name = "No changes allowed", Input = "s = \"BAAAB\", k = 0", Expected = "3", Call = "sol.CharacterReplacement(\"BAAAB\", 0)" },
                new() { Name = "A single letter", Input = "s = \"A\", k = 0", Expected = "1", Call = "sol.CharacterReplacement(\"A\", 0)" },
                new() { Name = "k covers everything", Input = "s = \"ABCD\", k = 4", Expected = "4", Call = "sol.CharacterReplacement(\"ABCD\", 4)" }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 2, `"AABABBA"` with `k = 1`. The highlighted window grows to the right; `count` holds its letter counts
            and `changes` is how many letters differ from the most common one. When `changes` goes over `k`, letters drop
            off the left until the window is fixable again, and `best` keeps the longest fixable window.
            """,
            VisualizationCode = """
            var s = "AABABBA";
            int k = 1;
            var count = new Dictionary<char, int>();
            int left = 0, best = 0, bestLeft = 0, changes = 0;
            var tracker = VisualizerRecorder.CreateArray(s, title: "424. Longest Repeating Character Replacement: keep the top letter, change the rest");
            tracker.Watch(count);
            tracker.Watch(() => changes);
            tracker.Watch(() => best);

            for (int right = 0; right < s.Length; right++)
            {
                count[s[right]] = count.GetValueOrDefault(s[right]) + 1;
                changes = right - left + 1 - count.Values.Max();

                while (changes > k)
                {
                    tracker.Step($"\"{s.Substring(left, right - left + 1)}\" would need {changes} changes, more than k = {k}: drop '{s[left]}' from the left",
                        pointers: new { left, right }, highlight: Enumerable.Range(left, right - left + 1));
                    count[s[left]]--;
                    left++;
                    changes = right - left + 1 - count.Values.Max();
                }

                var top = count.MaxBy(pair => pair.Value);
                int length = right - left + 1;
                bool record = length > best;
                if (record) (best, bestLeft) = (length, left);
                tracker.Step($"\"{s.Substring(left, length)}\": keep {top.Key} ×{top.Value} and change {changes} (≤ k), length {length}{(record ? ", a new best" : "")}",
                    pointers: new { left, right }, highlight: Enumerable.Range(left, length));
            }

            tracker.Step($"Done: \"{s.Substring(bestLeft, best)}\" can become one letter with at most {k} change, length {best}", highlight: Enumerable.Range(bestLeft, best));
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(m + n)",
            SpaceComplexity = "O(alphabet)",
            DescriptionMarkdown = """
            Given two strings `s` (length `m`) and `t` (length `n`), return the **shortest substring** of `s` that contains every character of `t`, **including duplicates** (if `t` has two `a`s, the window needs at least two). If no substring works, return `""`.

            The tests are chosen so that the answer is unique.

            ### Example 1
            - **Input:** `s = "ADOBECODEBANC", t = "ABC"`
            - **Output:** `"BANC"`
            - **Why:** `"BANC"` contains an `A`, a `B` and a `C`. `"ADOBEC"` does too, but it is longer.

            ### Example 2
            - **Input:** `s = "a", t = "a"`
            - **Output:** `"a"`

            ### Example 3
            - **Input:** `s = "a", t = "aa"`
            - **Output:** `""`
            - **Why:** `t` needs two `a`s and `s` has only one.

            ### Constraints
            - `1 <= m, n <= 10^5`
            - `s` and `t` consist of uppercase and lowercase English letters.
            - **Follow-up:** can you do it in `O(m + n)`?
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** among all windows of `s` that "cover" `t` (have enough of each of its characters), return the shortest.

            1. **Brute force:** from every start, extend until the window covers `t`: `O(m²)`. But the end that works for one start can never be *before* the end for the previous start, so all that rescanning can be shared.
            2. **Two phases with two pointers:** move `right` to **expand** until the window covers `t`. Then move `left` to **shrink** while it still covers `t`, recording the shortest window each time. When it stops covering, expand again.
            3. **Knowing "covers" in O(1):** keep `need[c]`, how many more copies of `c` the window needs (start from `t`'s counts), and `missing`, the total still needed. Adding a needed character (`need[c] > 0`) lowers `missing`; `need[c]` going below 0 just means a spare copy. The window covers `t` exactly when `missing == 0`.
            4. **Shrinking:** dropping `c` raises `need[c]`; if it becomes positive, the window just lost a copy it needed, so `missing` goes back up and we expand again.
            5. **Walk Example 1:** expanding to `"ADOBEC"` covers `t` (record 6). Dropping `A` breaks it; expand to `"DOBECODEBA"`, then shrink to `"CODEBA"`; expanding to the final `C` lets it shrink all the way to `"BANC"` (length 4).

            **Pattern to remember:** "shortest window that satisfies P" = expand until P holds, then shrink while P holds. A counter like `missing` turns "does P hold?" into an O(1) check.

            **Common mistakes:** ignoring duplicate letters in `t`; comparing whole count maps at every step; counting characters that aren't in `t`; returning a window when `t` was never covered.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Brute force: shortest covering window from every start",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(m²)",
                    SpaceComplexity = "O(alphabet)",
                    Intuition = "For each start, extend the end while tracking how many of `t`'s characters are still missing; the first covering end gives the shortest window from that start.",
                    BottleneckExplanation = "Each start rescans the characters after it from scratch, even though the covering end only ever moves right.",
                    Code = """
                    string MinWindowBruteForce(string s, string t)
                    {
                        string best = "";
                        for (int start = 0; start < s.Length; start++)
                        {
                            var need = t.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
                            int missing = t.Length;
                            for (int end = start; end < s.Length && missing > 0; end++)
                            {
                                if (need.TryGetValue(s[end], out int count))
                                {
                                    if (count > 0) missing--;
                                    need[s[end]] = count - 1;
                                }
                                if (missing == 0 && (best == "" || end - start + 1 < best.Length)) best = s.Substring(start, end - start + 1);
                            }
                        }
                        return best;
                    }

                    Show(MinWindowBruteForce("ADOBECODEBANC", "ABC"));   // "BANC"
                    """
                },
                new()
                {
                    Name = "Sliding window: expand until covered, shrink while covered",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m + n)",
                    SpaceComplexity = "O(alphabet)",
                    Intuition = "`need` counts what the window still lacks and `missing` totals it. Expand `right` until `missing` is 0, then advance `left` as far as the window stays covering, keeping the shortest. Each pointer moves at most `m` times."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public string MinWindow(string s, string t)
                {
                    var need = new Dictionary<char, int>();
                    foreach (char c in t) need[c] = need.GetValueOrDefault(c) + 1;
                    int missing = t.Length, left = 0, bestStart = 0, bestLength = int.MaxValue;

                    for (int right = 0; right < s.Length; right++)
                    {
                        if (need.TryGetValue(s[right], out int count))
                        {
                            if (count > 0) missing--;           // a copy the window still needed
                            need[s[right]] = count - 1;         // below 0 means a spare copy
                        }

                        while (missing == 0)                    // covers t: record it and try a shorter one
                        {
                            if (right - left + 1 < bestLength) (bestStart, bestLength) = (left, right - left + 1);
                            char drop = s[left++];
                            if (need.TryGetValue(drop, out int dropCount))
                            {
                                need[drop] = dropCount + 1;
                                if (dropCount + 1 > 0) missing++;   // dropped a copy t needs
                            }
                        }
                    }
                    return bestLength == int.MaxValue ? "" : s.Substring(bestStart, bestLength);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"ADOBECODEBANC\", t = \"ABC\"", Expected = "\"BANC\"", Call = "sol.MinWindow(\"ADOBECODEBANC\", \"ABC\")" },
                new() { Name = "Example 2", Input = "s = \"a\", t = \"a\"", Expected = "\"a\"", Call = "sol.MinWindow(\"a\", \"a\")" },
                new() { Name = "Example 3", Input = "s = \"a\", t = \"aa\"", Expected = "\"\"", Call = "sol.MinWindow(\"a\", \"aa\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Answer at the end", Input = "s = \"ab\", t = \"b\"", Expected = "\"b\"", Call = "sol.MinWindow(\"ab\", \"b\")" },
                new() { Name = "Needs both copies", Input = "s = \"aa\", t = \"aa\"", Expected = "\"aa\"", Call = "sol.MinWindow(\"aa\", \"aa\")" },
                new() { Name = "Skip a spare copy", Input = "s = \"bba\", t = \"ab\"", Expected = "\"ba\"", Call = "sol.MinWindow(\"bba\", \"ab\")" },
                new() { Name = "Whole string", Input = "s = \"abc\", t = \"cba\"", Expected = "\"abc\"", Call = "sol.MinWindow(\"abc\", \"cba\")" },
                new() { Name = "Character not in s", Input = "s = \"a\", t = \"b\"", Expected = "\"\"", Call = "sol.MinWindow(\"a\", \"b\")" }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            Example 1. The highlighted window expands with `right` until `missing` reaches 0 (it holds an A, a B and a C),
            then shrinks with `left` while it still does. `need` shows how many more of each letter the window needs (a
            negative number is a spare copy) and `best` is the shortest covering window so far.
            """,
            VisualizationCode = """
            var s = "ADOBECODEBANC";
            var t = "ABC";
            var need = new Dictionary<char, int>();
            foreach (char c in t) need[c] = need.GetValueOrDefault(c) + 1;
            int missing = t.Length, left = 0, bestStart = 0, bestLength = int.MaxValue;
            string best = "";
            var tracker = VisualizerRecorder.CreateArray(s, title: "76. Minimum Window Substring: expand until covered, then shrink");
            tracker.Watch(need);
            tracker.Watch(() => missing);
            tracker.Watch(() => best);

            for (int right = 0; right < s.Length; right++)
            {
                string added;
                if (need.TryGetValue(s[right], out int count))
                {
                    if (count > 0) missing--;
                    need[s[right]] = count - 1;
                    added = count <= 0 ? $"'{s[right]}' is a spare copy, nothing new"
                        : missing == 0 ? $"'{s[right]}' was the last letter t needed, so the window covers t"
                        : $"'{s[right]}' was needed, {missing} still missing";
                }
                else
                {
                    added = $"'{s[right]}' is not in t";
                }
                tracker.Step($"Expand: {added}", pointers: new { left, right }, highlight: Enumerable.Range(left, right - left + 1));

                while (missing == 0)
                {
                    int length = right - left + 1;
                    if (length < bestLength)
                    {
                        (bestStart, bestLength) = (left, length);
                        best = s.Substring(bestStart, bestLength);
                        tracker.Step($"\"{best}\" covers t and is the shortest so far ({length} letters): record it, then shrink from the left",
                            pointers: new { left, right }, highlight: Enumerable.Range(left, length));
                    }
                    else
                    {
                        tracker.Step($"\"{s.Substring(left, length)}\" covers t, but {length} letters is no shorter than \"{best}\": keep shrinking",
                            pointers: new { left, right }, highlight: Enumerable.Range(left, length));
                    }

                    char drop = s[left++];
                    if (need.TryGetValue(drop, out int dropCount))
                    {
                        need[drop] = dropCount + 1;
                        if (dropCount + 1 > 0) missing++;
                    }
                    if (missing > 0)
                    {
                        tracker.Step($"Dropping '{drop}' loses a letter t needs, so the window must expand again",
                            pointers: new { left, right }, highlight: Enumerable.Range(left, right - left + 1));
                    }
                }
            }

            tracker.Step(best == "" ? "Done: no window covers t" : $"Done: the shortest window is \"{best}\"", highlight: Enumerable.Range(bestStart, best.Length));
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            Given a string `s` made only of the characters `(`, `)`, `{`, `}`, `[` and `]`, decide whether it is **valid**:

            - every opening bracket is closed by a bracket of the **same type**,
            - brackets close in the **right order** (the one opened most recently closes first), and
            - every closing bracket has a matching opening bracket before it.

            ### Example 1
            - **Input:** `s = "()"`
            - **Output:** `true`

            ### Example 2
            - **Input:** `s = "()[]{}"`
            - **Output:** `true`

            ### Example 3
            - **Input:** `s = "(]"`
            - **Output:** `false`
            - **Why:** `(` is closed by `]`, a different type.

            ### Example 4
            - **Input:** `s = "([])"`
            - **Output:** `true`
            - **Why:** `[` was opened last, so it closes first; then `)` closes the `(`.

            ### Constraints
            - `1 <= s.length <= 10^4`
            - `s` consists of the characters `()[]{}` only.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** does every closing bracket close the most recently opened bracket that is still open, with nothing left open at the end?

            1. **Spot the order:** in `"{[()]}"` the brackets close in the reverse of the order they opened. "Last opened, first closed" is exactly what a **stack** does.
            2. **Scan once.** For an opening bracket, push the closer you will need later (`)` for `(`, `]` for `[`, `}` for `{`). For a closing bracket, it must equal the top of the stack: pop it. An empty stack or a different top means `false` right away.
            3. **At the end** the stack must be empty; otherwise something was opened and never closed, like `"(("`.
            4. **Walk `"([)]"`:** push `)`, push `]`; then `)` arrives but the top is `]`, so it is invalid: the `[` must close before the `(`.
            5. **Why pushing the closer helps:** comparing `c == expected.Pop()` is one check, instead of a lookup from closer back to opener.

            **Pattern to remember:** nested structure (brackets, tags, function calls, undo) → a stack of what's still open.

            **Common mistakes:** only counting brackets (`"([)]"` has balanced counts but is invalid); popping from an empty stack; forgetting the final "is the stack empty?" check.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Keep deleting adjacent pairs",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n²)",
                    SpaceComplexity = "O(n)",
                    Intuition = "A valid string always contains an adjacent pair like `()`, `[]` or `{}`. Delete pairs until nothing changes; the string was valid if nothing is left.",
                    BottleneckExplanation = "Each pass rescans the whole string and a deeply nested input like `((((…))))` loses only one pair per pass.",
                    Code = """
                    bool IsValidByRemovingPairs(string s)
                    {
                        string previous;
                        do
                        {
                            previous = s;
                            s = s.Replace("()", "").Replace("[]", "").Replace("{}", "");
                        } while (s != previous);
                        return s.Length == 0;
                    }

                    Show(IsValidByRemovingPairs("{[()()]}"));   // true
                    """
                },
                new()
                {
                    Name = "Stack of the closers still expected",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Push the matching closer for every opener. Each closer must equal the popped top; at the end the stack must be empty."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool IsValid(string s)
                {
                    var expected = new Stack<char>();
                    foreach (char c in s)
                    {
                        if (c == '(') expected.Push(')');
                        else if (c == '[') expected.Push(']');
                        else if (c == '{') expected.Push('}');
                        else if (expected.Count == 0 || expected.Pop() != c) return false;   // nothing open, or the wrong type
                    }
                    return expected.Count == 0;                                          // anything left was never closed
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "s = \"()\"", Expected = "true", Call = "sol.IsValid(\"()\")" },
                new() { Name = "Example 2", Input = "s = \"()[]{}\"", Expected = "true", Call = "sol.IsValid(\"()[]{}\")" },
                new() { Name = "Example 3", Input = "s = \"(]\"", Expected = "false", Call = "sol.IsValid(\"(]\")" },
                new() { Name = "Example 4", Input = "s = \"([])\"", Expected = "true", Call = "sol.IsValid(\"([])\")" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Only an opener", Input = "s = \"(\"", Expected = "false", Call = "sol.IsValid(\"(\")" },
                new() { Name = "Closer first", Input = "s = \")(\"", Expected = "false", Call = "sol.IsValid(\")(\")" },
                new() { Name = "Crossed pairs", Input = "s = \"([)]\"", Expected = "false", Call = "sol.IsValid(\"([)]\")" },
                new() { Name = "Never closed", Input = "s = \"((\"", Expected = "false", Call = "sol.IsValid(\"((\")" },
                new() { Name = "Deep nesting", Input = "s = \"{[({})]}\"", Expected = "true", Call = "sol.IsValid(\"{[({})]}\")" }
            },
            VisualizerKind = "ArrayPointers",
            VisualizationDescription = """
            The brackets of `"{[()()]}"`. `i` scans left to right; `expected` is the stack of closers still owed, with the
            top on the left. Each closer is checked against the top of the stack, and a matched pair turns green. The
            stack is empty at the end, so the string is valid.
            """,
            VisualizationCode = """
            var s = "{[()()]}";
            var expected = new Stack<char>();
            var openedAt = new Stack<int>();   // only to color each matched pair
            var tracker = VisualizerRecorder.CreateArray(s, title: "20. Valid Parentheses: a stack of the closers still owed");
            var cells = tracker.Options.ArrayData!.Items;
            tracker.Watch(expected);

            bool valid = true;
            for (int i = 0; i < s.Length && valid; i++)
            {
                char c = s[i];
                if (c == '(' || c == '[' || c == '{')
                {
                    char closer = c == '(' ? ')' : c == '[' ? ']' : '}';
                    expected.Push(closer);
                    openedAt.Push(i);
                    tracker.Step($"'{c}' opens a bracket: push the '{closer}' that must close it later", pointers: new { i });
                }
                else if (expected.Count == 0 || expected.Peek() != c)
                {
                    valid = false;
                    tracker.Step(expected.Count == 0 ? $"'{c}' has nothing to close: invalid" : $"'{c}' arrives but '{expected.Peek()}' is owed first: invalid", pointers: new { i }, highlight: new[] { i });
                }
                else
                {
                    expected.Pop();
                    int open = openedAt.Pop();
                    cells[open].Color = cells[i].Color = "#14532d";
                    tracker.Step($"'{c}' matches the top of the stack: pop it. Brackets {open} and {i} are a pair", pointers: new { i }, highlight: new[] { open, i });
                }
            }

            valid = valid && expected.Count == 0;
            tracker.Step(valid ? "The stack is empty: every bracket was closed in order, so the answer is true" : "Answer: false");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(log n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            An array of **distinct** numbers sorted in ascending order has been **rotated** between 1 and `n` times. Rotating once moves the last element to the front, so `[0,1,2,4,5,6,7]` rotated 4 times is `[4,5,6,7,0,1,2]`.

            Given the rotated array `nums`, return its **minimum** element. Your algorithm must run in `O(log n)` time.

            ### Example 1
            - **Input:** `nums = [3,4,5,1,2]`
            - **Output:** `1`
            - **Why:** it is `[1,2,3,4,5]` rotated 3 times.

            ### Example 2
            - **Input:** `nums = [4,5,6,7,0,1,2]`
            - **Output:** `0`

            ### Example 3
            - **Input:** `nums = [11,13,15,17]`
            - **Output:** `11`
            - **Why:** it was rotated 4 times, all the way around, so it is sorted again.

            ### Constraints
            - `1 <= n <= 5000`
            - `-5000 <= nums[i] <= 5000`
            - All the numbers are unique, and `nums` is a sorted array rotated between 1 and `n` times.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** a rotated sorted array is two ascending runs, like `[4,5,6,7]` then `[0,1,2]`, and the minimum is where the second run starts (the "drop"). Find it in `O(log n)`.

            1. **A scan finds the drop in `O(n)`,** but `O(log n)` means binary search: each comparison must rule out half the array.
            2. **Compare the middle with the last element.** Every value in the left run is bigger than every value in the right run.
               - `nums[mid] > nums[hi]`: `mid` is in the bigger left run, so the drop is somewhere **after** `mid`: `lo = mid + 1`.
               - `nums[mid] < nums[hi]`: `mid..hi` is sorted, so the minimum is `mid` itself or **before** it: `hi = mid` (keep `mid`, it might be the answer).
            3. **Stop when `lo == hi`:** one candidate is left, and it is the minimum.
            4. **Why `nums[hi]` and not `nums[lo]`?** When the range happens to be sorted, `nums[mid] < nums[hi]` still correctly heads left, while comparing with `nums[lo]` cannot tell "already sorted" apart from "drop on the right".
            5. **Walk Example 2:** `mid = 3` (7 > 2) → `lo = 4`. `mid = 5` (1 < 2) → `hi = 5`. `mid = 4` (0 < 1) → `hi = 4`. Now `lo == hi == 4`: the minimum is **0**.

            **Pattern to remember:** binary search works on any array where one comparison tells you which half the answer is in, not only on fully sorted arrays.

            **Common mistakes:** `hi = mid - 1` in the second case (it can skip the minimum); looping `while (lo <= hi)` with `hi = mid` (it never ends); comparing with `nums[lo]`.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Scan for the drop",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Walk the array until a number is smaller than the one before it: that is the minimum. No drop means the array is fully sorted and `nums[0]` wins.",
                    BottleneckExplanation = "It reads elements one by one, although both runs are sorted and one comparison could discard half of them.",
                    Code = """
                    int FindMinByScanning(int[] nums)
                    {
                        for (int i = 1; i < nums.Length; i++)
                            if (nums[i] < nums[i - 1]) return nums[i];   // the drop is the minimum
                        return nums[0];                                  // no drop: rotated all the way round
                    }

                    Show(FindMinByScanning(new[] { 4, 5, 6, 7, 0, 1, 2 }));   // 0
                    """
                },
                new()
                {
                    Name = "Binary search against the last element",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(log n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "If `nums[mid] > nums[hi]` the drop is to the right of `mid`; otherwise the minimum is `mid` or to its left. Halve the range until one element is left."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int FindMin(int[] nums)
                {
                    int lo = 0, hi = nums.Length - 1;
                    while (lo < hi)
                    {
                        int mid = lo + (hi - lo) / 2;
                        if (nums[mid] > nums[hi]) lo = mid + 1;   // mid is in the bigger left run: the drop is after it
                        else hi = mid;                            // mid..hi is sorted: the minimum is mid or before it
                    }
                    return nums[lo];
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [3,4,5,1,2]", Expected = "1", Call = "sol.FindMin(new[] { 3, 4, 5, 1, 2 })" },
                new() { Name = "Example 2", Input = "nums = [4,5,6,7,0,1,2]", Expected = "0", Call = "sol.FindMin(new[] { 4, 5, 6, 7, 0, 1, 2 })" },
                new() { Name = "Example 3", Input = "nums = [11,13,15,17]", Expected = "11", Call = "sol.FindMin(new[] { 11, 13, 15, 17 })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Two elements", Input = "nums = [2,1]", Expected = "1", Call = "sol.FindMin(new[] { 2, 1 })" },
                new() { Name = "One element", Input = "nums = [1]", Expected = "1", Call = "sol.FindMin(new[] { 1 })" },
                new() { Name = "Drop right after the start", Input = "nums = [5,1,2,3,4]", Expected = "1", Call = "sol.FindMin(new[] { 5, 1, 2, 3, 4 })" },
                new() { Name = "Drop at the very end", Input = "nums = [2,3,4,5,1]", Expected = "1", Call = "sol.FindMin(new[] { 2, 3, 4, 5, 1 })" },
                new() { Name = "Negative numbers", Input = "nums = [-1,-5,-3]", Expected = "-5", Call = "sol.FindMin(new[] { -1, -5, -3 })" }
            },
            VisualizerKind = "Bars",
            VisualizationDescription = """
            Example 2 as bars: you can see the two ascending runs and the drop between 7 and 0. Each step compares
            `nums[mid]` with `nums[hi]` and greys out the half that can't hold the minimum, until `lo` and `hi` meet on it.
            """,
            VisualizationCode = """
            var nums = new[] { 4, 5, 6, 7, 0, 1, 2 };
            var tracker = VisualizerRecorder.CreateBars(nums, title: "153. Find Minimum in Rotated Sorted Array: which side is the drop on?");
            var bars = tracker.Options.BarData!.Items;

            int lo = 0, hi = nums.Length - 1;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (nums[mid] > nums[hi])
                {
                    tracker.Step($"nums[mid] = {nums[mid]} > nums[hi] = {nums[hi]}: mid is in the higher left run, so the drop is after it. Rule out {lo}..{mid}",
                        pointers: new { lo, mid, hi }, highlight: new[] { mid, hi });
                    for (int i = lo; i <= mid; i++) bars[i].ColorHex = "#374151";
                    lo = mid + 1;
                }
                else
                {
                    tracker.Step($"nums[mid] = {nums[mid]} < nums[hi] = {nums[hi]}: mid..hi is sorted, so the minimum is mid or before it. Rule out {mid + 1}..{hi}",
                        pointers: new { lo, mid, hi }, highlight: new[] { mid, hi });
                    for (int i = mid + 1; i <= hi; i++) bars[i].ColorHex = "#374151";
                    hi = mid;
                }
            }

            bars[lo].ColorHex = "#22c55e";
            tracker.Step($"lo and hi meet at index {lo}: the minimum is {nums[lo]}", pointers: new { lo, hi });
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(log n)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            An ascending array of **distinct** numbers may have been **rotated** at an unknown index `k`, becoming `[nums[k], …, nums[n-1], nums[0], …, nums[k-1]]`. For example `[0,1,2,4,5,6,7]` rotated at index 3 becomes `[4,5,6,7,0,1,2]`.

            Given the rotated array `nums` and a `target`, return the index of `target`, or `-1` if it isn't there. Your algorithm must run in `O(log n)` time.

            ### Example 1
            - **Input:** `nums = [4,5,6,7,0,1,2], target = 0`
            - **Output:** `4`

            ### Example 2
            - **Input:** `nums = [4,5,6,7,0,1,2], target = 3`
            - **Output:** `-1`

            ### Example 3
            - **Input:** `nums = [1], target = 0`
            - **Output:** `-1`

            ### Constraints
            - `1 <= nums.length <= 5000`
            - `-10^4 <= nums[i], target <= 10^4`
            - All values are unique, and `nums` is an ascending array that is possibly rotated.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** binary search for `target`, except the array is sorted "with a twist".

            1. **Scanning is `O(n)`;** we need `O(log n)`, so each step must throw away half the range.
            2. **The key fact:** split a rotated sorted array at any `mid` and **at least one half is sorted**. If `nums[lo] <= nums[mid]`, the left half `lo..mid` is sorted; otherwise the right half `mid..hi` is.
            3. **Use the sorted half for a range check.** A sorted half contains `target` exactly when `target` lies between its two ends:
               - left sorted and `nums[lo] <= target < nums[mid]` → search left (`hi = mid - 1`), else search right;
               - right sorted and `nums[mid] < target <= nums[hi]` → search right (`lo = mid + 1`), else search left.
            4. Each step halves the range, so it is still `O(log n)`.
            5. **Walk Example 1** (target 0): `mid = 3` (7). The left half `4..7` is sorted and 0 isn't in it → go right. `mid = 5` (1): the left half `0..1` is sorted and holds 0 → go left. `mid = 4`: found at index **4**.

            **Another way:** find the rotation point first (that is problem 153), then run an ordinary binary search on the correct run: two `O(log n)` searches.

            **Common mistakes:** `<` vs `<=` in `nums[lo] <= nums[mid]` (with `lo == mid` the one-element left half *is* sorted); checking the unsorted half's range, which tells you nothing.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Linear scan",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O(n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Check every index until the target turns up.",
                    BottleneckExplanation = "It ignores that both runs are sorted, which is enough to discard half the array per comparison.",
                    Code = """
                    int SearchByScanning(int[] nums, int target) => Array.IndexOf(nums, target);

                    Show(SearchByScanning(new[] { 4, 5, 6, 7, 0, 1, 2 }, 0));   // 4
                    """
                },
                new()
                {
                    Name = "Find the rotation point, then binary search",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(log n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Locate the minimum with problem 153's search. Its index `pivot` tells where sorted position `i` really lives: `(i + pivot) % n`, so an ordinary binary search works on those positions.",
                    Code = """
                    int SearchWithPivot(int[] nums, int target)
                    {
                        int lo = 0, hi = nums.Length - 1;
                        while (lo < hi)                                  // 153: the index of the minimum
                        {
                            int mid = (lo + hi) / 2;
                            if (nums[mid] > nums[hi]) lo = mid + 1; else hi = mid;
                        }

                        int pivot = lo, n = nums.Length;
                        lo = 0;
                        hi = n - 1;
                        while (lo <= hi)                                 // binary search on the unrotated order
                        {
                            int mid = (lo + hi) / 2, real = (mid + pivot) % n;
                            if (nums[real] == target) return real;
                            if (nums[real] < target) lo = mid + 1; else hi = mid - 1;
                        }
                        return -1;
                    }

                    Show(SearchWithPivot(new[] { 4, 5, 6, 7, 0, 1, 2 }, 0));   // 4
                    """
                },
                new()
                {
                    Name = "One binary search: range-check the sorted half",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(log n)",
                    SpaceComplexity = "O(1)",
                    Intuition = "At every `mid`, one half is sorted. If the target lies within that half's two ends, search it; otherwise search the other half."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int Search(int[] nums, int target)
                {
                    int lo = 0, hi = nums.Length - 1;
                    while (lo <= hi)
                    {
                        int mid = lo + (hi - lo) / 2;
                        if (nums[mid] == target) return mid;

                        if (nums[lo] <= nums[mid])                            // the left half is sorted
                        {
                            if (nums[lo] <= target && target < nums[mid]) hi = mid - 1;
                            else lo = mid + 1;
                        }
                        else                                                 // the right half is sorted
                        {
                            if (nums[mid] < target && target <= nums[hi]) lo = mid + 1;
                            else hi = mid - 1;
                        }
                    }
                    return -1;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "nums = [4,5,6,7,0,1,2], target = 0", Expected = "4", Call = "sol.Search(new[] { 4, 5, 6, 7, 0, 1, 2 }, 0)" },
                new() { Name = "Example 2", Input = "nums = [4,5,6,7,0,1,2], target = 3", Expected = "-1", Call = "sol.Search(new[] { 4, 5, 6, 7, 0, 1, 2 }, 3)" },
                new() { Name = "Example 3", Input = "nums = [1], target = 0", Expected = "-1", Call = "sol.Search(new[] { 1 }, 0)" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Single element found", Input = "nums = [1], target = 1", Expected = "0", Call = "sol.Search(new[] { 1 }, 1)" },
                new() { Name = "Not rotated", Input = "nums = [1,3], target = 3", Expected = "1", Call = "sol.Search(new[] { 1, 3 }, 3)" },
                new() { Name = "Two elements, rotated", Input = "nums = [3,1], target = 1", Expected = "1", Call = "sol.Search(new[] { 3, 1 }, 1)" },
                new() { Name = "Target is the first element", Input = "nums = [5,1,3], target = 5", Expected = "0", Call = "sol.Search(new[] { 5, 1, 3 }, 5)" },
                new() { Name = "Target is the largest", Input = "nums = [4,5,6,7,8,1,2,3], target = 8", Expected = "4", Call = "sol.Search(new[] { 4, 5, 6, 7, 8, 1, 2, 3 }, 8)" }
            },
            VisualizerKind = "Bars",
            VisualizationDescription = """
            Example 1 as bars, searching for 0. At each `mid` the shaded band marks the half that is sorted and spans its
            values from one end to the other, so "is the target inside?" is just "is 0 inside the band?". The half that
            can't hold the target turns grey, until `mid` lands on it.
            """,
            VisualizationCode = """
            var nums = new[] { 4, 5, 6, 7, 0, 1, 2 };
            int target = 0;
            var tracker = VisualizerRecorder.CreateBars(nums, title: "33. Search in Rotated Sorted Array: one half is always sorted");
            var bars = tracker.Options.BarData!.Items;
            tracker.Watch(() => target);

            int lo = 0, hi = nums.Length - 1, found = -1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (nums[mid] == target)
                {
                    found = mid;
                    break;
                }

                if (nums[lo] <= nums[mid])
                {
                    bool inside = nums[lo] <= target && target < nums[mid];
                    tracker.Step($"nums[lo] = {nums[lo]} ≤ nums[mid] = {nums[mid]}: the left half is sorted. {target} is {(inside ? "" : "not ")}between {nums[lo]} and {nums[mid]}, so search the {(inside ? "left" : "right")} half",
                        pointers: new { lo, mid, hi }, shade: new BarShade(lo, mid, nums[mid], $"sorted {nums[lo]}..{nums[mid]}", Floor: nums[lo]));
                    int from = inside ? mid : lo, to = inside ? hi : mid;
                    for (int i = from; i <= to; i++) bars[i].ColorHex = "#374151";
                    if (inside) hi = mid - 1; else lo = mid + 1;
                }
                else
                {
                    bool inside = nums[mid] < target && target <= nums[hi];
                    tracker.Step($"nums[lo] = {nums[lo]} > nums[mid] = {nums[mid]}: the right half is sorted. {target} is {(inside ? "" : "not ")}between {nums[mid]} and {nums[hi]}, so search the {(inside ? "right" : "left")} half",
                        pointers: new { lo, mid, hi }, shade: new BarShade(mid, hi, nums[hi], $"sorted {nums[mid]}..{nums[hi]}", Floor: nums[mid]));
                    int from = inside ? lo : mid, to = inside ? mid : hi;
                    for (int i = from; i <= to; i++) bars[i].ColorHex = "#374151";
                    if (inside) lo = mid + 1; else hi = mid - 1;
                }
            }

            if (found >= 0) bars[found].ColorHex = "#22c55e";
            tracker.Step(found >= 0 ? $"nums[{found}] = {target}: found it at index {found}" : $"The range is empty: {target} is not in the array, answer -1", pointers: found >= 0 ? new { mid = found } : null);
            Display.Visualizer(tracker);
            """
        },
    };
}
