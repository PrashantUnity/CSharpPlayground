using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Which Python a document runs with, on each OS, described with a pretend computer.</summary>
public class PythonToolchainProviderTests : IDisposable
{
    private const string HomebrewPython = "/opt/homebrew/bin/python3";
    private const string HomebrewPrefix = "/opt/homebrew/opt/python@3.14/Frameworks/Python.framework/Versions/3.14";
    private const string PythonOrgPrefix = "/Library/Frameworks/Python.framework/Versions/3.14";

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_PythonToolchain_" + Guid.NewGuid().ToString("N"));

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

    private PythonToolchainProvider Provider(FakeHostEnvironment host, IProcessLauncher? launcher = null) =>
        new(host, launcher ?? new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json")), Path.Combine(_baseDir, "python"));

    private static FakeHostEnvironment Mac()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin"; // what an app started from the Finder gets
        return host;
    }

    [Fact]
    public async Task OnAMac_TheTerminalsPython_IsFound_EvenThoughTheAppsOwnPathIsMinimal()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin:/usr/bin:/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, externallyManaged: true);

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(python);
        Assert.Equal(HomebrewPython, python.ExecutablePath);
        Assert.Equal(new Version(3, 14, 6), python.Version);
        Assert.Equal("Python 3.14.6 (Homebrew)", python.Label);
        Assert.True(python.Is(PythonToolchainProvider.ExternallyManagedProperty));
    }

    [Fact]
    public async Task ApplesPythonStub_IsNeverStarted_WithoutTheCommandLineTools()
    {
        var host = Mac();
        host.AddPython("/usr/bin/python3", "3.9.6", prefix: "/Applications/Xcode.app/Contents/Developer/Library/Frameworks/Python3.framework/Versions/3.9");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.DoesNotContain("/usr/bin/python3", host.ProbedPaths);
    }

    [Fact]
    public async Task ApplesPython_IsUsed_WhenTheCommandLineToolsAreInstalled()
    {
        var host = Mac();
        host.CommandLineToolsInstalled = true;
        host.AddPython("/usr/bin/python3", "3.9.6", prefix: "/Applications/Xcode.app/Contents/Developer/Library/Frameworks/Python3.framework/Versions/3.9");

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal("Python 3.9.6 (Xcode Command Line Tools)", python?.Label);
    }

    [Fact]
    public async Task TheSavedChoice_WinsOverEverything()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix);
        host.AddPython("/usr/local/bin/python3", "3.14.3", prefix: PythonOrgPrefix);
        var provider = Provider(host);

        provider.Select("/usr/local/bin/python3");
        var python = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal("/usr/local/bin/python3", python?.ExecutablePath);
        Assert.Equal("python.org", python?.Source);
        // Remembered for the next time the studio starts.
        Assert.Equal("/usr/local/bin/python3", Provider(host).SelectedPath);
    }

    [Fact]
    public async Task ASavedChoiceThatStoppedWorking_FallsBackToTheNextPython()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix);
        host.AddBrokenPython("/Users/test/old-venv/bin/python");
        var provider = Provider(host);
        provider.Select("/Users/test/old-venv/bin/python");

        var python = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal(HomebrewPython, python?.ExecutablePath);
    }

    [Fact]
    public async Task AProjectsVirtualEnvironment_WinsOverThePath_FromAnyFolderBelowIt()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, externallyManaged: true);
        host.AddFile("/work/proj/.venv/pyvenv.cfg");
        host.AddPython("/work/proj/.venv/bin/python", "3.14.6", prefix: "/work/proj/.venv", basePrefix: HomebrewPrefix, externallyManaged: true);

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery("/work/proj/src/tools", "/work/proj"))).Toolchain;

        Assert.Equal("/work/proj/.venv/bin/python", python?.ExecutablePath);
        Assert.Equal("Project .venv", python?.Source);
        Assert.True(python!.Is(PythonToolchainProvider.VirtualEnvironmentProperty));
        // pip installs into a virtual environment are fine even when the Python it came from is externally managed.
        Assert.False(python.Is(PythonToolchainProvider.ExternallyManagedProperty));
    }

    [Fact]
    public async Task AnEnvFolderWithoutPyvenvCfg_IsJustAFolder()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix);
        host.AddPython("/work/proj/env/bin/python", "3.14.6", prefix: "/work/proj/env");

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery("/work/proj", "/work/proj"))).Toolchain;

        Assert.Equal(HomebrewPython, python?.ExecutablePath);
    }

    [Fact]
    public async Task TheStudioEnvironment_IsUsedOnceItExists()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, externallyManaged: true);
        var envPython = Path.Combine(_baseDir, "python", "envs", "3.14", "bin", "python");
        host.AddPython(envPython, "3.14.6", prefix: Path.Combine(_baseDir, "python", "envs", "3.14"), basePrefix: HomebrewPrefix);

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal(FakeHostEnvironment.Normalize(envPython), FakeHostEnvironment.Normalize(python!.ExecutablePath));
        Assert.Equal("Studio environment", python.Source);
        Assert.True(python.Is(PythonToolchainProvider.StudioEnvironmentProperty));
    }

    [Fact]
    public async Task APythonThatsTooOld_IsSkipped_AndTheGuidanceSaysSo()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/local/bin";
        host.AddPython("/usr/local/bin/python3", "3.8.10", prefix: "/Library/Frameworks/Python.framework/Versions/3.8");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Contains("3.8.10", resolution.Missing!.Summary);
        Assert.Contains("3.9", resolution.Missing.Summary);
    }

    [Theory]
    [InlineData(FakeOs.MacOS, "brew install python")]
    [InlineData(FakeOs.Linux, "sudo apt install python3 python3-venv")]
    [InlineData(FakeOs.Windows, "winget install Python.Python.3.13")]
    public async Task WithNoPython_TheGuidanceFitsTheOs(FakeOs os, string expected)
    {
        var resolution = await Provider(new FakeHostEnvironment(os)).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Equal("Python isn't installed", resolution.Missing!.Title);
        Assert.Contains(resolution.Missing.Steps, s => s.Contains(expected, StringComparison.Ordinal));
        Assert.Contains(expected, resolution.Missing.ToText());
    }

    [Fact]
    public async Task OnWindows_StoreShortcutsAreSkipped_AndPythonLauncherInstallsAreFound()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["PATH"] = @"C:\Users\test\AppData\Local\Microsoft\WindowsApps;C:\Windows";
        host.Variables["LOCALAPPDATA"] = @"C:\Users\test\AppData\Local";
        host.Variables["WINDIR"] = @"C:\Windows";
        host.AddBrokenPython(@"C:\Users\test\AppData\Local\Microsoft\WindowsApps\python.exe", exitCode: 9009);
        host.AddFile(@"C:\Windows\py.exe");
        const string installed = @"D:\Tools\Python313\python.exe";
        host.AddPython(installed, "3.13.1", prefix: @"D:\Tools\Python313");
        host.OnCommand = (file, args) => file.EndsWith("py.exe", StringComparison.OrdinalIgnoreCase) && args.SequenceEqual(["-0p"])
            ? new CommandResult(0, $" -V:3.13 *        {installed}\n", string.Empty, false)
            : null;

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal(installed, python?.ExecutablePath);
        Assert.Equal("Python launcher", python?.Source);
    }

    [Fact]
    public void OnWindows_AStudioEnvironmentsInterpreter_IsInScripts()
    {
        Assert.EndsWith(Path.Combine("Scripts", "python.exe"), Provider(new FakeHostEnvironment(FakeOs.Windows)).EnvironmentInterpreter(Path.Combine(_baseDir, "env")));
        Assert.EndsWith(Path.Combine("bin", "python"), Provider(Mac()).EnvironmentInterpreter(Path.Combine(_baseDir, "env")));
    }

    [Fact]
    public async Task TheList_ShowsEachInterpreterOnce_InTheOrderTheyreChosen()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin:/usr/local/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix);
        host.AddPython("/usr/local/bin/python3", "3.14.3", prefix: PythonOrgPrefix);
        // The same python.org install again, through its framework folder.
        host.AddPython(PythonOrgPrefix + "/bin/python3", "3.14.3", prefix: PythonOrgPrefix);

        var all = await Provider(host).ListAsync(new ToolchainQuery());

        Assert.Equal(["Python 3.14.6 (Homebrew)", "Python 3.14.3 (python.org)"], all.Select(p => p.Label));
    }

    [Fact]
    public async Task WhatAStartupScriptPrints_BeforeTheProbesAnswer_IsIgnored()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, noise: "sitecustomize: loading plugins\n");

        var python = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.Equal(new Version(3, 14, 6), python?.Version);
    }

    [Fact]
    public async Task TheStudioEnvironment_IsMadeFromThePython_WithItsPackagesStillVisible()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, externallyManaged: true);
        var launcher = new FakeProcessLauncher();
        var envFolder = Path.Combine(_baseDir, "python", "envs", "3.14");
        launcher.Behavior = (spec, p) =>
        {
            if (spec.Arguments.Take(2).SequenceEqual(["-m", "venv"]))
            {
                host.AddPython(Path.Combine(spec.Arguments[^1], "bin", "python"), "3.14.6", prefix: spec.Arguments[^1], basePrefix: HomebrewPrefix);
                p.Write("created\n");
            }
            p.Exit(0);
            return Task.CompletedTask;
        };
        var provider = Provider(host, launcher);
        var basePython = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain!;
        var output = new List<string>();

        var environment = await provider.EnsureStudioEnvironmentAsync(basePython, output.Add);

        var venvRun = Assert.Single(launcher.Started);
        Assert.Equal(HomebrewPython, venvRun.FileName);
        Assert.Equal(["-m", "venv", "--system-site-packages", envFolder], venvRun.Arguments);
        Assert.Equal("Studio environment", environment.Source);
        Assert.Contains(output, o => o.Contains("Studio environment ready", StringComparison.Ordinal));

        // It's there now, so asking again doesn't make it again.
        await provider.EnsureStudioEnvironmentAsync(basePython, output.Add);
        Assert.Single(launcher.Started);
    }

    [Fact]
    public async Task APythonThatCantMakeVirtualEnvironments_SaysWhatToInstall()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux) { LoginShellPath = "/usr/bin" };
        host.AddPython("/usr/bin/python3", "3.12.3", prefix: "/usr", externallyManaged: true, venv: false);
        var provider = Provider(host);
        var basePython = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain!;

        var result = await provider.RunActionAsync(PythonToolchainProvider.CreateStudioEnvironmentAction, new ToolchainQuery(), _ => { });

        Assert.False(result.Success);
        Assert.Contains("sudo apt install python3.12-venv", result.Message);
        Assert.Equal("System", basePython.Source);
    }
}
