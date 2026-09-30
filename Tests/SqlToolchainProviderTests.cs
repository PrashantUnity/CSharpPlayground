using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class SqlToolchainProviderTests : IDisposable
{
    private readonly string _tempDir;

    public SqlToolchainProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_SqlToolchainTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
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

    private SqlToolchainProvider CreateProvider(FakeHostEnvironment host, IProcessLauncher? launcher = null)
    {
        var settingsStore = new ToolchainSettingsStore(Path.Combine(_tempDir, "toolchains.json"));
        return new SqlToolchainProvider(host, launcher ?? new FakeProcessLauncher(), settingsStore);
    }

    [Fact]
    public async Task OnMacOS_DiscoversSqlite3FromStandardPaths()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/usr/bin:/bin";
        host.AddSql("/usr/bin/sqlite3", "3.51.0");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/usr/bin/sqlite3", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(3, 51, 0), resolution.Toolchain.Version);
        Assert.Contains("SQLite", resolution.Toolchain.DisplayName);
    }

    [Fact]
    public async Task OnLinux_DiscoversSqlite3FromPath()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/bin:/bin";
        host.AddSql("/usr/bin/sqlite3", "3.42.0");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/usr/bin/sqlite3", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(3, 42, 0), resolution.Toolchain.Version);
    }

    [Fact]
    public async Task OnWindows_DiscoversSqlite3FromProgramFiles()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.AddSql(@"C:\Program Files\SQLite\sqlite3.exe", "3.45.0");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal(new Version(3, 45, 0), resolution.Toolchain.Version);
    }

    [Fact]
    public async Task SQLITE3_PATH_EnvVar_TakesPrecedence()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["SQLITE3_PATH"] = "/opt/custom/sqlite3";
        host.AddSql("/opt/custom/sqlite3", "3.50.0");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/opt/custom/sqlite3", resolution.Toolchain.ExecutablePath);
    }

    [Fact]
    public async Task MissingToolchain_ReturnsGuidanceWithInstallActions()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Contains("SQLite", resolution.Missing.Summary);
        Assert.NotEmpty(resolution.Missing.Steps);
    }

    [Fact]
    public async Task MissingToolchain_OnLinux_SuggestsAptGetOrSqlite3()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Contains(resolution.Missing.Steps, s => s.Contains("apt") || s.Contains("sqlite3"));
    }

    [Fact]
    public async Task ExplicitlySelectedPath_TakesPrecedence()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/usr/bin:/bin";
        host.AddSql("/usr/bin/sqlite3", "3.40.0");
        host.AddSql("/opt/sqlite/sqlite3", "3.51.0");

        var settingsStore = new ToolchainSettingsStore(Path.Combine(_tempDir, "toolchains.json"));
        var provider = new SqlToolchainProvider(host, new FakeProcessLauncher(), settingsStore);
        provider.Select("/opt/sqlite/sqlite3");

        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Equal("/opt/sqlite/sqlite3", resolution.Toolchain!.ExecutablePath);
    }
}
