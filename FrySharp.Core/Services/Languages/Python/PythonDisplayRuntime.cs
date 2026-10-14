using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Provides zero-configuration runtime support for Python scripts in FrySharp,
/// injecting <c>dump()</c>, <c>display()</c>, and <c>Display</c> into Python scripts via
/// <c>sitecustomize.py</c> on <c>PYTHONPATH</c>.
/// </summary>
public static class PythonDisplayRuntime
{
    public static async Task<string> EnsureRuntimeFilesAsync(IHostEnvironment host, CancellationToken ct = default)
    {
        var folder = EmbeddedKernelFiles.Extract(typeof(PythonDisplayRuntime).Assembly, "PythonRuntime.",
            Path.Combine(Path.GetTempPath(), "FryStudio", "python_runtime"));
        var site = Path.Combine(folder, "sitecustomize.py");
        if (!File.Exists(site)) await File.WriteAllTextAsync(site, """
            try:
                import builtins, fry
                builtins.dump = fry.dump
                builtins.display = fry.display
                builtins.Display = fry.Display
            except Exception:
                pass
            """, ct);
        return folder;
    }
}
