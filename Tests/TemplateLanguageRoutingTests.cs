using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>A gallery template written in another language opens as a real file of that language, not as a C# document holding foreign code.</summary>
public class TemplateLanguageRoutingTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_TemplateRouting_" + Guid.NewGuid().ToString("N"));
    private readonly StudioLanguageServices _languages;
    private readonly LocalScriptStorageService _storage;

    public TemplateLanguageRoutingTests()
    {
        _languages = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        _storage = new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), _languages.Registry);
    }

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

    [Theory]
    [InlineData("rust_iterators_starter", ".rs")]
    [InlineData("cpp_algorithms_starter", ".cpp")]
    [InlineData("go_algorithms_starter", ".go")]
    [InlineData("fsharp_functional_starter", ".fsx")]
    [InlineData("sql_database_starter", ".sql")]
    public async Task ALanguageTemplate_OpensAsARealFileOfThatLanguage(string templateId, string extension)
    {
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == templateId);

        var script = await _storage.CreateNewScriptAsync(template.Title, templateId);

        Assert.NotNull(script.SourceFilePath);
        Assert.Equal(templateId + extension, Path.GetFileName(script.SourceFilePath));
        Assert.Equal(template.InitialCode, File.ReadAllText(script.SourceFilePath));
        Assert.Equal(template.InitialCode, script.Code);
        Assert.Equal(template.LanguageId, script.LanguageId);
    }

    [Fact]
    public async Task ACSharpTemplate_IsStillACSharpDocument()
    {
        var script = await _storage.CreateNewScriptAsync("Fibonacci", "quicksort_sorting_bars");

        Assert.Equal(LanguageIds.CSharp, script.LanguageId);
        Assert.Null(script.SourceFilePath);
    }

    [Fact]
    public async Task TheSameTemplateTwice_MakesTwoFiles()
    {
        var first = await _storage.CreateNewScriptAsync("Rust", "rust_iterators_starter");
        var second = await _storage.CreateNewScriptAsync("Rust", "rust_iterators_starter");

        Assert.NotEqual(first.SourceFilePath, second.SourceFilePath);
        Assert.Equal("rust_iterators_starter_2.rs", Path.GetFileName(second.SourceFilePath));
    }

    [Fact]
    public void EveryTemplateWithALanguage_NamesARegisteredSourceFileLanguage()
    {
        var withLanguage = CodeTemplateLibrary.GetTemplates().Where(t => t.LanguageId != null).ToList();

        Assert.NotEmpty(withLanguage);
        foreach (var template in withLanguage)
        {
            var language = (template.LanguageId != null ? _languages.Registry.Get(template.LanguageId) : null)
                ?? (template.LanguageId != null ? PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.Languages.Get(template.LanguageId) : null);
            Assert.NotNull(language);
            Assert.Equal(LanguageStorageKind.SourceFile, language.Storage);
            Assert.Equal(WorkspaceItemKind.Script, template.Kind);
        }
    }

    [Fact]
    public void EveryTemplateWrittenInAnotherLanguage_SaysSo()
    {
        // These four gallery cards were once C# documents holding C++, Go, F# and SQL.
        var templates = CodeTemplateLibrary.GetTemplates().ToDictionary(t => t.Id);

        Assert.Equal(LanguageIds.Cpp, templates["cpp_algorithms_starter"].LanguageId);
        Assert.Equal(LanguageIds.Go, templates["go_algorithms_starter"].LanguageId);
        Assert.Equal(LanguageIds.FSharp, templates["fsharp_functional_starter"].LanguageId);
        Assert.Equal(LanguageIds.Sql, templates["sql_database_starter"].LanguageId);
        Assert.Equal(LanguageIds.Rust, templates["rust_iterators_starter"].LanguageId);
    }
}
