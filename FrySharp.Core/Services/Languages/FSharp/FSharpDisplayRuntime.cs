using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// The display runtime of F# scripts: <c>Display.fsx</c> (<c>Display.table</c>, <c>Display.lineChart</c>, <c>Display.markdown</c>
/// ...) and the <c>Values.fsx</c> it loads, which says how a value is shown. A script run loads them (<c>--load</c>); the
/// notebook kernel loads them into its session. They are embedded in this assembly and written to the folder a run uses.
/// </summary>
public static class FSharpDisplayRuntime
{
    /// <summary>The embedded resources that make up the runtime ("FSharpRuntime.Display.fsx" ...).</summary>
    public const string ResourcePrefix = "FSharpRuntime.";

    /// <summary>The files of the runtime: name and text.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = ReadFiles();

    /// <summary>The text of <c>Display.fsx</c>.</summary>
    public static string DisplayFsxContent => Files.GetValueOrDefault("Display.fsx", string.Empty);

    private static Dictionary<string, string> ReadFiles()
    {
        var assembly = typeof(FSharpDisplayRuntime).Assembly;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            files[resource[ResourcePrefix.Length..]] = reader.ReadToEnd();
        }

        return files;
    }

    /// <summary>
    /// Writes the runtime into <paramref name="buildDir"/> and <c>buildDir/fry</c>, where a script's <c>--load</c> and
    /// <c>--lib</c> find it, and returns the second.
    /// </summary>
    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var displayDir = Path.Combine(buildDir, "fry");
        try
        {
            Directory.CreateDirectory(displayDir);
            foreach (var (name, content) in Files)
            {
                await File.WriteAllTextAsync(Path.Combine(displayDir, name), content, ct).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(buildDir, name), content, ct).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // A folder that can't be written fails the run with its own message.
        }

        return displayDir;
    }
}
