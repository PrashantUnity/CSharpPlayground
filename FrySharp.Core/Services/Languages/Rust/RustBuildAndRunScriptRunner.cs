using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Builds and runs a Rust source file (<c>.rs</c>) through a two-phase <see cref="ScriptRunPlan"/>:
/// Phase 1 (Build step): <c>cargo build</c> on a generated package whose one binary is the file (stops on a compile error).
/// Phase 2 (Run step): the built program, with the Terminal's standard input streaming to it.
/// Crates named in <c>// #crate:</c> comments are fetched and compiled once into a build folder shared by all scripts.
/// </summary>
public sealed class RustBuildAndRunScriptRunner(IHostEnvironment host, string rustRoot) : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var source = await ReadSourceAsync(context.SourceFilePath, ct).ConfigureAwait(false);
        var directives = RustDirectives.Parse(source);
        var displayCrate = await RustDisplayRuntime.EnsureCrateAsync(rustRoot, ct).ConfigureAwait(false);
        var stage = RustProjectStager.Prepare(rustRoot, context.SourceFilePath, directives, displayCrate, host.IsWindows);
        var environment = await RustProcessEnvironment.ForAsync(host, context.Toolchain, stage.TargetDir, ct).ConfigureAwait(false);

        // cargo looks for a rust-toolchain file and .cargo/config.toml from where it starts, so build in the file's folder.
        var sourceDir = RustProcessEnvironment.DirectoryOf(context.SourceFilePath);
        var buildDir = !string.IsNullOrEmpty(sourceDir) ? sourceDir : context.WorkingDirectory;

        var buildArgs = new List<string> { "build", "--manifest-path", stage.ManifestPath, "--color", "never" };
        if (directives.Release) buildArgs.Add("--release");

        var programName = Path.GetFileNameWithoutExtension(context.SourceFilePath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(programName)) programName = "main";

        return new ScriptRunPlan(
        [
            new ProcessStep("cargo build", new ProcessStartSpec
            {
                FileName = context.Toolchain.ExecutablePath,
                Arguments = buildArgs,
                WorkingDirectory = buildDir,
                Environment = environment
            }, IsBuildStep: true),

            new ProcessStep(programName, new ProcessStartSpec
            {
                FileName = stage.BinPath,
                Arguments = Array.Empty<string>(),
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: false)
        ]);
    }

    private static async Task<string> ReadSourceAsync(string path, CancellationToken ct)
    {
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, ct).ConfigureAwait(false) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}

/// <summary>The environment variables every Rust build and program starts with.</summary>
public static class RustProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(
        IHostEnvironment host, ToolchainInfo toolchain, string targetDir, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["CARGO_TERM_COLOR"] = "never",
            ["CARGO_TARGET_DIR"] = targetDir
        };

        var path = await host.GetLoginShellPathAsync(ct).ConfigureAwait(false) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;

        // Puts the chosen toolchain's rustc next to cargo, and finds the linker the way a terminal would.
        var bin = DirectoryOf(toolchain.ExecutablePath);
        if (!string.IsNullOrEmpty(bin))
        {
            path = bin + (host.IsWindows ? ";" : ":") + path;
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }

    /// <summary>The folder of a path, whichever slash it uses (a Windows path can be described on any machine).</summary>
    public static string? DirectoryOf(string path)
    {
        var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return slash > 0 ? path[..slash] : null;
    }
}
