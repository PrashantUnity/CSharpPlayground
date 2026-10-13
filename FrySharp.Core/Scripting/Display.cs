using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Scripting;

/// <summary>
/// What a script shows besides its console output: images, HTML, tables, charts, 3D plots, visualizers and ECharts. Each
/// call emits one output to the cell or script that is running (<see cref="InteractiveDisplayContext"/>), whatever host
/// shows it. The desktop studio's <c>Display</c> derives from this class and adds what only it can show (live controls,
/// <c>Animate</c>), so a desktop script calls both through the same name.
/// </summary>
public partial class Display
{
    /// <summary>Only a host's own <c>Display</c> derives from this class; scripts call its members statically.</summary>
    protected Display()
    {
    }

    public static void Image(byte[] bytes, string format = "PNG")
    {
        // The size is in the header: decoding the whole image just to read it (and never disposing the bitmap) cost a
        // full decode and leaked native memory on every call, every frame of a Display.Image animation.
        InteractiveDisplayContext.Emit(RichCellOutput.ForImage(bytes, format));
    }

    public static void Image(string base64OrPath)
    {
        if (string.IsNullOrWhiteSpace(base64OrPath)) return;

        if (base64OrPath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            var commaIdx = base64OrPath.IndexOf(',');
            if (commaIdx >= 0)
            {
                var base64 = base64OrPath.Substring(commaIdx + 1);
                var bytes = Convert.FromBase64String(base64);
                Image(bytes, "PNG");
                return;
            }
        }

        if (File.Exists(base64OrPath))
        {
            var bytes = File.ReadAllBytes(base64OrPath);
            var ext = Path.GetExtension(base64OrPath).TrimStart('.').ToUpperInvariant();
            Image(bytes, string.IsNullOrEmpty(ext) ? "PNG" : ext);
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64OrPath);
            Image(bytes, "PNG");
        }
        catch (FormatException)
        {
            Console.WriteLine($"[Display] Could not decode image data: {base64OrPath}");
        }
    }

    /// <summary>
    /// Shows an image of any kind the host knows: PNG/JPEG/GIF bytes, a path or data URL, a SkiaSharp image, bitmap,
    /// surface or data, or the host's own image type (an Avalonia <c>Bitmap</c> on the desktop).
    /// </summary>
    public static void Image(object anyImage)
    {
        if (anyImage == null) return;

        if (anyImage is byte[] bytes)
        {
            Image(bytes);
            return;
        }

        if (anyImage is string str)
        {
            Image(str);
            return;
        }

        if (ScriptHost.Profile.Display?.TryEncodeImage(anyImage) is { } png)
        {
            Image(png, "PNG");
            return;
        }

        if (SkiaImages.TryEncodePng(anyImage) is { } skiaPng)
        {
            Image(skiaPng, "PNG");
            return;
        }

        Console.WriteLine($"[Display] Unsupported image type: {anyImage.GetType().FullName}");
    }

    /// <summary>Displays an animated GIF in the interactive output cell.</summary>
    public static void Gif(byte[] gifBytes)
    {
        Image(gifBytes, "GIF");
    }

    /// <summary>Displays an animated GIF from a file path in the interactive output cell.</summary>
    public static void Gif(string filePath)
    {
        if (File.Exists(filePath))
        {
            var bytes = File.ReadAllBytes(filePath);
            Image(bytes, "GIF");
        }
    }

    public static void Html(string html)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Html,
            HtmlContent = html
        });
    }

    /// <summary>Shows Markdown: rendered as Markdown where the host has a renderer (the browser studio, Jupyter), and as
    /// basic HTML (headers, bold, italic, inline code, links) in the desktop's HTML view.</summary>
    public static void Markdown(string markdown)
    {
        InteractiveDisplayContext.Emit(MarkdownOutput.Create(markdown ?? string.Empty));
    }

    /// <summary>The current cell/script execution's cancellation token (Stop button + timeout), for
    /// code that wants to cooperatively check it — e.g. inside a bounded frame-rendering loop. See
    /// ThrowIfCancellationRequested() for the common case.</summary>
    public static CancellationToken CancellationToken => InteractiveCancellationContext.Current;

    /// <summary>Throws OperationCanceledException if Stop was pressed or the execution timed out.
    /// Call this once per loop iteration in a frame-rendering loop so it can actually be interrupted —
    /// without it, Roslyn scripting only checks cancellation between whole cells, never inside one, so
    /// an unchecked loop runs to completion regardless of Stop/timeout.</summary>
    public static void ThrowIfCancellationRequested() =>
        InteractiveCancellationContext.Current.ThrowIfCancellationRequested();

    public static void Table(DumpTableResult table)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Table,
            TableResult = table
        });
    }

    public static void Table(object anyTableOrCollection, string? label = null)
    {
        if (anyTableOrCollection is DumpTableResult table)
        {
            Table(table);
            return;
        }

        var result = DumpTableBuilder.Create(anyTableOrCollection, label);
        Table(result);
    }

    public static void Inspector(ObjectInspectorNode node)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.ObjectInspector,
            InspectorNode = node
        });
    }
}
