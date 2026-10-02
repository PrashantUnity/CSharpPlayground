using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public partial class CSharpSettingsViewModel
{
    [ObservableProperty]
    private LanguageSettingItemViewModel? _selectedLanguage;

    public ObservableCollection<LanguageSettingItemViewModel> Languages { get; } = new();
    public ObservableCollection<LanguageSettingItemViewModel> FilteredLanguages { get; } = new();

    private void InitializeLanguages()
    {
        Languages.Clear();
        FilteredLanguages.Clear();

        foreach (var language in _languageServices.Registry.All)
        {
            IToolchainProvider? dotNetProvider = null;
            if (language.Id == LanguageIds.CSharp)
            {
                dotNetProvider = new PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp.CSharpToolchainProvider(
                    _languageServices.Host, _languageServices.Processes, _languageServices.ToolchainSettings);
            }

            var item = new LanguageSettingItemViewModel(language, provider: language.Toolchain, dotNetProvider: dotNetProvider, parent: this);
            if (item.IsCSharp)
            {
                item.CSharpExecutionEngine = _settingsStore.GetSettings().CSharpExecutionEngine;
            }

            Languages.Add(item);
            FilteredLanguages.Add(item);
        }

        SelectedLanguage = Languages.FirstOrDefault();
        if (SelectedLanguage != null) SelectedLanguage.IsSelected = true;
        _ = RefreshAllLanguagesAsync();
    }

    partial void OnSelectedLanguageChanged(LanguageSettingItemViewModel? oldValue, LanguageSettingItemViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    public void SelectLanguageItem(LanguageSettingItemViewModel? item)
    {
        if (item == null) return;
        SelectedLanguage = item;
    }

    [RelayCommand]
    public void SelectCSharpEngine(string? mode)
    {
        if (SelectedLanguage?.IsCSharp == true && !string.IsNullOrWhiteSpace(mode))
        {
            SelectedLanguage.SetCSharpEngine(mode);
            HasPendingChanges = true;
        }
    }

    [RelayCommand]
    public async Task RefreshAllLanguagesAsync()
    {
        foreach (var item in Languages)
        {
            if (item.IsToolchainLanguage || item.IsCSharp)
            {
                await RefreshLanguageToolchainAsync(item);
            }
        }
    }

    [RelayCommand]
    public async Task RefreshLanguageToolchainAsync(LanguageSettingItemViewModel item)
    {
        var provider = item.Provider ?? item.DotNetProvider;
        if (provider == null) return;

        item.IsChecking = true;
        item.StatusBadge = "Scanning…";
        item.StatusColor = "#E3B341";

        var query = new ToolchainQuery(null, null);
        try
        {
            provider.Refresh();
            var allFound = await Task.Run(() => provider.ListAsync(query));
            var resolution = await Task.Run(() => provider.ResolveAsync(query));

            item.UpdateResolution(resolution, allFound);
        }
        catch (Exception ex)
        {
            item.IsChecking = false;
            item.IsFound = item.IsCSharp;
            item.StatusBadge = "Scan failed";
            item.StatusColor = "#F85149";
            item.MissingTitle = "Failed to inspect environment";
            item.MissingSummary = ex.Message;
        }
    }

    [RelayCommand]
    public void SetAutoDetect(LanguageSettingItemViewModel item)
    {
        item.IsAutoDetect = true;
        item.CustomPath = string.Empty;
        var provider = item.Provider ?? item.DotNetProvider;
        if (provider != null)
        {
            provider.Select(null);
            _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, null);
        }
        HasPendingChanges = true;
        _ = RefreshLanguageToolchainAsync(item);
    }

    [RelayCommand]
    public void SetCustomPath(LanguageSettingItemViewModel item)
    {
        item.IsAutoDetect = false;
        if (!string.IsNullOrWhiteSpace(item.CustomPath))
        {
            ApplyCustomPath(item, item.CustomPath);
        }
    }

    public void ApplyCustomPath(LanguageSettingItemViewModel item, string path)
    {
        item.IsAutoDetect = false;
        item.CustomPath = path.Trim();
        var provider = item.Provider ?? item.DotNetProvider;
        if (provider != null)
        {
            provider.Select(item.CustomPath);
            _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, item.CustomPath);
        }
        HasPendingChanges = true;
        _ = RefreshLanguageToolchainAsync(item);
    }

    [RelayCommand]
    public void SelectDiscoveredToolchain(ToolchainInfo? info)
    {
        if (SelectedLanguage == null || info == null) return;
        SelectedLanguage.SelectedDiscoveredToolchain = info;
        ApplyCustomPath(SelectedLanguage, info.ExecutablePath);
    }

    [RelayCommand]
    public async Task RunToolchainActionAsync(string? actionId)
    {
        if (SelectedLanguage?.Provider is not { } provider || string.IsNullOrWhiteSpace(actionId)) return;

        SelectedLanguage.IsRunningAction = true;
        SelectedLanguage.ActionStatusMessage = "Running action…";
        SelectedLanguage.ActionOutputLog = string.Empty;

        var query = new ToolchainQuery(null, null);
        void AppendOutput(string text)
        {
            SelectedLanguage.ActionOutputLog += text;
        }

        try
        {
            var result = await Task.Run(() => provider.RunActionAsync(actionId, query, AppendOutput));
            SelectedLanguage.ActionStatusMessage = result.Success ? result.Message : $"⚠️ {result.Message}";
            SelectedLanguage.ActionOutputLog += $"\n[Finished: {result.Message}]\n";
            await RefreshLanguageToolchainAsync(SelectedLanguage);
        }
        catch (Exception ex)
        {
            SelectedLanguage.ActionStatusMessage = $"Action failed: {ex.Message}";
            SelectedLanguage.ActionOutputLog += $"\n[Error: {ex.Message}]\n";
        }
        finally
        {
            SelectedLanguage.IsRunningAction = false;
        }
    }

    private void SaveLanguageSettings()
    {
        foreach (var item in Languages)
        {
            var provider = item.Provider ?? item.DotNetProvider;
            if (provider != null)
            {
                var path = item.IsAutoDetect ? null : item.CustomPath;
                provider.Select(path);
                _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, path);
            }
            if (item.IsCSharp)
            {
                var settings = _settingsStore.GetSettings();
                settings.CSharpExecutionEngine = item.CSharpExecutionEngine;
                _settingsStore.SaveSettings(settings);
            }
        }
    }

    private void ResetLanguageSettingsToDefaults()
    {
        foreach (var item in Languages)
        {
            var provider = item.Provider ?? item.DotNetProvider;
            if (provider != null)
            {
                item.IsAutoDetect = true;
                item.CustomPath = string.Empty;
                provider.Select(null);
                _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, null);
                _ = RefreshLanguageToolchainAsync(item);
            }
            if (item.IsCSharp)
            {
                item.CSharpExecutionEngine = "internal";
            }
        }
    }

    private void ApplySearchFilter(string query)
    {
        FilteredLanguages.Clear();
        var trimmed = query?.Trim() ?? string.Empty;
        foreach (var item in Languages)
        {
            if (string.IsNullOrEmpty(trimmed) ||
                item.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                item.RuntimeDescription.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                item.ActiveToolchainLabel.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                FilteredLanguages.Add(item);
            }
        }

        if (SelectedLanguage != null && !FilteredLanguages.Contains(SelectedLanguage))
        {
            SelectedLanguage = FilteredLanguages.FirstOrDefault();
        }
        else if (SelectedLanguage == null)
        {
            SelectedLanguage = FilteredLanguages.FirstOrDefault();
        }

        KeymapSearchQuery = trimmed;
    }
}
