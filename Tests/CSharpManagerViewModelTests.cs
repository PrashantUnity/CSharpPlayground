using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpManagerViewModelTests : IDisposable
{
    private readonly string _baseDir;
    private readonly LocalScriptStorageService _storage;

    public CSharpManagerViewModelTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubTests_" + Guid.NewGuid().ToString("N"));
        _storage = new LocalScriptStorageService(_baseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch { }
    }

    private (CSharpManagerViewModel vm, int scriptOpens, int notebookOpens, ScriptDocumentItem? lastScript, NotebookDocumentItem? lastNotebook) CreateHub()
    {
        var scriptOpens = 0;
        var notebookOpens = 0;
        ScriptDocumentItem? lastScript = null;
        NotebookDocumentItem? lastNotebook = null;

        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; lastScript = s; },
            openNotebookAction: n => { notebookOpens++; lastNotebook = n; },
            navigateToHomeAction: () => { });

        return (vm, scriptOpens, notebookOpens, lastScript, lastNotebook);
    }

    [Fact]
    public async Task LoadWorkspaceItemsAsync_ConcurrentCallFromConstructor_DoesNotCorruptOrDuplicateItems()
    {
        // Reproduces the exact scenario from the reported bug: several scripts and notebooks already
        // on disk (including duplicate titles), loaded fresh into the Hub.
        await _storage.CreateNewScriptAsync("C# Interactive Scratchpad");
        await _storage.CreateNewScriptAsync("1. Two Sum (Algorithm Workspace)");
        await _storage.CreateNewScriptAsync("PDF Document Automation");
        await _storage.CreateNewScriptAsync("C# Interactive Scratchpad"); // duplicate title, distinct id
        await _storage.CreateNewNotebookAsync("SkiaSharp Graphics & Image Generation");
        await _storage.CreateNewNotebookAsync("New Interactive Notebook");
        await _storage.CreateNewNotebookAsync("SkiaSharp Graphics & Image Generation"); // duplicate title
        await _storage.CreateNewNotebookAsync("Document Automation Notebook");

        var expectedTotal = (await _storage.LoadWorkspaceSummariesAsync()).Count;

        // The constructor itself fires off an unawaited LoadWorkspaceItemsAsync() — immediately
        // awaiting another call here deliberately races against it, exactly like a user clicking
        // something on the Hub before its initial load has finished.
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        Assert.Equal(expectedTotal, vm.AllItems.Count);
        Assert.Equal(vm.AllItems.Count, vm.TotalScripts + vm.TotalNotebooks);
        Assert.Equal(vm.AllItems.Count, vm.FilteredItems.Count);
        Assert.Equal(vm.AllItems.Count, vm.FilteredItemCount);
        // No item should appear twice (the corrupted/racy version could duplicate entries).
        Assert.Equal(vm.AllItems.Select(i => i.Id).Distinct().Count(), vm.AllItems.Count);
    }

    [Fact]
    public async Task CreateNewScriptCommand_NoParameter_InvokesOpenCallbackWithoutThrowing()
    {
        var scriptOpens = 0;
        ScriptDocumentItem? opened = null;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; opened = s; },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        // Exactly how Avalonia invokes a Button.Command with no CommandParameter set: ICommand.Execute(null).
        ((System.Windows.Input.ICommand)vm.CreateNewScriptCommand).Execute(null);
        await Task.Delay(200); // let the fire-and-forget async command body finish

        Assert.Equal(1, scriptOpens);
        Assert.NotNull(opened);
    }

    [Fact]
    public async Task CreateNewNotebookCommand_NoParameter_InvokesOpenCallbackWithoutThrowing()
    {
        var notebookOpens = 0;
        NotebookDocumentItem? opened = null;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: n => { notebookOpens++; opened = n; },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        ((System.Windows.Input.ICommand)vm.CreateNewNotebookCommand).Execute(null);
        await Task.Delay(200);

        Assert.Equal(1, notebookOpens);
        Assert.NotNull(opened);
    }

    [Fact]
    public async Task OpenItemCommand_ForEachExistingItem_InvokesCorrectCallbackTypeWithoutThrowing()
    {
        // Storage auto-seeds starter templates on first use, so exercise every item that ends up
        // in the workspace (seeded + explicit), not just the ones this test explicitly created.
        await _storage.CreateNewScriptAsync("Some Script");
        await _storage.CreateNewNotebookAsync("Some Notebook");

        var scriptOpens = 0;
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => scriptOpens++,
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        var expectedScripts = vm.AllItems.Count(i => i.IsScript);
        var expectedNotebooks = vm.AllItems.Count(i => i.IsNotebook);
        Assert.True(expectedScripts >= 1);
        Assert.True(expectedNotebooks >= 1);

        foreach (var item in vm.AllItems.ToList())
        {
            await vm.OpenItemAsync(item);
        }

        Assert.Equal(expectedScripts, scriptOpens);
        Assert.Equal(expectedNotebooks, notebookOpens);
    }

    [Fact]
    public async Task LaunchTemplateCommand_ForEveryRealStarterTemplate_OpensSomethingWithoutThrowing()
    {
        var (vm, _, _, _, _) = CreateHub();
        var scriptOpens = 0;
        var notebookOpens = 0;
        vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => scriptOpens++,
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        foreach (var template in vm.StarterTemplates.ToList())
        {
            await vm.LaunchTemplateAsync(template);
        }

        Assert.Equal(vm.StarterTemplates.Count, scriptOpens + notebookOpens);
    }

    // Reproduces the real reported bug: a template card with no busy-guard let a double-click (or a
    // click landing before the constructor's own unawaited load finished) create two documents for
    // the same template instead of one. LaunchTemplateAsync's synchronous prefix (the IsLaunching
    // check, set before any await) means the second of two back-to-back calls is guaranteed to see
    // IsLaunching already true and no-op — this isn't a timing-dependent/flaky assertion.
    [Fact]
    public async Task LaunchTemplateCommand_CalledTwiceBackToBack_CreatesExactlyOneDocument()
    {
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        var template = vm.StarterTemplates.First(t => t.Kind == WorkspaceItemKind.Notebook);

        var firstCall = vm.LaunchTemplateAsync(template);
        var secondCall = vm.LaunchTemplateAsync(template); // fired before firstCall's first await completes
        await Task.WhenAll(firstCall, secondCall);

        var matches = (await _storage.LoadWorkspaceSummariesAsync())
            .Count(i => string.Equals(i.Title, template.Title, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matches);
        Assert.Equal(1, notebookOpens);
    }

    // Same guard, exercised via the "New Notebook" entry point directly (no template involved) —
    // this is the exact path that produced the "New Interactive Notebook (Copy)" artifact found in a
    // real user's library: two rapid clicks on the plain New Notebook button.
    [Fact]
    public async Task CreateNewNotebookCommand_CalledTwiceBackToBack_CreatesExactlyOneDocument()
    {
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        var firstCall = vm.CreateNewNotebookAsync();
        var secondCall = vm.CreateNewNotebookAsync();
        await Task.WhenAll(firstCall, secondCall);

        var matches = (await _storage.LoadWorkspaceSummariesAsync())
            .Count(i => i.Title.StartsWith("New Interactive Notebook", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matches);
        Assert.Equal(1, notebookOpens);
    }
}
