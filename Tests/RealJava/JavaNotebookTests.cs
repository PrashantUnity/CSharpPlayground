using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;
using NotebookTabViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookTabViewModel;

namespace CSharpEditorPlugin.Tests.RealJava;

/// <summary>A notebook of C# and Java cells with real Java: each runs in its own kernel, from the notebook's UI.</summary>
[Collection(RealJavaCollection.Name)]
public class JavaNotebookTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaNotebook_" + Guid.NewGuid().ToString("N"));
    private readonly List<NotebookTabViewModel> _tabs = new();

    public void Dispose()
    {
        foreach (var tab in _tabs) tab.ShutdownKernels();
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private NotebookTabViewModel Tab(params NotebookCellItem[] cells)
    {
        var services = TestJava.Services(Path.Combine(_baseDir, "services"));
        ((JavaLanguage)services.Registry.Get(LanguageIds.Java)!).JavaToolchain.Select(TestJava.Require().ExecutablePath);
        var folder = Path.Combine(_baseDir, "work");
        Directory.CreateDirectory(folder);
        var notebook = new NotebookDocumentItem { Title = "Mixed" };
        notebook.Cells.AddRange(cells);
        var tab = new NotebookTabViewModel(notebook, filePath: Path.Combine(folder, "Mixed.frynb"), languages: services);
        _tabs.Add(tab);
        return tab;
    }

    private static NotebookCellItem CSharp(string source) => new() { Type = CellType.Code, Source = source };

    private static NotebookCellItem Java(string source) => new() { Type = CellType.Code, Source = source, Language = LanguageIds.Java };

    [JavaFact]
    public async Task CSharpAndJavaCells_EachKeepTheirOwnState()
    {
        var tab = Tab(
            CSharp("""
                var x = 1;
                x + 1
                """),
            Java("""
                int x = 40;
                x + 2;
                """),
            CSharp("x"),
            CSharp("""
                #!java
                System.out.println("Java: " + x);
                """));

        await tab.RunAllCellsAsync().WaitAsync(Patience);

        Assert.All(tab.Cells, c => Assert.False((bool)c.HasError, c.OutputText));
        Assert.Contains((string)"2", (string?)tab.Cells[0].OutputText);
        Assert.Equal((string?)"42\n", (string?)tab.Cells[1].OutputText);
        Assert.Contains((string)"1", (string?)tab.Cells[2].OutputText); // C#'s x, not Java's
        Assert.Equal((string?)"Java: 40\n", (string?)tab.Cells[3].OutputText);
        Assert.Contains(tab.Variables, v => v is { Name: "x", Kernel: "Java", ValueDisplay: "40" });
    }

    [JavaFact]
    public async Task Values_GoBetweenCSharpAndJava_BothWays()
    {
        var tab = Tab(
            CSharp("""
                var nums = new[] { 3, 1, 4 };
                """),
            Java("""
                #!share --from csharp nums
                int sum = nums[0] + nums[1] + nums[2];
                sum;
                """),
            CSharp("""
                #!share --from java sum --as total
                total * 10
                """));

        await tab.RunAllCellsAsync().WaitAsync(Patience);

        Assert.All(tab.Cells, c => Assert.False((bool)c.HasError, c.OutputText));
        Assert.Equal((string?)"8\n", (string?)tab.Cells[1].OutputText);
        Assert.Contains((string)"80", (string?)tab.Cells[2].OutputText);
    }
}
