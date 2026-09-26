using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealPython;

/// <summary>.py files run with the real Python: output, exit code, tracebacks, input and Stop.</summary>
[Collection(RealPythonCollection.Name)]
public class PythonScriptRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_PythonRun_" + Guid.NewGuid().ToString("N"));

    public PythonScriptRunTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private async Task<(ScriptRunSession Session, ConcurrentQueue<string> Output)> Start(string path)
    {
        var python = TestPython.Require();
        var services = TestPython.Services(Path.Combine(_dir, ".studio"));
        var language = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, python));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [PythonFact]
    public void TheInstalledPython_IsFound()
    {
        var python = TestPython.Require();

        Assert.True(python.Version >= PythonToolchainProvider.MinimumVersion, python.Label);
        Assert.True(File.Exists(python.ExecutablePath), python.ExecutablePath);
    }

    [PythonFact]
    public async Task AFile_RunsInItsFolder_WithItsOutputAndSiblingImports()
    {
        Write("helper.py", "def greet(name):\n    return f'Hello, {name}!'\n");
        var main = Write("main.py", "import os, sys\nimport helper\nprint(helper.greet('Ada'))\nprint('cwd', os.path.basename(os.getcwd()))\nprint('err line', file=sys.stderr)\n");

        var (session, output) = await Start(main);
        var result = await session.Completion.WaitAsync(Patience);

        var text = string.Concat(output);
        Assert.True(result.Succeeded, text);
        Assert.Contains("Hello, Ada!", text);
        Assert.Contains("cwd " + Path.GetFileName(_dir), text);
        Assert.Contains("err line", text);
    }

    [PythonFact]
    public async Task AnError_EndsTheRun_WithItsLineInProblems()
    {
        var main = Write("main.py", "print('before')\n\ndef f(x):\n    return 10 / x\n\nf(0)\n");

        var (session, output) = await Start(main);
        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("before", string.Concat(output));
        var problem = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal("ZeroDivisionError", problem.Id);
        Assert.Equal(4, problem.Line); // inside f, the deepest line of the script the traceback passes through
    }

    [PythonFact]
    public async Task AMissingModule_IsReported_ForInstalling()
    {
        var main = Write("main.py", "import fry_no_such_module_here.sub\n");

        var result = await (await Start(main)).Session.Completion.WaitAsync(Patience);

        Assert.Equal("fry_no_such_module_here", result.Diagnostics.MissingDependency);
    }

    [PythonFact]
    public async Task TypedInput_AnswersInput()
    {
        var main = Write("main.py", "name = input('Name? ')\nprint('Hi ' + name)\n");
        var (session, output) = await Start(main);

        await WaitUntil(() => string.Concat(output).Contains("Name? "));
        await session.SendInputAsync("Grace\n");
        var result = await session.Completion.WaitAsync(Patience);

        Assert.True(result.Succeeded, string.Concat(output));
        Assert.Contains("Hi Grace", string.Concat(output));
    }

    [PythonFact]
    public async Task OutputStreams_BeforeTheRunEnds()
    {
        var main = Write("main.py", "import time\nprint('first', end='', flush=True)\ntime.sleep(30)\n");
        var (session, output) = await Start(main);

        await WaitUntil(() => string.Concat(output).Contains("first"));
        Assert.False(session.Completion.IsCompleted);
        session.Stop();
        Assert.True((await session.Completion.WaitAsync(Patience)).WasCancelled);
    }

    [PythonFact]
    public async Task Stop_EndsAScriptThatNeverFinishes_AndWhatItStarted()
    {
        var main = Write("main.py", "import subprocess, sys, time\nsubprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])\nprint('ready', flush=True)\nwhile True:\n    time.sleep(0.1)\n");
        var (session, output) = await Start(main);
        await WaitUntil(() => string.Concat(output).Contains("ready"));

        var stopped = System.Diagnostics.Stopwatch.StartNew();
        session.Stop();
        var result = await session.Completion.WaitAsync(Patience);

        Assert.True(result.WasCancelled);
        Assert.True(stopped.Elapsed < TimeSpan.FromSeconds(5), $"Stop took {stopped.Elapsed}");
    }

    [PythonFact]
    public async Task TextThatIsntAscii_ArrivesIntact()
    {
        var main = Write("main.py", "print('héllo ✓ 日本語 🐍')\n");

        var (session, output) = await Start(main);
        await session.Completion.WaitAsync(Patience);

        Assert.Contains("héllo ✓ 日本語 🐍", string.Concat(output));
    }

    [PythonFact]
    public async Task TheStudioEnvironment_CanBeCreated_AndRunsCode()
    {
        var python = TestPython.Require();
        var services = TestPython.Services(Path.Combine(_dir, ".studio"));
        var language = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
        var log = new ConcurrentQueue<string>();

        var environment = await language.PythonToolchain.EnsureStudioEnvironmentAsync(python, log.Enqueue).WaitAsync(TimeSpan.FromMinutes(3));

        Assert.True(File.Exists(environment.ExecutablePath), string.Concat(log));
        Assert.True(environment.Is(PythonToolchainProvider.VirtualEnvironmentProperty));
        Assert.StartsWith(Path.Combine(_dir, ".studio"), environment.ExecutablePath);
        Assert.Equal(python.Version, environment.Version);
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
