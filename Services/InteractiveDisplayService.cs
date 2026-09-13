using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public enum CellOutputKind
{
    Text,
    Image,
    Html,
    Control,
    Error,
    Table
}

public class RichCellOutput
{
    public CellOutputKind Kind { get; set; } = CellOutputKind.Text;
    public string Text { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public string? ImageFormat { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? HtmlContent { get; set; }
    public Control? InteractiveControl { get; set; }
    public DumpTableResult? TableResult { get; set; }
}

public static class InteractiveDisplayContext
{
    private static readonly AsyncLocal<Action<RichCellOutput>?> _currentOutputHandler = new();

    public static IDisposable EnterScope(Action<RichCellOutput> handler)
    {
        var prev = _currentOutputHandler.Value;
        _currentOutputHandler.Value = handler;
        return new Scope(() => _currentOutputHandler.Value = prev);
    }

    public static void Emit(RichCellOutput output)
    {
        _currentOutputHandler.Value?.Invoke(output);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        public Scope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}

public static class Display
{
    public static void Image(byte[] bytes, string format = "PNG")
    {
        int? width = null;
        int? height = null;

        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            width = (int)bmp.Size.Width;
            height = (int)bmp.Size.Height;
        }
        catch
        {
            // Ignore if metadata extraction fails
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

        if (typeName.Contains("SkiaSharp.SKBitmap") || typeName.Contains("SkiaSharp.SKImage"))
        {
            try
            {
                // SKImage.FromBitmap(...) or skImage.Encode()
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

    public static void Control(Control control)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Control,
            InteractiveControl = control
        });
    }

    public static void Table(DumpTableResult table)
    {
        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Table,
            TableResult = table
        });
    }
}

public static class DisplayExtensions
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
