using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>An exported .ipynb names its language by name (<c>java</c>), with the MIME type (<c>text/x-java-source</c>) in its own field.</summary>
public class JupyterLanguageInfoTests
{
    [Theory]
    [InlineData("csharp", "csharp")]
    [InlineData("python", "python")]
    [InlineData("javascript", "javascript")]
    [InlineData("java", "java")]
    [InlineData("cpp", "c++")]
    [InlineData("go", "go")]
    [InlineData("fsharp", "fsharp")]
    [InlineData("sql", "sql")]
    [InlineData("rust", "rust")]
    public void ANotebookOfOneLanguage_IsExportedWithThatLanguagesName(string languageId, string expectedName)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = "Export",
            Kernel = languageId,
            Cells = { new NotebookCellItem { Type = CellType.Code, Language = languageId, Source = "x" } }
        };

        using var json = JsonDocument.Parse(DocumentExportService.ExportNotebookToIpynb(notebook));
        var info = json.RootElement.GetProperty("metadata").GetProperty("language_info");

        Assert.Equal(expectedName, info.GetProperty("name").GetString());
        Assert.Contains('/', info.GetProperty("mimetype").GetString()!);
    }

    [Fact]
    public void EveryLanguage_KeepsItsNameAndItsMimeTypeApart()
    {
        var services = new StudioLanguageServices(Path.Combine(Path.GetTempPath(), "FryPDF_Jupyter_" + Guid.NewGuid().ToString("N")));

        foreach (var language in services.Registry.All.Where(l => l.Jupyter != null))
        {
            var jupyter = language.Jupyter!;
            Assert.DoesNotContain('/', jupyter.LanguageName);
            Assert.DoesNotContain('/', jupyter.KernelName);
            Assert.Contains('/', jupyter.MimeType);
            Assert.StartsWith(".", jupyter.FileExtension);
        }
    }
}
