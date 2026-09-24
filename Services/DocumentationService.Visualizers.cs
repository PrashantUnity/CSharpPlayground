using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildVisualizersCategory()
    {
        return new DocCategory
        {
            Id = "visualizer_recorder",
            Title = "VisualizerRecorder API",
            IconKind = MaterialIconKind.ChartTimelineVariant,
            AccentColor = "#4EC9B0",
            Badge = "Algorithm Visualizer",
            Description = "Step-by-step recording framework for arrays, sorting bars, matrices, trees, graphs, boards, and 2D canvas.",
            Articles = new List<DocArticle>
            {
                CreateVisualizerOverviewArticle(),
                CreateArraysAndBarsArticle(),
                CreateMatrixAndBoardArticle(),
                CreateTreesAndGraphsArticle(),
                CreateLinkedListsAndRecursionArticle(),
                CreateCanvasRecorderArticle(),
                CreateConvenienceHelpersArticle()
            }
        };
    }

    private DocArticle CreateVisualizerOverviewArticle()
    {
        return new DocArticle
        {
            Id = "visualizer_overview",
            Title = "VisualizerRecorder Overview",
            Subtitle = "Capture algorithm state snapshots to create interactive scrubbing timelines.",
            ReadingTime = "5 min read",
            Summary = "VisualizerRecorder records step-by-step states of an algorithm, enabling playback, reverse scrubbing, and speed adjustment in notebooks and the results deck.",
            Keywords = new List<string> { "visualizer", "recorder", "timeline", "scrubber", "player", "playback", "sequence", "steps", "zoom", "fit", "full screen", "keyboard" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "How VisualizerRecorder Works",
                    Content = "Instead of static logging, VisualizerRecorder captures deep snapshots of your data structures at critical milestones. Each call to recorder.Step(...) records the current state, active pointers, highlights, and custom notes.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "When rendered, an interactive player appears with Play/Pause, Step Forward/Backward, Speed Slider (0.5x to 4x), and a Step Scrubber bar. Every step remembers the line of code that recorded it: scrubbing or playing highlights that line in the editor, and the Ln badge jumps to it."
                },
                new()
                {
                    Heading = "Seeing What Changed and What Is Queued",
                    Content = "Values that changed since the previous step (a swap, a new DP cell, an updated distance) are outlined in lime. Call recorder.Watch(queue) or tracker.Watch(queue) once after creating a collection, and every later step shows its real contents under the canvas: queues front to back, stacks top first, priority queues in dequeue order, plus sets, maps and lists. Items that just arrived get the same lime outline.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "The strip is labelled with your variable name, so tracker.Watch(frontier) shows up as \"frontier\"."
                },
                new()
                {
                    Heading = "Displaying the Visualizer",
                    Content = "To display a recorded sequence in the interactive deck or notebook:",
                    BulletPoints = new List<string>
                    {
                        "Display.Visualizer(recorder) — Renders the interactive visualizer in the Results deck.",
                        "recorder.DisplayVisualizer() — Fluent extension method returning the recorder.",
                        "Return value in Notebooks — Evaluating a VisualizerRecorder or tracker as the final line of a cell automatically mounts the interactive player!"
                    }
                },
                new()
                {
                    Heading = "Getting a Better Look",
                    Content = "Big drawings (a finished recursion tree, a long list, a large grid) do not have to be read through a keyhole:",
                    BulletPoints = new List<string>
                    {
                        "Fit to View — Zooms so the whole drawing shows, at every step of the playback, so a tree that grows while it plays never runs off the canvas. The zoom percentage button goes back to 100%.",
                        "Full Screen — The header button, or a double-click on the drawing, opens the visualizer over the whole window with all its controls. It shares the playback with the inline copy, so the editor keeps highlighting the current line. Press Esc to leave; the Ln badge leaves and jumps straight to the line.",
                        "Taller canvas — Drag the handle under the canvas to resize it; double-click the handle to reset.",
                        "Keyboard — Click the drawing, then use Left/Right to step, Home/End for the first and last step, Space to play or pause, + and - to zoom, 0 for 100% and F to fit."
                    },
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Drag the drawing to pan and use the mouse wheel to zoom; after Fit to View the drawing stays fitted when the canvas is resized, until you zoom or pan yourself."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new()
                {
                    MethodName = "Display.Visualizer",
                    ReturnType = "InteractiveVisualizerControl",
                    Parameters = "VisualizerRecorder recorder",
                    Description = "Mounts an interactive player with step scrubbing, playback controls, and inspectors in the active output deck."
                },
                new()
                {
                    MethodName = "recorder.ToSequence",
                    ReturnType = "VisualizerSequence",
                    Parameters = "",
                    Description = "Compiles all recorded steps into an immutable playback sequence."
                },
                new()
                {
                    MethodName = "recorder.Watch",
                    ReturnType = "VisualizerRecorder",
                    Parameters = "object collection, string name = (variable name)",
                    Description = "Shows a live Queue, Stack, PriorityQueue, HashSet, Dictionary or List beneath the visualizer at every later step. Trackers have the same Watch method."
                }
            }
        };
    }

    private DocArticle CreateArraysAndBarsArticle()
    {
        return new DocArticle
        {
            Id = "arrays_and_bars",
            Title = "Arrays, Pointers & Sorting Bars",
            Subtitle = "Step through two-pointer algorithms and dynamic bar charts for sorting.",
            ReadingTime = "5 min read",
            Summary = "Record array pointers (Left, Right, Mid) and bar charts for BubbleSort, QuickSort, MergeSort, and Binary Search.",
            Keywords = new List<string> { "array", "pointers", "bars", "sorting", "binary search", "quicksort", "bubblesort" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Array Pointer Stepping (CreateArray)",
                    Content = "Use VisualizerRecorder.CreateArray(...) to track one or more named indices. You can pass pointers as an anonymous object: new { Left = l, Right = r, Mid = m }.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Each pointer name receives a distinctive color (e.g. Left is blue, Right is amber, Mid is purple) and renders directly above its element."
                },
                new()
                {
                    Heading = "Sorting Bars (CreateBars)",
                    Content = "Use VisualizerRecorder.CreateBars(...) to render vertical bars proportional to element values. Call recorder.StepBars(...) or recorder.Step(...) after swaps or comparisons to capture the state."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new()
                {
                    MethodName = "VisualizerRecorder.CreateArray",
                    ReturnType = "VisualizerRecorder",
                    Parameters = "ArrayPointerData arrayData, string? title = null",
                    Description = "Initializes an array visualizer recorder with initial state and optional title."
                },
                new()
                {
                    MethodName = "VisualizerRecorder.CreateBars",
                    ReturnType = "BarVisualizerRecorder",
                    Parameters = "BarChartVisualizerData bars, string? title = null",
                    Description = "Initializes a sorting bar visualizer recorder."
                },
                new()
                {
                    MethodName = "recorder.Step",
                    ReturnType = "VisualizerRecorder",
                    Parameters = "string description, object? pointers = null",
                    Description = "Records a new step with an explanation string and an anonymous object containing pointer indices."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_binary_search",
                    Title = "Binary Search with Pointers",
                    Description = "Track Left, Right, and Mid pointers step-by-step through a sorted array.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var nums = new[] { 2, 5, 8, 12, 16, 23, 38, 56, 72, 91 };
int target = 23;

var recorder = VisualizerRecorder.CreateArray(
    ArrayPointerDataParser.Parse(nums),
    title: $""Binary Search for {target}"");

int left = 0, right = nums.Length - 1;
while (left <= right)
{
    int mid = left + (right - left) / 2;
    recorder.Step($""Check mid={mid} (val={nums[mid]}). Target={target}"",
        pointers: new { Left = left, Right = right, Mid = mid });

    if (nums[mid] == target)
    {
        recorder.Step($""Found {target} at index {mid}!"", pointers: new { Match = mid });
        break;
    }
    if (nums[mid] < target) left = mid + 1;
    else right = mid - 1;
}

Display.Visualizer(recorder);"
                },
                new()
                {
                    Id = "snip_quicksort_bars",
                    Title = "Sorting with VisualizerRecorder.CreateBars",
                    Description = "Animate BubbleSort or QuickSort bar chart transitions.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var data = new BarChartVisualizerData(new[] { 45, 12, 85, 32, 89, 39, 69, 22 });
var recorder = VisualizerRecorder.CreateBars(data, ""Bubble Sort Visualization"");

for (int i = 0; i < data.Items.Count - 1; i++)
{
    for (int j = 0; j < data.Items.Count - i - 1; j++)
    {
        if (data.Items[j].Value > data.Items[j + 1].Value)
        {
            // Swap
            var tmp = data.Items[j];
            data.Items[j] = data.Items[j + 1];
            data.Items[j + 1] = tmp;

            recorder.StepBars($""Swapped {data.Items[j].Value} and {data.Items[j + 1].Value}"");
        }
    }
}
recorder.StepBars(""Sorting Complete!"");
Display.Visualizer(recorder);"
                }
            }
        };
    }

    private DocArticle CreateMatrixAndBoardArticle()
    {
        return new DocArticle
        {
            Id = "matrix_and_board",
            Title = "Matrix Grids & DP Boards",
            Subtitle = "Visualize BFS/DFS traversals, shortest paths, and dynamic programming tables.",
            ReadingTime = "4 min read",
            Summary = "Step through 2D grids, flood fills, number of islands, and 2D dynamic programming matrices.",
            Keywords = new List<string> { "matrix", "grid", "board", "bfs", "dfs", "dp", "flood fill", "islands" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Grid Traversal Recording (CreateMatrix)",
                    Content = "VisualizerRecorder.CreateMatrix(...) models 2D grids. You can specify the active cell (Row, Col) or a collection of active cells, along with an auxiliary info dictionary (e.g. Queue contents, current distance, or step count).",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Coordinates (r, c) and values are displayed in high-contrast tiles with smooth zooming and panning."
                },
                new()
                {
                    Heading = "Board & DP Visualizer (CreateBoard)",
                    Content = "VisualizerRecorder.CreateBoard(...) is ideal for N-Queens, Sudoku, Chessboards, and 2D DP memoization tables. It supports cell statuses (Default, Active, Visited, Blocked, Target)."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new()
                {
                    MethodName = "VisualizerRecorder.CreateMatrix",
                    ReturnType = "MatrixVisualizerRecorder",
                    Parameters = "GridMatrixData grid, string? title = null",
                    Description = "Initializes a 2D matrix visualizer recorder."
                },
                new()
                {
                    MethodName = "recorder.Step",
                    ReturnType = "VisualizerRecorder",
                    Parameters = "string description, (int Row, int Col)? activeCell = null, IDictionary<string, string>? auxiliaryInfo = null",
                    Description = "Records a grid step highlighting an active coordinate and optional auxiliary metadata."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_matrix_bfs",
                    Title = "Matrix BFS Pathfinding",
                    Description = "Step through a breadth-first search on a 2D grid matrix.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"int[,] rawGrid = {
    { 1, 1, 0, 1 },
    { 0, 1, 1, 0 },
    { 0, 0, 1, 1 }
};

var matrixData = MatrixDataParser.Parse(rawGrid);
var recorder = VisualizerRecorder.CreateMatrix(matrixData, ""Matrix BFS Traversal"");

int rows = rawGrid.GetLength(0), cols = rawGrid.GetLength(1);
var visited = new bool[rows, cols];
var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

var queue = new Queue<(int r, int c)>();
recorder.Watch(queue); // draws the live queue under the grid at every step
queue.Enqueue((0, 0));
visited[0, 0] = true;

while (queue.Count > 0)
{
    var (r, c) = queue.Dequeue();
    recorder.Step($""Visiting ({r}, {c})"",
        activeCell: (r, c),
        auxiliaryInfo: new Dictionary<string, string> { [""Queue Remaining""] = queue.Count.ToString() });

    foreach (var (dr, dc) in directions)
    {
        int nr = r + dr, nc = c + dc;
        if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
        if (visited[nr, nc] || rawGrid[nr, nc] == 0) continue;

        // Mark on enqueue, not on dequeue, so no cell ever enters the queue twice.
        visited[nr, nc] = true;
        queue.Enqueue((nr, nc));
    }
}

Display.Visualizer(recorder);"
                }
            }
        };
    }

    private DocArticle CreateTreesAndGraphsArticle()
    {
        return new DocArticle
        {
            Id = "trees_and_graphs",
            Title = "Trees & Graph Visualizers",
            Subtitle = "Render hierarchical trees and network graphs with automated layout.",
            ReadingTime = "4 min read",
            Summary = "Inspect binary trees, BSTs, N-ary trees, and directed/undirected graphs with automated Buchheim tree layout and node highlight steps.",
            Keywords = new List<string> { "tree", "graph", "bst", "traversal", "nodes", "edges", "dijkstra" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Tree Layout & Traversals (CreateTree)",
                    Content = "VisualizerRecorder.CreateTree(...) accepts any object root (e.g. TreeNode, BSTNode). It automatically discovers Left/Right or Children properties via reflection and calculates clean non-overlapping coordinates using the Buchheim tree algorithm.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "You can record In-Order, Pre-Order, Post-Order, or Level-Order traversals highlighting activeNodeId."
                },
                new()
                {
                    Heading = "Graph Networks (CreateGraph)",
                    Content = "VisualizerRecorder.CreateGraph(...) builds a graph model from adjacency lists or edge collections. Call Step with activeNodeId or activeNodeIds to step through BFS, DFS, or topological sorting."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_tree_recorder",
                    Title = "Binary Search Tree In-Order Traversal",
                    Description = "Record in-order traversal steps on a hierarchical tree.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"// Simple binary tree node
public class Node
{
    public int Value { get; set; }
    public Node? Left { get; set; }
    public Node? Right { get; set; }
    public Node(int v) => Value = v;
}

var root = new Node(10)
{
    Left = new Node(5) { Left = new Node(2), Right = new Node(7) },
    Right = new Node(15) { Right = new Node(20) }
};

var recorder = VisualizerRecorder.CreateTree(root, ""BST In-Order Traversal"");

void InOrder(Node? n)
{
    if (n == null) return;
    InOrder(n.Left);
    recorder.Step($""Visit Node {n.Value}"", activeNodeId: n.Value.ToString());
    InOrder(n.Right);
}

InOrder(root);
Display.Visualizer(recorder);"
                }
            }
        };
    }

    private DocArticle CreateLinkedListsAndRecursionArticle()
    {
        return new DocArticle
        {
            Id = "linked_lists_and_recursion",
            Title = "Linked Lists & Recursion Trees",
            Subtitle = "Follow pointer rewiring in your own nodes and see recursion as a tree of calls.",
            ReadingTime = "4 min read",
            Summary = "LinkedListTracker records your own ListNode objects with named pointers; RecursionTracker turns recursive calls into a call tree showing the live call stack, memo hits and pruned branches.",
            Keywords = new List<string> { "linked list", "reverse", "pointers", "prev", "curr", "recursion", "call stack", "memoization", "backtracking", "prune" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "LinkedListTracker",
                    Content = "LinkedListTracker.Create(head) follows next pointers through your own nodes. Call tracker.Step(\"...\", new { prev, curr, next }) after each change: every node keeps its place, arrows follow the real next pointers, and your variables are drawn as labelled pointers. A null variable points at the null box, and a null next in the middle of the list shows as a slash.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "New nodes are picked up automatically, whether they are linked into the list or only held by a variable (a second list to merge, a dummy head)."
                },
                new()
                {
                    Heading = "RecursionTracker",
                    Content = "Open each call with using var call = calls.Enter($\"fib({n})\") and finish it with call.Return(value), call.Memo(value) for a cache hit, or call.Prune(reason) for a dead-end branch. The tree grows call by call; the path from main to the glowing call is the call stack, and teal calls are waiting for a result.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Drawing stops after 255 calls so large inputs stay responsive; the recursion itself keeps running normally."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new()
                {
                    MethodName = "LinkedListTracker.Create",
                    ReturnType = "LinkedListTracker",
                    Parameters = "object head, string? title = null",
                    Description = "Starts tracking the list reachable from head."
                },
                new()
                {
                    MethodName = "tracker.Step",
                    ReturnType = "LinkedListTracker",
                    Parameters = "string description, object? pointers = null",
                    Description = "Records the list now; pointers is an anonymous object such as new { prev, curr }."
                },
                new()
                {
                    MethodName = "RecursionTracker.Enter",
                    ReturnType = "RecursionCall",
                    Parameters = "object label",
                    Description = "Adds a call under the current one. End it with Return, Memo or Prune, or let the using end it."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_reverse_linked_list",
                    Title = "Reverse a Linked List with Named Pointers",
                    Description = "Watch each arrow flip while prev and curr walk the list.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"public class ListNode
{
    public int val;
    public ListNode? next;
    public ListNode(int val, ListNode? next = null) { this.val = val; this.next = next; }
}

var head = new ListNode(1, new ListNode(2, new ListNode(3)));
var tracker = LinkedListTracker.Create(head, ""Reverse a linked list"");

ListNode? prev = null, curr = head;
while (curr != null)
{
    var next = curr.next;
    curr.next = prev;              // flip the arrow
    tracker.Step($""Flip {curr.val}"", new { prev, curr, next });
    prev = curr;
    curr = next;
}

tracker.Step(""prev is the new head"", new { head = prev });
Display.Visualizer(tracker);"
                },
                new()
                {
                    Id = "snip_recursion_tree_memo",
                    Title = "Fibonacci Call Tree with a Memo",
                    Description = "Every call becomes a node; memo hits end a branch immediately.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var calls = RecursionTracker.Create(""fib(4) with a memo"");
var memo = new Dictionary<int, long>();
calls.Watch(memo);

long Fib(int n)
{
    using var call = calls.Enter($""fib({n})"");
    if (memo.TryGetValue(n, out var cached)) return call.Memo(cached);
    if (n < 2) return call.Return(n);
    return call.Return(memo[n] = Fib(n - 1) + Fib(n - 2));
}

Fib(4);
Display.Visualizer(calls);"
                }
            }
        };
    }

    private DocArticle CreateCanvasRecorderArticle()
    {
        return new DocArticle
        {
            Id = "canvas_recorder",
            Title = "Custom 2D Vector Canvas",
            Subtitle = "Build custom geometric visualizers for stacks, queues, and custom data structures.",
            ReadingTime = "4 min read",
            Summary = "VisualizerRecorder.CreateCanvas lets you draw arbitrary shapes (rectangles, circles, lines, text) into a snapshot sequence.",
            Keywords = new List<string> { "canvas", "vector", "shapes", "stack", "queue", "scene", "geometry" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Freeform Vector Scenes (CreateCanvas)",
                    Content = "When your algorithm doesn't fit standard arrays or grids (such as Stack LIFO operations, Priority Queues, or Geometric Convex Hulls), CreateCanvas gives you a blank 2D vector canvas. Each step clones the scene so you only express changes.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Use scene.AddRect, scene.AddCircle, scene.AddText, scene.AddLine, and scene.AddArrow with custom colors, strokes, and fills."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_stack_canvas",
                    Title = "Stack LIFO Visualization with Canvas",
                    Description = "Draw stacked cards as elements are pushed and popped.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var recorder = VisualizerRecorder.CreateCanvas(""Stack LIFO Visualizer"", width: 400, height: 260);

recorder.Step(""Push 'Item 1' to Stack"", scene =>
{
    scene.AddRect(120, 180, 160, 40, label: ""Item 1 (Bottom)"", fill: ""#0284c7"", stroke: ""#38bdf8"");
});

recorder.Step(""Push 'Item 2' to Stack"", scene =>
{
    scene.AddRect(120, 130, 160, 40, label: ""Item 2"", fill: ""#7c3aed"", stroke: ""#a78bfa"");
});

recorder.Step(""Push 'Item 3' to Stack (Top)"", scene =>
{
    scene.AddRect(120, 80, 160, 40, label: ""Item 3 (Top)"", fill: ""#10b981"", stroke: ""#34d399"");
});

Display.Visualizer(recorder);"
                }
            }
        };
    }

    private DocArticle CreateConvenienceHelpersArticle()
    {
        return new DocArticle
        {
            Id = "visualizer_helpers",
            Title = "One-Line Display Visualizers",
            Subtitle = "Quick one-line methods to visualize islands, matrices, trees, and linked lists.",
            ReadingTime = "3 min read",
            Summary = "Display data structures with zero boilerplate using Display.Islands, Display.Tree, Display.Graph, Display.Matrix, and Display.Bars.",
            Keywords = new List<string> { "display", "islands", "helpers", "linkedlist", "extensions", "one-line" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Display Extension Methods",
                    Content = "You don't always need to build a manual recorder. C# Code Studio provides instant one-line helpers:",
                    BulletPoints = new List<string>
                    {
                        "Display.Islands(grid) or grid.DisplayIslands() — Runs automated island detection with flood-fill playback.",
                        "Display.Matrix(grid) or grid.DisplayMatrix() — Displays interactive matrix with coordinates.",
                        "Display.Tree(root) or root.DisplayTree() — Generates Buchheim tree layout with traversal options.",
                        "Display.Graph(graph) or graph.DisplayGraph() — Displays directed or undirected network graph.",
                        "Display.LinkedList(head) or head.DisplayLinkedList() — Visualizes linked list with cycle detection.",
                        "Or just return the value on the last line of a cell: tree nodes (left/right or children), list nodes (next), 2D arrays, recorders and trackers are drawn automatically."
                    }
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_islands_helper",
                    Title = "Automated Number of Islands Visualizer",
                    Description = "Detect and animate 2D islands with a single line of code.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"int[,] map = {
    { 1, 1, 0, 0, 0 },
    { 1, 1, 0, 0, 1 },
    { 0, 0, 0, 1, 1 },
    { 0, 0, 0, 0, 0 },
    { 1, 0, 1, 1, 0 }
};

// Visualizer helper: automatically runs BFS/DFS and generates step-by-step playback!
Display.Islands(map, title: ""Pacific Archipelago Islands"");"
                }
            }
        };
    }
}
