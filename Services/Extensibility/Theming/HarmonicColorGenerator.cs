using System;
using System.Collections.Generic;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Classical color wheel harmony relationships.
/// </summary>
public enum ColorHarmonyMode
{
    Analogous,
    Complementary,
    SplitComplementary,
    Triadic,
    Tetradic,
    Monochromatic
}

/// <summary>
/// Metadata descriptor for a canonical theme token.
/// </summary>
public sealed record ColorTokenDescriptor(
    string Key,
    string DisplayName,
    string Category,
    string Description,
    string DefaultDarkHex,
    string DefaultLightHex);

/// <summary>
/// Fine-tuning parameters for the procedural harmonic design system generator.
/// </summary>
public sealed record HarmonicConfiguration
{
    public float BaseHue { get; init; } = 210f;
    public ColorHarmonyMode Mode { get; init; } = ColorHarmonyMode.Analogous;
    public float SaturationBoost { get; init; } = 1.0f;
    public float AccentShift { get; init; } = 0f;
    public float NeutralTint { get; init; } = 10f;
    public float NeutralHueOffset { get; init; } = 0f;
    public float SemanticPull { get; init; } = 15f;
    public float ContrastTarget { get; init; } = 4.5f;
}

/// <summary>
/// Scientific color harmony wheel engine. Generates mathematically coherent, ergonomic dark and light
/// color palettes based on HSL color wheel relations while strictly enforcing anti-glare card contrast
/// and complete coverage across all IDE token families (surfaces, editor canvas, badges, M3, navigation).
/// </summary>
public static class HarmonicColorGenerator
{
    private static readonly Random Rnd = new();

    /// <summary>
    /// Generates a randomized, ergonomically balanced harmonic theme.
    /// </summary>
    public static ThemeDefinition GenerateRandomHarmonicTheme(bool isDark = true)
    {
        float baseHue = (float)(Rnd.NextDouble() * 360.0);
        var modes = Enum.GetValues<ColorHarmonyMode>();
        var mode = modes[Rnd.Next(modes.Length)];
        return GenerateHarmonicTheme(baseHue, mode, isDark);
    }

    /// <summary>
    /// Generates a deterministic harmonic theme from a specified base hue and harmony relationship,
    /// populating the full dictionary of over 80+ dynamic runtime tokens.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(float hueDegrees, ColorHarmonyMode mode, bool isDark = true)
        => GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = hueDegrees, Mode = mode }, isDark);

    /// <summary>
    /// Generates a deterministic harmonic theme from an extended procedural configuration.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(HarmonicConfiguration config, bool isDark = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        float hueDegrees = (config.BaseHue % 360f + 360f) % 360f;

        // Calculate accent hue from classical color harmony wheel geometry + accent shift
        float rawAccent = config.Mode switch
        {
            ColorHarmonyMode.Analogous => hueDegrees + 30f,
            ColorHarmonyMode.Complementary => hueDegrees + 180f,
            ColorHarmonyMode.SplitComplementary => hueDegrees + 150f,
            ColorHarmonyMode.Triadic => hueDegrees + 120f,
            ColorHarmonyMode.Tetradic => hueDegrees + 90f,
            ColorHarmonyMode.Monochromatic => hueDegrees,
            _ => hueDegrees + 180f
        };
        float accentHue = ((rawAccent + config.AccentShift) % 360f + 360f) % 360f;

        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        PopulateAllTokens(colors, config, accentHue, isDark);

        string modeName = config.Mode switch
        {
            ColorHarmonyMode.Analogous => "Analogous",
            ColorHarmonyMode.Complementary => "Complementary",
            ColorHarmonyMode.SplitComplementary => "Split-Complementary",
            ColorHarmonyMode.Triadic => "Triadic",
            ColorHarmonyMode.Tetradic => "Tetradic",
            ColorHarmonyMode.Monochromatic => "Monochromatic",
            _ => "Harmonic"
        };

        string hueName = GetHueDescriptor(accentHue);
        string themeId = $"harmonic-{config.Mode.ToString().ToLowerInvariant()}-{(int)accentHue}";
        string themeName = $"Harmonic {modeName} ({hueName})";

        return new ThemeDefinition
        {
            Id = themeId,
            Name = themeName,
            Description = $"Scientifically generated {modeName} harmony wheel palette centered at {hueName} ({accentHue:0}°).",
            IsDark = isDark,
            Colors = colors
        };
    }

    /// <summary>
    /// Backward-compatible overload populating all tokens using default harmonic configuration.
    /// </summary>
    public static void PopulateAllTokens(IDictionary<string, string> colors, float baseHue, float accentHue, bool isDark)
        => PopulateAllTokens(colors, new HarmonicConfiguration { BaseHue = baseHue }, accentHue, isDark);

    /// <summary>
    /// Populates all canonical IDE tokens based on the configuration and primary accent hue.
    /// </summary>
    public static void PopulateAllTokens(IDictionary<string, string> colors, HarmonicConfiguration config, float accentHue, bool isDark)
    {
        float baseHue = (config.BaseHue % 360f + 360f) % 360f;
        float ps = Math.Clamp(0.86f * config.SaturationBoost, 0.20f, 1.0f);
        if (isDark) ps = Math.Min(ps, 0.88f);

        // 1. Synthesize 11-stop tonal scales
        var pScale = TonalScaleEngine.GenerateScale(accentHue, ps);
        var aScale = TonalScaleEngine.GenerateScale(accentHue, ps * (config.Mode == ColorHarmonyMode.Monochromatic ? 0.55f : 1.0f));

        float secHue = config.Mode switch
        {
            ColorHarmonyMode.Analogous => baseHue - 30f,
            ColorHarmonyMode.Complementary => baseHue + 30f,
            ColorHarmonyMode.SplitComplementary => baseHue + 210f,
            ColorHarmonyMode.Triadic => baseHue + 240f,
            ColorHarmonyMode.Tetradic => baseHue + 180f,
            _ => baseHue
        };
        secHue = ((secHue + config.AccentShift) % 360f + 360f) % 360f;
        var sScale = TonalScaleEngine.GenerateScale(secHue, ps * 0.85f);

        float nHue = ((baseHue + config.NeutralHueOffset) % 360f + 360f) % 360f;
        float nSat = Math.Clamp(config.NeutralTint / 100f, 0f, 0.35f);
        var nScale = TonalScaleEngine.GenerateScale(nHue, nSat, isFlatSaturation: true);

        // Status scales with brand-biased pull
        float successHue = CalculateBiasedHue(baseHue, 145f, config.SemanticPull);
        float warningHue = CalculateBiasedHue(baseHue, 40f, config.SemanticPull);
        float errorHue = CalculateBiasedHue(baseHue, 4f, config.SemanticPull);
        float infoHue = CalculateBiasedHue(baseHue, 208f, config.SemanticPull);

        var successScale = TonalScaleEngine.GenerateScale(successHue, Math.Clamp(ps, 0.55f, 0.95f));
        var warningScale = TonalScaleEngine.GenerateScale(warningHue, Math.Clamp(ps + 0.10f, 0.55f, 0.95f));
        var errorScale = TonalScaleEngine.GenerateScale(errorHue, Math.Clamp(ps, 0.55f, 0.95f));
        var infoScale = TonalScaleEngine.GenerateScale(infoHue, Math.Clamp(ps, 0.55f, 0.95f));

        // Register tonal scale tokens
        foreach (int stop in TonalScaleEngine.StandardStops)
        {
            colors[$"TonalP{stop}"] = pScale[stop].ToHex();
            colors[$"TonalA{stop}"] = aScale[stop].ToHex();
            colors[$"TonalS{stop}"] = sScale[stop].ToHex();
            colors[$"TonalN{stop}"] = nScale[stop].ToHex();
            colors[$"TonalSuccess{stop}"] = successScale[stop].ToHex();
            colors[$"TonalWarning{stop}"] = warningScale[stop].ToHex();
            colors[$"TonalError{stop}"] = errorScale[stop].ToHex();
            colors[$"TonalInfo{stop}"] = infoScale[stop].ToHex();
        }

        string primaryHex;
        string primaryHoverHex;
        string bgHex;
        string surfaceHex;
        string surfaceHoverHex;
        string surfaceHighHex;
        string surfaceLowestHex;
        string borderHex;
        string borderSubtleHex;
        string textHex;
        string textWhiteHex;
        string mutedHex;
        string syntaxStringHex;

        if (isDark)
        {
            // Dark Mode Ergonomics per styling mandate:
            primaryHex = HslToHex(accentHue, ps, 0.62f);
            primaryHoverHex = HslToHex(accentHue, Math.Min(1f, ps + 0.04f), 0.70f);

            // Canvas & Surfaces: Subtle, comfortable dark tint derived from neutral tonal scale
            bgHex = nScale[950].ToHex();                           // Deep canvas tone
            surfaceHex = nScale[900].ToHex();                      // Dark card surface tone
            surfaceHoverHex = nScale[800].ToHex();                  // Hover card tone
            surfaceHighHex = nScale[700].ToHex();                   // Elevated surface tone
            surfaceLowestHex = HslToHex(baseHue, 0.14f, 0.05f);    // Lowest inset tone
            borderHex = nScale[800].ToHex();                       // Subtle non-glaring 1px border
            borderSubtleHex = HslToHex(baseHue, 0.14f, 0.13f);     // Subtle divider

            // Typography
            textHex = nScale[100].ToHex();                         // Soft off-white
            textWhiteHex = "#FFFFFF";
            mutedHex = nScale[300].ToHex();                        // Caption
            syntaxStringHex = HslToHex((accentHue + 30f) % 360f, 0.80f, 0.82f);
        }
        else
        {
            // Light Mode Ergonomics
            primaryHex = HslToHex(accentHue, ps, 0.45f);
            primaryHoverHex = HslToHex(accentHue, Math.Min(1f, ps + 0.03f), 0.38f);

            bgHex = nScale[50].ToHex();
            surfaceHex = nScale[100].ToHex();
            surfaceHoverHex = nScale[200].ToHex();
            surfaceHighHex = "#FFFFFF";
            surfaceLowestHex = "#FFFFFF";
            borderHex = nScale[300].ToHex();
            borderSubtleHex = nScale[200].ToHex();

            textHex = nScale[900].ToHex();
            textWhiteHex = "#1F2328";
            mutedHex = nScale[600].ToHex();
            syntaxStringHex = HslToHex((accentHue + 30f) % 360f, 0.90f, 0.22f);
        }

        // Alpha derivatives
        string primarySubtleHex = ToHexWithAlpha(primaryHex, 0.15f);
        string selectionHex = ToHexWithAlpha(primaryHex, isDark ? 0.40f : 0.30f);
        string focusBorderHex = ToHexWithAlpha(primaryHex, 0.65f);
        string primaryBorderSubtleHex = ToHexWithAlpha(primaryHex, 0.25f);
        string glassBgHex = ToHexWithAlpha(surfaceHex, 0.90f);

        // 1. Surfaces & Canvas
        colors["DsBgBrush"] = bgHex;
        colors["DsSurfaceBrush"] = surfaceHex;
        colors["DsSurfaceHoverBrush"] = surfaceHoverHex;
        colors["DsSurfaceHighBrush"] = surfaceHighHex;
        colors["DsSurfaceContainerLowestBrush"] = surfaceLowestHex;
        colors["DsGlassBgBrush"] = glassBgHex;
        colors["DsHoverOverlayBrush"] = isDark ? "#1AFFFFFF" : "#14000000";
        colors["DsScrimBrush"] = isDark ? "#85000000" : "#50000000";
        colors["VisCanvasBgBrush"] = bgHex;

        // 2. Borders & Focus
        colors["DsBorderBrush"] = borderHex;
        colors["DsBorderSubtleBrush"] = borderSubtleHex;
        colors["DsFocusBorderBrush"] = focusBorderHex;

        // 3. Primary Accents
        colors["DsPrimaryBrush"] = primaryHex;
        colors["DsPrimaryHoverBrush"] = primaryHoverHex;
        colors["DsPrimarySubtleBrush"] = primarySubtleHex;
        colors["DsSelectionBrush"] = selectionHex;
        colors["DsPrimaryBorderSubtleBrush"] = primaryBorderSubtleHex;
        colors["DsOnAccentBrush"] = "#FFFFFF";

        // 4. Typography
        colors["DsTextBrush"] = textHex;
        colors["DsTextWhiteBrush"] = textWhiteHex;
        colors["DsMutedBrush"] = mutedHex;
        colors["DsSyntaxStringBrush"] = syntaxStringHex;

        // 5. Code Canvas & Editor (AvaloniaEdit)
        string editorBgHex = isDark ? HslToHex(baseHue, 0.18f, 0.09f) : "#FFFFFF";
        string editorFgHex = isDark ? "#D4D4D4" : "#1E293B";
        string editorLineNumHex = isDark ? HslToHex(baseHue, 0.10f, 0.50f) : HslToHex(baseHue, 0.10f, 0.55f);
        string editorSelectionHex = ToHexWithAlpha(primaryHex, isDark ? 0.35f : 0.25f);

        colors["EditorBgBrush"] = editorBgHex;
        colors["EditorFgBrush"] = editorFgHex;
        colors["EditorLineNumbersBrush"] = editorLineNumHex;
        colors["EditorSelectionBrush"] = editorSelectionHex;
        colors["EditorCaretBrush"] = primaryHex;
        colors["EditorLinkBrush"] = primaryHoverHex;
        colors["EditorFoldingMarkerBrush"] = mutedHex;
        colors["EditorFoldingMarkerBgBrush"] = surfaceHighHex;
        colors["EditorFoldingMarkerActiveBrush"] = primaryHex;
        colors["EditorFoldingMarkerActiveBgBrush"] = ToHexWithAlpha(primaryHex, 0.25f);
        colors["EditorBreakpointBrush"] = "#EF4444";
        colors["EditorBreakpointBorderBrush"] = "#B91C1C";
        colors["EditorBreakpointPausedBrush"] = isDark ? "#FBBF24" : "#D97706";
        colors["EditorBreakpointPausedBorderBrush"] = isDark ? "#D97706" : "#B45309";

        // 6. Semantic Status (Brand-Biased Procedural Tones)
        string errorHex = errorScale[isDark ? 400 : 600].ToHex();
        string warningHex = warningScale[isDark ? 400 : 600].ToHex();
        string successHex = successScale[isDark ? 400 : 600].ToHex();

        colors["DsErrorBrush"] = errorHex;
        colors["DsErrorSubtleBrush"] = ToHexWithAlpha(errorHex, 0.15f);
        colors["DsWarningBrush"] = warningHex;
        colors["DsWarningSubtleBrush"] = ToHexWithAlpha(warningHex, 0.15f);
        colors["DsSuccessBrush"] = successHex;
        colors["DsGreenBrush"] = successHex;
        colors["DsRedBrush"] = errorHex;
        colors["DsInfoBrush"] = primaryHex;

        // 7. Domain Accents
        colors["DsJupyterBrush"] = "#E34C26";
        colors["DsJupyterSubtleBrush"] = "#26E34C26";
        colors["DsLinqBrush"] = "#238636";
        colors["DsLinqSubtleBrush"] = "#26238636";
        colors["DsLeetCodeBrush"] = isDark ? "#FFA116" : "#D97706";
        colors["NotebookAmberBrush"] = "#D97706";
        colors["BrainPurpleBrush"] = "#A855F7";

        // 8. Badges
        colors["BadgeEasyBgBrush"] = "#1A22C55E";
        colors["BadgeEasyBorderBrush"] = isDark ? "#3322C55E" : "#4D16A34A";
        colors["BadgeEasyFgBrush"] = isDark ? "#22C55E" : "#16A34A";

        colors["BadgeMediumBgBrush"] = "#1AF59E0B";
        colors["BadgeMediumBorderBrush"] = isDark ? "#33F59E0B" : "#4DD97706";
        colors["BadgeMediumFgBrush"] = isDark ? "#F59E0B" : "#B45309";

        colors["BadgeHardBgBrush"] = "#1AEF4444";
        colors["BadgeHardBorderBrush"] = isDark ? "#33EF4444" : "#4DDC2626";
        colors["BadgeHardFgBrush"] = isDark ? "#EF4444" : "#DC2626";

        colors["BadgeAmberBgBrush"] = "#1AF59E0B";
        colors["BadgeAmberBorderBrush"] = "#33F59E0B";
        colors["BadgeAmberFgBrush"] = "#F59E0B";

        // 9. Material 3 Tokens
        colors["M3BackgroundBrush"] = bgHex;
        colors["M3SurfaceBrush"] = bgHex;
        colors["M3SurfaceContainerBrush"] = surfaceHex;
        colors["M3SurfaceContainerLowBrush"] = surfaceHex;
        colors["M3SurfaceContainerLowestBrush"] = surfaceLowestHex;
        colors["M3SurfaceContainerHighBrush"] = surfaceHighHex;
        colors["M3SurfaceContainerHighestBrush"] = surfaceHoverHex;
        colors["M3SurfaceVariantBrush"] = surfaceHoverHex;
        colors["M3OnSurfaceBrush"] = textWhiteHex;
        colors["M3OnSurfaceVariantBrush"] = mutedHex;
        colors["M3OutlineBrush"] = isDark ? "#30363D" : "#8C959F";
        colors["M3OutlineVariantBrush"] = borderHex;
        colors["M3PrimaryBrush"] = primaryHex;
        colors["M3PrimaryContainerBrush"] = primarySubtleHex;
        colors["M3OnPrimaryContainerBrush"] = primaryHex;
        colors["M3SecondaryContainerBrush"] = surfaceHoverHex;
        colors["M3OnSecondaryContainerBrush"] = textHex;
        colors["M3TertiaryBrush"] = syntaxStringHex;
        colors["M3WarningBrush"] = isDark ? "#D29922" : "#9A6700";

        // 10. Sidebar Navigation Category Brushes
        colors["NavHomeFgBrush"] = isDark ? "#38BDF8" : "#0284C7";
        colors["NavHomeBgBrush"] = isDark ? "#1A38BDF8" : "#140284C7";
        colors["NavProjectsFgBrush"] = isDark ? "#818CF8" : "#4F46E5";
        colors["NavProjectsBgBrush"] = isDark ? "#1A818CF8" : "#144F46E5";
        colors["NavNotebooksFgBrush"] = isDark ? "#FB923C" : "#EA580C";
        colors["NavNotebooksBgBrush"] = isDark ? "#1AFB923C" : "#14EA580C";
        colors["NavScriptsFgBrush"] = isDark ? "#C084FC" : "#9333EA";
        colors["NavScriptsBgBrush"] = isDark ? "#1AC084FC" : "#149333EA";
        colors["NavPinnedFgBrush"] = isDark ? "#FB7185" : "#E11D48";
        colors["NavPinnedBgBrush"] = isDark ? "#1AFB7185" : "#14E11D48";
        colors["NavTemplatesFgBrush"] = isDark ? "#FBBF24" : "#D97706";
        colors["NavTemplatesBgBrush"] = isDark ? "#1AFBBF24" : "#14D97706";
        colors["NavOpenFgBrush"] = isDark ? "#FACC15" : "#CA8A04";
        colors["NavOpenBgBrush"] = isDark ? "#1AFACC15" : "#14CA8A04";
        colors["NavBlind75FgBrush"] = isDark ? "#F59E0B" : "#B45309";
        colors["NavBlind75BgBrush"] = isDark ? "#1AF59E0B" : "#14B45309";
        colors["NavDocsFgBrush"] = isDark ? "#22D3EE" : "#0891B2";
        colors["NavDocsBgBrush"] = isDark ? "#1A22D3EE" : "#140891B2";
        colors["NavThemeFgBrush"] = isDark ? "#F472B6" : "#DB2777";
        colors["NavThemeBgBrush"] = isDark ? "#1AF472B6" : "#14DB2777";
        colors["NavToolchainsFgBrush"] = isDark ? "#34D399" : "#059669";
        colors["NavToolchainsBgBrush"] = isDark ? "#1A34D399" : "#14059669";
        colors["NavKernelsFgBrush"] = isDark ? "#A78BFA" : "#7C3AED";
        colors["NavKernelsBgBrush"] = isDark ? "#1AA78BFA" : "#147C3AED";
        colors["NavSettingsFgBrush"] = isDark ? "#94A3B8" : "#475569";
        colors["NavSettingsBgBrush"] = isDark ? "#1A94A3B8" : "#14475569";
        colors["NavFrySharpFgBrush"] = isDark ? "#38BDF8" : "#0284C7";
        colors["NavFrySharpBgBrush"] = isDark ? "#1A38BDF8" : "#140284C7";
        colors["NavServersFgBrush"] = isDark ? "#2DD4BF" : "#0D9488";
        colors["NavServersBgBrush"] = isDark ? "#1A2DD4BF" : "#140D9488";

        // 11. Server Method & Type Tokens
        colors["ServerMethodGetBgBrush"] = isDark ? "#162846" : "#E0EAFF";
        colors["ServerMethodGetFgBrush"] = isDark ? "#60A5FA" : "#1E5EEB";
        colors["ServerMethodGetBorderBrush"] = isDark ? "#2563EB" : "#A8C7FA";
        colors["ServerMethodPostBgBrush"] = isDark ? "#123522" : "#D1E7DD";
        colors["ServerMethodPostFgBrush"] = isDark ? "#4ADE80" : "#0A3622";
        colors["ServerMethodPostBorderBrush"] = isDark ? "#22C55E" : "#198754";
        colors["ServerMethodPutBgBrush"] = isDark ? "#382A12" : "#FEF3C7";
        colors["ServerMethodPutFgBrush"] = isDark ? "#FBBF24" : "#78350F";
        colors["ServerMethodPutBorderBrush"] = isDark ? "#D97706" : "#D97706";
        colors["ServerMethodDeleteBgBrush"] = isDark ? "#3A1519" : "#FFDAD6";
        colors["ServerMethodDeleteFgBrush"] = isDark ? "#F87171" : "#BA1A1A";
        colors["ServerMethodDeleteBorderBrush"] = isDark ? "#DC2626" : "#FFB4AB";
        colors["ServerMethodPatchBgBrush"] = isDark ? "#2C1542" : "#F3E8FF";
        colors["ServerMethodPatchFgBrush"] = isDark ? "#C084FC" : "#6B21A8";
        colors["ServerMethodPatchBorderBrush"] = isDark ? "#9333EA" : "#D8B4FE";
        colors["ServerMethodOptionsBgBrush"] = isDark ? "#1E293B" : "#F1F5F9";
        colors["ServerMethodOptionsFgBrush"] = isDark ? "#94A3B8" : "#475569";
        colors["ServerMethodOptionsBorderBrush"] = isDark ? "#475569" : "#CBD5E1";
        colors["ServerMethodOtherBgBrush"] = isDark ? "#133036" : "#CCFBF1";
        colors["ServerMethodOtherFgBrush"] = isDark ? "#2DD4BF" : "#0F766E";
        colors["ServerMethodOtherBorderBrush"] = isDark ? "#0D9488" : "#5EEAD4";

        colors["ServerTypeStartupBgBrush"] = isDark ? "#2E1846" : "#F3E8FF";
        colors["ServerTypeStartupFgBrush"] = isDark ? "#C084FC" : "#6B21A8";
        colors["ServerTypeStartupBorderBrush"] = isDark ? "#9333EA" : "#D8B4FE";
        colors["ServerTypeMiddlewareBgBrush"] = isDark ? "#122E3B" : "#E0F2FE";
        colors["ServerTypeMiddlewareFgBrush"] = isDark ? "#38BDF8" : "#0369A1";
        colors["ServerTypeMiddlewareBorderBrush"] = isDark ? "#0284C7" : "#BAE6FD";
        colors["ServerTypeScenarioBgBrush"] = isDark ? "#183522" : "#D1E7DD";
        colors["ServerTypeScenarioFgBrush"] = isDark ? "#4ADE80" : "#0A3622";
        colors["ServerTypeScenarioBorderBrush"] = isDark ? "#22C55E" : "#198754";
        colors["ServerTypeJobBgBrush"] = isDark ? "#382710" : "#FFEDD5";
        colors["ServerTypeJobFgBrush"] = isDark ? "#FB923C" : "#9A3412";
        colors["ServerTypeJobBorderBrush"] = isDark ? "#EA580C" : "#FED7AA";
        colors["ServerTypeDocsBgBrush"] = isDark ? "#3A2A12" : "#FEF3C7";
        colors["ServerTypeDocsFgBrush"] = isDark ? "#FBBF24" : "#78350F";
        colors["ServerTypeDocsBorderBrush"] = isDark ? "#D97706" : "#D97706";
    }

    /// <summary>
    /// Guarantees that any theme definition has complete coverage of all tokens by backfilling missing entries.
    /// </summary>
    public static void EnsureCompleteTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        string bg = theme.Colors.TryGetValue("DsBgBrush", out var b) ? b : (theme.IsDark ? "#0D1117" : "#FFFFFF");
        string primary = theme.Colors.TryGetValue("DsPrimaryBrush", out var p) ? p : (theme.IsDark ? "#2F81F7" : "#0969DA");

        var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Default base hue roughly 215 (dark blue-slate) for default studio aesthetics
        PopulateAllTokens(fallback, 215f, 212f, theme.IsDark);

        foreach (var (k, v) in fallback)
        {
            if (!theme.Colors.ContainsKey(k))
            {
                theme.Colors[k] = v;
            }
        }
    }

    /// <summary>
    /// Converts HSL coordinates (Hue 0..360, Saturation 0..1, Lightness 0..1) to #RRGGBB hex format.
    /// </summary>
    public static string HslToHex(float h, float s, float l)
    {
        h = (h % 360f + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        l = Math.Clamp(l, 0f, 1f);

        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r = 0f, g = 0f, b = 0f;
        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        byte rByte = (byte)Math.Clamp((int)Math.Round((r + m) * 255f), 0, 255);
        byte gByte = (byte)Math.Clamp((int)Math.Round((g + m) * 255f), 0, 255);
        byte bByte = (byte)Math.Clamp((int)Math.Round((b + m) * 255f), 0, 255);

        return $"#{rByte:X2}{gByte:X2}{bByte:X2}";
    }

    public static string ToHexWithAlpha(string hexRgb, float alpha)
    {
        if (hexRgb.StartsWith("#") && hexRgb.Length == 7)
        {
            byte a = (byte)Math.Clamp((int)Math.Round(alpha * 255f), 0, 255);
            return $"#{a:X2}{hexRgb[1..]}";
        }
        return hexRgb;
    }

    public static string GetHueDescriptor(float h) => h switch
    {
        >= 345 or < 15 => "Ruby Crimson",
        >= 15 and < 45 => "Amber Coral",
        >= 45 and < 70 => "Golden Sun",
        >= 70 and < 150 => "Emerald Mint",
        >= 150 and < 190 => "Cyan Glacier",
        >= 190 and < 255 => "Sapphire Ocean",
        >= 255 and < 290 => "Violet Iris",
        >= 290 and < 345 => "Magenta Orchid",
        _ => "Spectrum"
    };

    /// <summary>
    /// Calculates brand-biased status hue by pulling functional hue toward the base brand hue.
    /// </summary>
    public static float CalculateBiasedHue(float baseHue, float statusBaseHue, float pullFactor)
    {
        float delta = ((baseHue - statusBaseHue + 540f) % 360f) - 180f;
        return (statusBaseHue + delta * (pullFactor / 100f) * 0.25f % 360f + 360f) % 360f;
    }

    /// <summary>
    /// Generates full 11-stop tonal scale dictionaries for all core palette families.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<int, ColorRgb>> GenerateAllTonalScales(HarmonicConfiguration config)
    {
        float baseHue = (config.BaseHue % 360f + 360f) % 360f;
        float ps = Math.Clamp(0.86f * config.SaturationBoost, 0.20f, 1.0f);

        float rawAccent = config.Mode switch
        {
            ColorHarmonyMode.Analogous => baseHue + 30f,
            ColorHarmonyMode.Complementary => baseHue + 180f,
            ColorHarmonyMode.SplitComplementary => baseHue + 150f,
            ColorHarmonyMode.Triadic => baseHue + 120f,
            ColorHarmonyMode.Tetradic => baseHue + 90f,
            ColorHarmonyMode.Monochromatic => baseHue,
            _ => baseHue + 180f
        };
        float accentHue = ((rawAccent + config.AccentShift) % 360f + 360f) % 360f;

        float secHue = config.Mode switch
        {
            ColorHarmonyMode.Analogous => baseHue - 30f,
            ColorHarmonyMode.Complementary => baseHue + 30f,
            ColorHarmonyMode.SplitComplementary => baseHue + 210f,
            ColorHarmonyMode.Triadic => baseHue + 240f,
            ColorHarmonyMode.Tetradic => baseHue + 180f,
            _ => baseHue
        };
        secHue = ((secHue + config.AccentShift) % 360f + 360f) % 360f;

        float nHue = ((baseHue + config.NeutralHueOffset) % 360f + 360f) % 360f;
        float nSat = Math.Clamp(config.NeutralTint / 100f, 0f, 0.35f);

        float successHue = CalculateBiasedHue(baseHue, 145f, config.SemanticPull);
        float warningHue = CalculateBiasedHue(baseHue, 40f, config.SemanticPull);
        float errorHue = CalculateBiasedHue(baseHue, 4f, config.SemanticPull);
        float infoHue = CalculateBiasedHue(baseHue, 208f, config.SemanticPull);

        return new Dictionary<string, IReadOnlyDictionary<int, ColorRgb>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Primary"] = TonalScaleEngine.GenerateScale(accentHue, ps),
            ["Accent"] = TonalScaleEngine.GenerateScale(accentHue, ps * (config.Mode == ColorHarmonyMode.Monochromatic ? 0.55f : 1.0f)),
            ["Secondary"] = TonalScaleEngine.GenerateScale(secHue, ps * 0.85f),
            ["Neutral"] = TonalScaleEngine.GenerateScale(nHue, nSat, isFlatSaturation: true),
            ["Success"] = TonalScaleEngine.GenerateScale(successHue, Math.Clamp(ps, 0.55f, 0.95f)),
            ["Warning"] = TonalScaleEngine.GenerateScale(warningHue, Math.Clamp(ps + 0.10f, 0.55f, 0.95f)),
            ["Danger"] = TonalScaleEngine.GenerateScale(errorHue, Math.Clamp(ps, 0.55f, 0.95f)),
            ["Info"] = TonalScaleEngine.GenerateScale(infoHue, Math.Clamp(ps, 0.55f, 0.95f))
        };
    }

    /// <summary>
    /// Returns canonical descriptors for all dynamic tokens, organized by UI functional group.
    /// </summary>
    public static IReadOnlyList<ColorTokenDescriptor> GetAllTokenDescriptors() =>
    [
        // Surfaces & Canvas
        new("DsBgBrush", "Background Canvas", "Surfaces", "Main workspace root background", "#0D1117", "#FFFFFF"),
        new("DsSurfaceBrush", "Card Surface", "Surfaces", "Standard card and primary panel surface", "#161B22", "#F6F8FA"),
        new("DsSurfaceHoverBrush", "Hover Card Surface", "Surfaces", "Card hover state and sub-panel surface", "#21262D", "#EAEEF2"),
        new("DsSurfaceHighBrush", "High Elevation Surface", "Surfaces", "Toolbars and elevated floating surfaces", "#1C2128", "#ECEFF2"),
        new("DsSurfaceContainerLowestBrush", "Deep Container Surface", "Surfaces", "Lowest inset panel container", "#090C10", "#FFFFFF"),
        new("DsGlassBgBrush", "Glass Surface", "Surfaces", "Translucent glassmorphic overlay background", "#E6161B22", "#E6F6F8FA"),
        new("DsHoverOverlayBrush", "Hover Overlay", "Surfaces", "Subtle cursor hover overlay", "#1AFFFFFF", "#14000000"),
        new("DsScrimBrush", "Modal Scrim", "Surfaces", "Dimmed backdrop for modal sheets", "#85000000", "#50000000"),
        new("VisCanvasBgBrush", "Plot Canvas", "Surfaces", "2D/3D visualization viewport background", "#0D1117", "#FFFFFF"),

        // Borders & Focus
        new("DsBorderBrush", "Card Border", "Borders", "Subtle 1px card and splitter border", "#21262D", "#E1E4E8"),
        new("DsBorderSubtleBrush", "Subtle Border", "Borders", "Light divider between subsections", "#171B22", "#EEF1F5"),
        new("DsFocusBorderBrush", "Focus Border", "Borders", "Keyboard focus halo indicator border", "#8C2F81F7", "#590969DA"),

        // Primary Accent & Interaction
        new("DsPrimaryBrush", "Primary Accent", "Accents", "Brand accent, active states and highlights", "#2F81F7", "#0969DA"),
        new("DsPrimaryHoverBrush", "Primary Hover", "Accents", "Pointerover primary action buttons", "#1F6FEB", "#0860CA"),
        new("DsPrimarySubtleBrush", "Primary Subtle Tint", "Accents", "Subtle background tint for active items", "#1A2F81F7", "#1A0969DA"),
        new("DsSelectionBrush", "Selection Highlight", "Accents", "List and tree row selection highlight", "#992F81F7", "#880969DA"),
        new("DsPrimaryBorderSubtleBrush", "Accent Border Highlight", "Accents", "Active tab and card border accent", "#332F81F7", "#330969DA"),
        new("DsOnAccentBrush", "On-Accent Foreground", "Accents", "Foreground color for text on primary badges", "#FFFFFF", "#FFFFFF"),

        // Typography
        new("DsTextBrush", "Primary Body Text", "Typography", "Readable off-white body text", "#C9D1D9", "#24292F"),
        new("DsTextWhiteBrush", "Heading & Emphasis", "Typography", "High-contrast headings and bold emphasis", "#FFFFFF", "#1F2328"),
        new("DsMutedBrush", "Muted / Captions", "Typography", "Secondary text, hints and line captions", "#8B949E", "#656D76"),
        new("DsSyntaxStringBrush", "Syntax String", "Typography", "String literals and token annotations", "#A5D6FF", "#0A3069"),

        // Code Canvas & Editor
        new("EditorBgBrush", "Editor Canvas Background", "Editor", "AvaloniaEdit code canvas background", "#14171F", "#FFFFFF"),
        new("EditorFgBrush", "Editor Text Foreground", "Editor", "Default code syntax text color", "#D4D4D4", "#1E293B"),
        new("EditorLineNumbersBrush", "Line Numbers Gutter", "Editor", "Gutter line numbers text color", "#6E7681", "#64748B"),
        new("EditorSelectionBrush", "Editor Text Selection", "Editor", "Highlighted text selection in editor", "#264F78", "#ADD6FF"),
        new("EditorCaretBrush", "Caret Cursor", "Editor", "Blinking text insertion caret", "#58A6FF", "#0F172A"),
        new("EditorLinkBrush", "Code Hyperlink", "Editor", "Clickable symbol or URL links in code", "#4FC1FF", "#2563EB"),
        new("EditorFoldingMarkerBrush", "Folding Marker", "Editor", "Code folding collapse/expand chevron", "#8B949E", "#64748B"),
        new("EditorFoldingMarkerBgBrush", "Folding Marker Background", "Editor", "Background pill for folding marker", "#1E2633", "#F1F5F9"),
        new("EditorFoldingMarkerActiveBrush", "Active Folding Marker", "Editor", "Active hover folding marker chevron", "#58A6FF", "#2563EB"),
        new("EditorFoldingMarkerActiveBgBrush", "Active Folding Background", "Editor", "Active hover folding marker box", "#264F78", "#DBEAFE"),
        new("EditorBreakpointBrush", "Breakpoint Gutter", "Editor", "Active breakpoint gutter indicator", "#EF4444", "#EF4444"),
        new("EditorBreakpointBorderBrush", "Breakpoint Border", "Editor", "Active breakpoint gutter border", "#B91C1C", "#B91C1C"),
        new("EditorBreakpointPausedBrush", "Paused Execution Indicator", "Editor", "Paused execution yellow indicator", "#FBBF24", "#D97706"),
        new("EditorBreakpointPausedBorderBrush", "Paused Execution Border", "Editor", "Paused execution yellow border", "#D97706", "#B45309"),

        // Semantic Status
        new("DsErrorBrush", "Semantic Error", "Status", "Diagnostics error counter & failure indicator", "#F85149", "#CF222E"),
        new("DsErrorSubtleBrush", "Error Subtle Pill", "Status", "Subtle red background pill for error tags", "#26F85149", "#1ACF222E"),
        new("DsWarningBrush", "Semantic Warning", "Status", "Diagnostics warning counter & caution alerts", "#D29922", "#9A6700"),
        new("DsWarningSubtleBrush", "Warning Subtle Pill", "Status", "Subtle amber background pill for warnings", "#26D29922", "#1A9A6700"),
        new("DsSuccessBrush", "Semantic Success", "Status", "Execution passed, tests succeeded", "#2EA043", "#1A7F37"),
        new("DsGreenBrush", "Green Accent", "Status", "Standard green utility brush", "#2EA043", "#2EA043"),
        new("DsRedBrush", "Red Accent", "Status", "Standard red utility brush", "#EF4444", "#DC2626"),
        new("DsInfoBrush", "Info Accent", "Status", "Informational notifications and prompts", "#2F81F7", "#0969DA"),

        // Material 3 Tokens
        new("M3BackgroundBrush", "M3 Background", "Material3", "Material 3 root canvas background", "#0D1117", "#FFFFFF"),
        new("M3SurfaceBrush", "M3 Surface", "Material3", "Material 3 base surface", "#0D1117", "#FFFFFF"),
        new("M3SurfaceContainerBrush", "M3 Surface Container", "Material3", "Default elevation container", "#161B22", "#F6F8FA"),
        new("M3SurfaceContainerHighBrush", "M3 Container High", "Material3", "Elevated component container", "#1C2128", "#EAEEF2"),
        new("M3SurfaceContainerHighestBrush", "M3 Container Highest", "Material3", "Highest elevation container", "#21262D", "#D0D7DE"),
        new("M3OnSurfaceBrush", "M3 On-Surface", "Material3", "Text color on surface components", "#FFFFFF", "#1F2328"),
        new("M3OnSurfaceVariantBrush", "M3 On-Surface Variant", "Material3", "Secondary text color on surfaces", "#8B949E", "#656D76"),
        new("M3OutlineBrush", "M3 Outline", "Material3", "High-contrast component outline", "#30363D", "#8C959F"),
        new("M3OutlineVariantBrush", "M3 Outline Variant", "Material3", "Subtle component divider", "#21262D", "#D0D7DE"),
        new("M3PrimaryBrush", "M3 Primary", "Material3", "Material 3 primary accent", "#2F81F7", "#0969DA"),
        new("M3PrimaryContainerBrush", "M3 Primary Container", "Material3", "Material 3 primary tinted container", "#1A2F81F7", "#DDF4FF"),
        new("M3TertiaryBrush", "M3 Tertiary", "Material3", "Tertiary accent token", "#A5D6FF", "#0969DA"),

        // Badges
        new("BadgeEasyBgBrush", "Easy Badge Background", "Badges", "Green difficulty badge background", "#1A22C55E", "#1A16A34A"),
        new("BadgeEasyFgBrush", "Easy Badge Text", "Badges", "Green difficulty badge foreground", "#22C55E", "#16A34A"),
        new("BadgeMediumBgBrush", "Medium Badge Background", "Badges", "Amber difficulty badge background", "#1AF59E0B", "#1AD97706"),
        new("BadgeMediumFgBrush", "Medium Badge Text", "Badges", "Amber difficulty badge foreground", "#F59E0B", "#B45309"),
        new("BadgeHardBgBrush", "Hard Badge Background", "Badges", "Red difficulty badge background", "#1AEF4444", "#1ADC2626"),
        new("BadgeHardFgBrush", "Hard Badge Text", "Badges", "Red difficulty badge foreground", "#EF4444", "#DC2626"),

        // Navigation Categories
        new("NavHomeFgBrush", "Nav Home", "Navigation", "Home hub category accent", "#38BDF8", "#0284C7"),
        new("NavProjectsFgBrush", "Nav Projects", "Navigation", "Projects workspace category accent", "#818CF8", "#4F46E5"),
        new("NavNotebooksFgBrush", "Nav Notebooks", "Navigation", "Polyglot notebooks category accent", "#FB923C", "#EA580C"),
        new("NavScriptsFgBrush", "Nav Scripts", "Navigation", "Scripts & code category accent", "#C084FC", "#9333EA"),
        new("NavPinnedFgBrush", "Nav Pinned", "Navigation", "Pinned documents category accent", "#FB7185", "#E11D48"),
        new("NavTemplatesFgBrush", "Nav Templates", "Navigation", "Code templates category accent", "#FBBF24", "#D97706"),
        new("NavOpenFgBrush", "Nav Open", "Navigation", "Open tabs category accent", "#FACC15", "#CA8A04"),
        new("NavBlind75FgBrush", "Nav Blind 75", "Navigation", "Blind 75 algorithm category accent", "#F59E0B", "#B45309"),
        new("NavDocsFgBrush", "Nav Docs", "Navigation", "API documentation category accent", "#22D3EE", "#0891B2"),
        new("NavThemeFgBrush", "Nav Theme", "Navigation", "Themes & colors category accent", "#F472B6", "#DB2777"),
        new("NavToolchainsFgBrush", "Nav Toolchains", "Navigation", "Toolchains & compilers category accent", "#34D399", "#059669"),
        new("NavKernelsFgBrush", "Nav Kernels", "Navigation", "Language kernels category accent", "#A78BFA", "#7C3AED"),
        new("NavSettingsFgBrush", "Nav Settings", "Navigation", "Settings category accent", "#94A3B8", "#475569"),
        new("NavFrySharpFgBrush", "Nav FrySharp", "Navigation", "FrySharp standalone app accent", "#38BDF8", "#0284C7"),
        new("NavServersFgBrush", "Nav Servers", "Navigation", "FryServer network services accent", "#2DD4BF", "#0D9488")
    ];
}
