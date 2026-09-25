using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>The Scratchpad & Notes side bar shows a script's notes rendered as markdown, with Edit / Done to switch.</summary>
public class ScratchpadNotesPreviewTests : IDisposable
{
    private readonly string _storageDir = Path.Combine(Path.GetTempPath(), "FryPDF_NotesPreviewTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public ScratchpadNotesPreviewTests()
    {
        _storage = new LocalScriptStorageService(_storageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDir)) Directory.Delete(_storageDir, recursive: true);
        }
        catch { }
    }

    private CSharpCodeStudioViewModel Studio(ScriptDocumentItem script) =>
        new(script, _storage, new RoslynCompilerService(), new ScriptExecutionEngine(), backToHubAction: () => { }, backToHomeAction: () => { });

    private static ScriptDocumentItem Script(string notes) => new() { Title = "Notes test", Code = "var x = 1;", Notes = notes };

    [Fact]
    public void ABlind75Script_OpensWithItsStatementRendered()
    {
        var studio = Studio(Blind75CatalogService.ConvertToScript(Blind75CatalogService.GetProblemByNumber(1)!));

        Assert.True(studio.IsNotesPreviewMode);
        Assert.False(studio.IsEditingNotes);
        Assert.StartsWith("# 1. Two Sum", studio.Notes);
    }

    [Fact]
    public void AnEmptyScratchpad_OpensReadyToType()
    {
        var studio = Studio(Script(string.Empty));

        Assert.False(studio.IsNotesPreviewMode);
        Assert.True(studio.IsEditingNotes);
    }

    [Fact]
    public void EditAndDone_SwitchModes_AndOnlyEditAsksForTheCursor()
    {
        var studio = Studio(Script("# Plan\n\n- try a hash map"));
        int focusRequests = 0;
        studio.RequestFocusNotes += () => focusRequests++;

        studio.ToggleNotesPreviewCommand.Execute(null); // Edit
        Assert.True(studio.IsEditingNotes);
        Assert.Equal(1, focusRequests);

        studio.Notes += "\n- then sort";
        studio.ToggleNotesPreviewCommand.Execute(null); // Done
        Assert.True(studio.IsNotesPreviewMode);
        Assert.Equal(1, focusRequests);
        Assert.EndsWith("- then sort", studio.Script.Notes);
    }

    [Fact]
    public async Task SwitchingTabs_ShowsEachTabsNotesRendered_AndEmptyOnesForEditing()
    {
        var withNotes = await _storage.CreateNewScriptAsync("With notes");
        withNotes.Notes = "## Requirements\n\nReturn the indices.";
        var withoutNotes = await _storage.CreateNewScriptAsync("Without notes");
        withoutNotes.Notes = string.Empty;
        var studio = Studio(withNotes);

        await studio.UpdateActiveScriptAsync(withoutNotes);
        Assert.True(studio.IsEditingNotes);

        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == withNotes.Id));
        Assert.True(studio.IsNotesPreviewMode);
        Assert.Equal("## Requirements\n\nReturn the indices.", studio.Notes);
    }

    [Fact]
    public void InsertingATemplate_IntoAnEmptyScratchpad_ShowsItsNotesRendered()
    {
        var studio = Studio(Script(string.Empty));
        var template = new CodeTemplate { Title = "Two pointers", InitialCode = "// two pointers", Notes = "# Two pointers\n\nMove the ends inward." };

        studio.InsertTemplate(template);

        Assert.True(studio.IsNotesPreviewMode);
        Assert.Contains("Move the ends inward.", studio.Notes);
    }

    [Fact]
    public void InsertingATemplate_WhileEditingNotes_LeavesTheEditorOpen()
    {
        var studio = Studio(Script("my own notes"));
        studio.ToggleNotesPreviewCommand.Execute(null); // editing what I typed

        studio.InsertTemplate(new CodeTemplate { Title = "Sliding window", InitialCode = "// window", Notes = "# Sliding window" });

        Assert.True(studio.IsEditingNotes);
        Assert.Equal("my own notes\n\n# Sliding window", studio.Notes);
    }
}
