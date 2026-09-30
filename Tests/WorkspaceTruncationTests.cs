using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>A folder with more files than the Explorer lists must be reported as cut off, never silently look complete.</summary>
public class WorkspaceTruncationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_TruncationTests_" + Guid.NewGuid().ToString("N"));

    public WorkspaceTruncationTests()
    {
        Directory.CreateDirectory(_dir);
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

    private string Folder(string name)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Files(string folder, int count)
    {
        for (var i = 0; i < count; i++) File.WriteAllText(Path.Combine(folder, $"file{i:D3}.py"), "print(1)\n");
    }

    [Fact]
    public void AFolderWithFewerFilesThanTheLimit_IsListedInFull_AndNotTruncated()
    {
        var root = Folder("small");
        Files(root, 8);

        var files = WorkspaceWalker.Files(root, _ => true, out var truncated, maxFiles: 10);

        Assert.Equal(8, files.Count);
        Assert.False(truncated);
    }

    [Fact]
    public void AFolderWithExactlyTheLimit_IsNotReportedAsTruncated()
    {
        var root = Folder("exact");
        Files(root, 10);

        var files = WorkspaceWalker.Files(root, _ => true, out var truncated, maxFiles: 10);

        Assert.Equal(10, files.Count);
        Assert.False(truncated);
    }

    [Fact]
    public void AFolderWithMoreFilesThanTheLimit_IsCutAtTheLimit_AndSaysSo()
    {
        var root = Folder("big");
        Files(root, 25);

        var files = WorkspaceWalker.Files(root, _ => true, out var truncated, maxFiles: 10);

        Assert.Equal(10, files.Count);
        Assert.True(truncated);
    }

    [Fact]
    public void AFolderWithMoreFoldersThanTheLimit_SaysSo()
    {
        var root = Folder("wide");
        for (var i = 0; i < 12; i++) Directory.CreateDirectory(Path.Combine(root, $"sub{i:D2}"));

        var folders = WorkspaceWalker.Folders(root, out var truncated, maxFolders: 5);

        Assert.True(truncated);
        Assert.True(folders.Count <= 6);
    }

    [Fact]
    public void AFolderWithFewFolders_IsNotTruncated()
    {
        var root = Folder("narrow");
        for (var i = 0; i < 3; i++) Directory.CreateDirectory(Path.Combine(root, $"sub{i}"));

        WorkspaceWalker.Folders(root, out var truncated, maxFolders: 5);

        Assert.False(truncated);
    }

    [Fact]
    public async Task TheStorage_ReportsATruncatedWorkspace_OnlyWhenTheListingWasCut()
    {
        var storage = new LocalScriptStorageService(Path.Combine(_dir, "storage"), workspaceFileLimit: 10);
        Files(storage.LibraryRootPath, 4);

        await storage.LoadWorkspaceSummariesAsync();
        Assert.False(storage.IsWorkspaceTruncated);

        Files(storage.LibraryRootPath, 25);
        var summaries = await storage.LoadWorkspaceSummariesAsync();

        Assert.True(storage.IsWorkspaceTruncated);
        Assert.Equal(10, summaries.Count);
        Assert.Equal(10, storage.WorkspaceFileLimit);
    }

    [Fact]
    public async Task TheStudiosExplorer_KnowsWhenItIsShowingACutOffFolder()
    {
        var storage = new LocalScriptStorageService(Path.Combine(_dir, "studio"), workspaceFileLimit: 10);
        Files(storage.LibraryRootPath, 25);
        var script = new Models.ScriptDocumentItem { Title = "Open" };
        var studio = new CSharpCodeStudioViewModel(
            script,
            storage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });

        await studio.RefreshExplorerAsync();

        Assert.True(studio.IsExplorerTruncated);
        Assert.Contains("10", studio.ExplorerTruncationText);
    }
}
