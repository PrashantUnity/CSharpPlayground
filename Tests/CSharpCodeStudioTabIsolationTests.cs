using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpCodeStudioTabIsolationTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public CSharpCodeStudioTabIsolationTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_TabIsolationTests_" + Guid.NewGuid().ToString("N"));
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

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem initialScript)
    {
        return new CSharpCodeStudioViewModel(
            initialScript,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public async Task ConsoleOutput_IsIsolatedBetweenTabs()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Script One");
        doc1.Code = "Console.WriteLine(\"Output from Script 1\");";
        await _testStorage.SaveScriptAsync(doc1);

        var doc2 = await _testStorage.CreateNewScriptAsync("Script Two");
        doc2.Code = "Console.WriteLine(\"Output from Script 2\");";
        await _testStorage.SaveScriptAsync(doc2);

        var studio = CreateStudio(doc1);

        // Open Tab 2
        await studio.UpdateActiveScriptAsync(doc2);
        Assert.Equal(2, studio.OpenTabs.Count);

        // Execute in Tab 2
        await studio.RunCodeCommand.ExecuteAsync(null);
        Assert.Contains("Output from Script 2", studio.ConsoleOutput);

        var tab2 = studio.OpenTabs.First(t => t.Id == doc2.Id);
        Assert.Contains("Output from Script 2", tab2.ConsoleOutput);

        // Switch back to Tab 1
        var tab1 = studio.OpenTabs.First(t => t.Id == doc1.Id);
        await studio.SwitchToTabAsync(tab1);

        // Tab 1's console output should NOT have Tab 2's output
        Assert.DoesNotContain("Output from Script 2", studio.ConsoleOutput);
        Assert.DoesNotContain("Output from Script 2", tab1.ConsoleOutput);

        // Execute in Tab 1
        await studio.RunCodeCommand.ExecuteAsync(null);
        Assert.Contains("Output from Script 1", studio.ConsoleOutput);
        Assert.Contains("Output from Script 1", tab1.ConsoleOutput);

        // Switch back to Tab 2 and verify its console output was retained
        await studio.SwitchToTabAsync(tab2);
        Assert.Contains("Output from Script 2", studio.ConsoleOutput);
        Assert.DoesNotContain("Output from Script 1", studio.ConsoleOutput);
    }

    [Fact]
    public async Task Diagnostics_AreIsolatedBetweenTabs()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Syntax Error Script");
        doc1.Code = "invalid csharp code that causes compile errors;;;";
        await _testStorage.SaveScriptAsync(doc1);

        var doc2 = await _testStorage.CreateNewScriptAsync("Clean Script");
        doc2.Code = "int x = 42; Console.WriteLine(x);";
        await _testStorage.SaveScriptAsync(doc2);

        var studio = CreateStudio(doc1);

        // Wait a moment for diagnostics check on Tab 1
        await Task.Delay(400);

        var tab1 = studio.OpenTabs.First(t => t.Id == doc1.Id);
        // Switch to Tab 2 (clean)
        await studio.UpdateActiveScriptAsync(doc2);
        var tab2 = studio.OpenTabs.First(t => t.Id == doc2.Id);

        // Wait for clean diagnostics check
        await Task.Delay(400);

        // Tab 2 has clean code, so 0 compile errors
        Assert.Equal(0, studio.ErrorCount);
        Assert.Empty(tab2.Diagnostics);

        // Switch back to Tab 1
        await studio.SwitchToTabAsync(tab1);

        // Tab 1 should restore its diagnostics/errors
        Assert.True(studio.ErrorCount > 0, "Tab 1 errors should be restored when switching back");
        Assert.NotEmpty(tab1.Diagnostics);
    }

    [Fact]
    public async Task BreakpointsAndPausedState_AreIsolatedPerTab()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Script Alpha");
        doc1.Code = "int a = 1;\nint b = 2;\nint c = 3;";
        doc1.Breakpoints = new System.Collections.Generic.List<int> { 2 };
        await _testStorage.SaveScriptAsync(doc1);

        var doc2 = await _testStorage.CreateNewScriptAsync("Script Beta");
        doc2.Code = "string s = \"hello\";";
        doc2.Breakpoints = new System.Collections.Generic.List<int>();
        await _testStorage.SaveScriptAsync(doc2);

        var studio = CreateStudio(doc1);
        Assert.Single(studio.Breakpoints);
        Assert.Equal(2, studio.Breakpoints[0].LineNumber);

        // Open Tab 2
        await studio.UpdateActiveScriptAsync(doc2);
        Assert.Empty(studio.Breakpoints);

        // Add breakpoint on line 1 for Tab 2
        studio.ToggleBreakpoint(1);
        Assert.Single(studio.Breakpoints);
        Assert.Equal(1, studio.Breakpoints[0].LineNumber);

        // Switch back to Tab 1
        var tab1 = studio.OpenTabs.First(t => t.Id == doc1.Id);
        await studio.SwitchToTabAsync(tab1);

        // Tab 1 breakpoints should be line 2, not line 1
        Assert.Single(studio.Breakpoints);
        Assert.Equal(2, studio.Breakpoints[0].LineNumber);
    }

    [Fact]
    public async Task CaretPosition_IsRememberedPerTab()
    {
        var doc1 = await _testStorage.CreateNewScriptAsync("Doc 1");
        doc1.Code = "line 1\nline 2\nline 3\nline 4";
        await _testStorage.SaveScriptAsync(doc1);

        var doc2 = await _testStorage.CreateNewScriptAsync("Doc 2");
        doc2.Code = "item a\nitem b";
        await _testStorage.SaveScriptAsync(doc2);

        var studio = CreateStudio(doc1);
        var tab1 = studio.OpenTabs.First(t => t.Id == doc1.Id);
        tab1.CaretLine = 3;
        tab1.CaretColumn = 5;

        // Open Doc 2
        await studio.UpdateActiveScriptAsync(doc2);
        var tab2 = studio.OpenTabs.First(t => t.Id == doc2.Id);
        tab2.CaretLine = 2;
        tab2.CaretColumn = 1;

        // Switch back to Doc 1
        await studio.SwitchToTabAsync(tab1);
        Assert.Equal(3, tab1.CaretLine);
        Assert.Equal(5, tab1.CaretColumn);

        // Switch back to Doc 2
        await studio.SwitchToTabAsync(tab2);
        Assert.Equal(2, tab2.CaretLine);
        Assert.Equal(1, tab2.CaretColumn);
    }
}
