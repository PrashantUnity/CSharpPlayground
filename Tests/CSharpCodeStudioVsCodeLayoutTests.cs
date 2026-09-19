using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpCodeStudioVsCodeLayoutTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public CSharpCodeStudioVsCodeLayoutTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioVsCodeTests_" + Guid.NewGuid().ToString("N"));
        _testStorage = new LocalScriptStorageService(_testBaseDir);
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

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem? script = null)
    {
        var doc = script ?? new ScriptDocumentItem
        {
            Title = "VS Code Layout Test Script",
            Code = "int x = 42;\nstring msg = \"Hello VS Code\";\nint y = x + 10;\nConsole.WriteLine(msg);",
            Notes = "# Plan\nVS Code layout validation."
        };

        return new CSharpCodeStudioViewModel(
            doc,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public void InitialLayout_DefaultsToExplorerAndVisibleSidebar()
    {
        var studio = CreateStudio();

        Assert.Equal(0, studio.SelectedActivityBarIndex);
        Assert.True(studio.IsSideBarVisible);
        Assert.Equal("EXPLORER", studio.SideBarTitle);
    }

    [Theory]
    [InlineData(0, "EXPLORER")]
    [InlineData(1, "SEARCH")]
    [InlineData(2, "RUN AND DEBUG")]
    [InlineData(3, "DEPENDENCIES & NUGET")]
    [InlineData(4, "SCRATCHPAD & NOTES")]
    [InlineData(5, "PROBLEMS")]
    public void SelectActivityBarItem_SwitchesTitleAndEnsuresVisible(int index, string expectedTitle)
    {
        var studio = CreateStudio();
        studio.IsSideBarVisible = false;

        studio.SelectActivityBarItem(index);

        Assert.Equal(index, studio.SelectedActivityBarIndex);
        Assert.True(studio.IsSideBarVisible);
        Assert.Equal(expectedTitle, studio.SideBarTitle);
    }

    [Fact]
    public void SelectActivityBarItem_WhenAlreadyActive_TogglesVisibility()
    {
        var studio = CreateStudio();
        Assert.True(studio.IsSideBarVisible);
        Assert.Equal(0, studio.SelectedActivityBarIndex);

        // Clicking the already active item toggles the sidebar closed
        studio.SelectActivityBarItem(0);
        Assert.False(studio.IsSideBarVisible);

        // Clicking it again re-opens the sidebar
        studio.SelectActivityBarItem(0);
        Assert.True(studio.IsSideBarVisible);
    }

    [Fact]
    public void ToggleSideBarCommand_TogglesSideBarVisibility()
    {
        var studio = CreateStudio();
        Assert.True(studio.IsSideBarVisible);

        studio.ToggleSideBarCommand.Execute(null);
        Assert.False(studio.IsSideBarVisible);

        studio.ToggleSideBarCommand.Execute(null);
        Assert.True(studio.IsSideBarVisible);
    }

    [Fact]
    public void BackwardsCompatibility_SelectedLeftTabIndex_RoutesToCorrectActivityBarOrDeck()
    {
        var studio = CreateStudio();

        // LeftTab 0 -> Scratchpad (4)
        studio.SelectedLeftTabIndex = 0;
        Assert.Equal(4, studio.SelectedActivityBarIndex);
        Assert.True(studio.IsSideBarVisible);

        // LeftTab 1 -> References / Dependencies (3)
        studio.SelectedLeftTabIndex = 1;
        Assert.Equal(3, studio.SelectedActivityBarIndex);
        Assert.True(studio.IsSideBarVisible);

        // LeftTab 2 -> Testcases in bottom deck (3)
        studio.SelectedLeftTabIndex = 2;
        Assert.Equal(3, studio.SelectedBottomTabIndex);
        Assert.True(studio.IsBottomDeckExpanded);
    }

    [Fact]
    public void Search_FindsMatchesInCode_WithCorrectLineAndColumn()
    {
        var studio = CreateStudio();
        studio.Code = "var a = 10;\nvar b = 20;\nvar c = a + b;";

        studio.SearchQuery = "var";

        Assert.Equal(3, studio.SearchMatches.Count);
        Assert.Equal(1, studio.SearchMatches[0].LineNumber);
        Assert.Equal(1, studio.SearchMatches[0].Column);
        Assert.Equal(2, studio.SearchMatches[1].LineNumber);
        Assert.Equal(3, studio.SearchMatches[2].LineNumber);
        Assert.Equal("3 results", studio.SearchStatusText);
    }

    [Fact]
    public void ReplaceNext_ReplacesFirstOccurrenceInCode()
    {
        var studio = CreateStudio();
        studio.Code = "int first = 1;\nint second = 2;";

        studio.SearchQuery = "int";
        studio.ReplaceQuery = "long";

        studio.ReplaceNextCommand.Execute(null);

        Assert.Contains("long first = 1;", studio.Code);
        Assert.Contains("int second = 2;", studio.Code);
    }

    [Fact]
    public void ReplaceAll_ReplacesAllOccurrencesInCode()
    {
        var studio = CreateStudio();
        studio.Code = "foo(1); foo(2); foo(3);";

        studio.SearchQuery = "foo";
        studio.ReplaceQuery = "bar";

        studio.ReplaceAllCommand.Execute(null);

        Assert.Equal("bar(1); bar(2); bar(3);", studio.Code);
    }

    [Fact]
    public void CodeTemplates_AreLoadedAndCanBeFiltered()
    {
        var studio = CreateStudio();

        Assert.NotEmpty(studio.FilteredTemplates);

        studio.TemplateFilterQuery = "Two Sum";
        Assert.Contains(studio.FilteredTemplates, t => t.Title.Contains("Two Sum", StringComparison.OrdinalIgnoreCase));

        studio.TemplateFilterQuery = "NonExistentTemplateXYZ";
        Assert.Empty(studio.FilteredTemplates);

        studio.TemplateFilterQuery = string.Empty;
        Assert.NotEmpty(studio.FilteredTemplates);
    }

    [Fact]
    public void InsertTemplate_AppendsToCode()
    {
        var studio = CreateStudio();
        studio.Code = "// Existing Code";

        var template = CodeTemplateLibrary.GetTemplates().First();
        studio.InsertTemplateCommand.Execute(template);

        Assert.Contains("// Existing Code", studio.Code);
        Assert.Contains(template.InitialCode, studio.Code);
    }

    [Fact]
    public void BottomDeckRoutingCommands_SelectCorrectTabsAndExpandDeck()
    {
        var studio = CreateStudio();
        studio.IsBottomDeckExpanded = false;

        // Show Problems (Tab 2)
        studio.ShowProblemsTabCommand.Execute(null);
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.Equal(2, studio.SelectedBottomTabIndex);

        // Show Console (Tab 1)
        studio.IsBottomDeckExpanded = false;
        studio.ShowConsoleTabCommand.Execute(null);
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.Equal(1, studio.SelectedBottomTabIndex);

        // Show Dump Results (Tab 0)
        studio.IsBottomDeckExpanded = false;
        studio.ShowDumpResultsTabCommand.Execute(null);
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.Equal(0, studio.SelectedBottomTabIndex);

        // Show Test Cases (Tab 3)
        studio.IsBottomDeckExpanded = false;
        studio.ShowTestCasesTabCommand.Execute(null);
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.Equal(3, studio.SelectedBottomTabIndex);

        // Show Debugger (Tab 4)
        studio.IsBottomDeckExpanded = false;
        studio.ShowDebuggerTabCommand.Execute(null);
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.Equal(4, studio.SelectedBottomTabIndex);
    }

    [Fact]
    public void MultiTabs_InitializedWithSingleTab_AndTracksActiveDocument()
    {
        var studio = CreateStudio();

        Assert.Single(studio.OpenTabs);
        var tab = studio.OpenTabs[0];
        Assert.True(tab.IsActive);
        Assert.Equal(studio.Script.Id, tab.Id);
        Assert.Equal(studio.Script.Title, tab.Title);
    }

    [Fact]
    public async Task MultiTabs_OpeningSecondDocument_CreatesSecondTabAndSwitchesToIt()
    {
        var studio = CreateStudio();
        var secondDoc = new ScriptDocumentItem
        {
            Id = "doc-2",
            Title = "Secondary Script",
            Code = "// Second script code"
        };

        await studio.UpdateActiveScriptAsync(secondDoc);

        Assert.Equal(2, studio.OpenTabs.Count);
        Assert.Equal("doc-2", studio.Script.Id);
        Assert.False(studio.OpenTabs[0].IsActive);
        Assert.True(studio.OpenTabs[1].IsActive);
        Assert.Equal("Secondary Script", studio.OpenTabs[1].Title);
    }

    [Fact]
    public async Task MultiTabs_SwitchingTabs_PreservesCodeBetweenDocuments()
    {
        var studio = CreateStudio();
        studio.Code = "// Code in Document 1 (modified)";

        var secondDoc = new ScriptDocumentItem
        {
            Id = "doc-2",
            Title = "Document 2",
            Code = "// Code in Document 2"
        };
        await studio.UpdateActiveScriptAsync(secondDoc);

        // Switch back to Document 1 via Tab
        var firstTab = studio.OpenTabs[0];
        await studio.SwitchToTabAsync(firstTab);

        Assert.True(firstTab.IsActive);
        Assert.Equal("// Code in Document 1 (modified)", studio.Code);
    }

    [Fact]
    public async Task MultiTabs_ClosingTab_ActivatesRemainingTab()
    {
        var studio = CreateStudio();
        var secondDoc = new ScriptDocumentItem
        {
            Id = "doc-2",
            Title = "Document 2",
            Code = "// Code 2"
        };
        await studio.UpdateActiveScriptAsync(secondDoc);
        Assert.Equal(2, studio.OpenTabs.Count);

        // Close Document 2 tab
        var secondTab = studio.OpenTabs[1];
        await studio.CloseTabAsync(secondTab);

        Assert.Single(studio.OpenTabs);
        Assert.True(studio.OpenTabs[0].IsActive);
        Assert.Equal(studio.OpenTabs[0].Id, studio.Script.Id);
    }

    [Fact]
    public void BottomDeckGridLength_UpdatesDynamicallyOnExpandAndCollapse()
    {
        var studio = CreateStudio();
        Assert.True(studio.IsBottomDeckExpanded);
        Assert.True(studio.BottomDeckGridLength.Value > 0);

        studio.IsBottomDeckExpanded = false;
        Assert.Equal(0, studio.BottomDeckGridLength.Value);

        studio.IsBottomDeckExpanded = true;
        Assert.True(studio.BottomDeckGridLength.Value >= 280);
    }

    [Fact]
    public void SideBarGridLength_UpdatesDynamicallyOnExpandAndCollapse()
    {
        var studio = CreateStudio();
        Assert.True(studio.IsSideBarVisible);
        Assert.True(studio.SideBarGridLength.Value >= 280);

        studio.IsSideBarVisible = false;
        Assert.Equal(0, studio.SideBarGridLength.Value);

        studio.IsSideBarVisible = true;
        Assert.True(studio.SideBarGridLength.Value >= 280);
    }

    [Fact]
    public void ActivityBarFlags_SynchronizeWithSelectedActivityBarIndex()
    {
        var studio = CreateStudio();
        Assert.True(studio.IsExplorerActive);
        Assert.False(studio.IsSearchActive);

        studio.SelectActivityBarItem(1);
        Assert.False(studio.IsExplorerActive);
        Assert.True(studio.IsSearchActive);

        studio.SelectActivityBarItem(2);
        Assert.True(studio.IsDebugActive);

        studio.SelectActivityBarItem(3);
        Assert.True(studio.IsDependenciesActive);

        studio.SelectActivityBarItem(4);
        Assert.True(studio.IsScratchpadActive);

        studio.SelectActivityBarItem(5);
        Assert.True(studio.IsProblemsActive);
    }
}
