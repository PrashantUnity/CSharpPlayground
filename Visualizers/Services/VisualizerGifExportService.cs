using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Options for exporting an algorithm visualizer sequence to an animated GIF.
/// </summary>
public sealed class VisualizerGifExportOptions
{
    public int StartStep { get; set; } = 0;
    public int? EndStep { get; set; }
    public TimeSpan? StepDelay { get; set; }
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 360;
    public bool IncludeBanner { get; set; } = true;
    public bool LoopInfinitely { get; set; } = true;
    public bool Dither { get; set; } = false;
}

/// <summary>
/// Service for rasterizing VisualizerSequence steps into optimized animated GIF files.
/// </summary>
public static class VisualizerGifExportService
{
    private static readonly IBrush CanvasBgBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27)); // Slate-900
    private static readonly IBrush BannerBgBrush = new SolidColorBrush(Color.FromArgb(230, 39, 39, 42)); // Zinc-800
    private static readonly IPen BannerBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 63, 63, 70)), 1); // Zinc-700
    private static readonly IBrush TextWhiteBrush = new SolidColorBrush(Color.FromRgb(244, 244, 245));
    private static readonly IBrush TextMutedBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Sky-400
    private static readonly Typeface BannerTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>
    /// Renders a single step frame of a visualizer into a GifFrame.
    /// Must be invoked on the UI thread or headless dispatcher.
    /// </summary>
    public static GifFrame RenderStepFrame(
        VisualizerOptions options,
        int stepIndex,
        int totalSteps,
        int width,
        int height,
        bool includeBanner,
        TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sequence = options.Sequence;
        if (sequence != null && sequence.HasSteps)
        {
            sequence.SeekStep(stepIndex);
        }

        var eff = options.CloneWithViewState(new VisualizerViewState(options));
        var step = sequence?.CurrentStep;

        int bannerHeight = includeBanner ? 34 : 0;
        int renderHeight = Math.Max(60, height);
        int renderWidth = Math.Max(60, width);

        using var rtb = new RenderTargetBitmap(new PixelSize(renderWidth, renderHeight), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            // 1. Fill Canvas Background
            ctx.FillRectangle(CanvasBgBrush, new Rect(0, 0, renderWidth, renderHeight));

            // 2. Render Visualizer Canvas Content
            var canvasRect = new Rect(8, 8, renderWidth - 16, renderHeight - bannerHeight - 16);
            if (canvasRect.Width > 0 && canvasRect.Height > 0)
            {
                var renderer = VisualizerRendererFactory.GetRenderer(eff.Kind);
                renderer.Render(ctx, canvasRect, eff);
            }

            // 3. Optional Step Banner Footer
            if (includeBanner)
            {
                DrawStepBanner(ctx, renderWidth, renderHeight, stepIndex, totalSteps, step);
            }
        }

        return BitmapPixelReader.CreateFrame(rtb, delay);
    }

    private static void DrawStepBanner(
        DrawingContext ctx,
        int width,
        int height,
        int stepIndex,
        int totalSteps,
        VisualizerStep? step)
    {
        var bannerRect = new Rect(8, height - 32, width - 16, 24);
        ctx.DrawRectangle(BannerBgBrush, BannerBorderPen, new RoundedRect(bannerRect, 4));

        string stepPrefix = $"Step {stepIndex + 1}/{totalSteps}";
        string desc = step?.Description ?? "Step";
        string bannerText = $"{stepPrefix}: {desc}";

        var textFt = new FormattedText(
            bannerText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            BannerTypeface,
            11,
            TextWhiteBrush)
        {
            MaxTextWidth = Math.Max(20, width - 90),
            MaxTextHeight = 16
        };

        ctx.DrawText(textFt, new Point(16, height - 28));

        // Draw Line Number Badge if available
        if (step?.SourceLine > 0)
        {
            string lineText = $"Ln {step.SourceLine}";
            var lineFt = new FormattedText(
                lineText,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BannerTypeface,
                10,
                AccentBrush);

            double badgeX = width - 24 - lineFt.Width;
            ctx.DrawText(lineFt, new Point(badgeX, height - 27));
        }
    }

    /// <summary>
    /// Exports all steps in the given visualizer sequence to an animated GIF byte array.
    /// </summary>
    public static async Task<byte[]> ExportToGifBytesAsync(
        VisualizerOptions options,
        VisualizerGifExportOptions? exportOptions = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = exportOptions ?? new VisualizerGifExportOptions();

        var sequence = options.Sequence ?? throw new InvalidOperationException("Visualizer has no recorded sequence.");
        int total = sequence.TotalSteps;
        if (total == 0)
            throw new InvalidOperationException("Visualizer sequence has no steps.");

        int start = Math.Clamp(opts.StartStep, 0, total - 1);
        int end = Math.Clamp(opts.EndStep ?? (total - 1), start, total - 1);
        int count = end - start + 1;

        TimeSpan delay = opts.StepDelay
            ?? TimeSpan.FromMilliseconds(Math.Max(100, (int)(500 / Math.Max(0.1, sequence.PlaybackSpeed))));

        // 1. Rasterize Frames
        var frames = new List<GifFrame>(count);
        for (int i = start; i <= end; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int currentStep = i;
            var frame = RenderStepFrame(options, currentStep, total, opts.Width, opts.Height, opts.IncludeBanner, delay);

            frames.Add(frame);
            progress?.Report((double)frames.Count / (count * 2)); // First half of progress is rendering
        }

        // 2. Encode to Animated GIF in Background
        var encodeProgress = progress != null
            ? new Progress<double>(p => progress.Report(0.5 + p * 0.5))
            : null;

        return await Task.Run(() =>
            AnimatedGifEncoder.EncodeToBytes(
                frames,
                loopCount: opts.LoopInfinitely ? 0 : -1,
                dither: opts.Dither,
                progress: encodeProgress,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports visualizer steps to an animated GIF file on disk.
    /// </summary>
    public static async Task ExportToFileAsync(
        VisualizerOptions options,
        string outputPath,
        VisualizerGifExportOptions? exportOptions = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes = await ExportToGifBytesAsync(options, exportOptions, progress, cancellationToken).ConfigureAwait(false);
        if (Path.GetDirectoryName(outputPath) is { } dir && !string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken).ConfigureAwait(false);
    }
}
