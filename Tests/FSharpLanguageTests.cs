using AvaloniaEdit;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class FSharpLanguageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudioLanguageServices _services;

    public FSharpLanguageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_FSLangTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _services = new StudioLanguageServices(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void FSharpLanguage_IsRegisteredInLanguageServices()
    {
        var lang = _services.Registry.Get(LanguageIds.FSharp);
        Assert.NotNull(lang);
        Assert.IsType<FSharpLanguage>(lang);
        Assert.Equal("F#", lang.DisplayName);
        Assert.Equal(LanguageIds.FSharp, lang.Id);
    }

    [Fact]
    public void FSharpLanguage_ResolvesByAliasesAndExtensions()
    {
        var byFsx = _services.Registry.FindByExtension(".fsx");
        var byFs = _services.Registry.FindByExtension(".fs");
        var byUpper = _services.Registry.FindByExtension(".FSX");
        var byId = _services.Registry.Get("fsharp");
        var byFsAlias = _services.Registry.Get("fs");
        var byFsxAlias = _services.Registry.Get("fsx");

        Assert.NotNull(byFsx);
        Assert.NotNull(byFs);
        Assert.NotNull(byUpper);
        Assert.NotNull(byId);
        Assert.NotNull(byFsAlias);
        Assert.NotNull(byFsxAlias);

        Assert.Same(byId, byFsx);
        Assert.Same(byId, byFs);
        Assert.Same(byId, byUpper);
        Assert.Same(byId, byFsAlias);
        Assert.Same(byId, byFsxAlias);
    }

    [Fact]
    public void FSharpLanguage_DeclaresCorrectCapabilities()
    {
        var lang = _services.Registry.Get(LanguageIds.FSharp)!;
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.StandardInput));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.QuickInfo));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Completion));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.NotebookCells));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.ValueSharing));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Packages));
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.Formatting));
        Assert.False(lang.Capabilities.HasFlag(LanguageCapabilities.Debugging));
    }

    [Fact]
    public void FSharpLanguage_ProvidesSyntaxHighlightingForDarkAndLight()
    {
        var lang = _services.Registry.Get(LanguageIds.FSharp)!;
        var dark = lang.GetHighlighting(isDark: true);
        var light = lang.GetHighlighting(isDark: false);

        Assert.NotNull(dark);
        Assert.NotNull(light);
        Assert.Equal("F# Dark", dark.Name);
        Assert.Equal("F# Light", light.Name);
    }

    [Fact]
    public void FSharpLanguage_ProvidesIndentationAndFolding()
    {
        var lang = _services.Registry.Get(LanguageIds.FSharp)!;
        var indent = lang.CreateIndentationStrategy(new TextEditorOptions());
        var folding = lang.Folding;

        Assert.NotNull(indent);
        Assert.NotNull(folding);
    }

    [Fact]
    public void FSharpLanguage_ProvidesNewFileTemplate()
    {
        var lang = _services.Registry.Get(LanguageIds.FSharp)!;
        Assert.False(string.IsNullOrWhiteSpace(lang.NewFileTemplate));
        Assert.Contains("open System", lang.NewFileTemplate);
        Assert.Contains("printfn", lang.NewFileTemplate);
    }

    [Fact]
    public void FSharp_WorkspaceItemSummary_MapsToFunctionVariantIconAndColor()
    {
        var summary = new WorkspaceItemSummary
        {
            Title = "script.fsx",
            LanguageId = LanguageIds.FSharp
        };

        Assert.Equal(MaterialIconKind.FunctionVariant, summary.IconKind);
        Assert.Equal("#30B9DB", summary.IconForeground);
    }
}
