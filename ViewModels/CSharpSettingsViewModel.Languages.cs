using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

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
            var item = new LanguageSettingItemViewModel(language);
            Languages.Add(item);
            FilteredLanguages.Add(item);
        }

        SelectedLanguage = Languages.FirstOrDefault();
        _ = RefreshAllLanguagesAsync();
    }

    [RelayCommand]
    public void SelectLanguageItem(LanguageSettingItemViewModel? item)
    {
        if (item == null) return;
        SelectedLanguage = item;
    }

    [RelayCommand]
    public async Task RefreshAllLanguagesAsync()
    {
        foreach (var item in Languages)
        {
            if (item.IsToolchainLanguage)
            {
                await RefreshLanguageToolchainAsync(item);
            }
        }
    }

    [RelayCommand]
    public async Task RefreshLanguageToolchainAsync(LanguageSettingItemViewModel item)
    {
        if (item.Provider is not { } provider) return;

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
            item.IsFound = false;
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
        if (item.Provider != null)
        {
            item.Provider.Select(null);
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
        if (item.Provider != null)
        {
            item.Provider.Select(item.CustomPath);
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
            if (item.Provider != null)
            {
                var path = item.IsAutoDetect ? null : item.CustomPath;
                item.Provider.Select(path);
                _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, path);
            }
        }
    }

    private void ResetLanguageSettingsToDefaults()
    {
        foreach (var item in Languages)
        {
            if (item.Provider != null)
            {
                item.IsAutoDetect = true;
                item.CustomPath = string.Empty;
                item.Provider.Select(null);
                _languageServices.ToolchainSettings.SetSelectedPath(item.Language.Id, null);
                _ = RefreshLanguageToolchainAsync(item);
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
    }
}
