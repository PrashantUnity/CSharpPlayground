using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NotebookCellOutputTabTests
{
    [Fact]
    public void Cell_WithConsoleOnly_DefaultsToConsoleTab_HasMultipleOutputKindsFalse()
    {
        var item = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"Hello World\");",
            OutputText = "Hello World\n"
        };
        var vm = new NotebookCellViewModel(item);

        Assert.True(vm.HasOutput);
        Assert.True(vm.HasTextOutput);
        Assert.False(vm.HasTableOutput);
        Assert.Equal(1, vm.AvailableOutputCount);
        Assert.False(vm.HasMultipleOutputKinds);
        Assert.Equal(CellOutputTab.Console, vm.SelectedOutputTab);
        Assert.True(vm.IsConsoleTabActive);
        Assert.False(vm.IsTableTabActive);
        Assert.Equal("Console Output", vm.SingleOutputTitle);
        Assert.Equal("Console", vm.SingleOutputIconKind);
    }

    [Fact]
    public void Cell_WithTableOutputOnly_DefaultsToTableTab()
    {
        var item = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "numbers.Dump();"
        };
        var vm = new NotebookCellViewModel(item);

        var table = new DumpTableResult("Int32[5]");
        table.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = true });
        table.Rows.Add(new DumpTableRow(0, new List<DumpTableCell> { new() { DisplayText = "1" } }));
        table.Rows.Add(new DumpTableRow(1, new List<DumpTableCell> { new() { DisplayText = "4" } }));

        vm.SetTableOutput(table);

        Assert.True(vm.HasOutput);
        Assert.True(vm.HasTableOutput);
        Assert.False(vm.HasTextOutput);
        Assert.Equal(1, vm.AvailableOutputCount);
        Assert.False(vm.HasMultipleOutputKinds);
        Assert.Equal(CellOutputTab.Table, vm.SelectedOutputTab);
        Assert.True(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);
        Assert.Equal("Table Output", vm.SingleOutputTitle);
        Assert.Equal("Table", vm.SingleOutputIconKind);
        Assert.Equal("2", vm.TableRowCountText);
    }

    [Fact]
    public void Cell_WithDumpOutput_BothTableAndText_SelectsTableByDefault_HasMultipleOutputKindsTrue()
    {
        var item = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "squared.Dump(\"Squared Values\");"
        };
        var vm = new NotebookCellViewModel(item);

        // Simulate Dump execution: raw stdout text + structured table
        vm.OutputText = "=== Squared Values ===\n[List<Int32>]\n  [0] 144\n  [1] 2025\n  [2] 4624\n";

        var table = new DumpTableResult("Squared Values: Int32[7]");
        table.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = true });
        for (int i = 0; i < 7; i++)
        {
            table.Rows.Add(new DumpTableRow(i, new List<DumpTableCell> { new() { DisplayText = $"{i * i}" } }));
        }
        vm.SetTableOutput(table);

        Assert.True(vm.HasOutput);
        Assert.True(vm.HasTextOutput);
        Assert.True(vm.HasTableOutput);
        Assert.Equal(2, vm.AvailableOutputCount);
        Assert.True(vm.HasMultipleOutputKinds);

        // By default, Table is prioritized on .Dump()
        Assert.Equal(CellOutputTab.Table, vm.SelectedOutputTab);
        Assert.True(vm.IsTableTabSelected);
        Assert.False(vm.IsConsoleTabSelected);

        // One at a time: Table is active, Console is inactive
        Assert.True(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);

        Assert.Equal("7", vm.TableRowCountText);
        Assert.Equal("6", vm.ConsoleLineCountText); // 5 lines + trailing newline
    }

    [Fact]
    public void Cell_SelectOutputTab_SwitchesActiveRepresentation()
    {
        var item = new NotebookCellItem { Type = CellType.Code };
        var vm = new NotebookCellViewModel(item);

        vm.OutputText = "Output line 1\nOutput line 2\n";
        var table = new DumpTableResult("Data");
        table.Rows.Add(new DumpTableRow(0, new List<DumpTableCell> { new() { DisplayText = "A" } }));
        vm.SetTableOutput(table);

        Assert.Equal(CellOutputTab.Table, vm.SelectedOutputTab);
        Assert.True(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);

        // Switch to Console tab
        vm.SelectOutputTab(CellOutputTab.Console);

        Assert.Equal(CellOutputTab.Console, vm.SelectedOutputTab);
        Assert.False(vm.IsTableTabActive);
        Assert.True(vm.IsConsoleTabActive);
        Assert.True(vm.IsConsoleTabSelected);
        Assert.False(vm.IsTableTabSelected);

        // Switch back to Table tab via SetOutputTab command string
        vm.SetOutputTab("Table");

        Assert.Equal(CellOutputTab.Table, vm.SelectedOutputTab);
        Assert.True(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);
    }

    [Fact]
    public void Cell_SelectAllTab_MakesBothTableAndConsoleActiveSimultaneously()
    {
        var item = new NotebookCellItem { Type = CellType.Code };
        var vm = new NotebookCellViewModel(item);

        vm.OutputText = "Text representation\n";
        var table = new DumpTableResult("Table");
        table.Rows.Add(new DumpTableRow(0, new List<DumpTableCell> { new() { DisplayText = "Val" } }));
        vm.SetTableOutput(table);

        // Select All tab
        vm.SelectOutputTab(CellOutputTab.All);

        Assert.Equal(CellOutputTab.All, vm.SelectedOutputTab);
        Assert.True(vm.IsAllTabSelected);
        Assert.True(vm.IsTableTabActive);
        Assert.True(vm.IsConsoleTabActive);
    }

    [Fact]
    public void Cell_ClearOutput_ResetsOutputTabsAndState()
    {
        var item = new NotebookCellItem { Type = CellType.Code };
        var vm = new NotebookCellViewModel(item);

        vm.OutputText = "Log line\n";
        vm.SetTableOutput(new DumpTableResult("T"));

        Assert.True(vm.HasOutput);
        Assert.True(vm.HasMultipleOutputKinds);

        vm.ClearOutput();

        Assert.False(vm.HasOutput);
        Assert.False(vm.HasTextOutput);
        Assert.False(vm.HasTableOutput);
        Assert.Equal(0, vm.AvailableOutputCount);
        Assert.False(vm.HasMultipleOutputKinds);
        Assert.False(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);
    }

    [Fact]
    public void Cell_WithInspectorAndHtmlOutputs_SwitchesTabsAppropriately()
    {
        var item = new NotebookCellItem { Type = CellType.Code };
        var vm = new NotebookCellViewModel(item);

        var inspector = new ObjectInspectorNode("RootNode");
        vm.SetInspectorOutput(inspector);

        Assert.Equal(CellOutputTab.Inspector, vm.SelectedOutputTab);
        Assert.True(vm.IsInspectorTabActive);
        Assert.True(vm.IsInspectorTabSelected);

        // Set HTML output
        vm.SetHtmlContent("<h1>Formatted Report</h1>");

        Assert.Equal(CellOutputTab.Html, vm.SelectedOutputTab);
        Assert.True(vm.IsHtmlTabActive);
        Assert.False(vm.IsInspectorTabActive);
        Assert.True(vm.HasMultipleOutputKinds);
    }

    [Fact]
    public void Cell_RestoreFromModel_SelectsTableTabIfPresent()
    {
        var table = new DumpTableResult("Saved Table");
        table.Columns.Add(new DumpTableColumn { Header = "Col" });
        table.Rows.Add(new DumpTableRow(0, new List<DumpTableCell> { new() { DisplayText = "Val" } }));

        var item = new NotebookCellItem
        {
            Type = CellType.Code,
            OutputText = "Raw console output\n",
            TableSnapshot = table.ToSnapshot()
        };

        var vm = new NotebookCellViewModel(item);

        Assert.True(vm.HasTableOutput);
        Assert.True(vm.HasTextOutput);
        Assert.True(vm.HasMultipleOutputKinds);
        Assert.Equal(CellOutputTab.Table, vm.SelectedOutputTab);
        Assert.True(vm.IsTableTabActive);
        Assert.False(vm.IsConsoleTabActive);
    }
}
