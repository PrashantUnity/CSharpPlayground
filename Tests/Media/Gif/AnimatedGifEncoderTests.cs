using System;
using System.IO;
using System.Text;
using Avalonia.Headless;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;
using Xunit;

namespace CSharpEditorPlugin.Tests.Media.Gif;

public class AnimatedGifEncoderTests
{
    [Fact]
    public void EncodeToBytes_SingleFrame_ProducesValidGif89aHeaderAndDimensions()
    {
        int width = 40;
        int height = 30;
        byte[] bgraPixels = new byte[width * height * 4];

        // Draw solid red rectangle
        for (int i = 0; i < width * height; i++)
        {
            bgraPixels[i * 4 + 0] = 0;   // B
            bgraPixels[i * 4 + 1] = 0;   // G
            bgraPixels[i * 4 + 2] = 255; // R
            bgraPixels[i * 4 + 3] = 255; // A
        }

        var frame = new GifFrame(bgraPixels, width, height, TimeSpan.FromMilliseconds(200));
        byte[] gifBytes = AnimatedGifEncoder.EncodeToBytes([frame]);

        Assert.NotEmpty(gifBytes);
        string header = Encoding.ASCII.GetString(gifBytes, 0, 6);
        Assert.Equal("GIF89a", header);

        // Verify ImageDecoder reads matching dimensions
        Assert.True(ImageDecoder.TryReadDimensions(gifBytes, out int readW, out int readH));
        Assert.Equal(width, readW);
        Assert.Equal(height, readH);

        // Verify trailer byte
        Assert.Equal(0x3B, gifBytes[^1]);
    }

    [Fact]
    public void EncodeToBytes_MultipleFrames_ContainsNetscapeExtensionAndLoop()
    {
        int width = 32;
        int height = 32;

        var frames = new List<GifFrame>();
        for (int frameIdx = 0; frameIdx < 3; frameIdx++)
        {
            byte[] pixels = new byte[width * height * 4];
            byte colorVal = (byte)(frameIdx * 80);
            for (int i = 0; i < width * height; i++)
            {
                pixels[i * 4 + 0] = colorVal;
                pixels[i * 4 + 1] = colorVal;
                pixels[i * 4 + 2] = 255;
                pixels[i * 4 + 3] = 255;
            }
            frames.Add(new GifFrame(pixels, width, height, TimeSpan.FromMilliseconds(300)));
        }

        byte[] gifBytes = AnimatedGifEncoder.EncodeToBytes(frames, loopCount: 0);

        string ascii = Encoding.ASCII.GetString(gifBytes);
        Assert.Contains("NETSCAPE2.0", ascii);
        Assert.True(ImageDecoder.TryReadDimensions(gifBytes, out int w, out int h));
        Assert.Equal(width, w);
        Assert.Equal(height, h);
    }

    [Fact]
    public void OctreeColorQuantizer_PreservesTransparency()
    {
        int width = 10;
        int height = 10;
        byte[] pixels = new byte[width * height * 4];

        // Half opaque red, half transparent
        for (int i = 0; i < 50; i++)
        {
            pixels[i * 4 + 0] = 0;
            pixels[i * 4 + 1] = 0;
            pixels[i * 4 + 2] = 255;
            pixels[i * 4 + 3] = 255;
        }
        for (int i = 50; i < 100; i++)
        {
            pixels[i * 4 + 3] = 0; // Alpha = 0
        }

        var frame = new GifFrame(pixels, width, height, TimeSpan.FromMilliseconds(100), HasTransparency: true);
        var quantized = OctreeColorQuantizer.Quantize(frame);

        Assert.NotNull(quantized.TransparentIndex);
        Assert.Equal(0, quantized.TransparentIndex.Value);
        Assert.Equal(100, quantized.IndexedPixels.Length);
        Assert.Equal(768, quantized.PaletteRgb.Length);
    }

    private static void EnsureAvaloniaInitialized() =>
        TestHeadlessApp.EnsureInitialized();

    [Fact]
    public void RenderTargetBitmap_CanExtractPixels()
    {
        EnsureAvaloniaInitialized();
        using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(10, 10));
        var border = new Avalonia.Controls.Border
        {
            Width = 10,
            Height = 10,
            Background = Avalonia.Media.Brushes.Blue
        };
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.FillRectangle(Avalonia.Media.Brushes.Green, new Avalonia.Rect(0, 0, 10, 10));
        }

        byte[] buffer = BitmapPixelReader.ReadPixels(rtb);
        Assert.NotEmpty(buffer);

        var frame = new GifFrame(buffer, 10, 10, TimeSpan.FromMilliseconds(100));
        var bytes = AnimatedGifEncoder.EncodeToBytes([frame]);
        Assert.NotEmpty(bytes);
    }
}


