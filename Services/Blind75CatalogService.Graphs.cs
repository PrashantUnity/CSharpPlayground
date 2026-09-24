using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static IEnumerable<BlindProblemItem> GetGraphAndTrieProblems()
    {
        var list = new List<BlindProblemItem>(GetTrieAndBacktrackingProblems());
        list.AddRange(GetCoreGraphProblems());
        return list;
    }

    private static IEnumerable<BlindProblemItem> GetCoreGraphProblems() => new List<BlindProblemItem>
    {
        new()
        {
            Id = "blind75_200_number_of_islands",
            Number = 200,
            Title = "Number of Islands",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 60.2,
            IsPremium = false,
            HasVisualizer = true,
            VisualizerKind = "Graph",
            Tags = new List<string> { "Array", "DFS", "BFS", "Union Find", "Matrix" },
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(M * N)",
            DescriptionMarkdown = """
            Given an `m x n` 2D binary grid `grid` which represents a map of `'1'`s (land) and `'0'`s (water), return *the number of islands*.
            """,
            StarterCode = """
            public class Solution 
            {
                public int NumIslands(char[][] grid) 
                {
                    if (grid == null || grid.Length == 0) return 0;
                    int count = 0, m = grid.Length, n = grid[0].Length;

                    void Dfs(int r, int c)
                    {
                        if (r < 0 || r >= m || c < 0 || c >= n || grid[r][c] != '1') return;
                        grid[r][c] = '0';
                        Dfs(r + 1, c); Dfs(r - 1, c); Dfs(r, c + 1); Dfs(r, c - 1);
                    }

                    for (int r = 0; r < m; r++)
                    {
                        for (int c = 0; c < n; c++)
                        {
                            if (grid[r][c] == '1')
                            {
                                count++;
                                Dfs(r, c);
                            }
                        }
                    }
                    return count;
                }
            }

            var sol = new Solution();
            var grid = new[] {
                new[] { '1','1','0','0','0' },
                new[] { '1','1','0','0','0' },
                new[] { '0','0','1','0','0' },
                new[] { '0','0','0','1','1' }
            };
            sol.NumIslands(grid).Dump("NumIslands (expected: 3)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "3 islands", Input = "4x5 grid", ExpectedOutput = "3" }
            }
        },
        new()
        {
            Id = "blind75_133_clone_graph",
            Number = 133,
            Title = "Clone Graph",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 59.2,
            IsPremium = false,
            HasVisualizer = true,
            VisualizerKind = "Graph",
            Tags = new List<string> { "Hash Table", "DFS", "BFS", "Graph" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V)",
            DescriptionMarkdown = """
            Given a reference of a node in a connected undirected graph, return a **deep copy** (clone) of the graph.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Node 
            {
                public int val;
                public IList<Node> neighbors;
                public Node(int _val = 0, IList<Node> _neighbors = null) { val = _val; neighbors = _neighbors ?? new List<Node>(); }
            }

            public class Solution 
            {
                private readonly Dictionary<Node, Node> _cloned = new();

                public Node CloneGraph(Node node) 
                {
                    if (node == null) return null;
                    if (_cloned.TryGetValue(node, out var clone)) return clone;
                    clone = new Node(node.val);
                    _cloned[node] = clone;
                    foreach (var nei in node.neighbors) clone.neighbors.Add(CloneGraph(nei));
                    return clone;
                }
            }

            var n1 = new Node(1); var n2 = new Node(2);
            n1.neighbors.Add(n2); n2.neighbors.Add(n1);
            var sol = new Solution();
            var cloned = sol.CloneGraph(n1);
            cloned.Dump("Cloned Graph Root");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "2 nodes cycle", Input = "[[2],[1]]", ExpectedOutput = "[[2],[1]]" }
            }
        },
        new()
        {
            Id = "blind75_417_pacific_atlantic_water_flow",
            Number = 417,
            Title = "Pacific Atlantic Water Flow",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 55.5,
            IsPremium = false,
            Tags = new List<string> { "Array", "DFS", "BFS", "Matrix" },
            TimeComplexity = "O(M * N)",
            SpaceComplexity = "O(M * N)",
            DescriptionMarkdown = """
            Return a 2D list of grid coordinates `result` where `result[i] = [ri, ci]` denotes that rain water can flow from cell `(ri, ci)` to both the Pacific and Atlantic oceans.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public IList<IList<int>> PacificAtlantic(int[][] heights) 
                {
                    int m = heights.Length, n = heights[0].Length;
                    var pac = new bool[m, n];
                    var atl = new bool[m, n];

                    void Dfs(int r, int c, bool[,] visited, int prevH)
                    {
                        if (r < 0 || r >= m || c < 0 || c >= n || visited[r, c] || heights[r][c] < prevH) return;
                        visited[r, c] = true;
                        Dfs(r + 1, c, visited, heights[r][c]);
                        Dfs(r - 1, c, visited, heights[r][c]);
                        Dfs(r, c + 1, visited, heights[r][c]);
                        Dfs(r, c - 1, visited, heights[r][c]);
                    }

                    for (int c = 0; c < n; c++) { Dfs(0, c, pac, int.MinValue); Dfs(m - 1, c, atl, int.MinValue); }
                    for (int r = 0; r < m; r++) { Dfs(r, 0, pac, int.MinValue); Dfs(r, n - 1, atl, int.MinValue); }

                    var res = new List<IList<int>>();
                    for (int r = 0; r < m; r++)
                        for (int c = 0; c < n; c++)
                            if (pac[r, c] && atl[r, c]) res.Add(new List<int> { r, c });

                    return res;
                }
            }

            var sol = new Solution();
            var heights = new[] {
                new[] { 1,2,2,3,5 },
                new[] { 3,2,3,4,4 },
                new[] { 2,4,5,3,1 },
                new[] { 6,7,1,4,5 },
                new[] { 5,1,1,2,4 }
            };
            sol.PacificAtlantic(heights).Dump("Pacific Atlantic Intersections");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "5x5 heights", Input = "heights matrix", ExpectedOutput = "Coordinates list" }
            }
        },
        new()
        {
            Id = "blind75_207_course_schedule",
            Number = 207,
            Title = "Course Schedule",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 48.0,
            IsPremium = false,
            HasVisualizer = true,
            VisualizerKind = "Graph",
            Tags = new List<string> { "DFS", "BFS", "Graph", "Topological Sort" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V + E)",
            DescriptionMarkdown = """
            There are a total of `numCourses` courses you have to take, labeled from `0` to `numCourses - 1`. You are given an array `prerequisites` where `prerequisites[i] = [ai, bi]` indicates that you must take `bi` first if you want to take `ai`.
            Return `true` if you can finish all courses. Otherwise, return `false`.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public bool CanFinish(int numCourses, int[][] prerequisites) 
                {
                    var adj = new List<int>[numCourses];
                    for (int i = 0; i < numCourses; i++) adj[i] = new List<int>();
                    var inDegree = new int[numCourses];
                    foreach (var pre in prerequisites)
                    {
                        adj[pre[1]].Add(pre[0]);
                        inDegree[pre[0]]++;
                    }

                    var q = new Queue<int>();
                    for (int i = 0; i < numCourses; i++) if (inDegree[i] == 0) q.Enqueue(i);

                    int taken = 0;
                    while (q.Count > 0)
                    {
                        int node = q.Dequeue();
                        taken++;
                        foreach (var nei in adj[node])
                        {
                            if (--inDegree[nei] == 0) q.Enqueue(nei);
                        }
                    }
                    return taken == numCourses;
                }
            }

            var sol = new Solution();
            sol.CanFinish(2, new[] { new[] { 1, 0 } }).Dump("CanFinish [1,0] (expected: True)");
            sol.CanFinish(2, new[] { new[] { 1, 0 }, new[] { 0, 1 } }).Dump("CanFinish cycle (expected: False)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "No cycle", Input = "2, [[1,0]]", ExpectedOutput = "True" },
                new() { Name = "Cycle", Input = "2, [[1,0],[0,1]]", ExpectedOutput = "False" }
            }
        },
        new()
        {
            Id = "blind75_261_graph_valid_tree",
            Number = 261,
            Title = "Graph Valid Tree",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 48.2,
            IsPremium = true,
            HasVisualizer = true,
            VisualizerKind = "Graph",
            Tags = new List<string> { "DFS", "BFS", "Union Find", "Graph" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V + E)",
            DescriptionMarkdown = """
            Given `n` nodes labeled from `0` to `n - 1` and a list of undirected edges, write a function to check whether these edges make up a valid tree.
            """,
            StarterCode = """
            using System.Collections.Generic;

            public class Solution 
            {
                public bool ValidTree(int n, int[][] edges) 
                {
                    if (edges.Length != n - 1) return false;
                    var parent = new int[n];
                    for (int i = 0; i < n; i++) parent[i] = i;

                    int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

                    foreach (var edge in edges)
                    {
                        int r1 = Find(edge[0]), r2 = Find(edge[1]);
                        if (r1 == r2) return false;
                        parent[r1] = r2;
                    }
                    return true;
                }
            }

            var sol = new Solution();
            sol.ValidTree(5, new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 4 } }).Dump("ValidTree (expected: True)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Valid tree", Input = "5, [[0,1],[0,2],[0,3],[1,4]]", ExpectedOutput = "True" }
            }
        },
        new()
        {
            Id = "blind75_323_number_of_connected_components_in_an_undirected_graph",
            Number = 323,
            Title = "Number of Connected Components in an Undirected Graph",
            Category = "Graphs",
            Difficulty = ProblemDifficulty.Medium,
            AcceptanceRate = 65.2,
            IsPremium = true,
            Tags = new List<string> { "DFS", "BFS", "Union Find", "Graph" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V)",
            DescriptionMarkdown = """
            You have a graph of `n` nodes. You are given an integer `n` and an array `edges` where `edges[i] = [ai, bi]` indicates that there is an edge between `ai` and `bi` in the graph.
            Return *the number of connected components in the graph*.
            """,
            StarterCode = """
            public class Solution 
            {
                public int CountComponents(int n, int[][] edges) 
                {
                    var parent = new int[n];
                    for (int i = 0; i < n; i++) parent[i] = i;
                    int components = n;

                    int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

                    foreach (var edge in edges)
                    {
                        int r1 = Find(edge[0]), r2 = Find(edge[1]);
                        if (r1 != r2)
                        {
                            parent[r1] = r2;
                            components--;
                        }
                    }
                    return components;
                }
            }

            var sol = new Solution();
            sol.CountComponents(5, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 3, 4 } }).Dump("CountComponents (expected: 2)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "2 components", Input = "5, [[0,1],[1,2],[3,4]]", ExpectedOutput = "2" }
            }
        },
        new()
        {
            Id = "blind75_269_alien_dictionary",
            Number = 269,
            Title = "Alien Dictionary",
            Category = "Advanced Graphs",
            Difficulty = ProblemDifficulty.Hard,
            AcceptanceRate = 37.5,
            IsPremium = true,
            Tags = new List<string> { "Array", "String", "DFS", "BFS", "Graph", "Topological Sort" },
            TimeComplexity = "O(C)",
            SpaceComplexity = "O(V + E)",
            DescriptionMarkdown = """
            There is a new alien language that uses the English alphabet. Return a string of the unique letters in the new alien language sorted in lexicographically increasing order by the new language's rules. If the order is invalid, return `""`.
            """,
            StarterCode = """
            using System;
            using System.Collections.Generic;
            using System.Text;

            public class Solution 
            {
                public string AlienOrder(string[] words) 
                {
                    var adj = new Dictionary<char, HashSet<char>>();
                    var inDegree = new Dictionary<char, int>();
                    foreach (var w in words) foreach (var c in w) { adj[c] = new HashSet<char>(); inDegree[c] = 0; }

                    for (int i = 0; i < words.Length - 1; i++)
                    {
                        string w1 = words[i], w2 = words[i + 1];
                        if (w1.Length > w2.Length && w1.StartsWith(w2)) return "";
                        int minLen = Math.Min(w1.Length, w2.Length);
                        for (int j = 0; j < minLen; j++)
                        {
                            if (w1[j] != w2[j])
                            {
                                if (adj[w1[j]].Add(w2[j])) inDegree[w2[j]]++;
                                break;
                            }
                        }
                    }

                    var q = new Queue<char>();
                    foreach (var kvp in inDegree) if (kvp.Value == 0) q.Enqueue(kvp.Key);
                    var sb = new StringBuilder();
                    while (q.Count > 0)
                    {
                        char c = q.Dequeue();
                        sb.Append(c);
                        foreach (var nei in adj[c])
                        {
                            if (--inDegree[nei] == 0) q.Enqueue(nei);
                        }
                    }

                    return sb.Length == inDegree.Count ? sb.ToString() : "";
                }
            }

            var sol = new Solution();
            sol.AlienOrder(new[] { "wrt","wrf","er","ett","rftt" }).Dump("AlienOrder (expected: wertf)");
            """,
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Standard Alien", Input = "[\"wrt\",\"wrf\",\"er\",\"ett\",\"rftt\"]", ExpectedOutput = "wertf" }
            }
        }
    };
}
