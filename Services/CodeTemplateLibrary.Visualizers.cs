using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetVisualizerTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "data_structure_number_of_islands",
            Title = "200. Number of Islands (BFS Time-Travel)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Step-by-step BFS island discovery with scrubber slider, active cell pulse, and cluster analysis.",
            IconKind = MaterialIconKind.Island,
            AccentColor = "#10b981",
            AccentBackground = "#132b21",
            AccentBorder = "#059669",
            CategoryBadge = "LeetCode 200 • Medium",
            Tags = new List<string> { "BFS", "Matrix", "Islands", "Visualizer" },
            Notes = @"# 200. Number of Islands

Given an `m x n` 2D binary grid `grid` which represents a map of `'1'`s (land) and `'0'`s (water), return the number of islands.

An **island** is surrounded by water and is formed by connecting adjacent lands horizontally or vertically. You may assume all four edges of the grid are all surrounded by water.

### Time-Travel Playback:
- Drag the **scrubber slider** at the bottom to watch the BFS queue expand cell-by-cell.
- Click `Play / Pause` (`Space`) to watch the automated discovery animation.
- Hover on any cell to inspect its coordinates and island cluster assignment.",
            InitialCode = @"// LeetCode 200: Number of Islands with Step-by-Step Time-Travel Playback
char[][] grid = new char[][] {
    new char[] {'1','1','0','0','0'},
    new char[] {'1','1','0','0','0'},
    new char[] {'0','0','1','0','0'},
    new char[] {'0','0','0','1','1'}
};

// 1. Automatically runs BFS island detection and generates time-travel steps
Display.Islands(grid, title: ""Number of Islands (3 Found)"", recordSteps: true);

Console.WriteLine(""Use the playback controls at the bottom of the visualizer to step back and forth!"");"
        },
        new()
        {
            Id = "binary_search_two_pointers",
            Title = "Binary Search & Two-Pointer Stepping",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Script,
            Description = "Visualizes binary search with Left, Right, and Mid pointer markers step-by-step.",
            IconKind = MaterialIconKind.FormatListNumbered,
            AccentColor = "#38bdf8",
            AccentBackground = "#0c2838",
            AccentBorder = "#0284c7",
            CategoryBadge = "Search • Two-Pointers",
            Tags = new List<string> { "Binary Search", "Pointers", "Arrays" },
            Notes = @"# Binary Search Visualizer

Search a sorted array by repeatedly dividing the search interval in half.
Use `VisualizerRecorder` to step through the search and highlight `Left`, `Right`, and `Mid` pointers.",
            InitialCode = @"var nums = new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 };
int target = 13;

var recorder = VisualizerRecorder.CreateArray(
    ArrayPointerDataParser.Parse(nums),
    title: $""Binary Search for Target: {target}"");

int left = 0, right = nums.Length - 1;
while (left <= right)
{
    int mid = left + (right - left) / 2;
    recorder.Step($""Examine mid={mid} (Val={nums[mid]}). Target={target}"",
        pointers: new { Left = left, Right = right, Mid = mid });

    if (nums[mid] == target)
    {
        recorder.Step($""Found target {target} at index {mid}!"",
            pointers: new { Target = mid });
        break;
    }

    if (nums[mid] < target)
        left = mid + 1;
    else
        right = mid - 1;
}

Display.Visualizer(recorder);
Console.WriteLine(""Binary search completed. Scrub back and forth to inspect search partitions."");"
        },
        new()
        {
            Id = "binary_tree_traversals",
            Title = "Binary Search Tree (In-Order Playback)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Visualize In-Order, Pre-Order, and Level-Order traversals on hierarchical trees with Buchheim layout.",
            IconKind = MaterialIconKind.FamilyTree,
            AccentColor = "#a855f7",
            AccentBackground = "#28153c",
            AccentBorder = "#7c3aed",
            CategoryBadge = "Trees • Hierarchical",
            Tags = new List<string> { "Trees", "BST", "Traversal", "InOrder" },
            Notes = @"# Binary Tree Traversal Studio

Demonstrates non-overlapping Buchheim tree coordinate computation and time-travel step playback.",
            InitialCode = @"public class TreeNode
{
    public int val;
    public TreeNode? left;
    public TreeNode? right;

    public TreeNode(int v, TreeNode? l = null, TreeNode? r = null)
    {
        val = v;
        left = l;
        right = r;
    }
}

// Construct a balanced Binary Search Tree
var root = new TreeNode(4,
    new TreeNode(2, new TreeNode(1), new TreeNode(3)),
    new TreeNode(6, new TreeNode(5), new TreeNode(7))
);

// Visualizes tree and steps through In-Order traversal (1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7)
Display.Tree(root, title: ""Binary Search Tree (In-Order Traversal)"", recordTraversal: ""InOrder"");
Console.WriteLine(""Stepping through In-Order traversal with glowing active node halo."");"
        },
        new()
        {
            Id = "native_vector_charts",
            Title = "Interactive Native Charts & Analytics",
            Category = "Charting",
            Kind = WorkspaceItemKind.Notebook,
            Description = "High-performance vector charts with instant line, area, bar, and scatter switching, tooltip inspection, and CSV export.",
            IconKind = MaterialIconKind.ChartLine,
            AccentColor = "#f59e0b",
            AccentBackground = "#33220c",
            AccentBorder = "#d97706",
            CategoryBadge = "Analytics • Vector",
            Tags = new List<string> { "Charts", "Vector", "Analytics", "Metrics" },
            Notes = @"# Interactive Vector Charts

Zero-dependency vector charts rendered via Avalonia DrawingContext.
Switch between Line, Area, Bar, Scatter, and Pie/Donut with 1-click in the toolbar.",
            InitialCode = @"var monthlyMetrics = new[]
{
    new { Month = ""Jan"", Revenue = 42000, Profit = 14200 },
    new { Month = ""Feb"", Revenue = 48000, Profit = 16800 },
    new { Month = ""Mar"", Revenue = 55000, Profit = 21000 },
    new { Month = ""Apr"", Revenue = 51000, Profit = 19500 },
    new { Month = ""May"", Revenue = 64000, Profit = 26400 },
    new { Month = ""Jun"", Revenue = 72000, Profit = 31200 }
};

Display.Chart(monthlyMetrics, title: ""Q1-Q2 Financial Performance"", color: ""#38bdf8"");
Console.WriteLine(""Switch chart types using the toolbar buttons at top-right!"");"
        },
        new()
        {
            Id = "quicksort_sorting_bars",
            Title = "QuickSort (Interactive Sorting Bars)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Visualizes QuickSort pivot selection, partition swapping, and sorting progress bar-by-bar with time travel.",
            IconKind = MaterialIconKind.ChartBar,
            AccentColor = "#38bdf8",
            AccentBackground = "#0c2838",
            AccentBorder = "#0284c7",
            CategoryBadge = "Sorting • Divide & Conquer",
            Tags = new List<string> { "Sorting", "QuickSort", "Bars", "Visualizer" },
            Notes = @"# QuickSort Visualizer

Visualizes QuickSort on an array of values.
- Drag the **scrubber** to view every pivot selection, element comparison, and swap.
- Yellow = active/comparing, Pink = pivot, Green = sorted.",
            InitialCode = @"var data = new BarChartVisualizerData(new[] { 45, 12, 85, 32, 89, 39, 67, 23, 91, 54 })
{
    MaxValue = 100
};

var recorder = VisualizerRecorder.CreateBars(data, ""QuickSort Visualization"");

void QuickSort(BarChartVisualizerData bars, int low, int high)
{
    if (low < high)
    {
        int pi = Partition(bars, low, high);
        bars.Items[pi].IsSorted = true;
        recorder.Step($""Pivot {bars.Items[pi].DisplayValue} locked in sorted position {pi}."");
        QuickSort(bars, low, pi - 1);
        QuickSort(bars, pi + 1, high);
    }
    else if (low == high)
    {
        bars.Items[low].IsSorted = true;
        recorder.Step($""Element {bars.Items[low].DisplayValue} at index {low} is sorted."");
    }
}

int Partition(BarChartVisualizerData bars, int low, int high)
{
    var pivot = bars.Items[high];
    pivot.IsPivot = true;
    pivot.PointerLabel = ""pivot"";
    recorder.Step($""Selected pivot {pivot.DisplayValue} at index {high}."");

    int i = low - 1;
    for (int j = low; j < high; j++)
    {
        bars.Items[j].IsActive = true;
        bars.Items[j].PointerLabel = ""j"";
        if (i >= low) bars.Items[i].PointerLabel = ""i"";
        recorder.Step($""Compare index {j} ({bars.Items[j].DisplayValue}) with pivot ({pivot.DisplayValue})."");

        if (bars.Items[j].Value < pivot.Value)
        {
            i++;
            if (i != j)
            {
                var temp = bars.Items[i].Value;
                bars.Items[i].Value = bars.Items[j].Value;
                bars.Items[i].DisplayValue = bars.Items[j].DisplayValue;
                bars.Items[j].Value = temp;
                bars.Items[j].DisplayValue = temp.ToString();
                recorder.Step($""Swapped smaller element {bars.Items[i].DisplayValue} to index {i}."");
            }
        }
        bars.Items[j].IsActive = false;
        bars.Items[j].PointerLabel = """";
    }

    if (i >= low) bars.Items[i].PointerLabel = """";

    var tempP = bars.Items[i + 1].Value;
    bars.Items[i + 1].Value = bars.Items[high].Value;
    bars.Items[i + 1].DisplayValue = bars.Items[high].DisplayValue;
    bars.Items[high].Value = tempP;
    bars.Items[high].DisplayValue = tempP.ToString();

    bars.Items[high].IsPivot = false;
    bars.Items[high].PointerLabel = """";
    bars.Items[i + 1].IsPivot = false;
    bars.Items[i + 1].PointerLabel = """";

    recorder.Step($""Placed pivot {bars.Items[i + 1].DisplayValue} at partition index {i + 1}."");

    return i + 1;
}

QuickSort(data, 0, data.Items.Count - 1);

foreach (var item in data.Items)
{
    item.IsSorted = true;
    item.IsActive = false;
    item.IsPivot = false;
    item.PointerLabel = """";
}
recorder.Step(""QuickSort complete! All elements sorted in ascending order."");

Display.Visualizer(recorder);
Console.WriteLine(""QuickSort complete! Use playback controls to scrub through partitioning."");"
        },
        new()
        {
            Id = "custom_scene_canvas",
            Title = "Freeform 2D Scene Graph (Stack & Queue)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Build any custom data structure or diagram with circles, rects, lines, and arrows using the declarative Scene Graph SDK.",
            IconKind = MaterialIconKind.DrawingBox,
            AccentColor = "#ec4899",
            AccentBackground = "#380d22",
            AccentBorder = "#db2777",
            CategoryBadge = "Scene Graph • Custom SDK",
            Tags = new List<string> { "Canvas", "SDK", "Stack", "Queue", "Custom" },
            Notes = @"# Freeform 2D Scene Graph SDK

Create completely custom animations, state machines, hardware pipelines, or data structures.
Use `VisualizerRecorder.CreateCanvas()` and `.Step(desc, scene => { ... })` to animate any 2D visual representation.",
            InitialCode = @"// Create a 2D Canvas Visualizer with declarative shapes
var rec = VisualizerRecorder.CreateCanvas(""Stack Data Structure (LIFO)"", width: 500, height: 260);

// Step 1: Initial empty stack container
rec.Step(""Initial Stack: Empty Container"", scene =>
{
    scene.AddRect(180, 40, 140, 180, fill: ""#0f172a"", stroke: ""#334155"", cornerRadius: 6);
    scene.AddText(250, 230, ""Stack Base"", isCentered: true, color: ""#64748b"");
});

// Step 2: Push 10
rec.Step(""Push(10)"", scene =>
{
    scene.AddRect(190, 170, 120, 36, label: ""10"", fill: ""#1e293b"", stroke: ""#3b82f6"");
    scene.AddArrow(120, 188, 185, 188, label: ""TOP"");
});

// Step 3: Push 20
rec.Step(""Push(20)"", scene =>
{
    scene.AddRect(190, 125, 120, 36, label: ""20"", fill: ""#1e293b"", stroke: ""#3b82f6"");
    scene.Shapes.RemoveAll(s => s is SceneArrow);
    scene.AddArrow(120, 143, 185, 143, label: ""TOP"");
});

// Step 4: Push 30
rec.Step(""Push(30)"", scene =>
{
    scene.AddRect(190, 80, 120, 36, label: ""30"", fill: ""#1e293b"", stroke: ""#3b82f6"");
    scene.Shapes.RemoveAll(s => s is SceneArrow);
    scene.AddArrow(120, 98, 185, 98, label: ""TOP"");
});

// Step 5: Pop() removes 30
rec.Step(""Pop() -> Returns 30"", scene =>
{
    scene.Shapes.RemoveAll(s => s is SceneRect r && r.Label == ""30"");
    scene.Shapes.RemoveAll(s => s is SceneArrow);
    scene.AddArrow(120, 143, 185, 143, label: ""TOP"");
    scene.AddText(360, 90, ""Popped: 30"", color: ""#f43f5e"", isBold: true);
});

Display.Visualizer(rec);
Console.WriteLine(""Freeform 2D Scene Graph rendered! Use playback controls to step through Push and Pop operations."");"
        },
        new()
        {
            Id = "matrix_pathfinder_user_algo",
            Title = "A* & BFS Maze Pathfinder (User Custom Algo)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Step-by-step custom pathfinding with MatrixTracker: frontier queue, predecessor arrows, and shortest path reconstruction.",
            IconKind = MaterialIconKind.Compass,
            AccentColor = "#10b981",
            AccentBackground = "#132b21",
            AccentBorder = "#059669",
            CategoryBadge = "Pathfinding • User Algorithm",
            Tags = new List<string> { "BFS", "Pathfinding", "Maze", "MatrixTracker" },
            Notes = @"# Extensible Matrix Traversal with MatrixTracker

Write your own matrix algorithm in plain C# and track it with 1-line calls:
- `tracker.Visit(r, c)` marks the active scanner.
- `tracker.Enqueue(nr, nc, from: (r, c))` creates the frontier and directional parent arrow.
- `tracker.MarkPath(path)` draws the final route.
- `tracker.Watch(queue)` shows the queue's real contents under the grid at every step.",
            InitialCode = @"// Define raw maze grid: 'S'=Start, 'T'=Target, '#' = Wall, '.' = Empty
var maze = new string[] {
    ""S...#..."",
    "".##.#.#."",
    ""....#.#."",
    ""##.##.#."",
    ""......#T""
};

// 1. Wrap in MatrixTracker (automatically parses Start, Target, and Walls)
var tracker = MatrixTracker.Create(maze, title: ""User BFS Pathfinder with Predecessor Arrows"");

// 2. User's own pathfinding loop:
var queue = new Queue<(int r, int c)>();
var visited = new bool[5, 8];
var parent = new Dictionary<(int, int), (int, int)>();
tracker.Watch(queue); // draw the live queue (front -> back) under the grid at every step

queue.Enqueue((0, 0));
visited[0, 0] = true;
tracker.Snapshot(""Initialized BFS queue with Start (0, 0)"");

while (queue.Count > 0)
{
    var (r, c) = queue.Dequeue();
    tracker.Visit(r, c, $""Exploring ({r}, {c}) • Remaining in queue: {queue.Count}"");

    if (r == 4 && c == 7)
    {
        var path = tracker.TracePath(parent, (4, 7), (0, 0));
        tracker.MarkPath(path, $""Target Reached! Shortest Path: {path.Count} steps"");
        break;
    }

    foreach (var (nr, nc) in tracker.GetNeighbors(r, c, fourWay: true))
    {
        if (!visited[nr, nc] && tracker.Grid[nr, nc].State != GridCellState.Wall)
        {
            visited[nr, nc] = true;
            parent[(nr, nc)] = (r, c);
            queue.Enqueue((nr, nc));

            // Record discovery and predecessor back-pointer arrow:
            tracker.Enqueue(nr, nc, from: (r, c), note: $""Discovered ({nr}, {nc}) from ({r}, {c})"");
        }
    }
}

Display.Visualizer(tracker);
Console.WriteLine(""Use playback controls to scrub through the BFS exploration and predecessor arrows!"");"
        },
        new()
        {
            Id = "matrix_dp_minimum_path_sum",
            Title = "2D Dynamic Programming (Min Path Sum)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Interactive DP table calculation with predecessor dependency arrows, cost sub-labels, and optimal path.",
            IconKind = MaterialIconKind.TableHeadersEye,
            AccentColor = "#a855f7",
            AccentBackground = "#28153c",
            AccentBorder = "#7c3aed",
            CategoryBadge = "Dynamic Programming • Grid",
            Tags = new List<string> { "DP", "Matrix", "Predecessors", "Visualizer" },
            Notes = @"# 2D Dynamic Programming (Minimum Path Sum)

Given an `m x n` grid filled with non-negative numbers, find a path from top left to bottom right which minimizes the sum of all numbers along its path.
Shows state-transition arrows and cell cost annotations step-by-step.",
            InitialCode = @"int[,] costGrid = new int[,] {
    { 1, 3, 1 },
    { 1, 5, 1 },
    { 4, 2, 1 }
};

int rows = costGrid.GetLength(0);
int cols = costGrid.GetLength(1);

var tracker = MatrixTracker.CreateEmpty(rows, cols, title: ""Minimum Path Sum DP"");
for (int r = 0; r < rows; r++)
    for (int c = 0; c < cols; c++)
        tracker.SetCell(r, c, val: costGrid[r, c].ToString());

int[,] dp = new int[rows, cols];
var parent = new Dictionary<(int, int), (int, int)>();

for (int r = 0; r < rows; r++)
{
    for (int c = 0; c < cols; c++)
    {
        if (r == 0 && c == 0)
        {
            dp[0, 0] = costGrid[0, 0];
        }
        else if (r == 0)
        {
            dp[0, c] = dp[0, c - 1] + costGrid[0, c];
            parent[(0, c)] = (0, c - 1);
            tracker.PointTo(0, c, 0, c - 1, color: ""#38bdf8"");
        }
        else if (c == 0)
        {
            dp[r, 0] = dp[r - 1, 0] + costGrid[r, 0];
            parent[(r, 0)] = (r - 1, 0);
            tracker.PointTo(r, 0, r - 1, 0, color: ""#38bdf8"");
        }
        else
        {
            if (dp[r - 1, c] < dp[r, c - 1])
            {
                dp[r, c] = dp[r - 1, c] + costGrid[r, c];
                parent[(r, c)] = (r - 1, c);
                tracker.PointTo(r, c, r - 1, c, color: ""#38bdf8"");
            }
            else
            {
                dp[r, c] = dp[r, c - 1] + costGrid[r, c];
                parent[(r, c)] = (r, c - 1);
                tracker.PointTo(r, c, r, c - 1, color: ""#38bdf8"");
            }
        }

        tracker.SetCell(r, c, subLabel: $""dp={dp[r, c]}"");
        tracker.Visit(r, c, $""Computed dp[{r}, {c}] = {dp[r, c]}"", subLabel: $""dp={dp[r, c]}"");
    }
}

var optPath = tracker.TracePath(parent, (rows - 1, cols - 1), (0, 0));
tracker.MarkPath(optPath, $""Optimal Path Found! Minimum Cost: {dp[rows - 1, cols - 1]}"");

Display.Visualizer(tracker);
Console.WriteLine($""Min Path Sum: {dp[rows - 1, cols - 1]}"");"
        }
    };
}
