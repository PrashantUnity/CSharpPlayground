using System;
using System.Collections.Generic;
using Avalonia.Styling;

namespace FrySharp.Sdk;

/// <summary>
/// Public API for controlling application themes, color tokens, typography, and styles at runtime.
/// </summary>
public interface IThemeApi
{
    /// <summary>Sets or overrides a color resource token (e.g. 'DsPrimaryBrush', '#FF5722'). Takes effect immediately.</summary>
    void SetColor(string tokenName, string hexOrRgb);

    /// <summary>Gets the current hex string of a color resource token, or null if unset.</summary>
    string? GetColor(string tokenName);

    /// <summary>Sets typography font family and optional font size for a role ('editor', 'ui', 'mono').</summary>
    void SetFont(string role, string fontFamily, double? size = null);

    /// <summary>Sets layout density (Compact, Comfortable, Spacious).</summary>
    void SetDensity(LayoutDensity density);

    /// <summary>Sets or overrides a numeric spacing/padding resource token (e.g. 'DensityPaddingSmall', 'DensityPaddingMedium', 'DensityTabHeight', 'DensityRailWidth').</summary>
    void SetSpacing(string tokenName, double value);

    /// <summary>Sets or overrides a dimension or visual property (e.g. height, width, corner radius, thickness, double, etc.).</summary>
    void SetDimension(string tokenName, double value);

    /// <summary>Sets or overrides an arbitrary Avalonia resource token in real time.</summary>
    void SetResource(string tokenName, object value);

    /// <summary>Gets a resource token value from the active theme or overrides.</summary>
    object? GetResource(string tokenName);

    /// <summary>Registers a complete theme definition into the available theme catalog.</summary>
    void RegisterTheme(ThemeDefinition theme);

    /// <summary>Applies a registered theme by identifier ('dark-plus', 'light-plus', 'dracula', 'cyberpunk', etc.).</summary>
    bool ApplyTheme(string themeId);

    /// <summary>Gets all registered theme definitions.</summary>
    IReadOnlyList<ThemeDefinition> GetAvailableThemes();

    /// <summary>Gets active theme identifier.</summary>
    string ActiveThemeId { get; }

    /// <summary>Dynamically injects a compiled or instantiated Avalonia style into the application style tree.</summary>
    IDisposable AddStyle(IStyle style);

    /// <summary>Dynamically injects Avalonia XAML styles into the application style tree (accepts XAML snippet or avares:// URI).</summary>
    IDisposable AddStyle(string xaml);

    /// <summary>Resets all custom color overrides back to active theme defaults.</summary>
    void ResetToDefaults();

    /// <summary>Lays the studio out by a layout (fonts, type sizes and weights, radii, borders, spacing, shadows) given as the JSON Settings → Layout &amp; Typography exports. Returns false when it isn't one.</summary>
    bool ApplyLayout(string layoutJson);

    /// <summary>The layout in use, as JSON (the same shape <see cref="ApplyLayout"/> takes).</summary>
    string GetLayoutJson();

    /// <summary>The current value of a layout token (e.g. 'DsFontSize300', 'DsRadiusCard', 'DsCardPadding'), or null.</summary>
    object? GetLayoutToken(string tokenName);
}
