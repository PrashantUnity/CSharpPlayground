using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class NestedTableDetectorTests
{
    [Fact]
    public void TryDetectNestedTable_JsonArrayOfObjects_CreatesStructuredChildTable()
    {
        var json = """
            [
                {"id": 101, "name": "Alpha", "score": 95.5},
                {"id": 102, "name": "Beta", "score": 88.0}
            ]
            """;

        bool detected = NestedTableDetector.TryDetectNestedTable(json, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Equal("JSON Table [2 rows]", table!.Title);
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal("id", table.Columns[0].Header);
        Assert.Equal("name", table.Columns[1].Header);
        Assert.Equal("score", table.Columns[2].Header);

        Assert.True(table.Columns[0].IsNumeric);
        Assert.False(table.Columns[1].IsNumeric);
        Assert.True(table.Columns[2].IsNumeric);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("101", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("Alpha", table.Rows[0].Cells[1].DisplayText);
        Assert.Equal("95.5", table.Rows[0].Cells[2].DisplayText);
    }

    [Fact]
    public void TryDetectNestedTable_JsonArrayOfPrimitives_CreatesSingleColumnTable()
    {
        var json = """["red", "green", "blue"]""";

        bool detected = NestedTableDetector.TryDetectNestedTable(json, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Single(table!.Columns);
        Assert.Equal("Item", table.Columns[0].Header);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("red", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("green", table.Rows[1].Cells[0].DisplayText);
        Assert.Equal("blue", table.Rows[2].Cells[0].DisplayText);
    }

    [Fact]
    public void TryDetectNestedTable_JsonObject_CreatesKeyValueTable()
    {
        var json = """
            {
                "host": "localhost",
                "port": 5432,
                "ssl": true
            }
            """;

        bool detected = NestedTableDetector.TryDetectNestedTable(json, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Equal("JSON Object (3 properties)", table!.Title);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("Property", table.Columns[0].Header);
        Assert.Equal("Value", table.Columns[1].Header);

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("host", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("localhost", table.Rows[0].Cells[1].DisplayText);
        Assert.Equal("port", table.Rows[1].Cells[0].DisplayText);
        Assert.Equal("5432", table.Rows[1].Cells[1].DisplayText);
        Assert.Equal("ssl", table.Rows[2].Cells[0].DisplayText);
        Assert.Equal("true", table.Rows[2].Cells[1].DisplayText);
    }

    [Fact]
    public void TryDetectNestedTable_ComplexCollection_CreatesChildTable()
    {
        var orders = new List<OrderItemTest>
        {
            new OrderItemTest { ItemId = 1, Description = "Keyboard", Price = 49.99 },
            new OrderItemTest { ItemId = 2, Description = "Mouse", Price = 25.50 }
        };

        bool detected = NestedTableDetector.TryDetectNestedTable(orders, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Equal(3, table!.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Keyboard", table.Rows[0].Cells[1].DisplayText);
    }

    [Fact]
    public void TryDetectNestedTable_EmbeddedAsciiTable_CreatesChildTable()
    {
        var asciiTable = """
            +----+-------+
            | id | tag   |
            +----+-------+
            | 1  | v1.0  |
            | 2  | v2.0  |
            +----+-------+
            """;

        bool detected = NestedTableDetector.TryDetectNestedTable(asciiTable, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Equal(2, table!.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("v1.0", table.Rows[0].Cells[1].DisplayText);
    }

    [Fact]
    public void TryDetectNestedTable_DeeplyNested_SafelyStopsAtMaxDepth()
    {
        var deepJson = """
            [
                {
                    "group": "A",
                    "sub": [
                        {
                            "subgroup": "A1",
                            "items": [{"leaf": 1}]
                        }
                    ]
                }
            ]
            """;

        bool detected = NestedTableDetector.TryDetectNestedTable(deepJson, out var table);

        Assert.True(detected);
        Assert.NotNull(table);
        Assert.Single(table!.Rows);

        var subCell = table.Rows[0].Cells[1];
        Assert.True(subCell.HasNestedTable);
        Assert.NotNull(subCell.NestedTable);
    }

    [Fact]
    public void TryDetectNestedTable_MultiLevelHierarchy_CreatesNestedTablesAtEachLevel()
    {
        var hierarchy = new[]
        {
            new
            {
                Department = "Engineering",
                Budget = 2500000.0,
                Teams = new[]
                {
                    new
                    {
                        TeamName = "Compiler",
                        Members = new[]
                        {
                            new { Name = "Alice", Role = "Staff Engineer" },
                            new { Name = "Bob", Role = "Principal Engineer" }
                        }
                    }
                }
            }
        };

        var rootTable = DumpTableBuilder.Create(hierarchy, "Company Org");
        Assert.NotNull(rootTable);
        Assert.Single(rootTable.Rows);

        // Level 1 -> Level 2
        var teamsCell = rootTable.Rows[0].Cells.FirstOrDefault(c => c.HasNestedTable);
        Assert.NotNull(teamsCell);
        Assert.True(teamsCell.HasNestedTable);
        var teamsTable = teamsCell.NestedTable;
        Assert.NotNull(teamsTable);
        Assert.Single(teamsTable.Rows);

        // Level 2 -> Level 3
        var membersCell = teamsTable.Rows[0].Cells.FirstOrDefault(c => c.HasNestedTable);
        Assert.NotNull(membersCell);
        Assert.True(membersCell.HasNestedTable);
        var membersTable = membersCell.NestedTable;
        Assert.NotNull(membersTable);
        Assert.Equal(2, membersTable.Rows.Count);
        Assert.Equal("Alice", membersTable.Rows[0].Cells[0].DisplayText);
        Assert.Equal("Staff Engineer", membersTable.Rows[0].Cells[1].DisplayText);
    }

    private class OrderItemTest
    {
        public int ItemId { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
    }
}
