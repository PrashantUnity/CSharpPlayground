using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class DataStructureVisualizerTests
{
    private class TestTreeNode
    {
        public int val { get; set; }
        public TestTreeNode? left { get; set; }
        public TestTreeNode? right { get; set; }

        public TestTreeNode(int v, TestTreeNode? l = null, TestTreeNode? r = null)
        {
            val = v;
            left = l;
            right = r;
        }
    }

    private class TestListNode
    {
        public int val { get; set; }
        public TestListNode? next { get; set; }

        public TestListNode(int v, TestListNode? n = null)
        {
            val = v;
            next = n;
        }
    }

    private class PublicFieldTreeNode
    {
        public int val;
        public PublicFieldTreeNode? left;
        public PublicFieldTreeNode? right;

        public PublicFieldTreeNode(int v, PublicFieldTreeNode? l = null, PublicFieldTreeNode? r = null)
        {
            val = v;
            left = l;
            right = r;
        }
    }

    private class PublicFieldListNode
    {
        public int val;
        public PublicFieldListNode? next;

        public PublicFieldListNode(int v, PublicFieldListNode? n = null)
        {
            val = v;
            next = n;
        }
    }

    [Fact]
    public void MatrixDataParser_ShouldParse2DIntMatrix()
    {
        int[,] raw = new int[,]
        {
            { 1, 1, 0 },
            { 0, 1, 0 },
            { 0, 0, 1 }
        };

        var grid = MatrixDataParser.Parse(raw);

        Assert.Equal(3, grid.Rows);
        Assert.Equal(3, grid.Columns);
        Assert.Equal("1", grid[0, 0].DisplayValue);
        Assert.Equal(CellKind.Land, grid[0, 0].Kind);
        Assert.Equal(CellKind.Water, grid[0, 2].Kind);
    }

    [Fact]
    public void MatrixDataParser_ShouldParseJaggedCharArray()
    {
        char[][] raw = new char[][]
        {
            new char[] { '1', '1', '0', '0' },
            new char[] { '0', '0', '1', '1' }
        };

        var grid = MatrixDataParser.Parse(raw);

        Assert.Equal(2, grid.Rows);
        Assert.Equal(4, grid.Columns);
        Assert.Equal(CellKind.Land, grid[0, 0].Kind);
        Assert.Equal(CellKind.Water, grid[0, 2].Kind);
        Assert.Equal(CellKind.Land, grid[1, 2].Kind);
    }

    [Fact]
    public void MatrixDataParser_ShouldParseStringRowArray()
    {
        string[] rows = new[]
        {
            "11000",
            "11000",
            "00100",
            "00011"
        };

        var grid = MatrixDataParser.Parse(rows);

        Assert.Equal(4, grid.Rows);
        Assert.Equal(5, grid.Columns);
        Assert.Equal(CellKind.Land, grid[0, 0].Kind);
        Assert.Equal(CellKind.Water, grid[2, 0].Kind);
        Assert.Equal(CellKind.Land, grid[2, 2].Kind);
    }

    [Fact]
    public void IslandDetectorService_ShouldDetectThreeIslandsInClassicGrid()
    {
        char[][] grid = new char[][]
        {
            new char[] { '1', '1', '0', '0', '0' },
            new char[] { '1', '1', '0', '0', '0' },
            new char[] { '0', '0', '1', '0', '0' },
            new char[] { '0', '0', '0', '1', '1' }
        };

        var matrix = MatrixDataParser.Parse(grid);
        var islands = IslandDetectorService.Detect(matrix, fourDirectional: true);

        Assert.Equal(3, islands.Count);
        Assert.Equal(4, islands[0].Area); // Island 1: (0,0), (0,1), (1,0), (1,1)
        Assert.Equal(1, islands[1].Area); // Island 2: (2,2)
        Assert.Equal(2, islands[2].Area); // Island 3: (3,3), (3,4)

        // Cluster IDs assigned to cells
        Assert.Equal(1, matrix[0, 0].ClusterId);
        Assert.Equal(2, matrix[2, 2].ClusterId);
        Assert.Equal(3, matrix[3, 3].ClusterId);
    }

    [Fact]
    public void IslandDetectorService_ShouldGenerateStepByStepTrace()
    {
        char[][] grid = new char[][]
        {
            new char[] { '1', '0' },
            new char[] { '0', '1' }
        };

        var matrix = MatrixDataParser.Parse(grid);
        var (islands, seq) = IslandDetectorService.DetectAndGenerateSteps(matrix, recordSteps: true);

        Assert.Equal(2, islands.Count);
        Assert.True(seq.TotalSteps >= 4);

        // Step 0 should be initialization
        Assert.Equal(0, seq.Steps[0].StepIndex);
        Assert.Contains("Initializing", seq.Steps[0].Description);

        // Discovered Island 1 step should highlight (0, 0)
        var step1 = seq.Steps[1];
        Assert.Contains("Discovered Island #1", step1.Description);
        Assert.Contains((0, 0), step1.ActiveCells);

        // Final step should be scan complete
        var finalStep = seq.Steps[^1];
        Assert.Contains("Scan complete", finalStep.Description);
    }

    [Fact]
    public void VisualizerSequence_StepNavigation_ShouldStepBackAndForth()
    {
        var steps = new List<VisualizerStep>
        {
            new VisualizerStep(0, "Step 0"),
            new VisualizerStep(1, "Step 1"),
            new VisualizerStep(2, "Step 2"),
            new VisualizerStep(3, "Step 3")
        };

        var seq = new VisualizerSequence(steps);

        Assert.Equal(0, seq.CurrentIndex);
        Assert.False(seq.CanStepBack);
        Assert.True(seq.CanStepForward);
        Assert.Equal("1 / 4", seq.StepProgressText);

        seq.NextStep();
        Assert.Equal(1, seq.CurrentIndex);
        Assert.True(seq.CanStepBack);
        Assert.True(seq.CanStepForward);

        seq.LastStep();
        Assert.Equal(3, seq.CurrentIndex);
        Assert.False(seq.CanStepForward);
        Assert.True(seq.CanStepBack);

        seq.PrevStep();
        Assert.Equal(2, seq.CurrentIndex);

        seq.FirstStep();
        Assert.Equal(0, seq.CurrentIndex);

        // Clamped seeking
        seq.SeekStep(99);
        Assert.Equal(3, seq.CurrentIndex);
        seq.SeekStep(-10);
        Assert.Equal(0, seq.CurrentIndex);
    }

    [Fact]
    public void VisualizerRecorder_ShouldRecordCustomSteps()
    {
        int[,] matrix = new int[,] { { 0, 0 }, { 0, 0 } };
        var grid = MatrixDataParser.Parse(matrix);

        var recorder = VisualizerRecorder.CreateMatrix(grid, "Custom BFS Search");
        recorder.Step("Start BFS at (0,0)", activeCell: (0, 0));
        recorder.Step("Explore neighbor (0,1)", activeCell: (0, 1), auxiliaryInfo: new Dictionary<string, string> { ["Queue"] = "1" });
        recorder.Step("Explore neighbor (1,0)", activeCell: (1, 0));
        recorder.Step("Goal reached at (1,1)", activeCell: (1, 1));

        var seq = recorder.ToSequence();
        Assert.Equal(5, seq.TotalSteps); // 1 initial + 4 custom steps
        Assert.Equal("Custom BFS Search", recorder.Options.Title);
        Assert.Equal((0, 1), seq.Steps[2].ActiveCells[0]);
        Assert.Equal("1", seq.Steps[2].AuxiliaryInfo["Queue"]);
    }

    [Fact]
    public void TreeDataParser_ShouldParseCustomTreeNodeAndGenerateTraversal()
    {
        // Binary tree:
        //       4
        //      / \
        //     2   6
        //    / \
        //   1   3
        var root = new TestTreeNode(4,
            new TestTreeNode(2, new TestTreeNode(1), new TestTreeNode(3)),
            new TestTreeNode(6)
        );

        var treeData = TreeDataParser.Parse(root);
        Assert.NotNull(treeData);
        Assert.Equal("4", treeData.DisplayValue);
        Assert.Equal(2, treeData.Children.Count);
        Assert.Equal("2", treeData.Left?.DisplayValue);
        Assert.Equal("6", treeData.Right?.DisplayValue);

        // Buchheim Layout Engine coordinates
        var (w, h) = TreeLayoutEngine.ComputeLayout(treeData);
        Assert.True(w > 0);
        Assert.True(h > 0);
        Assert.True(treeData.Left!.X < treeData.Right!.X);

        // In-Order Traversal sequence (1 -> 2 -> 3 -> 4 -> 6)
        var seq = TreeDataParser.GenerateTraversalSteps(treeData, "InOrder");
        Assert.True(seq.TotalSteps >= 6);
        Assert.Contains("InOrder Visit: Node [1]", seq.Steps[1].Description);
        Assert.Contains("InOrder Visit: Node [2]", seq.Steps[2].Description);
    }

    [Fact]
    public void TreeDataParser_ShouldParsePublicFieldTreeNode_WithoutProperties()
    {
        // Binary tree using public fields (as in LeetCode & Roslyn user scripts)
        var root = new PublicFieldTreeNode(4,
            new PublicFieldTreeNode(2, new PublicFieldTreeNode(1), new PublicFieldTreeNode(3)),
            new PublicFieldTreeNode(6, new PublicFieldTreeNode(5), new PublicFieldTreeNode(7))
        );

        var treeData = TreeDataParser.Parse(root);
        Assert.NotNull(treeData);
        Assert.Equal("4", treeData.DisplayValue); // Must NOT be "Submission#0+TreeNode" or "PublicFieldTreeNode"
        Assert.Equal(2, treeData.Children.Count);
        Assert.Equal("2", treeData.Left?.DisplayValue);
        Assert.Equal("6", treeData.Right?.DisplayValue);
        Assert.Equal("1", treeData.Left?.Children[0].DisplayValue);
        Assert.Equal("3", treeData.Left?.Children[1].DisplayValue);
    }

    [Fact]
    public void LinkedListDataParser_ShouldParsePublicFieldListNode()
    {
        var head = new PublicFieldListNode(10,
            new PublicFieldListNode(20,
                new PublicFieldListNode(30)));

        var listData = LinkedListDataParser.Parse(head);
        Assert.NotNull(listData);
        Assert.Equal(3, listData.Nodes.Count);
        Assert.Equal("10", listData.Nodes[0].DisplayValue);
        Assert.Equal("20", listData.Nodes[1].DisplayValue);
        Assert.Equal("30", listData.Nodes[2].DisplayValue);
        Assert.False(listData.HasCycle);
    }

    [Fact]
    public void LinkedListDataParser_ShouldDetectCycleUsingFloydsAlgorithm()
    {
        // 1 -> 2 -> 3 -> 4 -> points back to 2
        var n1 = new TestListNode(1);
        var n2 = new TestListNode(2);
        var n3 = new TestListNode(3);
        var n4 = new TestListNode(4);
        n1.next = n2;
        n2.next = n3;
        n3.next = n4;
        n4.next = n2; // cycle to index 1

        var listData = LinkedListDataParser.Parse(n1);

        Assert.True(listData.HasCycle);
        Assert.Equal(1, listData.CycleTargetIndex);
        Assert.Equal(4, listData.Nodes.Count);
        Assert.True(listData.Nodes[1].IsCycleTarget);
        Assert.Equal(1, listData.Nodes[3].NextIndex);

        // Step generation
        var seq = LinkedListDataParser.GenerateCycleDetectionSteps(n1);
        Assert.True(seq.TotalSteps >= 3);
        Assert.Contains("Cycle Confirmed", seq.Steps[^1].Description);
    }

    [Fact]
    public void ArrayPointerDataParser_ShouldExtractNamedPointers()
    {
        int[] nums = new[] { 1, 3, 5, 7, 9, 11 };
        var pointers = new { Left = 0, Right = 5, Mid = 2 };

        var data = ArrayPointerDataParser.Parse(nums, pointers);

        Assert.Equal(6, data.Items.Count);
        Assert.Equal(3, data.Pointers.Count);

        var left = data.Pointers.Find(p => p.Name == "Left");
        var right = data.Pointers.Find(p => p.Name == "Right");
        var mid = data.Pointers.Find(p => p.Name == "Mid");

        Assert.NotNull(left);
        Assert.Equal(0, left.Index);
        Assert.NotNull(right);
        Assert.Equal(5, right.Index);
        Assert.NotNull(mid);
        Assert.Equal(2, mid.Index);
    }

    [Fact]
    public void Display_Islands_WithSteps_ShouldEmitVisualizerOutput()
    {
        int[,] grid = new int[,]
        {
            { 1, 0 },
            { 0, 1 }
        };

        RichCellOutput? emitted = null;
        using (InteractiveDisplayContext.EnterScope(output => emitted = output))
        {
            Display.Islands(grid, title: "Test Islands", recordSteps: true);
        }

        Assert.NotNull(emitted);
        Assert.Equal(CellOutputKind.Visualizer, emitted.Kind);
        Assert.True(emitted.IsVisualizerKind);
        Assert.True(emitted.IsControlKind);
        Assert.NotNull(emitted.VisualizerOptions);
        Assert.Equal("Test Islands", emitted.VisualizerOptions.Title);
        Assert.NotNull(emitted.VisualizerSequence);
        Assert.True(emitted.VisualizerSequence.TotalSteps >= 3);
    }

    [Fact]
    public void NotebookCellViewModel_SetVisualizerOutput_ShouldActivateVisualizerTab()
    {
        var model = new NotebookCellItem();
        var cell = new NotebookCellViewModel(model);
        var opt = new VisualizerOptions { Title = "Array Step", Kind = VisualizerKind.ArrayPointers };

        cell.SetVisualizerOutput(opt);

        Assert.True(cell.HasVisualizerOutput);
        Assert.True(cell.IsVisualizerTabActive);
        Assert.True(cell.IsVisualizerTabSelected);
        Assert.Equal("Array Step", cell.SingleOutputTitle);
        Assert.Equal("Grid", cell.SingleOutputIconKind);
        Assert.Equal(CellOutputTab.Visualizer, cell.SelectedOutputTab);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithIslandStepScript_ShouldExecuteWithoutErrors()
    {
        var kernel = new NotebookExecutionKernel();
        string script = @"
char[][] grid = new char[][] {
    new char[] {'1','1','0'},
    new char[] {'0','1','0'},
    new char[] {'0','0','1'}
};
Display.Islands(grid, title: ""Islands in Notebook"", recordSteps: true);
";

        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            script,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
        Assert.Equal(CellOutputKind.Visualizer, richOut.Kind);
        Assert.NotNull(richOut.VisualizerOptions);
        Assert.Equal("Islands in Notebook", richOut.VisualizerOptions.Title);
        Assert.NotNull(richOut.VisualizerSequence);
        Assert.True(richOut.VisualizerSequence.TotalSteps > 0);
    }

    [Fact]
    public void MatrixTracker_UserBfsPathfinding_ShouldRecordExplorationAndPath()
    {
        var maze = new[]
        {
            "S..#",
            ".#.#",
            "...T"
        };

        var tracker = MatrixTracker.Create(maze, "User BFS Test");
        Assert.Equal(GridCellState.Start, tracker[0, 0].State);
        Assert.Equal(GridCellState.Target, tracker[2, 3].State);
        Assert.Equal(GridCellState.Wall, tracker[0, 3].State);

        var queue = new Queue<(int r, int c)>();
        var visited = new bool[3, 4];
        var parent = new Dictionary<(int, int), (int, int)>();

        queue.Enqueue((0, 0));
        visited[0, 0] = true;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            tracker.Visit(r, c, $"Visiting ({r}, {c})");

            if (r == 2 && c == 3)
            {
                var path = tracker.TracePath(parent, (2, 3), (0, 0));
                tracker.MarkPath(path, "Found Target!");
                break;
            }

            foreach (var (nr, nc) in tracker.GetNeighbors(r, c, fourWay: true))
            {
                if (!visited[nr, nc] && tracker[nr, nc].State != GridCellState.Wall)
                {
                    visited[nr, nc] = true;
                    parent[(nr, nc)] = (r, c);
                    queue.Enqueue((nr, nc));
                    tracker.Enqueue(nr, nc, from: (r, c), note: $"Discovered ({nr}, {nc})");
                }
            }
        }

        var seq = tracker.ToSequence();
        Assert.True(seq.TotalSteps >= 5);
        Assert.Equal(GridCellState.Path, tracker[2, 3].State);
        Assert.Equal(GridCellState.Path, tracker[0, 0].State);
        Assert.NotEmpty(tracker[1, 0].Arrows);
        Assert.Equal(0, tracker[1, 0].Arrows[0].TargetRow);
        Assert.Equal(0, tracker[1, 0].Arrows[0].TargetCol);
    }

    [Fact]
    public void MatrixTracker_Backtracking_ShouldRecordBacktrackSteps()
    {
        var tracker = MatrixTracker.CreateEmpty(2, 2, "Backtrack Test");
        tracker.Visit(0, 0, "Enter (0,0)");
        tracker.Visit(0, 1, "Enter (0,1)");
        tracker.Backtrack(0, 1, "Dead end, backtrack from (0,1)");

        Assert.Equal(GridCellState.Backtracked, tracker[0, 1].State);
        var seq = tracker.ToSequence();
        Assert.Equal(4, seq.TotalSteps); // 1 initial + 3 operations
        Assert.Contains("Dead end", seq.Steps[3].Description);
    }

    [Fact]
    public void MatrixTraversalEngine_RunAStar_ShouldFindOptimalPath()
    {
        var raw = new[]
        {
            "S...",
            "###.",
            "T..."
        };

        var grid = MatrixDataParser.Parse(raw);
        var tracker = MatrixTraversalEngine.RunAStar(grid, (0, 0), (2, 0));

        Assert.NotNull(tracker);
        var seq = tracker.ToSequence();
        Assert.True(seq.TotalSteps > 0);
        Assert.Contains("Optimal Path", seq.Steps[^1].Description);
        Assert.Equal(GridCellState.Path, tracker[0, 0].State);
        Assert.Equal(GridCellState.Path, tracker[2, 0].State);
    }

    [Fact]
    public void MatrixTraversalEngine_RunFloodFill_ShouldColorConnectedRegion()
    {
        var raw = new[]
        {
            "AAAA",
            "ABBA",
            "AAAA"
        };

        var grid = MatrixDataParser.Parse(raw);
        var tracker = MatrixTraversalEngine.RunFloodFill(grid, (1, 1), "#10b981");

        Assert.NotNull(tracker);
        Assert.Equal("#10b981", tracker[1, 1].CustomColor);
        Assert.Equal("#10b981", tracker[1, 2].CustomColor);
        Assert.Null(tracker[0, 0].CustomColor);
    }

    [Fact]
    public void GridMatrixRenderer_HitTest_ShouldIncludeStateSubLabelAndArrows()
    {
        var grid = new GridMatrixData(3, 3);
        grid[1, 1].DisplayValue = "42";
        grid[1, 1].SubLabel = "cost=15";
        grid[1, 1].State = GridCellState.Frontier;
        grid[1, 1].Heat = 0.75;
        grid[1, 1].Metadata["Heuristic"] = "10";
        grid[1, 1].Arrows.Add(new GridCellArrow { TargetRow = 0, TargetCol = 1 });

        var renderer = new GridMatrixRenderer();
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Matrix,
            MatrixData = grid
        };

        var bounds = new Rect(0, 0, 400, 400);
        var hit = renderer.HitTest(new Point(200, 200), bounds, options);

        Assert.NotNull(hit);
        Assert.Equal(1, hit.Row);
        Assert.Equal(1, hit.Col);
        Assert.Contains("Frontier", hit.Details);
        Assert.Contains("cost=15", hit.Details);
        Assert.True(hit.Details.Contains("Heat: 75%") || hit.Details.Contains("Heat: 75 %"), $"Expected 'Heat: 75%' in '{hit.Details}'");
        Assert.Contains("Heuristic: 10", hit.Details);
    }

    [Fact]
    public void MatrixDataParser_WithCustomOptions_ShouldUseClassifierAndHeatmap()
    {
        int[,] raw = new int[,]
        {
            { 10, 20 },
            { 30, 40 }
        };

        var opts = new MatrixParseOptions
        {
            AutoHeatmap = true,
            StateClassifier = val => (int?)val == 40 ? GridCellState.Target : null,
            SubLabelExtractor = val => $"v:{val}"
        };

        var grid = MatrixDataParser.Parse(raw, opts);

        Assert.Equal(0.0, grid[0, 0].Heat!.Value, 2);
        Assert.Equal(1.0, grid[1, 1].Heat!.Value, 2);
        Assert.Equal(GridCellState.Target, grid[1, 1].State);
        Assert.Equal("v:10", grid[0, 0].SubLabel);
        Assert.Equal("v:40", grid[1, 1].SubLabel);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithMatrixTrackerScript_ShouldExecuteWithoutErrors()
    {
        var kernel = new NotebookExecutionKernel();
        string script = @"
var maze = new[] { ""S.#"", ""..T"" };
var tracker = MatrixTracker.Create(maze, ""Kernel Maze"");
tracker.Visit(0, 0);
tracker.Enqueue(1, 0, from: (0, 0));
tracker.Visit(1, 0);
tracker.Enqueue(1, 1, from: (1, 0));
tracker.Visit(1, 1);
tracker.Visit(1, 2);
tracker.MarkPath(new[] { (0, 0), (1, 0), (1, 1), (1, 2) });
Display.Visualizer(tracker);
";

        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            script,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
        Assert.Equal(CellOutputKind.Visualizer, richOut.Kind);
        Assert.Equal("Kernel Maze", richOut.VisualizerOptions!.Title);
        Assert.True(richOut.VisualizerSequence!.TotalSteps >= 5);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithSpecializedRecordersStep_ShouldExecuteWithoutAmbiguity()
    {
        var kernel = new NotebookExecutionKernel();
        string script = @"
// Test 1: Canvas recorder Step with untyped lambda
var canvasRec = VisualizerRecorder.CreateCanvas(""Stack"", 400, 200);
canvasRec.Step(""Push 10"", s => s.AddRect(10, 10, 50, 50, label: ""10""));

// Test 2: Bars recorder Step with untyped lambda
var barsRec = VisualizerRecorder.CreateBars(new BarChartVisualizerData(new[] { 10, 20 }));
barsRec.Step(""Swap"", b => b.Items[0].Value = 99);

// Test 3: Board recorder Step with untyped lambda
var boardRec = VisualizerRecorder.CreateBoard(new BoardVisualizerData(2, 2));
boardRec.Step(""Place"", bd => bd[0, 0].Value = ""Q"");

// Test 4: Matrix recorder Step with untyped lambda
var matrixRec = VisualizerRecorder.CreateMatrix(new GridMatrixData(2, 2));
matrixRec.Step(""Visit"", m => m[0, 0].State = GridCellState.Visited);

Display.Visualizer(canvasRec);
";

        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            script,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithQuickSortVisualizerTemplate_ShouldExecuteAndSortAccurately()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "quicksort_sorting_bars");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            template.InitialCode,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
        Assert.Equal(CellOutputKind.Visualizer, richOut.Kind);
        Assert.NotNull(richOut.VisualizerSequence);
        Assert.True(richOut.VisualizerSequence.TotalSteps > 20);

        // Step 0 should be the initial unsorted array
        var initialStep = richOut.VisualizerSequence.Steps[0];
        var initialBars = Assert.IsType<BarChartVisualizerData>(initialStep.Snapshot);
        var initialValues = initialBars.Items.Select(i => (int)i.Value).ToArray();
        Assert.Equal(new[] { 45, 12, 85, 32, 89, 39, 67, 23, 91, 54 }, initialValues);

        // Final step should be completely sorted in ascending order
        var finalStep = richOut.VisualizerSequence.Steps[^1];
        var finalBars = Assert.IsType<BarChartVisualizerData>(finalStep.Snapshot);
        var finalValues = finalBars.Items.Select(i => (int)i.Value).ToArray();
        Assert.Equal(new[] { 12, 23, 32, 39, 45, 54, 67, 85, 89, 91 }, finalValues);
        Assert.All(finalBars.Items, item => Assert.True(item.IsSorted));
    }

    [Fact]
    public void TreeDataParser_ParseLeetCodeFormat_ShouldConstructBalancedTree()
    {
        string leetcode = "[3, 9, 20, null, null, 15, 7]";
        var tree = TreeDataParser.Parse(leetcode);

        Assert.NotNull(tree);
        Assert.Equal("3", tree.DisplayValue);
        Assert.Equal("9", tree.Left?.DisplayValue);
        Assert.Equal("20", tree.Right?.DisplayValue);
        Assert.Null(tree.Left?.Left);
        Assert.Null(tree.Left?.Right);
        Assert.Equal("15", tree.Right?.Left?.DisplayValue);
        Assert.Equal("7", tree.Right?.Right?.DisplayValue);

        string serialized = TreeDataParser.ToLeetCodeString(tree);
        Assert.Equal("[3, 9, 20, null, null, 15, 7]", serialized);
    }

    [Fact]
    public void TreeLayoutEngine_SingleChildBinaryTree_ShouldNotOverlapParentAndChild()
    {
        // Right-child only: 1 -> 2
        var rightOnlyRoot = new TreeNodeData("1")
        {
            Right = new TreeNodeData("2")
        };
        rightOnlyRoot.Children.Add(rightOnlyRoot.Right);

        TreeLayoutEngine.ComputeLayout(rightOnlyRoot);
        Assert.True(rightOnlyRoot.X < rightOnlyRoot.Right.X, $"Expected Parent.X ({rightOnlyRoot.X}) < RightChild.X ({rightOnlyRoot.Right.X})");

        // Left-child only: 2 -> 1
        var leftOnlyRoot = new TreeNodeData("2")
        {
            Left = new TreeNodeData("1")
        };
        leftOnlyRoot.Children.Add(leftOnlyRoot.Left);

        TreeLayoutEngine.ComputeLayout(leftOnlyRoot);
        Assert.True(leftOnlyRoot.Left.X < leftOnlyRoot.X, $"Expected LeftChild.X ({leftOnlyRoot.Left.X}) < Parent.X ({leftOnlyRoot.X})");
    }

    [Fact]
    public void TreeTracker_InvertBinaryTree_ShouldRecordSwappedStructureSnapshots()
    {
        var tracker = TreeTracker.Create("[4, 2, 7, 1, 3, 6, 9]", "Invert Tree Test");
        Assert.Equal("2", tracker.Root.Left?.DisplayValue);
        Assert.Equal("7", tracker.Root.Right?.DisplayValue);

        // Swap children
        tracker.SwapChildren(tracker.Root, "Swapped root subtrees");

        // Verify root is currently swapped
        Assert.Equal("7", tracker.Root.Left?.DisplayValue);
        Assert.Equal("2", tracker.Root.Right?.DisplayValue);

        // Check snapshots
        Assert.Equal(2, tracker.Sequence.TotalSteps);

        // Snapshot 0 should have original orientation
        var step0Tree = Assert.IsType<TreeNodeData>(tracker.Sequence.Steps[0].Snapshot);
        Assert.Equal("2", step0Tree.Left?.DisplayValue);
        Assert.Equal("7", step0Tree.Right?.DisplayValue);

        // Snapshot 1 should have swapped orientation
        var step1Tree = Assert.IsType<TreeNodeData>(tracker.Sequence.Steps[1].Snapshot);
        Assert.Equal("7", step1Tree.Left?.DisplayValue);
        Assert.Equal("2", step1Tree.Right?.DisplayValue);
    }

    [Fact]
    public void TreeTracker_ValidateBST_ShouldTrackRangeAnnotations()
    {
        var tracker = TreeTracker.Create("[5, 1, 4, null, null, 3, 6]", "BST Test");
        var node4 = tracker.ResolveNode("4");
        Assert.NotNull(node4);

        tracker.Visit(node4, "Checking node 4", subLabel: "(5, +inf)", pointer: "curr");
        tracker.Highlight(node4, TreeNodeState.Target, "Violation at node 4");

        Assert.Equal(3, tracker.Sequence.TotalSteps);
        var latestStep = tracker.Sequence.Steps[^1];
        var latestTree = Assert.IsType<TreeNodeData>(latestStep.Snapshot);
        var targetNode = latestTree.FindNode(node4.Id);
        Assert.NotNull(targetNode);
        Assert.Equal(TreeNodeState.Target, targetNode.State);
        Assert.Equal("(5, +inf)", targetNode.SubLabel);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithTreeTrackerInvertTree_ShouldExecuteSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "leetcode_226_invert_binary_tree");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            template.InitialCode,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
        Assert.Equal(CellOutputKind.Visualizer, richOut.Kind);
        Assert.NotNull(richOut.VisualizerSequence);
        Assert.True(richOut.VisualizerSequence.TotalSteps >= 5);

        // Initial step should have root.Left = 2 and root.Right = 7
        var firstStepTree = Assert.IsType<TreeNodeData>(richOut.VisualizerSequence.Steps[0].Snapshot);
        Assert.Equal("2", firstStepTree.Left?.DisplayValue);
        Assert.Equal("7", firstStepTree.Right?.DisplayValue);

        // Final step should have root.Left = 7 and root.Right = 2
        var finalStepTree = Assert.IsType<TreeNodeData>(richOut.VisualizerSequence.Steps[^1].Snapshot);
        Assert.Equal("7", finalStepTree.Left?.DisplayValue);
        Assert.Equal("2", finalStepTree.Right?.DisplayValue);
    }

    [Fact]
    public void GraphDataParser_ParseLeetCodeEdges_ShouldConstructNodesAndEdges()
    {
        string edges = "[[0,1,4],[0,2,2],[1,2,1]]";
        var graph = GraphDataParser.Parse(edges, isDirected: true);

        Assert.NotNull(graph);
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(3, graph.Edges.Count);

        var e01 = graph.FindEdge("0", "1");
        Assert.NotNull(e01);
        Assert.Equal(4.0, e01.Weight);

        var e02 = graph.FindEdge("0", "2");
        Assert.NotNull(e02);
        Assert.Equal(2.0, e02.Weight);

        string serialized = GraphDataParser.ToLeetCodeString(graph);
        Assert.Equal("[[0,1,4],[0,2,2],[1,2,1]]", serialized);
    }

    [Fact]
    public void GraphData_Clone_ShouldDeepCopyNodesAndEdges()
    {
        var graph = new GraphData { IsDirected = true };
        graph.Nodes.Add(new GraphNodeData("0") { State = GraphNodeState.Default, SubLabel = "d=inf" });
        graph.Nodes.Add(new GraphNodeData("1") { State = GraphNodeState.Default });
        graph.Edges.Add(new GraphEdgeData("0", "1", 5) { State = GraphEdgeState.Default });

        var clone = graph.Clone();
        Assert.Equal(2, clone.Nodes.Count);
        Assert.Single(clone.Edges);

        // Mutate original
        graph.Nodes[0].State = GraphNodeState.Visited;
        graph.Nodes[0].SubLabel = "d=0";
        graph.Edges[0].State = GraphEdgeState.Relaxed;

        // Clone should remain unmutated
        Assert.Equal(GraphNodeState.Default, clone.Nodes[0].State);
        Assert.Equal("d=inf", clone.Nodes[0].SubLabel);
        Assert.Equal(GraphEdgeState.Default, clone.Edges[0].State);
    }

    [Fact]
    public void GraphTracker_DijkstraShortestPath_ShouldRecordRelaxationsAndPath()
    {
        var tracker = GraphTracker.Create("[[0,1,4],[0,2,2],[1,2,1],[1,3,5]]", "Dijkstra Test", isDirected: true);
        tracker.SetPointer("0", "src");
        tracker.Visit("0", "Visited source", subLabel: "d=0");

        tracker.RelaxEdge("0", "2", weight: 2, newDistance: 2, note: "Relaxed 0->2");
        tracker.RelaxEdge("0", "1", weight: 4, newDistance: 4, note: "Relaxed 0->1");

        tracker.MarkPath(new[] { "0", "2" }, "Path found");

        Assert.True(tracker.Sequence.TotalSteps >= 4);

        // Check path edge in final step
        var finalStep = tracker.Sequence.Steps[^1];
        var finalGraph = Assert.IsType<GraphData>(finalStep.Snapshot);
        var pathEdge = finalGraph.FindEdge("0", "2");
        Assert.NotNull(pathEdge);
        Assert.Equal(GraphEdgeState.Path, pathEdge.State);
    }

    [Fact]
    public void GraphTracker_TopologicalSort_ShouldTrackInDegreesAndRemoval()
    {
        var tracker = GraphTracker.Create("[[0,1],[0,2],[1,3],[2,3]]", "TopoSort", isDirected: true);

        tracker.Annotate("0", "in=0");
        tracker.Annotate("1", "in=1");
        tracker.Annotate("2", "in=1");
        tracker.Annotate("3", "in=2");
        tracker.Snapshot("In-degrees assigned");

        tracker.Enqueue("0", "Enqueued in-degree 0 vertex");
        tracker.Visit("0", "Processed vertex 0");

        Assert.Equal(4, tracker.Sequence.TotalSteps);
        var step1 = tracker.Sequence.Steps[1];
        var g1 = Assert.IsType<GraphData>(step1.Snapshot);
        Assert.Equal("in=0", g1.FindNode("0")?.SubLabel);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithGraphTrackerDijkstra_ShouldExecuteSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "dijkstra_shortest_path");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(
            template.InitialCode,
            onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut);
        Assert.Equal(CellOutputKind.Visualizer, richOut.Kind);
        Assert.NotNull(richOut.VisualizerSequence);
        Assert.True(richOut.VisualizerSequence.TotalSteps >= 5);

        // Final step should have path marked
        var finalGraph = Assert.IsType<GraphData>(richOut.VisualizerSequence.Steps[^1].Snapshot);
        Assert.Contains(finalGraph.Nodes, n => n.State == GraphNodeState.Path);

        // Stale priority-queue entries are skipped, so every vertex is extracted exactly once
        var extractions = richOut.VisualizerSequence.Steps
            .Select(s => s.Description)
            .Where(d => d.StartsWith("Extracted vertex"))
            .ToList();
        Assert.Equal(6, extractions.Count);
        Assert.Equal(6, extractions.Distinct().Count());
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithCourseScheduleTemplate_ShouldDrawPrerequisiteToCourseEdges()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "leetcode_207_course_schedule");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => richOut = r);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut?.VisualizerSequence);

        // [1, 0] in LeetCode's input means "take 0 before 1", so the arrow must run 0 -> 1
        var graph = Assert.IsType<GraphData>(richOut.VisualizerSequence.Steps[^1].Snapshot);
        Assert.NotNull(graph.FindEdge("0", "1"));
        Assert.Null(graph.FindEdge("1", "0"));
        Assert.Contains("Valid order: [0, 1, 2, 3, 4]", richOut.VisualizerSequence.Steps[^1].Description);
    }

    [Fact]
    public void TreeDataParser_GenerateTraversalSteps_ShouldSnapshotWholeTreeWithAccumulatedVisits()
    {
        static IEnumerable<TreeNodeData> All(TreeNodeData n) => new[] { n }.Concat(n.Children.SelectMany(All));

        var tree = TreeDataParser.ParseLeetCodeString("[4, 2, 6, 1, 3, 5, 7]")!;
        var seq = TreeDataParser.GenerateTraversalSteps(tree, "InOrder");

        Assert.All(seq.Steps, s => Assert.Equal(7, All(Assert.IsType<TreeNodeData>(s.Snapshot)).Count()));

        // In-order visits 1, 2, 3 first: at step 3, nodes 1 and 2 are done and 3 is current
        var step3 = All((TreeNodeData)seq.Steps[3].Snapshot!).ToDictionary(n => n.DisplayValue);
        Assert.Equal(TreeNodeState.Visited, step3["1"].State);
        Assert.Equal(TreeNodeState.Visited, step3["2"].State);
        Assert.Equal(TreeNodeState.Current, step3["3"].State);
        Assert.Equal(TreeNodeState.Default, step3["4"].State);
        Assert.Equal(new[] { step3["3"].Id }, seq.Steps[3].ActiveNodeIds);

        Assert.All(All((TreeNodeData)seq.Steps[^1].Snapshot!), n => Assert.Equal(TreeNodeState.Visited, n.State));
        Assert.All(All(tree), n => Assert.Equal(TreeNodeState.Default, n.State));
    }

    [Fact]
    public void VisualizerRecorder_TreeStepWithValueId_ShouldHighlightMatchingNode()
    {
        var root = new TestTreeNode(10, new TestTreeNode(5), new TestTreeNode(15));
        var recorder = VisualizerRecorder.CreateTree(root, "BST");

        recorder.Step("Visit Node 5", activeNodeId: "5");

        var step = recorder.Sequence.Steps[^1];
        var snapshot = Assert.IsType<TreeNodeData>(step.Snapshot);
        Assert.Equal(new[] { snapshot.FindByValue("5")!.Id }, step.ActiveNodeIds);
    }

    [Fact]
    public void VisualizerRecorder_ArrayStep_ShouldCaptureInPlaceMutationsOfSourceArray()
    {
        var nums = new[] { 1, 2, 3, 4, 5 };
        var recorder = VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(nums), "Reverse");

        for (int l = 0, r = nums.Length - 1; l < r; l++, r--)
        {
            (nums[l], nums[r]) = (nums[r], nums[l]);
            recorder.Step($"Swap {l} and {r}", pointers: new { L = l, R = r });
        }

        string ValuesAt(int step) => string.Join(",",
            Assert.IsType<ArrayPointerData>(recorder.Sequence.Steps[step].Snapshot).Items.Select(i => i.DisplayValue));

        Assert.Equal("1,2,3,4,5", ValuesAt(0));
        Assert.Equal("5,2,3,4,1", ValuesAt(1));
        Assert.Equal("5,4,3,2,1", ValuesAt(2));
    }

    [Fact]
    public void LinkedListDataParser_ShouldWalkBclLinkedListAndPlainCollections()
    {
        var bcl = LinkedListDataParser.Parse(new LinkedList<int>(new[] { 1, 2, 3 }));
        Assert.Equal(new[] { "1", "2", "3" }, bcl.Nodes.Select(n => n.DisplayValue));
        Assert.Null(bcl.Nodes[^1].NextIndex);
        Assert.False(bcl.HasCycle);

        var fromList = LinkedListDataParser.Parse(new List<string> { "a", "b" });
        Assert.Equal(new[] { "a", "b" }, fromList.Nodes.Select(n => n.DisplayValue));
    }

    [Fact]
    public void GraphDataParser_UndirectedInputs_ShouldNotDuplicateEdges()
    {
        var adjacency = new Dictionary<int, List<int>> { [0] = new() { 1, 2 }, [1] = new() { 0 }, [2] = new() { 0 } };
        Assert.Equal(2, GraphDataParser.Parse(adjacency, isDirected: false).Edges.Count);
        Assert.Equal(4, GraphDataParser.Parse(adjacency, isDirected: true).Edges.Count);

        int[,] symmetric = { { 0, 3, 0 }, { 3, 0, 1 }, { 0, 1, 0 } };
        var fromMatrix = GraphDataParser.Parse(symmetric, isDirected: false);
        Assert.Equal(2, fromMatrix.Edges.Count);
        Assert.Equal(3.0, fromMatrix.FindEdge("1", "0")?.Weight);
    }

    private static async Task<VisualizerSequence> RunCellForSequenceAsync(NotebookExecutionKernel kernel, string code, string? sourceId = null)
    {
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(code, onRichOutput: r => richOut = r, sourceId: sourceId);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(richOut?.VisualizerOptions?.Sequence);
        return richOut.VisualizerOptions.Sequence;
    }

    [Fact]
    public async Task VisualizerSteps_RecordTheLineOfTheCallThatCreatedThem()
    {
        var seq = await RunCellForSequenceAsync(new NotebookExecutionKernel(), @"var nums = new[] { 3, 1, 2 };
var rec = VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(nums));

rec.Step(""compare"", pointers: new { I = 0 });
Display.Visualizer(rec);");

        Assert.Equal(new[] { 2, 4 }, seq.Steps.Select(s => s.SourceLine));
        Assert.All(seq.Steps, s => Assert.True(string.IsNullOrEmpty(s.SourceFile)));
    }

    [Fact]
    public async Task VisualizerSteps_InNotebookCells_RecordTheCellThatDefinedTheCall()
    {
        var kernel = new NotebookExecutionKernel();
        await kernel.ExecuteCellAsync(@"void Mark(VisualizerRecorder r)
{
    r.Step(""marked"");
}", sourceId: "cellA");

        var seq = await RunCellForSequenceAsync(kernel, @"var rec = VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(new[] { 1 }));
Mark(rec);
Display.Visualizer(rec);", sourceId: "cellB");

        Assert.Equal(("cellB", 1), (seq.Steps[0].SourceFile, seq.Steps[0].SourceLine));
        Assert.Equal(("cellA", 3), (seq.Steps[1].SourceFile, seq.Steps[1].SourceLine));
    }

    [Fact]
    public async Task VisualizerSteps_FromTrackers_RecordCallerLines()
    {
        var kernel = new NotebookExecutionKernel();
        RichCellOutput? richOut = null;
        var result = await kernel.ExecuteCellAsync(@"var tracker = TreeTracker.Create(""[2, 1, 3]"");
tracker.Visit(""1"");
tracker.Highlight(""3"", TreeNodeState.Matched);
Display.Visualizer(tracker);", onRichOutput: r => richOut = r, sourceId: "cellC");

        Assert.True(result.Success, result.ErrorMessage);
        var steps = richOut!.VisualizerOptions!.Sequence!.Steps;
        Assert.Equal(new[] { 1, 2, 3 }, steps.Select(s => s.SourceLine));
        Assert.All(steps, s => Assert.Equal("cellC", s.SourceFile));
    }

    [Fact]
    public void VisualizerSteps_FromBuiltInTraversals_AreNotLinkedToSource()
    {
        var grid = MatrixDataParser.Parse(new[] { "S..", ".#.", "..T" });
        var tracker = MatrixTraversalEngine.RunBfs(grid, (0, 0));

        Assert.True(tracker.Sequence.TotalSteps > 3);
        Assert.All(tracker.Sequence.Steps, s => Assert.Equal(0, s.SourceLine));
    }

    [Fact]
    public async Task NotebookKernel_CompileErrorsInNamedCells_KeepEditorLineNumbers()
    {
        var result = await new NotebookExecutionKernel().ExecuteCellAsync("int a = 1;\nint b = ;", sourceId: "cellD");

        Assert.False(result.Success);
        Assert.Equal(2, result.Diagnostics[0].Line);
    }

    private class NaryNode
    {
        public string Name { get; set; } = "";
        public List<NaryNode> Children { get; } = new();
    }

    private class Pair
    {
        public object? Left { get; set; }
        public object? Right { get; set; }
    }

    [Fact]
    public void DataStructureDetector_RecognizesTreesListsAndRectangularGrids()
    {
        var nary = new NaryNode { Name = "root" };
        nary.Children.Add(new NaryNode { Name = "a" });

        Assert.Equal(DataStructureShape.Tree, DataStructureDetector.Detect(new TestTreeNode(2, new TestTreeNode(1), new TestTreeNode(3))));
        Assert.Equal(DataStructureShape.Tree, DataStructureDetector.Detect(new PublicFieldTreeNode(1)));
        Assert.Equal(DataStructureShape.Tree, DataStructureDetector.Detect(nary));
        Assert.Equal(DataStructureShape.LinkedList, DataStructureDetector.Detect(new TestListNode(1, new TestListNode(2))));
        Assert.Equal(DataStructureShape.LinkedList, DataStructureDetector.Detect(new PublicFieldListNode(1)));
        Assert.Equal(DataStructureShape.LinkedList, DataStructureDetector.Detect(new LinkedList<int>(new[] { 1, 2 }).First));
        Assert.Equal(DataStructureShape.Grid, DataStructureDetector.Detect(new[,] { { 1, 0 }, { 0, 1 } }));
        Assert.Equal(DataStructureShape.Grid, DataStructureDetector.Detect(new[] { new[] { '1', '0' }, new[] { '0', '1' } }));
    }

    [Fact]
    public void DataStructureDetector_LeavesOrdinaryValuesToTheInspector()
    {
        var cyclic = new TestTreeNode(1);
        cyclic.left = cyclic;

        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect(new[] { new[] { 3 }, new[] { 9, 20 } }));
        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect(new[] { 1, 2, 3 }));
        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect(new List<int> { 1 }));
        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect("text"));
        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect(new Pair { Left = 1, Right = 2 }));
        Assert.Equal(DataStructureShape.None, DataStructureDetector.Detect(cyclic));
    }

    [Fact]
    public void TreeDataParser_ChildPointingBackAtAncestor_ParsesWithoutRecursingForever()
    {
        var root = new TestTreeNode(1, new TestTreeNode(2));
        root.left!.right = root;

        var tree = TreeDataParser.Parse(root);

        Assert.NotNull(tree);
        Assert.Equal("2", tree.Left?.DisplayValue);
        Assert.Null(tree.Left?.Right);
    }

    private static async Task<RichCellOutput?> LastOutputOfCellAsync(string code)
    {
        RichCellOutput? last = null;
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(code, onRichOutput: r => last = r);
        Assert.True(result.Success, result.ErrorMessage);
        return last;
    }

    [Theory]
    [InlineData("public class TreeNode { public int val; public TreeNode? left, right; public TreeNode(int v, TreeNode? l = null, TreeNode? r = null) { val = v; left = l; right = r; } }\nvar root = new TreeNode(2, new TreeNode(1), new TreeNode(3));\nroot", VisualizerKind.Tree, "TreeNode")]
    [InlineData("public class ListNode { public int val; public ListNode? next; public ListNode(int v, ListNode? n = null) { val = v; next = n; } }\nvar head = new ListNode(1, new ListNode(2, new ListNode(3)));\nhead", VisualizerKind.LinkedList, "ListNode")]
    [InlineData("new[,] { { 1, 1, 0 }, { 0, 1, 0 } }", VisualizerKind.Matrix, null)]
    [InlineData("TreeTracker.Create(\"[1, 2, 3]\", \"Tracked\")", VisualizerKind.Tree, "Tracked")]
    [InlineData("VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(new[] { 4, 5 }), \"Recorded\")", VisualizerKind.ArrayPointers, "Recorded")]
    [InlineData("LinkedListTracker.Create(null, \"Tracked list\")", VisualizerKind.LinkedList, "Tracked list")]
    [InlineData("RecursionTracker.Create(\"Tracked calls\")", VisualizerKind.Tree, "Tracked calls")]
    public async Task NotebookKernel_ReturnedDataStructure_IsDrawnAsVisualizer(string code, VisualizerKind expectedKind, string? expectedTitle)
    {
        var output = await LastOutputOfCellAsync(code);

        Assert.Equal(CellOutputKind.Visualizer, output?.Kind);
        Assert.Equal(expectedKind, output!.VisualizerOptions!.Kind);
        if (expectedTitle != null) Assert.Equal(expectedTitle, output.VisualizerOptions.Title);
    }

    [Fact]
    public async Task NotebookKernel_ReturnedRaggedLists_StayInTheObjectInspector()
    {
        var output = await LastOutputOfCellAsync("new[] { new[] { 3 }, new[] { 9, 20 }, new[] { 15, 7 } }");

        Assert.Equal(CellOutputKind.ObjectInspector, output?.Kind);
    }

    [Fact]
    public void GraphRenderer_HitTest_ShouldFollowZoomedNodePositions()
    {
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Graph,
            GraphData = GraphDataParser.Parse("[[0,1]]"),
            Zoom = 0.5
        };
        var bounds = new Rect(0, 0, 400, 300);
        var renderer = new GraphRenderer();

        // Node "0" lays out at (200, 40); at 50% zoom it sits halfway to the canvas centre (200, 150)
        Assert.Equal("0", renderer.HitTest(new Point(200, 95), bounds, options)?.NodeId);
        Assert.Null(renderer.HitTest(new Point(200, 40), bounds, options));
    }
}



