using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Plain source files (main.py) in the workspace: listed, opened, saved safely, created, renamed and deleted.</summary>
public class SourceFileStorageTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_SourceFiles_" + Guid.NewGuid().ToString("N"));
    private readonly string _library;
    private readonly LocalScriptStorageService _storage;

    public SourceFileStorageTests()
    {
        _storage = new LocalScriptStorageService(_baseDir, new StudioLanguageServices(Path.Combine(_baseDir, "services")).Registry);
        _library = _storage.LibraryRootPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string relativePath, string text, bool withBom = false)
    {
        var path = Path.Combine(_library, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(withBom));
        return path;
    }

    [Fact]
    public async Task SourceFiles_AreListed_ButNotWhatToolsGenerate()
    {
        Write("main.py", "print('hi')\n");
        Write("pkg/helper.py", "X = 1\n");
        Write("env/tool.py", "# a folder of the user's own that happens to be called env\n");
        Write(".venv/pyvenv.cfg", "home = /usr/bin\n");
        Write(".venv/lib/python3.14/site-packages/requests/api.py", "");
        Write("pkg/__pycache__/helper.cpython-314.py", "");
        Write("node_modules/thing/index.py", "");
        Write("notes.txt", "not code");

        var summaries = await _storage.LoadWorkspaceSummariesAsync();
        var sources = summaries.Where(s => s.IsSourceFile).OrderBy(s => s.FolderPath).ThenBy(s => s.Title).ToList();

        Assert.Equal(new[] { "main", "tool", "helper" }, sources.Select(s => s.Title));
        Assert.Equal(new[] { "", "env", "pkg" }, sources.Select(s => s.FolderPath));
        var main = sources[0];
        Assert.Equal(LanguageIds.Python, main.LanguageId);
        Assert.Equal("Python", main.LanguageName);
        Assert.Equal(".py", main.DisplayExtension);
        Assert.Equal("Python", main.RuntimeBadgeText);

        var folders = await _storage.LoadFolderPathsAsync();
        Assert.Contains("pkg", folders);
        Assert.Contains("env", folders);
        Assert.DoesNotContain(folders, f => f.StartsWith(".venv", StringComparison.Ordinal) || f.Contains("__pycache__") || f.StartsWith("node_modules", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASourceFilesId_FollowsItsPath_AcrossSessions()
    {
        var path = Write("main.py", "print('hi')\n");
        var first = (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;
        var again = new LocalScriptStorageService(_baseDir);

        var second = (await again.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;

        Assert.Equal(first, second);
        Assert.Equal(LocalScriptStorageService.SourceFileId(path), first);
        Assert.NotEqual(first, LocalScriptStorageService.SourceFileId(Path.Combine(_library, "other.py")));
        Assert.True(LocalScriptStorageService.IsSourceFileId(first));
    }

    [Fact]
    public async Task AnOpenedSourceFile_IsItsText_WithItsLanguageAndPath()
    {
        var path = Write("main.py", "import sys\nprint(sys.version)\n");
        var id = (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;

        var document = await _storage.LoadScriptAsync(id);

        Assert.NotNull(document);
        Assert.Equal("main.py", document.Title);
        Assert.Equal("import sys\nprint(sys.version)\n", document.Code);
        Assert.Equal(LanguageIds.Python, document.LanguageId);
        Assert.Equal(Path.GetFullPath(path), document.SourceFilePath);
    }

    [Fact]
    public async Task Saving_WritesTheText_AndKeepsAByteOrderMark()
    {
        var path = Write("main.py", "print(1)\n", withBom: true);
        var id = (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;
        var document = (await _storage.LoadScriptAsync(id))!;

        document.Code = "print(2)\n";
        Assert.True(await _storage.SaveScriptAsync(document));

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        Assert.Equal("print(2)\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.DoesNotContain("SchemaVersion", File.ReadAllText(path)); // plain text, never JSON
    }

    [Fact]
    public async Task AnAutomaticSave_NeverWritesOverAnotherProgramsChange_ButCtrlSDoes()
    {
        var path = Write("main.py", "print('original')\n");
        var id = (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;
        var document = (await _storage.LoadScriptAsync(id))!;

        // Another editor changes the file after the studio opened it.
        File.WriteAllText(path, "print('from another editor')\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        document.Code = "print('studio edit')\n";

        Assert.False(await _storage.SaveScriptAsync(document));
        Assert.Equal("print('from another editor')\n", File.ReadAllText(path));

        Assert.True(await _storage.SaveSourceFileAsync(document, overwriteChangesOnDisk: true));
        Assert.Equal("print('studio edit')\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task AnAutomaticSave_OfUnchangedText_DoesntTouchTheFile()
    {
        var path = Write("main.py", "print(1)\n");
        var stamp = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(path, stamp);
        var id = (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile).Id;
        var document = (await _storage.LoadScriptAsync(id))!;

        Assert.True(await _storage.SaveScriptAsync(document));

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task ANewSourceFile_StartsFromItsLanguagesTemplate_WithAnImportableName()
    {
        var first = await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "tool", "pkg");
        var second = await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "tool", "pkg");
        var unnamed = await _storage.CreateNewSourceFileAsync(LanguageIds.Python);

        Assert.Equal(Path.Combine(_library, "pkg", "tool.py"), first!.SourceFilePath);
        Assert.Equal(Path.Combine(_library, "pkg", "tool_2.py"), second!.SourceFilePath);
        Assert.Matches(@"^script_\d{6}\.py$", unnamed!.Title);
        Assert.Contains("print(", File.ReadAllText(first.SourceFilePath!));
        Assert.Null(await _storage.CreateNewSourceFileAsync(LanguageIds.CSharp)); // C# documents are .frycs, not source files
    }

    [Fact]
    public async Task Renaming_MovesTheFile_AndKeepsItsLanguage()
    {
        var created = (await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "draft"))!;

        var renamed = await _storage.RenameSourceFileAsync(created.Id, "final");

        Assert.Equal(Path.Combine(_library, "final.py"), renamed.FilePath);
        Assert.True(File.Exists(renamed.FilePath));
        Assert.False(File.Exists(created.SourceFilePath));
        Assert.NotEqual(created.Id, renamed.Id);
        Assert.Equal("final.py", (await _storage.LoadScriptAsync(renamed.Id))!.Title);
    }

    [Fact]
    public async Task Renaming_OntoAnExistingFile_IsRefused()
    {
        Write("taken.py", "x = 1\n");
        var created = (await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "draft"))!;

        await Assert.ThrowsAsync<IOException>(() => _storage.RenameSourceFileAsync(created.Id, "taken.py"));
        Assert.Equal("x = 1\n", File.ReadAllText(Path.Combine(_library, "taken.py")));
    }

    [Fact]
    public async Task ACaseOnlyRename_Works()
    {
        var created = (await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "readme"))!;

        var renamed = await _storage.RenameSourceFileAsync(created.Id, "README.py");

        Assert.Equal("README.py", Path.GetFileName(renamed.FilePath));
        Assert.Contains("README.py", Directory.GetFiles(_library).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Deleting_RemovesTheFile()
    {
        var created = (await _storage.CreateNewSourceFileAsync(LanguageIds.Python, "gone"))!;

        await _storage.DeleteItemAsync(created.Id);

        Assert.False(File.Exists(created.SourceFilePath));
        Assert.Null(await _storage.LoadScriptAsync(created.Id));
    }

    [Fact]
    public async Task OpeningASourceFileFromElsewhere_OpensItInPlace()
    {
        var outside = Path.Combine(_baseDir, "elsewhere", "report.py");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        File.WriteAllText(outside, "print('report')\n");

        var result = await _storage.OpenExternalProjectAsync(outside);
        var document = await _storage.LoadScriptAsync(result.PrimaryDocumentId!);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Path.GetFullPath(outside), document!.SourceFilePath);
        Assert.False(File.Exists(Path.ChangeExtension(outside, ".frycs"))); // not imported into a .frycs, as .cs files are
    }

    [Fact]
    public async Task TheLegacyProjectList_LeavesSourceFilesOut()
    {
        Write("main.py", "print('hi')\n");

        Assert.DoesNotContain(await _storage.LoadScriptsAsync(), s => s.Title.Contains("main"));
    }
}
