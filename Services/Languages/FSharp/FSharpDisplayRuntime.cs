namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Provides display and inspection runtime support for F# scripts and notebooks in C# Code Studio,
/// emitting rich display MIME bundles for interactive HTML, image, JSON, and table rendering in Results (.DUMP).
/// </summary>
public static class FSharpDisplayRuntime
{
    public static async Task<string> EnsureDisplayPackageAsync(string buildDir, CancellationToken ct = default)
    {
        var displayDir = Path.Combine(buildDir, "fry");
        try
        {
            Directory.CreateDirectory(displayDir);
            var file = Path.Combine(displayDir, "Display.fsx");
            await File.WriteAllTextAsync(file, DisplayFsxContent, ct).ConfigureAwait(false);
        }
        catch
        {
            // Gracefully ignore directory creation issues
        }

        return displayDir;
    }

    public const string DisplayFsxContent = """
        namespace Fry

        open System
        open System.IO
        open System.Text.Json

        module Display =
            let private emitProtocol (jsonBundle: string) =
                printfn "__FRY_DISPLAY__ %s" jsonBundle

            /// Html renders raw HTML in the C# Code Studio Results (.DUMP) deck.
            let Html (htmlContent: string) =
                let data = dict [ ("text/html", htmlContent) ]
                let metadata = dict []
                let bundle = dict [
                    ("type", box "display")
                    ("data", box data)
                    ("metadata", box metadata)
                ]
                emitProtocol (JsonSerializer.Serialize(bundle))

            /// Image displays a local image file path or Base64-encoded image data in the Results deck.
            let Image (pathOrBase64: string) =
                let mutable b64 = pathOrBase64
                let mutable mime = "image/png"

                if b64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) then
                    let idx = b64.IndexOf(',')
                    if idx <> -1 then
                        b64 <- b64.Substring(idx + 1)
                elif File.Exists(pathOrBase64) then
                    let bytes = File.ReadAllBytes(pathOrBase64)
                    b64 <- Convert.ToBase64String(bytes)
                    let lower = pathOrBase64.ToLowerInvariant()
                    if lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") then
                        mime <- "image/jpeg"
                    elif lower.EndsWith(".svg") then
                        mime <- "image/svg+xml"

                let data = dict [ (mime, b64) ]
                let metadata = dict []
                let bundle = dict [
                    ("type", box "display")
                    ("data", box data)
                    ("metadata", box metadata)
                ]
                emitProtocol (JsonSerializer.Serialize(bundle))

            /// Json displays structured JSON in the Results deck.
            let Json (jsonString: string) =
                let data = dict [ ("text/plain", jsonString) ]
                let metadata = dict []
                let bundle = dict [
                    ("type", box "display")
                    ("data", box data)
                    ("metadata", box metadata)
                ]
                emitProtocol (JsonSerializer.Serialize(bundle))

            /// Dump serializes any F# record, tuple, list, or object to JSON and renders it in the Results deck.
            let Dump (value: obj) =
                try
                    let options = JsonSerializerOptions(WriteIndented = true)
                    let json = JsonSerializer.Serialize(value, options)
                    Json json
                with _ ->
                    printfn "%A" value
        """;
}
