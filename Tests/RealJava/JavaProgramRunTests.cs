using System.Collections.Concurrent;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJava;

/// <summary>.java files compiled and run with real JDK: javac build step, java execution, and diagnostics.</summary>
[Collection(RealJavaCollection.Name)]
public class JavaProgramRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaRun_" + Guid.NewGuid().ToString("N"));

    public JavaProgramRunTests()
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
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var language = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, java));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [JavaFact]
    public void TheInstalledJdk_IsFound()
    {
        var java = TestJava.Require();

        Assert.True(java.Version >= JavaToolchainProvider.MinimumVersion, java.Label);
        Assert.True(File.Exists(java.ExecutablePath), java.ExecutablePath);
    }

    [JavaFact]
    public async Task AFile_CompilesAndRunsAndPrints()
    {
        var path = Write("Hello.java", """
            public class Hello {
                public static void main(String[] args) {
                    System.out.println("Hello from Java run test!");
                }
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("Hello from Java run test!", text);
    }

    [JavaFact]
    public async Task AFailedCompileFile_StopsAtBuildStep_AndPopulatesProblems()
    {
        var path = Write("Bad.java", """
            public class Bad {
                public static void main(String[] args) {
                    int x = undefinedVariable;
                }
            }
            """);
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("Compile", result.FailedBuildStep);
        Assert.NotEmpty(result.Diagnostics.Diagnostics);
        var diag = result.Diagnostics.Diagnostics[0];
        Assert.Equal("JAVAC", diag.Id);
        Assert.Equal(3, diag.Line);
    }

    [JavaFact]
    public async Task AScriptWithMismatchedPublicClassName_CompilesAndRunsSeamlessly()
    {
        var path = Write("script_123456.java", """
            public class Quicksort {
                public static void main(String[] args) {
                    System.out.println("Quicksort running inside script_123456.java!");
                }
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("Quicksort running inside script_123456.java!", text);
    }
}
