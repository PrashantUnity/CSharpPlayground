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
            Tags = new List<string> { "Array", "DFS", "BFS", "Union Find", "Matrix" },
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(m · n) for the recursion in the worst case",
            DescriptionMarkdown = """
            Given an `m × n` grid of `'1'`s (land) and `'0'`s (water), return the number of **islands**.

            An island is a group of land cells connected **horizontally or vertically** (not diagonally), surrounded by water. Everything outside the grid is water.

            ### Example 1
            - **Input:** `grid = ["11110","11010","11000","00000"]`
            - **Output:** `1`
            - **Why:** all the land cells touch each other.

            ### Example 2
            - **Input:** `grid = ["11000","11000","00100","00011"]`
            - **Output:** `3`
            - **Why:** a 2×2 block top-left, a single cell in the middle, and a pair at the bottom right.

            ### Constraints
            - `1 <= m, n <= 300`
            - `grid[i][j]` is `'0'` or `'1'`.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** count the connected groups of `'1'` cells.

            1. **Scan the grid cell by cell.** The first time you meet a land cell that isn't part of an island you've already counted, you've found a new island: count it.
            2. **Then claim the whole island** so its other cells aren't counted again: flood fill from that cell (DFS or BFS) through every land neighbour, marking each one as seen. The easy way to mark is to turn it into water, `'0'`.
            3. **Each cell is visited a constant number of times:** once by the scan and at most once by a flood, so `O(m · n)`.
            4. **Walk Example 2:** the scan finds (0,0), floods the 2×2 block → 1. It skips the flooded cells, finds (2,2) → 2, then (3,3), whose flood also takes (3,4) → 3.

            **Pattern to remember:** "count connected regions" = scan + flood fill (or union-find). The same shape solves max area of island, surrounded regions and number of provinces.

            **Common mistakes:** counting diagonal neighbours; forgetting to mark cells (islands get counted many times, or the DFS loops forever); checking bounds after indexing the grid.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Flood fill with a queue (breadth-first)",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(min(m, n)) for the queue",
                    Intuition = "Same scan, but each new island is flooded level by level with a queue instead of recursion, so a huge island can't overflow the call stack.",
                    Code = """
                    int NumIslandsBfs(char[][] grid)
                    {
                        int islands = 0;
                        for (int r = 0; r < grid.Length; r++)
                            for (int c = 0; c < grid[0].Length; c++)
                            {
                                if (grid[r][c] != '1') continue;
                                islands++;
                                var queue = new Queue<(int R, int C)>();
                                queue.Enqueue((r, c));
                                grid[r][c] = '0';
                                while (queue.Count > 0)
                                {
                                    var (cr, cc) = queue.Dequeue();
                                    foreach (var (nr, nc) in new[] { (cr + 1, cc), (cr - 1, cc), (cr, cc + 1), (cr, cc - 1) })
                                    {
                                        if (nr < 0 || nc < 0 || nr >= grid.Length || nc >= grid[0].Length || grid[nr][nc] != '1') continue;
                                        grid[nr][nc] = '0';   // mark when queued, so nothing is queued twice
                                        queue.Enqueue((nr, nc));
                                    }
                                }
                            }
                        return islands;
                    }

                    Console.WriteLine(Judge.Format(NumIslandsBfs(Grid("11000", "11000", "00100", "00011"))));   // 3
                    """
                },
                new()
                {
                    Name = "Scan, and sink each new island with DFS",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m · n) worst-case recursion",
                    Intuition = "Every land cell the scan meets starts a new island; a depth-first flood turns the whole island to water so it's counted once."
                }
            },
            SupportCode = """
            // Grid("110", "011") is [["1","1","0"],["0","1","1"]].
            char[][] Grid(params string[] rows) => rows.Select(row => row.ToCharArray()).ToArray();
            """,
            SolutionCode = """
            public class Solution
            {
                public int NumIslands(char[][] grid)
                {
                    int islands = 0;
                    for (int r = 0; r < grid.Length; r++)
                        for (int c = 0; c < grid[0].Length; c++)
                            if (grid[r][c] == '1')
                            {
                                islands++;              // land nobody has reached yet: a new island
                                Sink(grid, r, c);       // flood all of it so it's counted once
                            }
                    return islands;
                }

                private void Sink(char[][] grid, int r, int c)
                {
                    if (r < 0 || c < 0 || r >= grid.Length || c >= grid[0].Length || grid[r][c] != '1') return;
                    grid[r][c] = '0';                   // seen: turn it into water
                    Sink(grid, r + 1, c);
                    Sink(grid, r - 1, c);
                    Sink(grid, r, c + 1);
                    Sink(grid, r, c - 1);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "grid = [\"11110\",\"11010\",\"11000\",\"00000\"]", Expected = "1", Call = "sol.NumIslands(Grid(\"11110\", \"11010\", \"11000\", \"00000\"))" },
                new() { Name = "Example 2", Input = "grid = [\"11000\",\"11000\",\"00100\",\"00011\"]", Expected = "3", Call = "sol.NumIslands(Grid(\"11000\", \"11000\", \"00100\", \"00011\"))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Only water", Input = "grid = [\"000\",\"000\"]", Expected = "0", Call = "sol.NumIslands(Grid(\"000\", \"000\"))" },
                new() { Name = "A single cell of land", Input = "grid = [\"1\"]", Expected = "1", Call = "sol.NumIslands(Grid(\"1\"))" },
                new() { Name = "Diagonals don't connect", Input = "grid = [\"101\",\"010\",\"101\"]", Expected = "5", Call = "sol.NumIslands(Grid(\"101\", \"010\", \"101\"))" },
                new() { Name = "One winding island", Input = "grid = [\"111\",\"001\",\"111\"]", Expected = "1", Call = "sol.NumIslands(Grid(\"111\", \"001\", \"111\"))" }
            },
            StressTestCode = """
            judge.Agree("Random grids vs the breadth-first version",
                random => Enumerable.Range(0, random.Next(1, 6)).Select(_ => new string(Enumerable.Range(0, 5).Select(_ => random.Next(3) == 0 ? '0' : '1').ToArray())).ToArray(),
                rows => NumIslandsBfs(Grid(rows)),
                rows => sol.NumIslands(Grid(rows)));
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 2 (grey is land, dark blue water). The scan finds a land cell no island has reached, counts a new
            island, and the flood colours every cell of it (one colour per island) as it turns them into water.
            """,
            VisualizationCode = """
            var rows = new[] { "11000", "11000", "00100", "00011" };
            var grid = Grid(rows);
            var tracker = MatrixTracker.Create(rows, "200. Number of Islands: count each island once, then sink it");
            string[] colors = { "#0f766e", "#7c3aed", "#c2410c", "#be185d", "#15803d" };
            int islands = 0;
            tracker.Watch(() => islands);

            void Sink(int r, int c, string color, string why)
            {
                if (r < 0 || c < 0 || r >= grid.Length || c >= grid[0].Length || grid[r][c] != '1') return;
                grid[r][c] = '0';
                tracker.Visit(r, c, why, color: color);
                string next = $"land next to island {islands}: sink it too";
                Sink(r + 1, c, color, $"({r + 1},{c}) is {next}");
                Sink(r - 1, c, color, $"({r - 1},{c}) is {next}");
                Sink(r, c + 1, color, $"({r},{c + 1}) is {next}");
                Sink(r, c - 1, color, $"({r},{c - 1}) is {next}");
            }

            for (int r = 0; r < grid.Length; r++)
                for (int c = 0; c < grid[0].Length; c++)
                    if (grid[r][c] == '1')
                    {
                        islands++;
                        Sink(r, c, colors[(islands - 1) % colors.Length], $"The scan reaches ({r},{c}): land that no island has claimed, so island {islands} starts here");
                    }

            tracker.Snapshot($"The scan is finished: {islands} islands");
            Display.Visualizer(tracker);
            """
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
            Tags = new List<string> { "Hash Table", "DFS", "BFS", "Graph" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V)",
            DescriptionMarkdown = """
            Given a node of a connected, undirected graph, return a **deep copy** (clone) of the whole graph: new nodes with the same values and the same connections, sharing no node with the original.

            Each node has a value `val` (1 to n, unique) and a list `neighbors`. The examples write the graph as an adjacency list: entry `i` lists the neighbours of node `i + 1`. The given node is always node 1.

            ### Example 1
            - **Input:** `adjList = [[2,4],[1,3],[2,4],[1,3]]`
            - **Output:** `[[2,4],[1,3],[2,4],[1,3]]`
            - **Why:** four nodes in a square, 1–2–3–4–1; the copy has the same shape.

            ### Example 2
            - **Input:** `adjList = [[]]`
            - **Output:** `[[]]`
            - **Why:** one node with no neighbours.

            ### Example 3
            - **Input:** `adjList = []`
            - **Output:** `[]`
            - **Why:** an empty graph; the node given is `null`.

            ### Constraints
            - The number of nodes is in the range `[0, 100]`, with `1 <= Node.val <= 100`, all unique.
            - No repeated edges and no self-loops; the graph is connected.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** copy every node and every edge exactly once, even though edges go both ways and cycles are everywhere.

            1. **Copying a node means copying its neighbours** too, and theirs, and so on: a graph traversal (DFS or BFS).
            2. **The danger is cycles:** 1's neighbour 2 has 1 as a neighbour again. Copying blindly would recurse forever, or create two copies of node 1.
            3. **Remember what you've copied:** a dictionary `original → copy`. Before copying a node, check it: if it's already there, reuse that copy. Put the new copy in the dictionary **before** visiting its neighbours, so a cycle back to it finds it.
            4. **Every node is copied once and every edge linked once:** `O(V + E)`.
            5. **Walk Example 1:** copy 1 → visit 2 → copy 2 → its neighbour 1 is already copied, link it → visit 3 → copy 3 → … until every copy has its neighbours.

            **Pattern to remember:** traversing a structure that can loop back → keep a visited map; when you're building something, the map also stores what you built (here, original → copy).

            **Common mistakes:** adding the copy to the map after the recursion (cycles then loop forever); returning original nodes as neighbours of copies (not a deep copy); forgetting the `null` input.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Breadth-first copying with a queue",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(V + E)",
                    SpaceComplexity = "O(V)",
                    Intuition = "Copy the start node, then take originals from a queue and link each copy to the copies of its neighbours, creating (and queueing) any neighbour copy that doesn't exist yet.",
                    Code = """
                    Node CloneBfs(Node start)
                    {
                        if (start == null) return null;
                        var copies = new Dictionary<Node, Node> { [start] = new Node(start.val) };
                        var queue = new Queue<Node>();
                        queue.Enqueue(start);
                        while (queue.Count > 0)
                        {
                            var node = queue.Dequeue();
                            foreach (var neighbour in node.neighbors)
                            {
                                if (!copies.ContainsKey(neighbour))
                                {
                                    copies[neighbour] = new Node(neighbour.val);
                                    queue.Enqueue(neighbour);
                                }
                                copies[node].neighbors.Add(copies[neighbour]);
                            }
                        }
                        return copies[start];
                    }

                    Console.WriteLine(Judge.Format(CloneBfs(BuildGraph(new[] { new[] { 2, 4 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 1, 3 } }))));   // [[2,4],[1,3],[2,4],[1,3]]
                    """
                },
                new()
                {
                    Name = "Depth-first copying with an original → copy map",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(V + E)",
                    SpaceComplexity = "O(V)",
                    Intuition = "Copy a node, record it in the map, then clone each neighbour (reusing copies already in the map) and link them."
                }
            },
            SupportCode = """
            public class Node
            {
                public int val;
                public IList<Node> neighbors;
                public Node(int val = 0) { this.val = val; neighbors = new List<Node>(); }
            }

            // BuildGraph([[2,4],[1,3],[2,4],[1,3]]): entry i lists the neighbours of node i + 1; returns node 1 (null when empty).
            Node BuildGraph(int[][] adjacency)
            {
                if (adjacency.Length == 0) return null;
                var nodes = Enumerable.Range(1, adjacency.Length).Select(value => new Node(value)).ToArray();
                for (int i = 0; i < adjacency.Length; i++)
                    foreach (int neighbour in adjacency[i]) nodes[i].neighbors.Add(nodes[neighbour - 1]);
                return nodes[0];
            }
            """,
            SolutionCode = """
            public class Solution
            {
                private readonly Dictionary<Node, Node> _copies = new();   // original -> its copy

                public Node CloneGraph(Node node)
                {
                    if (node == null) return null;
                    if (_copies.TryGetValue(node, out var existing)) return existing;   // copied already (a cycle led back here)

                    var copy = new Node(node.val);
                    _copies[node] = copy;                                 // record it before visiting the neighbours
                    foreach (var neighbour in node.neighbors)
                        copy.neighbors.Add(CloneGraph(neighbour));
                    return copy;
                }
            }
            """,
            TestSetupCode = """
            // A deep copy shares no node with the original and has the same shape.
            bool IsDeepCopy(Node original)
            {
                var copy = new Solution().CloneGraph(original);
                HashSet<Node> Reach(Node start)
                {
                    var seen = new HashSet<Node>();
                    var stack = new Stack<Node>();
                    if (start != null) stack.Push(start);
                    while (stack.Count > 0)
                        foreach (var next in stack.Pop() is var node && seen.Add(node) ? node.neighbors : new List<Node>())
                            stack.Push(next);
                    return seen;
                }
                return !Reach(copy).Overlaps(Reach(original)) && Judge.Format(copy) == Judge.Format(original);
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "adjList = [[2,4],[1,3],[2,4],[1,3]]", Expected = "[[2,4],[1,3],[2,4],[1,3]]", Call = "sol.CloneGraph(BuildGraph(new[] { new[] { 2, 4 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 1, 3 } }))" },
                new() { Name = "Example 2", Input = "adjList = [[]]", Expected = "[[]]", Call = "sol.CloneGraph(BuildGraph(new[] { new int[0] }))" },
                new() { Name = "Example 3", Input = "adjList = []", Expected = "[]", Call = "sol.CloneGraph(BuildGraph(new int[0][]))" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A real copy, no shared nodes", Input = "adjList = [[2,4],[1,3],[2,4],[1,3]]", Expected = "true", Call = "IsDeepCopy(BuildGraph(new[] { new[] { 2, 4 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 1, 3 } }))" },
                new() { Name = "Two nodes", Input = "adjList = [[2],[1]]", Expected = "[[2],[1]]", Call = "sol.CloneGraph(BuildGraph(new[] { new[] { 2 }, new[] { 1 } }))" },
                new() { Name = "A triangle", Input = "adjList = [[2,3],[1,3],[1,2]]", Expected = "[[2,3],[1,3],[1,2]]", Call = "sol.CloneGraph(BuildGraph(new[] { new[] { 2, 3 }, new[] { 1, 3 }, new[] { 1, 2 } }))" },
                new() { Name = "A line", Input = "adjList = [[2],[1,3],[2]]", Expected = "[[2],[1,3],[2]]", Call = "sol.CloneGraph(BuildGraph(new[] { new[] { 2 }, new[] { 1, 3 }, new[] { 2 } }))" }
            },
            StressTestCode = """
            judge.Agree("Random connected graphs are copied exactly",
                random =>
                {
                    int n = random.Next(1, 8);
                    var edges = new HashSet<(int, int)>();
                    for (int v = 2; v <= n; v++) edges.Add((random.Next(1, v), v));          // a random tree keeps it connected
                    for (int k = random.Next(0, n); k > 0; k--)
                    {
                        int a = random.Next(1, n + 1), b = random.Next(1, n + 1);
                        if (a != b) edges.Add((Math.Min(a, b), Math.Max(a, b)));
                    }
                    return Enumerable.Range(1, n)
                        .Select(v => edges.Where(e => e.Item1 == v || e.Item2 == v).Select(e => e.Item1 == v ? e.Item2 : e.Item1).OrderBy(x => x).ToArray())
                        .ToArray();
                },
                adjacency => Judge.Format(CloneBfs(BuildGraph(adjacency))),
                adjacency => Judge.Format(new Solution().CloneGraph(BuildGraph(adjacency))));
            """,
            VisualizerKind = "Graph",
            VisualizationDescription = """
            Example 1's square. Depth-first cloning copies a node (it turns into "copied"), records it in the map, then
            follows its neighbours; a neighbour that already has a copy is simply reused, which is what stops the cycle.
            `copied` shows the clone's adjacency list as the links are made.
            """,
            VisualizationCode = """
            int[][] adjacency = { new[] { 2, 4 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 1, 3 } };
            var start = BuildGraph(adjacency);
            var drawing = new Dictionary<int, List<int>>();
            for (int i = 0; i < adjacency.Length; i++) drawing[i + 1] = adjacency[i].ToList();
            var tracker = GraphTracker.Create(drawing, "133. Clone Graph: copy each node once, then link the copies", isDirected: false);

            var copies = new Dictionary<Node, Node>();
            var copied = new SortedDictionary<int, List<int>>();   // the clone so far: node -> neighbours
            tracker.Watch(copied);

            Node Clone(Node node)
            {
                if (copies.TryGetValue(node, out var existing))
                {
                    tracker.Visit(node.val, $"{node.val} already has a copy: reuse it instead of copying again (this is what stops the cycle)");
                    return existing;
                }

                var copy = new Node(node.val);
                copies[node] = copy;
                copied[node.val] = new List<int>();
                tracker.Visit(node.val, $"Copy {node.val} and record it in the map before visiting its neighbours", subLabel: "copied");
                foreach (var neighbour in node.neighbors)
                {
                    copy.neighbors.Add(Clone(neighbour));
                    copied[node.val].Add(neighbour.val);
                    tracker.MarkEdge(node.val, neighbour.val, GraphEdgeState.Visited);
                    tracker.Visit(node.val, $"Back at {node.val}: link its copy to the copy of {neighbour.val}");
                }
                return copy;
            }

            var cloneOfStart = Clone(start);
            tracker.ClearCurrent();
            tracker.Snapshot($"Every node has one copy and every edge is linked: {Judge.Format(cloneOfStart)}");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(m · n)",
            SpaceComplexity = "O(m · n)",
            DescriptionMarkdown = """
            An `m × n` island has a height for every cell. The **Pacific** touches its top and left edges and the **Atlantic** its bottom and right edges.

            Rain flows from a cell to a neighbouring cell (up, down, left, right) whose height is **less than or equal** to it, and from edge cells into the ocean they touch. Return every cell `[r, c]` from which water can reach **both** oceans, in any order.

            ### Example 1
            - **Input:** `heights = [[1,2,2,3,5],[3,2,3,4,4],[2,4,5,3,1],[6,7,1,4,5],[5,1,1,2,4]]`
            - **Output:** `[[0,4],[1,3],[1,4],[2,2],[3,0],[3,1],[4,0]]`
            - **Why:** e.g. from `[2,2]` (height 5), water runs up-left over 3, 2, 2 into the Pacific and down-right over 4, 3 into the Atlantic.

            ### Example 2
            - **Input:** `heights = [[1]]`
            - **Output:** `[[0,0]]`

            ### Constraints
            - `1 <= m, n <= 200`
            - `0 <= heights[r][c] <= 10^5`
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** which cells drain into the Pacific **and** into the Atlantic?

            1. **The direct way:** from every cell, search downhill and see which oceans you reach. That repeats the same searches over and over: `O((m · n)²)`.
            2. **Flip the direction.** Instead of asking where water from a cell goes, ask which cells can send water to an ocean. Start at the ocean's edge cells and walk **uphill** (to neighbours that are **at least as high**): every cell you reach can drain back down into that ocean.
            3. **Do it once per ocean:** one search from the top and left edges (Pacific), one from the bottom and right edges (Atlantic). Each marks the cells it reaches.
            4. **The answer is the overlap:** cells marked by both searches. Each search visits each cell once: `O(m · n)`.
            5. **Walk Example 1:** from the Pacific edges the climb covers the upper-left region; from the Atlantic edges the lower-right. `[2,2]`, the 5 in the middle, is high enough to be reached from both.

            **Pattern to remember:** "which cells can reach X?" is often easier backwards: start from X and reverse the moves (here, flow downhill ↔ climb uphill). Multi-source search from all edge cells at once.

            **Common mistakes:** climbing to strictly higher cells only (equal heights let water through); searching from every cell; forgetting that corner cells touch both oceans.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Follow the water downhill from every cell",
                    Kind = ApproachKind.Naive,
                    TimeComplexity = "O((m · n)²)",
                    SpaceComplexity = "O(m · n)",
                    Intuition = "For each cell, search every cell its water can flow to (neighbours not higher) and note whether that search touches the Pacific edges and the Atlantic edges.",
                    BottleneckExplanation = "Neighbouring cells repeat almost the same search; walking uphill once from each ocean shares all that work.",
                    Code = """
                    List<int[]> PacificAtlanticBruteForce(int[][] heights)
                    {
                        int rows = heights.Length, cols = heights[0].Length;
                        (bool Pacific, bool Atlantic) Drain(int sr, int sc)
                        {
                            bool pacific = false, atlantic = false;
                            var seen = new HashSet<(int, int)> { (sr, sc) };
                            var stack = new Stack<(int R, int C)>();
                            stack.Push((sr, sc));
                            while (stack.Count > 0)
                            {
                                var (r, c) = stack.Pop();
                                if (r == 0 || c == 0) pacific = true;
                                if (r == rows - 1 || c == cols - 1) atlantic = true;
                                foreach (var (nr, nc) in new[] { (r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1) })
                                    if (nr >= 0 && nc >= 0 && nr < rows && nc < cols && heights[nr][nc] <= heights[r][c] && seen.Add((nr, nc)))
                                        stack.Push((nr, nc));
                            }
                            return (pacific, atlantic);
                        }

                        var result = new List<int[]>();
                        for (int r = 0; r < rows; r++)
                            for (int c = 0; c < cols; c++)
                                if (Drain(r, c) == (true, true)) result.Add(new[] { r, c });
                        return result;
                    }

                    Console.WriteLine(Judge.Format(PacificAtlanticBruteForce(new[] { new[] { 1, 2 }, new[] { 2, 1 } })));   // [[0,1],[1,0]]
                    """
                },
                new()
                {
                    Name = "Climb uphill from each ocean, keep the overlap",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(m · n)",
                    SpaceComplexity = "O(m · n)",
                    Intuition = "Search from all Pacific edge cells to neighbours at least as high, then the same from the Atlantic edges; cells reached by both searches are the answer."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public IList<IList<int>> PacificAtlantic(int[][] heights)
                {
                    int rows = heights.Length, cols = heights[0].Length;
                    var pacific = new bool[rows, cols];
                    var atlantic = new bool[rows, cols];
                    for (int r = 0; r < rows; r++) { Climb(heights, r, 0, pacific); Climb(heights, r, cols - 1, atlantic); }
                    for (int c = 0; c < cols; c++) { Climb(heights, 0, c, pacific); Climb(heights, rows - 1, c, atlantic); }

                    var result = new List<IList<int>>();
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            if (pacific[r, c] && atlantic[r, c]) result.Add(new[] { r, c });
                    return result;
                }

                // Water flows downhill, so walk uphill from the ocean: every cell reached can drain into it.
                private void Climb(int[][] heights, int r, int c, bool[,] reached)
                {
                    if (reached[r, c]) return;
                    reached[r, c] = true;
                    foreach (var (nr, nc) in new[] { (r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1) })
                        if (nr >= 0 && nc >= 0 && nr < heights.Length && nc < heights[0].Length && heights[nr][nc] >= heights[r][c])
                            Climb(heights, nr, nc, reached);
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "heights = [[1,2,2,3,5],[3,2,3,4,4],[2,4,5,3,1],[6,7,1,4,5],[5,1,1,2,4]]", Expected = "[[0,4],[1,3],[1,4],[2,2],[3,0],[3,1],[4,0]]", Call = "sol.PacificAtlantic(new[] { new[] { 1, 2, 2, 3, 5 }, new[] { 3, 2, 3, 4, 4 }, new[] { 2, 4, 5, 3, 1 }, new[] { 6, 7, 1, 4, 5 }, new[] { 5, 1, 1, 2, 4 } })", AnyOrder = true },
                new() { Name = "Example 2", Input = "heights = [[1]]", Expected = "[[0,0]]", Call = "sol.PacificAtlantic(new[] { new[] { 1 } })", AnyOrder = true }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "Flat land drains everywhere", Input = "heights = [[1,1],[1,1]]", Expected = "[[0,0],[0,1],[1,0],[1,1]]", Call = "sol.PacificAtlantic(new[] { new[] { 1, 1 }, new[] { 1, 1 } })", AnyOrder = true },
                new() { Name = "A single row", Input = "heights = [[1,2,3]]", Expected = "[[0,0],[0,1],[0,2]]", Call = "sol.PacificAtlantic(new[] { new[] { 1, 2, 3 } })", AnyOrder = true },
                new() { Name = "A pit in the middle", Input = "heights = [[3,3,3],[3,1,3],[3,3,3]]", Expected = "[[0,0],[0,1],[0,2],[1,0],[1,2],[2,0],[2,1],[2,2]]", Call = "sol.PacificAtlantic(new[] { new[] { 3, 3, 3 }, new[] { 3, 1, 3 }, new[] { 3, 3, 3 } })", AnyOrder = true },
                new() { Name = "Only the corners", Input = "heights = [[1,2],[2,1]]", Expected = "[[0,1],[1,0]]", Call = "sol.PacificAtlantic(new[] { new[] { 1, 2 }, new[] { 2, 1 } })", AnyOrder = true }
            },
            StressTestCode = """
            judge.Agree("Random islands vs following the water",
                random => Enumerable.Range(0, random.Next(1, 5)).Select(_ => Enumerable.Range(0, 4).Select(_ => random.Next(0, 4)).ToArray()).ToArray(),
                heights => (object)PacificAtlanticBruteForce(heights),
                heights => sol.PacificAtlantic(heights),
                anyOrder: true);
            """,
            VisualizerKind = "Matrix",
            VisualizationDescription = """
            Example 1's heights. First the Pacific search climbs from the top and left edges (blue), then the Atlantic
            search from the bottom and right edges (amber). A cell the Atlantic search reaches that was already blue
            turns green: water from it reaches both oceans.
            """,
            VisualizationCode = """
            int[][] heights = { new[] { 1, 2, 2, 3, 5 }, new[] { 3, 2, 3, 4, 4 }, new[] { 2, 4, 5, 3, 1 }, new[] { 6, 7, 1, 4, 5 }, new[] { 5, 1, 1, 2, 4 } };
            int rows = heights.Length, cols = heights[0].Length;
            var tracker = MatrixTracker.Create(heights, "417. Pacific Atlantic: climb uphill from each ocean",
                new MatrixParseOptions { StateClassifier = _ => GridCellState.Default });
            var pacific = new bool[rows, cols];
            var atlantic = new bool[rows, cols];
            var both = new List<string>();
            tracker.Watch(both);

            void Climb(int r, int c, bool[,] reached, string ocean, string color)
            {
                if (reached[r, c]) return;
                reached[r, c] = true;
                bool reachesBoth = pacific[r, c] && atlantic[r, c];
                if (reachesBoth) both.Add($"[{r},{c}]");
                tracker.Visit(r, c, reachesBoth
                        ? $"({r},{c}) drains into the Atlantic too, so water here reaches both oceans"
                        : $"({r},{c}), height {heights[r][c]}, can drain down into the {ocean}",
                    color: reachesBoth ? "#15803d" : color);
                foreach (var (nr, nc) in new[] { (r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1) })
                    if (nr >= 0 && nc >= 0 && nr < rows && nc < cols && heights[nr][nc] >= heights[r][c])
                        Climb(nr, nc, reached, ocean, color);
            }

            tracker.Snapshot("Pacific first: climb from every cell on the top and left edges to neighbours at least as high");
            for (int r = 0; r < rows; r++) Climb(r, 0, pacific, "Pacific", "#1d4ed8");
            for (int c = 0; c < cols; c++) Climb(0, c, pacific, "Pacific", "#1d4ed8");
            tracker.Snapshot("Now the Atlantic: climb from the bottom and right edges the same way");
            for (int r = 0; r < rows; r++) Climb(r, cols - 1, atlantic, "Atlantic", "#b45309");
            for (int c = 0; c < cols; c++) Climb(rows - 1, c, atlantic, "Atlantic", "#b45309");

            tracker.Snapshot($"Green cells were reached from both oceans: {string.Join(",", both.OrderBy(x => x))}");
            Display.Visualizer(tracker);
            """
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
            Tags = new List<string> { "DFS", "BFS", "Graph", "Topological Sort" },
            TimeComplexity = "O(V + E)",
            SpaceComplexity = "O(V + E)",
            DescriptionMarkdown = """
            There are `numCourses` courses, labelled `0` to `numCourses - 1`. Each `prerequisites[i] = [a, b]` means you must take course `b` before course `a`.

            Return `true` if you can finish every course, or `false` otherwise.

            ### Example 1
            - **Input:** `numCourses = 2, prerequisites = [[1,0]]`
            - **Output:** `true`
            - **Why:** take 0, then 1.

            ### Example 2
            - **Input:** `numCourses = 2, prerequisites = [[1,0],[0,1]]`
            - **Output:** `false`
            - **Why:** 1 needs 0 and 0 needs 1, so neither can ever be started.

            ### Constraints
            - `1 <= numCourses <= 2000`
            - `0 <= prerequisites.length <= 5000`, with all pairs distinct.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** draw an arrow `b → a` for "b before a". You can finish everything exactly when these arrows contain **no cycle**.

            1. **Why cycles are the problem:** in `0 → 1 → 2 → 0`, each course waits for another one in the loop, forever. Without a loop, some course always has nothing left to wait for.
            2. **Take courses as they become available (Kahn's algorithm).** Count each course's unfinished prerequisites. Courses with a count of 0 can be taken now: put them in a queue.
            3. **Taking a course** lowers the count of every course it unlocks. Whenever a count drops to 0, that course is ready: queue it.
            4. **When the queue runs dry,** you've taken every course you'll ever be able to. If that's all of them, `true`; otherwise the rest are stuck waiting on each other, which means a cycle.
            5. **Each course and each arrow is handled once:** `O(V + E)`.

            **Another way:** depth-first search with three colours (unvisited, on the current path, done). Meeting a node that is still on the current path means a cycle.

            **Pattern to remember:** "can all tasks with dependencies be done / in what order?" is topological sorting. Counting in-degrees and peeling off the zeros also produces the order (Course Schedule II).

            **Common mistakes:** drawing the arrows backwards (you'll then count the wrong prerequisites); forgetting courses that have no prerequisites at all; treating a two-way pair as fine.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Depth-first search with three colours",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(V + E)",
                    SpaceComplexity = "O(V + E)",
                    Intuition = "Visit courses depth-first along the arrows. A course is 'in progress' while its descendants are explored; reaching an in-progress course again means you went around a loop.",
                    Code = """
                    bool CanFinishDfs(int numCourses, int[][] prerequisites)
                    {
                        var unlocks = Enumerable.Range(0, numCourses).Select(_ => new List<int>()).ToArray();
                        foreach (var p in prerequisites) unlocks[p[1]].Add(p[0]);
                        var state = new int[numCourses];   // 0 = not seen, 1 = on the current path, 2 = done
                        bool HasCycle(int course)
                        {
                            if (state[course] == 1) return true;    // back on the current path: a loop
                            if (state[course] == 2) return false;
                            state[course] = 1;
                            if (unlocks[course].Any(HasCycle)) return true;
                            state[course] = 2;
                            return false;
                        }
                        return !Enumerable.Range(0, numCourses).Any(HasCycle);
                    }

                    Console.WriteLine(Judge.Format(CanFinishDfs(2, new[] { new[] { 1, 0 }, new[] { 0, 1 } })));   // false
                    """
                },
                new()
                {
                    Name = "Kahn's algorithm: take courses whose prerequisites are done",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(V + E)",
                    SpaceComplexity = "O(V + E)",
                    Intuition = "Count unfinished prerequisites per course, start with the zeros, and every course taken lowers its dependants' counts. All courses taken ⇔ no cycle."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool CanFinish(int numCourses, int[][] prerequisites)
                {
                    var unlocks = new List<int>[numCourses];              // course -> courses that need it
                    var waitingOn = new int[numCourses];                   // unfinished prerequisites per course
                    for (int i = 0; i < numCourses; i++) unlocks[i] = new List<int>();
                    foreach (var p in prerequisites)
                    {
                        unlocks[p[1]].Add(p[0]);
                        waitingOn[p[0]]++;
                    }

                    var ready = new Queue<int>(Enumerable.Range(0, numCourses).Where(c => waitingOn[c] == 0));
                    int taken = 0;
                    while (ready.Count > 0)
                    {
                        int course = ready.Dequeue();
                        taken++;
                        foreach (int next in unlocks[course])
                            if (--waitingOn[next] == 0) ready.Enqueue(next);   // its last prerequisite is done
                    }
                    return taken == numCourses;                             // anything left is stuck on a cycle
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "numCourses = 2, prerequisites = [[1,0]]", Expected = "true", Call = "sol.CanFinish(2, new[] { new[] { 1, 0 } })" },
                new() { Name = "Example 2", Input = "numCourses = 2, prerequisites = [[1,0],[0,1]]", Expected = "false", Call = "sol.CanFinish(2, new[] { new[] { 1, 0 }, new[] { 0, 1 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "No prerequisites", Input = "numCourses = 3, prerequisites = []", Expected = "true", Call = "sol.CanFinish(3, new int[0][])" },
                new() { Name = "A chain", Input = "numCourses = 4, prerequisites = [[1,0],[2,1],[3,2]]", Expected = "true", Call = "sol.CanFinish(4, new[] { new[] { 1, 0 }, new[] { 2, 1 }, new[] { 3, 2 } })" },
                new() { Name = "A loop of three", Input = "numCourses = 3, prerequisites = [[0,1],[1,2],[2,0]]", Expected = "false", Call = "sol.CanFinish(3, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 } })" },
                new() { Name = "A course that needs itself", Input = "numCourses = 1, prerequisites = [[0,0]]", Expected = "false", Call = "sol.CanFinish(1, new[] { new[] { 0, 0 } })" },
                new() { Name = "A loop off to the side", Input = "numCourses = 4, prerequisites = [[1,0],[2,3],[3,2]]", Expected = "false", Call = "sol.CanFinish(4, new[] { new[] { 1, 0 }, new[] { 2, 3 }, new[] { 3, 2 } })" }
            },
            StressTestCode = """
            judge.Agree("Random prerequisites vs three-colour DFS",
                random =>
                {
                    int n = random.Next(1, 7);
                    var pairs = Enumerable.Range(0, random.Next(0, 8)).Select(_ => new[] { random.Next(n), random.Next(n) })
                        .GroupBy(p => (p[0], p[1])).Select(g => g.First()).ToArray();
                    return (n, pairs);
                },
                input => CanFinishDfs(input.n, input.pairs),
                input => sol.CanFinish(input.n, input.pairs));
            """,
            VisualizerKind = "Graph",
            VisualizationDescription = """
            Six courses; an arrow `b → a` means b comes before a, and each label counts the prerequisites a course still
            waits on. Courses with nothing left to wait on enter the queue; taking one lowers the counts it points to.
            Courses 4 and 5 need each other, so they never reach 0 and end up red: a cycle, so the answer is false.
            """,
            VisualizationCode = """
            int numCourses = 6;
            int[][] prerequisites = { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 1 }, new[] { 3, 2 }, new[] { 4, 3 }, new[] { 4, 5 }, new[] { 5, 4 } };
            var unlocks = new Dictionary<int, List<int>>();
            var waitingOn = new int[numCourses];
            for (int c = 0; c < numCourses; c++) unlocks[c] = new List<int>();
            foreach (var p in prerequisites)
            {
                unlocks[p[1]].Add(p[0]);
                waitingOn[p[0]]++;
            }

            var tracker = GraphTracker.Create(unlocks, "207. Course Schedule: take whatever has no unfinished prerequisites", isDirected: true);
            for (int c = 0; c < numCourses; c++) tracker.Annotate(c, $"needs {waitingOn[c]}");
            var ready = new Queue<int>(Enumerable.Range(0, numCourses).Where(c => waitingOn[c] == 0));
            foreach (int c in ready) tracker.Mark(c, GraphNodeState.Frontier);
            tracker.Watch(ready);
            tracker.Snapshot($"b → a means b comes before a. Only {string.Join(", ", ready)} need nothing, so they go in the queue");

            int taken = 0;
            while (ready.Count > 0)
            {
                int course = ready.Dequeue();
                taken++;
                tracker.Visit(course, $"Take course {course} ({taken} of {numCourses} done)", subLabel: "taken");
                foreach (int next in unlocks[course])
                {
                    waitingOn[next]--;
                    tracker.MarkEdge(course, next, GraphEdgeState.Visited);
                    tracker.Annotate(next, $"needs {waitingOn[next]}");
                    if (waitingOn[next] == 0)
                    {
                        ready.Enqueue(next);
                        tracker.Mark(next, GraphNodeState.Frontier);
                    }
                    tracker.Snapshot(waitingOn[next] == 0
                        ? $"{course} was the last thing {next} waited on: {next} can be taken, queue it"
                        : $"{next} still waits on {waitingOn[next]} more");
                }
            }

            var stuck = Enumerable.Range(0, numCourses).Where(c => waitingOn[c] > 0).ToList();
            tracker.ClearCurrent();
            foreach (int c in stuck) tracker.Mark(c, GraphNodeState.Cycle);
            tracker.Snapshot(stuck.Count == 0
                ? $"All {numCourses} courses were taken: true"
                : $"The queue is empty but {string.Join(" and ", stuck)} still wait on each other: they form a cycle, so the answer is false");
            Display.Visualizer(tracker);
            """
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
            Tags = new List<string> { "DFS", "BFS", "Union Find", "Graph" },
            TimeComplexity = "O(n + E · α(n)) ≈ O(n)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            You are given `n` nodes labelled `0` to `n - 1` and a list of undirected `edges`. Return `true` if these edges make up a **valid tree**: every node is connected, and there are no cycles.

            ### Example 1
            - **Input:** `n = 5, edges = [[0,1],[0,2],[0,3],[1,4]]`
            - **Output:** `true`

            ### Example 2
            - **Input:** `n = 5, edges = [[0,1],[1,2],[2,3],[1,3],[1,4]]`
            - **Output:** `false`
            - **Why:** 1–2–3–1 is a cycle.

            ### Constraints
            - `1 <= n <= 2000`
            - `0 <= edges.length <= 5000`
            - No repeated edges and no self-loops.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** a tree = connected + no cycles. Check both, cheaply.

            1. **Count first.** A tree on `n` nodes has exactly `n - 1` edges. Fewer can't connect everything; more must create a cycle. So `edges.Length != n - 1` → `false` straight away.
            2. **With exactly `n - 1` edges, "no cycle" is enough:** if the edges formed no cycle, they would join the nodes into one piece (each edge without a cycle merges two pieces, and `n - 1` merges leave one).
            3. **Detect a cycle with union-find:** start with every node in its own group. For each edge, find the groups of its two ends. Different groups → join them. **Same** group → the ends were already connected, so this edge closes a cycle.
            4. **Walk `[[0,1],[1,2],[2,0],[3,4]]` with n = 5:** 4 edges ✓. 0–1 joins, 1–2 joins, 2–0: already in one group → cycle → `false` (and indeed 3–4 is cut off from the rest).

            **Another way:** depth-first search from node 0, remembering each node's parent; reaching an already-visited node other than the parent is a cycle; at the end, all `n` nodes must have been visited.

            **Pattern to remember:** union-find answers "are these two already connected?" in near-constant time, which is exactly cycle detection in an undirected graph.

            **Common mistakes:** forgetting the edge count (a cycle plus a separate node passes a pure cycle check only if you also check connectivity); treating the edge back to the parent as a cycle in DFS.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Depth-first search: no revisits, everything reached",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n + E)",
                    SpaceComplexity = "O(n + E)",
                    Intuition = "Explore from node 0 remembering where you came from. Meeting a visited node that isn't your parent means a cycle; missing nodes at the end mean it isn't connected.",
                    Code = """
                    bool ValidTreeDfs(int n, int[][] edges)
                    {
                        var neighbours = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
                        foreach (var e in edges) { neighbours[e[0]].Add(e[1]); neighbours[e[1]].Add(e[0]); }
                        var visited = new HashSet<int>();
                        bool NoCycle(int node, int parent)
                        {
                            visited.Add(node);
                            foreach (int next in neighbours[node])
                            {
                                if (next == parent) continue;
                                if (visited.Contains(next) || !NoCycle(next, node)) return false;
                            }
                            return true;
                        }
                        return NoCycle(0, -1) && visited.Count == n;
                    }

                    Console.WriteLine(Judge.Format(ValidTreeDfs(5, new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 4 } })));   // true
                    """
                },
                new()
                {
                    Name = "Edge count + union-find",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "≈ O(n)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Require exactly n − 1 edges, then union the ends of each edge; an edge whose ends are already in one group closes a cycle."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public bool ValidTree(int n, int[][] edges)
                {
                    if (edges.Length != n - 1) return false;         // a tree on n nodes has exactly n - 1 edges

                    var parent = Enumerable.Range(0, n).ToArray();   // every node starts as its own group
                    int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
                    foreach (var e in edges)
                    {
                        int a = Find(e[0]), b = Find(e[1]);
                        if (a == b) return false;                      // already connected: this edge closes a cycle
                        parent[a] = b;                                 // join the two groups
                    }
                    return true;                                       // n - 1 edges and no cycle: one connected tree
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 5, edges = [[0,1],[0,2],[0,3],[1,4]]", Expected = "true", Call = "sol.ValidTree(5, new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 4 } })" },
                new() { Name = "Example 2", Input = "n = 5, edges = [[0,1],[1,2],[2,3],[1,3],[1,4]]", Expected = "false", Call = "sol.ValidTree(5, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 1, 3 }, new[] { 1, 4 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "One node", Input = "n = 1, edges = []", Expected = "true", Call = "sol.ValidTree(1, new int[0][])" },
                new() { Name = "Two separate pieces", Input = "n = 4, edges = [[0,1],[2,3]]", Expected = "false", Call = "sol.ValidTree(4, new[] { new[] { 0, 1 }, new[] { 2, 3 } })" },
                new() { Name = "Right count, but a cycle", Input = "n = 4, edges = [[0,1],[1,2],[2,0]]", Expected = "false", Call = "sol.ValidTree(4, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 } })" },
                new() { Name = "A straight line", Input = "n = 3, edges = [[0,1],[1,2]]", Expected = "true", Call = "sol.ValidTree(3, new[] { new[] { 0, 1 }, new[] { 1, 2 } })" }
            },
            StressTestCode = """
            judge.Agree("Random graphs vs depth-first search",
                random =>
                {
                    int n = random.Next(1, 7);
                    var edges = new HashSet<(int, int)>();
                    for (int k = random.Next(0, n + 1); k > 0; k--)
                    {
                        int a = random.Next(n), b = random.Next(n);
                        if (a != b) edges.Add((Math.Min(a, b), Math.Max(a, b)));
                    }
                    return (n, edges: edges.Select(e => new[] { e.Item1, e.Item2 }).ToArray());
                },
                input => ValidTreeDfs(input.n, input.edges),
                input => sol.ValidTree(input.n, input.edges));
            """,
            VisualizerKind = "Graph",
            VisualizationDescription = """
            `n = 5` with edges `[[0,1],[1,2],[2,0],[3,4]]`: the right number of edges for a tree, but not a tree. Each edge
            joins two groups (a group gets its own colour, listed in `groups`). The edge 2–0 finds both ends already in
            the same group, so it closes a cycle and turns red.
            """,
            VisualizationCode = """
            int n = 5;
            int[][] edges = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 }, new[] { 3, 4 } };
            var drawing = Enumerable.Range(0, n).ToDictionary(v => v, v => edges.Where(e => e[0] == v).Select(e => e[1]).ToList());
            var tracker = GraphTracker.Create(drawing, "261. Graph Valid Tree: n − 1 edges and no cycle", isDirected: false);
            string[] palette = { "#0f766e", "#7c3aed", "#c2410c", "#be185d", "#15803d" };

            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
            string groups = "";
            tracker.Watch(() => groups);
            void Repaint()
            {
                var byRoot = Enumerable.Range(0, n).GroupBy(Find).ToList();
                groups = string.Join(" ", byRoot.Select(g => "{" + string.Join(",", g) + "}"));
                foreach (var group in byRoot)
                    foreach (int v in group) tracker.Paint(v, group.Count() > 1 ? palette[group.Key % palette.Length] : null);
            }

            Repaint();
            tracker.Snapshot($"{edges.Length} edges for {n} nodes: exactly n − 1, as a tree needs. Now make sure no edge closes a cycle");
            bool valid = edges.Length == n - 1;
            foreach (var e in edges)
            {
                int a = Find(e[0]), b = Find(e[1]);
                if (a == b)
                {
                    tracker.MarkEdge(e[0], e[1], GraphEdgeState.Rejected);
                    tracker.Snapshot($"Edge {e[0]}–{e[1]}: both ends are already in the same group, so this edge closes a cycle. Not a tree");
                    valid = false;
                    break;
                }
                parent[a] = b;
                Repaint();
                tracker.MarkEdge(e[0], e[1], GraphEdgeState.Visited);
                tracker.Snapshot($"Edge {e[0]}–{e[1]}: the ends are in different groups, so join them");
            }

            if (valid) tracker.Snapshot("Every edge joined two separate groups: one tree, true");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(n + E · α(n)) ≈ O(n + E)",
            SpaceComplexity = "O(n)",
            DescriptionMarkdown = """
            You have `n` nodes labelled `0` to `n - 1` and a list of undirected `edges`. Return the number of **connected components**: groups of nodes that are connected to each other, directly or through other nodes.

            ### Example 1
            - **Input:** `n = 5, edges = [[0,1],[1,2],[3,4]]`
            - **Output:** `2`
            - **Why:** `{0, 1, 2}` and `{3, 4}`.

            ### Example 2
            - **Input:** `n = 5, edges = [[0,1],[1,2],[2,3],[3,4]]`
            - **Output:** `1`

            ### Constraints
            - `1 <= n <= 2000`
            - `1 <= edges.length <= 5000`
            - No repeated edges and no self-loops.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** how many separate pieces does the graph fall into?

            1. **Start with `n` pieces:** every node alone.
            2. **Each edge can glue two pieces together.** Keep the pieces in a union-find structure: `Find(x)` gives the representative of x's piece. For an edge `a–b`, if `Find(a) != Find(b)`, join them and the count goes down by one. If they're already in the same piece, the edge changes nothing.
            3. **The answer is `n` minus the number of successful joins.** Isolated nodes simply stay pieces of their own.
            4. **Keep it fast:** path compression (point nodes straight at their representative while finding) makes each operation nearly constant time.
            5. **Walk Example 1:** 5 pieces. 0–1 → 4. 1–2 → 3. 3–4 → 2.

            **Another way:** depth-first search: loop over the nodes, and every node not yet visited starts a new component that the search then colours in (like Number of Islands on a graph).

            **Pattern to remember:** counting groups under "is connected to" = union-find (or a flood fill). Redundant edges are exactly the joins that fail.

            **Common mistakes:** counting edges instead of pieces; forgetting isolated nodes; decrementing the count for an edge inside one piece.
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Depth-first search from every unvisited node",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(n + E)",
                    SpaceComplexity = "O(n + E)",
                    Intuition = "Build the adjacency list. Each node not yet visited starts a new component; a search from it visits the whole component.",
                    Code = """
                    int CountComponentsDfs(int n, int[][] edges)
                    {
                        var neighbours = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
                        foreach (var e in edges) { neighbours[e[0]].Add(e[1]); neighbours[e[1]].Add(e[0]); }
                        var visited = new bool[n];
                        void Visit(int node)
                        {
                            if (visited[node]) return;
                            visited[node] = true;
                            foreach (int next in neighbours[node]) Visit(next);
                        }
                        int components = 0;
                        for (int node = 0; node < n; node++)
                            if (!visited[node]) { components++; Visit(node); }
                        return components;
                    }

                    Console.WriteLine(Judge.Format(CountComponentsDfs(5, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 3, 4 } })));   // 2
                    """
                },
                new()
                {
                    Name = "Union-find: every successful join removes a piece",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "≈ O(n + E)",
                    SpaceComplexity = "O(n)",
                    Intuition = "Start with n pieces; for each edge, join the pieces of its ends if they differ. The number of pieces left is the answer."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public int CountComponents(int n, int[][] edges)
                {
                    var parent = Enumerable.Range(0, n).ToArray();                     // every node is its own piece
                    int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);   // with path compression

                    int components = n;
                    foreach (var e in edges)
                    {
                        int a = Find(e[0]), b = Find(e[1]);
                        if (a == b) continue;                                          // already the same piece
                        parent[a] = b;                                                 // glue two pieces together
                        components--;
                    }
                    return components;
                }
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "n = 5, edges = [[0,1],[1,2],[3,4]]", Expected = "2", Call = "sol.CountComponents(5, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 3, 4 } })" },
                new() { Name = "Example 2", Input = "n = 5, edges = [[0,1],[1,2],[2,3],[3,4]]", Expected = "1", Call = "sol.CountComponents(5, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 } })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "No edges", Input = "n = 3, edges = []", Expected = "3", Call = "sol.CountComponents(3, new int[0][])" },
                new() { Name = "A redundant edge", Input = "n = 3, edges = [[0,1],[1,2],[2,0]]", Expected = "1", Call = "sol.CountComponents(3, new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 } })" },
                new() { Name = "Isolated nodes count", Input = "n = 6, edges = [[0,1],[4,5]]", Expected = "4", Call = "sol.CountComponents(6, new[] { new[] { 0, 1 }, new[] { 4, 5 } })" },
                new() { Name = "Joined through the middle", Input = "n = 4, edges = [[0,1],[2,3],[1,2]]", Expected = "1", Call = "sol.CountComponents(4, new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 1, 2 } })" }
            },
            StressTestCode = """
            judge.Agree("Random graphs vs depth-first search",
                random =>
                {
                    int n = random.Next(1, 9);
                    var edges = new HashSet<(int, int)>();
                    for (int k = random.Next(0, n + 2); k > 0; k--)
                    {
                        int a = random.Next(n), b = random.Next(n);
                        if (a != b) edges.Add((Math.Min(a, b), Math.Max(a, b)));
                    }
                    return (n, edges: edges.Select(e => new[] { e.Item1, e.Item2 }).ToArray());
                },
                input => CountComponentsDfs(input.n, input.edges),
                input => sol.CountComponents(input.n, input.edges));
            """,
            VisualizerKind = "Graph",
            VisualizationDescription = """
            Six nodes with edges `[[0,1],[1,2],[2,0],[3,4]]`. Each piece with more than one node gets a colour, and
            `components` counts the pieces. A join lowers the count; the edge 2–0 connects two nodes that are already in
            one piece, so nothing changes; node 5 has no edges and stays a piece of its own.
            """,
            VisualizationCode = """
            int n = 6;
            int[][] edges = { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 0 }, new[] { 3, 4 } };
            var drawing = Enumerable.Range(0, n).ToDictionary(v => v, v => edges.Where(e => e[0] == v).Select(e => e[1]).ToList());
            var tracker = GraphTracker.Create(drawing, "323. Connected Components: every successful join removes a piece", isDirected: false);
            string[] palette = { "#0f766e", "#7c3aed", "#c2410c", "#be185d", "#15803d", "#1d4ed8" };

            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
            int components = n;
            tracker.Watch(() => components);
            void Repaint()
            {
                foreach (var group in Enumerable.Range(0, n).GroupBy(Find))
                    foreach (int v in group) tracker.Paint(v, group.Count() > 1 ? palette[group.Key % palette.Length] : null);
            }

            tracker.Snapshot($"Start with {n} pieces: every node on its own");
            foreach (var e in edges)
            {
                int a = Find(e[0]), b = Find(e[1]);
                if (a == b)
                {
                    tracker.MarkEdge(e[0], e[1], GraphEdgeState.Rejected);
                    tracker.Snapshot($"Edge {e[0]}–{e[1]}: both ends are already in one piece, so the count stays {components}");
                    continue;
                }
                parent[a] = b;
                components--;
                Repaint();
                tracker.MarkEdge(e[0], e[1], GraphEdgeState.Visited);
                tracker.Snapshot($"Edge {e[0]}–{e[1]} glues two pieces together: {components} pieces left");
            }

            tracker.Snapshot($"All edges are used: {components} connected components (node 5 has no edges, so it is one by itself)");
            Display.Visualizer(tracker);
            """
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
            TimeComplexity = "O(C) where C is the total number of letters in words",
            SpaceComplexity = "O(1): at most 26 letters and 26² rules",
            DescriptionMarkdown = """
            An alien language uses lowercase English letters in an unknown order. You get a list of `words` that is **sorted** in that alien order.

            Return a string of all the letters that appear, in an order consistent with the list. If no order is possible, return `""`. When several orders work, this solution returns the alphabetically smallest one (LeetCode accepts any).

            ### Example 1
            - **Input:** `words = ["wrt","wrf","er","ett","rftt"]`
            - **Output:** `"wertf"`
            - **Why:** `wrt < wrf` gives t before f, `wrf < er` gives w before e, `er < ett` gives r before t, `ett < rftt` gives e before r.

            ### Example 2
            - **Input:** `words = ["z","x"]`
            - **Output:** `"zx"`

            ### Example 3
            - **Input:** `words = ["z","x","z"]`
            - **Output:** `""`
            - **Why:** z before x and x before z can't both be true.

            ### Constraints
            - `1 <= words.length <= 100`
            - `1 <= words[i].length <= 100`, lowercase letters only.
            """,
            ThinkingProcessMarkdown = """
            **Restate it:** read letter-order rules out of the sorted list, then put the letters in an order that obeys every rule.

            1. **Where the rules come from:** compare **neighbouring** words. Like in a real dictionary, the first position where they differ decides their order: in `wrt` vs `wrf`, `t` comes before `f`. Letters after that position tell you nothing.
            2. **One impossible case:** if a word is a longer version of the next one (`"abc"` before `"ab"`), no alphabet can sort them that way → `""`.
            3. **Rules are arrows** `t → f` between letters. An order obeying all arrows is a **topological sort** (as in Course Schedule, problem 207): repeatedly output a letter no remaining arrow points to, and remove its arrows.
            4. **A cycle means contradictory rules** (Example 3: `z → x → z`): some letters never become free, so not every letter gets output → `""`.
            5. **Walk Example 1:** rules t→f, w→e, r→t, e→r. Only `w` has nothing before it → w, then e, r, t, f: `"wertf"`.

            **Pattern to remember:** "derive an order from pairwise comparisons" = build a graph of rules + topological sort; detect contradictions as cycles.

            **Common mistakes:** comparing non-adjacent words or all positions; missing the prefix case; forgetting letters that appear in no rule (they still belong in the answer); adding the same rule twice (double-counting in-degrees).
            """,
            Approaches = new List<ProblemApproachItem>
            {
                new()
                {
                    Name = "Depth-first topological sort",
                    Kind = ApproachKind.Alternative,
                    TimeComplexity = "O(C)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Collect the same rules, then run DFS: a letter is added after everything that must come after it, and the list is reversed at the end. Meeting a letter still on the current path means a contradiction.",
                    Code = """
                    string AlienOrderDfs(string[] words)
                    {
                        var after = new Dictionary<char, HashSet<char>>();
                        foreach (var word in words) foreach (char c in word) after.TryAdd(c, new HashSet<char>());
                        for (int i = 0; i + 1 < words.Length; i++)
                        {
                            string first = words[i], second = words[i + 1];
                            int k = 0;
                            while (k < first.Length && k < second.Length && first[k] == second[k]) k++;
                            if (k == Math.Min(first.Length, second.Length)) { if (first.Length > second.Length) return ""; continue; }
                            after[first[k]].Add(second[k]);
                        }

                        var state = new Dictionary<char, int>();   // 1 = on the current path, 2 = done
                        var reversed = new List<char>();
                        bool Visit(char letter)
                        {
                            if (state.TryGetValue(letter, out int s)) return s == 2;   // 1: a loop
                            state[letter] = 1;
                            if (!after[letter].All(Visit)) return false;
                            state[letter] = 2;
                            reversed.Add(letter);
                            return true;
                        }
                        if (!after.Keys.All(Visit)) return "";
                        reversed.Reverse();
                        return new string(reversed.ToArray());
                    }

                    Console.WriteLine(AlienOrderDfs(new[] { "wrt", "wrf", "er", "ett", "rftt" }));   // wertf
                    """
                },
                new()
                {
                    Name = "Rules from neighbouring words + Kahn's topological sort",
                    Kind = ApproachKind.Optimal,
                    TimeComplexity = "O(C)",
                    SpaceComplexity = "O(1)",
                    Intuition = "Each pair of neighbouring words gives at most one rule: the first letters that differ. Then output letters with no unmet rules, freeing the letters they point to."
                }
            },
            SolutionCode = """
            public class Solution
            {
                public string AlienOrder(string[] words)
                {
                    var after = new Dictionary<char, HashSet<char>>();    // letter -> letters that must come after it
                    var waitingOn = new Dictionary<char, int>();           // letter -> how many letters must come first
                    foreach (var word in words)
                        foreach (char c in word)
                        {
                            after.TryAdd(c, new HashSet<char>());
                            waitingOn.TryAdd(c, 0);
                        }

                    for (int i = 0; i + 1 < words.Length; i++)
                    {
                        string first = words[i], second = words[i + 1];
                        int k = 0;
                        while (k < first.Length && k < second.Length && first[k] == second[k]) k++;
                        if (k == Math.Min(first.Length, second.Length))
                        {
                            if (first.Length > second.Length) return "";   // "abc" before "ab": impossible
                            continue;                                        // a prefix tells us nothing
                        }
                        if (after[first[k]].Add(second[k])) waitingOn[second[k]]++;   // the first difference decides
                    }

                    // Kahn's topological sort; the smallest free letter goes first so the answer is predictable.
                    var ready = new SortedSet<char>(waitingOn.Where(pair => pair.Value == 0).Select(pair => pair.Key));
                    var order = new StringBuilder();
                    while (ready.Count > 0)
                    {
                        char letter = ready.Min;
                        ready.Remove(letter);
                        order.Append(letter);
                        foreach (char next in after[letter])
                            if (--waitingOn[next] == 0) ready.Add(next);
                    }
                    return order.Length == waitingOn.Count ? order.ToString() : "";   // letters left over sit on a cycle
                }
            }
            """,
            TestSetupCode = """
            // An order is valid when it lists every letter once and sorts the words exactly as given.
            bool IsConsistent(string[] words, string order)
            {
                var letters = words.SelectMany(w => w).Distinct().ToList();
                if (order.Length != letters.Count || !letters.All(order.Contains)) return false;
                int Compare(string x, string y)
                {
                    for (int k = 0; k < Math.Min(x.Length, y.Length); k++)
                        if (x[k] != y[k]) return order.IndexOf(x[k]).CompareTo(order.IndexOf(y[k]));
                    return x.Length.CompareTo(y.Length);
                }
                return Enumerable.Range(0, words.Length - 1).All(i => Compare(words[i], words[i + 1]) <= 0);
            }
            """,
            Tests = new List<BlindTest>
            {
                new() { Name = "Example 1", Input = "words = [\"wrt\",\"wrf\",\"er\",\"ett\",\"rftt\"]", Expected = "\"wertf\"", Call = "sol.AlienOrder(new[] { \"wrt\", \"wrf\", \"er\", \"ett\", \"rftt\" })" },
                new() { Name = "Example 2", Input = "words = [\"z\",\"x\"]", Expected = "\"zx\"", Call = "sol.AlienOrder(new[] { \"z\", \"x\" })" },
                new() { Name = "Example 3", Input = "words = [\"z\",\"x\",\"z\"]", Expected = "\"\"", Call = "sol.AlienOrder(new[] { \"z\", \"x\", \"z\" })" }
            },
            ExtraTests = new List<BlindTest>
            {
                new() { Name = "A longer word before its prefix", Input = "words = [\"abc\",\"ab\"]", Expected = "\"\"", Call = "sol.AlienOrder(new[] { \"abc\", \"ab\" })" },
                new() { Name = "One word", Input = "words = [\"z\"]", Expected = "\"z\"", Call = "sol.AlienOrder(new[] { \"z\" })" },
                new() { Name = "Letters without rules still count", Input = "words = [\"ab\",\"adc\"]", Expected = "\"abcd\"", Call = "sol.AlienOrder(new[] { \"ab\", \"adc\" })" },
                new() { Name = "The same word twice", Input = "words = [\"x\",\"x\"]", Expected = "\"x\"", Call = "sol.AlienOrder(new[] { \"x\", \"x\" })" },
                new() { Name = "Example 1's order is consistent", Input = "words = [\"wrt\",\"wrf\",\"er\",\"ett\",\"rftt\"]", Expected = "true", Call = "IsConsistent(new[] { \"wrt\", \"wrf\", \"er\", \"ett\", \"rftt\" }, sol.AlienOrder(new[] { \"wrt\", \"wrf\", \"er\", \"ett\", \"rftt\" }))" }
            },
            StressTestCode = """
            judge.Agree("Word lists sorted by a random alphabet: the order found sorts them the same way",
                random =>
                {
                    var alphabet = new string("abcde".OrderBy(_ => random.Next()).ToArray());
                    string Word() => new string(Enumerable.Range(0, random.Next(1, 4)).Select(_ => alphabet[random.Next(5)]).ToArray());
                    return Enumerable.Range(0, random.Next(1, 7)).Select(_ => Word())
                        .OrderBy(w => w, Comparer<string>.Create((x, y) =>
                        {
                            for (int k = 0; k < Math.Min(x.Length, y.Length); k++)
                                if (x[k] != y[k]) return alphabet.IndexOf(x[k]).CompareTo(alphabet.IndexOf(y[k]));
                            return x.Length.CompareTo(y.Length);
                        }))
                        .ToArray();
                },
                words => true,
                words => IsConsistent(words, sol.AlienOrder(words)) && IsConsistent(words, AlienOrderDfs(words)));
            """,
            VisualizerKind = "Graph",
            VisualizationDescription = """
            Example 1. First each pair of neighbouring words is compared and the rule from their first difference is
            drawn as an arrow. Then Kahn's algorithm outputs a letter nothing points to (`ready`), removes its arrows,
            and repeats; `order` grows to `"wertf"`.
            """,
            VisualizationCode = """
            string[] words = { "wrt", "wrf", "er", "ett", "rftt" };
            var letters = words.SelectMany(w => w).Distinct().ToList();
            var after = letters.ToDictionary(c => c, c => new HashSet<char>());
            var waitingOn = letters.ToDictionary(c => c, c => 0);
            var tracker = GraphTracker.Create(letters.ToDictionary(c => c, c => new List<char>()), "269. Alien Dictionary: rules from neighbours, then a topological sort", isDirected: true);
            tracker.Snapshot($"The letters used are {string.Join(", ", letters)}. Compare each word with the next one to find rules");

            for (int i = 0; i + 1 < words.Length; i++)
            {
                string first = words[i], second = words[i + 1];
                int k = 0;
                while (k < first.Length && k < second.Length && first[k] == second[k]) k++;
                if (k == Math.Min(first.Length, second.Length)) continue;
                if (after[first[k]].Add(second[k])) waitingOn[second[k]]++;
                tracker.AddEdge(first[k], second[k]);
                tracker.MarkEdge(first[k], second[k], GraphEdgeState.Active);
                tracker.Snapshot($"\"{first}\" before \"{second}\": they first differ at position {k}, so '{first[k]}' comes before '{second[k]}'");
                tracker.MarkEdge(first[k], second[k], GraphEdgeState.Default);
            }

            foreach (var (letter, count) in waitingOn) tracker.Annotate(letter, $"after {count}");
            var ready = new SortedSet<char>(waitingOn.Where(pair => pair.Value == 0).Select(pair => pair.Key));
            string order = "";
            tracker.Watch(ready);
            tracker.Watch(() => order);
            tracker.Snapshot("Now the topological sort: a letter can go next once no remaining rule puts anything before it");

            while (ready.Count > 0)
            {
                char letter = ready.Min;
                ready.Remove(letter);
                order += letter;
                foreach (char next in after[letter])
                {
                    tracker.MarkEdge(letter, next, GraphEdgeState.Visited);
                    tracker.Annotate(next, $"after {--waitingOn[next]}");
                    if (waitingOn[next] == 0) ready.Add(next);
                }
                tracker.Visit(letter, $"'{letter}' has nothing left before it: write it down. Its rules are used up", subLabel: $"#{order.Length}");
            }

            tracker.ClearCurrent();
            tracker.Snapshot(order.Length == letters.Count ? $"Every letter is placed: \"{order}\"" : "Some letters are stuck on a cycle: \"\"");
            Display.Visualizer(tracker);
            """
        }
    };
}
