using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class WorkspaceFileIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryPDF_IndexTests_" + Guid.NewGuid().ToString("N"));

    public WorkspaceFileIndexTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // The OS cleans the temp folder eventually.
        }
    }

    private void Touch(string relative, string? root = null)
    {
        var path = Path.Combine(root ?? _root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private static bool Wanted(string path) =>
        path.EndsWith(".py", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private async Task<WorkspaceFileIndex> IndexedAsync(int limit = 250_000)
    {
        var index = new WorkspaceFileIndex(Wanted, limit);
        await index.RebuildAsync(_root);
        return index;
    }

    [Fact]
    public async Task ItHoldsEveryWantedFile_AndSkipsTheFoldersTheExplorerSkips()
    {
        Touch("src/App.cs");
        Touch("src/util/helpers.py");
        Touch("docs/readme.md");
        Touch("notes.txt"); // Not a file the studio opens.
        Touch(".git/hooks/pre-commit.py");
        Touch("node_modules/pkg/index.py");

        var index = await IndexedAsync();

        Assert.Equal(3, index.Count);
        Assert.False(index.IsBuilding);
        Assert.Equal("src/util/helpers.py", Assert.Single(index.Find("helpers")).RelativePath);
        Assert.Empty(index.Find("pre-commit"));
        Assert.Empty(index.Find("notes"));
    }

    [Fact]
    public async Task MatchesAreRankedExactThenPrefixThenSubstringThenLettersInOrderThenPath()
    {
        Touch("readme.md");
        Touch("docs/notes/readme.md.py");
        Touch("util.py");
        Touch("utility.py");
        Touch("my_util.py");
        Touch("u_t_i_l.py");
        Touch("util/other.md");
        var index = await IndexedAsync();

        var found = index.Find("util").Select(m => m.RelativePath).ToList();

        Assert.Equal(new[] { "util.py", "utility.py", "my_util.py", "u_t_i_l.py", "util/other.md" }, found);
        Assert.Equal("readme.md", index.Find("readme.md")[0].RelativePath); // The exact name beats a longer one that contains it.
    }

    [Fact]
    public async Task TiesGoToTheShorterPath()
    {
        Touch("a/b/c/main.py");
        Touch("main.py");
        Touch("a/main.py");
        var index = await IndexedAsync();

        var found = index.Find("main.py").Select(m => m.RelativePath).ToList();

        Assert.Equal(new[] { "main.py", "a/main.py", "a/b/c/main.py" }, found);
    }

    [Fact]
    public async Task MatchingIgnoresCase_AndAFolderInTheQueryNarrowsToPaths()
    {
        Touch("src/App.cs");
        Touch("src/models/User.cs");
        Touch("tests/models/UserTests.cs");
        var index = await IndexedAsync();

        Assert.Equal("src/App.cs", Assert.Single(index.Find("APP.CS")).RelativePath);
        Assert.Equal("src/models/User.cs", index.Find("src/models/user")[0].RelativePath);
        Assert.Equal("src/models/User.cs", index.Find("src\\models\\user")[0].RelativePath);
        Assert.All(index.Find("models/"), m => Assert.Contains("models/", m.RelativePath));
    }

    [Fact]
    public async Task LettersInOrderFindAName_LikeAnIdesFuzzyOpen()
    {
        Touch("WorkspaceFileIndex.cs");
        Touch("Unrelated.cs");
        var index = await IndexedAsync();

        Assert.Equal("WorkspaceFileIndex.cs", Assert.Single(index.Find("wsfi")).Name);
    }

    [Fact]
    public async Task NothingTyped_OrNoLimit_FindsNothing()
    {
        Touch("a.py");
        var index = await IndexedAsync();

        Assert.Empty(index.Find(string.Empty));
        Assert.Empty(index.Find("   "));
        Assert.Empty(index.Find("a", limit: 0));
    }

    [Fact]
    public async Task TheResultsAreLimited_ToTheBestOnes()
    {
        for (var i = 0; i < 60; i++) Touch($"a{i:D2}.py");
        var index = await IndexedAsync();

        var found = index.Find("a", limit: 10);

        Assert.Equal(10, found.Count);
    }

    [Fact]
    public async Task ABiggerWorkspaceThanTheLimit_IsCutAndSaysSo()
    {
        for (var i = 0; i < 25; i++) Touch($"f{i:D2}.py");

        var index = await IndexedAsync(limit: 10);

        Assert.Equal(10, index.Count);
        Assert.True(index.IsTruncated);
    }

    [Fact]
    public async Task ExactlyTheLimit_IsNotCut()
    {
        for (var i = 0; i < 10; i++) Touch($"f{i:D2}.py");

        var index = await IndexedAsync(limit: 10);

        Assert.Equal(10, index.Count);
        Assert.False(index.IsTruncated);
    }

    [Fact]
    public async Task RebuildingSeesFilesAddedSince_AndAnotherFolderStartsFromItsOwnFiles()
    {
        Touch("first.py");
        var index = await IndexedAsync();
        Assert.Single(index.Find("first"));

        Touch("second.py");
        await index.RebuildAsync(_root);
        Assert.Single(index.Find("second"));

        var other = Path.Combine(_root, "elsewhere");
        Touch("third.py", other);
        await index.RebuildAsync(other);
        Assert.Equal(other, index.Root);
        Assert.Single(index.Find("third"));
        Assert.Empty(index.Find("first")); // The old folder's files are not this folder's.
    }

    [Fact]
    public async Task ItSaysWhenAWalkHasFinished()
    {
        Touch("one.py");
        var index = new WorkspaceFileIndex(Wanted);
        var updates = 0;
        index.Updated += () => Interlocked.Increment(ref updates);

        await index.RebuildAsync(_root);

        Assert.True(updates >= 1);
        Assert.False(index.IsBuilding);
    }

    [Fact]
    public async Task ASearchDuringAWalk_SeesTheFilesFoundSoFar()
    {
        // Enough files that the walk publishes before it ends.
        for (var i = 0; i < 6_000; i++) Touch($"bulk/f{i:D5}.py");
        var index = new WorkspaceFileIndex(Wanted);
        var sawPartial = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        index.Updated += () =>
        {
            if (index.IsBuilding && index.Count > 0) sawPartial.TrySetResult(index.Count);
        };

        var build = index.RebuildAsync(_root);
        await build;

        // Either a partial list was published on the way, or the walk was too quick to; the final list is complete either way.
        Assert.Equal(6_000, index.Count);
        if (sawPartial.Task.IsCompleted) Assert.InRange(await sawPartial.Task, 1, 6_000);
    }
}

public class GoToFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_GoToFileTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public GoToFileTests()
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

    private CSharpCodeStudioViewModel CreateStudio() => new(
        new Models.ScriptDocumentItem { Title = "Untitled" },
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    private void WriteFile(string relative, string content)
    {
        var path = Path.Combine(_storage.LibraryRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public async Task QuickOpen_FindsAFileThatIsNotOpen_AndOpensTheOneChosen()
    {
        WriteFile("tools/helpers.py", "def helper():\n    pass\n");
        var studio = CreateStudio();
        await _storage.FileIndex.RebuildAsync(_storage.ActiveWorkspaceRootPath); // Wait for a whole walk.

        studio.QuickOpen.Show();
        studio.QuickOpen.SearchText = "helpers";

        var item = Assert.Single(studio.QuickOpen.FilteredItems, i => i.Title == "helpers.py");
        Assert.Equal("tools", item.Subtitle);
        Assert.Equal("Files", item.Category);

        await studio.QuickOpen.ExecuteSelectedAsync();

        Assert.EndsWith("helpers.py", studio.Script.SourceFilePath);
        Assert.Contains(studio.OpenTabs, t => t.Title.Contains("helpers"));
        Assert.Contains("def helper", studio.Code);
    }

    [Fact]
    public async Task TheCommandAndLineModes_DoNotSearchFiles()
    {
        WriteFile("helpers.py", "x = 1\n");
        var studio = CreateStudio();
        await _storage.FileIndex.RebuildAsync(_storage.ActiveWorkspaceRootPath);
        var asked = 0;
        var search = studio.QuickOpen.FileSearch!;
        studio.QuickOpen.FileSearch = q =>
        {
            asked++;
            return search(q);
        };

        studio.QuickOpen.Show();
        studio.QuickOpen.SearchText = ">helpers";
        studio.QuickOpen.SearchText = ":12";
        Assert.Equal(0, asked);

        studio.QuickOpen.SearchText = "helpers";
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task NothingTyped_ListsNoFiles_JustTheOpenTabs()
    {
        WriteFile("helpers.py", "x = 1\n");
        var studio = CreateStudio();
        await _storage.FileIndex.RebuildAsync(_storage.ActiveWorkspaceRootPath);

        studio.QuickOpen.Show();

        Assert.DoesNotContain(studio.QuickOpen.FilteredItems, i => i.Category.StartsWith("Files"));
    }

    [Fact]
    public async Task AFileCreatedAfterTheIndexWasBuilt_IsFoundOnceTheWorkspaceChanged()
    {
        var studio = CreateStudio();
        await _storage.FileIndex.RebuildAsync(_storage.ActiveWorkspaceRootPath);
        Assert.Empty(studio.QuickOpen.FileSearch!("brandnew"));

        await _storage.CreateNewSourceFileAsync("python", "brandnew.py");
        // Reading FileIndex again notices the change and walks again in the background; wait for that walk.
        await _storage.FileIndex.RebuildAsync(_storage.ActiveWorkspaceRootPath);

        Assert.Contains(studio.QuickOpen.FileSearch!("brandnew"), i => i.Title == "brandnew.py");
    }
}
