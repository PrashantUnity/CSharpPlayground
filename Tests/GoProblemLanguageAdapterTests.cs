using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Go;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoProblemLanguageAdapterTests
{
    [Fact]
    public void Adapter_IsRegisteredInProblemLanguageRegistry()
    {
        var adapter = ProblemLanguageRegistry.GetAdapter(LanguageIds.Go);
        Assert.NotNull(adapter);
        Assert.Equal("go", adapter.LanguageId);
        Assert.Equal(".go", adapter.DefaultFileExtension);
    }

    [Fact]
    public void BuildScript_GeneratesValidGoScriptWithImportsAndHarness()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1); // Two Sum
        Assert.NotNull(problem);

        var adapter = GoProblemLanguageAdapter.Instance;
        var script = adapter.BuildScript(problem);

        Assert.NotNull(script);
        Assert.Equal(LanguageIds.Go, script.LanguageId);
        Assert.EndsWith(".go", script.Title);
        Assert.Contains("package main", script.Code);
        Assert.Contains("import (", script.Code);
        Assert.Contains("\"fmt\"", script.Code);
        Assert.Contains("func check(", script.Code);
        Assert.Contains("func main()", script.Code);
    }
}
