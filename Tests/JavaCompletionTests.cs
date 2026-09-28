using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class JavaCompletionTests
{
    private readonly JavaCompletionService _service = new();
    private readonly EditorAssistantContext _context = new();

    [Fact]
    public async Task DotMemberAccess_SystemOut_ReturnsPrintlnAndPrint()
    {
        const string code = "System.out.";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.DisplayText == "println");
        Assert.Contains(completions, c => c.DisplayText == "print");
        Assert.Contains(completions, c => c.DisplayText == "printf");
        Assert.Contains(completions, c => c.DisplayText == "flush");
    }

    [Fact]
    public async Task DotMemberAccess_Math_ReturnsMathStaticMethods()
    {
        const string code = "Math.";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.DisplayText == "max");
        Assert.Contains(completions, c => c.DisplayText == "min");
        Assert.Contains(completions, c => c.DisplayText == "sqrt");
        Assert.Contains(completions, c => c.DisplayText == "pow");
        Assert.Contains(completions, c => c.DisplayText == "PI");
    }

    [Fact]
    public async Task ScopeCompletions_PrefixPub_ReturnsPublicKeyword()
    {
        const string code = "pub";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        var pubItem = completions.FirstOrDefault(c => c.DisplayText == "public");
        Assert.NotNull(pubItem);
        Assert.Equal(CompletionItemKind.Keyword, pubItem.Kind);
    }

    [Fact]
    public async Task ScopeCompletions_PrefixSout_ReturnsSoutSnippet()
    {
        const string code = "sout";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        var soutItem = completions.FirstOrDefault(c => c.DisplayText == "sout");
        Assert.NotNull(soutItem);
        Assert.Equal(CompletionItemKind.Snippet, soutItem.Kind);
        Assert.Contains("System.out.println", soutItem.InsertionText);
    }

    [Fact]
    public async Task ScopeCompletions_PrefixPsvm_ReturnsPsvmSnippet()
    {
        const string code = "psvm";
        var completions = await _service.GetCompletionsAsync(code, code.Length, _context);

        var psvmItem = completions.FirstOrDefault(c => c.DisplayText == "psvm");
        Assert.NotNull(psvmItem);
        Assert.Equal(CompletionItemKind.Snippet, psvmItem.Kind);
        Assert.Contains("public static void main", psvmItem.InsertionText);
    }

    [Fact]
    public async Task ScopeCompletions_ExtractsDocumentVariablesAndMethods()
    {
        const string code = """
            public class Solution {
                private int maxScore = 99;

                public void calculateAverage() {
                    int totalCount = 10;
                    tot
                }
            }
            """;

        int caret = code.IndexOf("tot") + 3;
        var completions = await _service.GetCompletionsAsync(code, caret, _context);

        var totalCountItem = completions.FirstOrDefault(c => c.DisplayText == "totalCount");
        Assert.NotNull(totalCountItem);
        Assert.Equal(CompletionItemKind.Variable, totalCountItem.Kind);

        // Also test method completion
        caret = code.IndexOf("calculate") + 4; // "calc"
        completions = await _service.GetCompletionsAsync(code, caret, _context);
        var calcMethod = completions.FirstOrDefault(c => c.DisplayText == "calculateAverage");
        Assert.NotNull(calcMethod);
        Assert.Equal(CompletionItemKind.Method, calcMethod.Kind);
    }
}
