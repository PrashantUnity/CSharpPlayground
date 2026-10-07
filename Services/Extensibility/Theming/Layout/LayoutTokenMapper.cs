using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

/// <summary>
/// The layout design tokens and their values for a <see cref="LayoutSpec"/>. At the default spec every token equals the
/// size the studio was drawn with before it had tokens, so the default layout looks exactly as before.
/// <list type="bullet">
/// <item>Type ramp <c>DsFontSize050…800</c>, weights <c>DsWeight{Body,Label,Emphasis,Strong}</c>, letter spacing
/// <c>DsTracking050…120</c>, fonts <c>DsUiFontFamily</c> and <c>DsCodeFontFamily</c>.</item>
/// <item>Radii <c>DsRadius{XS…4XL,Full}</c> (plus <c>Top/Bottom/Left/Right</c> variants) and per component
/// (<c>DsRadiusCard</c>, …).</item>
/// <item>Borders <c>DsBorder{Thin,Medium,Top,Bottom,Left,Right,AccentLeft}</c>.</item>
/// <item>Spacing <c>DsSpace2…32</c> (named by their default pixels), card padding, control and tab heights.</item>
/// <item>Shadows <c>DsShadowLevel0…4</c>, <c>DsCardShadow</c> and the palette's modal, floating and Material
/// elevation shadows.</item>
/// </list>
/// </summary>
public static class LayoutTokenMapper
{
    /// <summary>The code font when the layout doesn't choose one.</summary>
    public const string DefaultCodeFont = "Cascadia Code, JetBrains Mono, Fira Code, Menlo, Consolas, monospace";

    /// <summary>Type ramp steps and their sizes at the default layout.</summary>
    public static readonly IReadOnlyList<(string Step, double Size)> TypeRamp =
    [
        ("050", 8.5), ("075", 9.5), ("100", 10), ("150", 10.5), ("200", 11), ("250", 11.5), ("300", 12),
        ("350", 12.5), ("400", 13), ("500", 14), ("600", 16), ("700", 20), ("800", 26),
    ];

    /// <summary>Radius steps and their sizes at the default layout.</summary>
    public static readonly IReadOnlyList<(string Step, double Radius)> RadiusSteps =
    [
        ("XS", 3), ("SM", 4), ("MD", 6), ("LG", 8), ("XL", 10), ("2XL", 12), ("3XL", 16), ("4XL", 24),
    ];

    /// <summary>The radius step each component uses unless overridden.</summary>
    public static readonly IReadOnlyDictionary<string, double> ComponentRadius = new Dictionary<string, double>
    {
        [LayoutComponents.Card] = 10,
        [LayoutComponents.Button] = 6,
        [LayoutComponents.Input] = 6,
        [LayoutComponents.Tab] = 6,
        [LayoutComponents.Row] = 4,
        [LayoutComponents.Dialog] = 16,
        [LayoutComponents.Chip] = 12,
        [LayoutComponents.Tooltip] = 6,
    };

    /// <summary>Letter-spacing steps (upper-case labels) at the default layout.</summary>
    public static readonly IReadOnlyList<(string Step, double Spacing)> Tracking = [("050", 0.5), ("060", 0.6), ("080", 0.8), ("100", 1.0), ("120", 1.2)];

    /// <summary>Spacing steps, named by their default pixels.</summary>
    public static readonly IReadOnlyList<double> SpaceSteps = [2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 32];

    /// <summary>Heights of text controls (buttons, boxes), named like the type ramp.</summary>
    public static readonly IReadOnlyList<(string Step, double Height)> ControlHeights = [("100", 26), ("150", 28), ("200", 30), ("300", 32), ("400", 34), ("500", 36)];

    public const double DefaultTabHeight = 35;
    public const double DefaultRowHeight = 28;
    public const double DefaultCardPaddingX = 18;
    public const double DefaultCardPaddingY = 16;
    public const double FullRadius = 9999;

    public static IReadOnlyDictionary<string, object> Map(LayoutSpec spec, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var tokens = new Dictionary<string, object>(StringComparer.Ordinal);

        // Fonts. Without a choice the interface keeps the platform's default font (what text inherits today).
        var ui = string.IsNullOrWhiteSpace(spec.UiFont) ? FontFamily.Default : new FontFamily(spec.UiFont);
        var code = new FontFamily(string.IsNullOrWhiteSpace(spec.CodeFont) ? DefaultCodeFont : spec.CodeFont);
        tokens["DsUiFontFamily"] = ui;
        tokens["M3FontFamily"] = ui;
        tokens["M3FontFamilyDisplay"] = ui;
        tokens["DsCodeFontFamily"] = code;
        tokens["M3FontFamilyMono"] = code;
        tokens["JetBrainsMonoFontFamily"] = code;

        // Type ramp: every step follows the base size; hierarchy stretches or flattens the steps around it.
        var baseSize = Math.Clamp(spec.BaseFontSize, 8, 24);
        var hierarchy = Math.Clamp(spec.Hierarchy, 0.5, 1.6);
        foreach (var (step, size) in TypeRamp)
        {
            tokens["DsFontSize" + step] = Quarter(baseSize * Math.Pow(size / LayoutSpec.DefaultBaseFontSize, hierarchy));
        }

        tokens["M3FontSizeBodyMedium"] = tokens["DsFontSize500"];
        tokens["M3FontSizeLabelMedium"] = tokens["DsFontSize300"];
        tokens["M3FontSizeLabelSmall"] = tokens["DsFontSize200"];

        tokens["DsWeightBody"] = Weight(spec.BodyWeight, FontWeight.Normal);
        tokens["DsWeightLabel"] = Weight(spec.LabelWeight, FontWeight.Medium);
        tokens["DsWeightEmphasis"] = Weight(spec.EmphasisWeight, FontWeight.SemiBold);
        tokens["DsWeightStrong"] = Weight(spec.StrongWeight, FontWeight.Bold);

        var tracking = Math.Clamp(spec.CapsLetterSpacing, 0, 3);
        foreach (var (step, spacing) in Tracking) tokens["DsTracking" + step] = Math.Round(spacing * tracking, 2);

        // Shape: radii scale together; pills and circles stay round.
        var radiusScale = Math.Clamp(spec.RadiusScale, 0, 3);
        foreach (var (step, radius) in RadiusSteps) AddRadius("DsRadius" + step, Half(radius * radiusScale));
        AddRadius("DsRadiusFull", FullRadius);

        // Material 3's shape scale, as the palette had it, following the same lever.
        foreach (var (name, radius) in new (string, double)[] { ("None", 0), ("ExtraSmall", 4), ("Small", 8), ("Medium", 12), ("Large", 16), ("ExtraLarge", 28) })
        {
            tokens["M3ShapeCorner" + name] = new CornerRadius(Half(radius * radiusScale));
        }

        var m3Medium = Half(12 * radiusScale);
        tokens["M3ShapeCornerFull"] = new CornerRadius(FullRadius);
        tokens["M3ShapeTopRoundedMedium"] = new CornerRadius(m3Medium, m3Medium, 0, 0);
        tokens["M3ShapeBottomRoundedMedium"] = new CornerRadius(0, 0, m3Medium, m3Medium);
        tokens["M3ShapeLeftRoundedPill"] = new CornerRadius(FullRadius, 0, 0, FullRadius);
        tokens["M3ShapeRightRoundedPill"] = new CornerRadius(0, FullRadius, FullRadius, 0);
        foreach (var component in LayoutComponents.All)
        {
            var radius = spec.RadiusOverrides.TryGetValue(component, out var set) ? Math.Max(0, set) : Half(ComponentRadius[component] * radiusScale);
            AddRadius("DsRadius" + component, radius);
        }

        // Borders and dividers.
        var width = Math.Clamp(spec.BorderWidth, 0, 4);
        var divider = spec.Dividers ? width : 0;
        tokens["DsBorderThin"] = new Thickness(width);
        tokens["DsBorderMedium"] = new Thickness(width * 1.5);
        tokens["DsBorderTop"] = new Thickness(0, divider, 0, 0);
        tokens["DsBorderBottom"] = new Thickness(0, 0, 0, divider);
        tokens["DsBorderLeft"] = new Thickness(divider, 0, 0, 0);
        tokens["DsBorderRight"] = new Thickness(0, 0, divider, 0);
        tokens["DsBorderNoBottom"] = new Thickness(width, width, width, 0);
        tokens["DsBorderAccentLeft"] = new Thickness(Math.Clamp(spec.AccentBarWidth, 0, 8), 0, 0, 0);
        tokens["DsCardBorder"] = new Thickness(spec.CardBorders ? width : 0);

        // Spacing: containers and controls follow density × scale.
        var f = spec.SpacingFactor;
        foreach (var px in SpaceSteps) tokens["DsSpace" + Name(px)] = Half(px * f);
        tokens["DsCardPadding"] = new Thickness(
            Size(spec, "CardPaddingX", DefaultCardPaddingX * f),
            Size(spec, "CardPaddingY", DefaultCardPaddingY * f));
        foreach (var (step, height) in ControlHeights) tokens["DsControlHeight" + step] = Half(height * f);
        tokens["DsControlHeight"] = Size(spec, "ControlHeight", 32 * f);
        tokens["DsControlHeightSmall"] = Size(spec, "ControlHeightSmall", 26 * f);
        tokens["DsRowHeight"] = Size(spec, "RowHeight", DefaultRowHeight * f);
        tokens["DsTabHeight"] = Size(spec, "TabHeight", DefaultTabHeight * f);

        // The density tokens of the scripting API, kept for scripts that read them. The activity bar stays 48 px wide.
        tokens["DensityTabHeight"] = tokens["DsTabHeight"];
        tokens["DensityRailWidth"] = 48.0;
        tokens["DensityPaddingSmall"] = Half(4 * f);
        tokens["DensityPaddingMedium"] = Half(8 * f);

        // Shadows.
        foreach (var (key, value) in Shadows(spec, isDark)) tokens[key] = value;
        return tokens;

        void AddRadius(string key, double r)
        {
            tokens[key] = new CornerRadius(r);
            tokens[key + "Top"] = new CornerRadius(r, r, 0, 0);
            tokens[key + "Bottom"] = new CornerRadius(0, 0, r, r);
            tokens[key + "Left"] = new CornerRadius(r, 0, 0, r);
            tokens[key + "Right"] = new CornerRadius(0, r, r, 0);
        }
    }

    /// <summary>The token name of a spacing step (<c>DsSpace16</c>).</summary>
    public static string SpaceToken(double px) => "DsSpace" + Name(px);

    /// <summary>The shadow tokens: Material elevation levels and the studio's modal, floating and bar shadows.</summary>
    public static IReadOnlyDictionary<string, BoxShadows> Shadows(LayoutSpec spec, bool isDark)
    {
        var strength = Math.Clamp(spec.ShadowStrength, 0, 3);
        var softness = Math.Clamp(spec.ShadowSoftness, 0.25, 3);
        BoxShadow Shadow(double y, double blur, uint alpha) => new()
        {
            OffsetX = 0,
            OffsetY = Math.Round(y * Math.Sqrt(softness), 1),
            Blur = Math.Round(blur * softness, 1),
            Spread = 0,
            Color = Color.FromArgb((byte)Math.Clamp(Math.Round(alpha * strength), 0, 255), 0, 0, 0),
        };

        BoxShadows Of(params BoxShadow[] layers) =>
            strength <= 0 ? new BoxShadows(default) : layers.Length == 1 ? new BoxShadows(layers[0]) : new BoxShadows(layers[0], layers[1..]);

        // Material 3's levels (as the palette had them), deeper in the dark where shadows read less.
        var dark = isDark ? 2.2 : 1.0;
        uint A(uint alpha) => (uint)Math.Min(255, Math.Round(alpha * dark));
        var level1 = Of(Shadow(1, 3, 0x14), Shadow(1, 2, 0x1E));
        var level2 = Of(Shadow(2, 6, 0x16), Shadow(4, 8, 0x1C));
        var level3 = Of(Shadow(4, 12, 0x1A), Shadow(8, 20, 0x20));
        var modalDialog = Of(Shadow(8, 24, 0x28), Shadow(24, 64, 0x48));
        var shadows = new Dictionary<string, BoxShadows>(StringComparer.Ordinal)
        {
            ["M3ElevationLevel0"] = new BoxShadows(default),
            ["M3ElevationLevel1"] = level1,
            ["M3ElevationLevel2"] = level2,
            ["M3ElevationLevel3"] = level3,
            ["M3ElevationModalDialog"] = modalDialog,
            ["DsShadowLevel0"] = new BoxShadows(default),
            ["DsShadowLevel1"] = Of(Shadow(1, 3, A(0x14)), Shadow(1, 2, A(0x1E))),
            ["DsShadowLevel2"] = Of(Shadow(2, 6, A(0x16)), Shadow(4, 8, A(0x1C))),
            ["DsShadowLevel3"] = Of(Shadow(4, 12, A(0x1A)), Shadow(8, 20, A(0x20))),
            ["DsShadowLevel4"] = Of(Shadow(8, 24, A(0x28)), Shadow(24, 64, A(0x48))),
            ["DsModalShadow"] = Of(Shadow(24, 64, isDark ? 0xB0u : 0x40u)),
            ["DsFloatingShadow"] = Of(Shadow(8, 24, isDark ? 0x60u : 0x20u)),
            ["DsBottomBarShadow"] = Of(Shadow(-4, 16, isDark ? 0x40u : 0x15u)),
        };
        shadows["DsCardShadow"] = shadows["DsShadowLevel" + Math.Clamp(spec.CardElevation, 0, 4)];
        return shadows;
    }

    private static double Size(LayoutSpec spec, string key, double fallback) =>
        spec.SizeOverrides.TryGetValue(key, out var value) ? Math.Max(0, value) : Half(fallback);

    private static FontWeight Weight(string? name, FontWeight fallback) =>
        Enum.TryParse<FontWeight>(name, ignoreCase: true, out var weight) ? weight : fallback;

    private static double Half(double value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;

    private static double Quarter(double value) => Math.Round(value * 4, MidpointRounding.AwayFromZero) / 4;

    private static string Name(double px) => px.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
