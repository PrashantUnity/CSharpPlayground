using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoNotebookKernelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeHostEnvironment _host;
    private readonly GoToolchainProvider _toolchain;

    public GoNotebookKernelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_GoNotebookTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _host = new FakeHostEnvironment(FakeOs.MacOS);
        _host.AddGo("/opt/homebrew/bin/go", "1.22.4");
        _toolchain = new GoToolchainProvider(_host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_tempDir, "tc.json")), _tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task SharedVariables_SetAndGetRoundTrip()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("myInt", "42", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myStr", "\"hello go\"", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myBool", "true", CancellationToken.None);

        var intVal = await kernel.GetValueJsonAsync("myInt", CancellationToken.None);
        var strVal = await kernel.GetValueJsonAsync("myStr", CancellationToken.None);
        var boolVal = await kernel.GetValueJsonAsync("myBool", CancellationToken.None);

        Assert.Equal("42", intVal);
        Assert.Equal("\"hello go\"", strVal);
        Assert.Equal("true", boolVal);

        var vars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Equal(3, vars.Count);
        Assert.Contains(vars, v => v.Name == "myInt" && v.TypeName == "int64");
        Assert.Contains(vars, v => v.Name == "myStr" && v.TypeName == "string");
        Assert.Contains(vars, v => v.Name == "myBool" && v.TypeName == "bool");
    }

    [Fact]
    public async Task MissingVariable_ThrowsKernelValueException()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await Assert.ThrowsAsync<KernelValueException>(async () =>
        {
            await kernel.GetValueJsonAsync("nonExistent", CancellationToken.None);
        });
    }

    [Fact]
    public async Task HardReset_ClearsVariablesAndState()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("count", "10", CancellationToken.None);
        Assert.True(kernel.IsSessionActive);

        kernel.HardReset();
        Assert.False(kernel.IsSessionActive);
    }
}
