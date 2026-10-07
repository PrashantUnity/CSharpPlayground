using System.Collections;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public static partial class Display
{
    public static void Image(byte[] bytes, string format = "PNG")
    {
        int? width = null;
        int? height = null;

        // The size is in the header: decoding the whole image just to read it (and never disposing the bitmap) cost a
        // full decode and leaked native memory on every call, every frame of a Display.Image animation.
        if (ImageDecoder.TryReadDimensions(bytes.AsSpan(0, Math.Min(bytes.Length, 64 * 1024)), out int w, out int h))
        {
            width = w;
            height = h;
        }

        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Image,
            ImageBytes = bytes,
            ImageFormat = format,
            ImageWidth = width,
            ImageHeight = height
        });
    }

    public static void Image(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
#pragma warning disable CS0618
        bitmap.Save(ms);
#pragma warning restore CS0618
        Image(ms.ToArray(), "PNG");
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
        catch
        {
            Console.WriteLine($"[Display] Could not decode image data: {base64OrPath}");
        }
    }

    public static void Image(object anyImage)
    {
        if (anyImage == null) return;

        if (anyImage is byte[] bytes)
        {
            Image(bytes);
            return;
        }

        if (anyImage is Bitmap avBmp)
        {
            Image(avBmp);
            return;
        }

        if (anyImage is string str)
        {
            Image(str);
            return;
        }

        // Handle SkiaSharp SKBitmap, SKImage, SKData, SKSurface via reflection
        var typeName = anyImage.GetType().FullName ?? string.Empty;

        if (typeName.Contains("SkiaSharp.SKBitmap"))
        {
            try
            {
                // 1. Try SKImage.FromBitmap(skBitmap) -> skImage.Encode()
                var skImageType = anyImage.GetType().Assembly.GetType("SkiaSharp.SKImage");
                var fromBitmapMethod = skImageType?.GetMethod("FromBitmap", new[] { anyImage.GetType() })
                    ?? skImageType?.GetMethods().FirstOrDefault(m => m.Name == "FromBitmap" && m.GetParameters().Length == 1);
                if (fromBitmapMethod?.Invoke(null, new[] { anyImage }) is object skImage)
                {
                    Image(skImage);
                    return;
                }

                // 2. Try skBitmap.Encode(SKEncodedImageFormat.Png, 100)
                var formatEnum = anyImage.GetType().Assembly.GetType("SkiaSharp.SKEncodedImageFormat");
                if (formatEnum != null && Enum.TryParse(formatEnum, "Png", out var pngVal))
                {
                    var formatEncodeMethod = anyImage.GetType().GetMethod("Encode", new[] { formatEnum, typeof(int) });
                    if (formatEncodeMethod?.Invoke(anyImage, new[] { pngVal, 100 }) is object dataObj)
                    {
                        var toArrayMethod = dataObj.GetType().GetMethod("ToArray");
                        if (toArrayMethod?.Invoke(dataObj, null) is byte[] skBytes)
                        {
                            Image(skBytes, "PNG");
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }
        else if (typeName.Contains("SkiaSharp.SKImage"))
        {
            try
            {
                var encodeMethod = anyImage.GetType().GetMethod("Encode", Type.EmptyTypes);
                if (encodeMethod != null)
                {
                    var data = encodeMethod.Invoke(anyImage, null);
                    if (data != null)
                    {
                        var toArrayMethod = data.GetType().GetMethod("ToArray");
                        if (toArrayMethod?.Invoke(data, null) is byte[] skBytes)
                        {
                            Image(skBytes, "PNG");
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }
        else if (typeName.Contains("SkiaSharp.SKSurface"))
        {
            try
            {
                var snapshotMethod = anyImage.GetType().GetMethod("Snapshot");
                if (snapshotMethod?.Invoke(anyImage, null) is object snapshot)
                {
                    Image(snapshot);
                    return;
                }
            }
            catch
            {
                // Fallback
            }
        }
        else if (typeName.Contains("SkiaSharp.SKData"))
        {
            try
            {
                var toArrayMethod = anyImage.GetType().GetMethod("ToArray");
                if (toArrayMethod?.Invoke(anyImage, null) is byte[] skBytes)
                {
                    Image(skBytes, "PNG");
                    return;
                }
            }
            catch
            {
                // Fallback
            }
        }

        Console.WriteLine($"[Display] Unsupported image type: {typeName}");
    }

    public static void Html(string html)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Html,
            HtmlContent = html
        });
    }

    /// <summary>Renders a small common subset of Markdown (headers, bold/italic, inline code, links)
    /// through the existing Html rich-output path — notebooks already have a Markdown cell type and an
    /// Html renderer, so this reuses both rather than introducing a new output kind.</summary>
    public static void Markdown(string markdown)
    {
        Html(ConvertMarkdownToHtml(markdown ?? string.Empty));
    }

    private static string ConvertMarkdownToHtml(string markdown)
    {
        // Encode the raw text first so injected tags below are the only real HTML — user-typed
        // Markdown must never be able to smuggle a <script> tag into the rendered output.
        var html = WebUtility.HtmlEncode(markdown);

        html = Regex.Replace(html, @"^###### (.*)$", "<h6>$1</h6>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^##### (.*)$", "<h5>$1</h5>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^#### (.*)$", "<h4>$1</h4>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^### (.*)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.*)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.*)$", "<h1>$1</h1>", RegexOptions.Multiline);

        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<b>$1</b>");
        html = Regex.Replace(html, @"\*(.+?)\*", "<i>$1</i>");
        html = Regex.Replace(html, @"`(.+?)`", "<code>$1</code>");
        html = Regex.Replace(html, @"\[(.+?)\]\((.+?)\)", "<a href=\"$2\">$1</a>");

        return html.Replace("\r\n", "\n").Replace("\n", "<br/>");
    }

    public static void Control(Control control)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Control,
            InteractiveControl = control
        });
    }

    /// <summary>
    /// Primary way to display a live, self-animating visual: hides the DispatcherTimer + repaint
    /// boilerplate behind a single call. <paramref name="onFrame"/> is invoked every frame with the
    /// control's DrawingContext and elapsed time — draw directly into it (immediate-mode, like Avalonia
    /// controls already draw themselves), no per-frame allocation needed. The returned control keeps
    /// animating live because it's the same object reference bound into the cell's output area, driven
    /// by Avalonia's own compositor — it is not re-created or re-serialized on every frame.
    /// Prefer this over a `for` loop calling Display.Image(...) repeatedly: it doesn't hold any
    /// execution lock while animating (the cell's script returns almost immediately after this call),
    /// so it never blocks other tabs, and it keeps animating for as long as you like without needing
    /// cooperative-cancellation checks. See Display.ThrowIfCancellationRequested() for the frame-loop
    /// alternative when you specifically want a bounded, finite sequence of frames instead.
    /// </summary>
    public static AnimatedRenderControl Animate(
        Action<DrawingContext, TimeSpan> onFrame,
        TimeSpan? interval = null,
        double width = 400,
        double height = 300)
    {
        var control = new AnimatedRenderControl(onFrame, interval, width, height);
        Control(control);
        return control;
    }

    /// <summary>The current cell/script execution's cancellation token (Stop button + timeout), for
    /// code that wants to cooperatively check it — e.g. inside a bounded frame-rendering loop. See
    /// ThrowIfCancellationRequested() for the common case, and prefer Display.Animate(...) instead of a
    /// loop wherever the visual doesn't need to be a fixed, finite number of frames.</summary>
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

public static partial class DisplayExtensions
{
    public static T Dump<T>(this T obj, string? label = null)
    {
        if (!string.IsNullOrEmpty(label))
        {
            Console.WriteLine($"=== {label} ===");
        }

        if (obj == null)
        {
            Console.WriteLine("<null>");
            try
            {
                var table = DumpTableBuilder.Create(null, label);
                Display.Table(table);
            }
            catch { }
            return obj;
        }

        if (obj is Control control)
        {
            Display.Control(control);
            return obj;
        }

        var typeName = obj.GetType().FullName ?? string.Empty;
        if (typeName.Contains("SkiaSharp") || obj is Bitmap)
        {
            Display.Image(obj);
            return obj;
        }

        if (obj is byte[] bytes && IsImageBytes(bytes))
        {
            Display.Image(bytes);
            return obj;
        }

        if (obj is string s)
        {
            if (s.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) ||
                (s.TrimStart().StartsWith("<") && s.Contains("<img") && s.TrimEnd().EndsWith(">")))
            {
                Display.Html(s);
                return obj;
            }
            Console.WriteLine(s);
            try
            {
                var table = DumpTableBuilder.Create(s, label);
                Display.Table(table);
            }
            catch { }
            return obj;
        }

        // Generate interactive visual table result
        try
        {
            var table = DumpTableBuilder.Create(obj, label);
            Display.Table(table);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Display] Notice: {ex.Message}");
        }

        // Print console representation
        if (obj is IDictionary dict)
        {
            Console.WriteLine($"[Dictionary: {dict.Count} entries]");
            foreach (DictionaryEntry entry in dict)
            {
                Console.WriteLine($"  {entry.Key} => {entry.Value}");
            }
            return obj;
        }

        if (obj is IEnumerable enumerable)
        {
            int index = 0;
            Console.WriteLine($"[{DumpTableBuilder.GetFriendlyTypeName(obj.GetType())}]");
            foreach (var item in enumerable)
            {
                Console.WriteLine($"  [{index++}] {item}");
            }
            return obj;
        }

        try
        {
            Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            Console.WriteLine(obj.ToString());
        }

        return obj;
    }

    public static T DisplayObject<T>(this T obj)
    {
        return Dump(obj);
    }

    private static bool IsImageBytes(byte[] bytes)
    {
        if (bytes.Length < 8) return false;
        // PNG magic bytes: 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        // JPEG magic bytes: 0xFF, 0xD8, 0xFF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
        // GIF magic bytes: 'G', 'I', 'F', '8'
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return true;
        // WEBP: RIFF....WEBP
        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true;

        return false;
    }
}
