using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// The steps of a visualizer drawn from a spec, built only as they are looked at. A step's data is the latest whole state
/// at or before it with the later steps' changes applied. The store keeps a copy every so often (a keyframe, at most
/// <see cref="MaxKeyframes"/> of them) so seeking anywhere replays few changes, and the last steps it drew, so stepping
/// back and forth costs nothing. Memory stays flat however many steps there are.
/// </summary>
internal sealed class VisualizerStepStore
{
    public const int MaxKeyframes = 64;
    private const int MinKeyframeInterval = 32;
    private const int RecentCapacity = 8;

    private readonly VisualizerSpec _spec;
    private readonly IVisualizerKind _kind;
    private readonly object? _initial;
    private readonly int _interval;

    // Per step: the latest step at or before it that has a whole state, or -1 when that is the spec's own state.
    private readonly int[] _base;

    // The data after a step, before its highlights and pointers; never changed once kept.
    private readonly Dictionary<int, object?> _keyframes = new();
    private readonly LinkedList<(int Step, object? Snapshot)> _recent = new();
    private readonly Lock _gate = new();

    private VisualizerStepStore(VisualizerSpec spec, IVisualizerKind kind, object? initial, int count)
    {
        _spec = spec;
        _kind = kind;
        _initial = initial;
        _interval = Math.Max(MinKeyframeInterval, (count + MaxKeyframes - 1) / MaxKeyframes);
        _base = new int[count];
        for (var i = 0; i < count; i++)
        {
            _base[i] = spec.Steps[i].State != null ? i : i == 0 ? -1 : _base[i - 1];
        }
    }

    /// <summary>The steps, ready to play; each builds its data the first time it is drawn.</summary>
    public static VisualizerSequence CreateSequence(VisualizerSpec spec, IVisualizerKind kind, object? initial, ICollection<string> notices)
    {
        var count = Math.Min(spec.Steps.Count, VisualLimits.MaxSteps);
        if (spec.Steps.Count > count) notices.Add(Notices.ShowingFirst(count, spec.Steps.Count, "steps"));
        if (spec.Steps.Take(count).Any(s => s.State != null && VisualizerStateSize.IsOverLimit(s.State))) notices.Add("some steps show only part of their data");

        var store = new VisualizerStepStore(spec, kind, initial, count);
        var steps = new List<VisualizerStep>(count);
        for (var i = 0; i < count; i++)
        {
            var source = spec.Steps[i];
            var step = new VisualizerStep(i, source.Description ?? string.Empty, spec.Kind)
            {
                AuxiliaryInfo = source.Notes != null ? new Dictionary<string, string>(source.Notes) : [],
                Watches = source.Watches?.Select(w => new StepWatch(w.Name, w.Kind, w.Items, w.Count)).ToList() ?? [],
                SourceLine = source.Line ?? 0,
                SourceFile = source.Source
            };

            kind.DecorateStep(step, source.Highlight ?? [], source.Pointers ?? []);
            var index = i;
            step.SnapshotSource = () => store.SnapshotAt(index);
            steps.Add(step);
        }

        return new VisualizerSequence(steps);
    }

    /// <summary>A step's data with its highlights and pointers, as the renderer draws it.</summary>
    public object? SnapshotAt(int index)
    {
        lock (_gate)
        {
            for (var node = _recent.First; node != null; node = node.Next)
            {
                if (node.Value.Step != index) continue;
                _recent.Remove(node);
                _recent.AddFirst(node);
                return node.Value.Snapshot;
            }

            var step = _spec.Steps[index];
            var snapshot = _kind.DecorateModel(StateAt(index), step.Highlight ?? [], step.Pointers ?? [], onStep: true);
            _recent.AddFirst((index, snapshot));
            if (_recent.Count > RecentCapacity) _recent.RemoveLast();
            return snapshot;
        }
    }

    // The data after a step: from the nearest keyframe (or whole state) at or before it, with the changes since applied.
    private object? StateAt(int index)
    {
        var start = _base[index];
        var from = start;
        object? model = null;
        var found = false;
        for (var k = index; k > start; k--)
        {
            if (!_keyframes.TryGetValue(k, out model)) continue;
            from = k;
            found = true;
            break;
        }

        if (!found) model = start < 0 ? _initial : Whole(start);
        if (from == index) return model;

        var working = _kind.Clone(model);
        for (var j = from + 1; j <= index; j++)
        {
            if (_spec.Steps[j].Changes is { } changes) _kind.Apply(working, changes);
            if (j % _interval == 0 && !_keyframes.ContainsKey(j)) _keyframes[j] = j == index ? working : _kind.Clone(working);
        }

        return working;
    }

    // A step's own whole state; kept only on a keyframe step, so many whole states don't fill memory.
    private object? Whole(int step)
    {
        if (_keyframes.TryGetValue(step, out var model)) return model;
        model = _kind.Build(_spec.Steps[step].State!, _spec, new List<string>());
        if (step % _interval == 0) _keyframes[step] = model;
        return model;
    }
}

/// <summary>Whether a state is bigger than the studio draws, counted without building it.</summary>
internal static class VisualizerStateSize
{
    public static bool IsOverLimit(VisualizerState state) =>
        state.Grid is { } grid && (long)grid.Values.Count * (grid.Values.Count > 0 ? grid.Values[0].Count : 0) > VisualLimits.MaxGridCells ||
        state.Tree is { } tree && tree.Nodes.Count > VisualLimits.MaxTreeNodes ||
        state.Graph is { } graph && (graph.Nodes.Count > VisualLimits.MaxGraphNodes || graph.Edges.Count > VisualLimits.MaxGraphEdges) ||
        state.LinkedList is { } list && list.Nodes.Count > VisualLimits.MaxListNodes ||
        state.Array is { } array && array.Values.Count > VisualLimits.MaxArrayItems ||
        state.Bars is { } bars && bars.Values.Count > VisualLimits.MaxArrayItems ||
        state.Canvas is { } canvas && canvas.Shapes.Count > VisualLimits.MaxCanvasShapes;
}
