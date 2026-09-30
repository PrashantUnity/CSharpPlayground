using System.IO;
using System.Reflection;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Provides zero-configuration runtime support for C++ scripts and algorithms in C# Code Studio,
/// staging <c>&lt;fry/display.hpp&gt;</c> and <c>"display.hpp"</c> into the compiler include path.
/// User scripts can call canonical visuals (<c>fry::line_chart</c>, <c>fry::tree</c>, etc.),
/// tables, HTML, images, and handles with interactive Results (.DUMP) deck rendering.
/// </summary>
public static class CppDisplayRuntime
{
    public static string DisplayHppContent => GetDisplayHppContent();

    private static string GetDisplayHppContent()
    {
        var asm = typeof(CppDisplayRuntime).Assembly;
        using var stream = asm.GetManifestResourceStream("CppRuntime.display.hpp");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Fallback to local source file if not yet compiled into assembly
        var localSource = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "Cpp", "Runtime", "display.hpp");
        if (File.Exists(localSource)) return File.ReadAllText(localSource);

        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "Services", "Languages", "Cpp", "Runtime", "display.hpp");
        if (File.Exists(candidate)) return File.ReadAllText(candidate);

        return "";
    }

    public static async Task<string> EnsureIncludeDirectoryAsync(string buildDir, CancellationToken ct = default)
    {
        var includeDir = Path.Combine(buildDir, "include");
        var fryIncludeDir = Path.Combine(includeDir, "fry");

        try
        {
            Directory.CreateDirectory(fryIncludeDir);

            var header1 = Path.Combine(fryIncludeDir, "display.hpp");
            var header2 = Path.Combine(includeDir, "display.hpp");

            var content = GetDisplayHppContent();
            await File.WriteAllTextAsync(header1, content, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(header2, content, ct).ConfigureAwait(false);
        }
        catch
        {
            // Gracefully ignore filesystem contention
        }

        return includeDir;
    }
}
