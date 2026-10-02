using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public sealed partial class ThemePresetItemViewModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsDark { get; init; } = true;
    public string BgHex { get; init; } = "#1E1E1E";
    public string AccentHex { get; init; } = "#007ACC";

    [ObservableProperty]
    private bool _isActive;
}

public sealed class ExtensionSettingItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public bool IsLoaded { get; init; } = true;
}

public partial class CSharpSettingsViewModel
{
    private Action<ScriptDocumentItem>? _openScriptAction;

    [ObservableProperty]
    private string _customizationScriptPath = string.Empty;

    [ObservableProperty]
    private bool _isCustomizationHotReloadActive = true;

    [ObservableProperty]
    private string _activeThemeName = "Dark+ (Default)";

    [ObservableProperty]
    private string _activeThemeId = "dark-plus";

    [ObservableProperty]
    private LayoutDensity _activeLayoutDensity = LayoutDensity.Comfortable;

    [ObservableProperty]
    private int _registeredCommandsCount;

    [ObservableProperty]
    private int _activeHooksCount;

    [ObservableProperty]
    private int _loadedExtensionsCount;

    [ObservableProperty]
    private bool _isGlobalScriptExisting;

    public ObservableCollection<ThemePresetItemViewModel> ThemePresets { get; } = new();
    public ObservableCollection<ExtensionSettingItemViewModel> InstalledExtensions { get; } = new();

    private void InitializeCustomizationSettings()
    {
        PopulateThemePresets();
        RefreshCustomizationData();

        StudioAppContext.Instance.ThemeEngine.ThemeChanged += OnEngineThemeChanged;
        StudioAppContext.Instance.CommandPipeline.CommandsChanged += OnCommandsOrHooksChanged;
        StudioAppContext.Instance.HookRegistry.HooksChanged += OnCommandsOrHooksChanged;
    }

    private void PopulateThemePresets()
    {
        ThemePresets.Clear();
        foreach (var theme in BuiltInThemes.All)
        {
            string bg = theme.Colors.TryGetValue("DsBgBrush", out var b) ? b : (theme.IsDark ? "#0D1117" : "#FFFFFF");
            string accent = theme.Colors.TryGetValue("DsPrimaryBrush", out var a) ? a : "#2F81F7";

            ThemePresets.Add(new ThemePresetItemViewModel
            {
                Id = theme.Id,
                Name = theme.Name,
                Description = theme.Description,
                IsDark = theme.IsDark,
                BgHex = bg,
                AccentHex = accent,
                IsActive = string.Equals(StudioAppContext.Instance.ThemeEngine.ActiveThemeId, theme.Id, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private void OnEngineThemeChanged(string newThemeId)
    {
        void Update()
        {
            ActiveThemeId = newThemeId;
            foreach (var preset in ThemePresets)
            {
                preset.IsActive = string.Equals(preset.Id, newThemeId, StringComparison.OrdinalIgnoreCase);
                if (preset.IsActive)
                {
                    ActiveThemeName = preset.Name;
                }
            }
        }

        if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
        else
        {
            Update();
        }
    }

    private void OnCommandsOrHooksChanged()
    {
        void Update()
        {
            RegisteredCommandsCount = StudioAppContext.Instance.CommandPipeline.Descriptors.Count;
            ActiveHooksCount = StudioAppContext.Instance.HookRegistry.ActiveHookCount;
        }

        if (Avalonia.Application.Current != null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
        else
        {
            Update();
        }
    }

    public void RefreshCustomizationData()
    {
        var app = StudioAppContext.Instance;
        CustomizationScriptPath = app.CustomizationManager?.Storage.GlobalInitScriptPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".frysharp", "init.csx");

        IsGlobalScriptExisting = File.Exists(CustomizationScriptPath);
        IsCustomizationHotReloadActive = app.CustomizationManager?.IsWatching == true;

        ActiveThemeId = app.ThemeEngine.ActiveThemeId;
        var matchingPreset = ThemePresets.FirstOrDefault(p => string.Equals(p.Id, ActiveThemeId, StringComparison.OrdinalIgnoreCase));
        ActiveThemeName = matchingPreset?.Name ?? ActiveThemeId;

        foreach (var preset in ThemePresets)
        {
            preset.IsActive = string.Equals(preset.Id, ActiveThemeId, StringComparison.OrdinalIgnoreCase);
        }

        RegisteredCommandsCount = app.CommandPipeline.Descriptors.Count;
        ActiveHooksCount = app.HookRegistry.ActiveHookCount;

        InstalledExtensions.Clear();
        if (app.ExtensionManager != null)
        {
            foreach (var ext in app.ExtensionManager.LoadedExtensions)
            {
                InstalledExtensions.Add(new ExtensionSettingItemViewModel
                {
                    Id = ext.Manifest.Id,
                    Name = string.IsNullOrWhiteSpace(ext.Manifest.Name) ? ext.Manifest.Id : ext.Manifest.Name,
                    Version = ext.Manifest.Version,
                    Author = ext.Manifest.Author,
                    Description = ext.Manifest.Description,
                    DirectoryPath = ext.ExtensionDirectory,
                    IsLoaded = true
                });
            }
        }
        LoadedExtensionsCount = InstalledExtensions.Count;
    }

    [RelayCommand]
    public void ApplyThemePreset(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;

        bool success = StudioAppContext.Instance.ThemeEngine.ApplyTheme(themeId);
        if (success)
        {
            ActiveThemeId = themeId;
            var preset = ThemePresets.FirstOrDefault(p => string.Equals(p.Id, themeId, StringComparison.OrdinalIgnoreCase));
            ActiveThemeName = preset?.Name ?? themeId;
            foreach (var p in ThemePresets)
            {
                p.IsActive = string.Equals(p.Id, themeId, StringComparison.OrdinalIgnoreCase);
            }
            ShowNotification($"Applied theme preset: {ActiveThemeName}", isError: false);
        }
        else
        {
            ShowNotification($"Failed to find theme preset '{themeId}'", isError: true);
        }
    }

    [RelayCommand]
    public void SetDensity(string densityName)
    {
        if (Enum.TryParse<LayoutDensity>(densityName, ignoreCase: true, out var density))
        {
            ActiveLayoutDensity = density;
            StudioAppContext.Instance.ThemeEngine.SetDensity(density);
            ShowNotification($"Layout density updated to {density}.", isError: false);
        }
    }

    [RelayCommand]
    public async Task ReloadCustomizationScriptAsync()
    {
        if (StudioAppContext.Instance.CustomizationManager is { } mgr)
        {
            var result = await mgr.ReloadAsync();
            if (result.Success)
            {
                RefreshCustomizationData();
                ShowNotification("Global customization script reloaded and applied successfully!", isError: false);
            }
            else
            {
                ShowNotification($"Customization script error: {result.ErrorMessage}", isError: true);
            }
        }
        else
        {
            ShowNotification("Customization manager not initialized.", isError: true);
        }
    }

    [RelayCommand]
    public async Task OpenGlobalScriptAsync()
    {
        string path = CustomizationScriptPath;
        if (StudioAppContext.Instance.CustomizationManager is { } mgr)
        {
            await mgr.Storage.EnsureInitScriptExistsAsync();
            path = mgr.Storage.GlobalInitScriptPath;
            CustomizationScriptPath = path;
            IsGlobalScriptExisting = true;
        }

        if (_openScriptAction != null)
        {
            var item = new ScriptDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = Path.GetFileName(path),
                SourceFilePath = path,
                LanguageId = PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageIds.CSharp,
                Code = File.Exists(path) ? await File.ReadAllTextAsync(path) : string.Empty
            };
            _openScriptAction.Invoke(item);
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowNotification($"Unable to open file in external editor: {ex.Message}", isError: true);
            }
        }
    }
}
