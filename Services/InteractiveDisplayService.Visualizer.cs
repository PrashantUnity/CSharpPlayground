using System;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Display
{
    public static InteractiveVisualizerControl Visualizer(VisualizerOptions options)
    {
        InteractiveVisualizerControl? control = null;
        try
        {
            // Script execution runs on a background thread; the control must still be built on
            // the UI thread, or later UI-thread access (e.g. clicking Play/Pause) throws because
            // Avalonia bound its properties to the wrong thread on first touch.
            control = Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess()
                ? Dispatcher.UIThread.Invoke(() => new InteractiveVisualizerControl(options))
                : new InteractiveVisualizerControl(options);
        }
        catch
        {
            // Headless test runner without Avalonia platform rendering
        }

        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Visualizer,
            InteractiveControl = control,
            VisualizerOptions = options
        });

        return control!;
    }

    public static InteractiveVisualizerControl Visualizer(VisualizerRecorder recorder)
    {
        return Visualizer(recorder.Options);
    }

    public static InteractiveVisualizerControl Visualizer(MatrixTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(TreeTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(GraphTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(LinkedListTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(RecursionTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(IntervalTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Visualizer(TrieTracker tracker)
    {
        return Visualizer(tracker.Options);
    }

    public static InteractiveVisualizerControl Islands(
        object grid,
        string? title = null,
        bool recordSteps = true,
        bool fourDirectional = true)
    {
        var matrixData = MatrixDataParser.Parse(grid);
        var (islands, sequence) = IslandDetectorService.DetectAndGenerateSteps(
            matrixData,
            recordSteps: recordSteps,
            fourDirectional: fourDirectional);

        var options = new VisualizerOptions
        {
            Title = title ?? $"Number of Islands ({islands.Count} Found)",
            Kind = VisualizerKind.Islands,
            MatrixData = matrixData,
            Sequence = recordSteps ? sequence : null
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Matrix(
        object grid,
        string? title = null,
        bool showCoordinates = true,
        bool showValues = true,
        double cellSize = 38.0)
    {
        var matrixData = MatrixDataParser.Parse(grid, cellSize);
        matrixData.ShowCoordinates = showCoordinates;
        matrixData.ShowValues = showValues;

        var options = new VisualizerOptions
        {
            Title = title ?? "Matrix Visualizer",
            Kind = VisualizerKind.Matrix,
            MatrixData = matrixData,
            ShowCoordinates = showCoordinates,
            ShowValues = showValues,
            CellSize = cellSize
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Tree(
        object root,
        string? title = null,
        string? recordTraversal = null)
    {
        if (root is TreeTracker tracker)
        {
            if (!string.IsNullOrEmpty(title)) tracker.Options.Title = title;
            return Visualizer(tracker);
        }

        var treeData = TreeDataParser.Parse(root);
        VisualizerSequence? sequence = null;

        if (treeData != null && !string.IsNullOrEmpty(recordTraversal))
        {
            sequence = TreeDataParser.GenerateTraversalSteps(treeData, recordTraversal);
        }

        var options = new VisualizerOptions
        {
            Title = title ?? "Tree Visualizer",
            Kind = VisualizerKind.Tree,
            TreeData = treeData,
            Sequence = sequence
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Graph(
        object graph,
        string? title = null,
        bool isDirected = true)
    {
        if (graph is GraphTracker tracker)
        {
            if (!string.IsNullOrEmpty(title)) tracker.Options.Title = title;
            return Visualizer(tracker);
        }

        var graphData = GraphDataParser.Parse(graph, isDirected);

        var options = new VisualizerOptions
        {
            Title = title ?? "Graph Visualizer",
            Kind = VisualizerKind.Graph,
            GraphData = graphData
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl LinkedList(
        object head,
        string? title = null,
        bool recordCycleSteps = false)
    {
        var listData = LinkedListDataParser.Parse(head);
        VisualizerSequence? sequence = null;

        if (recordCycleSteps)
        {
            sequence = LinkedListDataParser.GenerateCycleDetectionSteps(head);
        }

        var options = new VisualizerOptions
        {
            Title = title ?? (listData.HasCycle ? "Linked List (Cycle Detected)" : "Linked List Visualizer"),
            Kind = VisualizerKind.LinkedList,
            LinkedListData = listData,
            Sequence = sequence
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Array(
        object array,
        object? pointers = null,
        string? title = null)
    {
        var arrayData = ArrayPointerDataParser.Parse(array, pointers);

        var options = new VisualizerOptions
        {
            Title = title ?? "Array & Pointers Visualizer",
            Kind = VisualizerKind.ArrayPointers,
            ArrayData = arrayData
        };

        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Bars(
        IEnumerable<int> values,
        string? title = null)
    {
        var barData = new BarChartVisualizerData(values);
        return Bars(barData, title);
    }

    public static InteractiveVisualizerControl Bars(
        IEnumerable<double> values,
        string? title = null)
    {
        var barData = new BarChartVisualizerData(values);
        return Bars(barData, title);
    }

    public static InteractiveVisualizerControl Bars(
        BarChartVisualizerData barData,
        string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Sorting & Array Bars",
            Kind = VisualizerKind.Bars,
            BarData = barData
        };
        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Board(
        BoardVisualizerData boardData,
        string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? (boardData.Title ?? "Board & DP Visualizer"),
            Kind = VisualizerKind.Board,
            BoardData = boardData
        };
        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Canvas(
        VisualizerScene scene,
        string? title = null)
    {
        var options = new VisualizerOptions
        {
            Title = title ?? "Custom Canvas Visualizer",
            Kind = VisualizerKind.Canvas,
            SceneData = scene
        };
        return Visualizer(options);
    }

    public static InteractiveVisualizerControl Canvas(
        Action<VisualizerScene> draw,
        string? title = null,
        double width = 600,
        double height = 300)
    {
        var scene = new VisualizerScene(width, height);
        draw(scene);
        return Canvas(scene, title);
    }
}

public static partial class DisplayExtensions
{
    public static T DisplayIslands<T>(
        this T grid,
        string? title = null,
        bool recordSteps = true,
        bool fourDirectional = true)
    {
        if (grid != null)
        {
            Display.Islands(grid, title, recordSteps, fourDirectional);
        }
        return grid;
    }

    public static T DisplayMatrix<T>(
        this T grid,
        string? title = null,
        bool showCoordinates = true,
        bool showValues = true)
    {
        if (grid != null)
        {
            Display.Matrix(grid, title, showCoordinates, showValues);
        }
        return grid;
    }

    public static T DisplayTree<T>(
        this T root,
        string? title = null,
        string? recordTraversal = null)
    {
        if (root != null)
        {
            Display.Tree(root, title, recordTraversal);
        }
        return root;
    }

    public static T DisplayGraph<T>(
        this T graph,
        string? title = null,
        bool isDirected = true)
    {
        if (graph != null)
        {
            Display.Graph(graph, title, isDirected);
        }
        return graph;
    }

    public static T DisplayLinkedList<T>(
        this T head,
        string? title = null,
        bool recordCycleSteps = false)
    {
        if (head != null)
        {
            Display.LinkedList(head, title, recordCycleSteps);
        }
        return head;
    }

    public static T DisplayArray<T>(
        this T array,
        object? pointers = null,
        string? title = null)
    {
        if (array != null)
        {
            Display.Array(array, pointers, title);
        }
        return array;
    }

    public static BarChartVisualizerData DisplayBars(
        this BarChartVisualizerData barData,
        string? title = null)
    {
        Display.Bars(barData, title);
        return barData;
    }

    public static BoardVisualizerData DisplayBoard(
        this BoardVisualizerData boardData,
        string? title = null)
    {
        Display.Board(boardData, title);
        return boardData;
    }

    public static VisualizerScene DisplayCanvas(
        this VisualizerScene scene,
        string? title = null)
    {
        Display.Canvas(scene, title);
        return scene;
    }

    public static VisualizerRecorder? DisplayVisualizer(this VisualizerRecorder? recorder)
    {
        if (recorder != null)
        {
            Display.Visualizer(recorder);
        }
        return recorder;
    }

    public static MatrixTracker? DisplayVisualizer(this MatrixTracker? tracker)
    {
        if (tracker != null)
        {
            Display.Visualizer(tracker);
        }
        return tracker;
    }
}
