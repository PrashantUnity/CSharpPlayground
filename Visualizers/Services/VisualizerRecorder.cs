using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public partial class VisualizerRecorder : IVisualSource
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

    /// <summary>
    /// Records an array, list or string as a row of cells. Steps re-read a live array or list, so writes made by the
    /// algorithm (swaps, DP updates) show up and are outlined as changes.
    /// </summary>
    public static VisualizerRecorder CreateArray(
        System.Collections.IEnumerable values,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "") =>
        CreateArray(ArrayPointerDataParser.Parse(values), title, sourceLine, sourceFile);

    public VisualizerRecorder Step(
        string description,
        (int Row, int Col)? activeCell = null,
        IEnumerable<(int Row, int Col)>? activeCells = null,
        string? activeNodeId = null,
        IEnumerable<string>? activeNodeIds = null,
        object? pointers = null,
        IDictionary<string, string>? auxiliaryInfo = null,
        IEnumerable<int>? highlight = null,
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

        // Array cells to light up, e.g. the current window: highlight: Enumerable.Range(left, right - left + 1)
        if (highlight != null)
        {
            foreach (int index in highlight) step.ActiveCells.Add((0, index));
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
            Options.BarData.RefreshFromSource();
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

    /// <summary>The visualizer as a spec, as every language describes one.</summary>
    public VisualSpec ToVisualSpec() => VisualizerOptionsConverter.ToSpec(Options);

    /// <summary>Exports the recorded visualizer steps into an animated GIF file.</summary>
    public Task SaveGifAsync(string path, VisualizerGifExportOptions? exportOptions = null, IProgress<double>? progress = null, System.Threading.CancellationToken cancellationToken = default) =>
        VisualizerGifExportService.ExportToFileAsync(Options, path, exportOptions, progress, cancellationToken);

    /// <summary>Exports the recorded visualizer steps into an animated GIF file synchronously.</summary>
    public void SaveGif(string path, VisualizerGifExportOptions? exportOptions = null) =>
        SaveGifAsync(path, exportOptions).GetAwaiter().GetResult();

    /// <summary>Exports the recorded visualizer steps into animated GIF bytes.</summary>
    public Task<byte[]> ToGifBytesAsync(VisualizerGifExportOptions? exportOptions = null, IProgress<double>? progress = null, System.Threading.CancellationToken cancellationToken = default) =>
        VisualizerGifExportService.ExportToGifBytesAsync(Options, exportOptions, progress, cancellationToken);
}
