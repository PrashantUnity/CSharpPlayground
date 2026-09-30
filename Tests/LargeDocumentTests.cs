using System.Text;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>What the editor gives up, and keeps, for a document with tens of thousands of blocks or a couple of megabytes of text.</summary>
public class LargeDocumentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_LargeDocTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public LargeDocumentTests()
    {
        _storage = new LocalScriptStorageService(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // The OS cleans the temp folder eventually.
        }
    }

    [Fact]
    public void Foldings_UnderTheLimit_AreAllKept()
    {
        var foldings = new List<NewFolding> { new(0, 10), new(2, 5), new(20, 30) };

        var kept = FoldingLimits.Cap(foldings, limit: 3).ToList();

        Assert.Equal(foldings, kept);
    }

    [Fact]
    public void Foldings_OverTheLimit_KeepTheOutermostBlocksInDocumentOrder()
    {
        // A(0-100) holds B(1-50) which holds C(2-20), and D(60-90); E(200-300) holds F(210-250) which holds G(215-220).
        var a = new NewFolding(0, 100);
        var b = new NewFolding(1, 50);
        var c = new NewFolding(2, 20);
        var d = new NewFolding(60, 90);
        var e = new NewFolding(200, 300);
        var f = new NewFolding(210, 250);
        var g = new NewFolding(215, 220);

        var kept = FoldingLimits.Cap(new[] { g, c, e, a, f, d, b }, limit: 4).ToList();

        Assert.Equal(new[] { a, b, d, e }, kept);
    }

    [Fact]
    public void Foldings_ManyOutermostBlocks_KeepTheEarliest()
    {
        var blocks = Enumerable.Range(0, 100).Select(i => new NewFolding(i * 10, i * 10 + 5)).ToList();

        var kept = FoldingLimits.Cap(blocks, limit: 10).ToList();

        Assert.Equal(blocks.Take(10), kept);
    }

    [Fact]
    public void CSharpFolding_OfAHugeDocument_StaysWithinTheLimit_AndKeepsTheClass()
    {
        var code = new StringBuilder("class Generated\n{\n");
        for (int i = 0; i < FoldingLimits.MaxFoldings + 1_000; i++) code.Append("    void M").Append(i).Append("()\n    {\n    }\n");
        code.Append("}\n");

        var foldings = new CSharpFoldingStrategy().CreateNewFoldings(new TextDocument(code.ToString()), out _).ToList();

        Assert.Equal(FoldingLimits.MaxFoldings, foldings.Count);
        Assert.Equal(code.ToString().IndexOf('{'), foldings[0].StartOffset);
        Assert.Equal(foldings.OrderBy(x => x.StartOffset).ToList(), foldings);
    }

    [Fact]
    public async Task LiveDiagnostics_AreOffForAVeryLargeFile_AndComeBackWhenItShrinks()
    {
        var script = await _storage.CreateNewScriptAsync("Diagnosed");
        script.Code = "int broken = ;";
        await _storage.SaveScriptAsync(script);
        var studio = new CSharpCodeStudioViewModel(
            script,
            _storage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });

        // A small file is analysed as you type ...
        studio.Code = "int broken = ;\n";
        await WaitUntil(() => studio.ErrorCount > 0);

        // ... a huge one is not, and what was found before no longer describes it.
        studio.Code = string.Concat(Enumerable.Repeat("// filler line of a generated file\n", CSharpCodeStudioViewModel.LargeDocumentLength / 30));
        Assert.True(studio.Code.Length > CSharpCodeStudioViewModel.LargeDocumentLength);
        Assert.Equal(CSharpCodeStudioViewModel.LargeDocumentStatus, studio.CompilerStatusText);
        Assert.Equal(0, studio.ErrorCount);
        Assert.Empty(studio.Diagnostics);
        await Task.Delay(1_000);
        Assert.Equal(CSharpCodeStudioViewModel.LargeDocumentStatus, studio.CompilerStatusText);

        // Cutting it down brings the analysis back.
        studio.Code = "System.Console.WriteLine(1);\n";
        await WaitUntil(() => studio.CompilerStatusText == "Ready");
        Assert.Equal(0, studio.ErrorCount);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the condition never became true");
            await Task.Delay(25);
        }
    }
}
