using System.IO;
using System.Reflection;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Provides zero-configuration runtime support for C++ scripts and algorithms in FrySharp,
/// staging <c>&lt;fry/display.hpp&gt;</c> and <c>"display.hpp"</c> into the compiler include path.
/// User scripts can call canonical visuals (<c>fry::line_chart</c>, <c>fry::tree</c>, etc.),
/// tables, HTML, images, and handles with interactive Results (.DUMP) deck rendering.
/// </summary>
public static class CppDisplayRuntime
{
    private const string ResourcePrefix = "CppRuntime.";

    private static readonly SemaphoreSlim IncludeGate = new(1, 1);

    public static string DisplayHppContent => RuntimeFiles().FirstOrDefault(f => f.Name == "display.hpp").Content ?? string.Empty;

    /// <summary>The headers of the runtime (display.hpp for the charts, tables and visuals, values.hpp for showing, keeping and sharing values), by name.</summary>
    private static IReadOnlyList<(string Name, string Content)> RuntimeFiles()
    {
        var assembly = typeof(CppDisplayRuntime).Assembly;
        var files = new List<(string, string)>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            files.Add((resource[ResourcePrefix.Length..], reader.ReadToEnd()));
        }

        if (files.Count > 0) return files;

        // Fallback to the local source files if they are not compiled into the assembly
        var folder = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "Cpp", "Runtime");
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.hpp").Order(StringComparer.Ordinal).Select(f => (Path.GetFileName(f), File.ReadAllText(f))).ToList() : [];
    }

    private static async Task WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, ct).ConfigureAwait(false) == content) return;
        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Stages the headers as <c>&lt;fry/display.hpp&gt;</c> and <c>&lt;fry/values.hpp&gt;</c> (and <c>"display.hpp"</c>, <c>"fry_display.hpp"</c> for what was written
    /// against them) in an <c>include</c> folder under <paramref name="buildDir"/>, and returns it. Written only when a header changed.
    /// </summary>
    public static async Task<string> EnsureIncludeDirectoryAsync(string buildDir, CancellationToken ct = default)
    {
        var includeDir = Path.Combine(buildDir, "include");
        var fryIncludeDir = Path.Combine(includeDir, "fry");

        await IncludeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(fryIncludeDir);
            foreach (var (name, content) in RuntimeFiles())
            {
                await WriteIfChangedAsync(Path.Combine(fryIncludeDir, name), content, ct).ConfigureAwait(false);
                if (name == "display.hpp")
                {
                    await WriteIfChangedAsync(Path.Combine(includeDir, "display.hpp"), content, ct).ConfigureAwait(false);
                    await WriteIfChangedAsync(Path.Combine(includeDir, "fry_display.hpp"), content, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Gracefully ignore filesystem contention
        }
        finally
        {
            IncludeGate.Release();
        }

        return includeDir;
    }
}
