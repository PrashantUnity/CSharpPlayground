using System.Collections;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using ScriptDisplay = PdfEditorApp.Plugins.CSharpEditor.Scripting.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public static partial class DisplayExtensions
{
    /// <summary>
    /// Shows <paramref name="obj"/> the best way it can be shown (a table, an image, a host control or its text) and hands
    /// it back, so <c>Dump()</c> can sit inside an expression.
    /// </summary>
    public static T Dump<T>(this T obj, string? label = null)
    {
        if (!string.IsNullOrEmpty(label))
        {
            Console.WriteLine($"=== {label} ===");
        }

        if (obj == null)
        {
            Console.WriteLine("<null>");
            ShowTable(null, label, reportFailure: false);
            return obj;
        }

        var host = ScriptHost.Profile.Display;
        if (host?.TryCreateElementOutput(obj) is { } element)
        {
            InteractiveDisplayContext.Emit(element);
            return obj;
        }

        if (host?.TryEncodeImage(obj) is { } hostPng)
        {
            ScriptDisplay.Image(hostPng);
            return obj;
        }

        if (SkiaImages.IsSkiaImage(obj))
        {
            ScriptDisplay.Image(obj);
            return obj;
        }

        if (obj is byte[] bytes && IsImageBytes(bytes))
        {
            ScriptDisplay.Image(bytes);
            return obj;
        }

        if (obj is string s)
        {
            if (s.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) ||
                (s.TrimStart().StartsWith('<') && s.Contains("<img") && s.TrimEnd().EndsWith('>')))
            {
                ScriptDisplay.Html(s);
                return obj;
            }
            Console.WriteLine(s);
            ShowTable(s, label, reportFailure: false);
            return obj;
        }

        // Generate interactive visual table result
        ShowTable(obj, label, reportFailure: true);

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
            Console.WriteLine(JsonSerializer.Serialize(obj, IndentedJson));
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            Console.WriteLine(obj.ToString());
        }

        return obj;
    }

    public static T DisplayObject<T>(this T obj)
    {
        return Dump(obj);
    }

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    // A value the table builder can't take apart is still printed as text; for an object the notice says why there is no table.
    private static void ShowTable(object? value, string? label, bool reportFailure)
    {
        try
        {
            ScriptDisplay.Table(DumpTableBuilder.Create(value, label));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (reportFailure) Console.WriteLine($"[Display] Notice: {ex.Message}");
        }
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
