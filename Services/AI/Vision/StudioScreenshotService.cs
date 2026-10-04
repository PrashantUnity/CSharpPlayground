using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Controls.AI;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Studio;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.AI.Vision;

/// <summary>
/// Service providing high-fidelity visual workspace capture with automatic AI self-exclusion.
/// Eliminates the 'AI photographing itself' problem by rendering specific visual subtrees
/// or using an ephemeral stealth shutter during full-window captures.
/// </summary>
public static class StudioScreenshotService
{
    /// <summary>
    /// Captures a screenshot of the specified IDE workspace area, automatically
    /// excluding the AI Assistant's own window/overlay from the capture.
    /// </summary>
    public static async Task<byte[]> CaptureAsync(
        CaptureTarget target = CaptureTarget.WorkspaceArea,
        bool excludeSelf = true,
        AiComposerViewModel? composerVm = null)
    {
        var topLevel = StudioAppContext.Instance.UI.ActiveTopLevel as TopLevel
                       ?? StudioAppContext.Instance.UI.MainWindow as TopLevel;

        if (topLevel == null)
        {
            // Fallback for headless test environments or when no active window is mounted:
            // generate a clean 1000x800 bitmap
            return CreateFallbackBitmapBytes(1000, 800);
        }

        byte[] DoCapture()
        {
            // Determine the target visual to render
            Control? targetVisual = ResolveTargetVisual(topLevel, target) ?? topLevel;

            // If we are targeting the full window and excludeSelf is true:
            // Use the Ephemeral Stealth Shutter to hide the AI overlay for 1 frame
            bool needsStealthShutter = excludeSelf && (target == CaptureTarget.FullWindow || targetVisual == topLevel);
            bool wasGhostHidden = composerVm?.IsGhostHidden ?? false;

            try
            {
                if (needsStealthShutter && composerVm != null)
                {
                    composerVm.IsGhostHidden = true;
                    // Flush pending layout jobs to ensure the overlay disappears before snapshot
                    if (Avalonia.Application.Current != null)
                    {
                        Dispatcher.UIThread.RunJobs();
                    }
                }

                int width = Math.Max(1, (int)targetVisual.Bounds.Width);
                int height = Math.Max(1, (int)targetVisual.Bounds.Height);

                // If visual hasn't completed initial layout or is 0-sized
                if (width <= 1 || height <= 1)
                {
                    width = (int)(targetVisual.DesiredSize.Width > 0 ? targetVisual.DesiredSize.Width : 1200);
                    height = (int)(targetVisual.DesiredSize.Height > 0 ? targetVisual.DesiredSize.Height : 800);
                }

                try
                {
                    var pixelSize = new PixelSize(width, height);
                    var dpi = new Vector(96, 96);

                    using var rtb = new RenderTargetBitmap(pixelSize, dpi);
                    rtb.Render(targetVisual);

                    using var ms = new MemoryStream();
                    rtb.Save(ms, new PngBitmapEncoderOptions());
                    return ms.ToArray();
                }
                catch
                {
                    return CreateFallbackBitmapBytes(width, height);
                }
            }
            finally
            {
                if (needsStealthShutter && composerVm != null)
                {
                    composerVm.IsGhostHidden = wasGhostHidden;
                    if (Avalonia.Application.Current != null)
                    {
                        Dispatcher.UIThread.RunJobs();
                    }
                }
            }
        }

        if (Avalonia.Application.Current != null && !Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(DoCapture);
        }

        return DoCapture();
    }

    /// <summary>
    /// Captures a screenshot to a file on disk (defaulting to the system temp directory).
    /// </summary>
    public static async Task<string> CaptureToFileAsync(
        string? outputPath = null,
        CaptureTarget target = CaptureTarget.WorkspaceArea,
        bool excludeSelf = true,
        AiComposerViewModel? composerVm = null)
    {
        var bytes = await CaptureAsync(target, excludeSelf, composerVm);

        string fileName = $"fry_vision_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.png";
        string filePath = outputPath ?? Path.Combine(Path.GetTempPath(), fileName);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
    }

    private static Control? ResolveTargetVisual(TopLevel root, CaptureTarget target)
    {
        switch (target)
        {
            case CaptureTarget.ActiveEditor:
                // Find active text editor or notebook cell canvas
                var editor = root.GetVisualDescendants()
                    .OfType<BindableTextEditor>()
                    .FirstOrDefault(e => e.IsEffectivelyVisible);
                if (editor != null) return editor;

                // Fallback to any AvaloniaEdit TextEditor
                var baseEditor = root.GetVisualDescendants()
                    .OfType<AvaloniaEdit.TextEditor>()
                    .FirstOrDefault(e => e.IsEffectivelyVisible);
                if (baseEditor != null) return baseEditor;

                // Fallback to KeepAlivePageHost
                return root.GetVisualDescendants().OfType<KeepAlivePageHost>().FirstOrDefault();

            case CaptureTarget.WorkspaceArea:
                // Primary PageHost (contains code studio, docs, notebooks, hub, but EXCLUDES floating AI overlays)
                var pageHost = root.GetVisualDescendants().OfType<KeepAlivePageHost>().FirstOrDefault();
                if (pageHost != null) return pageHost;
                break;

            case CaptureTarget.BottomPanel:
                var bottomDeck = root.GetVisualDescendants().OfType<StudioBottomDeckControl>().FirstOrDefault();
                if (bottomDeck != null) return bottomDeck;
                break;

            case CaptureTarget.FullWindow:
            default:
                return root;
        }

        return root;
    }

    private static readonly byte[] MinimalValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static byte[] CreateFallbackBitmapBytes(int width, int height)
    {
        try
        {
            using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            using var ms = new MemoryStream();
            rtb.Save(ms, new PngBitmapEncoderOptions());
            return ms.ToArray();
        }
        catch
        {
            // Fallback for headless environments without graphics/Skia initialization
            return MinimalValidPngBytes;
        }
    }
}
