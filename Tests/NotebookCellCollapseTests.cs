using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NotebookCellCollapseTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;
    private readonly RoslynCompilerService _compiler;
    private readonly ScriptExecutionEngine _engine;

    public NotebookCellCollapseTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CollapseTests_" + Guid.NewGuid().ToString("N"));
        _testStorage = new LocalScriptStorageService(_testBaseDir);
        _compiler = new RoslynCompilerService();
        _engine = new ScriptExecutionEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    private CSharpNotebookStudioViewModel CreateStudio(NotebookDocumentItem? notebook = null)
    {
        var initialNotebook = notebook ?? new NotebookDocumentItem
        {
            Title = "Collapsible Studio Test",
            Cells =
            {
                new NotebookCellItem
                {
                    Id = "c1",
                    Type = CellType.Code,
                    Source = "var x = 42;\nConsole.WriteLine(x);",
                    OutputText = "42\n"
                },
                new NotebookCellItem
                {
                    Id = "c2",
                    Type = CellType.Markdown,
                    Source = "# Title\nSome markdown notes",
                }
            }
        };

        return new CSharpNotebookStudioViewModel(
            initialNotebook,
            _testStorage,
            _compiler,
            _engine,
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public void CellViewModel_Defaults_AreNotCollapsed()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"test\");"
        };
        var vm = new NotebookCellViewModel(cell);

        Assert.False(vm.IsInputCollapsed);
        Assert.False(vm.IsOutputCollapsed);
        Assert.False(vm.IsOutputScrolled);
        Assert.True(vm.IsInputVisible);
        Assert.False(vm.IsEntireCellCollapsed);
        Assert.Equal("Collapse Input", vm.ToggleInputCollapseText);
    }

    [Fact]
    public void CellViewModel_ToggleInputCollapse_TogglesInputVisibility()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "int a = 1;\nint b = 2;\nreturn a + b;"
        };
        var vm = new NotebookCellViewModel(cell);

        Assert.True(vm.IsInputVisible);
        Assert.False(vm.IsInputCollapsed);

        vm.ToggleInputCollapseCommand.Execute(null);

        Assert.True(vm.IsInputCollapsed);
        Assert.False(vm.IsInputVisible);
        Assert.Equal("Expand Input", vm.ToggleInputCollapseText);
        Assert.Equal("ChevronRight", vm.InputCollapseIcon);
        Assert.Contains("int a = 1;", vm.InputCollapsedSummaryText);
        Assert.Contains("3 lines", vm.InputCollapsedSummaryText);

        vm.ToggleInputCollapseCommand.Execute(null);

        Assert.False(vm.IsInputCollapsed);
        Assert.True(vm.IsInputVisible);
        Assert.Equal("Collapse Input", vm.ToggleInputCollapseText);
        Assert.Equal("ChevronDown", vm.InputCollapseIcon);
    }

    [Fact]
    public void CellViewModel_ToggleOutputCollapse_TogglesOutputVisibility()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"output\");",
            OutputText = "Output Line 1\nOutput Line 2\n"
        };
        var vm = new NotebookCellViewModel(cell);

        Assert.True(vm.HasOutput);
        Assert.False(vm.IsOutputCollapsed);
        Assert.True(vm.IsOutputVisible);
        Assert.False(vm.IsOutputCollapsedBarVisible);

        vm.ToggleOutputCollapseCommand.Execute(null);

        Assert.True(vm.IsOutputCollapsed);
        Assert.False(vm.IsOutputVisible);
        Assert.True(vm.IsOutputCollapsedBarVisible);
        Assert.Equal("Expand Output", vm.ToggleOutputCollapseText);
        Assert.Equal("ChevronRight", vm.OutputCollapseIcon);
        Assert.Contains("3 lines output", vm.OutputCollapsedSummaryText);

        vm.ToggleOutputCollapseCommand.Execute(null);

        Assert.False(vm.IsOutputCollapsed);
        Assert.True(vm.IsOutputVisible);
        Assert.False(vm.IsOutputCollapsedBarVisible);
        Assert.Equal("Collapse Output", vm.ToggleOutputCollapseText);
        Assert.Equal("ChevronDown", vm.OutputCollapseIcon);
    }

    [Fact]
    public void CellViewModel_ToggleOutputScrolled_TogglesScrolledMode()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "for(int i=0; i<100; i++) Console.WriteLine(i);"
        };
        var vm = new NotebookCellViewModel(cell);

        Assert.False(vm.IsOutputScrolled);
        Assert.Equal("Enable Scrolled Output", vm.ToggleOutputScrolledText);

        vm.ToggleOutputScrolledCommand.Execute(null);

        Assert.True(vm.IsOutputScrolled);
        Assert.Equal("Disable Scrolled Output", vm.ToggleOutputScrolledText);

        vm.ToggleOutputScrolledCommand.Execute(null);

        Assert.False(vm.IsOutputScrolled);
    }

    [Fact]
    public void CellViewModel_ToggleCellCollapse_FoldsBothInputAndOutput()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"both\");",
            OutputText = "Result"
        };
        var vm = new NotebookCellViewModel(cell);

        Assert.False(vm.IsEntireCellCollapsed);

        vm.ToggleCellCollapseCommand.Execute(null);

        Assert.True(vm.IsInputCollapsed);
        Assert.True(vm.IsOutputCollapsed);
        Assert.True(vm.IsEntireCellCollapsed);
        Assert.Equal("Expand Entire Cell", vm.ToggleCellCollapseText);

        vm.ToggleCellCollapseCommand.Execute(null);

        Assert.False(vm.IsInputCollapsed);
        Assert.False(vm.IsOutputCollapsed);
        Assert.False(vm.IsEntireCellCollapsed);
        Assert.Equal("Collapse Entire Cell", vm.ToggleCellCollapseText);
    }

    [Fact]
    public void CellViewModel_ClearOutput_ResetsOutputCollapsedState()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"clear test\");",
            OutputText = "Data"
        };
        var vm = new NotebookCellViewModel(cell);
        vm.CollapseOutput();

        Assert.True(vm.IsOutputCollapsed);

        vm.ClearOutput();

        Assert.False(vm.HasOutput);
        Assert.False(vm.IsOutputCollapsed);
        Assert.False(vm.IsOutputCollapsedBarVisible);
    }

    [Fact]
    public void TabViewModel_BulkCollapseAndExpand_AffectsAllCellsInTab()
    {
        var doc = new NotebookDocumentItem
        {
            Title = "Tab Bulk Test",
            Cells =
            {
                new NotebookCellItem
                {
                    Id = "c1",
                    Type = CellType.Code,
                    Source = "code 1",
                    OutputText = "out 1"
                },
                new NotebookCellItem
                {
                    Id = "c2",
                    Type = CellType.Code,
                    Source = "code 2",
                    OutputText = "out 2"
                },
                new NotebookCellItem
                {
                    Id = "c3",
                    Type = CellType.Markdown,
                    Source = "markdown 3"
                }
            }
        };

        var tab = new NotebookTabViewModel(doc);

        Assert.Equal(3, tab.Cells.Count);

        // 1. Collapse all inputs
        tab.CollapseAllInputsCommand.Execute(null);
        Assert.All(tab.Cells, c => Assert.True(c.IsInputCollapsed));

        // 2. Expand all inputs
        tab.ExpandAllInputsCommand.Execute(null);
        Assert.All(tab.Cells, c => Assert.False(c.IsInputCollapsed));

        // 3. Collapse all outputs
        tab.CollapseAllOutputsCommand.Execute(null);
        var cellsWithOutput = tab.Cells.Where(c => c.HasOutput).ToList();
        Assert.Equal(2, cellsWithOutput.Count);
        Assert.All(cellsWithOutput, c => Assert.True(c.IsOutputCollapsed));

        // 4. Expand all outputs
        tab.ExpandAllOutputsCommand.Execute(null);
        Assert.All(cellsWithOutput, c => Assert.False(c.IsOutputCollapsed));

        // 5. Collapse all cells
        tab.CollapseAllCellsCommand.Execute(null);
        Assert.All(tab.Cells, c => Assert.True(c.IsInputCollapsed));
        Assert.All(cellsWithOutput, c => Assert.True(c.IsOutputCollapsed));

        // 6. Expand all cells
        tab.ExpandAllCellsCommand.Execute(null);
        Assert.All(tab.Cells, c => Assert.False(c.IsInputCollapsed));
        Assert.All(cellsWithOutput, c => Assert.False(c.IsOutputCollapsed));
    }

    [Fact]
    public void StudioViewModel_BulkCommands_DelegateToActiveTab()
    {
        var studio = CreateStudio();
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal(2, studio.Cells.Count);

        // Bulk collapse inputs through studio
        studio.CollapseAllInputsCommand.Execute(null);
        Assert.All(studio.Cells, c => Assert.True(c.IsInputCollapsed));

        // Bulk expand inputs through studio
        studio.ExpandAllInputsCommand.Execute(null);
        Assert.All(studio.Cells, c => Assert.False(c.IsInputCollapsed));

        // Bulk collapse cells through studio
        studio.CollapseAllCellsCommand.Execute(null);
        Assert.All(studio.Cells, c => Assert.True(c.IsInputCollapsed));

        // Bulk expand cells through studio
        studio.ExpandAllCellsCommand.Execute(null);
        Assert.All(studio.Cells, c => Assert.False(c.IsInputCollapsed));
    }

    [Fact]
    public async Task StudioViewModel_DuplicateExplorerItem_PreservesCollapseFlags()
    {
        var studio = CreateStudio();
        Assert.NotNull(studio.ActiveTab);

        var firstCell = studio.Cells[0];
        firstCell.IsInputCollapsed = true;
        firstCell.IsOutputCollapsed = true;
        firstCell.IsOutputScrolled = true;

        var docFile = studio.ExplorerRootItems.First(x => x.Name == "Collapsible Studio Test.frynb");
        await studio.DuplicateExplorerItemAsync(docFile);

        Assert.Contains(studio.ExplorerRootItems, x => x.Name == "Collapsible Studio Test Copy.frynb");
        var duplicatedTab = studio.ActiveTab;
        Assert.NotNull(duplicatedTab);
        Assert.Equal("Collapsible Studio Test Copy.frynb", duplicatedTab.Title);

        var duplicatedFirstCell = duplicatedTab.Cells[0];
        Assert.True(duplicatedFirstCell.IsInputCollapsed);
        Assert.True(duplicatedFirstCell.IsOutputCollapsed);
        Assert.True(duplicatedFirstCell.IsOutputScrolled);
    }

    [Fact]
    public void Serialization_Roundtrip_PreservesCollapseState()
    {
        var originalCell = new NotebookCellItem
        {
            Id = "cell-42",
            Type = CellType.Code,
            Source = "Console.WriteLine(\"Persist test\");",
            IsInputCollapsed = true,
            IsOutputCollapsed = true,
            IsOutputScrolled = true,
            OutputText = "Result 42\n"
        };

        var originalDoc = new NotebookDocumentItem
        {
            Title = "Persistence Test",
            Cells = { originalCell }
        };

        var json = JsonSerializer.Serialize(originalDoc, new JsonSerializerOptions { WriteIndented = true });
        Assert.Contains("\"IsInputCollapsed\": true", json);
        Assert.Contains("\"IsOutputCollapsed\": true", json);
        Assert.Contains("\"IsOutputScrolled\": true", json);

        var deserializedDoc = JsonSerializer.Deserialize<NotebookDocumentItem>(json);
        Assert.NotNull(deserializedDoc);
        Assert.Single(deserializedDoc.Cells);

        var deserializedCell = deserializedDoc.Cells[0];
        Assert.Equal("cell-42", deserializedCell.Id);
        Assert.True(deserializedCell.IsInputCollapsed);
        Assert.True(deserializedCell.IsOutputCollapsed);
        Assert.True(deserializedCell.IsOutputScrolled);
    }

    [Fact]
    public void CellViewModel_CodeFoldingAndFormatting_ExecutesProperly()
    {
        var cell = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "class Test{void M(){int a=1;}}"
        };
        var vm = new NotebookCellViewModel(cell);

        bool foldFired = false;
        bool unfoldFired = false;
        bool formatFired = false;

        vm.RequestFoldAllCode += () => foldFired = true;
        vm.RequestUnfoldAllCode += () => unfoldFired = true;
        vm.RequestFormatCode += () => formatFired = true;

        vm.FoldAllCodeCommand.Execute(null);
        Assert.True(foldFired);

        vm.UnfoldAllCodeCommand.Execute(null);
        Assert.True(unfoldFired);

        vm.FormatCodeCommand.Execute(null);
        Assert.True(formatFired);
        Assert.Contains("class Test", vm.Source);
        Assert.Contains("void M()", vm.Source);
        Assert.Contains("int a = 1;", vm.Source);
    }

    [Fact]
    public void TabViewModel_CodeFoldingAndFormatting_ExecutesAcrossAllCodeCells()
    {
        var doc = new NotebookDocumentItem
        {
            Title = "Tab Code Folding Test",
            Cells =
            {
                new NotebookCellItem { Id = "c1", Type = CellType.Code, Source = "int a=1;" },
                new NotebookCellItem { Id = "c2", Type = CellType.Code, Source = "int b=2;" },
                new NotebookCellItem { Id = "c3", Type = CellType.Markdown, Source = "# Note\nUnchanged" }
            }
        };

        var tab = new NotebookTabViewModel(doc);

        int foldCount = 0;
        int unfoldCount = 0;

        foreach (var cell in tab.Cells.Where(c => c.IsCodeCell))
        {
            cell.RequestFoldAllCode += () => foldCount++;
            cell.RequestUnfoldAllCode += () => unfoldCount++;
        }

        tab.FoldAllCodeBlocksCommand.Execute(null);
        Assert.Equal(2, foldCount);

        tab.UnfoldAllCodeBlocksCommand.Execute(null);
        Assert.Equal(2, unfoldCount);

        tab.FormatAllCodeCellsCommand.Execute(null);
        Assert.Equal("int a = 1;", tab.Cells[0].Source.Trim());
        Assert.Equal("int b = 2;", tab.Cells[1].Source.Trim());
        Assert.Equal("# Note\nUnchanged", tab.Cells[2].Source);
    }

    [Fact]
    public void StudioViewModel_CodeFoldingAndFormatting_DelegatesToActiveTab()
    {
        var studio = CreateStudio();
        Assert.NotNull(studio.ActiveTab);

        int foldCount = 0;
        foreach (var cell in studio.Cells.Where(c => c.IsCodeCell))
        {
            cell.RequestFoldAllCode += () => foldCount++;
        }

        studio.FoldAllCodeBlocksCommand.Execute(null);
        Assert.Equal(1, foldCount);

        studio.FormatAllCodeCellsCommand.Execute(null);
        Assert.Contains("var x = 42;", studio.Cells[0].Source);
    }
}
