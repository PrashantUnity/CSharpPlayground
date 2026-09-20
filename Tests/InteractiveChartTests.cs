using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class InteractiveChartTests
{
    [Fact]
    public void ChartDataParser_ShouldParseListOfNumbers_WithQuadraticGrowth()
    {
        var squared = new List<int> { 1, 4, 9, 16, 25 };
        var opts = ChartDataParser.Parse(squared, title: "Quadratic Growth", color: "#4ec9b0");

        Assert.Equal("Quadratic Growth", opts.Title);
        Assert.Equal("#4ec9b0", opts.PrimaryColor);
        Assert.Single(opts.Series);

        var series = opts.Series[0];
        Assert.Equal(5, series.Points.Count);
        Assert.Equal(1, series.Points[0].Y);
        Assert.Equal(25, series.Points[4].Y);
        Assert.Equal(1, series.MinY);
        Assert.Equal(25, series.MaxY);
        Assert.Equal(11, series.AverageY);
    }

    [Fact]
    public void ChartDataParser_ShouldParseDictionary_KeyValues()
    {
        var data = new Dictionary<string, double>
        {
            { "Chrome", 65.5 },
            { "Safari", 18.2 },
            { "Edge", 5.3 },
            { "Firefox", 3.1 }
        };

        var opts = ChartDataParser.Parse(data, title: "Browser Market Share", chartType: "Pie");

        Assert.Equal(ChartType.Pie, opts.Type);
        Assert.Single(opts.Series);
        var series = opts.Series[0];
        Assert.Equal(4, series.Points.Count);
        Assert.Equal("Chrome", series.Points[0].Label);
        Assert.Equal(65.5, series.Points[0].Y);
        Assert.Equal("Firefox", series.Points[3].Label);
        Assert.Equal(3.1, series.Points[3].Y);
    }

    [Fact]
    public void ChartDataParser_ShouldParseXYTuples()
    {
        var points = new List<(double x, double y)>
        {
            (1.0, 10.0),
            (2.5, 25.0),
            (5.0, 50.0)
        };

        var opts = ChartDataParser.Parse(points, title: "Custom XY", chartType: "Scatter");

        Assert.Equal(ChartType.Scatter, opts.Type);
        Assert.Single(opts.Series);
        var series = opts.Series[0];
        Assert.Equal(3, series.Points.Count);
        Assert.Equal(1.0, series.Points[0].X);
        Assert.Equal(10.0, series.Points[0].Y);
        Assert.Equal(5.0, series.Points[2].X);
        Assert.Equal(50.0, series.Points[2].Y);
    }

    private class SampleSale
    {
        public string Region { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    [Fact]
    public void ChartDataParser_ShouldParseCustomObjectsWithProperties()
    {
        var sales = new List<SampleSale>
        {
            new() { Region = "North", Revenue = 150000m },
            new() { Region = "South", Revenue = 220000m },
            new() { Region = "West", Revenue = 180000m }
        };

        var opts = ChartDataParser.Parse(sales, title: "Regional Revenue", chartType: "Bar");

        Assert.Equal(ChartType.Bar, opts.Type);
        Assert.Single(opts.Series);
        var series = opts.Series[0];
        Assert.Equal(3, series.Points.Count);
        Assert.Equal("North", series.Points[0].Label);
        Assert.Equal(150000, series.Points[0].Y);
        Assert.Equal("South", series.Points[1].Label);
        Assert.Equal(220000, series.Points[1].Y);
    }

    [Fact]
    public void ChartDataParser_ShouldParseHistogram_FromContinuousValues()
    {
        var values = new[] { 1.2, 1.4, 1.9, 2.5, 2.8, 3.1, 4.0, 4.2, 4.3, 4.9 };
        var opts = ChartDataParser.ParseHistogram(values, binCount: 4, title: "Value Distribution");

        Assert.Equal(ChartType.Bar, opts.Type);
        Assert.Single(opts.Series);
        Assert.Equal(4, opts.Series[0].Points.Count);
        Assert.Equal(10, opts.Series[0].Points.Sum(p => p.Y));
    }

    [Fact]
    public void ChartRendererFactory_ShouldResolveCorrectRenderers()
    {
        var lineRenderer = ChartRendererFactory.GetRenderer(ChartType.Line);
        var barRenderer = ChartRendererFactory.GetRenderer(ChartType.Bar);
        var scatterRenderer = ChartRendererFactory.GetRenderer(ChartType.Scatter);
        var pieRenderer = ChartRendererFactory.GetRenderer(ChartType.Pie);

        Assert.IsType<LineChartRenderer>(lineRenderer);
        Assert.IsType<BarChartRenderer>(barRenderer);
        Assert.IsType<ScatterChartRenderer>(scatterRenderer);
        Assert.IsType<PieChartRenderer>(pieRenderer);
    }

    [Fact]
    public void HitTest_LineChartRenderer_ShouldFindClosestPoint()
    {
        var renderer = new LineChartRenderer();
        var opts = ChartDataParser.Parse(new[] { 10, 20, 30, 40, 50 });
        var bounds = new Rect(0, 0, 500, 300);

        // Point near right edge where Y = 50
        var hit = renderer.HitTest(new Point(450, 50), bounds, opts);
        Assert.NotNull(hit);
        Assert.Equal(50, hit.Point.Y);
    }

    [Fact]
    public void HitTest_PieChartRenderer_ShouldIdentifySliceByAngle()
    {
        var renderer = new PieChartRenderer();
        var opts = ChartDataParser.Parse(new Dictionary<string, double>
        {
            { "Top", 50 },
            { "Bottom", 50 }
        }, chartType: "Pie");

        var bounds = new Rect(0, 0, 400, 400);

        // Center is ~ (125, 200) due to legend on right.
        // Test within bounds should return non-null hit
        var hit = renderer.HitTest(new Point(125, 150), bounds, opts);
        Assert.NotNull(hit);
    }

    [Fact]
    public void ChartExportService_ToCsv_ShouldProduceValidCsvHeaderAndRows()
    {
        var opts = ChartDataParser.Parse(new[] { 10, 20, 30 }, title: "Test");
        var csv = ChartExportService.ToCsv(opts);

        Assert.Contains("Series,Index,Label,X,Y", csv);
        Assert.Contains("0,\"0\",0,10", csv);
        Assert.Contains("1,\"1\",1,20", csv);
        Assert.Contains("2,\"2\",2,30", csv);
    }

    [Fact]
    public void Display_Chart_ShouldEmitRichCellOutput_WithChartKind()
    {
        RichCellOutput? captured = null;
        using (InteractiveDisplayContext.EnterScope(outp => captured = outp))
        {
            var data = new[] { 1, 4, 9, 16, 25 };
            Display.Chart(data, title: "Quadratic Growth", color: "#4ec9b0");
        }

        Assert.NotNull(captured);
        Assert.Equal(CellOutputKind.Chart, captured.Kind);
        Assert.NotNull(captured.ChartOptions);
        Assert.Equal("Quadratic Growth", captured.ChartOptions.Title);
        Assert.Equal("#4ec9b0", captured.ChartOptions.PrimaryColor);
        Assert.Equal(5, captured.ChartOptions.Series[0].Points.Count);
    }

    [Fact]
    public void Display_ConvenienceMethods_ShouldSetCorrectChartType()
    {
        RichCellOutput? lineOut = null;
        using (InteractiveDisplayContext.EnterScope(o => lineOut = o))
        {
            Display.LineChart(new[] { 1, 2, 3 });
        }
        Assert.Equal(ChartType.Line, lineOut?.ChartOptions?.Type);

        RichCellOutput? barOut = null;
        using (InteractiveDisplayContext.EnterScope(o => barOut = o))
        {
            Display.BarChart(new[] { 1, 2, 3 });
        }
        Assert.Equal(ChartType.Bar, barOut?.ChartOptions?.Type);

        RichCellOutput? pieOut = null;
        using (InteractiveDisplayContext.EnterScope(o => pieOut = o))
        {
            Display.PieChart(new Dictionary<string, double> { { "A", 10 }, { "B", 20 } });
        }
        Assert.Equal(ChartType.Pie, pieOut?.ChartOptions?.Type);
    }

    [Fact]
    public void DisplayExtensions_Chart_ShouldReturnOriginalObject_AndEmit()
    {
        RichCellOutput? captured = null;
        var numbers = new[] { 100, 200, 300 };

        using (InteractiveDisplayContext.EnterScope(o => captured = o))
        {
            var returned = numbers.Chart(title: "Chained Revenue", color: "#FF5722");
            Assert.Same(numbers, returned);
        }

        Assert.NotNull(captured);
        Assert.Equal("Chained Revenue", captured.ChartOptions?.Title);
        Assert.Equal("#FF5722", captured.ChartOptions?.PrimaryColor);
    }

    [Fact]
    public void NotebookCellViewModel_Output_ChartTab_SelectionAndState()
    {
        var model = new NotebookCellItem();
        var cell = new NotebookCellViewModel(model);

        var opts = ChartDataParser.Parse(new[] { 5, 10, 15 }, title: "Growth Rate");
        cell.SetChartOutput(opts);

        Assert.True(cell.HasChartOutput);
        Assert.True(cell.HasOutput);
        Assert.Equal(CellOutputTab.Chart, cell.SelectedOutputTab);
        Assert.True(cell.IsChartTabActive);
        Assert.True(cell.IsChartTabSelected);
        Assert.Equal("Growth Rate", cell.SingleOutputTitle);
        Assert.Equal("ChartLine", cell.SingleOutputIconKind);

        // Clear output should reset
        cell.ClearOutput();
        Assert.False(cell.HasChartOutput);
        Assert.Null(cell.ChartOptions);
    }

    [Fact]
    public async Task NotebookExecutionKernel_WithDisplayChartScript_ShouldExecuteWithoutErrors()
    {
        var kernel = new NotebookExecutionKernel();
        RichCellOutput? captured = null;

        var userScript = @"
var squared = Enumerable.Range(1, 10).Select(x => x * x).ToList();
Display.Chart(squared, title: ""Quadratic Growth"", color: ""#4ec9b0"");
";

        var result = await kernel.ExecuteCellAsync(
            userScript,
            onRichOutput: outp => captured = outp);

        Assert.True(result.Success, $"Execution failed with error: {result.ErrorMessage}");
        Assert.Empty(result.ErrorMessage);
        Assert.NotNull(captured);
        Assert.Equal(CellOutputKind.Chart, captured.Kind);
        Assert.NotNull(captured.ChartOptions);
        Assert.Equal("Quadratic Growth", captured.ChartOptions.Title);
        Assert.Equal("#4ec9b0", captured.ChartOptions.PrimaryColor);
        Assert.Equal(10, captured.ChartOptions.Series[0].Points.Count);
        Assert.Equal(100, captured.ChartOptions.Series[0].MaxY);
    }

    [Fact]
    public async Task NotebookTabViewModel_RunCellsAboveAsync_ShouldExecuteSequentially()
    {
        var notebook = new NotebookDocumentItem();
        var tab = new NotebookTabViewModel(notebook);

        var cell1 = tab.CreateCellViewModel(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "var multiplier = 5;\nvar squared = Enumerable.Range(1, 5).Select(x => x * multiplier).ToList();"
        });

        var cell2 = tab.CreateCellViewModel(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Display.Chart(squared, title: \"Multiplied\");"
        });

        tab.Cells.Add(cell1);
        tab.Cells.Add(cell2);

        // Run cells above on cell 2 should execute cell 1, then cell 2
        await tab.RunCellsAboveAsync(cell2);

        Assert.False(cell1.HasError);
        Assert.False(cell2.HasError);
        Assert.True(cell2.HasChartOutput);
        Assert.Equal("Multiplied", cell2.ChartOptions?.Title);
    }

    [Fact]
    public void NotebookCellViewModel_MissingVariableHint_ShouldDetectCS0103()
    {
        var model = new NotebookCellItem();
        var cell = new NotebookCellViewModel(model);

        // Simulate CS0103 error
        cell.MissingVariableName = "squared";
        cell.HasMissingVariableError = true;

        Assert.True(cell.HasMissingVariableError);
        Assert.Equal("squared", cell.MissingVariableName);
        Assert.Equal("Variable 'squared' not defined in active kernel", cell.MissingVariableHintTitle);
    }
}
