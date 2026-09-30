using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealGo;

/// <summary>.go files compiled and run with real Go compiler: build step, binary execution, and diagnostics.</summary>
[Collection(RealGoCollection.Name)]
public class GoProgramRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_GoRun_" + Guid.NewGuid().ToString("N"));

    public GoProgramRunTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
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
        var go = TestGo.Require();
        var services = TestGo.Services(Path.Combine(_dir, ".studio"));
        var language = (GoLanguage)services.Registry.Get(LanguageIds.Go)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, go));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [GoFact]
    public void TheInstalledGo_IsFound()
    {
        var go = TestGo.Require();

        Assert.True(go.Version >= GoToolchainProvider.MinimumVersion, go.Label);
        Assert.True(File.Exists(go.ExecutablePath), go.ExecutablePath);
    }

    [GoFact]
    public async Task AFile_CompilesAndRunsAndPrints()
    {
        var path = Write("hello.go", """
            package main

            import "fmt"

            func main() {
                fmt.Println("Hello from Go run test!")
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Join("\n", output);
        Assert.Contains("Hello from Go run test!", joined);
    }

    [GoFact]
    public async Task GoroutinesAndChannels_RunConcurrently()
    {
        var path = Write("channels.go", """
            package main

            import "fmt"

            func sum(s []int, c chan int) {
                sum := 0
                for _, v := range s {
                    sum += v
                }
                c <- sum
            }

            func main() {
                s := []int{7, 2, 8, -9, 4, 0}
                c := make(chan int)
                go sum(s[:len(s)/2], c)
                go sum(s[len(s)/2:], c)
                x, y := <-c, <-c
                fmt.Printf("SUM=%d\n", x+y)
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Join("\n", output);
        Assert.Contains("SUM=12", joined);
    }

    [GoFact]
    public async Task CompilationError_IsParsedIntoDiagnostics()
    {
        var path = Write("broken.go", """
            package main

            func main() {
                undefinedSymbol()
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Diagnostics.Diagnostics);
        var diag = result.Diagnostics.Diagnostics[0];
        Assert.Equal(4, diag.Line);
        Assert.Contains("undefinedSymbol", diag.Message);
    }

    [GoFact]
    public async Task AProgramWithScanln_CanReceiveStandardInput()
    {
        var path = Write("interactive.go", """
            package main

            import "fmt"

            func main() {
                var name string
                if _, err := fmt.Scanln(&name); err == nil {
                    fmt.Printf("Welcome, %s!\n", name)
                }
            }
            """);
        var (session, output) = await Start(path);

        await session.SendInputAsync("Antigravity\n");

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("Welcome, Antigravity!", text);
    }
}
