using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RustLanguageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StudioLanguageServices _services;

    public RustLanguageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_RustLangTest_" + Guid.NewGuid().ToString("N"));
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
    public void RustLanguage_IsRegisteredInLanguageServices()
    {
        var lang = _services.Registry.Get(LanguageIds.Rust);

        Assert.IsType<RustLanguage>(lang);
        Assert.Equal("Rust", lang.DisplayName);
        Assert.Equal("rust", lang.Id);
    }

    [Fact]
    public void RustLanguage_ResolvesByAliasesAndExtensions()
    {
        var byId = _services.Registry.Get("rust");

        Assert.NotNull(byId);
        Assert.Same(byId, _services.Registry.Get("rs"));
        Assert.Same(byId, _services.Registry.Get("RUST"));
        Assert.Same(byId, _services.Registry.FindByExtension(".rs"));
        Assert.Same(byId, _services.Registry.FindByExtension(".RS"));
        Assert.Same(byId, _services.Registry.FindSourceFileLanguage("/work/main.rs"));
    }

    [Fact]
    public void NoOtherLanguage_AnswersToRustsNamesOrExtension()
    {
        var others = _services.Registry.All.Where(l => l.Id != LanguageIds.Rust).ToList();

        Assert.DoesNotContain(others, l => l.IsNamed("rust") || l.IsNamed("rs"));
        Assert.DoesNotContain(others, l => l.FileExtensions.Contains(".rs", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RustLanguage_HasCorrectVisualAndMetadataProperties()
    {
        var lang = (RustLanguage)_services.Registry.Get(LanguageIds.Rust)!;

        Assert.Equal("LanguageRust", lang.IconKind);
        Assert.Equal("#DEA584", lang.AccentHex);
        Assert.Equal("RS", lang.ShortName);
        Assert.Equal("//", lang.LineCommentPrefix);
        Assert.Equal(LanguageStorageKind.SourceFile, lang.Storage);
        Assert.Equal([".rs"], lang.FileExtensions);
        Assert.True(lang.IsCompiled);
        Assert.Contains("fn main()", lang.NewFileTemplate);
        Assert.Contains("// #crate:", lang.NewFileTemplate);
    }

    [Fact]
    public void RustLanguage_DescribesItselfToJupyterByItsRealName()
    {
        var jupyter = _services.Registry.Get(LanguageIds.Rust)!.Jupyter!;

        Assert.Equal("rust", jupyter.KernelName);
        Assert.Equal("rust", jupyter.LanguageName);
        Assert.Equal(".rs", jupyter.FileExtension);
    }

    [Fact]
    public void RustLanguage_ExposesTheRunPipeline()
    {
        var lang = (RustLanguage)_services.Registry.Get(LanguageIds.Rust)!;

        Assert.Same(lang.RustToolchain, lang.Toolchain);
        Assert.NotNull(lang.ScriptRunner);
        Assert.IsType<RustDiagnosticParser>(lang.RunDiagnostics);
        Assert.True(lang.Capabilities.HasFlag(LanguageCapabilities.StandardInput));
    }

    [Fact]
    public void WorkspaceItemSummary_ReturnsRustIconAndAccent()
    {
        var summary = new WorkspaceItemSummary
        {
            LanguageId = LanguageIds.Rust,
            IsSourceFile = true
        };

        Assert.Equal(MaterialIconKind.LanguageRust, summary.IconKind);
    }

    [Fact]
    public void ANewRustFile_IsOfferedFromTheSourceFileLanguages()
    {
        Assert.Contains(_services.Registry.SourceFileLanguages, l => l.Id == LanguageIds.Rust);
    }
}
