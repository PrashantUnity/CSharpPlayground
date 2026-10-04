using System.IO;
using System.Reflection;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Provides display and inspection runtime support for F# scripts and notebooks in FrySharp,
/// emitting rich display MIME bundles for interactive visuals, charts, HTML, images, and tables in Results (.DUMP).
/// </summary>
public static class FSharpDisplayRuntime
{
    public static string DisplayFsxContent => GetDisplayFsxContent();

    private static string GetDisplayFsxContent()
    {
        var asm = typeof(FSharpDisplayRuntime).Assembly;
        using var stream = asm.GetManifestResourceStream("FSharpRuntime.Display.fsx");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Fallback to local source file
        var localSource = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "FSharp", "Runtime", "Display.fsx");
        if (File.Exists(localSource)) return File.ReadAllText(localSource);

        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "Services", "Languages", "FSharp", "Runtime", "Display.fsx");
        if (File.Exists(candidate)) return File.ReadAllText(candidate);

        return "";
    }

    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var displayDir = Path.Combine(buildDir, "fry");
        try
        {
            Directory.CreateDirectory(displayDir);
            var content = GetDisplayFsxContent();
            var file1 = Path.Combine(displayDir, "Display.fsx");
            var file2 = Path.Combine(buildDir, "Display.fsx");
            await File.WriteAllTextAsync(file1, content, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(file2, content, ct).ConfigureAwait(false);
        }
        catch
        {
            // Gracefully ignore directory creation issues
        }

        return displayDir;
    }
}
