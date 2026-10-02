using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Provides zero-configuration runtime support for Node.js scripts in C# Code Studio,
/// injecting <c>dump()</c>, <c>display()</c>, and <c>Display</c> into JavaScript scripts.
/// </summary>
public static class JavaScriptDisplayRuntime
{
    public static async Task<string> EnsureRuntimeFilesAsync(IHostEnvironment host, CancellationToken ct = default)
    {
        var folder = EmbeddedKernelFiles.Extract(typeof(JavaScriptDisplayRuntime).Assembly, "JavaScriptRuntime.",
            Path.Combine(Path.GetTempPath(), "FryStudio", "js_runtime"));
        var preload = Path.Combine(folder, "preload.js");
        if (!File.Exists(preload)) await File.WriteAllTextAsync(preload, """
            try {
                const fry = require('./fry');
                globalThis.Display = fry.Display;
                globalThis.dump = fry.dump;
                globalThis.display = fry.display;
            } catch (e) {}
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
