using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Which Java JDK a script runs with, on each OS, described with a pretend computer.</summary>
public class JavaToolchainProviderTests : IDisposable
{
    private const string HomebrewJava = "/opt/homebrew/opt/openjdk/bin/java";
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaToolchain_" + Guid.NewGuid().ToString("N"));

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

    private JavaToolchainProvider Provider(FakeHostEnvironment host, IProcessLauncher? launcher = null) =>
        new(host, launcher ?? new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json")), Path.Combine(_baseDir, "java"));

    private static FakeHostEnvironment Mac()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin";
        return host;
    }

    [Fact]
    public async Task OnAMac_TheTerminalsJava_IsFound_EvenThoughTheAppsOwnPathIsMinimal()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/opt/openjdk/bin:/usr/bin:/bin";
        host.AddJava(HomebrewJava, "17.0.20");

        var java = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(java);
        Assert.Equal(HomebrewJava, java.ExecutablePath);
        Assert.Equal(new Version(17, 0, 20), java.Version);
        Assert.Equal("Java 17.0.20 (Homebrew)", java.Label);
    }

    [Fact]
    public async Task OlderJava_IsIgnored_WhenBelowMinimumVersion()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/local/bin:/usr/bin";
        host.AddJava("/usr/local/bin/java", "1.8.0_392");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Contains("1.8.0", resolution.Missing!.Summary);
    }

    [Fact]
    public async Task JavaWithoutJavacCompiler_IsRejected_AsJreOnly()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin:/usr/bin";
        host.AddJava("/opt/homebrew/bin/java", "17.0.10", hasCompiler: false);

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Contains("requires a Java Development Kit (JDK)", resolution.Missing!.Summary);
    }

    [Fact]
    public async Task UserSelectedJava_WinsOverPathAndSystem()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/bin";
        host.AddJava("/usr/bin/java", "17.0.10");

        const string custom = "/custom/jdk-21/bin/java";
        host.AddJava(custom, "21.0.2");

        var provider = Provider(host);
        provider.Select(custom);

        var java = (await provider.ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(java);
        Assert.Equal(custom, java.ExecutablePath);
        Assert.Equal(new Version(21, 0, 2), java.Version);
    }

    [Fact]
    public async Task JavaHome_WinsOverPath()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/bin";
        host.AddJava("/usr/bin/java", "11.0.12");

        const string javaHome = "/Library/Java/JavaVirtualMachines/temurin-17.jdk/Contents/Home";
        host.Variables["JAVA_HOME"] = javaHome;
        host.AddJava($"{javaHome}/bin/java", "17.0.20");

        var java = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(java);
        Assert.Equal($"{javaHome}/bin/java", java.ExecutablePath);
        Assert.Equal(new Version(17, 0, 20), java.Version);
    }

    [Fact]
    public async Task OnWindows_LocatesAdoptiumJdk()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.AddDirectory(@"C:\Program Files\Eclipse Adoptium");
        host.AddDirectory(@"C:\Program Files\Eclipse Adoptium\jdk-17.0.10.7-hotspot");

        const string winJava = @"C:\Program Files\Eclipse Adoptium\jdk-17.0.10.7-hotspot\bin\java.exe";
        host.AddJava(winJava, "17.0.10");

        var java = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(java);
        Assert.Equal(FakeHostEnvironment.Normalize(winJava), FakeHostEnvironment.Normalize(java.ExecutablePath));
        Assert.Equal(new Version(17, 0, 10), java.Version);
        Assert.Equal("Java 17.0.10 (Adoptium)", java.Label);
    }

    [Fact]
    public async Task WhenNoJavaInstalled_ReturnsActionableGuidance()
    {
        var host = Mac();
        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Equal("JDK isn't installed", resolution.Missing.Title);
        Assert.Contains(resolution.Missing.Steps, a => a.Contains("brew install openjdk"));
    }

    [Theory]
    [InlineData("openjdk version \"17.0.20\" 2026-07-21", 17, 0, 20)]
    [InlineData("java version \"21.0.2\" 2024-01-16 LTS", 21, 0, 2)]
    [InlineData("openjdk version \"11.0.22\"", 11, 0, 22)]
    [InlineData("java version \"1.8.0_392\"", 1, 8, 0)]
    [InlineData("openjdk version \"25-ea\"", 25, 0, 0)]
    public void ParseJavaVersion_HandlesVariousDistributions(string output, int major, int minor, int build)
    {
        var version = JavaToolchainProvider.ParseJavaVersion(output);
        Assert.NotNull(version);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        if (build > 0) Assert.Equal(build, version.Build);
    }
}
