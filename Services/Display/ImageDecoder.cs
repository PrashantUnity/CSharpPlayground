using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Turns image files and image bytes into bitmaps without stalling the studio. Sizes come from the file header (no
/// decode, <see cref="ImageHeader"/>); decoding runs off the UI thread and never at more than <see cref="MaxDisplayWidth"/> pixels across (a
/// 24-megapixel photo decoded in full is ~100 MB and ~1 s of work on the UI thread, for a picture the screen shows at a
/// fraction of that).
/// </summary>
public static class ImageDecoder
{
    /// <summary>Images are decoded at most this wide for display (zooming past it shows the decoded pixels larger).</summary>
    public const int MaxDisplayWidth = 4096;

    /// <summary>Files larger than this are not decoded at all; the viewer says why instead.</summary>
    public const long MaxDecodeBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Decodes <paramref name="data"/> for display: at most <paramref name="maxWidth"/> pixels across when the header
    /// says it is wider, in full otherwise. Safe on any thread.
    /// </summary>
    public static Bitmap Decode(ReadOnlySpan<byte> header, Stream data, int maxWidth = MaxDisplayWidth)
    {
        if (ImageHeader.TryReadDimensions(header, out int width, out _) && width > maxWidth)
        {
            return Bitmap.DecodeToWidth(data, maxWidth, BitmapInterpolationMode.MediumQuality);
        }

        return new Bitmap(data);
    }

    /// <summary>Decodes image bytes for display (see <see cref="Decode(ReadOnlySpan{byte}, Stream, int)"/>).</summary>
    public static Bitmap Decode(byte[] bytes, int maxWidth = MaxDisplayWidth)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        return Decode(bytes.AsSpan(0, Math.Min(bytes.Length, 64 * 1024)), ms, maxWidth);
    }

    /// <summary>Decodes an image file for display on a background thread.</summary>
    public static Task<Bitmap> DecodeFileAsync(string path, int maxWidth = MaxDisplayWidth, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (info.Length > MaxDecodeBytes)
            {
                throw new InvalidDataException($"The image is {info.Length / 1048576.0:F0} MB; images over {MaxDecodeBytes / 1048576} MB are not opened.");
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            var header = new byte[(int)Math.Min(64 * 1024, info.Length)];
            int read = fs.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            fs.Position = 0;
            return Decode(header.AsSpan(0, read), fs, maxWidth);
        }, cancellationToken);
}
