using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Provides zero-configuration runtime support for Node.js scripts in FrySharp,
/// injecting <c>dump()</c>, <c>display()</c>, and <c>Display</c> into JavaScript scripts.
/// </summary>
public static class JavaScriptDisplayRuntime
{
    public static async Task<string> EnsureRuntimeFilesAsync(IHostEnvironment host, CancellationToken ct = default)
    {
        var folder = EmbeddedKernelFiles.Extract(typeof(JavaScriptDisplayRuntime).Assembly, "JavaScriptRuntime.",
            Path.Combine(Path.GetTempPath(), "FryStudio", "js_runtime"));

        // Use __dirname-relative require so the preload works regardless of the script's CWD,
        // and surface errors instead of silently swallowing them (which left Display undefined).
        var preload = Path.Combine(folder, "preload.js");
        await File.WriteAllTextAsync(preload, """
            const path = require('path');
            const fry = require(path.join(__dirname, 'fry'));
            globalThis.Display = fry.Display;
            globalThis.dump = fry.dump;
            globalThis.display = fry.display;
            """, ct);

        var pkg = Path.Combine(folder, "package.json");
        if (!File.Exists(pkg)) await File.WriteAllTextAsync(pkg, """
            {
                "name": "fry",
                "version": "1.0.0",
                "main": "fry.js"
            }
            """, ct);

        var fryDisplay = Path.Combine(folder, "fry_display.js");
        if (!File.Exists(fryDisplay)) await File.WriteAllTextAsync(fryDisplay, """
            module.exports = require('./fry');
            """, ct);

        return folder;
    }
}
