using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;
using CSharpSettingsViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.CSharpSettingsViewModel;
using CSharpStudioHostViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.CSharpStudioHostViewModel;

namespace CSharpEditorPlugin.Tests;

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
        Assert.True((bool)vm.ConvertTabsToSpaces);
        Assert.False((bool)vm.WordWrap);
        Assert.True((bool)vm.ShowLineNumbers);
        Assert.True((bool)vm.EnableSyntaxHighlighting);
        Assert.True((bool)vm.EnableAutoCompletion);
        Assert.Equal(13.0, vm.FontSize);
        Assert.Equal(0, vm.ExecutionTimeoutSeconds);
        Assert.False((bool)vm.AutoClearConsoleOnRun);
        Assert.Equal(10000, vm.MaxTerminalOutputLines);
        Assert.True((bool)vm.NullableChecksEnabled);
        Assert.Equal((string?)"13.0", (string?)vm.LanguageVersion);
        Assert.False((bool)vm.HasPendingChanges);
    }

    [Fact]
    public void Settings_CanModifyAndPersistViaApply()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.TabSize = 2;
        vm.WordWrap = true;
        vm.ExecutionTimeoutSeconds = 45;
        vm.LanguageVersion = "12.0";

        Assert.True((bool)vm.HasPendingChanges);

        vm.Apply();

        Assert.False((bool)vm.HasPendingChanges);
        Assert.True((bool)vm.HasStatusMessage);

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
        Assert.False((bool)vm.WordWrap);
        Assert.Equal(0, vm.ExecutionTimeoutSeconds);
        Assert.True((bool)vm.HasPendingChanges);
    }

    [Fact]
    public void EditorFeatureToggles_CanBeDisabledPersistedAndReset()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        // Defaults are both ON
        Assert.True((bool)vm.EnableSyntaxHighlighting);
        Assert.True((bool)vm.EnableAutoCompletion);

        // User disables both
        vm.EnableSyntaxHighlighting = false;
        vm.EnableAutoCompletion = false;
        Assert.True((bool)vm.HasPendingChanges);

        // Persist
        vm.Apply();
        Assert.False((bool)vm.HasPendingChanges);

        // Reload fresh from disk
        var freshStore = new StudioSettingsStore(Path.Combine(_tempFolder, "studio_settings.json"));
        var loaded = freshStore.GetSettings();
        Assert.False(loaded.EnableSyntaxHighlighting);
        Assert.False(loaded.EnableAutoCompletion);

        // ResetDefaults brings them back ON
        vm.ResetDefaults();
        Assert.True((bool)vm.EnableSyntaxHighlighting);
        Assert.True((bool)vm.EnableAutoCompletion);
        Assert.True((bool)vm.HasPendingChanges);
    }

    [Fact]
    public void Languages_ListsRegisteredLanguages()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.Languages);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.CSharp);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.Python);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.JavaScript);
        Assert.Contains(vm.Languages, l => l.Language.Id == LanguageIds.Java);
    }

    [Fact]
    public void Languages_CSharp_IsConfiguredAsInProcess()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);

        Assert.False(csharp.IsToolchainLanguage);
        Assert.True((bool)csharp.IsFound);
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

        Assert.False((bool)python.IsAutoDetect);
        Assert.Equal((string?)customBinary, (string?)python.CustomPath);
        Assert.Equal(customBinary, _services.ToolchainSettings.GetSelectedPath(LanguageIds.Python));
    }

    [Fact]
    public void Languages_CanSelectCustomJdkPathForJava()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var java = vm.Languages.FirstOrDefault(l => l.Language.Id == LanguageIds.Java);
        Assert.NotNull(java);

        const string customBinary = "/custom/path/to/java";
        vm.ApplyCustomPath(java, customBinary);

        Assert.False((bool)java.IsAutoDetect);
        Assert.Equal((string?)customBinary, (string?)java.CustomPath);
        Assert.Equal(customBinary, _services.ToolchainSettings.GetSelectedPath(LanguageIds.Java));
    }

    [Fact]
    public void Languages_CanSetAutoDetectMode()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var js = vm.Languages.FirstOrDefault(l => l.Language.Id == LanguageIds.JavaScript);
        Assert.NotNull(js);

        vm.ApplyCustomPath(js, "/usr/bin/node");
        Assert.False((bool)js.IsAutoDetect);

        vm.SetAutoDetect(js);
        Assert.True((bool)js.IsAutoDetect);
        Assert.Empty((string)js.CustomPath);
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
        Assert.True((bool)host.IsOnManagerPage);

        // Navigate to Settings
        host.NavigateToSettings("Editor");

        Assert.Same(host.SettingsViewModel, host.CurrentPage);
        Assert.False((bool)host.IsOnManagerPage);
        Assert.Equal((string?)"Settings", (string?)host.ActiveDocumentTitle);
        Assert.Equal((string?)"Editor", (string?)host.SettingsViewModel.ActiveCategory);

        // Navigate back
        host.NavigateToPreviousPage();

        Assert.Same(host.ManagerViewModel, host.CurrentPage);
        Assert.True((bool)host.IsOnManagerPage);
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

    [Fact]
    public void Languages_ExposeCapabilitiesAndStorageProfile()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);

        Assert.True(csharp.IsCSharp);
        Assert.NotEmpty(csharp.CapabilityItems);
        Assert.Contains(csharp.CapabilityItems, c => c.Name == "Live Diagnostics" && c.IsSupported);
        Assert.Contains(csharp.CapabilityItems, c => c.Name == "Interactive Debugging" && c.IsSupported);
        Assert.Contains(".frycs", csharp.FileExtensionsDisplay);
        Assert.Equal("FryDocument (.frycs)", csharp.StorageBadge);

        var python = vm.Languages.First(l => l.Language.Id == LanguageIds.Python);
        Assert.False(python.IsCSharp);
        Assert.NotEmpty(python.CapabilityItems);
        Assert.Contains(python.CapabilityItems, c => c.Name == "Variable Sharing" && c.IsSupported);
        Assert.Contains(python.CapabilityItems, c => c.Name == "Notebook Code Cells" && c.IsSupported);
        Assert.Contains(".py", python.FileExtensionsDisplay);
        Assert.Equal("Source File", python.StorageBadge);
    }

    [Fact]
    public void Languages_SelectionUpdatesIsSelectedFlag()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);
        var python = vm.Languages.First(l => l.Language.Id == LanguageIds.Python);

        vm.SelectLanguageItem(csharp);
        Assert.True((bool)csharp.IsSelected);
        Assert.False((bool)python.IsSelected);

        vm.SelectLanguageItem(python);
        Assert.False((bool)csharp.IsSelected);
        Assert.True((bool)python.IsSelected);
    }

    [Fact]
    public void Keymap_CategoryChipsAndSearchFilteringWork()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.KeymapCategoryChips);
        Assert.True((bool)vm.KeymapCategoryChips.First(c => c.Name == "All").IsSelected);
        Assert.Equal(vm.Shortcuts.Count, vm.FilteredShortcuts.Count);

        // Filter by category
        var debugChip = vm.KeymapCategoryChips.First(c => c.Name == "Debug");
        vm.SelectKeymapCategory(debugChip);

        Assert.True((bool)debugChip.IsSelected);
        Assert.All(vm.FilteredShortcuts, s => Assert.Equal("Debug", s.Category));

        // Filter by search query
        vm.KeymapSearchQuery = "F10";
        Assert.Single(vm.FilteredShortcuts);
        Assert.Equal("Step Over", vm.FilteredShortcuts[0].Action);

        // Clear filter
        var allChip = vm.KeymapCategoryChips.First(c => c.Name == "All");
        vm.SelectKeymapCategory(allChip);
        vm.KeymapSearchQuery = string.Empty;
        Assert.Equal(vm.Shortcuts.Count, vm.FilteredShortcuts.Count);
    }

    [Fact]
    public void Languages_CSharp_HasToolchainConfigurationAndExecutionEngine()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);

        Assert.True(csharp.IsCSharp);
        Assert.False(csharp.IsToolchainLanguage); // Preserved invariant
        Assert.True(csharp.HasDotNetToolchain);
        Assert.True(csharp.HasToolchainConfiguration);
        Assert.NotNull(csharp.DotNetProvider);
        Assert.Equal((string?)"internal", (string?)csharp.CSharpExecutionEngine);
        Assert.True(csharp.IsInProcessRoslynSelected);
        Assert.False(csharp.IsExternalDotNetSelected);
    }

    [Fact]
    public void Languages_CanSelectCSharpExecutionEngineAndCustomPath()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);
        var csharp = vm.Languages.First(l => l.Language.Id == LanguageIds.CSharp);

        // Switch to external .NET SDK engine
        vm.SelectCSharpEngine("external");
        Assert.Equal((string?)"external", (string?)csharp.CSharpExecutionEngine);
        Assert.True(csharp.IsExternalDotNetSelected);
        Assert.False(csharp.IsInProcessRoslynSelected);
        Assert.True((bool)vm.HasPendingChanges);

        // Set custom dotnet binary path
        const string customDotNet = "/usr/local/share/dotnet/dotnet";
        vm.ApplyCustomPath(csharp, customDotNet);
        Assert.False((bool)csharp.IsAutoDetect);
        Assert.Equal((string?)customDotNet, (string?)csharp.CustomPath);
        Assert.Equal(customDotNet, _services.ToolchainSettings.GetSelectedPath(LanguageIds.CSharp));

        // Save settings
        vm.Apply();
        Assert.False((bool)vm.HasPendingChanges);
        Assert.Equal("external", _settingsStore.GetSettings().CSharpExecutionEngine);

        // Reset to defaults
        vm.ResetDefaults();
        Assert.Equal((string?)"internal", (string?)csharp.CSharpExecutionEngine);
        Assert.True(csharp.IsInProcessRoslynSelected);
        Assert.True((bool)csharp.IsAutoDetect);
    }

    private sealed class TestDummyLanguage : LanguageDefinition
    {
        public override string Id => "testdummy";
        public override string DisplayName => "TestDummy";
        public override IReadOnlyList<string> FileExtensions => [".dummy"];
        public override IReadOnlyList<string> Aliases => ["dummy"];
        public override string AccentHex => "#987654";
    }

    [Fact]
    public async Task RefreshAllLanguagesAsync_DoesNotThrow_WhenLanguagesCollectionModifiedConcurrently()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        // Start RefreshAllLanguagesAsync
        var refreshTask = vm.RefreshAllLanguagesAsync();

        // Concurrently mutate the Languages collection (simulating extension registration during scan)
        var dummy = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.LanguageSettingItemViewModel(new TestDummyLanguage(), parent: vm);
        vm.Languages.Add(dummy);
        vm.FilteredLanguages.Add(dummy);

        // Also trigger registry sync
        var regToken = _services.Registry.Register(new TestDummyLanguage());
        try
        {
            vm.SynchronizeLanguagesFromRegistry();

            // Must complete without throwing InvalidOperationException: Collection was modified
            await refreshTask;
        }
        finally
        {
            regToken.Dispose();
        }
    }
}
