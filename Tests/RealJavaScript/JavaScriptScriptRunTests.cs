using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealJavaScript;

/// <summary>.js files run with real Node.js: output, exit code, and tracebacks.</summary>
[Collection(RealJavaScriptCollection.Name)]
public class JavaScriptScriptRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JSRun_" + Guid.NewGuid().ToString("N"));

    public JavaScriptScriptRunTests()
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
        var node = TestJavaScript.Require();
        var services = TestJavaScript.Services(Path.Combine(_dir, ".studio"));
        var language = (JavaScriptLanguage)services.Registry.Get(LanguageIds.JavaScript)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, node));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [JavaScriptFact]
    public void TheInstalledNode_IsFound()
    {
        var node = TestJavaScript.Require();

        Assert.True(node.Version >= JavaScriptToolchainProvider.MinimumVersion, node.Label);
        Assert.True(File.Exists(node.ExecutablePath), node.ExecutablePath);
    }

    [JavaScriptFact]
    public async Task AFile_RunsAndPrints()
    {
        var path = Write("hello.js", "console.log('Hello from JS run test!');\n");
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("Hello from JS run test!", text);
    }

    [JavaScriptFact]
    public async Task AFailedFile_HasExitCode_AndProblemsDiagnostics()
    {
        var path = Write("fail.js", "throw new Error('Boom from JS test!');\n");
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Succeeded);
        var diag = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal("Error", diag.Id);
        Assert.Contains("Boom from JS test!", diag.Message);
        Assert.Equal(1, diag.Line);
    }
}
