using Avalonia;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;
using NotebookTabViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookTabViewModel;

namespace CSharpEditorPlugin.Tests;

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

        // A plain list's values have no labels, only their places.
        Assert.Contains("Series,Index,Label,X,Y", csv);
        Assert.Contains("0,\"\",0,10", csv);
        Assert.Contains("1,\"\",1,20", csv);
        Assert.Contains("2,\"\",2,30", csv);
    }

    // In a locale that writes 1,5 the numbers split their field; a gap was written "NaN"; a quote in a name broke the row.
    [Fact]
    public void ChartExportService_ToCsv_IsReadableCsvInAnyLocale()
    {
        var opts = new ChartOptions
        {
            Series = { new ChartSeries { Name = "The \"best\" store", Points = { new ChartDataPoint(0, 1.5, "Jan"), new ChartDataPoint(1, double.NaN, "Feb") } } }
        };
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        string csv;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            csv = ChartExportService.ToCsv(opts);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }

        var rows = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("\"The \"\"best\"\" store\",0,\"Jan\",0,1.5", rows[1]);
        Assert.Equal("\"The \"\"best\"\" store\",1,\"Feb\",1,", rows[2]);
    }

    // A gap has nothing drawn, so the pointer never lands on it: it finds the nearest value that is there.
    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Scatter)]
    [InlineData(ChartType.Bar)]
    public void AHitTest_NeverLandsOnAGap(ChartType type)
    {
        var opts = new ChartOptions
        {
            Type = type,
            Series = { new ChartSeries { Points = { new ChartDataPoint(0, 1), new ChartDataPoint(1, double.NaN), new ChartDataPoint(2, 3) } } }
        };
        var renderer = ChartRendererFactory.GetRenderer(type);
        var bounds = new Rect(0, 0, 600, 300);

        // Everywhere over the plot.
        var hits = (from x in Enumerable.Range(0, 120)
                    from y in Enumerable.Range(0, 30)
                    select renderer.HitTest(new Point(x * 5, y * 10), bounds, opts)).Where(h => h != null).ToList();

        Assert.NotEmpty(hits); // the values either side of the gap are there to find
        Assert.All(hits, h => Assert.True(double.IsFinite(h!.Point.Y), $"{type} hit the gap"));
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
        var spec = captured.ChartSpec();
        Assert.Equal("Quadratic Growth", spec.Title);
        Assert.Equal("#4ec9b0", spec.Color);
        Assert.Equal(5, spec.Series[0].Y.Count);
    }

    [Fact]
    public void Display_ConvenienceMethods_ShouldSetCorrectChartType()
    {
        RichCellOutput? lineOut = null;
        using (InteractiveDisplayContext.EnterScope(o => lineOut = o))
        {
            Display.LineChart(new[] { 1, 2, 3 });
        }
        Assert.Equal(ChartType.Line, lineOut!.ChartSpec().Kind);

        RichCellOutput? barOut = null;
        using (InteractiveDisplayContext.EnterScope(o => barOut = o))
        {
            Display.BarChart(new[] { 1, 2, 3 });
        }
        Assert.Equal(ChartType.Bar, barOut!.ChartSpec().Kind);

        RichCellOutput? pieOut = null;
        using (InteractiveDisplayContext.EnterScope(o => pieOut = o))
        {
            Display.PieChart(new Dictionary<string, double> { { "A", 10 }, { "B", 20 } });
        }
        Assert.Equal(ChartType.Pie, pieOut!.ChartSpec().Kind);
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
        Assert.Equal("Chained Revenue", captured.ChartSpec().Title);
        Assert.Equal("#FF5722", captured.ChartSpec().Color);
    }

    [Fact]
    public void NotebookCellViewModel_Output_ChartTab_SelectionAndState()
    {
        var model = new NotebookCellItem();
        var cell = new NotebookCellViewModel(model);

        var opts = ChartDataParser.Parse(new[] { 5, 10, 15 }, title: "Growth Rate");
        cell.AddVisual(VisualOutput.Create(ChartOptionsConverter.ToSpec(opts)));

        Assert.True(cell.HasChartOutput);
        Assert.True((bool)cell.HasOutput);
        Assert.Equal(CellOutputTab.Chart, cell.SelectedOutputTab);
        Assert.True(cell.IsChartTabActive);
        Assert.True(cell.IsChartTabSelected);
        Assert.Equal("Growth Rate", cell.SingleOutputTitle);
        Assert.Equal("ChartLine", cell.SingleOutputIconKind);

        // Clear output should reset
        cell.ClearOutput();
        Assert.False(cell.HasChartOutput);
        Assert.Empty(cell.ChartVisuals);
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
        var spec = captured.ChartSpec();
        Assert.Equal("Quadratic Growth", spec.Title);
        Assert.Equal("#4ec9b0", spec.Color);
        Assert.Equal(10, spec.Series[0].Y.Count);
        Assert.Equal(100, captured.ChartModel().Series[0].MaxY);
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

        Assert.False((bool)cell1.HasError);
        Assert.False((bool)cell2.HasError);
        Assert.True(cell2.HasChartOutput);
        Assert.Equal("Multiplied", Assert.Single(cell2.ChartVisuals).Spec.Title);
    }

    [Fact]
    public void NotebookCellViewModel_MissingVariableHint_ShouldDetectCS0103()
    {
        var model = new NotebookCellItem();
        var cell = new NotebookCellViewModel(model);

        // Simulate CS0103 error
        cell.MissingVariableName = "squared";
        cell.HasMissingVariableError = true;

        Assert.True((bool)cell.HasMissingVariableError);
        Assert.Equal((string?)"squared", (string?)cell.MissingVariableName);
        Assert.Equal("Variable 'squared' not defined in active kernel", cell.MissingVariableHintTitle);
    }
}
