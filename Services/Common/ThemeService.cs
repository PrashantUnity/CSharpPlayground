using Avalonia;
using Avalonia.Styling;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>
/// Toggles between the default light and dark themes. Callers must invoke this from the UI thread (e.g. a
/// button command) — reading Avalonia styled properties off the UI thread throws.
/// </summary>
public static class ThemeService
{
    public static bool IsDark
    {
        get
        {
            var app = Application.Current;
            if (app == null) return true;
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                return true;
            }
            try
            {
                return app.ActualThemeVariant != ThemeVariant.Light;
            }
            catch
            {
                return true;
            }
        }
    }

    public static bool ToggleTheme()
    {
        var app = Application.Current;
        if (app == null || !Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) return IsDark;

        var goingDark = !IsDark;
        // Through the theme engine, so the studio's colours switch with the controls' variant (flipping only the
        // variant left a dark theme's colours on light controls) and the choice is remembered with the other settings.
        var themeId = goingDark ? Extensibility.Theming.BuiltInThemes.DarkPlus.Id : Extensibility.Theming.BuiltInThemes.LightPlus.Id;
        if (!Extensibility.StudioAppContext.Instance.ThemeEngine.ApplyTheme(themeId))
        {
            app.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        return goingDark;
    }
}
