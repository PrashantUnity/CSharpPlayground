using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>
/// Domain partial for Theme &amp; Colors settings, handling token inspection, category filtering,
/// and clipboard exports (Avalonia XAML, CSS Custom Properties, Tailwind CSS v4, W3C DTCG tokens).
/// </summary>
public partial class CSharpSettingsViewModel
{
    [ObservableProperty]
    private string _tokenSearchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedTokenCategory = "All";

    [ObservableProperty]
    private int _matchingTokensCount;

    public IReadOnlyList<string> AvailableTokenCategories { get; } =
    [
        "All",
        "Surfaces",
        "Accents",
        "Borders",
        "Typography",
        "Editor",
        "Status",
        "Material3",
        "Badges",
        "Navigation"
    ];

    public ObservableCollection<CategoryFilterItemViewModel> CategoryFilters { get; } = new();
    public ObservableCollection<ColorTokenItemViewModel> ColorTokens { get; } = new();
    public PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common.RangeObservableCollection<ColorTokenItemViewModel> FilteredColorTokens { get; } = new();

    public int TotalTokensCount => ColorTokens.Count;

    partial void OnTokenSearchQueryChanged(string value) => FilterColorTokens();

    partial void OnSelectedTokenCategoryChanged(string value)
    {
        foreach (var f in CategoryFilters)
        {
            f.IsSelected = string.Equals(f.Name, value, StringComparison.OrdinalIgnoreCase);
        }
        FilterColorTokens();
    }

    private void PopulateColorTokens()
    {
        ColorTokens.Clear();
        foreach (var desc in HarmonicColorGenerator.GetAllTokenDescriptors())
        {
            ColorTokens.Add(new ColorTokenItemViewModel
            {
                Key = desc.Key,
                DisplayName = desc.DisplayName,
                Category = desc.Category,
                Description = desc.Description,
                CurrentHex = desc.DefaultDarkHex
            });
        }
        FilterColorTokens();
    }

    public void RefreshColorTokenValues()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        foreach (var token in ColorTokens)
        {
            string? hex = engine.GetColor(token.Key);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                token.CurrentHex = hex;
            }
        }
        FilterColorTokens();
    }

    private void FilterColorTokens()
    {
        var matching = new List<ColorTokenItemViewModel>();
        string q = (TokenSearchQuery ?? string.Empty).Trim();
        string cat = SelectedTokenCategory ?? "All";

        foreach (var t in ColorTokens)
        {
            bool matchCat = string.Equals(cat, "All", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(t.Category, cat, StringComparison.OrdinalIgnoreCase);

            if (!matchCat) continue;

            if (string.IsNullOrEmpty(q) ||
                t.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.CurrentHex.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                matching.Add(t);
            }
        }

        // Applying a theme changes the tokens' colours, not which tokens are listed: their swatches follow by binding,
        // so the list is only replaced when the set really changed, and then in one go (it used to be cleared and
        // refilled row by row, rebuilding every swatch, on every theme).
        if (!matching.SequenceEqual(FilteredColorTokens)) FilteredColorTokens.ReplaceAll(matching);
        MatchingTokensCount = FilteredColorTokens.Count;
    }

    [RelayCommand]
    public void SelectTokenCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        SelectedTokenCategory = category;
    }

    [RelayCommand]
    public void ClearTokenSearch()
    {
        TokenSearchQuery = string.Empty;
    }

    [RelayCommand]
    public async Task CopySwatchHexAsync(TonalSwatchItemViewModel swatch)
    {
        if (swatch == null || string.IsNullOrWhiteSpace(swatch.Hex)) return;

        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(swatch.Hex);
            }
            swatch.IsCopied = true;
            ShowNotification($"Copied stop {swatch.Stop}: {swatch.Hex}", isError: false);
            _ = Task.Delay(1500).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => swatch.IsCopied = false);
            });
        }
        catch (Exception ex)
        {
            ShowNotification($"Clipboard copy failed: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    public async Task CopyAvaloniaXamlAsync()
    {
        var theme = BuildPaletteTheme();
        string xaml = ThemeExportService.ExportToAvaloniaXaml(theme);
        await CopyExportTextAsync(xaml, "Copied Avalonia ResourceDictionary XAML!");
    }

    [RelayCommand]
    public async Task CopyCssVariablesAsync()
    {
        var light = BuildPaletteTheme(isDark: false);
        var dark = BuildPaletteTheme(isDark: true);
        string css = ThemeExportService.ExportToCssVariables(light, dark);
        await CopyExportTextAsync(css, "Copied CSS Custom Properties!");
    }

    [RelayCommand]
    public async Task CopyTailwindAsync()
    {
        var theme = BuildPaletteTheme();
        string tw = ThemeExportService.ExportToTailwindV4(theme);
        await CopyExportTextAsync(tw, "Copied Tailwind CSS v4 @theme!");
    }

    [RelayCommand]
    public async Task CopyDtcgJsonAsync()
    {
        var theme = BuildPaletteTheme();
        string json = ThemeExportService.ExportToDtcgJson(theme);
        await CopyExportTextAsync(json, "Copied W3C DTCG Token JSON!");
    }

    [RelayCommand]
    public async Task CopySwiftUiAsync()
    {
        var theme = BuildPaletteTheme();
        string swift = ThemeExportService.ExportToSwiftUI(theme);
        await CopyExportTextAsync(swift, "Copied SwiftUI Color extension!");
    }

    [RelayCommand]
    public async Task CopyComposeAsync()
    {
        var theme = BuildPaletteTheme();
        string compose = ThemeExportService.ExportToJetpackCompose(theme);
        await CopyExportTextAsync(compose, "Copied Jetpack Compose Color tokens!");
    }

    [RelayCommand]
    public async Task ImportDtcgFromClipboardAsync()
    {
        try
        {
            string? json = null;
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                json = await desktop.MainWindow.Clipboard.TryGetTextAsync();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                ShowNotification("Clipboard is empty or does not contain text.", isError: true);
                return;
            }

            ImportDtcgJson(json);
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to import DTCG JSON: {ex.Message}", isError: true);
        }
    }

    /// <summary>Imports design tokens (DTCG JSON), saves them to My themes and applies them. Returns the saved theme.</summary>
    internal SavedTheme? ImportDtcgJson(string json)
    {
        try
        {
            var imported = ThemeExportService.ImportFromDtcgJson(json, "imported-harmonic", "Imported DTCG Theme", IsThemeDarkMode);
            if (imported == null || imported.Colors.Count == 0)
            {
                ShowNotification("No valid design tokens found in clipboard JSON.", isError: true);
                return null;
            }

            // Into the user's library (named after the Save-as box when something is typed there), so it is still there
            // after a restart and can be renamed or deleted like any saved theme.
            var name = string.IsNullOrWhiteSpace(NewThemeName) ? $"Imported theme {DateTime.Now:d MMM HH:mm}" : NewThemeName.Trim();
            var saved = Library.Save(name, imported, source: "imported");
            var engine = StudioAppContext.Instance.ThemeEngine;
            engine.RegisterTheme(saved.Theme);
            UserThemes.Insert(0, PresetFor(saved));
            engine.ApplyTheme(saved.Id);
            RememberActiveTheme(saved.Id);
            NewThemeName = string.Empty;

            if (imported.Colors.TryGetValue("DsPrimaryBrush", out var primaryHex))
            {
                SeedHexInput = primaryHex;
            }

            RefreshColorTokenValues();
            ShowNotification($"Imported {imported.Colors.Count} tokens and saved them as \"{saved.Name}\".", isError: false);
            return saved;
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to import DTCG JSON: {ex.Message}", isError: true);
            return null;
        }
    }

    private async Task CopyExportTextAsync(string text, string successMessage)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(text);
            }
            ShowNotification(successMessage, isError: false);
        }
        catch (Exception ex)
        {
            ShowNotification($"Clipboard copy failed: {ex.Message}", isError: true);
        }
    }

    public void ResetDefaultTheme()
    {
        string defaultId = IsThemeDarkMode ? "dark-plus" : "light-plus";
        ApplyThemePreset(defaultId);
    }

    [RelayCommand]
    public async Task ResetDefaultThemeAsync()
    {
        string defaultId = IsThemeDarkMode ? "dark-plus" : "light-plus";
        await ApplyThemePresetAsync(defaultId);
    }

    [RelayCommand]
    public async Task CopyTokenHexAsync(ColorTokenItemViewModel item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.CurrentHex)) return;

        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(item.CurrentHex);
            }
            item.IsCopied = true;
            ShowNotification($"Copied {item.Key}: {item.CurrentHex}", isError: false);

            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => item.IsCopied = false);
            });
        }
        catch (Exception ex)
        {
            ShowNotification($"Clipboard copy failed: {ex.Message}", isError: true);
        }
    }

    private void RefreshThemePresetsWithHarmonic(ThemeDefinition theme)
    {
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
            IsDark = theme.IsDark,
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
    }
}
