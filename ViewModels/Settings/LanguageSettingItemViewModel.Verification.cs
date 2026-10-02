using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public partial class LanguageSettingItemViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestButtonLabel))]
    [NotifyPropertyChangedFor(nameof(TestButtonFullLabel))]
    private bool _isTestingHelloWorld;

    [ObservableProperty]
    private bool _hasHelloWorldResult;

    [ObservableProperty]
    private bool _isHelloWorldSuccess;

    [ObservableProperty]
    private string _helloWorldStatusBadge = string.Empty;

    [ObservableProperty]
    private string _helloWorldStatusColor = "#4EBA6F";

    [ObservableProperty]
    private string _helloWorldExecutionMessage = string.Empty;

    [ObservableProperty]
    private string _helloWorldOutput = string.Empty;

    [ObservableProperty]
    private string _helloWorldEngineDetails = string.Empty;

    [ObservableProperty]
    private string _helloWorldElapsedText = string.Empty;

    public string TestButtonLabel => IsTestingHelloWorld ? "Testing…" : "Test Runtime";

    public string TestButtonFullLabel => IsTestingHelloWorld ? "Testing…" : "Run Hello World Check";

    [RelayCommand]
    public async Task TestHelloWorldAsync()
    {
        if (IsTestingHelloWorld) return;

        IsTestingHelloWorld = true;
        HasHelloWorldResult = true;
        IsHelloWorldSuccess = false;
        HelloWorldStatusBadge = "Running…";
        HelloWorldStatusColor = "#E3B341";
        HelloWorldExecutionMessage = $"Testing {DisplayName} execution with Hello World…";
        HelloWorldOutput = string.Empty;
        HelloWorldEngineDetails = string.Empty;
        HelloWorldElapsedText = string.Empty;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Run(async () =>
            {
                if (IsCSharp)
                {
                    await TestCSharpAsync(stopwatch);
                }
                else
                {
                    await TestExternalLanguageAsync(stopwatch);
                }
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            SetResult(false, "Test Failed", "#F85149", $"Error: {ex.Message}", ex.ToString(), stopwatch.Elapsed);
        }
        finally
        {
            void Finish() => IsTestingHelloWorld = false;
            if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Finish();
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(Finish);
            }
        }
    }

    [RelayCommand]
    public void ClearHelloWorldResult()
    {
        HasHelloWorldResult = false;
        HelloWorldOutput = string.Empty;
        HelloWorldExecutionMessage = string.Empty;
        HelloWorldStatusBadge = string.Empty;
        HelloWorldElapsedText = string.Empty;
        HelloWorldEngineDetails = string.Empty;
    }

    private async Task TestCSharpAsync(Stopwatch sw)
    {
        if (IsInProcessRoslynSelected)
        {
            HelloWorldEngineDetails = "Roslyn In-Memory (.NET 10)";
            const string code = "Console.WriteLine(\"Hello from FryPDF!\");";
            var compiler = new RoslynCompilerService();
            var (compileOk, assemblyBytes, diagnostics) = compiler.CompileToAssembly(code, ExecutionLanguageMode.Statements);
            if (!compileOk || assemblyBytes == null)
            {
                sw.Stop();
                var errText = string.Join("\n", diagnostics.Select(d => $"[{d.Severity}] Line {d.Line}: {d.Message}"));
                SetResult(false, "Compile Error", "#F85149", "Roslyn in-memory compilation failed.", errText, sw.Elapsed);
                return;
            }

            var execEngine = new ScriptExecutionEngine();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var outSb = new StringBuilder();
            var execResult = await execEngine.ExecuteAsync(assemblyBytes, s => outSb.Append(s), cts.Token);
            sw.Stop();

            var output = !string.IsNullOrWhiteSpace(execResult.Output)
                ? execResult.Output.Trim()
                : (!string.IsNullOrWhiteSpace(execResult.Error) ? execResult.Error.Trim() : outSb.ToString().Trim());

            var success = execResult.Success && !execResult.WasCancelled;
            var badge = success ? "Passed" : (execResult.WasCancelled ? "Timed out" : "Execution Error");
            var color = success ? "#4EBA6F" : "#F85149";
            var msg = success
                ? "Hello World executed successfully via In-Process Roslyn."
                : (!string.IsNullOrWhiteSpace(execResult.Error) ? execResult.Error : "Execution failed or timed out.");

            SetResult(success, badge, color, msg, output, sw.Elapsed);
        }
        else
        {
            HelloWorldEngineDetails = "External .NET SDK CLI";
            var provider = DotNetProvider;
            if (provider == null)
            {
                sw.Stop();
                SetResult(false, "No .NET Provider", "#F85149", ".NET SDK toolchain provider is not available.", "No .NET toolchain provider.", sw.Elapsed);
                return;
            }

            var res = await provider.ResolveAsync(new ToolchainQuery(null, null));
            if (res.Toolchain is not { } sdk)
            {
                sw.Stop();
                SetResult(false, ".NET SDK Missing", "#F85149", "No .NET SDK discovered on machine.", res.Missing?.Summary ?? "Please install .NET SDK or use In-Process Roslyn.", sw.Elapsed);
                return;
            }

            HelloWorldEngineDetails = $"External .NET SDK ({sdk.Version})";
            var host = ParentSettings?.LanguageServices.Host ?? new HostEnvironment();
            var processes = ParentSettings?.LanguageServices.Processes ?? new ProcessLauncher();
            var runner = new CSharpBuildAndRunScriptRunner(host);

            var tempDir = Path.Combine(Path.GetTempPath(), "FryStudio", "verification", "csharp");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, "Program.cs");
            await File.WriteAllTextAsync(tempFile, "Console.WriteLine(\"Hello from FryPDF!\");");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var plan = await runner.PlanAsync(new ScriptRunContext(tempFile, tempDir, sdk), cts.Token);
            var executor = new ScriptRunExecutor(processes);
            var outSb = new StringBuilder();
            var session = executor.Start(plan, tempFile, Language.RunDiagnostics, s => outSb.Append(s), cts.Token);
            var result = await session.Completion;
            sw.Stop();

            var success = result.ExitCode == 0 && result.StartError == null && result.FailedBuildStep == null && !result.WasCancelled;
            var badge = success ? "Passed" : (result.WasCancelled ? "Timed out" : $"Failed (Exit {result.ExitCode})");
            var color = success ? "#4EBA6F" : "#F85149";
            var msg = success
                ? $"Hello World compiled and ran via .NET SDK {sdk.Version}."
                : (result.StartError ?? result.FailedBuildStep ?? $"Process exited with code {result.ExitCode}");

            SetResult(success, badge, color, msg, outSb.ToString().Trim(), sw.Elapsed);
        }
    }

    private async Task TestExternalLanguageAsync(Stopwatch sw)
    {
        var provider = Provider ?? Language.Toolchain;
        if (provider == null && Language.ScriptRunner == null)
        {
            sw.Stop();
            SetResult(false, "No Runner", "#E3B341", $"{DisplayName} does not support standalone script execution.", "No toolchain or script runner configured.", sw.Elapsed);
            return;
        }

        ToolchainInfo? toolchain = null;
        if (provider != null)
        {
            var res = await provider.ResolveAsync(new ToolchainQuery(null, null));
            toolchain = res.Toolchain;
            if (toolchain == null)
            {
                sw.Stop();
                var missingMsg = res.Missing?.Summary ?? $"{DisplayName} compiler/runtime was not detected.";
                SetResult(false, "Not Detected", "#F85149", missingMsg, "Please install the toolchain or specify executable path above.", sw.Elapsed);
                return;
            }
        }

        var runner = Language.ScriptRunner;
        if (runner == null || toolchain == null)
        {
            sw.Stop();
            SetResult(false, "No Runner", "#E3B341", $"{DisplayName} has no script runner implemented.", "No runner available.", sw.Elapsed);
            return;
        }

        HelloWorldEngineDetails = $"{toolchain.Label}";

        var ext = Language.FileExtensions.FirstOrDefault() ?? ".txt";
        var tempDir = Path.Combine(Path.GetTempPath(), "FryStudio", "verification", Language.Id);
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"hello_world{ext}");

        var code = Language.NewFileTemplate;
        if (string.IsNullOrWhiteSpace(code))
        {
            code = GetFallbackTemplate(Language.Id);
        }
        await File.WriteAllTextAsync(tempFile, code);

        var processes = ParentSettings?.LanguageServices.Processes ?? new ProcessLauncher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var plan = await runner.PlanAsync(new ScriptRunContext(tempFile, tempDir, toolchain), cts.Token);
        var executor = new ScriptRunExecutor(processes);
        var outSb = new StringBuilder();
        var session = executor.Start(plan, tempFile, Language.RunDiagnostics, s => outSb.Append(s), cts.Token);
        var result = await session.Completion;
        sw.Stop();

        var success = result.ExitCode == 0 && result.StartError == null && result.FailedBuildStep == null && !result.WasCancelled;
        var badge = success ? "Passed" : (result.WasCancelled ? "Timed out" : $"Failed (Exit {result.ExitCode})");
        var color = success ? "#4EBA6F" : "#F85149";
        var msg = success
            ? $"Hello World executed successfully with {toolchain.Label}."
            : (result.StartError ?? result.FailedBuildStep ?? $"Process exited with code {result.ExitCode}");

        SetResult(success, badge, color, msg, outSb.ToString().Trim(), sw.Elapsed);
    }

    private void SetResult(bool success, string badge, string color, string message, string output, TimeSpan elapsed)
    {
        void Apply()
        {
            IsHelloWorldSuccess = success;
            HelloWorldStatusBadge = badge;
            HelloWorldStatusColor = color;
            HelloWorldExecutionMessage = message;
            HelloWorldOutput = output;
            HelloWorldElapsedText = elapsed.TotalMilliseconds < 1000
                ? $"{elapsed.TotalMilliseconds:F0} ms"
                : $"{elapsed.TotalSeconds:F2} s";
            HasHelloWorldResult = true;
        }

        if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    private static string GetFallbackTemplate(string languageId) => languageId switch
    {
        LanguageIds.CSharp => "Console.WriteLine(\"Hello from FryPDF!\");",
        LanguageIds.Python => "print(\"Hello from Python!\")",
        LanguageIds.JavaScript => "console.log(\"Hello from JavaScript!\");",
        LanguageIds.Java => "public class Main { public static void main(String[] args) { System.out.println(\"Hello from Java!\"); } }",
        LanguageIds.Cpp => "#include <iostream>\nint main() { std::cout << \"Hello from C++!\" << std::endl; return 0; }",
        LanguageIds.Go => "package main\nimport \"fmt\"\nfunc main() { fmt.Println(\"Hello from Go!\") }",
        LanguageIds.Rust => "fn main() { println!(\"Hello from Rust!\"); }",
        LanguageIds.FSharp => "printfn \"Hello from F#!\"",
        LanguageIds.Sql => "SELECT 'Hello from SQL!' AS greeting;",
        _ => "// Hello World\n"
    };
}
