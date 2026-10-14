namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// The display crate every Rust script can use, with no setup: <c>fry::line_chart(&amp;d).title("T").show()</c>,
/// <c>fry::table(&amp;rows, "Title")</c>, <c>fry::dump!(value)</c>, <c>fry::html(..)</c> and <c>fry::image(..)</c>
/// print <c>__FRY_DISPLAY__</c> lines that <c>ExternalOutputProcessor</c> turns into visuals, tables, images and HTML.
/// </summary>
public static class RustDisplayRuntime
{
    private static string GetResourceContent(string filename)
    {
        var asm = typeof(RustDisplayRuntime).Assembly;
        using var stream = asm.GetManifestResourceStream($"RustRuntime.{filename}");
        if (stream != null)
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }

        var localSource = Path.Combine(AppContext.BaseDirectory, "Services", "Languages", "Rust", "Runtime", filename);
        if (File.Exists(localSource)) return File.ReadAllText(localSource);
        return "";
    }

    /// <summary>Writes the crate under <c>rustRoot/fry</c> (only when it changed) and returns its folder, or null if it can't be written.</summary>
    public static async Task<string?> EnsureCrateAsync(string rustRoot, CancellationToken ct = default)
    {
        var crateDir = Path.Combine(rustRoot, "fry");
        try
        {
            Directory.CreateDirectory(Path.Combine(crateDir, "src"));
            var cargoToml = GetResourceContent("Cargo.toml");
            var libRs = GetResourceContent("lib.rs");
            await WriteIfChangedAsync(Path.Combine(crateDir, "Cargo.toml"), cargoToml, ct).ConfigureAwait(false);
            await WriteIfChangedAsync(Path.Combine(crateDir, "src", "lib.rs"), libRs, ct).ConfigureAwait(false);
            return crateDir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // An unchanged file keeps its timestamp, so cargo doesn't rebuild the crate.
    private static async Task WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, ct).ConfigureAwait(false) == content) return;
        await File.WriteAllTextAsync(path, content, new System.Text.UTF8Encoding(false), ct).ConfigureAwait(false);
    }
}
