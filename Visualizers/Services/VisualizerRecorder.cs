using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class VisualizerRecorder
{
    private readonly WatchList _watches = new();

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    public VisualizerRecorder(VisualizerOptions options)
    {
        Options = options;
        Sequence = new VisualizerSequence();
        Options.Sequence = Sequence;
    }

    /// <summary>Shows a live queue, stack, set, map or list beneath the visualizer at every step recorded after this call.</summary>
    public VisualizerRecorder Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "")
    {
        _watches.Add(collection, name);
        return this;
    }

    public static MatrixVisualizerRecorder CreateMatrix(
        GridMatrixData grid,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Matrix Visualizer",
            Kind = VisualizerKind.Matrix,
            MatrixData = grid
        };
        var recorder = new MatrixVisualizerRecorder(options);
        recorder.Step("Initial Grid State", sourceLine, sourceFile);
        return recorder;
    }

    public static VisualizerRecorder CreateArray(
        ArrayPointerData arrayData,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Array & Pointers Visualizer",
            Kind = VisualizerKind.ArrayPointers,
            ArrayData = arrayData
        };
        var recorder = new VisualizerRecorder(options);
        recorder.Step("Initial Array State", sourceLine: sourceLine, sourceFile: sourceFile);
        return recorder;
    }

    public static TreeVisualizerRecorder CreateTree(
        object root,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var treeData = TreeDataParser.Parse(root);
        var options = new VisualizerOptions
        {
            Title = title ?? "Tree Visualizer",
            Kind = VisualizerKind.Tree,
            TreeData = treeData
        };
        var recorder = new TreeVisualizerRecorder(options);
        recorder.Step("Initial Tree State", sourceLine, sourceFile);
        return recorder;
    }

    public static GraphVisualizerRecorder CreateGraph(
        object graph,
        string? title = null,
        bool isDirected = true,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var graphData = GraphDataParser.Parse(graph, isDirected);
        var options = new VisualizerOptions
        {
            Title = title ?? (isDirected ? "Directed Graph Visualizer" : "Undirected Graph Visualizer"),
            Kind = VisualizerKind.Graph,
            GraphData = graphData
        };
        var recorder = new GraphVisualizerRecorder(options);
        recorder.Step("Initial Graph State", sourceLine, sourceFile);
        return recorder;
    }

    public static BarVisualizerRecorder CreateBars(
        BarChartVisualizerData bars,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Sorting & Array Bars",
            Kind = VisualizerKind.Bars,
            BarData = bars
        };
        var recorder = new BarVisualizerRecorder(options);
        recorder.Step("Initial Bar State", sourceLine, sourceFile);
        return recorder;
    }

    public static BoardVisualizerRecorder CreateBoard(
        BoardVisualizerData board,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Board & DP Visualizer",
            Kind = VisualizerKind.Board,
            BoardData = board
        };
        var recorder = new BoardVisualizerRecorder(options);
        recorder.Step("Initial Board State", sourceLine, sourceFile);
        return recorder;
    }

    public static CanvasVisualizerRecorder CreateCanvas(
        string? title = null,
        double width = 600,
        double height = 300,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var scene = new VisualizerScene(width, height);
        var options = new VisualizerOptions
        {
            Title = title ?? "Custom Canvas Visualizer",
            Kind = VisualizerKind.Canvas,
            SceneData = scene
        };
        var recorder = new CanvasVisualizerRecorder(options);
        recorder.Step("Initial Scene", sourceLine: sourceLine, sourceFile: sourceFile);
        return recorder;
    }

    public VisualizerRecorder StepScene(
        string description,
        Action<VisualizerScene> draw,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.SceneData == null)
        {
            Options.SceneData = new VisualizerScene();
        }

        // Clone current scene before applying mutations
        var nextScene = Options.SceneData.Clone();
        draw(nextScene);
        Options.SceneData = nextScene;

        var step = NewStep(description, sourceLine, sourceFile);
        step.Snapshot = nextScene.Clone();
        Sequence.AddStep(step);
        return this;
    }

    public VisualizerRecorder StepBars(
        string description,
        Action<BarChartVisualizerData>? updateBars = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.BarData != null)
        {
            updateBars?.Invoke(Options.BarData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.BarData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepBoard(
        string description,
        Action<BoardVisualizerData>? updateBoard = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.BoardData != null)
        {
            updateBoard?.Invoke(Options.BoardData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.BoardData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepMatrix(
        string description,
        Action<GridMatrixData>? updateMatrix = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.MatrixData != null)
        {
            updateMatrix?.Invoke(Options.MatrixData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.MatrixData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepTree(
        string description,
        Action<TreeNodeData>? updateTree = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.TreeData != null)
        {
            updateTree?.Invoke(Options.TreeData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.TreeData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepGraph(
        string description,
        Action<GraphData>? updateGraph = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (Options.GraphData != null)
        {
            updateGraph?.Invoke(Options.GraphData);

            var step = NewStep(description, sourceLine, sourceFile);
            step.Snapshot = Options.GraphData.Clone();
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder Step(
        string description,
        (int Row, int Col)? activeCell = null,
        IEnumerable<(int Row, int Col)>? activeCells = null,
        string? activeNodeId = null,
        IEnumerable<string>? activeNodeIds = null,
        object? pointers = null,
        IDictionary<string, string>? auxiliaryInfo = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var step = NewStep(description, sourceLine, sourceFile);

        if (activeCell.HasValue)
        {
            step.ActiveCells.Add(activeCell.Value);
        }
        if (activeCells != null)
        {
            step.ActiveCells.AddRange(activeCells);
        }

        if (!string.IsNullOrEmpty(activeNodeId))
        {
            step.ActiveNodeIds.Add(activeNodeId);
        }
        if (activeNodeIds != null)
        {
            step.ActiveNodeIds.AddRange(activeNodeIds);
        }

        if (auxiliaryInfo != null)
        {
            foreach (var kvp in auxiliaryInfo)
            {
                step.AuxiliaryInfo[kvp.Key] = kvp.Value;
            }
        }

        if (pointers != null)
        {
            ExtractPointers(pointers, step);
        }

        ResolveActiveNodeIds(step);

        // Take snapshot based on active data kind
        if (Options.MatrixData != null)
        {
            step.Snapshot = Options.MatrixData.Clone();
        }
        else if (Options.BarData != null)
        {
            step.Snapshot = Options.BarData.Clone();
        }
        else if (Options.BoardData != null)
        {
            step.Snapshot = Options.BoardData.Clone();
        }
        else if (Options.SceneData != null)
        {
            step.Snapshot = Options.SceneData.Clone();
        }
        else if (Options.TreeData != null)
        {
            step.Snapshot = Options.TreeData.Clone();
        }
        else if (Options.GraphData != null)
        {
            step.Snapshot = Options.GraphData.Clone();
        }
        else if (Options.ArrayData != null)
        {
            Options.ArrayData.RefreshFromSource();
            step.Snapshot = Options.ArrayData.Clone();
        }

        Sequence.AddStep(step);
        return this;
    }

    private VisualizerStep NewStep(string description, int sourceLine, string sourceFile) =>
        new(Sequence.TotalSteps, description, Options.Kind)
        {
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

    // Parsed nodes get generated ids (node_1, ...), but callers naturally pass the node's value ("10").
    private void ResolveActiveNodeIds(VisualizerStep step)
    {
        for (int i = 0; i < step.ActiveNodeIds.Count; i++)
        {
            var id = step.ActiveNodeIds[i];
            var resolved = Options.TreeData != null
                ? (Options.TreeData.FindNode(id) ?? Options.TreeData.FindByValue(id))?.Id
                : Options.GraphData?.FindNode(id)?.Id;

            if (resolved != null)
            {
                step.ActiveNodeIds[i] = resolved;
            }
        }
    }

    private void ExtractPointers(object pointers, VisualizerStep step)
    {
        if (Options.ArrayData == null) return;

        var type = pointers.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var pointerList = new List<PointerMarkerData>();

        foreach (var prop in props)
        {
            var val = prop.GetValue(pointers);
            if (val is int idx)
            {
                string name = prop.Name;
                string color = VisualizerPaletteService.GetPointerColor(name);
                pointerList.Add(new PointerMarkerData(name, idx, color));
                step.AuxiliaryInfo[name] = $"[{idx}]";
            }
        }

        step.CustomData = pointerList;
    }

    public VisualizerSequence ToSequence() => Sequence;
}

public class CanvasVisualizerRecorder : VisualizerRecorder
{
    public CanvasVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public CanvasVisualizerRecorder Step(
        string description,
        Action<VisualizerScene> draw,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepScene(description, draw, sourceLine, sourceFile);
        return this;
    }
}

public class BarVisualizerRecorder : VisualizerRecorder
{
    public BarVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public BarVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBars(description, null, sourceLine, sourceFile);
        return this;
    }

    public BarVisualizerRecorder Step(
        string description,
        Action<BarChartVisualizerData> updateBars,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBars(description, updateBars, sourceLine, sourceFile);
        return this;
    }
}

public class BoardVisualizerRecorder : VisualizerRecorder
{
    public BoardVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public BoardVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBoard(description, null, sourceLine, sourceFile);
        return this;
    }

    public BoardVisualizerRecorder Step(
        string description,
        Action<BoardVisualizerData> updateBoard,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepBoard(description, updateBoard, sourceLine, sourceFile);
        return this;
    }
}

public class MatrixVisualizerRecorder : VisualizerRecorder
{
    public MatrixVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public MatrixVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepMatrix(description, null, sourceLine, sourceFile);
        return this;
    }

    public MatrixVisualizerRecorder Step(
        string description,
        Action<GridMatrixData> updateMatrix,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepMatrix(description, updateMatrix, sourceLine, sourceFile);
        return this;
    }
}

public class TreeVisualizerRecorder : VisualizerRecorder
{
    public TreeVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public TreeVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepTree(description, null, sourceLine, sourceFile);
        return this;
    }

    public TreeVisualizerRecorder Step(
        string description,
        Action<TreeNodeData> updateTree,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepTree(description, updateTree, sourceLine, sourceFile);
        return this;
    }
}

public class GraphVisualizerRecorder : VisualizerRecorder
{
    public GraphVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public GraphVisualizerRecorder Step(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepGraph(description, null, sourceLine, sourceFile);
        return this;
    }

    public GraphVisualizerRecorder Step(
        string description,
        Action<GraphData> updateGraph,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        StepGraph(description, updateGraph, sourceLine, sourceFile);
        return this;
    }
}
