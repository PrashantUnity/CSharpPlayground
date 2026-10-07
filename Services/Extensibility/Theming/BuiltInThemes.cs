using System.Collections.Generic;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Predefined theme definitions ready to be applied or customized.
/// </summary>
public static class BuiltInThemes
{
    public static readonly ThemeDefinition DarkPlus = new()
    {
        Id = "dark-plus",
        Name = "Dark+ (Default)",
        Description = "Standard modern VS Code Dark+ palette",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#0D1117",
            ["DsSurfaceBrush"] = "#161B22",
            ["DsSurfaceHoverBrush"] = "#21262D",
            ["DsSurfaceHighBrush"] = "#1C2128",
            ["DsBorderBrush"] = "#21262D",
            ["DsBorderSubtleBrush"] = "#171B22",
            ["DsTextBrush"] = "#C9D1D9",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#8B949E",
            ["DsPrimaryBrush"] = "#2F81F7",
            ["DsPrimaryHoverBrush"] = "#1F6FEB",
            ["DsPrimarySubtleBrush"] = "#1A2F81F7",
            ["DsSelectionBrush"] = "#992F81F7",
            ["DsFocusBorderBrush"] = "#8C2F81F7",
            ["DsPrimaryBorderSubtleBrush"] = "#332F81F7",
            ["M3PrimaryBrush"] = "#2F81F7"
        }
    };

    public static readonly ThemeDefinition LightPlus = new()
    {
        Id = "light-plus",
        Name = "Light+ (Default)",
        Description = "Standard modern VS Code Light+ palette",
        IsDark = false,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#FFFFFF",
            ["DsSurfaceBrush"] = "#F6F8FA",
            ["DsSurfaceHoverBrush"] = "#EAEEF2",
            ["DsSurfaceHighBrush"] = "#FFFFFF",
            ["DsBorderBrush"] = "#D0D7DE",
            ["DsBorderSubtleBrush"] = "#EAEFF5",
            ["DsTextBrush"] = "#24292F",
            ["DsTextWhiteBrush"] = "#0969DA",
            ["DsMutedBrush"] = "#57606A",
            ["DsPrimaryBrush"] = "#0969DA",
            ["DsPrimaryHoverBrush"] = "#0854B0",
            ["DsPrimarySubtleBrush"] = "#1A0969DA",
            ["DsSelectionBrush"] = "#880969DA",
            ["DsFocusBorderBrush"] = "#8C0969DA",
            ["DsPrimaryBorderSubtleBrush"] = "#330969DA",
            ["M3PrimaryBrush"] = "#0969DA"
        }
    };

    public static readonly ThemeDefinition Dracula = new()
    {
        Id = "dracula",
        Name = "Dracula Pro",
        Description = "Dark gothic purple theme with vibrant pastel accents",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#21222C",
            ["DsSurfaceBrush"] = "#282A36",
            ["DsSurfaceHoverBrush"] = "#343746",
            ["DsSurfaceHighBrush"] = "#44475A",
            ["DsBorderBrush"] = "#44475A",
            ["DsBorderSubtleBrush"] = "#343746",
            ["DsTextBrush"] = "#F8F8F2",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#6272A4",
            ["DsPrimaryBrush"] = "#BD93F9",
            ["DsPrimaryHoverBrush"] = "#D6ACFF",
            ["DsPrimarySubtleBrush"] = "#1ABD93F9",
            ["DsSelectionBrush"] = "#99BD93F9",
            ["DsFocusBorderBrush"] = "#8CBD93F9",
            ["DsPrimaryBorderSubtleBrush"] = "#33BD93F9",
            ["M3PrimaryBrush"] = "#BD93F9"
        }
    };

    public static readonly ThemeDefinition Cyberpunk = new()
    {
        Id = "cyberpunk",
        Name = "Cyberpunk Neon",
        Description = "Futuristic high-contrast neon cyan and hot pink theme",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#0D0E15",
            ["DsSurfaceBrush"] = "#13141F",
            ["DsSurfaceHoverBrush"] = "#1D1E2C",
            ["DsSurfaceHighBrush"] = "#242638",
            ["DsBorderBrush"] = "#2C2D42",
            ["DsBorderSubtleBrush"] = "#1E1F30",
            ["DsTextBrush"] = "#E0E6F0",
            ["DsTextWhiteBrush"] = "#00FFCC",
            ["DsMutedBrush"] = "#727894",
            ["DsPrimaryBrush"] = "#00FFCC",
            ["DsPrimaryHoverBrush"] = "#33FFD6",
            ["DsPrimarySubtleBrush"] = "#1A00FFCC",
            ["DsSelectionBrush"] = "#9900FFCC",
            ["DsFocusBorderBrush"] = "#8C00FFCC",
            ["DsPrimaryBorderSubtleBrush"] = "#3300FFCC",
            ["M3PrimaryBrush"] = "#00FFCC"
        }
    };

    public static readonly ThemeDefinition Monokai = new()
    {
        Id = "monokai",
        Name = "Monokai Classic",
        Description = "Warm retro coding theme with vibrant amber and green",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#272822",
            ["DsSurfaceBrush"] = "#1E1F1C",
            ["DsSurfaceHoverBrush"] = "#3E3D32",
            ["DsSurfaceHighBrush"] = "#49483E",
            ["DsBorderBrush"] = "#3E3D32",
            ["DsBorderSubtleBrush"] = "#2E2E28",
            ["DsTextBrush"] = "#F8F8F2",
            ["DsTextWhiteBrush"] = "#FD971F",
            ["DsMutedBrush"] = "#75715E",
            ["DsPrimaryBrush"] = "#A6E22E",
            ["DsPrimaryHoverBrush"] = "#B8EA4E",
            ["DsPrimarySubtleBrush"] = "#1AA6E22E",
            ["DsSelectionBrush"] = "#99A6E22E",
            ["DsFocusBorderBrush"] = "#8CA6E22E",
            ["DsPrimaryBorderSubtleBrush"] = "#33A6E22E",
            ["M3PrimaryBrush"] = "#A6E22E"
        }
    };

    public static readonly ThemeDefinition OneDark = new()
    {
        Id = "one-dark",
        Name = "One Dark Pro",
        Description = "Deep balanced navy theme with sky blue accents",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["DsBgBrush"] = "#1E1E24",
            ["DsSurfaceBrush"] = "#282C34",
            ["DsSurfaceHoverBrush"] = "#353B45",
            ["DsSurfaceHighBrush"] = "#3E4451",
            ["DsBorderBrush"] = "#3E4451",
            ["DsBorderSubtleBrush"] = "#2C313A",
            ["DsTextBrush"] = "#ABB2BF",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#5C6370",
            ["DsPrimaryBrush"] = "#61AFEF",
            ["DsPrimaryHoverBrush"] = "#74B9F0",
            ["DsPrimarySubtleBrush"] = "#1A61AFEF",
            ["DsSelectionBrush"] = "#9961AFEF",
            ["DsFocusBorderBrush"] = "#8C61AFEF",
            ["DsPrimaryBorderSubtleBrush"] = "#3361AFEF",
            ["M3PrimaryBrush"] = "#61AFEF"
        }
    };

    static BuiltInThemes()
    {
        foreach (var theme in All)
        {
            HarmonicColorGenerator.EnsureCompleteTheme(theme);
        }
    }

    public static IReadOnlyList<ThemeDefinition> All =>
    [
        DarkPlus,
        LightPlus,
        Dracula,
        Cyberpunk,
        Monokai,
        OneDark
    ];
}
