using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Shows a script's image output (<see cref="RichCellOutput.ImageBytes"/>) as a bitmap. Each byte array is decoded once,
/// at most <see cref="ImageDecoder.MaxDisplayWidth"/> across, and the bitmap is kept for as long as the bytes live: a
/// template's binding may be evaluated again and again. Bytes that aren't an image show nothing.
/// </summary>
/// <example>
/// <code>
/// &lt;Image Source="{Binding ImageBytes, Converter={x:Static common:ImageBytesToBitmapConverter.Instance}}" /&gt;
/// </code>
/// </example>
public sealed class ImageBytesToBitmapConverter : IValueConverter
{
    public static readonly ImageBytesToBitmapConverter Instance = new();

    // Keyed by the array itself: an output's bytes never change after it is emitted.
    private static readonly ConditionalWeakTable<byte[], Decoded> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        return Cache.GetValue(bytes, static b => new Decoded(TryDecode(b))).Bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Bitmap? TryDecode(byte[] bytes)
    {
        try
        {
            return ImageDecoder.Decode(bytes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    // A failed decode is remembered too, so it is not retried on every evaluation.
    private sealed record Decoded(Bitmap? Bitmap);
}
