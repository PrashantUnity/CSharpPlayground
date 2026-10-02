using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

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
}
