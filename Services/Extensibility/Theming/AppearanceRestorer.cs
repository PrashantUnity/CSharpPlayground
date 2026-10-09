using System;
using System.Collections.Generic;
using System.Diagnostics;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Puts the studio back the way the user left it: the active theme (built in, from the library, or a generated or
/// imported one that was never saved, from its snapshot), the layout (with its density), single-token overrides and the
/// light/dark choice.
/// The studio's entry points (the plugin and the Runner) call it once when the studio opens; the customization script
/// runs afterwards, so the user's own code still has the last word.
/// </summary>
public static class AppearanceRestorer
{
    /// <summary>
    /// Applies the appearance saved under <paramref name="baseDirectory"/> (the studio's data folder; the user's own
    /// when not given), reading only the settings file and, at most, one theme file.
    /// </summary>
    public static string? RestoreFrom(string? baseDirectory, DynamicThemeEngine engine)
    {
        var folder = string.IsNullOrWhiteSpace(baseDirectory) ? PdfEditorApp.Plugins.CSharpEditor.Services.Languages.StudioLanguageServices.DefaultBaseDirectory() : baseDirectory;
        try
        {
            return Restore(new StudioSettingsStore(System.IO.Path.Combine(folder, "studio_settings.json")),
                new ThemeLibraryStore(System.IO.Path.Combine(folder, "themes")), engine);
        }
        catch (Exception ex)
        {
            // The studio must open whatever happened to the preferences.
            Debug.WriteLine($"[AppearanceRestorer] Couldn't restore the saved appearance: {ex}");
            return null;
        }
    }

    /// <summary>Applies the saved appearance. Returns the id of the theme it applied, or <c>null</c>.</summary>
    public static string? Restore(StudioSettingsStore settings, ThemeLibraryStore library, DynamicThemeEngine engine)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(engine);
        var appearance = settings.GetSettings().Appearance;
        string? applied = null;

        if (appearance.ActiveThemeId is { Length: > 0 } id)
        {
            if (!engine.HasTheme(id))
            {
                if (library.Get(id) is { } saved)
                {
                    engine.RegisterTheme(saved.Theme);
                }
                else if (appearance.ActiveThemeSnapshot is { } snapshot && string.Equals(snapshot.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    engine.RegisterTheme(Normalize(snapshot));
                }
            }

            if (engine.ApplyTheme(id)) applied = id;
            else Debug.WriteLine($"[AppearanceRestorer] The saved theme '{id}' no longer exists; keeping the default.");
        }
        else if (appearance.PreferDark is bool dark)
        {
            applied = engine.ApplyTheme(dark ? BuiltInThemes.DarkPlus.Id : BuiltInThemes.LightPlus.Id) ? (dark ? BuiltInThemes.DarkPlus.Id : BuiltInThemes.LightPlus.Id) : null;
        }

        // The layout (fonts, sizes, radii, borders, spacing, shadows); a preferences file from before layouts had only a
        // density, which becomes a layout with that density.
        var layout = Layout.LayoutSpec.FromJson(appearance.Layout);
        if (layout == null && Enum.TryParse<LayoutDensity>(appearance.Density, ignoreCase: true, out var density) && density != LayoutDensity.Comfortable)
        {
            layout = Layout.LayoutSpec.Default with { Density = density };
        }

        if (layout != null && !layout.SameLayoutAs(Layout.LayoutSpec.Default)) engine.ApplyLayout(layout);

        foreach (var (token, hex) in appearance.TokenOverrides)
        {
            try
            {
                engine.SetColor(token, hex);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"[AppearanceRestorer] Skipping colour override '{token}' = '{hex}': {ex.Message}");
            }
        }

        return applied;
    }

    /// <summary>A theme read back from JSON, with the case-insensitive token lookups themes are used with.</summary>
    public static ThemeDefinition Normalize(ThemeDefinition theme) => new()
    {
        Id = theme.Id,
        Name = theme.Name,
        Description = theme.Description,
        IsDark = theme.IsDark,
        Colors = new Dictionary<string, string>(theme.Colors ?? new(), StringComparer.OrdinalIgnoreCase),
        Fonts = new Dictionary<string, string>(theme.Fonts ?? new(), StringComparer.OrdinalIgnoreCase),
        Numbers = new Dictionary<string, double>(theme.Numbers ?? new(), StringComparer.OrdinalIgnoreCase),
    };
}
