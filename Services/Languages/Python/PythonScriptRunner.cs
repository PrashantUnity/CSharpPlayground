using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Runs a <c>.py</c> file the way a terminal would: <c>python -u file.py</c> in the file's folder, so its imports,
/// relative paths, <c>__name__ == "__main__"</c>, multiprocessing and GUI windows (a matplotlib <c>plt.show()</c>) all
/// behave as usual.
/// </summary>
public sealed class PythonScriptRunner(IHostEnvironment host) : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var environment = await PythonProcessEnvironment.ForAsync(host, context.Toolchain, ct);
        return new ScriptRunPlan(
        [
            new ProcessStep("Run", new ProcessStartSpec
            {
                FileName = context.Toolchain.ExecutablePath,
                // -u: output arrives as it's printed rather than when a buffer fills.
                Arguments = ["-u", context.SourceFilePath],
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            })
        ]);
    }
}

/// <summary>The environment variables every Python the studio starts gets.</summary>
public static class PythonProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(IHostEnvironment host, ToolchainInfo python, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["PYTHONUNBUFFERED"] = "1",
            // Output is read as UTF-8 whatever the system's locale says; open() keeps its usual default.
            ["PYTHONIOENCODING"] = "utf-8",
            // The Terminal shows plain text: no colored tracebacks (3.13+) or colored library output.
            ["PYTHON_COLORS"] = "0",
            ["NO_COLOR"] = "1"
        };

        // The same PATH as the user's terminal, so programs the script starts are found (an app launched from the Finder
        // has a minimal one).
        var path = await host.GetLoginShellPathAsync(ct) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;

        // Like "activating" a virtual environment: its own tools come first, and pip installs go into it.
        if (python.Is(PythonToolchainProvider.VirtualEnvironmentProperty))
        {
            var bin = Path.GetDirectoryName(python.ExecutablePath);
            if (!string.IsNullOrEmpty(bin)) path = bin + (host.IsWindows ? ";" : ":") + path;
            environment["VIRTUAL_ENV"] = python.Get(PythonToolchainProvider.PrefixProperty);
            environment["PYTHONHOME"] = null;
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }
}
