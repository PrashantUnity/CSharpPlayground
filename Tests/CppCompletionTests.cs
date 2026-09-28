using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CppCompletionTests
{
    private readonly CppCompletionService _service = new();
    private readonly EditorAssistantContext _context = new();

    [Fact]
    public async Task ScopeResolution_StdColonColon_ReturnsStandardLibraryMembers()
    {
        const string code = "std::";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.DisplayText == "vector");
        Assert.Contains(completions, c => c.DisplayText == "string");
        Assert.Contains(completions, c => c.DisplayText == "cout");
        Assert.Contains(completions, c => c.DisplayText == "cin");
        Assert.Contains(completions, c => c.DisplayText == "endl");
        Assert.Contains(completions, c => c.DisplayText == "make_unique");
        Assert.Contains(completions, c => c.DisplayText == "make_shared");
    }

    [Fact]
    public async Task ScopeResolution_StdWithPrefix_FiltersMembers()
    {
        const string code = "std::vec";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        var vectorItem = completions.FirstOrDefault(c => c.DisplayText == "vector");
        Assert.NotNull(vectorItem);
        Assert.DoesNotContain(completions, c => c.DisplayText == "cout");
    }

    [Fact]
    public async Task ArrowPointerAccess_ReturnsCommonMemberFunctions()
    {
        const string code = "ptr->";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.DisplayText == "size");
        Assert.Contains(completions, c => c.DisplayText == "empty");
        Assert.Contains(completions, c => c.DisplayText == "push_back");
        Assert.Contains(completions, c => c.DisplayText == "get");
        Assert.Contains(completions, c => c.DisplayText == "reset");
    }

    [Fact]
    public async Task DotMemberAccess_ReturnsCommonMemberFunctions()
    {
        const string code = "vec.";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.DisplayText == "size");
        Assert.Contains(completions, c => c.DisplayText == "empty");
        Assert.Contains(completions, c => c.DisplayText == "push_back");
        Assert.Contains(completions, c => c.DisplayText == "begin");
        Assert.Contains(completions, c => c.DisplayText == "end");
    }

    [Fact]
    public async Task ScopeCompletions_PrefixCon_ReturnsConstexprAndConcept()
    {
        const string code = "con";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.Contains(completions, c => c.DisplayText == "constexpr" && c.Kind == CompletionItemKind.Keyword);
        Assert.Contains(completions, c => c.DisplayText == "consteval" && c.Kind == CompletionItemKind.Keyword);
        Assert.Contains(completions, c => c.DisplayText == "concept" && c.Kind == CompletionItemKind.Keyword);
        Assert.Contains(completions, c => c.DisplayText == "const" && c.Kind == CompletionItemKind.Keyword);
    }

    [Fact]
    public async Task ScopeCompletions_PrefixMain_ReturnsMainSnippet()
    {
        const string code = "main";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        var mainItem = completions.FirstOrDefault(c => c.DisplayText == "main");
        Assert.NotNull(mainItem);
        Assert.Equal(CompletionItemKind.Snippet, mainItem.Kind);
        Assert.Contains("int main()", mainItem.InsertionText);
    }

    [Fact]
    public async Task ScopeCompletions_PrefixCout_ReturnsCoutSnippet()
    {
        const string code = "cout";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        var coutSnippet = completions.FirstOrDefault(c => c.DisplayText == "cout");
        Assert.NotNull(coutSnippet);
        Assert.Equal(CompletionItemKind.Snippet, coutSnippet.Kind);
        Assert.Contains("std::cout <<", coutSnippet.InsertionText);
    }

    [Fact]
    public async Task ScopeCompletions_ExtractsDocumentVariablesAndFunctions()
    {
        const string code = """
            #include <iostream>

            void processTelemetryData(int packetId) {
                int totalPackets = 42;
                tot
            }
            """;

        int caret = code.IndexOf("tot") + 3;
        var completions = await _service.GetCompletionsAsync(code, caret, _context);

        var totalPacketsItem = completions.FirstOrDefault(c => c.DisplayText == "totalPackets");
        Assert.NotNull(totalPacketsItem);
        Assert.Equal(CompletionItemKind.Variable, totalPacketsItem.Kind);

        // Also test function completion
        caret = code.IndexOf("process") + 4; // "proc"
        completions = await _service.GetCompletionsAsync(code, caret, _context);
        var procFunc = completions.FirstOrDefault(c => c.DisplayText == "processTelemetryData");
        Assert.NotNull(procFunc);
        Assert.Equal(CompletionItemKind.Method, procFunc.Kind);
    }
}
