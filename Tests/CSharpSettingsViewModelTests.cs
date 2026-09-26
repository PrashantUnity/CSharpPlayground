using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpSettingsViewModelTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StudioLanguageServices _services;
    private readonly StudioSettingsStore _settingsStore;

    public CSharpSettingsViewModelTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_SettingsTests_" + Guid.NewGuid().ToString("N"));
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
    public void Settings_InitializesWithDefaultValues()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.Equal(4, vm.TabSize);
        Assert.True(vm.ConvertTabsToSpaces);
        Assert.False(vm.WordWrap);
        Assert.True(vm.ShowLineNumbers);
        Assert.Equal(13.0, vm.FontSize);
        Assert.Equal(0, vm.ExecutionTimeoutSeconds);
        Assert.False(vm.AutoClearConsoleOnRun);
        Assert.Equal(10000, vm.MaxTerminalOutputLines);
        Assert.True(vm.NullableChecksEnabled);
        Assert.Equal("13.0", vm.LanguageVersion);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void Settings_CanModifyAndPersistViaApply()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.TabSize = 2;
        vm.WordWrap = true;
        vm.ExecutionTimeoutSeconds = 45;
        vm.LanguageVersion = "12.0";

        Assert.True(vm.HasPendingChanges);

        vm.Apply();

        Assert.False(vm.HasPendingChanges);
        Assert.True(vm.HasStatusMessage);

        // Reload fresh from disk
        var freshStore = new StudioSettingsStore(Path.Combine(_tempFolder, "studio_settings.json"));
        var loaded = freshStore.GetSettings();

        Assert.Equal(2, loaded.TabSize);
        Assert.True(loaded.WordWrap);
        Assert.Equal(45, loaded.ExecutionTimeoutSeconds);
        Assert.Equal("12.0", loaded.LanguageVersion);
    }

    [Fact]
    public void Settings_ResetDefaults_RestoresInitialPreferences()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.TabSize = 8;
        vm.WordWrap = true;
        vm.ExecutionTimeoutSeconds = 60;

        vm.ResetDefaults();

        Assert.Equal(4, vm.TabSize);
        Assert.False(vm.WordWrap);
        Assert.Equal(0, vm.ExecutionTimeoutSeconds);
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public void Languages_ListsRegisteredLanguages()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.Languages);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.CSharp);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.Python);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.JavaScript);
    }

    [Fact]
    public void Languages_CSharp_IsConfiguredAsInProcess()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);

        Assert.False(csharp.IsToolchainLanguage);
        Assert.True(csharp.IsFound);
        Assert.False(csharp.IsMissing);
        Assert.NotEmpty(csharp.EnvironmentDetails);
    }

    [Fact]
    public void Languages_CanSelectCustomInterpreterPath()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var python = vm.Languages.FirstOrDefault(l => l.Language.Id == LanguageIds.Python);
        Assert.NotNull(python);

        const string customBinary = "/custom/path/to/python3";
        vm.ApplyCustomPath(python, customBinary);

        Assert.False(python.IsAutoDetect);
        Assert.Equal(customBinary, python.CustomPath);
        Assert.Equal(customBinary, _services.ToolchainSettings.GetSelectedPath(LanguageIds.Python));
    }

    [Fact]
    public void Languages_CanSetAutoDetectMode()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var js = vm.Languages.FirstOrDefault(l => l.Language.Id == LanguageIds.JavaScript);
        Assert.NotNull(js);

        vm.ApplyCustomPath(js, "/usr/bin/node");
        Assert.False(js.IsAutoDetect);

        vm.SetAutoDetect(js);
        Assert.True(js.IsAutoDetect);
        Assert.Empty(js.CustomPath);
        Assert.Null(_services.ToolchainSettings.GetSelectedPath(LanguageIds.JavaScript));
    }

    [Fact]
    public void Languages_SearchFilter_FiltersCorrectly()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.SearchQuery = "python";
        Assert.Single(vm.FilteredLanguages);
        Assert.Equal(LanguageIds.Python, vm.FilteredLanguages[0].Language.Id);

        vm.SearchQuery = string.Empty;
        Assert.Equal(vm.Languages.Count, vm.FilteredLanguages.Count);
    }

    [Fact]
    public void Keymap_PopulatesStandardShortcuts()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.Shortcuts);
        Assert.Contains(vm.Shortcuts, s => s.Shortcut.Contains("Ctrl+,", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.Shortcuts, s => s.Shortcut.Contains("Ctrl+B", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.Shortcuts, s => s.Shortcut.Contains("Ctrl+J", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.Shortcuts, s => s.Shortcut.Contains("F5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Navigation_Callbacks_AreInvoked()
    {
        bool hubCalled = false;
        bool prevCalled = false;

        var vm = new CSharpSettingsViewModel(
            _services,
            _settingsStore,
            backToHubAction: () => hubCalled = true,
            backToPreviousAction: () => prevCalled = true);

        vm.BackToHub();
        Assert.True(hubCalled);

        vm.BackToPrevious();
        Assert.True(prevCalled);
    }

    [Fact]
    public void HostViewModel_NavigatesToSettingsAndBack()
    {
        var host = new CSharpStudioHostViewModel(languages: _services);

        Assert.Same(host.ManagerViewModel, host.CurrentPage);
        Assert.True(host.IsOnManagerPage);

        // Navigate to Settings
        host.NavigateToSettings("Editor");

        Assert.Same(host.SettingsViewModel, host.CurrentPage);
        Assert.False(host.IsOnManagerPage);
        Assert.Equal("Settings", host.ActiveDocumentTitle);
        Assert.Equal("Editor", host.SettingsViewModel.ActiveCategory);

        // Navigate back
        host.NavigateToPreviousPage();

        Assert.Same(host.ManagerViewModel, host.CurrentPage);
        Assert.True(host.IsOnManagerPage);
    }

    [Fact]
    public void ManagerViewModel_HasNavigateToSettingsCommand()
    {
        string? navigatedCategory = null;
        var managerVm = new CSharpManagerViewModel(
            new LocalScriptStorageService(_tempFolder),
            openScriptAction: _ => { },
            openNotebookAction: _ => { },
            languages: _services,
            navigateToSettingsAction: cat => navigatedCategory = cat);

        managerVm.NavigateToSettings("Languages");

        Assert.Equal("Languages", navigatedCategory);
    }
}
