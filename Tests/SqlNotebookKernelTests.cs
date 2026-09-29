using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class SqlNotebookKernelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeHostEnvironment _host;
    private readonly SqlToolchainProvider _toolchain;

    public SqlNotebookKernelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_SqlNotebookTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _host = new FakeHostEnvironment(FakeOs.MacOS);
        _host.AddSql("/usr/bin/sqlite3", "3.51.0");
        _toolchain = new SqlToolchainProvider(_host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_tempDir, "tc.json")));
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
    public async Task SharedVariables_SetPrimitives_CanRetrieveThem()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new SqlNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("myInt", "42", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myStr", "\"hello sql\"", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myBool", "true", CancellationToken.None);

        var intVal = await kernel.GetValueJsonAsync("myInt", CancellationToken.None);
        var strVal = await kernel.GetValueJsonAsync("myStr", CancellationToken.None);
        var boolVal = await kernel.GetValueJsonAsync("myBool", CancellationToken.None);

        Assert.Equal("42", intVal);
        Assert.Equal("\"hello sql\"", strVal);
        Assert.Equal("true", boolVal);

        var vars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Equal(3, vars.Count);
        Assert.Contains(vars, v => v.Name == "myInt");
        Assert.Contains(vars, v => v.Name == "myStr");
        Assert.Contains(vars, v => v.Name == "myBool");
    }

    [Fact]
    public async Task SharedVariables_SetArray_IsTrackedInVariables()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new SqlNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        // An array of objects from Python / other kernels
        var json = "[{\"id\":1,\"name\":\"Alice\"},{\"id\":2,\"name\":\"Bob\"}]";
        await kernel.SetValueFromJsonAsync("shared_users", json, CancellationToken.None);

        // The kernel tracks it as a shared variable
        var vars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Contains(vars, v => v.Name == "shared_users");

        // The session should be active (we have a shared variable)
        Assert.True(kernel.IsSessionActive);
    }

    [Fact]
    public async Task MissingVariable_ThrowsKernelValueException()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new SqlNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await Assert.ThrowsAsync<KernelValueException>(async () =>
        {
            await kernel.GetValueJsonAsync("nonExistent", CancellationToken.None);
        });
    }

    [Fact]
    public async Task HardReset_ClearsVariablesAndSession()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new SqlNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("counter", "10", CancellationToken.None);
        Assert.True(kernel.IsSessionActive);

        kernel.HardReset();
        Assert.False(kernel.IsSessionActive);
    }
}
