using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

[Collection(ExtensionTestsCollection.Name)]
public class ZigExtensionTests : IDisposable
{
    private readonly string _extensionPath;
    private readonly ExtensionManager _manager = new();

    public ZigExtensionTests()
    {
        try { StudioAppContext.Instance.Languages.Unregister("zig"); } catch { }

        string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".frysharp", "cache", "packages", "git", "github.com-prashantunity-frysharp.zig", "v1.0.0");
        if (Directory.Exists(cacheDir) && File.Exists(Path.Combine(cacheDir, "extension.json")))
        {
            _extensionPath = cacheDir;
        }
        else
        {
            var pkgService = new GitPackageService();
            var url = GitPackageUrl.Parse("https://github.com/PrashantUnity/FrySharp.Zig.git#v1.0.0");
            var res = pkgService.ResolveAndDownloadAsync(url).GetAwaiter().GetResult();
            _extensionPath = res.ExtensionDirectory;
        }
    }

    public void Dispose()
    {
        try { _manager.Dispose(); } catch { }
        try { StudioAppContext.Instance.Languages.Unregister("zig"); } catch { }
    }

    [Fact]
    public void ZigExtension_DirectoryAndManifest_Exist()
    {
        Assert.True(Directory.Exists(_extensionPath), $"Extension folder not found at: {_extensionPath}");
        var manifestPath = Path.Combine(_extensionPath, "extension.json");
        Assert.True(File.Exists(manifestPath), $"extension.json not found at: {manifestPath}");
    }

    [Fact]
    public async Task ZigExtension_CompilesAndLoadsViaExtensionManager()
    {
        var result = await _manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);

        Assert.True(result.Success, $"Load failed: {result.ErrorMessage} - Diags: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Extension);
        Assert.Equal("zig-support", result.ExtensionId);

        // Verify Zig registered in Studio SDK
        var app = StudioAppContext.Instance;
        var zig = app.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.Equal("Zig", zig.DisplayName);
        Assert.Equal("ZIG", zig.ShortName);
        Assert.Contains(".zig", zig.FileExtensions);
        Assert.Contains(".zon", zig.FileExtensions);
        Assert.Equal("#F7A41D", zig.AccentHex);

        // Verify capabilities
        Assert.True(zig.Has(LanguageCapabilities.Completion));
        Assert.True(zig.Has(LanguageCapabilities.QuickInfo));
        Assert.True(zig.Has(LanguageCapabilities.StandardInput));
        Assert.True(zig.Has(LanguageCapabilities.LiveDiagnostics));
        Assert.True(zig.Has(LanguageCapabilities.NotebookCells));

        // Subsystems populated
        Assert.NotNull(zig.RunDiagnostics);
        Assert.NotNull(zig.ScriptRunner);
        Assert.NotNull(zig.Toolchain);
        Assert.NotNull(zig.NotebookKernels);
    }

    [Fact]
    public async Task ZigExtension_ResolvesAndLoadsDirectlyFromGitHub()
    {
        var pkgService = new GitPackageService();
        var url = GitPackageUrl.Parse("https://github.com/PrashantUnity/FrySharp.Zig.git#v1.0.0");
        var result = await pkgService.ResolveAndDownloadAsync(url, forceRefresh: true);

        Assert.True(result.Success, $"Failed to resolve from GitHub: {result.ErrorMessage}");
        Assert.NotNull(result.Manifest);
        Assert.Equal("zig-support", result.Manifest.Id);
        Assert.True(Directory.Exists(result.ExtensionDirectory));
        Assert.True(File.Exists(Path.Combine(result.ExtensionDirectory, "extension.json")));

        using var isolateManager = new ExtensionManager();
        var loadResult = await isolateManager.LoadExtensionAsync(result.ExtensionDirectory, enableHotReload: false);

        Assert.True(loadResult.Success, $"Failed to load GitHub extension: {loadResult.ErrorMessage}");
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.Equal("Zig", zig.DisplayName);
        Assert.Equal("#F7A41D", zig.AccentHex);
    }

    [Fact]
    public async Task ZigDiagnosticParser_ParsesCompilerErrorsAndWarnings()
    {
        await EnsureZigLoadedAsync();
        var app = StudioAppContext.Instance;
        var zig = app.Languages.Get("zig");
        Assert.NotNull(zig);
        var parser = zig.RunDiagnostics;
        Assert.NotNull(parser);

        string sampleOutput = """
            src/main.zig:12:5: error: expected ';' after statement
            src/main.zig:15:9: warning: local variable 'x' is never mutated
            src/main.zig:18:1: note: declared here
            """;
        var diagResult = parser.Parse(sampleOutput, "/projects/src/main.zig");
        Assert.Equal(3, diagResult.Diagnostics.Count);
        Assert.Equal(DiagnosticSeverity.Error, diagResult.Diagnostics[0].Severity);
        Assert.Equal(12, diagResult.Diagnostics[0].Line);
        Assert.Equal(5, diagResult.Diagnostics[0].Column);
        Assert.Contains("expected ';'", diagResult.Diagnostics[0].Message);
    }

    [Fact]
    public async Task ZigToolchain_DiscoversLocalCompiler_OrProvidesGuidance()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.NotNull(zig.Toolchain);

        var query = new ToolchainQuery(Directory.GetCurrentDirectory());
        var resolution = await zig.Toolchain.ResolveAsync(query, CancellationToken.None);

        if (resolution.IsFound && resolution.Toolchain != null)
        {
            Assert.Equal("zig", resolution.Toolchain.LanguageId);
            Assert.True(File.Exists(resolution.Toolchain.ExecutablePath));
            Assert.NotNull(resolution.Toolchain.Version);
        }
        else
        {
            Assert.NotNull(resolution.Missing);
            Assert.Contains("Zig", resolution.Missing.Title);
            Assert.NotEmpty(resolution.Missing.Steps);
        }
    }

    [Fact]
    public async Task ZigScriptRunner_PlansAndExecutesFile_WithProcessOutput()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.NotNull(zig.ScriptRunner);
        Assert.NotNull(zig.Toolchain);
        Assert.NotNull(zig.RunDiagnostics);

        var tempDir = Path.Combine(Path.GetTempPath(), "FryStudio_ZigScriptTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var scriptPath = Path.Combine(tempDir, "script.zig");
            string scriptCode = """
                const std = @import("std");

                pub fn main() void {
                    std.debug.print("Hello from Zig ScriptRunner Test! 100\n", .{});
                }
                """;
            await File.WriteAllTextAsync(scriptPath, scriptCode);

            var query = new ToolchainQuery(tempDir);
            var resolution = await zig.Toolchain.ResolveAsync(query, CancellationToken.None);

            if (resolution.IsFound && resolution.Toolchain != null)
            {
                var runContext = new ScriptRunContext(scriptPath, tempDir, resolution.Toolchain);
                var plan = await zig.ScriptRunner.PlanAsync(runContext);

                Assert.Single(plan.Steps);
                var step = plan.Steps[0];
                Assert.Equal(resolution.Toolchain.ExecutablePath, step.Spec.FileName);
                Assert.Equal(["run", scriptPath], step.Spec.Arguments);

                // Live process execution
                var outputQueue = new ConcurrentQueue<string>();
                var processes = new ProcessLauncher();
                var executor = new ScriptRunExecutor(processes);
                var session = executor.Start(plan, scriptPath, zig.RunDiagnostics, text => outputQueue.Enqueue(text));

                var result = await session.Completion;
                string allOutput = string.Join("", outputQueue);

                Assert.True(result.Succeeded, $"Script execution failed. Output: {allOutput}");
                Assert.Contains("Hello from Zig ScriptRunner Test! 100", allOutput);
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ZigNotebookKernel_ExecutesCellAndCapturesOutput()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.True(zig.Has(LanguageCapabilities.NotebookCells));
        Assert.NotNull(zig.NotebookKernels);

        using var kernel = zig.NotebookKernels.Create(new KernelCreationContext(() => Path.GetTempPath()));
        Assert.NotNull(kernel);
        Assert.Equal("zig", kernel.LanguageId);
        Assert.True(kernel.CanForceStop);

        var output = new System.Text.StringBuilder();
        var request = new KernelExecutionRequest
        {
            Code = """
                std.debug.print("Zig Notebook Cell Output: {d}\n", .{42});
                """,
            OnConsole = text => output.Append(text)
        };

        var result = await kernel.ExecuteAsync(request, CancellationToken.None);
        Assert.True(result.Success, $"Execution failed: {result.ErrorMessage}");
        Assert.Contains("Zig Notebook Cell Output: 42", output.ToString());
    }

    [Fact]
    public async Task ZigNotebookKernel_ExecutesFullProgramWithMain()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.NotNull(zig.NotebookKernels);

        using var kernel = zig.NotebookKernels.Create(new KernelCreationContext(() => Path.GetTempPath()));
        Assert.NotNull(kernel);

        var output = new System.Text.StringBuilder();
        var request = new KernelExecutionRequest
        {
            Code = """
                const std = @import("std");

                pub fn main() void {
                    const x: i32 = 123;
                    const y: i32 = 456;
                    std.debug.print("SUM: {d}\n", .{x + y});
                }
                """,
            OnConsole = text => output.Append(text)
        };

        var result = await kernel.ExecuteAsync(request, CancellationToken.None);
        Assert.True(result.Success, $"Execution failed: {result.ErrorMessage}");
        Assert.Contains("SUM: 579", output.ToString());
    }

    [Fact]
    public async Task ZigNotebookKernel_HandlesCompilationErrors_Gracefully()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.NotNull(zig.NotebookKernels);

        using var kernel = zig.NotebookKernels.Create(new KernelCreationContext(() => Path.GetTempPath()));
        Assert.NotNull(kernel);

        var output = new System.Text.StringBuilder();
        var request = new KernelExecutionRequest
        {
            Code = """
                this is completely invalid zig syntax @@!!$$;
                """,
            OnConsole = text => output.Append(text)
        };

        var result = await kernel.ExecuteAsync(request, CancellationToken.None);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ZigNotebookKernel_ManagesVariableSharing()
    {
        await EnsureZigLoadedAsync();
        var zig = StudioAppContext.Instance.Languages.Get("zig");
        Assert.NotNull(zig);
        Assert.NotNull(zig.NotebookKernels);

        using var kernel = zig.NotebookKernels.Create(new KernelCreationContext(() => Path.GetTempPath()));
        Assert.NotNull(kernel);

        await kernel.SetValueFromJsonAsync("zigVar", "{\"score\": 99, \"player\": \"ZigHero\"}", CancellationToken.None);
        var json = await kernel.GetValueJsonAsync("zigVar", CancellationToken.None);
        Assert.Contains("99", json);
        Assert.Contains("ZigHero", json);

        var vars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Single(vars);
        Assert.Equal("zigVar", vars[0].Name);

        kernel.HardReset();
        var resetVars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Empty(resetVars);
    }

    private async Task EnsureZigLoadedAsync()
    {
        var app = StudioAppContext.Instance;
        var zig = app.Languages.Get("zig");
        if (zig == null || !zig.Has(LanguageCapabilities.NotebookCells))
        {
            var res = await _manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
            Assert.True(res.Success, $"Failed to load extension: {res.ErrorMessage}");
        }
    }
}
