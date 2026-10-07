using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>
/// Domain partial for Theme &amp; Colors settings, computing live WCAG 2.1 &amp; APCA (WCAG 3.0)
/// contrast pairings, and multi-deficiency status safety metrics.
/// </summary>
public partial class CSharpSettingsViewModel
{
    [ObservableProperty]
    private string _seedHexInput = "#38BDF8";

    public ObservableCollection<ContrastPairingItemViewModel> ContrastPairings { get; } = [];
    public ObservableCollection<VisionDeficiencyStatusItemViewModel> VisionSafetyItems { get; } = [];

    public IReadOnlyList<HarmonicSeedPreset> CuratedSeedPresets { get; } =
    [
        new("Sapphire", 199f, ColorHarmonyMode.Complementary, "#38BDF8", "Deep blue with complementary warmth"),
        new("Emerald", 160f, ColorHarmonyMode.SplitComplementary, "#10B981", "Vibrant matrix emerald with split accents"),
        new("Amethyst", 271f, ColorHarmonyMode.Analogous, "#A855F7", "Royal purple with smooth analogous gradient"),
        new("Crimson", 350f, ColorHarmonyMode.SplitComplementary, "#F43F5E", "Bold crimson sunset with crisp contrast"),
        new("Cyber Amber", 38f, ColorHarmonyMode.Triadic, "#F59E0B", "High-energy cyber amber triadic palette"),
        new("Nord Ice", 189f, ColorHarmonyMode.Analogous, "#06B6D4", "Frosty arctic cyan with cool serene tones"),
        new("Rose Quartz", 330f, ColorHarmonyMode.Complementary, "#EC4899", "Delicate rose quartz with mint complement"),
        new("Slate Minimal", 215f, ColorHarmonyMode.Monochromatic, "#64748B", "Distraction-free refined monochrome slate")
    ];

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void ApplySeedPreset(HarmonicSeedPreset preset)
    {
        if (preset == null) return;
        SelectedHarmonyMode = preset.Mode;
        SeedHexInput = preset.SeedHex;
        SelectedHueDegrees = preset.Hue;
        ApplyHarmonicConfiguration();
    }

    partial void OnSeedHexInputChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        string clean = value.Trim().TrimStart('#');
        if (clean.Length == 6 && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            var rgb = ColorRgb.FromHex("#" + clean);
            var hsl = rgb.ToHsl();
            SelectedHueDegrees = hsl.H;
            SaturationBoost = Math.Clamp(hsl.S / 0.85f, 0.6f, 1.4f);
        }
    }

    private void UpdatePerceptualContrastAndSafety(string canvasHex, string surfaceHex, string primaryHex)
    {
        string textHex = IsThemeDarkMode ? "#E6EDF3" : "#0F172A";

        (string Role, string ForeName, string ForeHex, string BackName, string BackHex)[] pairings =
        [
            ("Body Text on Canvas", "Text", textHex, "Canvas", canvasHex),
            ("Surface Text", "Text", textHex, "Surface", surfaceHex),
            ("Primary on Canvas", "Primary", primaryHex, "Canvas", canvasHex),
            ("Primary on Surface", "Primary", primaryHex, "Surface", surfaceHex),
            ("Success Status", "Success", "#10B981", "Canvas", canvasHex),
            ("Warning Status", "Warning", "#F59E0B", "Canvas", canvasHex),
            ("Error Status", "Error", "#EF4444", "Canvas", canvasHex)
        ];

        // Same rows every time (fixed roles): recolour them in place rather than rebuilding them on every slider tick.
        bool reuseContrast = ContrastPairings.Count == pairings.Length && ContrastPairings.Select(c => c.RoleName).SequenceEqual(pairings.Select(p => p.Role));
        if (!reuseContrast) ContrastPairings.Clear();
        for (int index = 0; index < pairings.Length; index++)
        {
            var p = pairings[index];
            var fRgb = ColorRgb.FromHex(p.ForeHex);
            var bRgb = ColorRgb.FromHex(p.BackHex);

            float wcag = fRgb.ContrastRatio(bRgb);
            float apca = ApcaEngine.ContrastLc(fRgb, bRgb);
            string wcagBadge = wcag >= 7.0f ? "AAA" : wcag >= 4.5f ? "AA" : "FAIL";

            if (reuseContrast)
            {
                var row = ContrastPairings[index];
                row.ForeHex = p.ForeHex;
                row.BackHex = p.BackHex;
                row.WcagRatio = (float)Math.Round(wcag, 1);
                row.WcagBadge = wcagBadge;
                row.IsWcagPassed = wcag >= 4.5f;
                row.ApcaLc = (float)Math.Round(apca, 1);
                row.ApcaRating = ApcaEngine.GetRating(apca);
                row.IsApcaPassed = ApcaEngine.IsLargeTextCompliant(apca);
                continue;
            }

            ContrastPairings.Add(new ContrastPairingItemViewModel
            {
                RoleName = p.Role,
                ForeRole = p.ForeName,
                ForeHex = p.ForeHex,
                BackRole = p.BackName,
                BackHex = p.BackHex,
                WcagRatio = (float)Math.Round(wcag, 1),
                WcagBadge = wcagBadge,
                IsWcagPassed = wcag >= 4.5f,
                ApcaLc = (float)Math.Round(apca, 1),
                ApcaRating = ApcaEngine.GetRating(apca),
                IsApcaPassed = ApcaEngine.IsLargeTextCompliant(apca)
            });
        }

        (string Name, VisionDeficiency Deficiency)[] deficiencies =
        [
            ("Normal (Trichromat)", VisionDeficiency.Normal),
            ("Deuteranopia (Green)", VisionDeficiency.Deuteranopia),
            ("Protanopia (Red)", VisionDeficiency.Protanopia),
            ("Tritanopia (Blue)", VisionDeficiency.Tritanopia),
            ("Achromatopsia (Mono)", VisionDeficiency.Achromatopsia)
        ];

        var rawSuccess = ColorRgb.FromHex("#10B981");
        var rawError = ColorRgb.FromHex("#EF4444");

        bool reuseVision = VisionSafetyItems.Count == deficiencies.Length && VisionSafetyItems.Select(v => v.Deficiency).SequenceEqual(deficiencies.Select(d => d.Deficiency));
        if (!reuseVision) VisionSafetyItems.Clear();
        for (int index = 0; index < deficiencies.Length; index++)
        {
            var def = deficiencies[index];
            var simS = ColorBlindnessEngine.Simulate(rawSuccess, def.Deficiency);
            var simE = ColorBlindnessEngine.Simulate(rawError, def.Deficiency);
            float dE = ColorBlindnessEngine.DeltaE(simS, simE);
            bool safe = dE >= 12.0f;

            string label = safe
                ? (dE >= 25f ? "Safe (Distinct)" : "Pass (Low Contrast)")
                : "Confusable (ΔE < 12)";

            if (reuseVision)
            {
                var row = VisionSafetyItems[index];
                row.SimulatedSuccessHex = simS.ToHex();
                row.SimulatedErrorHex = simE.ToHex();
                row.DeltaE = (float)Math.Round(dE, 1);
                row.IsSafe = safe;
                row.StatusLabel = label;
                continue;
            }

            VisionSafetyItems.Add(new VisionDeficiencyStatusItemViewModel
            {
                DeficiencyName = def.Name,
                Deficiency = def.Deficiency,
                SimulatedSuccessHex = simS.ToHex(),
                SimulatedErrorHex = simE.ToHex(),
                DeltaE = (float)Math.Round(dE, 1),
                IsSafe = safe,
                StatusLabel = label
            });
        }
    }
}
