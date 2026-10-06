using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

[Collection("SettingsTests")]
public class CSharpSettingsViewModelCustomizationTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StudioLanguageServices _services;
    private readonly StudioSettingsStore _settingsStore;

    public CSharpSettingsViewModelCustomizationTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_SettingsCustomizationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _services = new StudioLanguageServices(_tempFolder);
        _settingsStore = _services.StudioSettings;
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
    public void CustomizationCategory_IsPresentAndCanBeSelected()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        var customCategory = vm.Categories.FirstOrDefault(c => c.Id == "Customization");
        Assert.NotNull(customCategory);
        Assert.Equal("Customization & Extensions", customCategory.Title);

        vm.SelectCategory("Customization");
        Assert.True(vm.IsCustomizationCategoryActive);
        Assert.False(vm.IsLanguagesCategoryActive);
        Assert.False(vm.IsEditorCategoryActive);
    }

    [Fact]
    public void ThemePresets_ArePopulatedWithBuiltInThemes()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.ThemePresets);
        Assert.Contains(vm.ThemePresets, t => t.Id == "dracula");
        Assert.Contains(vm.ThemePresets, t => t.Id == "cyberpunk");
        Assert.Contains(vm.ThemePresets, t => t.Id == "dark-plus");
    }

    [Fact]
    public void ApplyThemePresetCommand_AppliesThemeSuccessfully()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.ApplyThemePresetCommand.Execute("dracula");

        Assert.Equal("dracula", vm.ActiveThemeId);
        Assert.Equal("Dracula Pro", vm.ActiveThemeName);

        var draculaPreset = vm.ThemePresets.First(p => p.Id == "dracula");
        Assert.True(draculaPreset.IsActive);
    }

    [Fact]
    public void SetDensityCommand_UpdatesLayoutDensity()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.SetDensityCommand.Execute("Compact");
        Assert.Equal(LayoutDensity.Compact, vm.ActiveLayoutDensity);

        vm.SetDensityCommand.Execute("Spacious");
        Assert.Equal(LayoutDensity.Spacious, vm.ActiveLayoutDensity);
    }

    [Fact]
    public async Task ReloadCustomizationScriptCommand_ExecutesWithoutError()
    {
        var customFolder = Path.Combine(_tempFolder, "custom_mgr");
        var mgr = new CustomizationManager(customFolder);
        StudioAppContext.Instance.CustomizationManager = mgr;

        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        await vm.ReloadCustomizationScriptCommand.ExecuteAsync(null);

        Assert.NotNull(vm.StatusMessage);
        Assert.False(vm.IsStatusError);
    }

    [Fact]
    public async Task OpenGlobalScriptCommand_WithDelegate_InvokesOpenScript()
    {
        var customFolder = Path.Combine(_tempFolder, "custom_open");
        var mgr = new CustomizationManager(customFolder);
        StudioAppContext.Instance.CustomizationManager = mgr;

        ScriptDocumentItem? openedDoc = null;
        var vm = new CSharpSettingsViewModel(
            _services,
            _settingsStore,
            openScriptAction: doc => openedDoc = doc);

        await vm.OpenGlobalScriptCommand.ExecuteAsync(null);

        Assert.NotNull(openedDoc);
        Assert.Equal("init.csx", openedDoc.Title);
        Assert.Equal(LanguageIds.CSharp, openedDoc.LanguageId);
    }

    [Fact]
    public void ToggleImportPackageFormCommand_TogglesVisibility()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.False(vm.IsImportPackageFormVisible);
        vm.ToggleImportPackageFormCommand.Execute(null);
        Assert.True(vm.IsImportPackageFormVisible);
        vm.ToggleImportPackageFormCommand.Execute(null);
        Assert.False(vm.IsImportPackageFormVisible);
    }

    [Fact]
    public void UseSampleGitPackageCommand_SetsUrlAndOpensForm()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.UseSampleGitPackageCommand.Execute("https://github.com/PrashantUnity/FrySharp.Zig.git#v1.0.0");

        Assert.True(vm.IsImportPackageFormVisible);
        Assert.Equal("https://github.com/PrashantUnity/FrySharp.Zig.git#v1.0.0", vm.PackageGitUrl);
    }

    [Fact]
    public async Task ImportGitPackageCommand_WithEmptyUrl_ShowsError()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        vm.PackageGitUrl = "   ";

        await vm.ImportGitPackageCommand.ExecuteAsync(null);

        Assert.True(vm.PackageImportHasError);
        Assert.Contains("valid Git URL", vm.PackageImportStatus);
    }

    [Fact]
    public async Task ImportGitPackageCommand_WithValidPackage_InstallsAndRefreshes()
    {
        var pkgDir = Path.Combine(_tempFolder, "sample_git_pkg");
        Directory.CreateDirectory(pkgDir);
        await File.WriteAllTextAsync(Path.Combine(pkgDir, "extension.json"), """
        {
            "id": "com.test.gitpkg",
            "name": "Test Git Package",
            "version": "1.0.0",
            "author": "Tester",
            "description": "Test package import"
        }
        """);
        await File.WriteAllTextAsync(Path.Combine(pkgDir, "Plugin.cs"), """
        using System.Threading.Tasks;
        using FrySharp.Sdk;
        namespace TestPkg;
        public class Plugin : IExtensionEntryPoint
        {
            public Task InitializeAsync(IExtensionContext context) => Task.CompletedTask;
            public Task DeactivateAsync() => Task.CompletedTask;
        }
        """);

        var app = StudioAppContext.Instance;
        var customCacheRoot = Path.Combine(_tempFolder, "pkg_cache");
        app.GitPackageService = new GitPackageService(customCacheRoot: customCacheRoot);

        // Pre-populate cache so download is simulated
        var cachedTarget = Path.Combine(customCacheRoot, "github.com-test-gitpkg", "v1.0.0");
        Directory.CreateDirectory(cachedTarget);
        File.Copy(Path.Combine(pkgDir, "extension.json"), Path.Combine(cachedTarget, "extension.json"));
        File.Copy(Path.Combine(pkgDir, "Plugin.cs"), Path.Combine(cachedTarget, "Plugin.cs"));

        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        vm.PackageGitUrl = "https://github.com/test/gitpkg.git#v1.0.0";

        await vm.ImportGitPackageCommand.ExecuteAsync(null);

        Assert.True(vm.PackageImportSuccess);
        Assert.False(vm.PackageImportHasError);
        Assert.Contains(vm.InstalledExtensions, e => e.Id == "com.test.gitpkg");

        // Now test removal
        var item = vm.InstalledExtensions.First(e => e.Id == "com.test.gitpkg");
        await vm.RemoveExtensionCommand.ExecuteAsync(item);
        Assert.DoesNotContain(vm.InstalledExtensions, e => e.Id == "com.test.gitpkg");
    }
}
