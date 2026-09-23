using Avalonia;
using Avalonia.Styling;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Toggles the app-wide Avalonia theme variant. Callers must invoke this from the UI thread (e.g. a
/// button command) — reading Avalonia styled properties off the UI thread throws.
/// </summary>
public static class ThemeService
{
    public static bool IsDark => Application.Current?.ActualThemeVariant != ThemeVariant.Light;

    public static bool ToggleTheme()
    {
        var app = Application.Current;
        if (app == null) return IsDark;

        var goingDark = !IsDark;
        app.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;
        return goingDark;
    }
}
