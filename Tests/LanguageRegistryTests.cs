using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Languages are found by id, alias and extension, and one can be added by registering it.</summary>
public class LanguageRegistryTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_LanguageRegistryTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void BuiltInServices_KnowCSharp_WithEverythingItHas()
    {
        var services = new StudioLanguageServices(_baseDir);
        var csharp = services.CSharp;

        Assert.Same(csharp, services.Registry.Get("C#"));
        Assert.Same(csharp, services.Registry.Get("cs"));
        Assert.Same(csharp, services.Registry.FindByExtension(".frycs"));
        Assert.Equal(LanguageStorageKind.FryDocument, csharp.Storage);
        Assert.True(csharp.Has(LanguageCapabilities.Debugging | LanguageCapabilities.TestCases | LanguageCapabilities.LiveDiagnostics));
        Assert.Null(csharp.Toolchain);
        Assert.Null(csharp.ScriptRunner);
        Assert.IsType<NotebookExecutionKernel>(csharp.NotebookKernels!.Create(new KernelCreationContext(() => null)));
    }

    [Fact]
    public void CreatingServices_TouchesNothingOnDisk()
    {
        _ = new StudioLanguageServices(_baseDir);

        Assert.False(Directory.Exists(_baseDir));
    }

    [Fact]
    public void ALanguage_IsFoundByIdAliasAndExtension_IgnoringCase()
    {
        var registry = new LanguageRegistry();
        var fake = FakeLanguage.RegisterIn(registry);

        Assert.Same(fake, registry.Get("FAKELANG"));
        Assert.Same(fake, registry.Get(" fk "));
        Assert.Same(fake, registry.FindByExtension(".FAKE"));
        Assert.Same(fake, registry.FindByExtension("/work/folder.v2/main.fake"));
        Assert.Same(fake, registry.FindSourceFileLanguage(@"C:\work\main.fake"));
        Assert.Null(registry.FindByExtension("/work/main.txt"));
        Assert.Null(registry.Get(null));
        Assert.Contains(fake, registry.SourceFileLanguages);
        Assert.Contains(fake, registry.NotebookLanguages);
    }

    [Fact]
    public void RegisteringAClashingLanguage_IsRefused()
    {
        var registry = new LanguageRegistry();
        FakeLanguage.RegisterIn(registry);

        Assert.Throws<InvalidOperationException>(() => FakeLanguage.RegisterIn(registry));
    }

    [Fact]
    public void ADocument_IsItsLanguage_OrCSharpWhenItNamesNoneKnown()
    {
        var services = new StudioLanguageServices(_baseDir, configure: (_, r) => FakeLanguage.RegisterIn(r));

        Assert.Equal(FakeLanguage.LanguageId, services.LanguageOf(new ScriptDocumentItem { LanguageId = "fk" }).Id);
        Assert.Same(services.CSharp, services.LanguageOf(new ScriptDocumentItem()));
        Assert.Same(services.CSharp, services.LanguageOf(new ScriptDocumentItem { LanguageId = "cobol" }));
    }

    [Fact]
    public void TheLanguage_IsNeverSavedIntoAFrycsDocument()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new ScriptDocumentItem { LanguageId = "fakelang", SourceFilePath = "/x/main.fake" });

        Assert.DoesNotContain("fakelang", json);
        Assert.DoesNotContain("SourceFilePath", json);
    }

    [Fact]
    public void ACellsLanguage_IsSavedOnlyWhenItHasOne()
    {
        Assert.DoesNotContain("\"Language\"", System.Text.Json.JsonSerializer.Serialize(new NotebookCellItem()));
        Assert.Contains("\"Language\":\"python\"", System.Text.Json.JsonSerializer.Serialize(new NotebookCellItem { Language = "python" }));
    }
}
