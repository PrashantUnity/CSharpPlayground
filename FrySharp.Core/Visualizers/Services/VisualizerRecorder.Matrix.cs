using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Matrix: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
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
