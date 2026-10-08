using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;

/// <summary>
/// Represents a single frame in an animated GIF sequence.
/// </summary>
/// <param name="RgbaPixels">Raw 32-bit RGBA/BGRA pixel buffer (length = Width * Height * 4).</param>
/// <param name="Width">Width of the frame in pixels.</param>
/// <param name="Height">Height of the frame in pixels.</param>
/// <param name="Delay">Duration this frame is displayed.</param>
/// <param name="IsBgra">True if pixel format is BGRA (default for Avalonia RenderTargetBitmap), false if RGBA.</param>
/// <param name="HasTransparency">Whether pixels with alpha &lt; 128 should be treated as transparent.</param>
public sealed record GifFrame(
    byte[] RgbaPixels,
    int Width,
    int Height,
    TimeSpan Delay,
    bool IsBgra = true,
    bool HasTransparency = false)
{
    /// <summary>
    /// Frame delay expressed in hundredths of a second (centiseconds), as required by GIF89a.
    /// Clamped between 2 (20ms, ~50fps) and 65535 (655.35 seconds).
    /// </summary>
    public int DelayCentiseconds =>
        Math.Clamp((int)Math.Round(Delay.TotalMilliseconds / 10.0), 2, 65535);
}
