using System.Reflection;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Provides display and inspection runtime support for Go scripts and notebooks in C# Code Studio,
/// emitting rich display MIME bundles for interactive visuals, charts, HTML, images, and tables.
/// </summary>
public static class GoDisplayRuntime
{
    private const string ResourcePrefix = "GoRuntime.";

    private static readonly SemaphoreSlim ModuleGate = new(1, 1);

    /// <summary>The files of the fry package (fry.go for the charts and tables, values.go for showing, keeping and sharing values), by name.</summary>
    private static IReadOnlyList<(string Name, string Content)> RuntimeFiles()
    {
        var assembly = typeof(GoDisplayRuntime).Assembly;
        var files = new List<(string, string)>();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            files.Add((resource[ResourcePrefix.Length..], reader.ReadToEnd()));
        }

        if (files.Count > 0) return files;

        // Fallback to the local source files if they are not compiled into the assembly
        var folder = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "Go", "Runtime");
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.go").Order(StringComparer.Ordinal).Select(f => (Path.GetFileName(f), File.ReadAllText(f))).ToList() : [];
    }

    private static async Task WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, ct).ConfigureAwait(false) == content) return;
        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The fry package as a module of its own, <c>fry</c> under <paramref name="goRoot"/>, that every notebook cell builds against
    /// (<c>replace fry =&gt; that folder</c>): written once and kept as it is while the engine's files don't change, so the build cache keeps the compiled package.
    /// </summary>
    public static async Task<string> EnsureSharedModuleAsync(string goRoot, CancellationToken ct = default)
    {
        var folder = Path.Combine(goRoot, "fry");
        await ModuleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(folder);
            var files = RuntimeFiles();
            await WriteIfChangedAsync(Path.Combine(folder, "go.mod"), "module fry\n\ngo 1.21\n", ct).ConfigureAwait(false);
            foreach (var (name, content) in files) await WriteIfChangedAsync(Path.Combine(folder, name), content, ct).ConfigureAwait(false);
            foreach (var stale in Directory.GetFiles(folder, "*.go").Where(f => files.All(file => file.Name != Path.GetFileName(f)))) File.Delete(stale);
        }
        finally
        {
            ModuleGate.Release();
        }

        return folder;
    }

    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var files = RuntimeFiles();

        // 1. Module package: buildDir/fry/*.go + go.mod
        var fryDir = Path.Combine(buildDir, "fry");
        Directory.CreateDirectory(fryDir);
        foreach (var (name, content) in files) await WriteIfChangedAsync(Path.Combine(fryDir, name), content, ct).ConfigureAwait(false);
        var fryMod = Path.Combine(fryDir, "go.mod");
        if (!File.Exists(fryMod))
        {
            await File.WriteAllTextAsync(fryMod, "module fry\n\ngo 1.21\n", ct).ConfigureAwait(false);
        }

        // 2. GOPATH package: buildDir/src/fry/fry.go
        var gopathDir = Path.Combine(buildDir, "src", "fry");
        Directory.CreateDirectory(gopathDir);
        foreach (var (name, content) in files) await WriteIfChangedAsync(Path.Combine(gopathDir, name), content, ct).ConfigureAwait(false);

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

        // 5. Package fry_display in GOPATH
        var fryDisplayGopath = Path.Combine(buildDir, "src", "fry_display");
        Directory.CreateDirectory(fryDisplayGopath);
        await File.WriteAllTextAsync(Path.Combine(fryDisplayGopath, "display.go"), """
            package fry_display

            import "fry"

            type ChartOptions struct {
                Type  string
                Title string
                X     []float64
                Y     []float64
            }

            func Chart(opts ChartOptions) *fry.DisplayHandle {
                if len(opts.Y) > 0 {
                    return fry.Chart(opts.Y, fry.Title(opts.Title))
                }
                return fry.Chart(opts.X, fry.Title(opts.Title))
            }
            """, ct).ConfigureAwait(false);

        return fryDir;
    }
}
