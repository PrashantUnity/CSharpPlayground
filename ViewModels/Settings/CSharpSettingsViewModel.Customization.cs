using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public sealed partial class ThemePresetItemViewModel : ObservableObject
{
    /// <summary>The palette a saved theme was made from (its sections, locks and engine), if it came from the palette studio.</summary>
    public System.Text.Json.JsonElement? Palette { get; set; }

    /// <summary>The layout saved with the theme ("Include current layout"), applied with it.</summary>
    public System.Text.Json.JsonElement? Layout { get; set; }

    public string Id { get; init; } = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>A theme from the user's library (can be renamed and deleted); built-in ones can only be duplicated.</summary>
    public bool IsUserTheme { get; init; }

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editName = string.Empty;

    /// <summary>Delete was clicked once: the row asks to confirm before the theme is removed.</summary>
    [ObservableProperty]
    private bool _isConfirmingDelete;
    public bool IsDark { get; init; } = true;
    public string BgHex { get; init; } = "#1E1E1E";
    public string SurfaceHex { get; init; } = "#252526";
    public string AccentHex { get; init; } = "#007ACC";
    public string TextHex { get; init; } = "#CCCCCC";

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
    public bool IsGitPackage { get; init; }
    public string? GitUrl { get; init; }
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

    [ObservableProperty]
    private bool _isImportPackageFormVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportButtonText))]
    private bool _isImportingPackage;

    [ObservableProperty]
    private string _packageGitUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPackageImportStatus))]
    private string _packageImportStatus = string.Empty;

    [ObservableProperty]
    private bool _packageImportHasError;

    [ObservableProperty]
    private bool _packageImportSuccess;

    public bool HasPackageImportStatus => !string.IsNullOrEmpty(PackageImportStatus);
    public string ImportButtonText => IsImportingPackage ? "Installing..." : "Import & Install";

    public ObservableCollection<ThemePresetItemViewModel> ThemePresets { get; } = new();
    public ObservableCollection<ExtensionSettingItemViewModel> InstalledExtensions { get; } = new();

    private void InitializeCustomizationSettings()
    {
        PopulateThemePresets();
        LoadSavedThemes();
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
            ThemePresets.Add(PresetFor(theme, isUserTheme: false));
        }
    }

    private void OnEngineThemeChanged(string newThemeId)
    {
        void Update()
        {
            // A theme chosen anywhere (the light/dark toggle, the command palette, a script) is the one to restore.
            if (!string.Equals(ActiveThemeId, newThemeId, StringComparison.OrdinalIgnoreCase)) RememberActiveTheme(newThemeId);
            ActiveThemeId = newThemeId;
            foreach (var preset in AllThemePresets)
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
        var matchingPreset = AllThemePresets.FirstOrDefault(p => string.Equals(p.Id, ActiveThemeId, StringComparison.OrdinalIgnoreCase));
        ActiveThemeName = matchingPreset?.Name ?? ActiveThemeId;

        foreach (var preset in AllThemePresets)
        {
            preset.IsActive = string.Equals(preset.Id, ActiveThemeId, StringComparison.OrdinalIgnoreCase);
        }

        RegisteredCommandsCount = app.CommandPipeline.Descriptors.Count;
        ActiveHooksCount = app.HookRegistry.ActiveHookCount;

        InstalledExtensions.Clear();
        if (app.ExtensionManager != null)
        {
            var gitUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? wsRoot = app.WorkspaceService.RootPath;
            if (!string.IsNullOrWhiteSpace(wsRoot))
            {
                string lockPath = Path.Combine(wsRoot, ".frysharp", "extensions-lock.json");
                if (File.Exists(lockPath))
                {
                    try
                    {
                        var lockfile = JsonSerializer.Deserialize<WorkspaceExtensionLockfile>(
                            File.ReadAllText(lockPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (lockfile?.Dependencies != null)
                        {
                            foreach (var (k, v) in lockfile.Dependencies)
                            {
                                gitUrls[k] = v.Url;
                            }
                        }
                    }
                    catch { }
                }
            }

            foreach (var ext in app.ExtensionManager.LoadedExtensions)
            {
                bool isGit = gitUrls.TryGetValue(ext.Manifest.Id, out var gUrl) || ext.ExtensionDirectory.Contains("cache" + Path.DirectorySeparatorChar + "packages" + Path.DirectorySeparatorChar + "git", StringComparison.OrdinalIgnoreCase);
                InstalledExtensions.Add(new ExtensionSettingItemViewModel
                {
                    Id = ext.Manifest.Id,
                    Name = string.IsNullOrWhiteSpace(ext.Manifest.Name) ? ext.Manifest.Id : ext.Manifest.Name,
                    Version = ext.Manifest.Version,
                    Author = ext.Manifest.Author,
                    Description = ext.Manifest.Description,
                    DirectoryPath = ext.ExtensionDirectory,
                    IsLoaded = true,
                    IsGitPackage = isGit,
                    GitUrl = gUrl
                });
            }
        }
        LoadedExtensionsCount = InstalledExtensions.Count;
    }

    public void ApplyThemePreset(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;

        bool success = StudioAppContext.Instance.ThemeEngine.ApplyTheme(themeId);
        if (success)
        {
            ActiveThemeId = themeId;
            var preset = AllThemePresets.FirstOrDefault(p => string.Equals(p.Id, themeId, StringComparison.OrdinalIgnoreCase));
            ActiveThemeName = preset?.Name ?? themeId;
            RememberActiveTheme(themeId);
            foreach (var p in AllThemePresets)
            {
                p.IsActive = string.Equals(p.Id, themeId, StringComparison.OrdinalIgnoreCase);
            }

            // A theme saved with a layout brings its layout too.
            if (PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutSpec.FromJson(preset?.Layout) is { } layout)
            {
                PushLayoutUndo(StudioAppContext.Instance.ThemeEngine.Layout);
                ApplyLayoutNow(layout, layout.PresetId);
            }

            // A saved theme made in the palette studio opens its palette there again, ready to be changed.
            if (PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette.PaletteSpec.FromJson(preset?.Palette) is { } palette)
            {
                ChangePalette(palette);
                _appliedPaletteSpec = palette;
            }

            RefreshColorTokenValues();
            ShowNotification($"Applied theme preset: {ActiveThemeName}", isError: false);
        }
        else
        {
            ShowNotification($"Failed to find theme preset '{themeId}'", isError: true);
        }
    }

    [RelayCommand]
    public async Task ApplyThemePresetAsync(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;

        var preset = AllThemePresets.FirstOrDefault(p => string.Equals(p.Id, themeId, StringComparison.OrdinalIgnoreCase));
        string themeName = preset?.Name ?? themeId;

        ApplyThemePreset(themeId);
        await Task.CompletedTask;
    }

    public void GenerateHarmonicPalette()
    {
        var theme = HarmonicColorGenerator.GenerateRandomHarmonicTheme(isDark: true);
        StudioAppContext.Instance.ThemeEngine.RegisterTheme(theme);
        bool success = StudioAppContext.Instance.ThemeEngine.ApplyTheme(theme.Id);
        if (success)
        {
            RememberActiveTheme(theme.Id);
            ActiveThemeId = theme.Id;
            ActiveThemeName = theme.Name;

            var existingHarmonic = ThemePresets.FirstOrDefault(p => p.Id.StartsWith("harmonic-", StringComparison.OrdinalIgnoreCase));
            if (existingHarmonic != null)
            {
                ThemePresets.Remove(existingHarmonic);
            }

            ThemePresets.Add(new ThemePresetItemViewModel
            {
                Id = theme.Id,
                Name = theme.Name,
                Description = theme.Description,
                IsActive = true,
                BgHex = theme.Colors.TryGetValue("DsBgBrush", out var bg) ? bg : "#0E1117",
                SurfaceHex = theme.Colors.TryGetValue("DsSurfaceBrush", out var s) ? s : "#161B22",
                AccentHex = theme.Colors.TryGetValue("DsPrimaryBrush", out var acc) ? acc : "#38BDF8",
                TextHex = theme.Colors.TryGetValue("DsTextBrush", out var t) ? t : "#E6EDF3"
            });

            foreach (var p in AllThemePresets)
            {
                p.IsActive = string.Equals(p.Id, theme.Id, StringComparison.OrdinalIgnoreCase);
            }

            RefreshColorTokenValues();
            ShowNotification($"Generated & applied {theme.Name}!", isError: false);
        }
        else
        {
            ShowNotification("Failed to apply generated harmonic theme.", isError: true);
        }
    }

    [RelayCommand]
    public async Task GenerateHarmonicPaletteAsync()
    {
        GenerateHarmonicPalette();
        await Task.CompletedTask;
    }

    public void SetDensity(string densityName)
    {
        if (Enum.TryParse<LayoutDensity>(densityName, ignoreCase: true, out var density))
        {
            ActiveLayoutDensity = density;
            StudioAppContext.Instance.ThemeEngine.SetDensity(density);
            RememberDensity(density);
            ShowNotification($"Layout density updated to {density}.", isError: false);
        }
    }

    [RelayCommand]
    public async Task SetDensityAsync(string densityName)
    {
        if (Enum.TryParse<LayoutDensity>(densityName, ignoreCase: true, out var density))
        {
            SetDensity(densityName);
            await Task.CompletedTask;
        }
    }

    [RelayCommand]
    public async Task ReloadCustomizationScriptAsync()
    {
        if (StudioAppContext.Instance.CustomizationManager is { } mgr)
        {
            // Compiling the customization script is real work: report it (status bar, a line if it takes a while).
            using var activity = _activities.Start(new ActivityOptions("Reloading customization script", ActivityLocation.Window));
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

    [RelayCommand]
    public void ToggleImportPackageForm()
    {
        IsImportPackageFormVisible = !IsImportPackageFormVisible;
        if (IsImportPackageFormVisible)
        {
            PackageImportStatus = string.Empty;
            PackageImportHasError = false;
            PackageImportSuccess = false;
        }
    }

    [RelayCommand]
    public void UseSampleGitPackage(string? url)
    {
        PackageGitUrl = url ?? "https://github.com/PrashantUnity/FrySharp.Zig.git#v1.0.0";
        IsImportPackageFormVisible = true;
        PackageImportStatus = string.Empty;
        PackageImportHasError = false;
        PackageImportSuccess = false;
    }

    [RelayCommand]
    public async Task ImportGitPackageAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageGitUrl))
        {
            PackageImportHasError = true;
            PackageImportSuccess = false;
            PackageImportStatus = "Please enter a valid Git URL or repository identifier.";
            return;
        }

        IsImportingPackage = true;
        PackageImportHasError = false;
        PackageImportSuccess = false;
        PackageImportStatus = "Resolving and acquiring package from Git...";

        try
        {
            var app = StudioAppContext.Instance;
            var gitService = app.GitPackageService;
            string? wsRoot = app.WorkspaceService.RootPath;

            GitPackageResolutionResult res;
            if (!string.IsNullOrWhiteSpace(wsRoot) && Directory.Exists(wsRoot))
            {
                PackageImportStatus = "Cloning and adding to workspace dependencies...";
                res = await gitService.InstallPackageToWorkspaceAsync(wsRoot, PackageGitUrl.Trim());
            }
            else
            {
                PackageImportStatus = "Downloading and registering global package...";
                res = await gitService.InstallGlobalPackageAsync(PackageGitUrl.Trim());
            }

            if (!res.Success)
            {
                PackageImportHasError = true;
                PackageImportStatus = res.ErrorMessage ?? "Failed to acquire Git package.";
                ShowNotification($"Package import failed: {PackageImportStatus}", isError: true);
                return;
            }

            PackageImportStatus = "Compiling and activating extension...";
            var loadResult = await app.ExtensionManager.LoadExtensionAsync(res.ExtensionDirectory, enableHotReload: true);
            if (!loadResult.Success)
            {
                PackageImportHasError = true;
                PackageImportStatus = loadResult.ErrorMessage ?? "Extension compiled with errors.";
                ShowNotification($"Extension load error: {PackageImportStatus}", isError: true);
                return;
            }

            RefreshCustomizationData();

            PackageImportSuccess = true;
            string pkgName = res.Manifest?.Name ?? res.PackageId;
            string pkgVer = res.Manifest?.Version ?? "1.0.0";
            PackageImportStatus = $"Successfully installed {pkgName} v{pkgVer}!";
            ShowNotification($"Installed extension: {pkgName} (v{pkgVer})", isError: false);
        }
        catch (Exception ex)
        {
            PackageImportHasError = true;
            PackageImportStatus = $"Error: {ex.Message}";
            ShowNotification($"Import failed: {ex.Message}", isError: true);
        }
        finally
        {
            IsImportingPackage = false;
        }
    }

    [RelayCommand]
    public async Task RemoveExtensionAsync(ExtensionSettingItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;

        try
        {
            var app = StudioAppContext.Instance;
            await app.ExtensionManager.UnloadExtensionAsync(item.Id);

            string? wsRoot = app.WorkspaceService.RootPath;
            if (!string.IsNullOrWhiteSpace(wsRoot) && Directory.Exists(wsRoot))
            {
                await app.GitPackageService.UninstallPackageFromWorkspaceAsync(wsRoot, item.Id);
            }
            await app.GitPackageService.UninstallGlobalPackageAsync(item.Id);

            RefreshCustomizationData();
            ShowNotification($"Uninstalled extension: {item.Name}", isError: false);
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to uninstall: {ex.Message}", isError: true);
        }
    }
}
