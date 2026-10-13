using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// Turns a visualizer spec into what the visualizer control draws: the data of its kind, its steps (built as they are
/// looked at), and the steps the studio records itself when a spec asks for them: finding islands, a tree traversal,
/// cycle detection in a list.
/// </summary>
public static class VisualizerRenderModelBuilder
{
    public static VisualizerOptions Build(VisualizerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var kind = VisualizerKinds.For(spec.Kind);
        var notices = new List<string>();
        var options = new VisualizerOptions
        {
            Title = spec.Title ?? string.Empty,
            Subtitle = spec.Subtitle ?? string.Empty,
            Kind = spec.Kind,
            Summary = spec.Summary,
            ShowCoordinates = spec.ShowCoordinates ?? true,
            ShowValues = spec.ShowValues ?? true,
            CellSize = spec.CellSize ?? VisualizerRenderDefaults.CellSize,
            FitOnOpen = spec.FitOnOpen ?? true,
            Width = spec.Width,
            Height = spec.Height
        };

        var initial = kind.Build(spec.State, spec, notices);
        if (spec.Steps.Count > 0)
        {
            kind.Assign(options, initial);
            options.Sequence = VisualizerStepStore.CreateSequence(spec, kind, initial, notices);
        }
        else
        {
            kind.Assign(options, kind.DecorateModel(initial, spec.Highlight ?? [], spec.Pointers ?? [], onStep: false));
            RecordStudioSteps(spec, options);
        }

        if (string.IsNullOrEmpty(options.Title)) options.Title = DefaultTitle(options);
        options.Notice = notices.Count > 0 ? string.Join(" · ", notices.Distinct()) : null;
        return options;
    }

    // Steps a spec asks the studio to work out, so every language gets them without writing the algorithm.
    private static void RecordStudioSteps(VisualizerSpec spec, VisualizerOptions options)
    {
        switch (spec.Kind)
        {
            case VisualizerKind.Islands when options.MatrixData is { } grid:
                var record = spec.Islands?.RecordSteps ?? true;
                var (_, sequence) = IslandDetectorService.DetectAndGenerateSteps(grid, recordSteps: record, fourDirectional: spec.Islands?.FourDirectional ?? true);
                options.Sequence = record ? sequence : null;
                break;
            case VisualizerKind.Tree when spec.Traversal is { } traversal && options.TreeData is { } root:
                options.Sequence = TreeDataParser.GenerateTraversalSteps(root, TraversalName(traversal));
                break;
            case VisualizerKind.LinkedList when spec.DetectCycle == true && options.LinkedListData is { } list:
                var head = spec.State.LinkedList?.Head is { } id ? Math.Max(0, list.Nodes.FindIndex(n => n.Id == id)) : 0;
                options.Sequence = LinkedListDataParser.GenerateCycleDetectionSteps(list, head);
                break;
        }
    }

    private static string TraversalName(TreeTraversal traversal) => traversal switch
    {
        TreeTraversal.Inorder => "inorder",
        TreeTraversal.Postorder => "postorder",
        TreeTraversal.LevelOrder => "levelorder",
        _ => "preorder"
    };

    // The titles Display.Matrix, Display.Tree… have always given.
    private static string DefaultTitle(VisualizerOptions options) => options.Kind switch
    {
        VisualizerKind.Matrix => "Matrix Visualizer",
        VisualizerKind.Islands => $"Number of Islands ({options.MatrixData?.Islands.Count ?? 0} Found)",
        VisualizerKind.Tree => "Tree Visualizer",
        VisualizerKind.Graph => "Graph Visualizer",
        VisualizerKind.LinkedList => options.LinkedListData?.HasCycle == true ? "Linked List (Cycle Detected)" : "Linked List Visualizer",
        VisualizerKind.ArrayPointers => "Array & Pointers Visualizer",
        VisualizerKind.Bars => "Sorting & Array Bars",
        VisualizerKind.Board => "Board & DP Visualizer",
        _ => "Custom Canvas Visualizer"
    };
}
