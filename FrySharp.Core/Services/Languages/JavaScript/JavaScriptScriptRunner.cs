using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Runs a <c>.js</c> file with Node.js in the script's folder: <c>node file.js</c>, so relative requires, imports,
/// <c>node_modules</c>, and top-level await work naturally.
/// </summary>
public sealed class JavaScriptScriptRunner(IHostEnvironment host) : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var environment = await JavaScriptProcessEnvironment.ForAsync(host, context.Toolchain, ct);
        var jsRuntimeDir = await JavaScriptDisplayRuntime.EnsureRuntimeFilesAsync(host, ct).ConfigureAwait(false);
        var preloadFile = Path.Combine(jsRuntimeDir, "preload.js");
        var args = File.Exists(preloadFile)
            ? new[] { "-r", preloadFile, context.SourceFilePath }
            : new[] { context.SourceFilePath };

        return new ScriptRunPlan(
        [
            new ProcessStep("Run", new ProcessStartSpec
            {
                FileName = context.Toolchain.ExecutablePath,
                Arguments = args,
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            })
        ]);
    }
}

/// <summary>The environment variables every Node.js process started by the studio gets.</summary>
public static class JavaScriptProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(IHostEnvironment host, ToolchainInfo node, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["FORCE_COLOR"] = "0"
        };

        var path = await host.GetLoginShellPathAsync(ct) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;

        var bin = Path.GetDirectoryName(node.ExecutablePath);
        if (!string.IsNullOrEmpty(bin))
        {
            path = bin + (host.IsWindows ? ";" : ":") + path;
        }

        if (path.Length > 0) environment["PATH"] = path;

        var jsRuntimeDir = await JavaScriptDisplayRuntime.EnsureRuntimeFilesAsync(host, ct).ConfigureAwait(false);
        var existingNodePath = host.GetEnvironmentVariable("NODE_PATH");
        environment["NODE_PATH"] = string.IsNullOrEmpty(existingNodePath)
            ? jsRuntimeDir
            : jsRuntimeDir + (host.IsWindows ? ";" : ":") + existingNodePath;

        return environment;
    }
}
