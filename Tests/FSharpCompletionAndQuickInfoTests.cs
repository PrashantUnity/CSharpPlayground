using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class FSharpCompletionAndQuickInfoTests
{
    private readonly FSharpCompletionService _completion = new();
    private readonly EditorAssistantContext _context = new();

    [Fact]
    public async Task Completion_OffersListModuleMembersAfterDot()
    {
        var code = "List.";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 5, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "map");
        Assert.Contains(items, i => i.DisplayText == "filter");
        Assert.Contains(items, i => i.DisplayText == "fold");
        Assert.Contains(items, i => i.DisplayText == "length");
    }

    [Fact]
    public async Task Completion_OffersDisplayModuleMembers()
    {
        var code = "Display.";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 8, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "Dump");
        Assert.Contains(items, i => i.DisplayText == "Html");
        Assert.Contains(items, i => i.DisplayText == "Image");
    }

    [Fact]
    public async Task Completion_OffersKeywordsAndCoreFunctionsInGeneralScope()
    {
        var code = "pr";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 2, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "printfn");
    }

    [Fact]
    public void QuickInfo_FindsCoreFSharpFunctions()
    {
        var info = FSharpQuickInfoProvider.Lookup("printfn");
        Assert.NotNull(info);
        Assert.Contains("TextWriterFormat", info.Signature);
        Assert.Contains("stdout", info.Summary);

        var listMap = FSharpQuickInfoProvider.Lookup("List.map");
        Assert.NotNull(listMap);
        Assert.Equal("Microsoft.FSharp.Collections.List", listMap.Module);
        Assert.Contains("'T list -> 'U list", listMap.Signature);
    }

    [Fact]
    public void QuickInfo_FindsDisplayDumpHelper()
    {
        var info = FSharpQuickInfoProvider.Lookup("Display.Dump");
        Assert.NotNull(info);
        Assert.Equal("Fry.Display", info.Module);
        Assert.Contains("Display.Dump", info.Signature);
    }
}
