using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.FSharp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class FSharpProblemLanguageAdapterTests
{
    [Fact]
    public void Adapter_IsRegisteredInProblemLanguageRegistry()
    {
        var adapter = ProblemLanguageRegistry.GetAdapter(LanguageIds.FSharp);
        Assert.NotNull(adapter);
        Assert.Equal("fsharp", adapter.LanguageId);
        Assert.Equal(".fsx", adapter.DefaultFileExtension);
    }

    [Fact]
    public void BuildScript_GeneratesValidFSharpScriptWithHarness()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1); // Two Sum
        Assert.NotNull(problem);

        var adapter = FSharpProblemLanguageAdapter.Instance;
        var script = adapter.BuildScript(problem);

        Assert.NotNull(script);
        Assert.Equal(LanguageIds.FSharp, script.LanguageId);
        Assert.EndsWith(".fsx", script.Title);
        Assert.Contains("open System", script.Code);
        Assert.Contains("let check", script.Code);
        Assert.Contains("printfn", script.Code);
    }
}
