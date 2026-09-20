using System;
using System.Collections.Generic;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class UniversalVisualizerTests
{
    [Fact]
    public void BarChartVisualizerData_ShouldPopulateFromIntValues()
    {
        var data = new BarChartVisualizerData(new[] { 10, 40, 90, 25, 75 })
        {
            MaxValue = 100
        };

        Assert.Equal(5, data.Items.Count);
        Assert.Equal(10, data.Items[0].Value);
        Assert.Equal("10", data.Items[0].DisplayValue);
        Assert.Equal(100, data.MaxValue);

        var clone = data.Clone();
        Assert.Equal(5, clone.Items.Count);
        clone.Items[0].Value = 999;
        Assert.Equal(10, data.Items[0].Value); // Deep clone test
    }

    [Fact]
    public void BarVisualizerRenderer_HitTest_ShouldLocateBar()
    {
        var data = new BarChartVisualizerData(new[] { 20, 40, 60, 80 });
        var renderer = new BarVisualizerRenderer();
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Bars,
            BarData = data
        };

        var bounds = new Rect(0, 0, 400, 200);

        // Point inside Bar 0 (x around 74..134, baselineY = 164)
        var result = renderer.HitTest(new Point(100, 150), bounds, options);
        Assert.NotNull(result);
        Assert.Contains("Bar", result.Title);
    }

    [Fact]
    public void BoardVisualizerData_ShouldConstructWithCheckerboardAndArrows()
    {
        var board = new BoardVisualizerData(4, 4, checkerboard: true)
        {
            Title = "4-Queens Problem"
        };

        board[0, 1].Value = "♛";
        board[0, 1].IsActive = true;
        board[1, 3].Value = "♛";
        board[2, 0].Value = "♛";
        board[3, 2].Value = "♛";

        // Add DP derivation arrow
        board[3, 2].Arrows.Add(new BoardCellArrow
        {
            TargetRow = 2,
            TargetCol = 0,
            Label = "Predecessor",
            ColorHex = "#38bdf8"
        });

        Assert.Equal(4, board.Rows);
        Assert.Equal(4, board.Columns);
        Assert.True(board.IsCheckerboard);
        Assert.Single(board[3, 2].Arrows);
        Assert.Equal(2, board[3, 2].Arrows[0].TargetRow);

        var clone = board.Clone();
        Assert.Equal("♛", clone[0, 1].Value);
        clone[0, 1].Value = " ";
        Assert.Equal("♛", board[0, 1].Value); // Deep clone test
    }

    [Fact]
    public void BoardVisualizerRenderer_HitTest_ShouldLocateCell()
    {
        var board = new BoardVisualizerData(3, 3);
        board[1, 1].Value = "42";
        board[1, 1].SubLabel = "dp";

        var renderer = new BoardVisualizerRenderer();
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Board,
            BoardData = board
        };

        var bounds = new Rect(0, 0, 300, 300);
        var hit = renderer.HitTest(new Point(150, 150), bounds, options);

        Assert.NotNull(hit);
        Assert.Equal(1, hit.Row);
        Assert.Equal(1, hit.Col);
        Assert.Contains("42", hit.Details);
    }

    [Fact]
    public void VisualizerScene_ShouldSupportDeclarativeShapesAndHitTest()
    {
        var scene = new VisualizerScene(500, 300);
        var r = scene.AddRect(50, 50, 100, 60, label: "Node A", fill: "#1e293b", stroke: "#3b82f6");
        var c = scene.AddCircle(250, 80, radius: 25, label: "State 1", fill: "#10b981");
        var arrow = scene.AddArrow(150, 80, 225, 80, label: "Transition");
        var text = scene.AddText(50, 140, "Pipeline Execution", fontSize: 14, isBold: true);

        Assert.Equal(4, scene.Shapes.Count);

        var clone = scene.Clone();
        Assert.Equal(4, clone.Shapes.Count);

        var renderer = new CanvasSceneRenderer();
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Canvas,
            SceneData = scene
        };

        var bounds = new Rect(0, 0, 500, 300);

        // Hit test inside rectangle
        var hitRect = renderer.HitTest(new Point(100, 80), bounds, options);
        Assert.NotNull(hitRect);
        Assert.Equal("Node A", hitRect.Title);

        // Hit test inside circle
        var hitCircle = renderer.HitTest(new Point(250, 80), bounds, options);
        Assert.NotNull(hitCircle);
        Assert.Equal("State 1", hitCircle.Title);
    }

    [Fact]
    public void VisualizerRecorder_CanvasStep_ShouldPreserveSceneHistory()
    {
        var recorder = VisualizerRecorder.CreateCanvas("Stack History", 400, 200);

        recorder.Step("Push A", scene =>
        {
            scene.AddRect(100, 100, 60, 30, "A");
        });

        recorder.Step("Push B", scene =>
        {
            scene.AddRect(100, 60, 60, 30, "B");
        });

        var seq = recorder.ToSequence();
        Assert.Equal(3, seq.TotalSteps); // 1 initial + 2 steps

        var snap0 = seq.Steps[0].Snapshot as VisualizerScene;
        var snap1 = seq.Steps[1].Snapshot as VisualizerScene;
        var snap2 = seq.Steps[2].Snapshot as VisualizerScene;

        Assert.NotNull(snap0);
        Assert.NotNull(snap1);
        Assert.NotNull(snap2);

        Assert.Empty(snap0.Shapes);
        Assert.Single(snap1.Shapes);
        Assert.Equal(2, snap2.Shapes.Count);
    }

    [Fact]
    public void Display_Bars_And_Board_And_Canvas_ShouldEmitRichOutputs()
    {
        RichCellOutput? emittedBars = null;
        using (InteractiveDisplayContext.EnterScope(o => emittedBars = o))
        {
            Display.Bars(new[] { 5, 10, 15 }, "Test Bars");
        }
        Assert.NotNull(emittedBars);
        Assert.Equal(CellOutputKind.Visualizer, emittedBars.Kind);
        Assert.Equal(VisualizerKind.Bars, emittedBars.VisualizerOptions?.Kind);

        RichCellOutput? emittedBoard = null;
        using (InteractiveDisplayContext.EnterScope(o => emittedBoard = o))
        {
            var board = new BoardVisualizerData(3, 3);
            Display.Board(board, "Test Board");
        }
        Assert.NotNull(emittedBoard);
        Assert.Equal(CellOutputKind.Visualizer, emittedBoard.Kind);
        Assert.Equal(VisualizerKind.Board, emittedBoard.VisualizerOptions?.Kind);

        RichCellOutput? emittedCanvas = null;
        using (InteractiveDisplayContext.EnterScope(o => emittedCanvas = o))
        {
            Display.Canvas(s => s.AddCircle(50, 50, 20), "Test Canvas");
        }
        Assert.NotNull(emittedCanvas);
        Assert.Equal(CellOutputKind.Visualizer, emittedCanvas.Kind);
        Assert.Equal(VisualizerKind.Canvas, emittedCanvas.VisualizerOptions?.Kind);
    }
}
