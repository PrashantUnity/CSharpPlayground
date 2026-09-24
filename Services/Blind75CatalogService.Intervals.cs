using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetIntervalProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_57_insert_interval",
            Number = 57,
            Title = "Insert Interval",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 41.5,
            IsPremium = false,
            Tags = new List<string> { "Array" },
            TimeComplexity = "O(N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            You are given an array of non-overlapping intervals `intervals` where `intervals[i] = [starti, endi]` sorted in ascending order by `starti`. You are also given an interval `newInterval = [start, end]`.
            Insert `newInterval` into `intervals` such that `intervals` is still sorted and non-overlapping.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int[][] Insert(int[][] intervals, int[] newInterval) 
                {
                    var res = new List<int[]>();
                    int i = 0, n = intervals.Length;
                    while (i < n && intervals[i][1] < newInterval[0]) res.Add(intervals[i++]);
                    while (i < n && intervals[i][0] <= newInterval[1])
                    {
                        newInterval[0] = Math.Min(newInterval[0], intervals[i][0]);
                        newInterval[1] = Math.Max(newInterval[1], intervals[i][1]);
                        i++;
                    }
                    res.Add(newInterval);
                    while (i < n) res.Add(intervals[i++]);
                    return res.ToArray();
                }
            }

            var sol = new Solution();
            sol.Insert(new[] { new[] { 1, 3 }, new[] { 6, 9 } }, new[] { 2, 5 }).Dump("Insert Interval");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard insert", Input = "[[1,3],[6,9]], [2,5]", ExpectedOutput = "[[1,5],[6,9]]" }
            }
        },
        new()
        {
            Id = "blind75_56_merge_intervals",
            Number = 56,
            Title = "Merge Intervals",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 50.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Sorting" },
            TimeComplexity = "O(N log N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an array of `intervals` where `intervals[i] = [starti, endi]`, merge all overlapping intervals, and return *an array of the non-overlapping intervals that cover all the intervals in the input*.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;

            public class Solution 
            {
                public int[][] Merge(int[][] intervals) 
                {
                    Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
                    var merged = new List<int[]>();
                    foreach (var interval in intervals)
                    {
                        if (merged.Count == 0 || merged[^1][1] < interval[0]) merged.Add(interval);
                        else merged[^1][1] = Math.Max(merged[^1][1], interval[1]);
                    }
                    return merged.ToArray();
                }
            }

            var sol = new Solution();
            sol.Merge(new[] { new[] { 1, 3 }, new[] { 2, 6 }, new[] { 8, 10 }, new[] { 15, 18 } }).Dump("Merged Intervals");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Overlap", Input = "[[1,3],[2,6],[8,10],[15,18]]", ExpectedOutput = "[[1,6],[8,10],[15,18]]" }
            }
        },
        new()
        {
            Id = "blind75_435_non_overlapping_intervals",
            Number = 435,
            Title = "Non-overlapping Intervals",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 54.0,
            IsPremium = false,
            Tags = new List<string> { "Array", "Dynamic Programming", "Greedy", "Sorting" },
            TimeComplexity = "O(N log N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an array of intervals `intervals` where `intervals[i] = [starti, endi]`, return the minimum number of intervals you need to remove to make the rest of the intervals non-overlapping.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int EraseOverlapIntervals(int[][] intervals) 
                {
                    Array.Sort(intervals, (a, b) => a[1].CompareTo(b[1]));
                    int count = 0, prevEnd = intervals[0][1];
                    for (int i = 1; i < intervals.Length; i++)
                    {
                        if (intervals[i][0] < prevEnd) count++;
                        else prevEnd = intervals[i][1];
                    }
                    return count;
                }
            }

            var sol = new Solution();
            sol.EraseOverlapIntervals(new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 1, 3 } }).Dump("EraseOverlapIntervals (expected: 1)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Four intervals", Input = "[[1,2],[2,3],[3,4],[1,3]]", ExpectedOutput = "1" }
            }
        },
        new()
        {
            Id = "blind75_252_meeting_rooms",
            Number = 252,
            Title = "Meeting Rooms",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Easy,
            AcceptanceRate = 60.5,
            IsPremium = true,
            Tags = new List<string> { "Array", "Sorting" },
            TimeComplexity = "O(N log N)",
            SpaceComplexity = "O(1)",
            DescriptionMarkdown = """
            Given an array of meeting time intervals where `intervals[i] = [starti, endi]`, determine if a person could attend all meetings.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public bool CanAttendMeetings(int[][] intervals) 
                {
                    Array.Sort(intervals, (a, b) => a[0].CompareTo(b[0]));
                    for (int i = 1; i < intervals.Length; i++)
                    {
                        if (intervals[i][0] < intervals[i - 1][1]) return false;
                    }
                    return true;
                }
            }

            var sol = new Solution();
            sol.CanAttendMeetings(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } }).Dump("CanAttendMeetings (expected: False)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Overlap", Input = "[[0,30],[5,10],[15,20]]", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_253_meeting_rooms_ii",
            Number = 253,
            Title = "Meeting Rooms II",
            Category = "Intervals",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 53.0,
            IsPremium = true,
            Tags = new List<string> { "Array", "Two Pointers", "Greedy", "Sorting", "Heap" },
            TimeComplexity = "O(N log N)",
            SpaceComplexity = "O(N)",
            DescriptionMarkdown = """
            Given an array of meeting time intervals where `intervals[i] = [starti, endi]`, return the minimum number of conference rooms required.
            """,
            StarterCode = """
            using System;

            public class Solution 
            {
                public int MinMeetingRooms(int[][] intervals) 
                {
                    int n = intervals.Length;
                    var start = new int[n];
                    var end = new int[n];
                    for (int i = 0; i < n; i++) { start[i] = intervals[i][0]; end[i] = intervals[i][1]; }
                    Array.Sort(start);
                    Array.Sort(end);

                    int rooms = 0, endIdx = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (start[i] < end[endIdx]) rooms++;
                        else endIdx++;
                    }
                    return rooms;
                }
            }

            var sol = new Solution();
            sol.MinMeetingRooms(new[] { new[] { 0, 30 }, new[] { 5, 10 }, new[] { 15, 20 } }).Dump("MinMeetingRooms (expected: 2)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3 intervals", Input = "[[0,30],[5,10],[15,20]]", ExpectedOutput = "2" }
            }
        }
    };
}
