using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;

namespace CSharpEditorPlugin.Tests.RealPython;

/// <summary>The Code Studio running .py files with the real Python, as the user sees it: Terminal, Problems, input, Stop.</summary>
[Collection(RealPythonCollection.Name)]
public class CodeStudioPythonRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioPython_" + Guid.NewGuid().ToString("N"));

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

    private async Task<CSharpCodeStudioViewModel> StudioWith(string fileName, string code)
    {
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        ((PythonLanguage)services.Registry.Get(LanguageIds.Python)!).PythonToolchain.Select(TestPython.Require().ExecutablePath);
        var storage = new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), services.Registry);
        File.WriteAllText(Path.Combine(storage.LibraryRootPath, fileName), code);

        var studio = new CSharpCodeStudioViewModel(
            await storage.CreateNewScriptAsync("Notes"), storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { },
            blindProgress: new LocalBlindProgressService(Path.Combine(_baseDir, "progress")),
            languages: services);
        await studio.SwitchToScriptAsync(studio.ExplorerRootItems.Single(i => i.Name == fileName));
        return studio;
    }

    [PythonFact]
    public async Task APyFile_RunsWithPython_AndItsOutputShowsInTheTerminal()
    {
        var studio = await StudioWith("hello.py", "import sys\nprint('hello', sys.version_info.major)\n");

        await studio.RunCodeCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.Contains((string)"hello 3", (string?)studio.ConsoleOutput);
        Assert.Contains((string)"exited with code 0", (string?)studio.ConsoleOutput);
        Assert.Equal((string?)"Completed", (string?)studio.CompilerStatusText);
        Assert.StartsWith("Python 3.", studio.RuntimeLabel);
    }

    [PythonFact]
    public async Task AnError_ShowsInProblems_AtItsLine()
    {
        var studio = await StudioWith("broken.py", "values = [1, 2]\nprint(values[1])\nprint(values[5])\n");

        await studio.RunCodeCommand.ExecuteAsync(null).WaitAsync(Patience);

        var problem = Assert.Single(studio.Diagnostics);
        Assert.Equal("IndexError", problem.Id);
        Assert.Equal(3, problem.Line);
        Assert.Equal((string?)"Exited with code 1", (string?)studio.CompilerStatusText);
    }

    [PythonFact]
    public async Task AMissingModule_OffersToInstallIt()
    {
        var studio = await StudioWith("needs.py", "import fry_no_such_module_here\n");

        await studio.RunCodeCommand.ExecuteAsync(null).WaitAsync(Patience);

        Assert.Equal("Install fry_no_such_module_here", Assert.Single(studio.Diagnostics).QuickFixLabel);
    }

    [PythonFact]
    public async Task InputTypedInTheTerminal_ReachesInput()
    {
        var studio = await StudioWith("ask.py", "name = input('Name? ')\nprint('Hi', name)\n");

        var run = studio.RunCodeCommand.ExecuteAsync(null);
        await WaitUntil(() => studio.IsAcceptingProgramInput && studio.ConsoleOutput.Contains("Name? "));
        studio.ProgramInputText = "Linus";
        await studio.SendProgramInputCommand.ExecuteAsync(null);
        await run.WaitAsync(Patience);

        Assert.Contains((string)"Hi Linus", (string?)studio.ConsoleOutput);
    }

    [PythonFact]
    public async Task Stop_EndsARunningScript()
    {
        var studio = await StudioWith("forever.py", "import time\nprint('started', flush=True)\nwhile True:\n    time.sleep(0.1)\n");

        var run = studio.RunCodeCommand.ExecuteAsync(null);
        await WaitUntil(() => studio.ConsoleOutput.Contains("started"));
        studio.StopCommand.Execute(null);
        await run.WaitAsync(Patience);

        Assert.Equal((string?)"🛑 Cancelled", (string?)studio.CompilerStatusText);
        Assert.False((bool)studio.IsExecuting);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(20);
        }
    }
}
