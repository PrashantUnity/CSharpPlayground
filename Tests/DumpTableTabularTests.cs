using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class DumpTableTabularTests
{
    [Fact]
    public void DataTable_ProducesStructuredDumpTableResult_WithCorrectTypesAndAlignment()
    {
        var dt = new DataTable("Employees");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("Salary", typeof(double));
        dt.Columns.Add("IsActive", typeof(bool));
        dt.Columns.Add("HireDate", typeof(DateTime));

        dt.Rows.Add(1, "Alice Smith", 85000.50, true, new DateTime(2023, 1, 15));
        dt.Rows.Add(2, "Bob Johnson", 92000.00, false, new DateTime(2022, 6, 1));
        dt.Rows.Add(3, DBNull.Value, 72500.25, true, DBNull.Value);

        var table = DumpTableBuilder.Create(dt, "Staff Roster");

        Assert.NotNull(table);
        Assert.Contains("DataTable", table.Title);
        Assert.Equal("Staff Roster", table.Label);
        Assert.Equal(5, table.Columns.Count);

        // Verify numeric alignment flags
        Assert.True(table.Columns[0].IsNumeric); // Id
        Assert.False(table.Columns[1].IsNumeric); // FullName
        Assert.True(table.Columns[2].IsNumeric); // Salary
        Assert.False(table.Columns[3].IsNumeric); // IsActive
        Assert.False(table.Columns[4].IsNumeric); // HireDate

        // Verify rows
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("1", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("Alice Smith", table.Rows[0].Cells[1].DisplayText);
        Assert.Equal("85000.5", table.Rows[0].Cells[2].DisplayText);
        Assert.Equal("True", table.Rows[0].Cells[3].DisplayText);

        // Verify DBNull cell handling
        Assert.True(table.Rows[2].Cells[1].IsNull);
        Assert.Equal("<null>", table.Rows[2].Cells[1].DisplayText);
    }

    [Fact]
    public void DataView_ProducesDumpTableResult_RespectingFilterAndSort()
    {
        var dt = new DataTable("Products");
        dt.Columns.Add("SKU", typeof(string));
        dt.Columns.Add("Price", typeof(decimal));

        dt.Rows.Add("A-100", 10.50m);
        dt.Rows.Add("B-200", 55.00m);
        dt.Rows.Add("C-300", 25.00m);

        var dv = new DataView(dt)
        {
            RowFilter = "Price > 20",
            Sort = "Price DESC"
        };

        var table = DumpTableBuilder.Create(dv, "Premium Products");

        Assert.NotNull(table);
        Assert.Contains("DataView", table.Title);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("B-200", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("C-300", table.Rows[1].Cells[0].DisplayText);
    }

    [Fact]
    public void MockDataFrame_ReflectiveExtraction_ExtractsColumnsAndRowsAccurately()
    {
        var mockDf = new MockDataFrame
        {
            Columns = new List<object>
            {
                new MockDataFrameColumn("Ticker", typeof(string), new object[] { "AAPL", "MSFT", "GOOGL" }),
                new MockDataFrameColumn("MarketCap_B", typeof(double), new object[] { 3450.5, 3120.8, 2250.0 }),
                new MockDataFrameColumn("Employees", typeof(int), new object[] { 161000, 221000, 190000 })
            },
            Rows = new MockDataFrameRows(3)
        };

        var isTabular = DumpTableBuilder.IsTabularObject(mockDf, out var table, "Stock Overview");

        Assert.True(isTabular);
        Assert.NotNull(table);
        Assert.Contains("DataFrame [3 rows × 3 cols]", table.Title);
        Assert.Equal("Stock Overview", table.Label);
        Assert.Equal(3, table.Columns.Count);

        Assert.Equal("Ticker", table.Columns[0].Header);
        Assert.False(table.Columns[0].IsNumeric);

        Assert.Equal("MarketCap_B", table.Columns[1].Header);
        Assert.True(table.Columns[1].IsNumeric);

        Assert.Equal("Employees", table.Columns[2].Header);
        Assert.True(table.Columns[2].IsNumeric);

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("AAPL", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("3450.5", table.Rows[0].Cells[1].DisplayText);
        Assert.Equal("161000", table.Rows[0].Cells[2].DisplayText);
    }

    [Fact]
    public void DisplayTable_EmitsRichCellOutput_WithTableResult()
    {
        var dt = new DataTable("Telemetry");
        dt.Columns.Add("Sensor", typeof(string));
        dt.Columns.Add("Reading", typeof(float));
        dt.Rows.Add("Temperature", 72.4f);

        RichCellOutput? emitted = null;
        using (InteractiveDisplayContext.EnterScope(output => emitted = output))
        {
            Display.Table(dt, "Engine Sensors");
        }

        Assert.NotNull(emitted);
        Assert.Equal(CellOutputKind.Table, emitted.Kind);
        Assert.NotNull(emitted.TableResult);
        Assert.Equal("Engine Sensors", emitted.TableResult.Label);
        Assert.Equal(2, emitted.TableResult.Columns.Count);
        Assert.Single(emitted.TableResult.Rows);
    }

    [Fact]
    public async Task NotebookKernel_CellReturningDataTable_AutoEmitsTableOutput()
    {
        var kernel = new NotebookExecutionKernel();

        string cellCode = @"
using System.Data;
var dt = new DataTable(""Inventory"");
dt.Columns.Add(""Item"", typeof(string));
dt.Columns.Add(""Qty"", typeof(int));
dt.Rows.Add(""Coffee"", 42);
dt.Rows.Add(""Tea"", 18);
dt";

        RichCellOutput? richOutput = null;
        var result = await kernel.ExecuteCellAsync(
            cellCode,
            onRichOutput: r => richOutput = r);

        Assert.True(result.Success);
        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.Table, richOutput.Kind);
        Assert.NotNull(richOutput.TableResult);
        Assert.Equal(2, richOutput.TableResult.Columns.Count);
        Assert.Equal(2, richOutput.TableResult.Rows.Count);
        Assert.Equal("Coffee", richOutput.TableResult.Rows[0].Cells[0].DisplayText);
        Assert.Equal("42", richOutput.TableResult.Rows[0].Cells[1].DisplayText);
    }

    [Fact]
    public async Task NotebookKernel_CellCallingDumpOnDataTable_EmitsTableOutput()
    {
        var kernel = new NotebookExecutionKernel();

        string cellCode = @"
using System.Data;
var dt = new DataTable(""LogEvents"");
dt.Columns.Add(""Level"", typeof(string));
dt.Columns.Add(""Message"", typeof(string));
dt.Rows.Add(""INFO"", ""Application started"");
dt.Dump(""Startup Events"");";

        RichCellOutput? richOutput = null;
        var result = await kernel.ExecuteCellAsync(
            cellCode,
            onRichOutput: r => richOutput = r);

        Assert.True(result.Success);
        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.Table, richOutput.Kind);
        Assert.NotNull(richOutput.TableResult);
        Assert.Equal("Startup Events", richOutput.TableResult.Label);
        Assert.Equal("INFO", richOutput.TableResult.Rows[0].Cells[0].DisplayText);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteMicrosoftDataAnalysis_RendersInteractiveTable()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "nuget_dataframe_analysis");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();

        RichCellOutput? richOutput = null;
        var result = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => richOutput = r);

        if (!result.Success)
        {
            throw new Exception($"DataFrame cell failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }

        Assert.NotNull(richOutput);
        Assert.Equal(CellOutputKind.Table, richOutput.Kind);
        Assert.NotNull(richOutput.TableResult);
        Assert.Contains("DataFrame", richOutput.TableResult.Title);
        Assert.Equal(4, richOutput.TableResult.Columns.Count);
        Assert.Equal(5, richOutput.TableResult.Rows.Count);
        Assert.Equal("Espresso Pro", richOutput.TableResult.Rows[0].Cells[0].DisplayText);
        Assert.Equal("Equipment", richOutput.TableResult.Rows[0].Cells[1].DisplayText);
        Assert.True(richOutput.TableResult.Columns[2].IsNumeric); // Unit Price
        Assert.True(richOutput.TableResult.Columns[3].IsNumeric); // Stock Qty
    }
}

// Mock classes that structurally mirror Microsoft.Data.Analysis.DataFrame for zero-dependency reflection tests
public class MockDataFrame
{
    public List<object> Columns { get; set; } = new();
    public MockDataFrameRows Rows { get; set; } = new(0);
}

public class MockDataFrameRows
{
    public long Count { get; }
    public MockDataFrameRows(long count) => Count = count;
}

public class MockDataFrameColumn
{
    public string Name { get; set; }
    public Type DataType { get; set; }
    public long Length => _values.Length;

    private readonly object[] _values;

    public MockDataFrameColumn(string name, Type dataType, object[] values)
    {
        Name = name;
        DataType = dataType;
        _values = values;
    }

    public object this[long index] => _values[index];
    public object this[int index] => _values[index];
}
