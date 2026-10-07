using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>
/// Settings save themselves: every change is written to the user's preferences a moment after it is made (there is no
/// Apply button), and the look of the studio (theme, generated or imported themes, harmony controls, density) is
/// remembered so a restart comes back exactly as it was. Saved themes live in the theme library, as many as the user
/// wants, each with the name they gave it.
/// </summary>
public partial class CSharpSettingsViewModel
{
    /// <summary>How long after the last change Settings saves itself.</summary>
    internal static TimeSpan AutoSaveDelay { get; set; } = TimeSpan.FromMilliseconds(400);

    // True while the page fills its controls from the saved settings: that is not a change to save.
    private bool _initializing = true;
    private DispatcherTimer? _autoSaveTimer;

    /// <summary>"Saved", "Saving…": shown where the Apply button used to be.</summary>
    [ObservableProperty]
    private string _saveStatusText = "All changes saved";

    /// <summary>The name typed for "Save theme as".</summary>
    [ObservableProperty]
    private string _newThemeName = string.Empty;

    private ThemeLibraryStore Library => _languageServices.ThemeLibrary;

    partial void OnHasPendingChangesChanged(bool value)
    {
        if (!value || _initializing) return;
        SaveStatusText = "Saving…";
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        // Without a running UI (tests) nothing would ever tick the timer: Apply() and FlushAutoSave() save explicitly.
        if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime) return;
        if (_autoSaveTimer == null)
        {
            _autoSaveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = AutoSaveDelay };
            _autoSaveTimer.Tick += (_, _) =>
            {
                _autoSaveTimer!.Stop();
                SaveChanges(flush: false);
            };
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    /// <summary>Saves what is waiting now (the studio closing).</summary>
    public void FlushAutoSave()
    {
        _autoSaveTimer?.Stop();
        if (HasPendingChanges) SaveChanges(flush: true);
        _settingsStore.Flush();
    }

    private void SaveChanges(bool flush)
    {
        SaveEditorSettings();
        SaveLanguageSettings();
        HasPendingChanges = false;
        SaveStatusText = "All changes saved";
        if (flush) _settingsStore.Flush();
    }

    private void FinishInitialization()
    {
        _initializing = false;
        HasPendingChanges = false;
    }

    // ── Appearance ───────────────────────────────────────────────────────────

    /// <summary>Remembers the theme now applied: a built-in or library theme by id, any other (generated, imported) whole.</summary>
    private void RememberActiveTheme(string themeId)
    {
        if (_initializing) return;
        var engine = StudioAppContext.Instance.ThemeEngine;
        var theme = engine.GetTheme(themeId);
        var snapshot = DynamicThemeEngine.IsBuiltInTheme(themeId) || ThemeLibraryStore.IsLibraryId(themeId) ? null : theme;
        _settingsStore.Update(s =>
        {
            s.Appearance.ActiveThemeId = themeId;
            s.Appearance.ActiveThemeSnapshot = snapshot;
            if (theme != null) s.Appearance.PreferDark = theme.IsDark;
        });
    }

    private void RememberHarmony()
    {
        if (_initializing) return;
        var harmony = CurrentHarmonySettings();
        _settingsStore.Update(s => s.Appearance.Harmony = harmony);
    }

    private void RememberDensity(LayoutDensity density)
    {
        if (_initializing) return;
        _settingsStore.Update(s => s.Appearance.Density = density.ToString());
    }

    private HarmonyStudioSettings CurrentHarmonySettings() => new()
    {
        Mode = SelectedHarmonyMode.ToString(),
        HueDegrees = SelectedHueDegrees,
        IsDark = IsThemeDarkMode,
        SaturationBoost = SaturationBoost,
        AccentShift = AccentShift,
        NeutralTint = NeutralTint,
        SemanticPull = SemanticPull,
        ContrastTarget = ContrastTarget,
        VisionDeficiency = SelectedVisionDeficiency.ToString(),
    };

    // Puts the theme studio's controls back as they were (before the previews are worked out from them).
    private void RestoreHarmonyControls()
    {
        var harmony = _settingsStore.GetSettings().Appearance.Harmony;
        if (harmony == null) return;
        if (Enum.TryParse<ColorHarmonyMode>(harmony.Mode, ignoreCase: true, out var mode)) SelectedHarmonyMode = mode;
        if (Enum.TryParse<PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath.VisionDeficiency>(harmony.VisionDeficiency, ignoreCase: true, out var vision))
        {
            SelectedVisionDeficiency = vision;
        }

        SelectedHueDegrees = harmony.HueDegrees;
        IsThemeDarkMode = harmony.IsDark;
        SaturationBoost = harmony.SaturationBoost;
        AccentShift = harmony.AccentShift;
        NeutralTint = harmony.NeutralTint;
        SemanticPull = harmony.SemanticPull;
        ContrastTarget = harmony.ContrastTarget;
        var density = _settingsStore.GetSettings().Appearance.Density;
        if (Enum.TryParse<LayoutDensity>(density, ignoreCase: true, out var saved)) ActiveLayoutDensity = saved;
    }

    // ── Theme library ────────────────────────────────────────────────────────

    /// <summary>The user's saved themes, most recently changed first (the curated ones are in <see cref="ThemePresets"/>).</summary>
    public ObservableCollection<ThemePresetItemViewModel> UserThemes { get; } = new();

    /// <summary>Curated and saved themes together, for marking the active one.</summary>
    private IEnumerable<ThemePresetItemViewModel> AllThemePresets => ThemePresets.Concat(UserThemes);

    /// <summary>Loads the saved themes off the UI thread (a library can hold hundreds) and lists them with the presets.</summary>
    private void LoadSavedThemes()
    {
        var library = Library;
        if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime)
        {
            // No UI to keep responsive (tests, tools): read the library now rather than on a busy thread pool.
            ShowSavedThemes(library.List());
            return;
        }

        Task.Run(library.List).ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully) return;
            void Show()
            {
                ShowSavedThemes(t.Result);
            }

            if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime || Dispatcher.UIThread.CheckAccess()) Show();
            else Dispatcher.UIThread.Post(Show);
        }, TaskScheduler.Default);
    }

    /// <summary>The library loaded, for tests and tools.</summary>
    internal void LoadSavedThemesNow()
    {
        ShowSavedThemes(Library.List());
    }

    private void ShowSavedThemes(IReadOnlyList<SavedTheme> themes)
    {
        foreach (var saved in themes)
        {
            StudioAppContext.Instance.ThemeEngine.RegisterTheme(saved.Theme);
            if (UserThemes.All(p => !string.Equals(p.Id, saved.Id, StringComparison.OrdinalIgnoreCase))) UserThemes.Add(PresetFor(saved));
        }
    }

    private ThemePresetItemViewModel PresetFor(SavedTheme saved)
    {
        var preset = PresetFor(saved.Theme, isUserTheme: true, saved.ModifiedUtc);
        preset.Palette = saved.Palette;
        preset.Layout = saved.Layout;
        return preset;
    }

    private ThemePresetItemViewModel PresetFor(ThemeDefinition theme, bool isUserTheme, DateTime? modifiedUtc = null)
    {
        string Color(string key, string fallback) => theme.Colors.TryGetValue(key, out var hex) ? hex : fallback;
        return new ThemePresetItemViewModel
        {
            Id = theme.Id,
            Name = theme.Name,
            Description = isUserTheme ? $"Saved {modifiedUtc?.ToLocalTime():d MMM yyyy, HH:mm}" : theme.Description,
            IsDark = theme.IsDark,
            IsUserTheme = isUserTheme,
            BgHex = Color("DsBgBrush", theme.IsDark ? "#0D1117" : "#FFFFFF"),
            SurfaceHex = Color("DsSurfaceBrush", theme.IsDark ? "#161B22" : "#F6F8FA"),
            AccentHex = Color("DsPrimaryBrush", "#2F81F7"),
            TextHex = Color("DsTextBrush", theme.IsDark ? "#E6EDF3" : "#24292F"),
            IsActive = string.Equals(StudioAppContext.Instance.ThemeEngine.ActiveThemeId, theme.Id, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Saves the theme now applied to the library, under the name typed in <see cref="NewThemeName"/>.</summary>
    [RelayCommand]
    public void SaveCurrentThemeAs()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var current = engine.GetTheme(engine.ActiveThemeId);
        if (current == null)
        {
            ShowNotification("There is no theme applied to save.", isError: true);
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewThemeName) ? $"{current.Name} copy" : NewThemeName.Trim();
        // A theme made in the palette studio keeps its palette (sections, locks, engine), so it can be reopened and changed.
        var palette = current.Id.StartsWith("harmonic-", StringComparison.OrdinalIgnoreCase) && _appliedPaletteSpec != null
            ? _appliedPaletteSpec.ToJson()
            : (System.Text.Json.JsonElement?)null;
        var layout = IncludeLayoutInTheme ? StudioAppContext.Instance.ThemeEngine.Layout.ToJson() : (System.Text.Json.JsonElement?)null;
        var saved = Library.Save(name, current, CurrentHarmonySettings(), palette, source: ThemeLibraryStore.IsLibraryId(current.Id) ? "duplicate" : "generated", layout: layout);
        engine.RegisterTheme(saved.Theme);
        UserThemes.Insert(0, PresetFor(saved));
        engine.ApplyTheme(saved.Id);
        ActiveThemeId = saved.Id;
        ActiveThemeName = saved.Name;
        RememberActiveTheme(saved.Id);
        NewThemeName = string.Empty;
        ShowNotification($"Saved \"{saved.Name}\" to your themes.", isError: false);
    }

    /// <summary>Renames a saved theme to the name typed in its row.</summary>
    [RelayCommand]
    public void RenameUserTheme(ThemePresetItemViewModel? item)
    {
        if (item is not { IsUserTheme: true }) return;
        var newName = string.IsNullOrWhiteSpace(item.EditName) ? item.Name : item.EditName.Trim();
        item.IsRenaming = false;
        if (newName == item.Name) return;
        if (Library.Rename(item.Id, newName) is { } renamed)
        {
            StudioAppContext.Instance.ThemeEngine.RegisterTheme(renamed.Theme);
            item.Name = renamed.Name;
            if (string.Equals(ActiveThemeId, item.Id, StringComparison.OrdinalIgnoreCase)) ActiveThemeName = renamed.Name;
            ShowNotification($"Renamed to \"{renamed.Name}\".", isError: false);
        }
    }

    [RelayCommand]
    public void BeginRenameUserTheme(ThemePresetItemViewModel? item)
    {
        if (item is not { IsUserTheme: true }) return;
        item.EditName = item.Name;
        item.IsConfirmingDelete = false;
        item.IsRenaming = true;
    }

    [RelayCommand]
    public void CancelRenameUserTheme(ThemePresetItemViewModel? item)
    {
        if (item != null) item.IsRenaming = false;
    }

    /// <summary>Makes a saved copy of any theme (built-in ones included, which can't be changed themselves).</summary>
    [RelayCommand]
    public void DuplicateTheme(ThemePresetItemViewModel? item)
    {
        if (item == null) return;
        var engine = StudioAppContext.Instance.ThemeEngine;
        if (engine.GetTheme(item.Id) is not { } theme) return;
        var copy = Library.Duplicate(theme, $"{item.Name} copy");
        engine.RegisterTheme(copy.Theme);
        UserThemes.Insert(0, PresetFor(copy));
        ShowNotification($"Saved a copy as \"{copy.Name}\".", isError: false);
    }

    /// <summary>First click on Delete: the row asks to confirm.</summary>
    [RelayCommand]
    public void AskDeleteUserTheme(ThemePresetItemViewModel? item)
    {
        if (item is not { IsUserTheme: true }) return;
        item.IsConfirmingDelete = !item.IsConfirmingDelete;
    }

    /// <summary>Deletes a saved theme (built-in ones can't be). If it was in use, the studio goes back to Dark+.</summary>
    [RelayCommand]
    public void DeleteUserTheme(ThemePresetItemViewModel? item)
    {
        if (item is not { IsUserTheme: true }) return;
        item.IsConfirmingDelete = false;
        if (!Library.Delete(item.Id)) return;
        UserThemes.Remove(item);
        if (string.Equals(StudioAppContext.Instance.ThemeEngine.ActiveThemeId, item.Id, StringComparison.OrdinalIgnoreCase))
        {
            ApplyThemePreset(BuiltInThemes.DarkPlus.Id);
        }

        ShowNotification($"Deleted \"{item.Name}\".", isError: false);
    }
}
