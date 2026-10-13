using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

/// <summary>
/// A visualizer the C# side built (a <see cref="VisualizerOptions"/> with its recorded steps) as a spec. Each step says
/// only what changed since the one before when changes can say it, and carries a whole state otherwise; a step that
/// changed nothing carries neither. Highlights and pointers become the step's own.
/// </summary>
public static class VisualizerOptionsConverter
{
    public static VisualizerSpec ToSpec(VisualizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var kind = VisualizerKinds.For(options.Kind);
        var spec = new VisualizerSpec
        {
            Title = string.IsNullOrEmpty(options.Title) ? null : options.Title,
            Subtitle = string.IsNullOrEmpty(options.Subtitle) ? null : options.Subtitle,
            Kind = options.Kind,
            Summary = options.Summary,
            ShowCoordinates = options.ShowCoordinates && (options.MatrixData?.ShowCoordinates ?? options.BoardData?.ShowCoordinates ?? true),
            ShowValues = options.ShowValues && (options.MatrixData?.ShowValues ?? options.BarData?.ShowValues ?? true),
            CellSize = options.MatrixData?.CellSize ?? options.CellSize,
            FitOnOpen = options.FitOnOpen,
            Width = options.Width,
            Height = options.Height
        };

        var steps = options.Sequence?.Steps ?? [];

        // A visualizer made from steps alone starts from its first step's data.
        var model = kind.InitialModel(options) ?? steps.Select(s => s.Snapshot).FirstOrDefault(s => s != null);
        var previous = kind.ToState(model);
        spec.State = previous;

        if (steps.Count == 0)
        {
            var (highlight, pointers) = Extras(kind, null, model);
            spec.Highlight = highlight;
            spec.Pointers = pointers;
        }

        foreach (var step in steps)
        {
            // A step without a snapshot of its own shows the data as the step before left it.
            model = step.Snapshot ?? model;
            var state = kind.ToState(model);
            var stepSpec = new VisualizerStepSpec
            {
                Description = string.IsNullOrEmpty(step.Description) ? null : step.Description,
                Notes = step.AuxiliaryInfo.Count > 0 ? new Dictionary<string, string>(step.AuxiliaryInfo) : null,
                Watches = step.Watches.Count > 0 ? step.Watches.Select(w => new WatchSpec { Name = w.Name, Kind = w.Kind, Items = w.Items.ToList(), Count = w.Count }).ToList() : null,
                Line = step.SourceLine > 0 ? step.SourceLine : null,
                Source = step.SourceFile
            };

            var changes = kind.Diff(previous, state);
            if (changes == null) stepSpec.State = state;
            else if (changes.Cells != null || changes.Items != null || changes.Nodes != null || changes.Edges != null) stepSpec.Changes = changes;

            (stepSpec.Highlight, stepSpec.Pointers) = Extras(kind, step, model);
            spec.Steps.Add(stepSpec);
            previous = state;
        }

        return spec;
    }

    private static (List<ElementRef>? Highlight, List<PointerSpec>? Pointers) Extras(IVisualizerKind kind, VisualizerStep? step, object? model)
    {
        var highlight = new List<ElementRef>();
        var pointers = new List<PointerSpec>();
        if (model != null) kind.ReadExtras(step, model, highlight, pointers);
        return (highlight.Count > 0 ? highlight : null, pointers.Count > 0 ? pointers : null);
    }
}
