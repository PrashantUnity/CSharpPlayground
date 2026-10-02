using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Resolves a string resource key (e.g. "CodeStringBrush", "CodeMemberBrush") to the
/// current theme's <see cref="IBrush" /> by looking it up in <see cref="Application.Current" />'s
/// merged resource dictionaries. This allows Model-layer code to return a semantic brush key
/// rather than a hardcoded hex value, so the visual automatically adapts to light/dark themes.
/// </summary>
/// <example>
/// <code>
/// Foreground="{Binding ValueForegroundKey,
///     Converter={x:Static controls:ResourceKeyBrushConverter.Instance}}"
/// </code>
/// </example>
public sealed class ResourceKeyBrushConverter : IValueConverter
{
    public static readonly ResourceKeyBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current is not null)
        {
            // TryGetResource accepts the current ThemeVariant for theme-aware lookups.
            var theme = Application.Current.ActualThemeVariant;
            if (Application.Current.TryGetResource(key, theme, out var res) && res is IBrush brush)
                return brush;
        }

        // Graceful fallback — transparent so the text is still legible via inherited color
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
