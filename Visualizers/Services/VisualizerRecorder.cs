using System;
using System.Collections.Generic;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class VisualizerRecorder
{
    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    public VisualizerRecorder(VisualizerOptions options)
    {
        Options = options;
        Sequence = new VisualizerSequence();
        Options.Sequence = Sequence;
    }

    public static MatrixVisualizerRecorder CreateMatrix(GridMatrixData grid, string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Matrix Visualizer",
            Kind = VisualizerKind.Matrix,
            MatrixData = grid
        };
        var recorder = new MatrixVisualizerRecorder(options);
        recorder.Step("Initial Grid State");
        return recorder;
    }

    public static VisualizerRecorder CreateArray(ArrayPointerData arrayData, string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Array & Pointers Visualizer",
            Kind = VisualizerKind.ArrayPointers,
            ArrayData = arrayData
        };
        var recorder = new VisualizerRecorder(options);
        recorder.Step("Initial Array State");
        return recorder;
    }

    public static TreeVisualizerRecorder CreateTree(object root, string? title = null)
    {
        var treeData = TreeDataParser.Parse(root);
        var options = new VisualizerOptions
        {
            Title = title ?? "Tree Visualizer",
            Kind = VisualizerKind.Tree,
            TreeData = treeData
        };
        var recorder = new TreeVisualizerRecorder(options);
        recorder.Step("Initial Tree State");
        return recorder;
    }

    public static GraphVisualizerRecorder CreateGraph(object graph, string? title = null, bool isDirected = true)
    {
        var graphData = GraphDataParser.Parse(graph, isDirected);
        var options = new VisualizerOptions
        {
            Title = title ?? (isDirected ? "Directed Graph Visualizer" : "Undirected Graph Visualizer"),
            Kind = VisualizerKind.Graph,
            GraphData = graphData
        };
        var recorder = new GraphVisualizerRecorder(options);
        recorder.Step("Initial Graph State");
        return recorder;
    }

    public static BarVisualizerRecorder CreateBars(BarChartVisualizerData bars, string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Sorting & Array Bars",
            Kind = VisualizerKind.Bars,
            BarData = bars
        };
        var recorder = new BarVisualizerRecorder(options);
        recorder.Step("Initial Bar State");
        return recorder;
    }

    public static BoardVisualizerRecorder CreateBoard(BoardVisualizerData board, string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Board & DP Visualizer",
            Kind = VisualizerKind.Board,
            BoardData = board
        };
        var recorder = new BoardVisualizerRecorder(options);
        recorder.Step("Initial Board State");
        return recorder;
    }

    public static CanvasVisualizerRecorder CreateCanvas(string? title = null, double width = 600, double height = 300)
    {
        var scene = new VisualizerScene(width, height);
        var options = new VisualizerOptions
        {
            Title = title ?? "Custom Canvas Visualizer",
            Kind = VisualizerKind.Canvas,
            SceneData = scene
        };
        var recorder = new CanvasVisualizerRecorder(options);
        recorder.Step("Initial Scene");
        return recorder;
    }

    public VisualizerRecorder StepScene(string description, Action<VisualizerScene> draw)
    {
        if (Options.SceneData == null)
        {
            Options.SceneData = new VisualizerScene();
        }

        // Clone current scene before applying mutations
        var nextScene = Options.SceneData.Clone();
        draw(nextScene);
        Options.SceneData = nextScene;

        var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
        {
            Snapshot = nextScene.Clone()
        };
        Sequence.AddStep(step);
        return this;
    }

    public VisualizerRecorder StepBars(string description, Action<BarChartVisualizerData>? updateBars = null)
    {
        if (Options.BarData != null)
        {
            updateBars?.Invoke(Options.BarData);

            var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
            {
                Snapshot = Options.BarData.Clone()
            };
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepBoard(string description, Action<BoardVisualizerData>? updateBoard = null)
    {
        if (Options.BoardData != null)
        {
            updateBoard?.Invoke(Options.BoardData);

            var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
            {
                Snapshot = Options.BoardData.Clone()
            };
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepMatrix(string description, Action<GridMatrixData>? updateMatrix = null)
    {
        if (Options.MatrixData != null)
        {
            updateMatrix?.Invoke(Options.MatrixData);

            var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
            {
                Snapshot = Options.MatrixData.Clone()
            };
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepTree(string description, Action<TreeNodeData>? updateTree = null)
    {
        if (Options.TreeData != null)
        {
            updateTree?.Invoke(Options.TreeData);

            var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
            {
                Snapshot = Options.TreeData.Clone()
            };
            Sequence.AddStep(step);
        }
        return this;
    }

    public VisualizerRecorder StepGraph(string description, Action<GraphData>? updateGraph = null)
    {
        if (Options.GraphData != null)
        {
            updateGraph?.Invoke(Options.GraphData);

            var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind)
            {
                Snapshot = Options.GraphData.Clone()
            };
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
        IDictionary<string, string>? auxiliaryInfo = null)
    {
        var step = new VisualizerStep(Sequence.TotalSteps, description, Options.Kind);

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

        Sequence.AddStep(step);
        return this;
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

    public CanvasVisualizerRecorder Step(string description, Action<VisualizerScene> draw)
    {
        StepScene(description, draw);
        return this;
    }
}

public class BarVisualizerRecorder : VisualizerRecorder
{
    public BarVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public BarVisualizerRecorder Step(string description)
    {
        StepBars(description, null);
        return this;
    }

    public BarVisualizerRecorder Step(string description, Action<BarChartVisualizerData> updateBars)
    {
        StepBars(description, updateBars);
        return this;
    }
}

public class BoardVisualizerRecorder : VisualizerRecorder
{
    public BoardVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public BoardVisualizerRecorder Step(string description)
    {
        StepBoard(description, null);
        return this;
    }

    public BoardVisualizerRecorder Step(string description, Action<BoardVisualizerData> updateBoard)
    {
        StepBoard(description, updateBoard);
        return this;
    }
}

public class MatrixVisualizerRecorder : VisualizerRecorder
{
    public MatrixVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public MatrixVisualizerRecorder Step(string description)
    {
        StepMatrix(description, null);
        return this;
    }

    public MatrixVisualizerRecorder Step(string description, Action<GridMatrixData> updateMatrix)
    {
        StepMatrix(description, updateMatrix);
        return this;
    }
}

public class TreeVisualizerRecorder : VisualizerRecorder
{
    public TreeVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public TreeVisualizerRecorder Step(string description)
    {
        StepTree(description, null);
        return this;
    }

    public TreeVisualizerRecorder Step(string description, Action<TreeNodeData> updateTree)
    {
        StepTree(description, updateTree);
        return this;
    }
}

public class GraphVisualizerRecorder : VisualizerRecorder
{
    public GraphVisualizerRecorder(VisualizerOptions options) : base(options) { }

    public GraphVisualizerRecorder Step(string description)
    {
        StepGraph(description, null);
        return this;
    }

    public GraphVisualizerRecorder Step(string description, Action<GraphData> updateGraph)
    {
        StepGraph(description, updateGraph);
        return this;
    }
}


