using System.Reflection;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Encodes a SkiaSharp image object (SKBitmap, SKImage, SKSurface or SKData) as PNG bytes. SkiaSharp is found by
/// reflection: scripts may reference it while FrySharp itself does not.
/// </summary>
public static class SkiaImages
{
    /// <summary>True when <paramref name="value"/> is one of SkiaSharp's image types.</summary>
    public static bool IsSkiaImage(object? value) =>
        value?.GetType().FullName is { } name &&
        (name == "SkiaSharp.SKBitmap" || name == "SkiaSharp.SKImage" || name == "SkiaSharp.SKSurface" || name == "SkiaSharp.SKData");

    /// <summary>The image as PNG bytes, or null when it is not a SkiaSharp image or can't be encoded.</summary>
    public static byte[]? TryEncodePng(object? value)
    {
        if (!IsSkiaImage(value)) return null;

        try
        {
            return value!.GetType().FullName switch
            {
                "SkiaSharp.SKBitmap" => EncodeBitmap(value),
                "SkiaSharp.SKImage" => EncodeImage(value),
                "SkiaSharp.SKSurface" => value.GetType().GetMethod("Snapshot", Type.EmptyTypes)?.Invoke(value, null) is { } snapshot
                    ? EncodeImage(snapshot)
                    : null,
                "SkiaSharp.SKData" => ToArray(value),
                _ => null,
            };
        }
        catch (TargetInvocationException)
        {
            return null;
        }
    }

    private static byte[]? EncodeBitmap(object bitmap)
    {
        var type = bitmap.GetType();
        var skImageType = type.Assembly.GetType("SkiaSharp.SKImage");
        var fromBitmap = skImageType?.GetMethod("FromBitmap", [type]);
        if (fromBitmap?.Invoke(null, [bitmap]) is { } image)
        {
            using (image as IDisposable)
            {
                return EncodeImage(image);
            }
        }

        // SKBitmap.Encode(SKEncodedImageFormat.Png, 100)
        var formatType = type.Assembly.GetType("SkiaSharp.SKEncodedImageFormat");
        if (formatType != null && Enum.TryParse(formatType, "Png", out var png) &&
            type.GetMethod("Encode", [formatType, typeof(int)])?.Invoke(bitmap, [png, 100]) is { } data)
        {
            using (data as IDisposable)
            {
                return ToArray(data);
            }
        }

        return null;
    }

    private static byte[]? EncodeImage(object image)
    {
        // SKImage.Encode() writes PNG.
        if (image.GetType().GetMethod("Encode", Type.EmptyTypes)?.Invoke(image, null) is not { } data) return null;
        using (data as IDisposable)
        {
            return ToArray(data);
        }
    }

    private static byte[]? ToArray(object data) =>
        data.GetType().GetMethod("ToArray", Type.EmptyTypes)?.Invoke(data, null) as byte[];
}
