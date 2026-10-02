using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class NotebookExtensibilityTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public NotebookExtensibilityTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_NotebookExtTests_" + Guid.NewGuid().ToString("N"));
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
        catch
        {
        }
    }

    private CSharpNotebookStudioViewModel CreateStudio(NotebookDocumentItem? notebook = null)
    {
        var initialNotebook = notebook ?? new NotebookDocumentItem
        {
            Title = "Extensibility Test Notebook"
        };
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        return new CSharpNotebookStudioViewModel(
            initialNotebook,
            _testStorage,
            compiler,
            engine,
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public async Task NotebookCell_CanAccessAppAndStudio_AndModifyThemes()
    {
        var kernel = new NotebookExecutionKernel();

        string code = """
            App.Themes.ApplyTheme("cyberpunk");
            App.Themes.SetColor("DsPrimaryBrush", "#FF0077");
            """;

        var result = await kernel.ExecuteCellAsync(code);
        Assert.True(result.Success, result.ErrorMessage);

        Assert.Equal("cyberpunk", StudioAppContext.Instance.ThemeEngine.ActiveThemeId);
        Assert.True(StudioAppContext.Instance.ThemeEngine.Overrides.ContainsKey("DsPrimaryBrush"));
        Assert.Equal("#FF0077", StudioAppContext.Instance.ThemeEngine.Overrides["DsPrimaryBrush"]);
    }

    [Fact]
    public async Task NotebookCell_CanRegisterCustomCommand_AndInvokeViaPipeline()
    {
        var kernel = new NotebookExecutionKernel();

        string code = """
            App.Commands.Register("nb.custom.cmd", "Custom Action", () =>
            {
                App.State.Set("nb_action_key", 9999);
            }, gesture: "Ctrl+Alt+9");
            """;

        var result = await kernel.ExecuteCellAsync(code);
        Assert.True(result.Success, result.ErrorMessage);

        Assert.Contains("nb.custom.cmd", StudioAppContext.Instance.Commands.RegisteredCommands);

        // Execute via pipeline
        bool executed = await StudioAppContext.Instance.Commands.ExecuteAsync("nb.custom.cmd");
        Assert.True(executed);
        Assert.Equal(9999, StudioAppContext.Instance.State.Get<int>("nb_action_key"));
    }

    [Fact]
    public async Task NotebookCell_CanStoreAndRetrieveCustomState()
    {
        var kernel = new NotebookExecutionKernel();

        string code = """
            App.State.Set("shared_nb_data", "Hello from Notebook Cell");
            """;

        var result = await kernel.ExecuteCellAsync(code);
        Assert.True(result.Success, result.ErrorMessage);

        Assert.Equal("Hello from Notebook Cell", StudioAppContext.Instance.State.Get<string>("shared_nb_data"));
    }

    [Fact]
    public async Task NotebookCell_TriggersBeforeAndAfterExecutionHooks()
    {
        var studio = CreateStudio();
        var tab = studio.ActiveTab!;
        var cell = tab.Cells.First(c => c.Type == CellType.Code);
        cell.Source = "int x = 10 + 20;";

        bool beforeFired = false;
        bool afterFired = false;

        using var beforeDisp = StudioAppContext.Instance.Hooks.OnBeforeScriptRun(ctx =>
        {
            beforeFired = true;
            Assert.Contains("10 + 20", ctx.SourceCode);
        });

        using var afterDisp = StudioAppContext.Instance.Hooks.OnAfterScriptRun(ctx =>
        {
            afterFired = true;
            Assert.True(ctx.Success);
        });

        await tab.RunSingleCellAsync(cell);

        Assert.True(beforeFired);
        Assert.True(afterFired);
    }

    [Fact]
    public async Task NotebookCell_ExecutionCanBeCancelled_ByHook()
    {
        var studio = CreateStudio();
        var tab = studio.ActiveTab!;
        var cell = tab.Cells.First(c => c.Type == CellType.Code);
        cell.Source = "int forbidden = 403;";

        using var beforeDisp = StudioAppContext.Instance.Hooks.OnBeforeScriptRun(ctx =>
        {
            if (ctx.SourceCode.Contains("forbidden"))
            {
                ctx.Cancel("Forbidden cell code detected.");
            }
        });

        await tab.RunSingleCellAsync(cell);

        Assert.Contains("Forbidden cell code detected", cell.OutputText);
    }

    [Fact]
    public void NotebookDocumentContextAdapter_ExposesActiveCellAndFormatsCode()
    {
        var studio = CreateStudio();
        var tab = studio.ActiveTab!;
        var cell = tab.Cells.First(c => c.Type == CellType.Code);
        cell.Source = "int    a=1+2 ;";
        tab.ActiveCell = cell;

        var activeDoc = StudioAppContext.Instance.Editor.ActiveDocument;
        Assert.NotNull(activeDoc);
        Assert.Contains(cell.Source, activeDoc.Text);

        activeDoc.Format();
        Assert.Equal("int a = 1 + 2;", cell.Source.Trim());
    }

    [Fact]
    public void NotebookStudio_CommandPalette_IncludesExtensibilityAndCustomizationCommands()
    {
        var studio = CreateStudio();
        StudioAppContext.Instance.Commands.Register("test.nb.cmd1", "Dynamic Test Command", () => { });

        studio.ShowQuickOpen("commands");

        var items = studio.QuickOpen.FilteredItems;
        Assert.Contains(items, i => i.Title.Contains("Dynamic Test Command"));
        Assert.Contains(items, i => i.Title.Contains("Theme - Dracula Pro"));
        Assert.Contains(items, i => i.Title.Contains("Reload Customizations"));
        Assert.Contains(items, i => i.Title.Contains("Apply Active Cell as Customization"));
        Assert.Contains(items, i => i.Title.Contains("Open User Customization Script"));

        studio.QuickOpen.SearchText = ">Dynamic";
        Assert.Contains(studio.QuickOpen.FilteredItems, i => i.Title.Contains("Dynamic Test Command"));
    }

    [Fact]
    public void ThemeApi_SetSpacingAndSetDimension_ModifiesOverrides()
    {
        StudioAppContext.Instance.Themes.SetSpacing("DensityPaddingMedium", 16.0);
        StudioAppContext.Instance.Themes.SetDimension("DensityTabHeight", 42.0);

        Assert.True(StudioAppContext.Instance.ThemeEngine.Overrides.ContainsKey("DensityPaddingMedium"));
        Assert.Equal("16", StudioAppContext.Instance.ThemeEngine.Overrides["DensityPaddingMedium"]);
        Assert.True(StudioAppContext.Instance.ThemeEngine.Overrides.ContainsKey("DensityTabHeight"));
        Assert.Equal("42", StudioAppContext.Instance.ThemeEngine.Overrides["DensityTabHeight"]);
    }
}

