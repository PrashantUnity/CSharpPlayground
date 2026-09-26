using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealPython;

/// <summary>A notebook of C# and Python cells with the real Python: each runs in its own kernel, from the notebook's UI.</summary>
[Collection(RealPythonCollection.Name)]
public class PythonNotebookTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_PythonNotebook_" + Guid.NewGuid().ToString("N"));
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
        var services = TestPython.Services(Path.Combine(_baseDir, "services"));
        ((PythonLanguage)services.Registry.Get(LanguageIds.Python)!).PythonToolchain.Select(TestPython.Require().ExecutablePath);
        var folder = Path.Combine(_baseDir, "work");
        Directory.CreateDirectory(folder);
        var notebook = new NotebookDocumentItem { Title = "Mixed" };
        notebook.Cells.AddRange(cells);
        var tab = new NotebookTabViewModel(notebook, filePath: Path.Combine(folder, "Mixed.frynb"), languages: services);
        _tabs.Add(tab);
        return tab;
    }

    private static NotebookCellItem CSharp(string source) => new() { Type = CellType.Code, Source = source };

    private static NotebookCellItem Python(string source) => new() { Type = CellType.Code, Source = source, Language = LanguageIds.Python };

    private static Task Run(NotebookTabViewModel tab, int cell) => tab.RunSingleCellAsync(tab.Cells[cell]).WaitAsync(Patience);

    [PythonFact]
    public async Task CSharpAndPythonCells_EachKeepTheirOwnState()
    {
        var tab = Tab(CSharp("var x = 1;\nx + 1"), Python("x = 40\nx + 2"), CSharp("x"), CSharp("#!python\nimport os\nprint(x, os.path.basename(os.getcwd()))"));

        await tab.RunAllCellsAsync().WaitAsync(Patience);

        Assert.All(tab.Cells, c => Assert.False(c.HasError, c.OutputText));
        Assert.Contains("2", tab.Cells[0].OutputText);
        Assert.Equal("42\n", tab.Cells[1].OutputText);
        Assert.Contains("1", tab.Cells[2].OutputText); // C#'s x, not Python's
        Assert.Equal("40 work\n", tab.Cells[3].OutputText); // Python runs in the notebook's folder
        Assert.Matches(@"^\.NET \(C#\) · Python 3\.\d+", tab.KernelName);
        Assert.Contains(tab.Variables, v => v is { Name: "x", Kernel: "Python", ValueDisplay: "40" });
    }

    [PythonFact]
    public async Task Values_GoBetweenCSharpAndPython_BothWays()
    {
        var tab = Tab(
            CSharp("var nums = new[] { 3, 1, 4 };\nvar info = new Dictionary<string, object> { [\"name\"] = \"Ada\", [\"age\"] = 36 };"),
            Python("#!share --from csharp nums\n#!share --from csharp info\nsquares = [n * n for n in nums]\ncount = len(squares)\nprint(sum(nums), info['name'], type(nums).__name__)"),
            CSharp("#!share --from python squares\n#!share --from python count --as n\nsquares.Sum() * n"));

        await tab.RunAllCellsAsync().WaitAsync(Patience);

        Assert.All(tab.Cells, c => Assert.False(c.HasError, c.OutputText));
        Assert.Equal("8 Ada list\n", tab.Cells[1].OutputText);
        Assert.Contains("78", tab.Cells[2].OutputText); // (9 + 1 + 16) * 3
    }

    [PythonFact]
    public async Task AnError_NamesTheCellAsTheNotebookShowsIt()
    {
        var tab = Tab(Python("def f(v):\n    return 10 / v"), Python("f(0)"));

        await Run(tab, 0);
        await Run(tab, 1);

        var cell = tab.Cells[1];
        Assert.True(cell.HasError);
        Assert.Contains($"<Cell [{cell.ExecutionCount}]>", cell.OutputText);
        Assert.Contains($"<Cell [{tab.Cells[0].ExecutionCount}]>\", line 2, in f", cell.OutputText);
        Assert.Contains("ZeroDivisionError", cell.OutputText);
    }

    [PythonFact]
    public async Task AMissingModule_IsOfferedForInstall_AndAnUndefinedName_ForRunningTheCellsAbove()
    {
        var tab = Tab(Python("import fry_no_such_module"), Python("print(total)"));

        await Run(tab, 0);
        await Run(tab, 1);

        Assert.Equal("fry_no_such_module", tab.Cells[0].MissingDependency);
        Assert.Equal("Install fry_no_such_module", tab.Cells[0].InstallMissingDependencyLabel);
        Assert.Equal("total", tab.Cells[1].MissingVariableName);
    }

    [PythonFact]
    public async Task Input_IsAskedForInTheCell()
    {
        var tab = Tab(Python("name = input('Name? ')\nprint('Hi', name)"));
        var cell = tab.Cells[0];

        var run = tab.RunSingleCellAsync(cell);
        var clock = Stopwatch.StartNew();
        while (!cell.IsAwaitingInput && !run.IsCompleted && clock.Elapsed < Patience) await Task.Delay(20);
        Assert.True(cell.IsAwaitingInput, cell.OutputText);
        Assert.Equal("Name? ", cell.InputPrompt);
        cell.InputText = "Ada";
        cell.SubmitInput();
        await run.WaitAsync(Patience);

        Assert.False(cell.HasError, cell.OutputText);
        Assert.Contains("Name? Ada\n", cell.OutputText);
        Assert.Contains("Hi Ada", cell.OutputText);
    }

    [PythonFact]
    public async Task Stop_InterruptsAPythonCell_AndItsVariablesLiveOn()
    {
        var tab = Tab(Python("n = 5"), Python("import time\ntime.sleep(30)"), Python("n * 2"));
        await Run(tab, 0);

        var sleeping = tab.RunSingleCellAsync(tab.Cells[1]);
        await Task.Delay(700);
        var clock = Stopwatch.StartNew();
        tab.InterruptExecution();
        await sleeping.WaitAsync(Patience);
        var stopped = clock.Elapsed;
        var status = tab.KernelStatusText;
        await Run(tab, 2);

        Assert.True(stopped < TimeSpan.FromSeconds(3), $"stopping took {stopped}");
        Assert.Equal("🛑 Cell execution interrupted", status);
        Assert.Equal("10\n", tab.Cells[2].OutputText);
    }

    [PythonFact]
    public async Task ClosingTheNotebook_EndsItsPythonProgram()
    {
        var tab = Tab(Python("x = 1"));
        await Run(tab, 0);
        var kernel = (ProtocolKernel)Assert.Single(tab.Kernels, k => k.LanguageId == LanguageIds.Python);
        var process = Process.GetProcessById(kernel.ProcessId!.Value);

        tab.ShutdownKernels();

        Assert.True(process.WaitForExit(5000), "the kernel's program is still running");
    }
}
