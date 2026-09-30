using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

// Board: the recorder's factory and step for it, and its typed recorder.
public partial class VisualizerRecorder
{
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
