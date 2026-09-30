using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>What the Search panel's matcher finds, and how "Find in Files" reads a workspace.</summary>
public class TextMatcherTests
{
    private static List<(int Line, int Column, int Length, string Text)> Scan(TextMatcher matcher, string text)
    {
        var found = new List<(int, int, int, string)>();
        matcher.Scan(text, (line, column, length, lineText) => found.Add((line, column, length, lineText.ToString())));
        return found;
    }

    [Fact]
    public void Plain_IgnoresCaseUnlessAsked()
    {
        var insensitive = Scan(new TextMatcher("foo", matchCase: false, wholeWord: false, useRegex: false), "Foo foo FOO");
        var sensitive = Scan(new TextMatcher("foo", matchCase: true, wholeWord: false, useRegex: false), "Foo foo FOO");

        Assert.Equal(new[] { 1, 5, 9 }, insensitive.Select(m => m.Column));
        Assert.Equal(new[] { 5 }, sensitive.Select(m => m.Column));
    }

    [Fact]
    public void WholeWord_SkipsWordsThatOnlyContainTheQuery()
    {
        var found = Scan(new TextMatcher("foo", matchCase: false, wholeWord: true, useRegex: false), "foo foobar barfoo foo_1 (foo)");

        Assert.Equal(new[] { 1, 26 }, found.Select(m => m.Column));
    }

    [Fact]
    public void WholeWord_OfAQueryThatStartsWithAPunctuationMark_OnlyChecksItsWordEnd()
    {
        var found = Scan(new TextMatcher(".Length", matchCase: true, wholeWord: true, useRegex: false), "items.Length + items.LengthOf");

        Assert.Equal(new[] { 6 }, found.Select(m => m.Column));
    }

    [Fact]
    public void Regex_FindsPatterns_AndIgnoresEmptyMatches()
    {
        var numbers = Scan(new TextMatcher(@"\d+", matchCase: false, wholeWord: false, useRegex: true), "a1 b22 c333");
        var empty = Scan(new TextMatcher("x*", matchCase: false, wholeWord: false, useRegex: true), "abc");

        Assert.Equal(new[] { (1, 2, 1), (1, 5, 2), (1, 9, 3) }, numbers.Select(m => (m.Line, m.Column, m.Length)));
        Assert.Empty(empty);
    }

    [Fact]
    public void Regex_WholeWord_WrapsThePatternInWordBoundaries()
    {
        var found = Scan(new TextMatcher("fo+", matchCase: false, wholeWord: true, useRegex: true), "fo foo xfoo fooo");

        Assert.Equal(new[] { 1, 4, 13 }, found.Select(m => m.Column));
    }

    [Fact]
    public void Regex_ThatDoesNotParse_ReportsWhy()
    {
        var matcher = new TextMatcher("(unclosed", matchCase: false, wholeWord: false, useRegex: true);

        Assert.False(matcher.IsValid);
        Assert.NotNull(matcher.Error);
        Assert.Empty(Scan(matcher, "(unclosed"));
    }

    [Fact]
    public void Lines_EndAtTheLineBreak_WhichIsNotPartOfThem()
    {
        var found = Scan(new TextMatcher("foo", matchCase: false, wholeWord: false, useRegex: false), "a\r\nfoo\r\nbfoo");

        Assert.Equal(new[] { (2, 1, "foo"), (3, 2, "bfoo") }, found.Select(m => (m.Line, m.Column, m.Text)));
    }

    [Fact]
    public void Replace_CountsAndHonoursTheOptions()
    {
        var plain = new TextMatcher("foo", matchCase: false, wholeWord: false, useRegex: false);
        var word = new TextMatcher("foo", matchCase: false, wholeWord: true, useRegex: false);

        Assert.Equal("bar(1); bar(2);", plain.Replace("foo(1); FOO(2);", "bar", out var all));
        Assert.Equal(2, all);
        Assert.Equal("bar(1); foo(2);", plain.Replace("foo(1); foo(2);", "bar", out var one, limit: 1));
        Assert.Equal(1, one);
        Assert.Equal("x foobar", word.Replace("foo foobar", "x", out _));
        Assert.Equal("unchanged", plain.Replace("unchanged", "bar", out var none));
        Assert.Equal(0, none);
    }

    [Fact]
    public void Replace_KeepsLineBreaks_AndUsesGroupsInRegexMode()
    {
        var plain = new TextMatcher("foo", matchCase: false, wholeWord: false, useRegex: false);
        var regex = new TextMatcher(@"(\w+)=(\d+)", matchCase: false, wholeWord: false, useRegex: true);

        Assert.Equal("a\r\nbar\r\nb\nbar\n", plain.Replace("a\r\nfoo\r\nb\nfoo\n", "bar", out _));
        Assert.Equal("1=a\n2=b", regex.Replace("a=1\nb=2", "$2=$1", out _));
    }
}

public class WorkspaceTextSearchTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_FindInFilesTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public WorkspaceTextSearchTests()
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

    private string Root => _storage.ActiveWorkspaceRootPath;

    private void WriteFile(string relativePath, string text)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static TextMatcher Plain(string query) => new(query, matchCase: false, wholeWord: false, useRegex: false);

    private static async Task<(List<FileHits> Files, WorkspaceSearchSummary Summary)> Search(
        IReadOnlyList<string> paths, string root, TextMatcher matcher,
        IReadOnlyDictionary<string, string>? open = null, int maxMatches = WorkspaceTextSearch.MaxMatches)
    {
        var files = new List<FileHits>();
        var summary = await WorkspaceTextSearch.RunAsync(paths, root, matcher, open, files.Add, CancellationToken.None, maxMatches: maxMatches);
        return (files, summary);
    }

    [Fact]
    public async Task FindsMatchesInScripts_SourceFiles_AndNotebookCells_InFileListOrder()
    {
        var script = await _storage.CreateNewScriptAsync("Alpha");
        script.Code = "var one = 1;\nConsole.WriteLine(\"needle in a script\");\n";
        await _storage.SaveScriptAsync(script);
        var notebook = await _storage.CreateNewNotebookAsync("Beta");
        notebook.Cells.Clear();
        notebook.Cells.Add(new NotebookCellItem { Source = "// nothing here" });
        notebook.Cells.Add(new NotebookCellItem { Source = "var x = 1;\nvar y = \"NEEDLE in cell two\";" });
        await _storage.SaveNotebookAsync(notebook);
        WriteFile("sub/helper.py", "print('hello')\n# a needle in python\n");

        var paths = new[] { "sub/helper.py", "Alpha.frycs", "Beta.frynb" };
        var (files, summary) = await Search(paths, Root, Plain("needle"));

        Assert.Equal(paths, files.Select(f => f.RelativePath));
        Assert.Equal(3, summary.FilesWithMatches);
        Assert.Equal(3, summary.Matches);
        Assert.Equal(3, summary.FilesSearched);

        var python = files[0].Hits.Single();
        Assert.Equal((2, 5, 6, 0), (python.Line, python.Column, python.Length, python.Cell));
        Assert.Equal("# a needle in python", python.Text);

        var inScript = files[1].Hits.Single();
        Assert.Equal((2, 0), (inScript.Line, inScript.Cell));

        var inCell = files[2].Hits.Single();
        Assert.Equal((2, 2), (inCell.Line, inCell.Cell));
    }

    [Fact]
    public async Task SkipsBinaryFiles_AndFilesThatAreGone()
    {
        File.WriteAllBytes(Path.Combine(Root, "blob.py"), new byte[] { 0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0x00, 0x01, 0x02 });
        WriteFile("real.py", "needle\n");

        var (files, summary) = await Search(new[] { "blob.py", "vanished.py", "real.py" }, Root, Plain("needle"));

        Assert.Equal(new[] { "real.py" }, files.Select(f => f.RelativePath));
        Assert.Equal(3, summary.FilesSearched);
    }

    [Fact]
    public async Task StopsAtTheLimitOfMatches_AndSaysSo()
    {
        for (int i = 0; i < 12; i++) WriteFile($"many{i:D2}.py", string.Concat(Enumerable.Repeat("needle\n", 100)));
        var paths = Enumerable.Range(0, 12).Select(i => $"many{i:D2}.py").ToList();

        var (files, summary) = await Search(paths, Root, Plain("needle"), maxMatches: 250);

        Assert.True(summary.HitLimit);
        Assert.Equal(250, summary.Matches);
        Assert.Equal(250, files.Sum(f => f.Hits.Count));
        Assert.Equal(3, files.Count);
    }

    [Fact]
    public async Task OneFile_ListsNoMoreThanItsShare()
    {
        WriteFile("generated.py", string.Concat(Enumerable.Repeat("needle\n", WorkspaceTextSearch.MaxMatchesPerFile + 300)));

        var (files, summary) = await Search(new[] { "generated.py" }, Root, Plain("needle"));

        Assert.Equal(WorkspaceTextSearch.MaxMatchesPerFile, files.Single().Hits.Count);
        Assert.False(summary.HitLimit);
    }

    [Fact]
    public async Task OpenDocuments_AreSearchedAsTheEditorHoldsThem_NotAsSaved()
    {
        var script = await _storage.CreateNewScriptAsync("Alpha");
        script.Code = "// on disk: oldword\n";
        await _storage.SaveScriptAsync(script);
        var open = new Dictionary<string, string> { [script.Id] = "// in the editor: newword\n" };

        var (foundNew, _) = await Search(new[] { "Alpha.frycs" }, Root, Plain("newword"), open);
        var (foundOld, _) = await Search(new[] { "Alpha.frycs" }, Root, Plain("oldword"), open);

        Assert.Single(foundNew);
        Assert.Empty(foundOld);
    }

    [Fact]
    public async Task ALongLine_IsCutAroundTheMatch()
    {
        WriteFile("minified.py", new string('a', 5_000) + "needle" + new string('b', 5_000) + "\n");

        var (files, _) = await Search(new[] { "minified.py" }, Root, Plain("needle"));

        var hit = files.Single().Hits.Single();
        Assert.Equal(5_001, hit.Column);
        Assert.True(hit.Text.Length < 300);
        Assert.Contains("needle", hit.Text);
    }

    [Fact]
    public async Task ACancelledSearch_Throws()
    {
        WriteFile("real.py", "needle\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WorkspaceTextSearch.RunAsync(new[] { "real.py" }, Root, Plain("needle"), null, _ => { }, cts.Token));
    }

    [Fact]
    public async Task TheFileIndexFeedsTheSearch()
    {
        for (int i = 0; i < 40; i++) WriteFile($"pkg{i % 4}/file{i}.py", i % 10 == 0 ? "needle\n" : "nothing\n");

        var index = _storage.FileIndex;
        await WaitUntil(() => !index.IsBuilding && index.Count == 40);
        var (files, summary) = await Search(index.Paths, Root, Plain("needle"));

        Assert.Equal(4, files.Count);
        Assert.Equal(40, summary.FilesSearched);
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

/// <summary>The Search panel of the studio: the open document, and the workspace.</summary>
public class StudioSearchPanelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_SearchPanelTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public StudioSearchPanelTests()
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

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem script) => new(
        script,
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    private async Task<ScriptDocumentItem> SavedScript(string title, string code)
    {
        var script = await _storage.CreateNewScriptAsync(title);
        script.Code = code;
        await _storage.SaveScriptAsync(script);
        return script;
    }

    [Fact]
    public async Task DocumentSearch_HonoursWholeWordAndRegex()
    {
        var studio = CreateStudio(await SavedScript("Alpha", "int total = 1;\nint totals = 2;\nint sub_total = 3;"));

        studio.SearchQuery = "total";
        Assert.Equal(3, studio.SearchMatches.Count);

        studio.SearchWholeWord = true;
        Assert.Equal(new[] { 1 }, studio.SearchMatches.Select(m => m.LineNumber));

        studio.SearchWholeWord = false;
        studio.SearchUseRegex = true;
        studio.SearchQuery = @"\btotal\w*";
        Assert.Equal(new[] { 1, 2 }, studio.SearchMatches.Select(m => m.LineNumber));

        studio.SearchQuery = "(unclosed";
        Assert.Empty(studio.SearchMatches);
        Assert.Equal("Invalid regular expression", studio.SearchStatusText);
    }

    [Fact]
    public async Task Replace_HonoursWholeWord()
    {
        var studio = CreateStudio(await SavedScript("Alpha", "total totals total"));
        studio.SearchWholeWord = true;
        studio.SearchQuery = "total";
        studio.ReplaceQuery = "sum";

        studio.ReplaceAllCommand.Execute(null);

        Assert.Equal("sum totals sum", studio.Code);
    }

    [Fact]
    public async Task WorkspaceSearch_ListsAHeaderPerFileFollowedByItsMatches()
    {
        var alpha = await SavedScript("Alpha", "var a = 1;\n// needle one\n// needle two\n");
        await SavedScript("Beta", "// needle three\n");
        await SavedScript("Gamma", "// nothing at all\n");
        var studio = CreateStudio(alpha);
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 3);

        studio.SearchAllFiles = true;
        studio.SearchQuery = "needle";
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count > 0);

        Assert.Equal("3 results in 2 files", studio.SearchStatusText);
        var headers = studio.SearchMatches.Where(m => m.IsFileHeader).ToList();
        Assert.Equal(new[] { "Alpha.frycs", "Beta.frycs" }, headers.Select(h => h.FilePath).OrderBy(p => p));
        Assert.Equal(new[] { 2, 1 }, headers.OrderBy(h => h.FilePath).Select(h => h.MatchCount));
        Assert.Equal(5, studio.SearchMatches.Count);
        Assert.All(studio.SearchMatches.Where(m => !m.IsFileHeader), m => Assert.NotNull(m.FilePath));
        // Each header is followed by exactly its own matches.
        for (int i = 0; i < studio.SearchMatches.Count; i++)
        {
            if (!studio.SearchMatches[i].IsFileHeader) continue;
            var header = studio.SearchMatches[i];
            Assert.All(studio.SearchMatches.Skip(i + 1).Take(header.MatchCount), m => Assert.Equal(header.FilePath, m.FilePath));
        }
    }

    [Fact]
    public async Task WorkspaceSearch_SeesUnsavedEditsOfTheOpenDocument()
    {
        var alpha = await SavedScript("Alpha", "// saved text\n");
        var studio = CreateStudio(alpha);
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 1);
        studio.Code = "// typed but not saved: zebra\n";

        studio.SearchAllFiles = true;
        studio.SearchQuery = "zebra";
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count > 0);

        Assert.Equal("Alpha.frycs", studio.SearchMatches[0].FilePath);
        Assert.Equal(2, studio.SearchMatches.Count);
    }

    [Fact]
    public async Task WorkspaceSearch_WithNoMatches_SaysSo()
    {
        var studio = CreateStudio(await SavedScript("Alpha", "// text\n"));
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 1);

        studio.SearchAllFiles = true;
        studio.SearchQuery = "absent";
        await WaitUntil(() => !studio.IsSearching);

        Assert.Empty(studio.SearchMatches);
        Assert.Equal("No results in 1 file", studio.SearchStatusText);
        Assert.Equal("No results found.", studio.SearchEmptyText);
    }

    [Fact]
    public async Task TypingAgain_ReplacesTheSearchThatWasWaiting()
    {
        var alpha = await SavedScript("Alpha", "// apple\n");
        await SavedScript("Beta", "// banana\n");
        var studio = CreateStudio(alpha);
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 2);

        studio.SearchAllFiles = true;
        studio.SearchQuery = "apple";
        studio.SearchQuery = "banana";
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count > 0);
        await Task.Delay(400);

        Assert.All(studio.SearchMatches, m => Assert.Equal("Beta.frycs", m.FilePath));
    }

    [Fact]
    public async Task OpeningAnotherFolder_SearchesItForTheSameText()
    {
        var alpha = await SavedScript("Alpha", "// needle in the first workspace\n");
        var studio = CreateStudio(alpha);
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 1);
        studio.SearchAllFiles = true;
        studio.SearchQuery = "needle";
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count == 2);
        Assert.Equal("Alpha.frycs", studio.SearchMatches[0].FilePath);

        var other = Path.Combine(_dir, "other-project");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "elsewhere.py"), "# a needle in the second one\n");
        await _storage.OpenExternalProjectAsync(other);
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count > 0 && studio.SearchMatches[0].FilePath == "elsewhere.py");

        Assert.Equal(2, studio.SearchMatches.Count);
    }

    [Fact]
    public async Task ChoosingAResultInAnotherFile_OpensItAndGoesToTheLine()
    {
        var alpha = await SavedScript("Alpha", "// alpha\n");
        var beta = await SavedScript("Beta", "// first\n// second line has needle\n");
        var studio = CreateStudio(alpha);
        (int Line, int Column)? navigated = null;
        studio.RequestNavigateToCaret += (line, column) => navigated = (line, column);
        await WaitUntil(() => !_storage.FileIndex.IsBuilding && _storage.FileIndex.Count == 2);
        studio.SearchAllFiles = true;
        studio.SearchQuery = "needle";
        await WaitUntil(() => !studio.IsSearching && studio.SearchMatches.Count == 2);

        studio.NavigateToSearchMatchCommand.Execute(studio.SearchMatches.Single(m => !m.IsFileHeader));
        await WaitUntil(() => navigated != null);

        Assert.Equal(beta.Id, studio.Script.Id);
        Assert.Equal((2, 20), navigated);
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
