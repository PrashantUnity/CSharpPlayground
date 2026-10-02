using PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;

namespace CSharpEditorPlugin.Tests;

public class StudioZoomTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StudioLanguageServices _services;
    private readonly StudioSettingsStore _settingsStore;

    public StudioZoomTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_ZoomTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _services = new StudioLanguageServices(_tempFolder);
        _settingsStore = _services.StudioSettings;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void ZoomIn_IncrementsByStep_AndClampsAtMax()
    {
        var current = 13.0;
        var next = EditorZoomController.ZoomIn(current);
        Assert.Equal(14.0, next);

        var clamped = EditorZoomController.ZoomIn(EditorZoomController.MaxFontSize);
        Assert.Equal(EditorZoomController.MaxFontSize, clamped);
    }

    [Fact]
    public void ZoomOut_DecrementsByStep_AndClampsAtMin()
    {
        var current = 13.0;
        var next = EditorZoomController.ZoomOut(current);
        Assert.Equal(12.0, next);

        var clamped = EditorZoomController.ZoomOut(EditorZoomController.MinFontSize);
        Assert.Equal(EditorZoomController.MinFontSize, clamped);
    }

    [Fact]
    public void Reset_RestoresDefault13()
    {
        var reset = EditorZoomController.Reset();
        Assert.Equal(EditorZoomController.DefaultFontSize, reset);
        Assert.Equal(13.0, reset);
    }

    [Theory]
    [InlineData(13.0, "100%")]
    [InlineData(26.0, "200%")]
    [InlineData(6.5, "69%")] // 9.0 clamped min -> 9/13 = 69%
    [InlineData(50.0, "277%")] // 36.0 clamped max -> 36/13 = 277%
    public void FormatPercentage_CalculatesAccurately(double size, string expectedPercent)
    {
        var result = EditorZoomController.FormatPercentage(size);
        Assert.Equal(expectedPercent, result);
    }

    [Fact]
    public void Clamp_HandlesNegativeNaNAndOutOfBounds()
    {
        Assert.Equal(EditorZoomController.DefaultFontSize, EditorZoomController.Clamp(double.NaN));
        Assert.Equal(EditorZoomController.DefaultFontSize, EditorZoomController.Clamp(-5));
        Assert.Equal(EditorZoomController.MinFontSize, EditorZoomController.Clamp(2.0));
        Assert.Equal(EditorZoomController.MaxFontSize, EditorZoomController.Clamp(100.0));
    }

    [Fact]
    public void SettingsStore_FiresSettingsChangedEvent_OnSave()
    {
        StudioSettings? observed = null;
        _settingsStore.SettingsChanged += s => observed = s;

        var settings = _settingsStore.GetSettings();
        settings.FontSize = 16.0;
        _settingsStore.SaveSettings(settings);

        Assert.NotNull(observed);
        Assert.Equal(16.0, observed!.FontSize);
    }

    [Fact]
    public async Task EditorZoomController_ScheduleSave_PersistsToStore()
    {
        EditorZoomController.ScheduleSave(_settingsStore, 18.0);

        // Wait for 400ms debounce to persist
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && Math.Abs(_settingsStore.GetSettings().FontSize - 18.0) > 0.001)
        {
            await Task.Delay(50);
        }

        var reloaded = _settingsStore.GetSettings();
        Assert.Equal(18.0, reloaded.FontSize);
    }

    [Fact]
    public void CodeStudioViewModel_ZoomInAndOut_UpdatesFontSizeAndPercentage()
    {
        var storage = new LocalScriptStorageService(_tempFolder);
        var compiler = new RoslynCompilerService();
        var executionEngine = new ScriptExecutionEngine();
        var script = new ScriptDocumentItem { Title = "TestScript", Code = "Console.WriteLine(1);" };

        var vm = new CSharpCodeStudioViewModel(
            script,
            storage,
            compiler,
            executionEngine,
            backToHubAction: () => { },
            languages: _services,
            postToUiThread: action => action());

        Assert.Equal(13.0, vm.EditorFontSize);
        Assert.Equal("100%", vm.ZoomPercentageText);

        vm.ZoomInCommand.Execute(null);
        Assert.Equal(14.0, vm.EditorFontSize);
        Assert.Equal("108%", vm.ZoomPercentageText);

        vm.ZoomOutCommand.Execute(null);
        Assert.Equal(13.0, vm.EditorFontSize);
        Assert.Equal("100%", vm.ZoomPercentageText);

        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);
        Assert.Equal(15.0, vm.EditorFontSize);

        vm.ResetZoomCommand.Execute(null);
        Assert.Equal(13.0, vm.EditorFontSize);
        Assert.Equal("100%", vm.ZoomPercentageText);
    }

    [Fact]
    public void NotebookStudioViewModel_ZoomInAndOut_UpdatesFontSizeAndPercentage()
    {
        var storage = new LocalScriptStorageService(_tempFolder);
        var compiler = new RoslynCompilerService();
        var executionEngine = new ScriptExecutionEngine();
        var notebook = new NotebookDocumentItem { Title = "TestNotebook" };

        var vm = new CSharpNotebookStudioViewModel(
            notebook,
            storage,
            compiler,
            executionEngine,
            backToHubAction: () => { },
            languages: _services);

        Assert.Equal(13.0, vm.EditorFontSize);
        Assert.Equal("100%", vm.ZoomPercentageText);

        vm.ZoomInCommand.Execute(null);
        Assert.Equal(14.0, vm.EditorFontSize);
        Assert.Equal("108%", vm.ZoomPercentageText);

        vm.ResetZoomCommand.Execute(null);
        Assert.Equal(13.0, vm.EditorFontSize);
        Assert.Equal("100%", vm.ZoomPercentageText);
    }
}
