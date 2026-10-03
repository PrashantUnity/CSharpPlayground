using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>Which C++ compiler a script compiles and runs with on each OS, tested on a pretend computer.</summary>
public class CppToolchainProviderTests : IDisposable
{
    private const string HomebrewClang = "/opt/homebrew/opt/llvm/bin/clang++";
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CppToolchain_" + Guid.NewGuid().ToString("N"));

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

    private CppToolchainProvider Provider(FakeHostEnvironment host, IProcessLauncher? launcher = null) =>
        new(host, launcher ?? new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json")), Path.Combine(_baseDir, "cpp"));

    private static FakeHostEnvironment Mac()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin";
        return host;
    }

    [Fact]
    public async Task OnAMac_TheTerminalsClang_IsFound_EvenThoughTheAppsOwnPathIsMinimal()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/opt/llvm/bin:/usr/bin:/bin";
        host.AddCpp(HomebrewClang, "17.0.6", "clang");

        var cpp = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(cpp);
        Assert.Equal(HomebrewClang, cpp.ExecutablePath);
        Assert.Equal(new Version(17, 0, 6), cpp.Version);
        Assert.Equal("Clang 17.0.6 (Homebrew)", cpp.Label);
    }

    [Fact]
    public async Task OlderCompiler_IsIgnored_WhenBelowMinimumVersion()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/local/bin:/usr/bin";
        host.AddCpp("/usr/local/bin/g++", "4.8.5", "gcc");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Contains("4.8.5", resolution.Missing!.Summary);
    }

    [Fact]
    public async Task UserSelectedCompiler_WinsOverPathAndSystem()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/bin";
        host.AddCpp("/usr/bin/clang++", "15.0.0", "apple");

        const string custom = "/custom/llvm-18/bin/clang++";
        host.AddCpp(custom, "18.1.2", "clang");

        var provider = Provider(host);
        provider.Select(custom);

        var cpp = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(cpp);
        Assert.Equal(custom, cpp.ExecutablePath);
        Assert.Equal(new Version(18, 1, 2), cpp.Version);
    }

    [Fact]
    public async Task OnLinux_LocatesGccCompiler()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/local/bin:/usr/bin:/bin";
        const string linuxGcc = "/usr/bin/g++";
        host.AddCpp(linuxGcc, "13.2.0", "gcc");

        var cpp = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(cpp);
        Assert.Equal(linuxGcc, cpp.ExecutablePath);
        Assert.Equal(new Version(13, 2, 0), cpp.Version);
        Assert.Equal("GCC 13.2.0 (System)", cpp.Label);
    }

    [Fact]
    public async Task OnWindows_LocatesVisualBasicOrMsvc()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130");

        const string winMsvc = @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\bin\Hostx64\x64\cl.exe";
        host.AddCpp(winMsvc, "19.38.33134", "msvc");

        var cpp = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(cpp);
        Assert.Equal(FakeHostEnvironment.Normalize(winMsvc), FakeHostEnvironment.Normalize(cpp.ExecutablePath));
        Assert.Equal(new Version(19, 38, 33134), cpp.Version);
        Assert.Equal("MSVC 19.38.33134 (Visual Studio)", cpp.Label);
    }

    [Fact]
    public async Task WhenNoCompilerInstalled_ReturnsActionableGuidance()
    {
        var host = Mac();
        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Equal("C++ compiler isn't installed", resolution.Missing.Title);
        Assert.Contains(resolution.Missing.Steps, a => a.Contains("brew install llvm") || a.Contains("xcode-select"));
    }

    [Theory]
    [InlineData("Apple clang version 15.0.0 (clang-1500.0.40.1)", CppCompilerVendor.AppleClang, 15, 0, 0)]
    [InlineData("Homebrew clang version 22.1.7\nTarget: arm64-apple-darwin25.5.0", CppCompilerVendor.Clang, 22, 1, 7)]
    [InlineData("g++ (Ubuntu 13.2.0-23ubuntu4) 13.2.0", CppCompilerVendor.Gcc, 13, 2, 0)]
    [InlineData("Microsoft (R) C/C++ Optimizing Compiler Version 19.38.33134 for x64", CppCompilerVendor.Msvc, 19, 38, 33134)]
    public void ParseCompilerVersion_HandlesVariousDistributions(string output, CppCompilerVendor vendor, int major, int minor, int build)
    {
        var version = CppToolchainProvider.ParseCompilerVersion(output, vendor);
        Assert.NotNull(version);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        if (build > 0) Assert.Equal(build, version.Build);
    }
}
