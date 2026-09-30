using System.Reflection;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Provides display and inspection runtime support for Go scripts and notebooks in C# Code Studio,
/// emitting rich display MIME bundles for interactive visuals, charts, HTML, images, and tables.
/// </summary>
public static class GoDisplayRuntime
{
    private static string GetFryGoContent()
    {
        var asm = typeof(GoDisplayRuntime).Assembly;
        using var stream = asm.GetManifestResourceStream("GoRuntime.fry.go");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Fallback to local source file if not yet compiled into assembly
        var localSource = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "Go", "Runtime", "fry.go");
        if (File.Exists(localSource)) return File.ReadAllText(localSource);
        return "";
    }

    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var content = GetFryGoContent();

        // 1. Module package: buildDir/fry/fry.go + go.mod
        var fryDir = Path.Combine(buildDir, "fry");
        Directory.CreateDirectory(fryDir);
        await File.WriteAllTextAsync(Path.Combine(fryDir, "fry.go"), content, ct).ConfigureAwait(false);
        var fryMod = Path.Combine(fryDir, "go.mod");
        if (!File.Exists(fryMod))
        {
            await File.WriteAllTextAsync(fryMod, "module fry\n\ngo 1.20\n", ct).ConfigureAwait(false);
        }

        // 2. GOPATH package: buildDir/src/fry/fry.go
        var gopathDir = Path.Combine(buildDir, "src", "fry");
        Directory.CreateDirectory(gopathDir);
        await File.WriteAllTextAsync(Path.Combine(gopathDir, "fry.go"), content, ct).ConfigureAwait(false);

        // 3. Local root go.mod: if cell.go or main.go runs directly in buildDir
        var rootMod = Path.Combine(buildDir, "go.mod");
        if (!File.Exists(rootMod))
        {
            await File.WriteAllTextAsync(rootMod, "module cell\n\ngo 1.20\n\nrequire fry v0.0.0\nreplace fry => ./fry\n", ct).ConfigureAwait(false);
        }

        // 4. Backwards compatibility: buildDir/fry/display/display.go
        var displayDir = Path.Combine(buildDir, "fry", "display");
        Directory.CreateDirectory(displayDir);
        await File.WriteAllTextAsync(Path.Combine(displayDir, "display.go"), """
            package display

            import "fry"

            func Html(c string) { fry.Html(c) }
            func Image(p any) { fry.Image(p) }
            func Json(j string) { fry.Json(j) }
            func Dump(v any) { fry.Dump(v) }
            """, ct).ConfigureAwait(false);

        return fryDir;
    }
}
