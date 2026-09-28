using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealCpp;

/// <summary>.cpp files compiled and run with real C++ compiler (clang++ / g++): build step, binary execution, and diagnostics.</summary>
[Collection(RealCppCollection.Name)]
public class CppProgramRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_CppRun_" + Guid.NewGuid().ToString("N"));

    public CppProgramRunTests()
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
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var language = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, cpp));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [CppFact]
    public void TheInstalledCompiler_IsFound()
    {
        var cpp = TestCpp.Require();

        Assert.True(cpp.Version >= CppToolchainProvider.MinimumVersion, cpp.Label);
        Assert.True(File.Exists(cpp.ExecutablePath), cpp.ExecutablePath);
    }

    [CppFact]
    public async Task AFile_CompilesAndRunsAndPrints()
    {
        var path = Write("hello.cpp", """
            #include <iostream>

            int main() {
                std::cout << "Hello from C++ run test!" << std::endl;
                return 0;
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("Hello from C++ run test!", text);
    }

    [CppFact]
    public async Task AFailedCompileFile_StopsAtBuildStep_AndPopulatesProblems()
    {
        var path = Write("bad.cpp", """
            #include <iostream>

            int main() {
                int x = undefined_variable_here;
                return 0;
            }
            """);
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("Compile", result.FailedBuildStep);
        Assert.NotEmpty(result.Diagnostics.Diagnostics);
        var diag = result.Diagnostics.Diagnostics[0];
        Assert.Equal(4, diag.Line);
        Assert.Contains("undefined_variable_here", diag.Message);
    }

    [CppFact]
    public async Task AProgramWithCin_CanReceiveStandardInput()
    {
        var path = Write("interactive.cpp", """
            #include <iostream>
            #include <string>

            int main() {
                std::string name;
                if (std::cin >> name) {
                    std::cout << "Welcome, " << name << "!" << std::endl;
                }
                return 0;
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

    [CppFact]
    public async Task AProgramWithDisplayRuntime_EmitsInteractiveTableJson()
    {
        var path = Write("display_test.cpp", """
            #include <iostream>
            #include <vector>
            #include <fry/display.hpp>

            int main() {
                std::vector<int> squares = { 1, 4, 9, 16 };
                fry::dump(squares, "Squares");
                return 0;
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        var text = string.Concat(output);
        Assert.Contains("__FRY_DISPLAY__", text);
        Assert.Contains("application/vnd.fry.table+json", text);
        Assert.Contains("Squares", text);
        Assert.Contains("[0,1]", text);
        Assert.Contains("[3,16]", text);
    }

    [CppFact]
    public async Task ANotebookCell_ExecutesWithRealCompiler_AndPrintsOutput()
    {
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var language = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;

        var context = new PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.KernelCreationContext(() => _dir);
        using var kernel = language.NotebookKernels!.Create(context);

        var console = new System.Text.StringBuilder();
        var request = new PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.KernelExecutionRequest
        {
            Code = """
                std::vector<int> nums = { 10, 20, 30 };
                for (int n : nums) {
                    std::cout << "item: " << n << std::endl;
                }
                """,
            OnConsole = line => console.Append(line)
        };

        var result = await kernel.ExecuteAsync(request, default);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("item: 10", console.ToString());
        Assert.Contains("item: 20", console.ToString());
        Assert.Contains("item: 30", console.ToString());
    }
}
