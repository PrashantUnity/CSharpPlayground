using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

/// <summary>The components whose shape and spacing can be set on their own (otherwise they follow the global scales).</summary>
public static class LayoutComponents
{
    public const string Card = "Card";
    public const string Button = "Button";
    public const string Input = "Input";
    public const string Tab = "Tab";
    public const string Row = "Row";
    public const string Dialog = "Dialog";
    public const string Chip = "Chip";
    public const string Tooltip = "Tooltip";

    public static readonly IReadOnlyList<string> All = [Card, Button, Input, Tab, Row, Dialog, Chip, Tooltip];
}

/// <summary>
/// How the studio is laid out and set in type, independent of its colours: fonts, the type ramp, weights, corner radii,
/// borders, spacing and density, and shadows. Immutable and deterministic (the same spec always gives the same tokens),
/// saved as JSON in the preferences and in the layout library, and stepped through by undo.
/// </summary>
public sealed record LayoutSpec
{
    public const int CurrentVersion = 1;

    /// <summary>The type ramp's reference size: the size of body text (<c>DsFontSize300</c>) at 100%.</summary>
    public const double DefaultBaseFontSize = 12;

    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public static LayoutSpec Default { get; } = new();

    public int Version { get; init; } = CurrentVersion;

    /// <summary>The preset this layout started from (for display only).</summary>
    public string? PresetId { get; init; }

    // ── Typography ──────────────────────────────────────────────────────────

    /// <summary>The interface font; <c>null</c> keeps the studio's default.</summary>
    public string? UiFont { get; init; }

    /// <summary>The font of code everywhere (editors, code blocks, terminals); <c>null</c> keeps the default.</summary>
    public string? CodeFont { get; init; }

    /// <summary>Body text size; every step of the type ramp follows it (10–16).</summary>
    public double BaseFontSize { get; init; } = DefaultBaseFontSize;

    /// <summary>How far headings and captions stand from body text: 1 as designed, below 1 flatter, above 1 bolder (0.85–1.25).</summary>
    public double Hierarchy { get; init; } = 1.0;

    public string BodyWeight { get; init; } = nameof(FontWeight.Normal);

    public string LabelWeight { get; init; } = nameof(FontWeight.Medium);

    public string EmphasisWeight { get; init; } = nameof(FontWeight.SemiBold);

    public string StrongWeight { get; init; } = nameof(FontWeight.Bold);

    /// <summary>Multiplies the letter spacing of upper-case labels (0–2).</summary>
    public double CapsLetterSpacing { get; init; } = 1.0;

    // ── Shape ───────────────────────────────────────────────────────────────

    /// <summary>Multiplies every corner radius (0 square … 2.5 very round); pills and circles stay round.</summary>
    public double RadiusScale { get; init; } = 1.0;

    /// <summary>Corner radius of a component in pixels, overriding the scale (see <see cref="LayoutComponents"/>).</summary>
    public IReadOnlyDictionary<string, double> RadiusOverrides { get; init; } = new Dictionary<string, double>();

    // ── Borders ─────────────────────────────────────────────────────────────

    /// <summary>Width of borders and dividers in pixels (0–3).</summary>
    public double BorderWidth { get; init; } = 1;

    /// <summary>Whether cards and panels draw their outline.</summary>
    public bool CardBorders { get; init; } = true;

    /// <summary>Whether divider lines between rows and regions are drawn.</summary>
    public bool Dividers { get; init; } = true;

    /// <summary>Width of the coloured accent bar on selected or flagged items (0–6).</summary>
    public double AccentBarWidth { get; init; } = 2;

    // ── Spacing ─────────────────────────────────────────────────────────────

    public LayoutDensity Density { get; init; } = LayoutDensity.Comfortable;

    /// <summary>Multiplies the spacing of containers and controls, on top of the density (0.75–1.5).</summary>
    public double SpacingScale { get; init; } = 1.0;

    /// <summary>Component sizes in pixels overriding the scales: ControlHeight, ControlHeightSmall, RowHeight, TabHeight, CardPaddingX, CardPaddingY.</summary>
    public IReadOnlyDictionary<string, double> SizeOverrides { get; init; } = new Dictionary<string, double>();

    // ── Elevation ───────────────────────────────────────────────────────────

    /// <summary>Multiplies shadow opacity (0 none … 2 strong).</summary>
    public double ShadowStrength { get; init; } = 1.0;

    /// <summary>Multiplies shadow blur and distance (0.5 crisp … 2 soft).</summary>
    public double ShadowSoftness { get; init; } = 1.0;

    /// <summary>The shadow level of cards: 0 flat … 4 floating.</summary>
    public int CardElevation { get; init; }

    /// <summary>The spacing multiplier of a density.</summary>
    public static double DensityFactor(LayoutDensity density) => density switch
    {
        LayoutDensity.Compact => 0.85,
        LayoutDensity.Spacious => 1.15,
        _ => 1.0,
    };

    /// <summary>Density and spacing scale together.</summary>
    [JsonIgnore]
    public double SpacingFactor => DensityFactor(Density) * Math.Clamp(SpacingScale, 0.5, 2.0);

    public LayoutSpec WithRadiusOverride(string component, double? radius) => this with { RadiusOverrides = With(RadiusOverrides, component, radius) };

    public LayoutSpec WithSizeOverride(string key, double? size) => this with { SizeOverrides = With(SizeOverrides, key, size) };

    private static Dictionary<string, double> With(IReadOnlyDictionary<string, double> source, string key, double? value)
    {
        var copy = new Dictionary<string, double>(source, StringComparer.Ordinal);
        if (value is double v) copy[key] = v;
        else copy.Remove(key);
        return copy;
    }

    public JsonElement ToJson() => JsonSerializer.SerializeToElement(this, JsonOptions);

    /// <summary>A spec read back from JSON; <c>null</c> when it isn't one (or is from a newer version).</summary>
    public static LayoutSpec? FromJson(JsonElement? json)
    {
        // Ours always carries its version: any other object (which would read as all defaults) isn't a layout.
        if (json is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(nameof(Version), out _)) return null;
        try
        {
            var spec = element.Deserialize<LayoutSpec>(JsonOptions);
            if (spec == null || spec.Version > CurrentVersion) return null;
            return spec with
            {
                RadiusOverrides = new Dictionary<string, double>(spec.RadiusOverrides ?? new Dictionary<string, double>(), StringComparer.Ordinal),
                SizeOverrides = new Dictionary<string, double>(spec.SizeOverrides ?? new Dictionary<string, double>(), StringComparer.Ordinal),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether two specs give the same layout (records compare their dictionaries by reference).</summary>
    public bool SameLayoutAs(LayoutSpec other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return (this with { PresetId = null, RadiusOverrides = Empty, SizeOverrides = Empty })
               == (other with { PresetId = null, RadiusOverrides = Empty, SizeOverrides = Empty })
               && SameEntries(RadiusOverrides, other.RadiusOverrides)
               && SameEntries(SizeOverrides, other.SizeOverrides);
    }

    private static readonly IReadOnlyDictionary<string, double> Empty = new Dictionary<string, double>();

    private static bool SameEntries(IReadOnlyDictionary<string, double> a, IReadOnlyDictionary<string, double> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v.Equals(kv.Value));
}

/// <summary>A built-in layout.</summary>
public sealed record LayoutPreset(string Id, string Name, string Description, LayoutSpec Spec);

/// <summary>The studio's built-in layouts: the looks of well-known design systems, each a starting point.</summary>
public static class LayoutPresets
{
    public const string StudioId = "layout-studio";

    public static readonly IReadOnlyList<LayoutPreset> All =
    [
        new(StudioId, "Studio", "The studio as designed: balanced radius, 1 px borders, flat cards.", LayoutSpec.Default with { PresetId = StudioId }),
        new("layout-vscode", "VS Code", "Compact and crisp: small radii, dense rows, no shadows.", LayoutSpec.Default with
        {
            PresetId = "layout-vscode", Density = LayoutDensity.Compact, RadiusScale = 0.5, BaseFontSize = 12, ShadowStrength = 0.6,
            LabelWeight = nameof(FontWeight.Normal), EmphasisWeight = nameof(FontWeight.SemiBold),
        }),
        new("layout-fluent", "Fluent 2", "Windows 11: 4 and 8 px corners, soft layered shadows.", LayoutSpec.Default with
        {
            PresetId = "layout-fluent", UiFont = "Segoe UI Variable, Segoe UI, Inter, sans-serif", RadiusScale = 0.75, CardElevation = 1, ShadowSoftness = 1.3,
        }),
        new("layout-material", "Material 3", "Roomy and round: 12–28 px corners, larger type, tonal elevation.", LayoutSpec.Default with
        {
            PresetId = "layout-material", UiFont = "Roboto, Inter, sans-serif", RadiusScale = 1.6, Density = LayoutDensity.Spacious, BaseFontSize = 13,
            CardBorders = false, CardElevation = 1,
        }),
        new("layout-macos", "macOS", "System font, gentle radii and soft, wide shadows.", LayoutSpec.Default with
        {
            PresetId = "layout-macos", UiFont = "SF Pro Text, -apple-system, BlinkMacSystemFont, Inter, sans-serif", RadiusScale = 1.15, ShadowSoftness = 1.6,
            CardElevation = 1, BorderWidth = 1,
        }),
        new("layout-sharp", "Sharp", "Square corners, hairline borders, no shadows.", LayoutSpec.Default with
        {
            PresetId = "layout-sharp", RadiusScale = 0, ShadowStrength = 0,
        }),
        new("layout-soft", "Soft", "Very round, borderless cards lifted by soft shadows.", LayoutSpec.Default with
        {
            PresetId = "layout-soft", RadiusScale = 1.6, CardBorders = false, CardElevation = 2, ShadowSoftness = 1.5, ShadowStrength = 0.8,
        }),
        new("layout-large", "Large text", "Bigger, heavier type and more room, for comfortable reading.", LayoutSpec.Default with
        {
            PresetId = "layout-large", BaseFontSize = 14, SpacingScale = 1.15, BodyWeight = nameof(FontWeight.Medium),
            LabelWeight = nameof(FontWeight.SemiBold), EmphasisWeight = nameof(FontWeight.Bold), StrongWeight = nameof(FontWeight.ExtraBold),
        }),
    ];

    public static LayoutPreset? Get(string? id) => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool IsBuiltIn(string? id) => Get(id) != null;
}
