using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetIntervalsAndMathProblems()
    {
        var list = new List<BlindProblemItem>(GetIntervalProblems());
        list.AddRange(GetMathAndBitProblems());
        return list;
    }

    private static IEnumerable<BlindProblemItem> GetMathAndBitProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_48_rotate_image",
            Number = 48,
            Title = "Rotate Image",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 75.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Math", "Matrix" },
            TimeComplexity = "O(N^2)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            You are given an `n x n` 2D `matrix` representing an image, rotate the image by **90 degrees (clockwise)** in-place.
            """,
            StarterCode = """
            public class Solution 
            {
                public void Rotate(int[][] matrix) 
                {
                    int n = matrix.Length;
                    for (int i = 0; i < n; i++)
                        for (int j = i + 1; j < n; j++)
                            (matrix[i][j], matrix[j][i]) = (matrix[j][i], matrix[i][j]);

                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n / 2; j++)
                            (matrix[i][j], matrix[i][n - 1 - j]) = (matrix[i][n - 1 - j], matrix[i][j]);
                }
            }

            var sol = new Solution();
            var m = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } };
            sol.Rotate(m);
            m.Dump("Rotated 90 Deg Clockwise");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3x3", Input = "[[1,2,3],[4,5,6],[7,8,9]]", ExpectedOutput = "[[7,4,1],[8,5,2],[9,6,3]]" }
            }
        },
        new()
        {
            Id = "blind75_54_spiral_matrix",
            Number = 54,
            Title = "Spiral Matrix",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 51.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Matrix", "Simulation" },
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an `m x n` `matrix`, return *all elements of the* `matrix` *in spiral order*.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public IList<int> SpiralOrder(int[][] matrix) 
                {
                    var res = new List<int>();
                    int top = 0, bottom = matrix.Length - 1;
                    int left = 0, right = matrix[0].Length - 1;

                    while (top <= bottom && left <= right)
                    {
                        for (int c = left; c <= right; c++) res.Add(matrix[top][c]);
                        top++;
                        for (int r = top; r <= bottom; r++) res.Add(matrix[r][right]);
                        right--;
                        if (top <= bottom)
                        {
                            for (int c = right; c >= left; c--) res.Add(matrix[bottom][c]);
                            bottom--;
                        }
                        if (left <= right)
                        {
                            for (int r = bottom; r >= top; r--) res.Add(matrix[r][left]);
                            left++;
                        }
                    }
                    return res;
                }
            }

            var sol = new Solution();
            var m = new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new[] { 7, 8, 9 } };
            sol.SpiralOrder(m).Dump("Spiral Order");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3x3", Input = "[[1,2,3],[4,5,6],[7,8,9]]", ExpectedOutput = "[1,2,3,6,9,8,7,4,5]" }
            }
        },
        new()
        {
            Id = "blind75_73_set_matrix_zeroes",
            Number = 73,
            Title = "Set Matrix Zeroes",
            Category = "Math & Geometry",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 57.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Matrix" },
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an `m x n` integer matrix `matrix`, if an element is `0`, set its entire row and column to `0`'s in place.
            """,
            StarterCode = """
            public class Solution 
            {
                public void SetZeroes(int[][] matrix) 
                {
                    int m = matrix.Length, n = matrix[0].Length;
                    bool firstRowZero = false, firstColZero = false;

                    for (int r = 0; r < m; r++) if (matrix[r][0] == 0) firstColZero = true;
                    for (int c = 0; c < n; c++) if (matrix[0][c] == 0) firstRowZero = true;

                    for (int r = 1; r < m; r++)
                        for (int c = 1; c < n; c++)
                            if (matrix[r][c] == 0) { matrix[r][0] = 0; matrix[0][c] = 0; }

                    for (int r = 1; r < m; r++)
                        for (int c = 1; c < n; c++)
                            if (matrix[r][0] == 0 || matrix[0][c] == 0) matrix[r][c] = 0;

                    if (firstColZero) for (int r = 0; r < m; r++) matrix[r][0] = 0;
                    if (firstRowZero) for (int c = 0; c < n; c++) matrix[0][c] = 0;
                }
            }

            var sol = new Solution();
            var m = new[] { new[] { 1, 1, 1 }, new[] { 1, 0, 1 }, new[] { 1, 1, 1 } };
            sol.SetZeroes(m);
            m.Dump("Set Matrix Zeroes");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Center zero", Input = "[[1,1,1],[1,0,1],[1,1,1]]", ExpectedOutput = "[[1,0,1],[0,0,0],[1,0,1]]" }
            }
        },
        new()
        {
            Id = "blind75_371_sum_of_two_integers",
            Number = 371,
            Title = "Sum of Two Integers",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 53.5,
            IsPremium = false,
            Tags = new List<string> { "Math", "Bit Manipulation" },
            TimeComplexity = "O(1)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given two integers `a` and `b`, return *the sum of the two integers without using the operators* `+` and `-`.
            """,
            StarterCode = """
            public class Solution 
            {
                public int GetSum(int a, int b) 
                {
                    while (b != 0)
                    {
                        int carry = (a & b) << 1;
                        a = a ^ b;
                        b = carry;
                    }
                    return a;
                }
            }

            var sol = new Solution();
            sol.GetSum(1, 2).Dump("GetSum 1 + 2 (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "1 and 2", Input = "1, 2", ExpectedOutput = "3" }
            }
        },
        new()
        {
            Id = "blind75_191_number_of_1_bits",
            Number = 191,
            Title = "Number of 1 Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 72.5,
            IsPremium = false,
            Tags = new List<string> { "Divide and Conquer", "Bit Manipulation" },
            TimeComplexity = "O(1)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Write a function that takes the binary representation of a positive integer and returns the number of set bits it has (also known as the Hamming weight).
            """,
            StarterCode = """
            public class Solution 
            {
                public int HammingWeight(int n) 
                {
                    int count = 0;
                    while (n != 0)
                    {
                        n &= (n - 1);
                        count++;
                    }
                    return count;
                }
            }

            var sol = new Solution();
            sol.HammingWeight(11).Dump("HammingWeight 11 (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "11 (1011)", Input = "11", ExpectedOutput = "3" }
            }
        },
        new()
        {
            Id = "blind75_338_counting_bits",
            Number = 338,
            Title = "Counting Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 80.5,
            IsPremium = false,
            Tags = new List<string> { "Dynamic Programming", "Bit Manipulation" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1) auxiliary",
            DescriptionMarkdown = """
            Given an integer `n`, return an array `ans` of length `n + 1` such that for each `i` (`0 <= i <= n`), `ans[i]` is the **number of** `1`'**s** in the binary representation of `i`.
            """,
            StarterCode = """
            public class Solution 
            {
                public int[] CountBits(int n) 
                {
                    var dp = new int[n + 1];
                    for (int i = 1; i <= n; i++)
                    {
                        dp[i] = dp[i >> 1] + (i & 1);
                    }
                    return dp;
                }
            }

            var sol = new Solution();
            sol.CountBits(5).Dump("CountBits 5 (expected: [0,1,1,2,1,2])");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "n=5", Input = "5", ExpectedOutput = "[0,1,1,2,1,2]" }
            }
        },
        new()
        {
            Id = "blind75_268_missing_number",
            Number = 268,
            Title = "Missing Number",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 72.2,
            IsPremium = false,
            Tags = new List<string> { "Array", "Hash Table", "Math", "Binary Search", "Bit Manipulation" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an array `nums` containing `n` distinct numbers in the range `[0, n]`, return *the only number in the range that is missing from the array*.
            """,
            StarterCode = """
            public class Solution 
            {
                public int MissingNumber(int[] nums) 
                {
                    int res = nums.Length;
                    for (int i = 0; i < nums.Length; i++) res ^= i ^ nums[i];
                    return res;
                }
            }

            var sol = new Solution();
            sol.MissingNumber(new[] { 3, 0, 1 }).Dump("MissingNumber (expected: 2)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "[3,0,1]", Input = "[3,0,1]", ExpectedOutput = "2" }
            }
        },
        new()
        {
            Id = "blind75_190_reverse_bits",
            Number = 190,
            Title = "Reverse Bits",
            Category = "Bit Manipulation",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 60.5,
            IsPremium = false,
            Tags = new List<string> { "Divide and Conquer", "Bit Manipulation" },
            TimeComplexity = "O(1)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Reverse bits of a given 32 bits unsigned integer.
            """,
            StarterCode = """
            public class Solution 
            {
                public uint reverseBits(uint n) 
                {
                    uint res = 0;
                    for (int i = 0; i < 32; i++)
                    {
                        res = (res << 1) | (n & 1);
                        n >>= 1;
                    }
                    return res;
                }
            }

            var sol = new Solution();
            sol.reverseBits(43261596).Dump("ReverseBits");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "43261596", Input = "43261596", ExpectedOutput = "964176192" }
            }
        },
        new()
        {
            Id = "blind75_295_find_median_from_data_stream",
            Number = 295,
            Title = "Find Median from Data Stream",
            Category = "Heap / Priority Queue",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 52.0,
            IsPremium = false,
            Tags = new List<string> { "Two Pointers", "Design", "Sorting", "Heap", "Data Stream" },
            TimeComplexity = "O(log N) add, O(1) find",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            The **median** is the middle value in an ordered integer list. Implement the `MedianFinder` class with `AddNum` and `FindMedian`.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class MedianFinder 
            {
                private readonly PriorityQueue<int, int> _small = new(); // max-heap
                private readonly PriorityQueue<int, int> _large = new(); // min-heap

                public void AddNum(int num) 
                {
                    _small.Enqueue(num, -num);
                    if (_small.Count > 0 && _large.Count > 0 && _small.Peek() > _large.Peek())
                    {
                        int val = _small.Dequeue();
                        _large.Enqueue(val, val);
                    }
                    if (_small.Count > _large.Count + 1)
                    {
                        int val = _small.Dequeue();
                        _large.Enqueue(val, val);
                    }
                    if (_large.Count > _small.Count)
                    {
                        int val = _large.Dequeue();
                        _small.Enqueue(val, -val);
                    }
                }

                public double FindMedian() 
                {
                    if (_small.Count > _large.Count) return _small.Peek();
                    return (_small.Peek() + _large.Peek()) / 2.0;
                }
            }

            var mf = new MedianFinder();
            mf.AddNum(1); mf.AddNum(2);
            mf.FindMedian().Dump("Median of 1, 2 (expected: 1.5)");
            mf.AddNum(3);
            mf.FindMedian().Dump("Median of 1, 2, 3 (expected: 2)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "1, 2, 3", Input = "Add(1), Add(2), FindMedian(), Add(3), FindMedian()", ExpectedOutput = "1.5, 2.0" }
            }
        }
    };
}
