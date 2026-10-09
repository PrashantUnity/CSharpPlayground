using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;

/// <summary>
/// High-performance, pure .NET 10 GIF89a multi-frame encoder.
/// Produces valid, standard animated GIFs with Netscape 2.0 loop controls, per-frame palettes, and LZW compression.
/// </summary>
public sealed class AnimatedGifEncoder
{
    private static readonly byte[] HeaderGif89a = Encoding.ASCII.GetBytes("GIF89a");
    private static readonly byte[] NetscapeExtensionHeader =
    [
        0x21, 0xFF, 0x0B, // Extension Introducer, App Extension, length 11
        0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, // "NETSCAPE2.0"
        0x03, 0x01 // Sub-block length 3, sub-block ID 1
    ];

    public static byte[] EncodeToBytes(
        IReadOnlyList<GifFrame> frames,
        int loopCount = 0,
        bool dither = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        Encode(frames, ms, loopCount, dither, progress, cancellationToken);
        return ms.ToArray();
    }

    public static void Encode(
        IReadOnlyList<GifFrame> frames,
        Stream stream,
        int loopCount = 0,
        bool dither = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(stream);
        if (frames.Count == 0)
            throw new ArgumentException("At least one frame is required for GIF encoding.", nameof(frames));

        int width = frames[0].Width;
        int height = frames[0].Height;

        // 1. Write GIF89a Header
        stream.Write(HeaderGif89a, 0, HeaderGif89a.Length);

        // Quantize first frame early to use as Global Color Table
        var firstQuantized = OctreeColorQuantizer.Quantize(frames[0], dither);

        // 2. Write Logical Screen Descriptor (LSD)
        WriteShort(stream, width);
        WriteShort(stream, height);
        // Packed Fields: GCT Flag = 1, Color Res = 7 (8 bits), Sort = 0, GCT Size = 7 (256 colors)
        stream.WriteByte(0xF7);
        stream.WriteByte(0x00); // Background color index
        stream.WriteByte(0x00); // Pixel aspect ratio

        // 3. Write Global Color Table (from first frame)
        stream.Write(firstQuantized.PaletteRgb, 0, firstQuantized.PaletteRgb.Length);

        // 4. Write Netscape 2.0 Looping Application Extension
        if (frames.Count > 1 || loopCount >= 0)
        {
            stream.Write(NetscapeExtensionHeader, 0, NetscapeExtensionHeader.Length);
            WriteShort(stream, loopCount); // 0 = infinite loop
            stream.WriteByte(0x00); // Block Terminator
        }

        // 5. Write Each Frame
        for (int i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = frames[i];
            var quantized = i == 0 ? firstQuantized : OctreeColorQuantizer.Quantize(frame, dither);
            WriteFrame(stream, frame, quantized);
            progress?.Report((double)(i + 1) / frames.Count);
        }

        // 6. Write GIF Trailer (0x3B)
        stream.WriteByte(0x3B);
        stream.Flush();
    }

    public static async Task EncodeAsync(
        IReadOnlyList<GifFrame> frames,
        Stream stream,
        int loopCount = 0,
        bool dither = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Encode(frames, stream, loopCount, dither, progress, cancellationToken), cancellationToken);
    }

    private static void WriteFrame(Stream stream, GifFrame frame, QuantizedFrame quantized)
    {
        // Graphic Control Extension (GCE)
        stream.WriteByte(0x21); // Extension Introducer
        stream.WriteByte(0xF9); // Graphic Control Label
        stream.WriteByte(0x04); // Block Size (4 bytes)

        bool hasTrans = quantized.TransparentIndex.HasValue;
        // Disposal Method: 2 = Restore to Background, 1 = Do Not Dispose
        // Packed: (Disposal << 2) | (Transparency ? 1 : 0)
        byte disposal = 0x02; // Restore to background
        byte packedGce = (byte)((disposal << 2) | (hasTrans ? 0x01 : 0x00));
        stream.WriteByte(packedGce);

        WriteShort(stream, frame.DelayCentiseconds);
        stream.WriteByte((byte)(quantized.TransparentIndex ?? 0));
        stream.WriteByte(0x00); // Block Terminator

        // Image Descriptor
        stream.WriteByte(0x2C); // Image Separator
        WriteShort(stream, 0); // Image Left
        WriteShort(stream, 0); // Image Top
        WriteShort(stream, frame.Width);
        WriteShort(stream, frame.Height);

        // Packed: Local Color Table Flag (0x80) | Size of LCT (0x07 = 256 colors)
        stream.WriteByte(0x87);

        // Local Color Table
        stream.Write(quantized.PaletteRgb, 0, quantized.PaletteRgb.Length);

        // LZW Compressed Pixel Data
        var lzw = new LzwEncoder(frame.Width, frame.Height, quantized.IndexedPixels, 8);
        lzw.Encode(stream);
    }

    private static void WriteShort(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
    }
}
