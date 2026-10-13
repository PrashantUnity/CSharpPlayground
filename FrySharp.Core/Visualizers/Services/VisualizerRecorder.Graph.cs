using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Graph: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
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
