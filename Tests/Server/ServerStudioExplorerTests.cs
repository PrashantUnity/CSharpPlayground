using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The Server Studio has the same Explorer as the other studios: the workspace folder as VS Code shows it, files by their
/// own names, and new / rename / duplicate / delete working on the disk. Renaming a document renames its file (it only
/// changed the title inside before), a copy is a document of its own, and deleting a server document deletes it (it did
/// nothing).
/// </summary>
public class ServerStudioExplorerTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "FryPDF_ServerExplorer_" + Guid.NewGuid().ToString("N"));
    private readonly string _workspace;
    private readonly LocalScriptStorageService _storage;

    public ServerStudioExplorerTests()
    {
        _workspace = Path.Combine(_base, "workspace");
        Directory.CreateDirectory(Path.Combine(_workspace, "api"));
        Directory.CreateDirectory(Path.Combine(_workspace, "node_modules", "pkg"));
        Directory.CreateDirectory(Path.Combine(_workspace, ".git"));
        File.WriteAllText(Path.Combine(_workspace, "node_modules", "pkg", "index.js"), "x");
        File.WriteAllText(Path.Combine(_workspace, ".DS_Store"), "x");
        File.WriteAllText(Path.Combine(_workspace, "notes.md"), "# notes");
        _storage = new LocalScriptStorageService(Path.Combine(_base, "data"));
    }

    public void Dispose()
    {
        _storage.Dispose();
        try { Directory.Delete(_base, recursive: true); } catch (IOException) { }
    }

    private async Task<(FryServerStudioViewModel Studio, FryServerDocumentItem Open)> StudioAsync()
    {
        Assert.True((await _storage.OpenExternalProjectAsync(_workspace)).Success);
        var open = await _storage.CreateNewServerDocumentAsync("Orders", "api");
        await _storage.CreateNewServerDocumentAsync("Orders"); // the same title at the top: both are listed
        var studio = new FryServerStudioViewModel(open, storageService: _storage);
        await studio.Explorer!.RefreshExplorer();
        return (studio, open);
    }

    private static List<ExplorerItemViewModel> All(IEnumerable<ExplorerItemViewModel> items)
    {
        var result = new List<ExplorerItemViewModel>();
        try
        {
            var snapshot = items.ToArray();
            foreach (var item in snapshot)
            {
                result.Add(item);
                result.AddRange(All(item.Children));
            }
        }
        catch (Exception)
        {
            // Collection may be mutated concurrently during background tree rebuilds.
        }
        return result;
    }

    [Fact]
    public async Task TheExplorer_ListsTheFolder_AsVsCodeDoes()
    {
        var (studio, _) = await StudioAsync();
        var explorer = studio.Explorer!;

        var top = explorer.ExplorerRootItems.Select(i => i.Name).ToList();
        Assert.Equal(new[] { "api", "node_modules", "notes.md", "Orders.fryserver" }, top);
        Assert.Equal("Orders.fryserver", Assert.Single(explorer.ExplorerRootItems.Single(i => i.Name == "api").Children).Name);
        Assert.Equal("ServerNetwork", explorer.ExplorerRootItems.Single(i => i.Name == "Orders.fryserver").IconKind);

        var modules = explorer.ExplorerRootItems.Single(i => i.Name == "node_modules");
        Assert.False(modules.ChildrenLoaded);
        modules.IsExpanded = true;
        for (var i = 0; i < 100 && !modules.ChildrenLoaded; i++) await Task.Delay(20);
        Assert.Equal("pkg", Assert.Single(modules.Children).Name);
    }

    [Fact]
    public async Task OpeningAServerFile_LoadsItHere_AndTheExplorerMarksIt()
    {
        var (studio, open) = await StudioAsync();
        var other = studio.Explorer!.ExplorerRootItems.Single(i => i.Name == "Orders.fryserver");
        Assert.NotEqual(open.Id, other.DocumentId);

        other.SelectCommand.Execute(null);
        for (var i = 0; i < 100 && studio.Document?.Id != other.DocumentId; i++) await Task.Delay(20);

        Assert.Equal(other.DocumentId, studio.Document!.Id);
        Assert.True(other.IsSelected);
    }

    [Fact]
    public async Task RenamingTheOpenDocument_RenamesItsFile_AndTheStudioFollows()
    {
        var (studio, open) = await StudioAsync();
        var item = All(studio.Explorer!.ExplorerRootItems).Single(i => i.DocumentId == open.Id);

        item.StartRename();
        item.EditName = "Shop";
        item.CommitRename();
        for (var i = 0; i < 100 && (!File.Exists(Path.Combine(_workspace, "api", "Shop.fryserver")) || item.FullPath != "api/Shop.fryserver"); i++) await Task.Delay(20);

        Assert.True(File.Exists(Path.Combine(_workspace, "api", "Shop.fryserver")));
        Assert.False(File.Exists(Path.Combine(_workspace, "api", "Orders.fryserver")));
        Assert.Equal("Shop.fryserver", item.Name);
        Assert.Equal("api/Shop.fryserver", item.FullPath);
        Assert.Equal("Shop", studio.DocumentTitle);
        Assert.Equal("Shop", (await _storage.LoadServerDocumentAsync(open.Id))!.Title);
    }

    [Fact]
    public async Task DuplicatingAndDeleting_WorkOnTheDisk()
    {
        var (studio, open) = await StudioAsync();
        var explorer = studio.Explorer!;
        var item = All(explorer.ExplorerRootItems).Single(i => i.DocumentId == open.Id);

        item.RequestDuplicateCommand.Execute(null);
        var copyPath = Path.Combine(_workspace, "api", "Orders copy.fryserver");
        for (var i = 0; i < 100 && (!File.Exists(copyPath) || !All(explorer.ExplorerRootItems).Any(x => x.Name == "Orders copy.fryserver")); i++) await Task.Delay(20);
        var copy = JsonSerializer.Deserialize<FryServerDocumentItem>(File.ReadAllText(copyPath))!;
        Assert.NotEqual(open.Id, copy.Id);          // a document of its own
        Assert.Equal("Orders copy", copy.Title);

        string? deleted = null;
        explorer.DocumentDeleted += id => deleted = id;
        var original = All(explorer.ExplorerRootItems).Single(i => i.DocumentId == open.Id);
        original.RequestDeleteCommand.Execute(null);
        original.ConfirmDeleteCommand.Execute(null);
        for (var i = 0; i < 100 && (File.Exists(Path.Combine(_workspace, "api", "Orders.fryserver")) || deleted == null); i++) await Task.Delay(20);

        Assert.False(File.Exists(Path.Combine(_workspace, "api", "Orders.fryserver")));
        Assert.Equal(open.Id, deleted);
        Assert.NotEqual(open.Id, studio.Document!.Id); // the open document went away with its file
    }

    [Fact]
    public async Task ANewServer_IsCreatedInTheFolder_OpenedAndNamedNext()
    {
        var (studio, _) = await StudioAsync();
        var explorer = studio.Explorer!;
        var api = explorer.ExplorerRootItems.Single(i => i.Name == "api");

        api.RequestNewFileCommand.Execute(null);
        ExplorerItemViewModel? created = null;
        for (var i = 0; i < 100 && (created == null || !created.IsRenaming); i++)
        {
            await Task.Delay(20);
            try
            {
                created = All(explorer.ExplorerRootItems).FirstOrDefault(c => c.FullPath == "api/New Server.fryserver"); // the tree is rebuilt
            }
            catch (Exception)
            {
                // Retry on next tick if tree is being mutated concurrently
            }
        }

        Assert.NotNull(created);
        Assert.True(created!.IsRenaming);
        Assert.Equal(created.DocumentId, studio.Document!.Id);
    }

    [Fact]
    public async Task Storage_RenameRefusesATakenName_AndDuplicatesPlainFiles()
    {
        await _storage.OpenExternalProjectAsync(_workspace);
        var first = await _storage.CreateNewNotebookAsync("A");
        await _storage.CreateNewNotebookAsync("B");

        await Assert.ThrowsAsync<IOException>(() => _storage.RenameDocumentFileAsync(first.Id, "B.frynb"));
        Assert.Equal("C.frynb", await _storage.RenameDocumentFileAsync(first.Id, "C"));
        Assert.Equal("C", (await _storage.LoadNotebookAsync(first.Id))!.Title);

        Assert.Equal("notes copy.md", await _storage.DuplicateFileAsync("notes.md"));
        Assert.Equal("notes copy 2.md", await _storage.DuplicateFileAsync("notes.md"));
        Assert.Equal("# notes", File.ReadAllText(Path.Combine(_workspace, "notes copy 2.md")));
    }
}
