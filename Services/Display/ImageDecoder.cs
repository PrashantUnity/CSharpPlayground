using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Turns image files and image bytes into bitmaps without stalling the studio. Sizes come from the file header (no
/// decode); decoding runs off the UI thread and never at more than <see cref="MaxDisplayWidth"/> pixels across (a
/// 24-megapixel photo decoded in full is ~100 MB and ~1 s of work on the UI thread, for a picture the screen shows at a
/// fraction of that).
/// </summary>
public static class ImageDecoder
{
    /// <summary>Images are decoded at most this wide for display (zooming past it shows the decoded pixels larger).</summary>
    public const int MaxDisplayWidth = 4096;

    /// <summary>Files larger than this are not decoded at all; the viewer says why instead.</summary>
    public const long MaxDecodeBytes = 256L * 1024 * 1024;

    /// <summary>Reads an image file's pixel size from its header (PNG, GIF, BMP, JPEG), without decoding it.</summary>
    public static bool TryReadDimensions(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[(int)Math.Min(64 * 1024, fs.Length)];
            int read = fs.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            return TryReadDimensions(header.AsSpan(0, read), out width, out height);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads an image's pixel size from the first bytes of its data (PNG, GIF, BMP, JPEG).</summary>
    public static bool TryReadDimensions(ReadOnlySpan<byte> header, out int width, out int height)
    {
        width = 0;
        height = 0;
        int read = header.Length;
        if (read < 10) return false;

        // PNG: 89 50 4E 47 0D 0A 1A 0A, then the IHDR chunk.
        if (read >= 24 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }

        // GIF87a / GIF89a.
        if (header[0] == 'G' && header[1] == 'I' && header[2] == 'F')
        {
            width = header[6] | (header[7] << 8);
            height = header[8] | (header[9] << 8);
            return width > 0 && height > 0;
        }

        // BMP.
        if (read >= 26 && header[0] == 'B' && header[1] == 'M')
        {
            width = header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24);
            height = Math.Abs(header[22] | (header[23] << 8) | (header[24] << 16) | (header[25] << 24));
            return width > 0 && height > 0;
        }

        // JPEG: walk the segments to the start-of-frame marker.
        if (header[0] == 0xFF && header[1] == 0xD8)
        {
            int pos = 2;
            while (pos + 4 < read)
            {
                if (header[pos] != 0xFF) { pos++; continue; }
                while (pos < read && header[pos] == 0xFF) pos++;
                if (pos >= read) break;

                byte marker = header[pos++];
                if (marker == 0xD9 || marker == 0xDA) break; // end of image, or start of scan
                if (pos + 2 > read) break;
                int chunkLen = (header[pos] << 8) | header[pos + 1];
                if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB)
                {
                    if (pos + 7 <= read)
                    {
                        height = (header[pos + 3] << 8) | header[pos + 4];
                        width = (header[pos + 5] << 8) | header[pos + 6];
                        return width > 0 && height > 0;
                    }
                }

                pos += chunkLen;
            }
        }

        return false;
    }

    /// <summary>
    /// Decodes <paramref name="data"/> for display: at most <paramref name="maxWidth"/> pixels across when the header
    /// says it is wider, in full otherwise. Safe on any thread.
    /// </summary>
    public static Bitmap Decode(ReadOnlySpan<byte> header, Stream data, int maxWidth = MaxDisplayWidth)
    {
        if (TryReadDimensions(header, out int width, out _) && width > maxWidth)
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
