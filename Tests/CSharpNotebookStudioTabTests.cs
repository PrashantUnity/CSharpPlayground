using System;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpNotebookStudioTabTests
{
    private CSharpNotebookStudioViewModel CreateStudio()
    {
        var initialNotebook = new NotebookDocumentItem
        {
            Title = "Document Automation Notebook"
        };
        var storage = new LocalScriptStorageService();
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        return new CSharpNotebookStudioViewModel(
            initialNotebook,
            storage,
            compiler,
            engine,
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public void Studio_InitialState_HasInitialTab_AndIsActive()
    {
        var studio = CreateStudio();

        Assert.Single(studio.Tabs);
        Assert.NotNull(studio.ActiveTab);
        Assert.True(studio.HasActiveTab);
        Assert.False(studio.HasNoTabs);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
        Assert.NotEmpty(studio.Cells);
    }

    [Fact]
    public void OpenDocument_MultipleFiles_DisplaysMultipleTabs()
    {
        var studio = CreateStudio();

        // Find sample file in Explorer tree
        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        Assert.NotNull(codeFolder);

        var sampleFile = codeFolder.Children.FirstOrDefault(x => x.Name == "codefrydev.frynb");
        Assert.NotNull(sampleFile);

        // Open second notebook
        studio.OpenDocument(sampleFile);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("codefrydev.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
        Assert.False(studio.Tabs[0].IsActive);
    }

    [Fact]
    public void OpenDocument_SameFileTwice_DoesNotDuplicateTabs()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var docFile = codeFolder!.Children.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(docFile);

        // Click same file again
        studio.OpenDocument(docFile);

        Assert.Single(studio.Tabs);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab!.Title);
    }

    [Fact]
    public void SelectTab_SwitchesActiveTab_AndUpdatesExplorerSelection()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var sampleFile = codeFolder!.Children.FirstOrDefault(x => x.Name == "codefrydev.frynb");
        studio.OpenDocument(sampleFile!);

        Assert.Equal("codefrydev.frynb", studio.ActiveTab!.Title);

        // Switch back to first tab
        studio.SelectTab(studio.Tabs[0]);

        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.Tabs[0].IsActive);
        Assert.False(studio.Tabs[1].IsActive);

        // Verify Explorer selection updated
        var firstItem = codeFolder.Children.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(firstItem);
        Assert.True(firstItem.IsSelected);
    }

    [Fact]
    public void CloseTab_SwitchesToAdjacentTab()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var sampleFile = codeFolder!.Children.FirstOrDefault(x => x.Name == "codefrydev.frynb");
        studio.OpenDocument(sampleFile!);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.Equal("codefrydev.frynb", studio.ActiveTab!.Title);

        // Close the active tab
        studio.CloseTab(studio.ActiveTab);

        Assert.Single(studio.Tabs);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
    }

    [Fact]
    public void CloseTab_WhenAllTabsClosed_HasNoTabs()
    {
        var studio = CreateStudio();

        Assert.Single(studio.Tabs);

        studio.CloseTab(studio.Tabs[0]);

        Assert.Empty(studio.Tabs);
        Assert.Null(studio.ActiveTab);
        Assert.True(studio.HasNoTabs);
        Assert.False(studio.HasActiveTab);
    }

    [Fact]
    public void BreadcrumbFormatting_MarkdownCell_CleansMarkdownTokensWithoutDuplicatingOrCSharpPrefix()
    {
        var studio = CreateStudio();

        var activeTab = studio.ActiveTab!;
        var mdCellItem = new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = "# 📓 Polyglot Notebook Demo Copy\nWrite documentation or notes in this cell."
        };
        var mdCellVm = activeTab.CreateCellViewModel(mdCellItem);
        activeTab.Cells.Add(mdCellVm);
        activeTab.SelectCell(mdCellVm);

        // Verify breadcrumb segment formatting
        Assert.Equal("Code", studio.BreadcrumbFolder);
        Assert.Equal("Document Automation Notebook.frynb", studio.BreadcrumbDocument);
        Assert.Equal("Markdown: 📓 Polyglot Notebook Demo Copy", activeTab.ActiveCellBadgeText);
        Assert.Equal("FormatHeaderPound", activeTab.ActiveCellTypeIcon);
        Assert.Equal("#4EC9B0", activeTab.ActiveCellTypeColor);

        // Ensure no bizarre "> C# # 📓" concatenation
        Assert.DoesNotContain("C# #", studio.BreadcrumbText);
        Assert.DoesNotContain("> C#", studio.BreadcrumbText);
    }

    [Fact]
    public void BreadcrumbFormatting_CodeCell_FormatsCleanSnippet()
    {
        var studio = CreateStudio();

        var activeTab = studio.ActiveTab!;
        var codeCellItem = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "public class People\n{\n}"
        };
        var codeCellVm = activeTab.CreateCellViewModel(codeCellItem);
        codeCellVm.ExecutionCount = 1;
        activeTab.Cells.Add(codeCellVm);
        activeTab.SelectCell(codeCellVm);

        Assert.Equal("Cell [1]: public class People", activeTab.ActiveCellBadgeText);
        Assert.Equal("CodeBraces", activeTab.ActiveCellTypeIcon);
        Assert.Equal("#58A6FF", activeTab.ActiveCellTypeColor);
        Assert.Equal("Code › Document Automation Notebook.frynb › Cell [1]: public class People", studio.BreadcrumbText);
    }

    [Fact]
    public void NewNotebookTab_CreatesTabAndAddsToExplorer()
    {
        var studio = CreateStudio();

        var initialCount = studio.Tabs.Count;
        studio.NewNotebookTab();

        Assert.Equal(initialCount + 1, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.StartsWith("Notebook_", studio.ActiveTab.Title);

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        Assert.NotNull(codeFolder);
        Assert.Contains(codeFolder.Children, x => x.Name == studio.ActiveTab.Title);
    }

    [Fact]
    public void DeleteExplorerItem_ClosesOpenTab()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var sampleFile = codeFolder!.Children.FirstOrDefault(x => x.Name == "codefrydev.frynb");
        studio.OpenDocument(sampleFile!);

        Assert.Equal(2, studio.Tabs.Count);

        // Delete from explorer
        studio.DeleteExplorerItem(sampleFile!);

        Assert.Single(studio.Tabs);
        Assert.DoesNotContain(studio.Tabs, t => t.Title == "codefrydev.frynb");
    }
}
