using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Charts, 3D plots and visualizers as outputs: drawn once, never built as controls on the script's thread, kept when a
/// notebook is saved, and fed by data the way the user wrote it.
/// </summary>
public class VisualOutputRegressionTests
{
    // A control made on the script's worker thread is half-built (its XAML fails to load there, silently); the Results
    // deck crashed attaching one ("AttachedToLogicalTreeCore called for 'InteractiveChartControl' but control has no
    // logical parent"). A visual output is data; the view that shows it builds the control on the UI thread.
    [Fact]
    public async Task DisplayChart_OnTheScriptThread_EmitsDataAndNoControl()
    {
        var outputs = await Emitted(() => Display.Chart(new[] { 1, 4, 9 }, title: "Squares"));

        var chart = Assert.Single(outputs);
        Assert.Equal(CellOutputKind.Chart, chart.Kind);
        Assert.Null(chart.InteractiveControl);
    }

    [Fact]
    public async Task DisplayVisualizer_OnTheScriptThread_EmitsDataAndNoControl()
    {
        var outputs = await Emitted(() => Display.Matrix(new[,] { { 1, 0 }, { 0, 1 } }, title: "Grid"));

        var visualizer = Assert.Single(outputs);
        Assert.Equal(CellOutputKind.Visualizer, visualizer.Kind);
        Assert.Null(visualizer.InteractiveControl);
    }

    [Theory]
    [InlineData("Display.Chart(new[] { 1, 4, 9 });")]
    [InlineData("Display.Matrix(new[,] { { 1, 0 }, { 0, 1 } });")]
    public async Task ANotebookVisual_IsOneOutput_NotAlsoAWidget(string code)
    {
        var (_, cell) = await RunNotebookCell(code);

        Assert.False(cell.HasError, cell.OutputText);
        Assert.False(cell.HasInteractiveControl);
        Assert.Equal(1, cell.AvailableOutputCount);
    }

    // A cell's last expression is shown too: a visual the cell already displayed must not be shown again, or as an
    // object inspector.
    [Theory]
    [InlineData("Display.Chart(new[] { 1, 4, 9 })", CellOutputKind.Chart)]
    [InlineData("Display.Scatter3D(new[] { new Point3D(1, 2, 3), new Point3D(4, 5, 6) })", CellOutputKind.Plot3D)]
    [InlineData("Display.Matrix(new[,] { { 1, 0 }, { 0, 1 } })", CellOutputKind.Visualizer)]
    public async Task AVisualAsTheLastExpression_IsShownOnce(string code, CellOutputKind kind)
    {
        var outputs = new List<RichCellOutput>();
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(code, onRichOutput: outputs.Add);

        Assert.True(result.Success, result.ErrorMessage);
        var only = Assert.Single(outputs);
        Assert.Equal(kind, only.Kind);
    }

    [Fact]
    public async Task AReturnedChartModel_IsShownAsAChart_WithoutAControl()
    {
        var outputs = new List<RichCellOutput>();
        var result = await new NotebookExecutionKernel().ExecuteCellAsync(
            "ChartDataParser.Parse(new[] { 1, 2, 3 }, title: \"Model\")", onRichOutput: outputs.Add);

        Assert.True(result.Success, result.ErrorMessage);
        var chart = Assert.Single(outputs);
        Assert.Equal(CellOutputKind.Chart, chart.Kind);
        Assert.Null(chart.InteractiveControl);
    }

    [Fact]
    public void AReopenedNotebook_StillShowsItsChart()
    {
        var cell = new NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = "Display.Chart(...)" });
        cell.AddVisual(VisualOutput.Create(ChartOptionsConverter.ToSpec(ChartDataParser.Parse(new[] { 1, 2, 3 }, title: "Saved chart"))));

        var reopened = new NotebookCellViewModel(cell.Model);

        Assert.True(reopened.HasChartOutput);
        Assert.Equal("Saved chart", Assert.Single(reopened.ChartVisuals).Spec.Title);
    }

    [Fact]
    public void Points3D_FromIntegerTuples_AreAllPlotted()
    {
        var options = Plot3DDataParser.Parse(new List<(int, int, int)> { (1, 2, 3), (4, 5, 6) });

        var points = Assert.Single(options.Series).Points;
        Assert.Equal(2, points.Count);
        Assert.Equal((4.0, 5.0, 6.0), (points[1].X, points[1].Y, points[1].Z));
    }

    [Fact]
    public void Points3D_WithACoordinateThatIsNotANumber_AreSkipped_NotACrash()
    {
        var rows = new object[] { new { X = 1.0, Y = 2.0, Z = 3.0 }, new { X = "left", Y = 2.0, Z = 3.0 } };

        var error = Record.Exception(() => Plot3DDataParser.Parse(rows));

        Assert.Null(error);
    }

    // A missing value is a gap in the line, not a point at zero.
    [Fact]
    public void AChartValueThatIsNotANumber_IsAGap_NotZero()
    {
        var options = ChartDataParser.Parse(new[] { 1.0, double.NaN, 3.0, double.PositiveInfinity });

        var points = options.Series[0].Points;
        Assert.Equal(4, points.Count);
        Assert.True(double.IsNaN(points[1].Y), $"the NaN became {points[1].Y}");
        Assert.True(double.IsNaN(points[3].Y), $"infinity became {points[3].Y}");
    }

    private static async Task<List<RichCellOutput>> Emitted(Action display)
    {
        var outputs = new List<RichCellOutput>();
        await Task.Run(() =>
        {
            using var scope = InteractiveDisplayContext.EnterScope(output =>
            {
                lock (outputs) outputs.Add(output);
            });
            display();
        });
        return outputs;
    }

    private static async Task<(NotebookTabViewModel Tab, NotebookCellViewModel Cell)> RunNotebookCell(string code)
    {
        var tab = new NotebookTabViewModel(new NotebookDocumentItem());
        var cell = tab.CreateCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = code });
        tab.Cells.Add(cell);
        await tab.RunSingleCellAsync(cell);
        return (tab, cell);
    }
}
