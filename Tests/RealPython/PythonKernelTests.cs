using System.Collections.Concurrent;
using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealPython;

/// <summary>The notebook's Python kernel with the real Python: state, output, errors, Stop, crashes, input, values.</summary>
[Collection(RealPythonCollection.Name)]
public class PythonKernelTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_PythonKernel_" + Guid.NewGuid().ToString("N"));
    private readonly string _notebookFolder;
    private readonly List<INotebookKernel> _kernels = new();

    public PythonKernelTests()
    {
        _notebookFolder = Path.Combine(_baseDir, "notebooks");
        Directory.CreateDirectory(_notebookFolder);
    }

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ProtocolKernel Kernel()
    {
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        var python = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
        python.PythonToolchain.Select(TestPython.Require().ExecutablePath);
        var kernel = (ProtocolKernel)python.NotebookKernels.Create(new KernelCreationContext(() => _notebookFolder));
        _kernels.Add(kernel);
        return kernel;
    }

    private sealed record Run(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich, List<string> Chunks);

    private static async Task<Run> Execute(ProtocolKernel kernel, string code, CancellationToken ct = default, Func<string, bool, CancellationToken, Task<string?>>? ask = null)
    {
        var chunks = new ConcurrentQueue<string>();
        var rich = new ConcurrentQueue<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = chunks.Enqueue,
            OnRichOutput = rich.Enqueue,
            OnInputRequest = ask
        }, ct).WaitAsync(Patience);
        return new Run(result, string.Concat(chunks), rich.ToList(), chunks.ToList());
    }

    [PythonFact]
    public async Task State_CarriesOverBetweenCells_AndTheLastExpressionIsShown()
    {
        var kernel = Kernel();

        var first = await Execute(kernel, "x = 41");
        var second = await Execute(kernel, "x + 1");

        Assert.True(first.Result.Success, first.Console);
        Assert.Equal("42\n", second.Console);
        Assert.StartsWith("Python 3.", kernel.DisplayName);
    }

    [PythonFact]
    public async Task OutputStreams_WhileTheCellRuns()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "import time\nprint('first', end='', flush=True)\ntime.sleep(0.5)\nprint(' second')");

        Assert.Equal("first second\n", run.Console);
        Assert.True(run.Chunks.Count >= 2, "the first part arrived before the cell finished");
    }

    [PythonFact]
    public async Task AnError_ShowsItsTraceback_WithTheLineOfTheCell()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "def f(v):\n    return 10 / v\n\nf(0)");

        Assert.False(run.Result.Success);
        Assert.Equal("ZeroDivisionError: division by zero", run.Result.ErrorMessage);
        Assert.Contains("line 2, in f", run.Console);
        Assert.DoesNotContain("fry_kernel.py", run.Console); // the kernel's own frames are hidden
    }

    [PythonFact]
    public async Task ASyntaxError_IsReported()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "print('unclosed'");

        Assert.False(run.Result.Success);
        Assert.StartsWith("SyntaxError", run.Result.ErrorMessage);
    }

    [PythonFact]
    public async Task MissingModulesAndNames_AreNamed()
    {
        var kernel = Kernel();

        var module = await Execute(kernel, "import fry_no_such_module.sub");
        var name = await Execute(kernel, "print(not_defined_anywhere)");

        Assert.Equal("fry_no_such_module", module.Result.MissingDependency);
        Assert.Equal("not_defined_anywhere", name.Result.MissingName);
    }

    [PythonFact]
    public async Task SysExit_EndsTheCell_NotTheKernel()
    {
        var kernel = Kernel();
        await Execute(kernel, "x = 1");
        var process = kernel.ProcessId;

        var exit = await Execute(kernel, "import sys\nsys.exit(3)");
        var after = await Execute(kernel, "x");

        Assert.False(exit.Result.Success);
        Assert.Equal("1\n", after.Console);
        Assert.Equal(process, kernel.ProcessId);
    }

    [PythonFact]
    public async Task Stop_InterruptsABusyLoop_AndASleep_AndTheKernelLivesOn()
    {
        var kernel = Kernel();
        await Execute(kernel, "x = 7");
        var process = kernel.ProcessId;

        foreach (var code in new[] { "while True:\n    pass", "import time\ntime.sleep(30)" })
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var clock = Stopwatch.StartNew();
            var run = await Execute(kernel, code, stop.Token);

            Assert.True(run.Result.WasCancelled, run.Console);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"stopping took {clock.Elapsed}");
        }

        Assert.Equal("7\n", (await Execute(kernel, "x")).Console);
        Assert.Equal(process, kernel.ProcessId);
    }

    [PythonFact]
    public async Task AKernelThatDies_FailsItsCell_AndTheNextCellStartsAFreshOne()
    {
        var kernel = Kernel();
        await Execute(kernel, "x = 1");

        var clock = Stopwatch.StartNew();
        var crash = await Execute(kernel, "import os\nos._exit(3)");
        var next = await Execute(kernel, "print('fresh')");

        Assert.False(crash.Result.Success);
        Assert.Contains("exit code 3", crash.Result.ErrorMessage);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30));
        Assert.Contains("variables from before are gone", next.Console);
        Assert.Contains("fresh", next.Console);
    }

    [PythonFact]
    public async Task Input_IsAskedOfWhoeverRunsTheCell()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "name = input('Name? ')\nprint('Hi', name)", ask: (_, _, _) => Task.FromResult<string?>("Ada"));

        Assert.True(run.Result.Success, run.Console);
        Assert.Contains("Name? Ada\n", run.Console);
        Assert.Contains("Hi Ada", run.Console);
    }

    [PythonFact]
    public async Task WhatCCodeAndChildProcessesWrite_ShowsAsOutput_WithoutBreakingTheProtocol()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "import os, subprocess, sys\nos.write(1, b'raw fd write\\n')\nsubprocess.run([sys.executable, '-c', 'print(\"child says hi\")'])\nprint('after')");
        var next = await Execute(kernel, "40 + 2");

        Assert.True(run.Result.Success, run.Console);
        Assert.Contains("raw fd write", run.Console);
        Assert.Contains("child says hi", run.Console);
        Assert.Contains("after", run.Console);
        Assert.Equal("42\n", next.Console);
    }

    [PythonFact]
    public async Task RichReprs_BecomeRichOutput()
    {
        var kernel = Kernel();

        var html = await Execute(kernel, "class H:\n    def _repr_html_(self):\n        return '<b>bold</b>'\nH()");
        var png = await Execute(kernel, "import base64\nclass P:\n    def _repr_png_(self):\n        return base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==')\nP()");

        Assert.Equal("<b>bold</b>", Assert.Single(html.Rich).HtmlContent);
        var image = Assert.Single(png.Rich);
        Assert.Equal(CellOutputKind.Image, image.Kind);
        Assert.Equal(0x89, image.ImageBytes![0]);
    }

    [PythonFact]
    public async Task Variables_AndValues_GoInAndOut()
    {
        var kernel = Kernel();
        await Execute(kernel, "total = 42\nwords = ['a', 'b']\nimport math");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        await kernel.SetValueFromJsonAsync("shared", "[1,2,3]", CancellationToken.None);
        var sum = await Execute(kernel, "sum(shared)");

        Assert.Contains(variables, v => v is { Name: "total", TypeName: "int", ValueDisplay: "42" });
        Assert.DoesNotContain(variables, v => v.Name == "math"); // modules aren't variables
        Assert.Equal("6\n", sum.Console);
        Assert.Equal("[\"a\", \"b\"]", await kernel.GetValueJsonAsync("words", CancellationToken.None));
        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("nothing", CancellationToken.None));
    }

    [PythonFact]
    public async Task AModuleInTheNotebooksFolder_CanBeImported()
    {
        File.WriteAllText(Path.Combine(_notebookFolder, "helpers.py"), "def triple(v):\n    return v * 3\n");
        var kernel = Kernel();

        var run = await Execute(kernel, "import helpers, os\nprint(helpers.triple(14), os.path.basename(os.getcwd()))");

        Assert.True(run.Result.Success, run.Console);
        Assert.Equal("42 notebooks\n", run.Console);
    }

    [PythonFact]
    public async Task WhenItsInputEnds_TheKernelAnswersWhatItWasSent_ThenExits_EvenWithACellStillBusy()
    {
        var kernelFolder = EmbeddedKernelFiles.Extract(typeof(PythonKernelLauncher).Assembly, PythonKernelLauncher.ResourcePrefix, Path.Combine(_baseDir, "kernel"));

        async Task<(string Output, TimeSpan Took)> Run(string input)
        {
            var start = new ProcessStartInfo(TestPython.Require().ExecutablePath, ["-u", Path.Combine(kernelFolder, "fry_kernel.py"), "--cwd", _notebookFolder])
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var clock = Stopwatch.StartNew();
            using var process = Process.Start(start)!;
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(15000), "the kernel didn't exit when its input ended");
            return (await output, clock.Elapsed);
        }

        var answered = await Run("{\"type\": \"execute\", \"id\": \"1\", \"code\": \"x = 6 * 7\", \"cell\": \"[1]\"}\n{\"type\": \"get_value\", \"id\": \"2\", \"name\": \"x\"}\n");
        var busy = await Run("{\"type\": \"execute\", \"id\": \"1\", \"code\": \"import time\\ntime.sleep(30)\", \"cell\": \"[1]\"}\n");

        Assert.Contains("\"json\": \"42\"", answered.Output);
        Assert.True(busy.Took < TimeSpan.FromSeconds(10), $"a busy kernel took {busy.Took} to exit");
    }

    [PythonFact]
    public async Task ClosingTheKernel_EndsItsProgram()
    {
        var kernel = Kernel();
        await Execute(kernel, "x = 1");
        var process = Process.GetProcessById(kernel.ProcessId!.Value);

        kernel.Dispose();

        Assert.True(process.WaitForExit(5000), "the kernel's program is still running");
    }
}
