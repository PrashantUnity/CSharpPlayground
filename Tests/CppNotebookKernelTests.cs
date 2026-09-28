using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CppNotebookKernelTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CppNb_" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public void Properties_AreExpected()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var context = new KernelCreationContext(() => host.HomeDirectory);
        using var kernel = new CppNotebookKernel(toolchain, processes, host, context);

        Assert.Equal(LanguageIds.Cpp, kernel.LanguageId);
        Assert.True(kernel.CanForceStop);
        Assert.False(kernel.IsSessionActive);
    }

    [Fact]
    public async Task SetValueFromJson_StoresAndSharesVariable()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var context = new KernelCreationContext(() => host.HomeDirectory);
        using var kernel = new CppNotebookKernel(toolchain, processes, host, context);

        await kernel.SetValueFromJsonAsync("count", "42", default);
        await kernel.SetValueFromJsonAsync("pi", "3.14159", default);
        await kernel.SetValueFromJsonAsync("name", "\"Antigravity\"", default);

        Assert.True(kernel.IsSessionActive);

        var countJson = await kernel.GetValueJsonAsync("count", default);
        Assert.Equal("42", countJson);

        var vars = await kernel.GetVariablesAsync(default);
        Assert.Equal(3, vars.Count);
        Assert.Contains(vars, v => v.Name == "count" && v.TypeName == "long long" && v.Kernel == "C++");
        Assert.Contains(vars, v => v.Name == "pi" && v.TypeName == "double" && v.Kernel == "C++");
        Assert.Contains(vars, v => v.Name == "name" && v.TypeName == "std::string" && v.Kernel == "C++");
    }

    [Fact]
    public async Task HardReset_ClearsSessionState()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var context = new KernelCreationContext(() => host.HomeDirectory);
        using var kernel = new CppNotebookKernel(toolchain, processes, host, context);

        await kernel.SetValueFromJsonAsync("x", "100", default);
        Assert.True(kernel.IsSessionActive);

        kernel.HardReset();
        Assert.False(kernel.IsSessionActive);
        var vars = await kernel.GetVariablesAsync(default);
        Assert.Empty(vars);
    }

    [Fact]
    public async Task UnknownVariable_ThrowsKernelValueException()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var context = new KernelCreationContext(() => host.HomeDirectory);
        using var kernel = new CppNotebookKernel(toolchain, processes, host, context);

        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("non_existent", default));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompilerMissing_ReturnsErrorWithGuidance()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var context = new KernelCreationContext(() => host.HomeDirectory);
        using var kernel = new CppNotebookKernel(toolchain, processes, host, context);

        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "std::cout << 42 << std::endl;"
        }, default);

        Assert.False(result.Success);
        Assert.Contains("brew install llvm", result.ConsoleOutput);
    }
}
