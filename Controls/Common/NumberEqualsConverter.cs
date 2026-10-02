using System.Globalization;
using Avalonia.Data.Converters;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Compares an integer-valued binding to an integer ConverterParameter and returns true when equal.
/// Solves the Avalonia ObjectConverters.Equal mismatch where a boxed <c>int</c> binding is compared
/// against a <c>string</c> ConverterParameter (always false), breaking the active-tab class binding.
/// </summary>
/// <example>
/// <code>
/// Classes.active="{Binding SelectedBottomTabIndex,
///     Converter={x:Static controls:NumberEqualsConverter.Instance},
///     ConverterParameter=0}"
/// </code>
/// </example>
public sealed class NumberEqualsConverter : IValueConverter
{
    public static readonly NumberEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter is not null &&
            int.TryParse(parameter.ToString(), out int paramInt))
            return intVal == paramInt;

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
