using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public static partial class Display
{
    /// <summary>
    /// Displays an animated GIF in the interactive output cell.
    /// </summary>
    public static void Gif(byte[] gifBytes)
    {
        Image(gifBytes, "GIF");
    }

    /// <summary>
    /// Displays an animated GIF from a file path in the interactive output cell.
    /// </summary>
    public static void Gif(string filePath)
    {
        if (File.Exists(filePath))
        {
            var bytes = File.ReadAllBytes(filePath);
            Image(bytes, "GIF");
        }
    }

    /// <summary>
    /// Exports recorded algorithm visualizer steps directly to an animated GIF file.
    /// </summary>
    public static void SaveGif(
        string outputPath,
        VisualizerRecorder recorder,
        VisualizerGifExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        SaveGif(outputPath, recorder.Options, options);
    }

    /// <summary>
    /// Exports algorithm visualizer steps directly to an animated GIF file.
    /// </summary>
    public static void SaveGif(
        string outputPath,
        VisualizerOptions visualizerOptions,
        VisualizerGifExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(visualizerOptions);
        VisualizerGifExportService.ExportToFileAsync(visualizerOptions, outputPath, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Exports algorithm visualizer steps directly to an animated GIF file asynchronously.
    /// </summary>
    public static Task SaveGifAsync(
        string outputPath,
        VisualizerOptions visualizerOptions,
        VisualizerGifExportOptions? options = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visualizerOptions);
        return VisualizerGifExportService.ExportToFileAsync(visualizerOptions, outputPath, options, progress, cancellationToken);
    }

    /// <summary>
    /// Encodes a sequence of arbitrary GifFrames to an animated GIF file.
    /// </summary>
    public static void SaveGif(
        string outputPath,
        IReadOnlyList<GifFrame> frames,
        int loopCount = 0,
        bool dither = false)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (Path.GetDirectoryName(outputPath) is { } dir && !string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = File.Create(outputPath);
        AnimatedGifEncoder.Encode(frames, fs, loopCount, dither);
    }

    /// <summary>
    /// Records a procedural animation loop into an animated GIF file using Avalonia DrawingContext.
    /// </summary>
    public static void AnimateToGif(
        string outputPath,
        int frameCount,
        Action<int, DrawingContext> drawFrame,
        int width = 640,
        int height = 360,
        TimeSpan? frameDelay = null,
        bool loopInfinitely = true)
    {
        ArgumentNullException.ThrowIfNull(drawFrame);
        if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be greater than 0.");

        var delay = frameDelay ?? TimeSpan.FromMilliseconds(50); // 20 FPS default
        var frames = new List<GifFrame>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                drawFrame(i, ctx);
            }

            frames.Add(BitmapPixelReader.CreateFrame(rtb, delay));
        }

        SaveGif(outputPath, frames, loopCount: loopInfinitely ? 0 : -1);
    }
}
