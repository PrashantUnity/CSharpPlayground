using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Cpp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CppProblemLanguageAdapterTests
{
    [Fact]
    public void Adapter_IsRegisteredInProblemLanguageRegistry()
    {
        var adapter = ProblemLanguageRegistry.GetAdapter(LanguageIds.Cpp);
        Assert.NotNull(adapter);
        Assert.Equal("cpp", adapter.LanguageId);
        Assert.Equal(".cpp", adapter.DefaultFileExtension);
    }

    [Fact]
    public void BuildScript_GeneratesValidCppScriptWithHeadersAndHarness()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1); // Two Sum
        Assert.NotNull(problem);

        var adapter = CppProblemLanguageAdapter.Instance;
        var script = adapter.BuildScript(problem);

        Assert.NotNull(script);
        Assert.Equal(LanguageIds.Cpp, script.LanguageId);
        Assert.EndsWith(".cpp", script.Title);
        Assert.Contains("#include <iostream>", script.Code);
        Assert.Contains("#include <vector>", script.Code);
        Assert.Contains("class Solution", script.Code);
        Assert.Contains("check(", script.Code);
        Assert.Contains("int main()", script.Code);
    }

    [Fact]
    public void TranspileCallToCpp_ConvertsCSharpArraysToCppInitializerLists()
    {
        const string csharpCall = "sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)";
        var cppCall = ProblemCodeTranspiler.TranspileCallToCpp(csharpCall);

        Assert.Equal("sol.twoSum({ 2, 7, 11, 15 }, 9)", cppCall);
    }

    [Fact]
    public void BuildScript_IncludesLinkedListSupport_ForLinkedListProblem()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(21); // Merge Two Sorted Lists (Linked List)
        if (problem != null)
        {
            var script = CppProblemLanguageAdapter.Instance.BuildScript(problem);
            Assert.Contains("struct ListNode", script.Code);
            Assert.Contains("buildList", script.Code);
        }
    }
}
