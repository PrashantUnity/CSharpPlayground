using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;

/// <summary>
/// Turns a <see cref="GeneratedPalette"/> into the studio's colour tokens: surfaces from the neutral scale, text and
/// every accent solved for contrast on the surface it sits on (WCAG and APCA), plus <c>Syntax{Role}Brush</c> and
/// <c>ChartSeries{n}Brush</c> for the editors and charts.
/// </summary>
public static class ThemeTokenMapper
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    // Navigation categories and server badges keep their meaning (their hue), and take the palette's lightness, chroma and contrast.
    private static readonly (string Key, string Reference)[] NavigationHues =
    [
        ("Home", "#38BDF8"), ("Projects", "#818CF8"), ("Notebooks", "#FB923C"), ("Scripts", "#C084FC"), ("Pinned", "#FB7185"),
        ("Templates", "#FBBF24"), ("Open", "#FACC15"), ("Blind75", "#F59E0B"), ("Docs", "#22D3EE"), ("Theme", "#F472B6"),
        ("Toolchains", "#34D399"), ("Kernels", "#A78BFA"), ("Settings", "#94A3B8"), ("FrySharp", "#38BDF8"), ("Servers", "#2DD4BF"),
    ];

    private static readonly (string Key, string Reference)[] ServerHues =
    [
        ("ServerMethodGet", "#3B82F6"), ("ServerMethodPost", "#22C55E"), ("ServerMethodPut", "#F59E0B"), ("ServerMethodDelete", "#EF4444"),
        ("ServerMethodPatch", "#A855F7"), ("ServerMethodOptions", "#64748B"), ("ServerMethodOther", "#14B8A6"),
        ("ServerTypeStartup", "#A855F7"), ("ServerTypeMiddleware", "#0EA5E9"), ("ServerTypeScenario", "#22C55E"),
        ("ServerTypeJob", "#F97316"), ("ServerTypeDocs", "#F59E0B"),
    ];

    public static Dictionary<string, string> Map(GeneratedPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var spec = palette.Spec;
        var engine = palette.Engine;
        var dark = spec.IsDark;
        var target = (float)Math.Clamp(spec.ContrastTarget, 3, 21);
        var aaa = target >= 7;
        var textLc = aaa ? 90f : 75f;
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var neutral = palette.Parts[PaletteSections.Neutral];
        var neutralVariant = palette.Parts[PaletteSections.NeutralVariant];
        ColorRgb Neutral(double lstar) => engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(lstar));

        // Tonal scales.
        foreach (var (prefix, role) in new[]
                 {
                     ("TonalP", PaletteSections.Primary), ("TonalA", PaletteSections.Tertiary), ("TonalS", PaletteSections.Secondary),
                     ("TonalN", PaletteSections.Neutral), ("TonalSuccess", PaletteSections.Success), ("TonalWarning", PaletteSections.Warning),
                     ("TonalError", PaletteSections.Error), ("TonalInfo", PaletteSections.Info),
                 })
        {
            var scale = palette.Scales[role];
            foreach (var stop in Stops) colors[$"{prefix}{stop}"] = scale[stop].ToHex();
        }

        // Surfaces (CIE L*; Material 3 surface tones adapted to an IDE).
        var bg = palette.Background;
        var surface = palette.Surface;
        var surfaceHover = Neutral(dark ? 16 : 93);
        var surfaceHigh = dark ? Neutral(14) : ColorRgb.White;
        var surfaceLowest = dark ? Neutral(3) : ColorRgb.White;
        var border = Neutral(dark ? 22 : 86);
        var borderSubtle = Neutral(dark ? 14 : 92);

        // Text, solved on the surface (the harder of window and card in both schemes).
        var text = Solve(neutral, dark ? 90 : 15, surface, Math.Max(target, 7f), textLc);
        var muted = Solve(neutralVariant, dark ? 60 : 45, surface, target, 60f);
        var heading = dark ? ColorRgb.White : ColorRgb.FromHex("#1F2328");

        // Accents.
        var primary = Accent(PaletteSections.Primary, surface);
        var primaryParts = palette.Parts[PaletteSections.Primary];
        var primaryHover = engine.Compose(primaryParts.Hue, primaryParts.Chroma, engine.Decompose(primary).Tone + (dark ? 8 : -8));
        var onPrimaryLight = ColorRgb.White;
        var onPrimaryDark = engine.Compose(primaryParts.Hue, Math.Min(primaryParts.Chroma, engine.VividChroma * 0.3), engine.ToneFromLstar(12));
        var onAccent = onPrimaryLight.ContrastRatio(primary) >= onPrimaryDark.ContrastRatio(primary) ? onPrimaryLight : onPrimaryDark;
        var tertiary = Accent(PaletteSections.Tertiary, surface);

        var error = Accent(PaletteSections.Error, surface);
        var warning = Accent(PaletteSections.Warning, surface);
        var success = Accent(PaletteSections.Success, surface);
        var info = Accent(PaletteSections.Info, surface);

        Set("DsBgBrush", bg);
        Set("DsSurfaceBrush", surface);
        Set("DsSurfaceHoverBrush", surfaceHover);
        Set("DsSurfaceHighBrush", surfaceHigh);
        Set("DsSurfaceContainerLowestBrush", surfaceLowest);
        colors["DsGlassBgBrush"] = surface.ToHexWithAlpha(0.90f);
        colors["DsHoverOverlayBrush"] = dark ? "#1AFFFFFF" : "#14000000";
        colors["DsScrimBrush"] = dark ? "#85000000" : "#50000000";
        Set("VisCanvasBgBrush", bg);

        Set("DsBorderBrush", border);
        Set("DsBorderSubtleBrush", borderSubtle);
        colors["DsFocusBorderBrush"] = primary.ToHexWithAlpha(0.65f);

        Set("DsPrimaryBrush", primary);
        Set("DsPrimaryHoverBrush", primaryHover);
        colors["DsPrimarySubtleBrush"] = primary.ToHexWithAlpha(0.15f);
        colors["DsSelectionBrush"] = primary.ToHexWithAlpha(dark ? 0.40f : 0.30f);
        colors["DsPrimaryBorderSubtleBrush"] = primary.ToHexWithAlpha(0.25f);
        Set("DsOnAccentBrush", onAccent);

        Set("DsTextBrush", text);
        Set("DsTextWhiteBrush", heading);
        Set("DsMutedBrush", muted);

        // Editor.
        var editorBg = palette.EditorBackground;
        var editorFg = Solve(neutral, dark ? 86 : 18, editorBg, Math.Max(target, 7f), textLc);
        Set("EditorBgBrush", editorBg);
        Set("EditorFgBrush", editorFg);
        Set("EditorLineNumbersBrush", Solve(neutralVariant, dark ? 48 : 58, editorBg, 3f, 30f));
        colors["EditorSelectionBrush"] = primary.ToHexWithAlpha(dark ? 0.35f : 0.25f);
        Set("EditorCaretBrush", primary);
        Set("EditorLinkBrush", primaryHover);
        Set("EditorFoldingMarkerBrush", muted);
        Set("EditorFoldingMarkerBgBrush", surfaceHigh);
        Set("EditorFoldingMarkerActiveBrush", primary);
        colors["EditorFoldingMarkerActiveBgBrush"] = primary.ToHexWithAlpha(0.25f);
        Set("EditorBreakpointBrush", error);
        Set("EditorBreakpointBorderBrush", Shift(error, dark ? -15 : -12));
        Set("EditorBreakpointPausedBrush", warning);
        Set("EditorBreakpointPausedBorderBrush", Shift(warning, -15));

        // Syntax and charts.
        foreach (var role in PaletteSections.SyntaxRoles) Set($"Syntax{role}Brush", palette.Keys[PaletteSections.SyntaxSection(role)]);
        Set("DsSyntaxStringBrush", palette.Keys[PaletteSections.SyntaxSection("String")]);
        for (var series = 1; series <= PaletteSections.ChartSeriesCount; series++) Set($"ChartSeries{series}Brush", palette.Keys[PaletteSections.Chart(series)]);

        // Status.
        Set("DsErrorBrush", error);
        colors["DsErrorSubtleBrush"] = error.ToHexWithAlpha(0.15f);
        Set("DsWarningBrush", warning);
        colors["DsWarningSubtleBrush"] = warning.ToHexWithAlpha(0.15f);
        Set("DsSuccessBrush", success);
        Set("DsGreenBrush", success);
        Set("DsRedBrush", error);
        Set("DsInfoBrush", info);

        // Brands of other products keep their own colours.
        colors["DsJupyterBrush"] = "#E34C26";
        colors["DsJupyterSubtleBrush"] = "#26E34C26";
        colors["DsLinqBrush"] = "#238636";
        colors["DsLinqSubtleBrush"] = "#26238636";
        colors["DsLeetCodeBrush"] = dark ? "#FFA116" : "#D97706";
        Set("NotebookAmberBrush", warning);
        Set("BrainPurpleBrush", tertiary);

        // Badges.
        Badge("BadgeEasy", success);
        Badge("BadgeMedium", warning);
        Badge("BadgeHard", error);
        Badge("BadgeAmber", warning);

        // Material 3.
        Set("M3BackgroundBrush", bg);
        Set("M3SurfaceBrush", bg);
        Set("M3SurfaceContainerBrush", surface);
        Set("M3SurfaceContainerLowBrush", surface);
        Set("M3SurfaceContainerLowestBrush", surfaceLowest);
        Set("M3SurfaceContainerHighBrush", surfaceHigh);
        Set("M3SurfaceContainerHighestBrush", surfaceHover);
        Set("M3SurfaceVariantBrush", surfaceHover);
        Set("M3OnSurfaceBrush", heading);
        Set("M3OnSurfaceVariantBrush", muted);
        Set("M3OutlineBrush", engine.Compose(neutralVariant.Hue, neutralVariant.Chroma, engine.ToneFromLstar(dark ? 32 : 60)));
        Set("M3OutlineVariantBrush", border);
        Set("M3PrimaryBrush", primary);
        colors["M3PrimaryContainerBrush"] = primary.ToHexWithAlpha(0.15f);
        Set("M3OnPrimaryContainerBrush", primary);
        Set("M3SecondaryContainerBrush", surfaceHover);
        Set("M3OnSecondaryContainerBrush", text);
        Set("M3TertiaryBrush", tertiary);
        Set("M3WarningBrush", warning);

        // Navigation categories.
        foreach (var (key, reference) in NavigationHues)
        {
            var hue = engine.Decompose(ColorRgb.FromHex(reference)).Hue;
            var chroma = key == "Settings" ? palette.Parts[PaletteSections.NeutralVariant].Chroma * 2 : engine.VividChroma * Math.Clamp(spec.ChromaBoost, 0.3, 1.6) * 0.9;
            var fg = ContrastSolver.SolveTone(engine, hue, chroma, engine.ToneFromLstar(dark ? 72 : 45), surface, target, 45f).Color;
            Set($"Nav{key}FgBrush", fg);
            colors[$"Nav{key}BgBrush"] = fg.ToHexWithAlpha(dark ? 0.10f : 0.08f);
        }

        // Server methods and types: a tinted pill with readable text and a border.
        foreach (var (key, reference) in ServerHues)
        {
            var hue = engine.Decompose(ColorRgb.FromHex(reference)).Hue;
            var chroma = engine.VividChroma * (key.EndsWith("Options", StringComparison.Ordinal) ? 0.25 : 0.9);
            var pill = engine.Compose(hue, chroma * 0.35, engine.ToneFromLstar(dark ? 16 : 94));
            Set($"{key}BgBrush", pill);
            Set($"{key}FgBrush", ContrastSolver.SolveTone(engine, hue, chroma, engine.ToneFromLstar(dark ? 75 : 30), pill, target, 60f).Color);
            Set($"{key}BorderBrush", engine.Compose(hue, chroma * 0.8, engine.ToneFromLstar(dark ? 42 : 80)));
        }

        return colors;

        void Set(string key, ColorRgb color) => colors[key] = color.ToHex();

        ColorRgb Solve(PerceptualColor role, double lstar, ColorRgb against, float minRatio, float minLc) =>
            ContrastSolver.SolveTone(engine, role.Hue, role.Chroma, engine.ToneFromLstar(lstar), against, minRatio, minLc).Color;

        // A key colour, kept as it is when it is readable on the surface, moved in tone only as far as needed when not.
        ColorRgb Accent(string role, ColorRgb against)
        {
            var key = palette.Keys[role];
            if (ContrastSolver.Meets(key, against, target, 45f)) return key;
            var parts = engine.Decompose(key);
            return ContrastSolver.SolveTone(engine, parts.Hue, palette.Parts[role].Chroma, parts.Tone, against, target, 45f).Color;
        }

        ColorRgb Shift(ColorRgb color, double toneDelta)
        {
            var parts = engine.Decompose(color);
            return engine.Compose(parts.Hue, parts.Chroma, parts.Tone + toneDelta);
        }

        void Badge(string key, ColorRgb fg)
        {
            colors[$"{key}BgBrush"] = fg.ToHexWithAlpha(0.10f);
            colors[$"{key}BorderBrush"] = fg.ToHexWithAlpha(dark ? 0.20f : 0.30f);
            Set($"{key}FgBrush", fg);
        }
    }
}
