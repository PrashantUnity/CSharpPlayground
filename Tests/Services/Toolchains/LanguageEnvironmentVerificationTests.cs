using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class LanguageEnvironmentVerificationTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StudioLanguageServices _services;

    public LanguageEnvironmentVerificationTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_VerifyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _services = new StudioLanguageServices(_tempFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task CSharp_TestHelloWorld_ExecutesSuccessfullyViaInProcessRoslyn()
    {
        var settingsVm = new CSharpSettingsViewModel(_services);
        var csharpItem = settingsVm.Languages.First(l => l.IsCSharp);

        Assert.False(csharpItem.HasHelloWorldResult);
        Assert.False(csharpItem.IsTestingHelloWorld);
        Assert.Equal("Test Runtime", csharpItem.TestButtonLabel);

        await csharpItem.TestHelloWorldAsync();

        Assert.True(csharpItem.HasHelloWorldResult);
        Assert.True(csharpItem.IsHelloWorldSuccess);
        Assert.Equal("Passed", csharpItem.HelloWorldStatusBadge);
        Assert.Contains("Hello from FryPDF!", csharpItem.HelloWorldOutput);
        Assert.False(string.IsNullOrWhiteSpace(csharpItem.HelloWorldElapsedText));
        Assert.Contains("Roslyn", csharpItem.HelloWorldEngineDetails);
    }

    [Fact]
    public async Task ClearHelloWorldResult_ClearsState()
    {
        var settingsVm = new CSharpSettingsViewModel(_services);
        var csharpItem = settingsVm.Languages.First(l => l.IsCSharp);

        await csharpItem.TestHelloWorldAsync();
        Assert.True(csharpItem.HasHelloWorldResult);

        csharpItem.ClearHelloWorldResult();
        Assert.False(csharpItem.HasHelloWorldResult);
        Assert.Empty(csharpItem.HelloWorldOutput);
        Assert.Empty(csharpItem.HelloWorldStatusBadge);
    }

    [Fact]
    public async Task MissingToolchain_ReportsNotDetected()
    {
        var fakeMissingProvider = new FakeMissingToolchainProvider();
        var fakeLang = new FakeTestLanguage(fakeMissingProvider);
        var item = new LanguageSettingItemViewModel(fakeLang, provider: fakeMissingProvider);

        await item.TestHelloWorldAsync();

        Assert.True(item.HasHelloWorldResult);
        Assert.False(item.IsHelloWorldSuccess);
        Assert.Equal("Not Detected", item.HelloWorldStatusBadge);
        Assert.Contains("not detected", item.HelloWorldExecutionMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ButtonLabels_ReflectTestingState()
    {
        var fakeLang = new FakeTestLanguage(null);
        var item = new LanguageSettingItemViewModel(fakeLang);

        Assert.Equal("Test Runtime", item.TestButtonLabel);
        Assert.Equal("Run Hello World Check", item.TestButtonFullLabel);

        item.IsTestingHelloWorld = true;
        Assert.Equal("Testing…", item.TestButtonLabel);
        Assert.Equal("Testing…", item.TestButtonFullLabel);
    }

    private sealed class FakeTestLanguage : LanguageDefinition
    {
        private readonly IToolchainProvider? _provider;

        public FakeTestLanguage(IToolchainProvider? provider)
        {
            _provider = provider;
        }

        public override string Id => "fake-lang";
        public override string DisplayName => "FakeLang";
        public override string ShortName => "Fake";
        public override IReadOnlyList<string> Aliases => ["fake"];
        public override IReadOnlyList<string> FileExtensions => [".fake"];
        public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
        public override LanguageCapabilities Capabilities => LanguageCapabilities.None;
        public override string IconKind => "CodeBraces";
        public override string AccentHex => "#FFFFFF";
        public override string LineCommentPrefix => "//";
        public override string RuntimeDescription => "Fake Runtime";
        public override IToolchainProvider? Toolchain => _provider;
    }

    private sealed class FakeMissingToolchainProvider : IToolchainProvider
    {
        public string LanguageId => "fake-lang";
        public string ToolName => "FakeTool";
        public string? SelectedPath => null;
        public IReadOnlyList<ToolchainAction> Actions => Array.Empty<ToolchainAction>();

        public void Select(string? path) { }
        public void Refresh() { }
        public Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ToolchainInfo>>(Array.Empty<ToolchainInfo>());

        public Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default) =>
            Task.FromResult(ToolchainResolution.NotFound(new MissingToolchainGuidance("Fake missing", "Fake runtime not detected.", Array.Empty<string>())));

        public Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string>? onOutput = null, CancellationToken ct = default) =>
            Task.FromResult(new ToolchainActionResult(false, "Not implemented"));
    }
}
