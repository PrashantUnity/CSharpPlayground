using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>Running files the studio's way from any host, with the real Python and Node.js where they are installed.</summary>
public sealed class FileRunnerTests
{
    private static readonly FileRunner Runner = new(ToolchainFactAttribute.Registry, new RoslynCompilerService(), new ProcessLauncher());

    private static async Task<(FileRunResult Result, string Output)> RunAsync(string path, params string[] arguments)
    {
        var output = new StringBuilder();
        var language = Runner.LanguageOf(path) ?? throw new InvalidOperationException($"No language runs {path}.");
        var result = await Runner.RunFileAsync(path, language, new FileRunOptions
        {
            OnOutput = text => { lock (output) output.Append(text); },
            Arguments = arguments,
        }, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
        return (result, output.ToString());
    }

    [Fact]
    public void FilesAreRunByTheirExtension()
    {
        Assert.Equal(LanguageIds.CSharp, Runner.LanguageOf("a/b.cs")?.Id);
        Assert.Equal(LanguageIds.CSharp, Runner.LanguageOf("a/b.csx")?.Id);
        Assert.Equal(LanguageIds.Python, Runner.LanguageOf("a/b.py")?.Id);
        Assert.Equal(LanguageIds.JavaScript, Runner.LanguageOf("a/b.mjs")?.Id);
        Assert.Null(Runner.LanguageOf("a/b.txt"));
    }

    [Fact]
    public async Task ACSharpFile_Runs()
    {
        using var folder = new TempFolder();
        var (result, output) = await RunAsync(folder.File("hello.cs", "Console.WriteLine(\"from a file\");"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("from a file", output);
    }

    [ToolchainFact(LanguageIds.Python)]
    public async Task APythonFile_GetsEachArgumentWhole_AndReportsItsExitCode()
    {
        using var folder = new TempFolder();
        var path = folder.File("args.py", "import sys\nprint(sys.argv[1:])\nsys.exit(3)\n");

        var (result, output) = await RunAsync(path, "two words", "x");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("['two words', 'x']", output);
    }

    [ToolchainFact(LanguageIds.Python)]
    public async Task APythonError_IsAProblemAtItsLine()
    {
        using var folder = new TempFolder();
        var (result, _) = await RunAsync(folder.File("bad.py", "x = 1\nprint(undefined_name)\n"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(result.Problems, p => p.Line == 2);
    }

    [ToolchainFact(LanguageIds.JavaScript)]
    public async Task AJavaScriptFile_GetsItsArguments()
    {
        using var folder = new TempFolder();
        var (result, output) = await RunAsync(folder.File("args.js", "console.log(JSON.stringify(process.argv.slice(2)));"), "two words");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[\"two words\"]", output);
    }
}
