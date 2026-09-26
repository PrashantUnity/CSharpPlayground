using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The Hub's STUDIO ENVIRONMENT card has a row per language that runs with an installed toolchain, saying what was found
/// or how to install one. With C# and <see cref="FakeLanguage"/> only, so nothing on the machine is looked at.
/// </summary>
public class HubToolchainStatusTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubToolchains_" + Guid.NewGuid().ToString("N"));

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

    private (CSharpManagerViewModel Hub, FakeLanguage Fake) Hub()
    {
        FakeLanguage? fake = null;
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"), registerBuiltInLanguages: false, configure: (_, registry) =>
        {
            registry.Register(new CSharpLanguage());
            fake = FakeLanguage.RegisterIn(registry);
        });
        var storage = new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), services.Registry);
        return (new CSharpManagerViewModel(storage, _ => { }, _ => { }, languages: services), fake!);
    }

    [Fact]
    public void ThereIsARow_ForEachLanguageThatNeedsAToolchain_AndNothingIsLookedForUntilTheHubIsShown()
    {
        var (hub, fake) = Hub();

        var row = Assert.Single(hub.ToolchainStatuses); // C# runs inside the studio: no row
        Assert.Equal("FakeLang", row.Title);
        Assert.True(row.IsChecking);
        Assert.Equal(0, fake.FakeToolchain.ResolveCount);
    }

    [Fact]
    public async Task AFoundToolchain_ShowsItsNameAndWhereItCameFrom()
    {
        var (hub, _) = Hub();

        await hub.RefreshToolchainStatusesAsync();

        var row = hub.ToolchainStatuses[0];
        Assert.True(row.IsFound);
        Assert.False(row.IsMissing);
        Assert.Equal("FakeLang 1.2.3 · Test", row.Status);
        Assert.Equal("/fake/bin/fakec", row.Detail);
    }

    [Fact]
    public async Task AMissingToolchain_SaysSo_WithHowToInstallIt_AndLookingAgainFindsItOnceInstalled()
    {
        var (hub, fake) = Hub();
        fake.FakeToolchain.Installed = false;

        await hub.RefreshToolchainStatusesAsync();
        var row = hub.ToolchainStatuses[0];
        var missing = (row.IsMissing, row.Status, row.Detail);
        fake.FakeToolchain.Installed = true;
        await hub.LookAgainForToolchainsCommand.ExecuteAsync(null);

        Assert.True(missing.IsMissing);
        Assert.Equal("FakeLang not found: hover for how to install", missing.Status);
        Assert.Contains("fakepkg install fakelang", missing.Detail);
        Assert.True(row.IsFound);
    }
}
