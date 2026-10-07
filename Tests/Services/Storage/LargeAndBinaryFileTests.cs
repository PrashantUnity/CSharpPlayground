using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Opening files that are not editable text: a binary file is described, not loaded; a file over the editor limit is not
/// read into memory; and neither description is ever saved over the real file.
/// </summary>
public class LargeAndBinaryFileTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_LargeFiles_" + Guid.NewGuid().ToString("N"));
    private readonly string _library;
    private readonly LocalScriptStorageService _storage;

    public LargeAndBinaryFileTests()
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

    private async Task<string> IdOf(string fileName) =>
        (await _storage.LoadWorkspaceSummariesAsync()).Single(s => s.IsSourceFile && s.Title == Path.GetFileNameWithoutExtension(fileName)).Id;

    [Fact]
    public async Task ABinaryFileWithATextExtension_IsDescribed_AndCtrlSNeverWritesTheDescriptionOverIt()
    {
        // A Python name, binary content (a NUL in the first bytes): it used to open as a snippet that Ctrl+S then wrote
        // over the real file.
        var path = Path.Combine(_library, "blob.py");
        var original = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x00, 0x01, 0x02, 0x03, 0x00, 0xFF };
        File.WriteAllBytes(path, original);

        var document = (await _storage.LoadScriptAsync(await IdOf("blob.py")))!;
        Assert.StartsWith("Binary asset", document.Description);
        Assert.DoesNotContain('\0', document.Code);

        Assert.True(await _storage.SaveSourceFileAsync(document, overwriteChangesOnDisk: true));
        document.Code += "\n// typed by the user";
        Assert.True(await _storage.SaveSourceFileAsync(document, overwriteChangesOnDisk: true));

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task AFileOverTheEditorLimit_IsNotLoaded_AndIsNeverOverwritten()
    {
        var path = Path.Combine(_library, "huge.py");
        long size = LocalScriptStorageService.MaxEditorFileBytes + 1024;
        using (var writer = new FileStream(path, FileMode.Create))
        {
            var line = Encoding.UTF8.GetBytes("print('a line of a very long generated file')\n");
            for (long written = 0; written < size; written += line.Length) writer.Write(line);
        }

        long before = new FileInfo(path).Length;
        var document = (await _storage.LoadScriptAsync(await IdOf("huge.py")))!;

        Assert.StartsWith("Large file", document.Description);
        Assert.True(document.Code.Length < 4096, $"the editor got {document.Code.Length} characters of a {before} byte file");
        Assert.Contains("too large to open", document.Code);

        document.Code = "oops";
        Assert.True(await _storage.SaveSourceFileAsync(document, overwriteChangesOnDisk: true));
        Assert.Equal(before, new FileInfo(path).Length);
    }

    [Fact]
    public async Task AnOrdinaryTextFile_StillOpensAndSavesAsBefore()
    {
        var path = Path.Combine(_library, "main.py");
        File.WriteAllText(path, "print(1)\n");
        var document = (await _storage.LoadScriptAsync(await IdOf("main.py")))!;
        Assert.Equal("print(1)\n", document.Code);

        document.Code = "print(2)\n";
        Assert.True(await _storage.SaveSourceFileAsync(document, overwriteChangesOnDisk: true));
        Assert.Equal("print(2)\n", File.ReadAllText(path));
    }
}
