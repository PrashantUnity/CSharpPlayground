using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Canvas: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
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
