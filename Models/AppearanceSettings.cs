using System.Collections.Generic;
using System.Text.Json;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// How the studio looks, remembered between runs: the active theme (and, for a generated or imported theme that was
/// not saved to the library, all of its colours), density, colour overrides and the theme studio's controls.
/// </summary>
public sealed class AppearanceSettings
{
    /// <summary>The theme to show at start. <c>null</c>: the default (Dark+).</summary>
    public string? ActiveThemeId { get; set; }

    /// <summary>
    /// The active theme's colours when it is neither built in nor in the theme library (generated or imported and not
    /// saved yet), so it comes back after a restart exactly as it was.
    /// </summary>
    public ThemeDefinition? ActiveThemeSnapshot { get; set; }

    /// <summary>The light/dark choice made with the toolbar toggle, used when no theme was chosen.</summary>
    public bool? PreferDark { get; set; }

    /// <summary>Compact, Comfortable or Spacious.</summary>
    public string Density { get; set; } = "Comfortable";

    /// <summary>Single colour tokens changed by hand, on top of the theme (token key → hex).</summary>
    public Dictionary<string, string> TokenOverrides { get; set; } = new();

    /// <summary>The theme studio's controls (harmony wheel, sliders, vision simulation).</summary>
    public HarmonyStudioSettings Harmony { get; set; } = new();

    /// <summary>The palette being designed (sections, locks, engine, seed), kept as written by the palette generator.</summary>
    public JsonElement? Palette { get; set; }

    /// <summary>The layout in use (fonts, type ramp, radii, borders, spacing, shadows), as the layout spec writes it.</summary>
    public JsonElement? Layout { get; set; }

    /// <summary>The preset or saved layout it was picked from, for showing which one is in use.</summary>
    public string? ActiveLayoutId { get; set; }
}

/// <summary>The theme studio's harmony controls, as the user left them.</summary>
public sealed class HarmonyStudioSettings
{
    public string Mode { get; set; } = "Complementary";
    public float HueDegrees { get; set; } = 210f;
    public bool IsDark { get; set; } = true;
    public float SaturationBoost { get; set; } = 1.0f;
    public float AccentShift { get; set; }
    public float NeutralTint { get; set; } = 10f;
    public float SemanticPull { get; set; } = 15f;
    public float ContrastTarget { get; set; } = 4.5f;
    public string VisionDeficiency { get; set; } = "Normal";
}
