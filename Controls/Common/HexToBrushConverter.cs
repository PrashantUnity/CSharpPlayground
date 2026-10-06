using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Converts a hex color string (e.g. "#2F81F7", "#1A2F81F7") or token key to an Avalonia IBrush.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            if (s.StartsWith('#') && Color.TryParse(s, out var color))
            {
                return new SolidColorBrush(color);
            }

            if (Application.Current is not null &&
                Application.Current.TryGetResource(s, Application.Current.ActualThemeVariant, out var res) &&
                res is IBrush brush)
            {
                return brush;
            }
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
