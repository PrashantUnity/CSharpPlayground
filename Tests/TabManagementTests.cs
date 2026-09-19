using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class TabManagementTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public TabManagementTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_TabManagementTests_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task CSharpCodeStudio_CloseOtherTabs_LeavesOnlyTargetTab()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Script 1");
        var doc2 = await _testStorage.CreateNewScriptAsync("Script 2");
        var doc3 = await _testStorage.CreateNewScriptAsync("Script 3");

        var studio = new CSharpCodeStudioViewModel(
            doc1,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            () => { },
            () => { });

        await studio.UpdateActiveScriptAsync(doc2);
        await studio.UpdateActiveScriptAsync(doc3);

        Assert.Equal(3, studio.OpenTabs.Count);

        var targetTab = studio.OpenTabs.First(t => t.Title == "Script 2");
        await studio.CloseOtherTabsAsync(targetTab);

        Assert.Single(studio.OpenTabs);
        Assert.Equal("Script 2", studio.OpenTabs[0].Title);
        Assert.Equal("Script 2", studio.Script.Title);
    }

    [Fact]
    public async Task CSharpCodeStudio_CloseTabsToTheRight_RemovesSubsequentTabs()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Tab A");
        var doc2 = await _testStorage.CreateNewScriptAsync("Tab B");
        var doc3 = await _testStorage.CreateNewScriptAsync("Tab C");

        var studio = new CSharpCodeStudioViewModel(
            doc1,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            () => { },
            () => { });

        await studio.UpdateActiveScriptAsync(doc2);
        await studio.UpdateActiveScriptAsync(doc3);

        Assert.Equal(3, studio.OpenTabs.Count);

        // Target Tab B (index 1)
        var tabB = studio.OpenTabs.First(t => t.Title == "Tab B");
        await studio.CloseTabsToTheRightAsync(tabB);

        Assert.Equal(2, studio.OpenTabs.Count);
        Assert.Contains(studio.OpenTabs, t => t.Title == "Tab A");
        Assert.Contains(studio.OpenTabs, t => t.Title == "Tab B");
        Assert.DoesNotContain(studio.OpenTabs, t => t.Title == "Tab C");
    }

    [Fact]
    public async Task CSharpCodeStudio_CloseAllTabs_CreatesFreshUntitledScript()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Old Script 1");
        var doc2 = await _testStorage.CreateNewScriptAsync("Old Script 2");

        var studio = new CSharpCodeStudioViewModel(
            doc1,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            () => { },
            () => { });

        await studio.UpdateActiveScriptAsync(doc2);
        Assert.Equal(2, studio.OpenTabs.Count);

        await studio.CloseAllTabsAsync();

        // Never leaves editor empty: creates a fresh untitled scratchpad
        Assert.Single(studio.OpenTabs);
        Assert.Equal("Untitled Script", studio.OpenTabs[0].Title);
        Assert.Equal("Untitled Script", studio.Script.Title);
    }

    [Fact]
    public async Task CSharpNotebookStudio_CloseOtherTabs_LeavesOnlyTargetNotebook()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        var nb1 = new NotebookDocumentItem { Title = "Notebook 1" };
        var studio = new CSharpNotebookStudioViewModel(
            nb1,
            _testStorage,
            compiler,
            engine,
            () => { },
            () => { });

        await studio.NewNotebookTab();
        var tab2 = studio.ActiveTab;
        if (tab2 != null) tab2.Title = "Notebook 2";

        await studio.NewNotebookTab();
        var tab3 = studio.ActiveTab;
        if (tab3 != null) tab3.Title = "Notebook 3";

        Assert.Equal(3, studio.Tabs.Count);

        var targetTab = studio.Tabs.First(t => t.Title == "Notebook 2");
        studio.CloseOtherTabs(targetTab);

        Assert.Single(studio.Tabs);
        Assert.Equal("Notebook 2", studio.Tabs[0].Title);
        Assert.Equal(targetTab, studio.ActiveTab);
    }

    [Fact]
    public async Task CSharpNotebookStudio_CloseTabsToTheRight_RemovesSubsequentTabs()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        var nb1 = new NotebookDocumentItem { Title = "First" };
        var studio = new CSharpNotebookStudioViewModel(
            nb1,
            _testStorage,
            compiler,
            engine,
            () => { },
            () => { });

        await studio.NewNotebookTab();
        var tab2 = studio.ActiveTab;
        if (tab2 != null) tab2.Title = "Middle";

        await studio.NewNotebookTab();
        var tab3 = studio.ActiveTab;
        if (tab3 != null) tab3.Title = "Last";

        Assert.Equal(3, studio.Tabs.Count);

        var middleTab = studio.Tabs.First(t => t.Title == "Middle");
        studio.CloseTabsToTheRight(middleTab);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.DoesNotContain(studio.Tabs, t => t.Title == "Last");
    }
}
