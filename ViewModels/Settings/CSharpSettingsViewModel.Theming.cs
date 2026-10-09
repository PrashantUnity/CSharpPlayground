using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>
/// Domain partial for Theme &amp; Colors settings, managing the interactive Color Harmony Wheel,
/// 11-stop tonal scale synthesizer, vision-deficiency simulations, and live IDE chrome preview.
/// </summary>
public partial class CSharpSettingsViewModel
{
    [ObservableProperty]
    private ColorHarmonyMode _selectedHarmonyMode = ColorHarmonyMode.Complementary;

    [ObservableProperty]
    private HarmonyModeOption? _selectedHarmonyOption;

    [ObservableProperty]
    private VisionDeficiency _selectedVisionDeficiency = VisionDeficiency.Normal;

    [ObservableProperty]
    private VisionDeficiencyOption? _selectedVisionDeficiencyOption;

    [ObservableProperty]
    private float _selectedHueDegrees = 210f;

    [ObservableProperty]
    private string _selectedHueDescriptor = "Sapphire Ocean (210°)";

    [ObservableProperty]
    private string _currentHueHex = "#38BDF8";

    [ObservableProperty]
    private bool _isThemeDarkMode = true;

    [ObservableProperty]
    private float _saturationBoost = 1.0f;

    [ObservableProperty]
    private float _accentShift = 0f;

    [ObservableProperty]
    private float _neutralTint = 10f;

    [ObservableProperty]
    private float _semanticPull = 15f;

    [ObservableProperty]
    private float _contrastTarget = 4.5f;

    [ObservableProperty]
    private string _contrastStatusSummary = "WCAG AA (4.5:1) Target";

    [ObservableProperty]
    private string _chordCanvasHex = "#0D1117";

    [ObservableProperty]
    private string _chordSurfaceHex = "#161B22";

    [ObservableProperty]
    private string _chordPrimaryHex = "#38BDF8";

    [ObservableProperty]
    private string _chordSecondaryHex = "#A855F7";

    [ObservableProperty]
    private string _primaryAccentLabel = "Primary (+180°)";

    [ObservableProperty]
    private string _secondaryAccentLabel = "Secondary (+180°)";

    public ObservableCollection<TonalSwatchItemViewModel> PrimaryTonalSwatches { get; } = [];
    public ObservableCollection<TonalSwatchItemViewModel> AccentTonalSwatches { get; } = [];
    public ObservableCollection<TonalSwatchItemViewModel> NeutralTonalSwatches { get; } = [];

    public IReadOnlyList<ColorHarmonyMode> AvailableHarmonyModes { get; } = Enum.GetValues<ColorHarmonyMode>();

    public IReadOnlyList<HarmonyModeOption> AvailableHarmonyOptions { get; } =
    [
        new() { Mode = ColorHarmonyMode.SplitComplementary, Title = "Split-Complementary", OffsetLabel = "+150°, +210°", Description = "Rich contrast with natural balance" },
        new() { Mode = ColorHarmonyMode.Complementary, Title = "Complementary", OffsetLabel = "180°", Description = "Direct opposite for high contrast" },
        new() { Mode = ColorHarmonyMode.Analogous, Title = "Analogous", OffsetLabel = "±30°", Description = "Smooth adjacent color harmony" },
        new() { Mode = ColorHarmonyMode.Triadic, Title = "Triadic", OffsetLabel = "+120°, +240°", Description = "Equilateral triangle palette" },
        new() { Mode = ColorHarmonyMode.Tetradic, Title = "Tetradic", OffsetLabel = "+90°, +180°", Description = "Dual complementary rectangle" },
        new() { Mode = ColorHarmonyMode.Monochromatic, Title = "Monochromatic", OffsetLabel = "0°", Description = "Tonal lightness variations" }
    ];

    public IReadOnlyList<VisionDeficiencyOption> AvailableVisionDeficiencyOptions { get; } =
    [
        new() { Deficiency = VisionDeficiency.Normal, Name = "Normal (Full Vision)", Prevalence = "Standard", Description = "Full trichromat vision" },
        new() { Deficiency = VisionDeficiency.Deuteranopia, Name = "Deuteranopia (Green-Blind)", Prevalence = "~6% males", Description = "Red/Green confusion" },
        new() { Deficiency = VisionDeficiency.Protanopia, Name = "Protanopia (Red-Blind)", Prevalence = "~2% males", Description = "Red darkened, L-cone absent" },
        new() { Deficiency = VisionDeficiency.Tritanopia, Name = "Tritanopia (Blue-Blind)", Prevalence = "~0.01%", Description = "Blue/Yellow confusion" },
        new() { Deficiency = VisionDeficiency.Achromatopsia, Name = "Achromatopsia (Monochrome)", Prevalence = "Rare", Description = "Complete color blindness" }
    ];

    partial void OnSelectedHarmonyOptionChanged(HarmonyModeOption? value)
    {
        if (value != null && SelectedHarmonyMode != value.Mode)
        {
            SelectedHarmonyMode = value.Mode;
        }
    }

    partial void OnSelectedHarmonyModeChanged(ColorHarmonyMode value)
    {
        if (SelectedHarmonyOption == null || SelectedHarmonyOption.Mode != value)
        {
            SelectedHarmonyOption = AvailableHarmonyOptions.FirstOrDefault(o => o.Mode == value) ?? AvailableHarmonyOptions[0];
        }
        UpdateHarmonyChords();
    }

    partial void OnSelectedVisionDeficiencyOptionChanged(VisionDeficiencyOption? value)
    {
        if (value != null && SelectedVisionDeficiency != value.Deficiency)
        {
            SelectedVisionDeficiency = value.Deficiency;
        }
    }

    partial void OnSelectedVisionDeficiencyChanged(VisionDeficiency value)
    {
        if (SelectedVisionDeficiencyOption == null || SelectedVisionDeficiencyOption.Deficiency != value)
        {
            SelectedVisionDeficiencyOption = AvailableVisionDeficiencyOptions.FirstOrDefault(o => o.Deficiency == value) ?? AvailableVisionDeficiencyOptions[0];
        }
        UpdateHarmonyChords();
    }

    partial void OnSelectedHueDegreesChanged(float value)
    {
        SelectedHueDescriptor = $"{HarmonicColorGenerator.GetHueDescriptor(value)} ({(int)value}°)";
        UpdateHarmonyChords();
    }

    partial void OnIsThemeDarkModeChanged(bool value) => UpdateHarmonyChords();
    partial void OnSaturationBoostChanged(float value) => UpdateHarmonyChords();
    partial void OnAccentShiftChanged(float value) => UpdateHarmonyChords();
    partial void OnNeutralTintChanged(float value) => UpdateHarmonyChords();
    partial void OnSemanticPullChanged(float value) => UpdateHarmonyChords();

    partial void OnContrastTargetChanged(float value)
    {
        ContrastStatusSummary = value >= 7.0f ? "WCAG AAA (7.0:1) Target" : "WCAG AA (4.5:1) Target";
        UpdateHarmonyChords();
    }

    private void InitializeThemingSettings()
    {
        RestoreHarmonyControls();
        RestorePaletteSpec();
        SelectedHarmonyOption = AvailableHarmonyOptions.FirstOrDefault(o => o.Mode == SelectedHarmonyMode) ?? AvailableHarmonyOptions[0];
        SelectedVisionDeficiencyOption = AvailableVisionDeficiencyOptions.FirstOrDefault(o => o.Deficiency == SelectedVisionDeficiency) ?? AvailableVisionDeficiencyOptions[0];
        UpdateHarmonyChords();

        CategoryFilters.Clear();
        foreach (var cat in AvailableTokenCategories)
        {
            CategoryFilters.Add(new CategoryFilterItemViewModel
            {
                Name = cat,
                IsSelected = string.Equals(cat, SelectedTokenCategory, StringComparison.OrdinalIgnoreCase)
            });
        }

        PopulateColorTokens();
        RefreshColorTokenValues();
        StudioAppContext.Instance.ThemeEngine.ThemeChanged += OnThemeChangedForInspector;
    }

    public HarmonicConfiguration GetCurrentHarmonicConfiguration() => new()
    {
        BaseHue = SelectedHueDegrees,
        Mode = SelectedHarmonyMode,
        SaturationBoost = SaturationBoost,
        AccentShift = AccentShift,
        NeutralTint = NeutralTint,
        SemanticPull = SemanticPull,
        ContrastTarget = ContrastTarget,
        Engine = SelectedColorEngine
    };

    // Dragging the hue wheel or a slider changes a value many times a second: the previews are worked out once per UI
    // turn, from the latest values, instead of once per change.
    private int _harmonyUpdateQueued;

    private void UpdateHarmonyChords()
    {
        if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime)
        {
            UpdateHarmonyChordsNow();
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _harmonyUpdateQueued, 1) == 1) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            System.Threading.Interlocked.Exchange(ref _harmonyUpdateQueued, 0);
            UpdateHarmonyChordsNow();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    // The swatch rows are kept and recoloured (their values are observable), not cleared and rebuilt on every change.
    private static void SyncSwatches(System.Collections.ObjectModel.ObservableCollection<TonalSwatchItemViewModel> target, IReadOnlyList<(int Stop, string Hex, bool IsKeyRole)> wanted)
    {
        if (target.Count == wanted.Count && target.Select(t => t.Stop).SequenceEqual(wanted.Select(w => w.Stop)))
        {
            for (int i = 0; i < wanted.Count; i++)
            {
                target[i].Hex = wanted[i].Hex;
                target[i].IsKeyRole = wanted[i].IsKeyRole;
            }

            return;
        }

        target.Clear();
        foreach (var w in wanted) target.Add(new TonalSwatchItemViewModel { Stop = w.Stop, Hex = w.Hex, IsKeyRole = w.IsKeyRole });
    }

    private void UpdateHarmonyChordsNow()
    {
        // The controls are remembered as they move (written to disk once they settle).
        RememberHarmony();
        var config = GetCurrentHarmonicConfiguration();
        float baseHue = (SelectedHueDegrees % 360f + 360f) % 360f;
        string rawHueHex = HarmonicColorGenerator.HslToHex(baseHue, 0.85f * config.SaturationBoost, 0.55f);
        CurrentHueHex = SimulateHex(rawHueHex);

        // The palette being designed (sections, locks, engine), from which every preview below is taken.
        var (palette, tokens) = RefreshPalettePreview();
        SyncScale(PrimaryTonalSwatches, PaletteSections.Primary);
        SyncScale(AccentTonalSwatches, PaletteSections.Tertiary);
        SyncScale(NeutralTonalSwatches, PaletteSections.Neutral);

        string rawCanvas = tokens["DsBgBrush"];
        string rawSurface = tokens["DsSurfaceBrush"];
        string rawPrimary = tokens["DsPrimaryBrush"];
        string rawSecondary = palette.Keys[PaletteSections.Secondary].ToHex();

        ChordCanvasHex = SimulateHex(rawCanvas);
        ChordSurfaceHex = SimulateHex(rawSurface);
        ChordPrimaryHex = SimulateHex(rawPrimary);
        ChordSecondaryHex = SimulateHex(rawSecondary);

        UpdatePerceptualContrastAndSafety(rawCanvas, rawSurface, rawPrimary);

        // Perceptual Delta E distinction check for status colors
        if (SelectedVisionDeficiency != VisionDeficiency.Normal)
        {
            var successRgb = ColorRgb.FromHex("#10B981");
            var errorRgb = ColorRgb.FromHex("#EF4444");
            bool distinct = ColorBlindnessEngine.IsDistinct(successRgb, errorRgb, SelectedVisionDeficiency, threshold: 12f);
            if (!distinct)
            {
                ContrastStatusSummary = $"Warning: Status colors blur under {SelectedVisionDeficiency} (ΔE < 12)";
            }
            else
            {
                ContrastStatusSummary = $"{SelectedVisionDeficiency} Simulated | WCAG {(ContrastTarget >= 7f ? "AAA" : "AA")}";
            }
        }
        else
        {
            ContrastStatusSummary = ContrastTarget >= 7.0f ? "WCAG AAA (7.0:1) Target" : "WCAG AA (4.5:1) Target";
        }

        PrimaryAccentLabel = SelectedHarmonyMode switch
        {
            ColorHarmonyMode.Analogous => "Analogous (+30°)",
            ColorHarmonyMode.Complementary => "Complementary (180°)",
            ColorHarmonyMode.SplitComplementary => "Split (+150°)",
            ColorHarmonyMode.Triadic => "Triadic (+120°)",
            ColorHarmonyMode.Tetradic => "Tetradic (+90°)",
            ColorHarmonyMode.Monochromatic => "Tonal (0°)",
            _ => "Primary Accent"
        };

        SecondaryAccentLabel = SelectedHarmonyMode switch
        {
            ColorHarmonyMode.Analogous => "Analogous (-30°)",
            ColorHarmonyMode.Complementary => "Direct Opposite",
            ColorHarmonyMode.SplitComplementary => "Split (+210°)",
            ColorHarmonyMode.Triadic => "Triadic (+240°)",
            ColorHarmonyMode.Tetradic => "Tetradic (+270°)",
            ColorHarmonyMode.Monochromatic => "Tonal Subtlety",
            _ => "Harmony Accent"
        };
    }

    // A role's scale, with the stop nearest its key colour marked.
    private void SyncScale(ObservableCollection<TonalSwatchItemViewModel> swatches, string role)
    {
        var palette = CurrentPalette;
        var scale = palette.Scales[role];
        var keyTone = palette.Engine.Decompose(palette.Keys[role]).Tone;
        var keyStop = TonalScaleEngine.StandardStops.MinBy(stop => Math.Abs(palette.Engine.ToneOfStop(stop) - keyTone));
        SyncSwatches(swatches, TonalScaleEngine.StandardStops.Select(stop => (stop, SimulateHex(scale[stop].ToHex()), stop == keyStop)).ToList());
    }

    private string SimulateHex(string hex)
    {
        if (SelectedVisionDeficiency == VisionDeficiency.Normal) return hex;
        var rgb = ColorRgb.FromHex(hex);
        var sim = ColorBlindnessEngine.Simulate(rgb, SelectedVisionDeficiency);
        return sim.ToHex();
    }

    // Several theme changes in a row (a theme, then its colours from a script) refresh the inspector once.
    private int _inspectorRefreshQueued;

    private void OnThemeChangedForInspector(string themeId)
    {
        if (System.Threading.Interlocked.Exchange(ref _inspectorRefreshQueued, 1) == 1) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            System.Threading.Interlocked.Exchange(ref _inspectorRefreshQueued, 0);
            RefreshColorTokenValues();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void ApplyHarmonicConfiguration()
    {
        var theme = BuildPaletteTheme();
        _appliedPaletteSpec = CurrentSpec();
        StudioAppContext.Instance.ThemeEngine.RegisterTheme(theme);
        bool success = StudioAppContext.Instance.ThemeEngine.ApplyTheme(theme.Id);
        if (success)
        {
            RememberActiveTheme(theme.Id);
            RememberHarmony();
            ActiveThemeId = theme.Id;
            ActiveThemeName = theme.Name;
            RefreshThemePresetsWithHarmonic(theme);
            RefreshColorTokenValues();
            ShowNotification($"Applied {theme.Name} with 120+ dynamic tokens!", isError: false);
        }
        else
        {
            ShowNotification("Failed to apply harmonic configuration.", isError: true);
        }
    }

    [RelayCommand]
    public async Task ApplyHarmonicConfigurationAsync()
    {
        ApplyHarmonicConfiguration();
        await Task.CompletedTask;
    }

    /// <summary>Generates a new palette (locked sections stay) and applies it.</summary>
    public void RandomizeHarmonicWheel()
    {
        GeneratePalette();
        ApplyHarmonicConfiguration();
    }

    [RelayCommand]
    public async Task RandomizeHarmonicWheelAsync()
    {
        RandomizeHarmonicWheel();
        await Task.CompletedTask;
    }
}
