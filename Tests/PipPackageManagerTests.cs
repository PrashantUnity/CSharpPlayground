using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary><c>%pip</c> commands, and installing into the studio environment when the Python itself doesn't allow it.</summary>
public class PipPackageManagerTests : IDisposable
{
    private const string HomebrewPython = "/opt/homebrew/bin/python3";
    private const string HomebrewPrefix = "/opt/homebrew/opt/python@3.14/Frameworks/Python.framework/Versions/3.14";

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_Pip_" + Guid.NewGuid().ToString("N"));
    private readonly FakeHostEnvironment _host = new(FakeOs.MacOS) { LoginShellPath = "/opt/homebrew/bin" };
    private readonly FakeProcessLauncher _launcher = new();
    private readonly PythonToolchainProvider _toolchains;
    private readonly PipPackageManager _pip;

    public PipPackageManagerTests()
    {
        _toolchains = new PythonToolchainProvider(_host, _launcher, new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json")), Path.Combine(_baseDir, "python"));
        _pip = new PipPackageManager(_toolchains, _launcher, _host);
        _launcher.Behavior = (spec, p) =>
        {
            if (spec.Arguments.Take(2).SequenceEqual(["-m", "venv"]))
            {
                var folder = spec.Arguments[^1];
                _host.AddPython(Path.Combine(folder, "bin", "python"), "3.14.6", prefix: folder, basePrefix: HomebrewPrefix, sitePackages: folder + "/lib/python3.14/site-packages");
            }
            else
            {
                p.Write("Successfully installed numpy-2.3.0\n");
            }
            p.Exit(0);
            return Task.CompletedTask;
        };
    }

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

    private async Task<ToolchainInfo> Python(bool externallyManaged)
    {
        _host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, externallyManaged: externallyManaged);
        return (await _toolchains.ResolveAsync(new ToolchainQuery())).Toolchain!;
    }

    [Theory]
    [InlineData("%pip install numpy pandas", new[] { "install", "numpy", "pandas" })]
    [InlineData("!pip install \"numpy>=2\" 'pandas < 3'", new[] { "install", "numpy>=2", "pandas < 3" })]
    [InlineData("  %pip3 list  ", new[] { "list" })]
    public void PipLines_AreCommands(string line, string[] arguments)
    {
        Assert.True(_pip.TryParseDirective(line, out var command));
        Assert.Equal(arguments, command.Arguments);
    }

    [Theory]
    [InlineData("pip install numpy")]
    [InlineData("%pipx install black")]
    [InlineData("%conda install numpy")]
    [InlineData("import pip")]
    public void OtherLines_AreNotPipCommands(string line) =>
        Assert.False(_pip.TryParseDirective(line, out _));

    [Fact]
    public void Arguments_KeepPipQuietAndUnattended()
    {
        Assert.Equal(
            ["-m", "pip", "install", "numpy", "--progress-bar", "off", "--disable-pip-version-check", "--no-input"],
            PipPackageManager.PipArguments(["install", "numpy"]));
        // Only install/download/wheel know --progress-bar; pip refuses it elsewhere.
        Assert.Equal(["-m", "pip", "list", "--disable-pip-version-check", "--no-input"], PipPackageManager.PipArguments(["list"]));
        // Nobody can answer "Proceed (Y/n)?".
        Assert.Contains("-y", PipPackageManager.PipArguments(["uninstall", "numpy"]));
    }

    [Fact]
    public void MissingModules_MapToTheirPackages()
    {
        Assert.Equal("opencv-python", _pip.PackageForMissingDependency("cv2"));
        Assert.Equal("scikit-learn", _pip.PackageForMissingDependency("sklearn.linear_model"));
        Assert.Equal("numpy", _pip.PackageForMissingDependency("numpy.core"));
        Assert.Equal("requests", _pip.PackageForMissingDependency("requests"));
        Assert.Equal(["install", "Pillow"], _pip.InstallCommand("Pillow").Arguments);
    }

    [Fact]
    public async Task InstallingIntoAnExternallyManagedPython_UsesTheStudioEnvironment_FromThenOn()
    {
        var python = await Python(externallyManaged: true);
        var output = new List<string>();
        _pip.TryParseDirective("%pip install numpy", out var command);

        var result = await _pip.RunAsync(command, python, output.Add);

        Assert.True(result.Success);
        var environment = result.SwitchedToolchain!;
        Assert.Equal("Studio environment", environment.Source);
        Assert.EndsWith("site-packages", result.AddedSearchPath);
        var runs = _launcher.Started.ToList();
        Assert.Equal(2, runs.Count);
        Assert.Equal(HomebrewPython, runs[0].FileName); // makes the environment
        Assert.Equal(environment.ExecutablePath, runs[1].FileName); // then pip runs in it, never in Homebrew's Python
        Assert.Equal(["-m", "pip", "install", "numpy"], runs[1].Arguments.Take(4));
        Assert.Equal(environment.ExecutablePath, _toolchains.SelectedPath);
        Assert.Contains(output, o => o.Contains("studio's own environment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstallingIntoAPythonThatAllowsIt_UsesThatPython()
    {
        var python = await Python(externallyManaged: false);
        _pip.TryParseDirective("%pip install numpy", out var command);

        var result = await _pip.RunAsync(command, python, _ => { });

        Assert.True(result.Success);
        Assert.Null(result.SwitchedToolchain);
        Assert.Equal(HomebrewPython, Assert.Single(_launcher.Started).FileName);
    }

    [Fact]
    public async Task ListingPackages_NeverMakesAnEnvironment()
    {
        var python = await Python(externallyManaged: true);
        _pip.TryParseDirective("%pip list", out var command);

        await _pip.RunAsync(command, python, _ => { });

        Assert.Equal(HomebrewPython, Assert.Single(_launcher.Started).FileName);
        Assert.Null(_toolchains.SelectedPath);
    }

    [Fact]
    public async Task APythonWithoutPip_SaysSo()
    {
        _host.AddPython(HomebrewPython, "3.14.6", prefix: HomebrewPrefix, pip: false);
        var python = (await _toolchains.ResolveAsync(new ToolchainQuery())).Toolchain!;
        _pip.TryParseDirective("%pip install numpy", out var command);

        var result = await _pip.RunAsync(command, python, _ => { });

        Assert.False(result.Success);
        Assert.Contains("has no pip", result.Message);
        Assert.Empty(_launcher.Started);
    }
}
