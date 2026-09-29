using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoCompletionAndQuickInfoTests
{
    private readonly GoCompletionService _completion = new();
    private readonly EditorAssistantContext _context = new();

    [Fact]
    public async Task Completion_OffersPackageMembersAfterDot()
    {
        var code = "fmt.";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 4, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "Println");
        Assert.Contains(items, i => i.DisplayText == "Printf");
        Assert.Contains(items, i => i.DisplayText == "Sprintf");
    }

    [Fact]
    public async Task Completion_OffersStringsPackageMembers()
    {
        var code = "strings.Co";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 10, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "Contains");
        Assert.Contains(items, i => i.DisplayText == "Count");
    }

    [Fact]
    public async Task Completion_OffersKeywordsAndTypesInGeneralScope()
    {
        var code = "f";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 1, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "func");
        Assert.Contains(items, i => i.DisplayText == "for");
        Assert.Contains(items, i => i.DisplayText == "float64");
    }

    [Fact]
    public void QuickInfo_FindsBuiltinFunctions()
    {
        var info = GoQuickInfoProvider.Lookup("append");
        Assert.NotNull(info);
        Assert.Equal("builtin", info.Package);
        Assert.Contains("slice []Type", info.Signature);

        var lenInfo = GoQuickInfoProvider.Lookup("len");
        Assert.NotNull(lenInfo);
        Assert.Contains("Returns the length", lenInfo.Summary);
    }

    [Fact]
    public void QuickInfo_FindsQualifiedMembers()
    {
        var info = GoQuickInfoProvider.Lookup("fmt.Println");
        Assert.NotNull(info);
        Assert.Equal("fmt", info.Package);
        Assert.Contains("func Println", info.Signature);
    }

    [Fact]
    public void QuickInfo_ExtractsSymbolFromCode()
    {
        var code = "fmt.Println(\"hello\")";
        var symbol = GoQuickInfoProvider.ExtractSymbol(code, 6); // inside Println

        Assert.Equal("fmt.Println", symbol);
    }
}
