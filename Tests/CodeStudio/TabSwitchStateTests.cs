using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>What switching tabs must and must not change about the documents.</summary>
public class TabSwitchStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_TabSwitchTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public TabSwitchStateTests()
    {
        _storage = new LocalScriptStorageService(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // The OS cleans the temp folder eventually.
        }
    }

    private CSharpCodeStudioViewModel CreateStudio(PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem script) => new(
        script,
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    [Fact]
    public async Task JustSwitchingTabs_DoesNotMarkAnyTabModified()
    {
        var first = await _storage.CreateNewScriptAsync("Alpha");
        first.Code = "// alpha";
        await _storage.SaveScriptAsync(first);
        var second = await _storage.CreateNewScriptAsync("Beta");
        second.Code = "// beta";
        await _storage.SaveScriptAsync(second);

        var studio = CreateStudio(first);
        await studio.UpdateActiveScriptAsync(second);
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == second.Id));

        Assert.All(studio.OpenTabs, t => Assert.False((bool)t.IsDirty, $"'{t.Title}' was only looked at, not edited"));
    }

    [Fact]
    public async Task EditingATab_MarksItModified_AndSwitchingAwayKeepsTheEdit()
    {
        var first = await _storage.CreateNewScriptAsync("Alpha");
        var second = await _storage.CreateNewScriptAsync("Beta");
        var studio = CreateStudio(first);
        await studio.UpdateActiveScriptAsync(second);
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));

        studio.Code = "// edited alpha";
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == second.Id));
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));

        Assert.Equal((string?)"// edited alpha", (string?)studio.Code);
        Assert.True((bool)studio.OpenTabs.First(t => t.Id == first.Id).IsDirty);
        Assert.False((bool)studio.OpenTabs.First(t => t.Id == second.Id).IsDirty);
    }
}
