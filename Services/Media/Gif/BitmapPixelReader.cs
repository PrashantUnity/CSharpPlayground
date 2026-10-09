using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;

/// <summary>
/// Fast, zero-allocation helper for reading raw BGRA pixel bytes from Avalonia RenderTargetBitmap instances.
/// </summary>
public static class BitmapPixelReader
{
    /// <summary>
    /// Reads the raw 32-bit BGRA pixel bytes from a RenderTargetBitmap into a managed byte array.
    /// </summary>
    public static byte[] ReadPixels(RenderTargetBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var size = bitmap.PixelSize;
        int stride = size.Width * 4;
        byte[] buffer = new byte[size.Height * stride];

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return buffer;
    }

    /// <summary>
    /// Converts a rendered RenderTargetBitmap into a GifFrame with the given display duration.
    /// </summary>
    public static GifFrame CreateFrame(RenderTargetBitmap bitmap, TimeSpan delay, bool hasTransparency = false)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var pixels = ReadPixels(bitmap);
        return new GifFrame(pixels, bitmap.PixelSize.Width, bitmap.PixelSize.Height, delay, IsBgra: true, HasTransparency: hasTransparency);
    }
}
