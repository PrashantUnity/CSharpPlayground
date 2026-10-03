using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class DumpTableViewInteractiveTests
{
    [Fact]
    public void FilterRows_FiltersMatchingText_UpdatesFilteredRowsAndSummary()
    {
        var table = new DumpTableResult("Employees");
        table.Columns.Add(new DumpTableColumn { Header = "Name", IsNumeric = false });
        table.Columns.Add(new DumpTableColumn { Header = "Dept", IsNumeric = false });

        table.Rows.Add(new DumpTableRow(0, new[]
        {
            new DumpTableCell { DisplayText = "Alice" },
            new DumpTableCell { DisplayText = "Engineering" }
        }));
        table.Rows.Add(new DumpTableRow(1, new[]
        {
            new DumpTableCell { DisplayText = "Bob" },
            new DumpTableCell { DisplayText = "Marketing" }
        }));
        table.Rows.Add(new DumpTableRow(2, new[]
        {
            new DumpTableCell { DisplayText = "Charlie" },
            new DumpTableCell { DisplayText = "Engineering" }
        }));

        Assert.Equal("(3 items)", table.SummaryText);

        // Filter by Engineering
        table.FilterText = "engineering";
        Assert.Equal(2, table.FilteredRows.Count);
        Assert.Equal("(2 of 3 rows)", table.SummaryText);
        Assert.Equal("Alice", table.FilteredRows[0].Cells[0].DisplayText);
        Assert.Equal("Charlie", table.FilteredRows[1].Cells[0].DisplayText);

        // Clear filter
        table.ClearFilter();
        Assert.False(table.IsFiltered);
        Assert.Equal(3, table.FilteredRows.Count);
        Assert.Equal("(3 items)", table.SummaryText);
    }

    [Fact]
    public void SortByColumn_SortsNumericallyWhenColumnIsNumeric()
    {
        var table = new DumpTableResult("Scores");
        table.Columns.Add(new DumpTableColumn { Header = "Score", IsNumeric = true });

        table.Rows.Add(new DumpTableRow(0, new[] { new DumpTableCell { DisplayText = "100", RawValue = 100L, IsNumeric = true } }));
        table.Rows.Add(new DumpTableRow(1, new[] { new DumpTableCell { DisplayText = "2", RawValue = 2L, IsNumeric = true } }));
        table.Rows.Add(new DumpTableRow(2, new[] { new DumpTableCell { DisplayText = "25", RawValue = 25L, IsNumeric = true } }));
        table.Rows.Add(new DumpTableRow(3, new[] { new DumpTableCell { DisplayText = "3", RawValue = 3L, IsNumeric = true } }));

        // 1. Sort Ascending
        table.SortByColumn(0);
        Assert.Equal(0, table.SortColumnIndex);
        Assert.False(table.IsSortDescending);
        Assert.Equal("2", table.FilteredRows[0].Cells[0].DisplayText);
        Assert.Equal("3", table.FilteredRows[1].Cells[0].DisplayText);
        Assert.Equal("25", table.FilteredRows[2].Cells[0].DisplayText);
        Assert.Equal("100", table.FilteredRows[3].Cells[0].DisplayText);

        // 2. Sort Descending
        table.SortByColumn(0);
        Assert.True(table.IsSortDescending);
        Assert.Equal("100", table.FilteredRows[0].Cells[0].DisplayText);
        Assert.Equal("25", table.FilteredRows[1].Cells[0].DisplayText);
        Assert.Equal("3", table.FilteredRows[2].Cells[0].DisplayText);
        Assert.Equal("2", table.FilteredRows[3].Cells[0].DisplayText);

        // 3. Clear Sort
        table.SortByColumn(0);
        Assert.False(table.IsSorted);
        Assert.Null(table.SortColumnIndex);
    }

    [Fact]
    public void ToMarkdown_FormatsValidGfmTable()
    {
        var table = new DumpTableResult("Users");
        table.Columns.Add(new DumpTableColumn { Header = "Id", IsNumeric = true });
        table.Columns.Add(new DumpTableColumn { Header = "Username", IsNumeric = false });

        table.Rows.Add(new DumpTableRow(0, new[]
        {
            new DumpTableCell { DisplayText = "1", IsNumeric = true },
            new DumpTableCell { DisplayText = "Alice" }
        }));

        var md = table.ToMarkdown();

        Assert.Contains("| Id | Username |", md);
        Assert.Contains("| ---: | --- |", md);
        Assert.Contains("| 1 | Alice |", md);
    }

    [Fact]
    public void NestedTable_TogglesExpansionAndRowHasNestedTableProperty()
    {
        var childTable = new DumpTableResult("SubItems");
        childTable.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = false });
        childTable.Rows.Add(new DumpTableRow(0, new[] { new DumpTableCell { DisplayText = "SubA" } }));

        var parentCell = new DumpTableCell
        {
            DisplayText = "[1 items]",
            NestedTable = childTable
        };

        var row = new DumpTableRow(0, new[] { parentCell });

        Assert.True(parentCell.HasNestedTable);
        Assert.True(row.HasNestedTable);
        Assert.False(row.IsExpanded);

        // Expand
        parentCell.IsNestedExpanded = true;
        Assert.True(row.IsExpanded);

        // Collapse
        parentCell.IsNestedExpanded = false;
        Assert.False(row.IsExpanded);
    }
}
