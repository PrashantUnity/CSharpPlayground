using AvaloniaEdit;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class GoLanguageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudioLanguageServices _services;

    public GoLanguageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_GoLangTest_" + Guid.NewGuid().ToString("N"));
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
    public void GoLanguage_IsRegisteredInLanguageServices()
    {
        var lang = _services.Registry.Get(LanguageIds.Go);
        Assert.NotNull(lang);
        Assert.IsType<GoLanguage>(lang);
        Assert.Equal("Go", lang.DisplayName);
        Assert.Equal(LanguageIds.Go, lang.Id);
    }

    [Fact]
    public void GoLanguage_ResolvesByAliasesAndExtensions()
    {
        var byExt = _services.Registry.FindByExtension(".go");
        var byUpperExt = _services.Registry.FindByExtension(".GO");
        var byId = _services.Registry.Get("go");
        var byGolang = _services.Registry.Get("golang");

        Assert.NotNull(byExt);
        Assert.NotNull(byUpperExt);
        Assert.NotNull(byId);
        Assert.NotNull(byGolang);

        Assert.Same(byId, byExt);
        Assert.Same(byId, byUpperExt);
        Assert.Same(byId, byGolang);
    }

    [Fact]
    public void GoLanguage_HasCorrectVisualAndMetadataProperties()
    {
        var lang = (GoLanguage)_services.Registry.Get(LanguageIds.Go)!;

        Assert.Equal("LanguageGo", lang.IconKind);
        Assert.Equal("#00ADD8", lang.AccentHex);
        Assert.Equal(LanguageStorageKind.SourceFile, lang.Storage);
        Assert.Contains(".go", lang.FileExtensions);
        Assert.Contains("package main", lang.NewFileTemplate);
        Assert.Contains("func main()", lang.NewFileTemplate);
        Assert.True(lang.IsCompiled);
    }

    [Fact]
    public void GoLanguage_ExposesExpectedCapabilities()
    {
        var lang = _services.Registry.Get(LanguageIds.Go)!;
        var caps = lang.Capabilities;

        Assert.True(caps.HasFlag(LanguageCapabilities.StandardInput));
        Assert.True(caps.HasFlag(LanguageCapabilities.NotebookCells));
        Assert.True(caps.HasFlag(LanguageCapabilities.ValueSharing));
        Assert.True(caps.HasFlag(LanguageCapabilities.Packages));
        Assert.True(caps.HasFlag(LanguageCapabilities.Breakpoints));
        Assert.True(caps.HasFlag(LanguageCapabilities.Debugging));
        Assert.True(caps.HasFlag(LanguageCapabilities.Completion));
        Assert.True(caps.HasFlag(LanguageCapabilities.QuickInfo));
    }

    [Fact]
    public void GoLanguage_ExposesServicesAndStrategies()
    {
        var lang = (GoLanguage)_services.Registry.Get(LanguageIds.Go)!;

        Assert.NotNull(lang.Toolchain);
        Assert.NotNull(lang.Packages);
        Assert.NotNull(lang.Debugger);
        Assert.NotNull(lang.EditorAssistants);
        Assert.NotNull(lang.Folding);
        Assert.NotNull(lang.RunDiagnostics);
        Assert.NotNull(lang.CreateIndentationStrategy(new TextEditorOptions()));

        var highlighting = lang.GetHighlighting(isDark: true);
        Assert.NotNull(highlighting);
        Assert.Equal("Go Dark", highlighting.Name);

        var lightHighlighting = lang.GetHighlighting(isDark: false);
        Assert.NotNull(lightHighlighting);
        Assert.Equal("Go Light", lightHighlighting.Name);
    }

    [Fact]
    public void WorkspaceItemSummary_ReturnsGoIcon()
    {
        var summary = new WorkspaceItemSummary
        {
            LanguageId = LanguageIds.Go,
            IsSourceFile = true
        };

        Assert.Equal(MaterialIconKind.LanguageGo, summary.IconKind);
    }
}
