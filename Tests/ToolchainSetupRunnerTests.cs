using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;
using LanguageSettingItemViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.LanguageSettingItemViewModel;
using ToolchainSetupStepItem = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.ToolchainSetupStepItem;

namespace CSharpEditorPlugin.Tests;

public class ToolchainSetupRunnerTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_SetupRunner_" + Guid.NewGuid().ToString("N"));

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
    public async Task ToolchainSetupRunner_RunsCommandSuccessfully_ViaProcessLauncher()
    {
        var launcher = new FakeProcessLauncher();
        var host = new FakeHostEnvironment(FakeOs.MacOS);

        var outputLines = new List<string>();
        var success = await ToolchainSetupRunner.ExecuteAsync(
            "echo 'hello world'",
            host,
            launcher,
            line => outputLines.Add(line),
            CancellationToken.None);

        Assert.True(success);
        Assert.Single(launcher.Started);
        Assert.Contains(outputLines, l => l.Contains("echo 'hello world'"));
    }

    [Fact]
    public async Task ToolchainSetupRunner_HandlesProcessFailureGracefully()
    {
        var launcher = new FakeProcessLauncher
        {
            FailToStart = _ => true
        };
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var outputLines = new List<string>();

        var success = await ToolchainSetupRunner.ExecuteAsync(
            "nonexistent-tool install",
            host,
            launcher,
            line => outputLines.Add(line));

        Assert.False(success);
        Assert.Contains(outputLines, l => l.Contains("Failed to start command"));
    }

    [Fact]
    public void ToolchainSetupStepItem_ParsesCommandsAndUrlsCorrectly()
    {
        var winHost = new FakeHostEnvironment(FakeOs.Windows);
        var stepWithCmd = LanguageSettingItemViewModel.ParseSetupStep("Run winget install LLVM.LLVM in terminal", winHost);
        Assert.True(stepWithCmd.HasCommand);
        Assert.Equal("winget install LLVM.LLVM", stepWithCmd.Command);
        Assert.True(stepWithCmd.IsRunnable);

        var stepWithUrl = LanguageSettingItemViewModel.ParseSetupStep("Download installer: https://visualstudio.microsoft.com/", winHost);
        Assert.True(stepWithUrl.HasUrl);
        Assert.Equal("https://visualstudio.microsoft.com/", stepWithUrl.Url);

        var plainStep = LanguageSettingItemViewModel.ParseSetupStep("Restart your machine after installation", winHost);
        Assert.False(plainStep.HasCommand);
        Assert.False(plainStep.HasUrl);
        Assert.False(plainStep.IsRunnable);
    }

    [Fact]
    public async Task CopyCommandToClipboardAsync_SetsClipboardTextAndVisualFeedback()
    {
        var step = new ToolchainSetupStepItem
        {
            RawText = "winget install LLVM.LLVM",
            Title = "Install LLVM",
            Command = "winget install LLVM.LLVM",
            IsRunnable = true
        };

        var fakeLang = new FakeLanguage();
        var vm = new LanguageSettingItemViewModel(fakeLang);

        await vm.CopyCommandToClipboardAsync(step, clipboard: null);

        Assert.True((bool)step.IsCopied);
        Assert.Equal("Copied! ✓", step.CopyButtonLabel);
    }

    [Fact]
    public void MissingGuidance_PopulatesSetupSteps_InLanguageSettingItemViewModel()
    {
        var fakeLang = new FakeLanguage();
        fakeLang.FakeToolchain.Installed = false;
        var vm = new LanguageSettingItemViewModel(fakeLang);

        var resolution = ToolchainResolution.NotFound(new MissingToolchainGuidance(
            "FakeLang Missing",
            "Please install FakeLang.",
            [
                "Run winget install fakelang",
                "Visit https://fakelang.org/download"
            ],
            "https://fakelang.org/installer"));

        vm.UpdateResolution(resolution, allFound: Array.Empty<ToolchainInfo>());

        Assert.True(vm.IsMissing);
        Assert.True(vm.HasDownloadUrl);
        Assert.Equal((string?)"https://fakelang.org/installer", (string?)vm.MissingDownloadUrl);
        Assert.Equal(2, vm.SetupSteps.Count);
        Assert.Equal("winget install fakelang", vm.SetupSteps[0].Command);
        Assert.Equal("https://fakelang.org/download", vm.SetupSteps[1].Url);
    }

    [Fact]
    public void CppToolchainProvider_ExposesPlatformSpecificActions()
    {
        var winHost = new FakeHostEnvironment(FakeOs.Windows);
        var winProvider = new CppToolchainProvider(winHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "win_tc.json")), Path.Combine(_baseDir, "win_cpp"));

        Assert.Contains(winProvider.Actions, a => a.Id == "winget-install-llvm");
        Assert.Contains(winProvider.Actions, a => a.Id == "winget-install-vs");

        var macHost = new FakeHostEnvironment(FakeOs.MacOS);
        var macProvider = new CppToolchainProvider(macHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "mac_tc.json")), Path.Combine(_baseDir, "mac_cpp"));

        Assert.Contains(macProvider.Actions, a => a.Id == "install-xcode-clt");
        Assert.Contains(macProvider.Actions, a => a.Id == "brew-install-llvm");
    }

    [Fact]
    public void JavaToolchainProvider_ExposesPlatformSpecificActions()
    {
        var winHost = new FakeHostEnvironment(FakeOs.Windows);
        var winProvider = new JavaToolchainProvider(winHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "win_java_tc.json")), Path.Combine(_baseDir, "win_java"));

        Assert.Contains(winProvider.Actions, a => a.Id == "winget-install-openjdk");

        var macHost = new FakeHostEnvironment(FakeOs.MacOS);
        var macProvider = new JavaToolchainProvider(macHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "mac_java_tc.json")), Path.Combine(_baseDir, "mac_java"));

        Assert.Contains(macProvider.Actions, a => a.Id == "brew-install-openjdk");
    }

    [Fact]
    public void JavaScriptToolchainProvider_ExposesPlatformSpecificActions()
    {
        var winHost = new FakeHostEnvironment(FakeOs.Windows);
        var winProvider = new JavaScriptToolchainProvider(winHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "win_js_tc.json")), Path.Combine(_baseDir, "win_js"));

        Assert.Contains(winProvider.Actions, a => a.Id == "winget-install-node");

        var macHost = new FakeHostEnvironment(FakeOs.MacOS);
        var macProvider = new JavaScriptToolchainProvider(macHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "mac_js_tc.json")), Path.Combine(_baseDir, "mac_js"));

        Assert.Contains(macProvider.Actions, a => a.Id == "brew-install-node");
    }

    [Fact]
    public void CSharpToolchainProvider_ExposesPlatformSpecificActions()
    {
        var winHost = new FakeHostEnvironment(FakeOs.Windows);
        var winProvider = new CSharpToolchainProvider(winHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "win_cs_tc.json")));

        Assert.Contains(winProvider.Actions, a => a.Id == "winget-install-dotnet");

        var macHost = new FakeHostEnvironment(FakeOs.MacOS);
        var macProvider = new CSharpToolchainProvider(macHost, new FakeProcessLauncher(),
            new ToolchainSettingsStore(Path.Combine(_baseDir, "mac_cs_tc.json")));

        Assert.Contains(macProvider.Actions, a => a.Id == "brew-install-dotnet");
    }
}
