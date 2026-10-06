using System;
using System.Collections.Generic;
using System.Linq;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;

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

    /// <summary>The colour model the palette is built in.</summary>
    public ColorEngineKind Engine { get; init; } = ColorEngineKind.Oklch;
}

/// <summary>
/// The harmony wheel's theme generator: a facade over <see cref="PaletteGenerator"/> and <see cref="ThemeTokenMapper"/>
/// (perceptual OKLCH or HCT palettes with every text and accent colour solved for contrast). The wheel's hue is the
/// familiar colour-wheel hue; it is turned into the engine's own hue before the palette is built.
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
        float accentHue = AccentHue(config);
        return ThemeFromPalette(PaletteGenerator.Generate(ToPaletteSpec(config, accentHue, isDark)), accentHue);
    }

    /// <summary>The theme a palette makes, named after its harmony and the wheel hue of its accent.</summary>
    public static ThemeDefinition ThemeFromPalette(GeneratedPalette palette, float accentHue)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var mode = palette.Spec.Harmony;
        var colors = ThemeTokenMapper.Map(palette);

        string modeName = mode switch
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
        string themeId = $"harmonic-{mode.ToString().ToLowerInvariant()}-{(int)accentHue}";
        string themeName = $"Harmonic {modeName} ({hueName})";

        return new ThemeDefinition
        {
            Id = themeId,
            Name = themeName,
            Description = $"{modeName} harmony built in {palette.Engine.DisplayName}, centred at {hueName} ({accentHue:0}°).",
            IsDark = palette.Spec.IsDark,
            Colors = colors
        };
    }

    /// <summary>The wheel hue of the primary accent: the base hue turned by the harmony mode and the accent shift.</summary>
    public static float AccentHue(HarmonicConfiguration config)
    {
        float hueDegrees = (config.BaseHue % 360f + 360f) % 360f;
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
        return ((rawAccent + config.AccentShift) % 360f + 360f) % 360f;
    }

    /// <summary>The palette spec the wheel describes, with the primary at <paramref name="accentHue"/> (wheel degrees).</summary>
    public static PaletteSpec ToPaletteSpec(HarmonicConfiguration config, float accentHue, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(config);
        var engine = ColorEngines.Get(config.Engine);
        return new PaletteSpec
        {
            Engine = config.Engine,
            Harmony = config.Mode,
            BaseHue = EngineHue(engine, accentHue),
            IsDark = isDark,
            ChromaBoost = Math.Clamp(config.SaturationBoost, 0.2f, 2.0f),
            NeutralTint = Math.Clamp(config.NeutralTint, 0f, 35f),
            SemanticPull = Math.Clamp(config.SemanticPull, 0f, 100f),
            ContrastTarget = Math.Clamp(config.ContrastTarget, 3f, 21f),
        };
    }

    /// <summary>The engine hue of a colour-wheel hue: where a vivid colour of that wheel hue sits in the engine.</summary>
    public static double EngineHue(IColorEngine engine, float wheelHue) =>
        engine.Decompose(ColorRgb.FromHex(HslToHex(wheelHue, 0.85f, 0.55f))).Hue;

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
        ArgumentNullException.ThrowIfNull(colors);
        var palette = PaletteGenerator.Generate(ToPaletteSpec(config, accentHue, isDark));
        foreach (var (key, value) in ThemeTokenMapper.Map(palette)) colors[key] = value;
    }

    /// <summary>
    /// Guarantees that any theme definition has complete coverage of all tokens by backfilling missing entries.
    /// </summary>
    public static void EnsureCompleteTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        // The same for every theme of a scheme: worked out once. Syntax and chart colours are left out on purpose, so a
        // theme that doesn't set them keeps each language's own highlighting and the default chart colours.
        var fallback = theme.IsDark ? DarkFallback.Value : LightFallback.Value;
        foreach (var (k, v) in fallback)
        {
            if (!theme.Colors.ContainsKey(k))
            {
                theme.Colors[k] = v;
            }
        }
    }

    private static readonly Lazy<Dictionary<string, string>> DarkFallback = new(() => Fallback(isDark: true));
    private static readonly Lazy<Dictionary<string, string>> LightFallback = new(() => Fallback(isDark: false));

    private static Dictionary<string, string> Fallback(bool isDark)
    {
        // Default base hue roughly 215 (dark blue-slate) for default studio aesthetics
        var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        PopulateAllTokens(fallback, 215f, 212f, isDark);
        foreach (var key in fallback.Keys.Where(k => k.StartsWith("Syntax", StringComparison.Ordinal) || k.StartsWith("ChartSeries", StringComparison.Ordinal)).ToList())
        {
            fallback.Remove(key);
        }

        return fallback;
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
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<int, ColorRgb>> GenerateAllTonalScales(HarmonicConfiguration config, bool isDark = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        var palette = PaletteGenerator.Generate(ToPaletteSpec(config, AccentHue(config), isDark));
        return new Dictionary<string, IReadOnlyDictionary<int, ColorRgb>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Primary"] = palette.Scales[PaletteSections.Primary],
            ["Accent"] = palette.Scales[PaletteSections.Tertiary],
            ["Secondary"] = palette.Scales[PaletteSections.Secondary],
            ["Neutral"] = palette.Scales[PaletteSections.Neutral],
            ["Success"] = palette.Scales[PaletteSections.Success],
            ["Warning"] = palette.Scales[PaletteSections.Warning],
            ["Danger"] = palette.Scales[PaletteSections.Error],
            ["Info"] = palette.Scales[PaletteSections.Info]
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
