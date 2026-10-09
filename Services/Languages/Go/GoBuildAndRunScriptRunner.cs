using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Compiles and runs a Go source file (.go) through a two-phase <see cref="ScriptRunPlan"/>:
/// Phase 1 (Build step): <c>go build -o &lt;outDir&gt;/bin file.go</c> (stops immediately on compilation error).
/// Phase 2 (Run step): <c>&lt;outDir&gt;/bin</c> with interactive terminal standard input streaming.
/// </summary>
public sealed class GoBuildAndRunScriptRunner(IHostEnvironment host) : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var goExecutable = context.Toolchain.ExecutablePath;

        var normalizedSource = context.SourceFilePath.Replace('\\', '/');
        var fileBaseName = Path.GetFileNameWithoutExtension(normalizedSource);
        if (string.IsNullOrWhiteSpace(fileBaseName)) fileBaseName = "main";

        // Isolated compilation folder based on script path hash
        var hash = Math.Abs(context.SourceFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "go_build", hash);
        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch
        {
            // Directory creation failure will be caught if unwritable
        }

        var binName = host.IsWindows ? $"{fileBaseName}.exe" : fileBaseName;
        var binPath = Path.Combine(outDir, binName);
        var sourceDir = (ExecutableSearch.GetDirectoryName(context.SourceFilePath) ?? context.WorkingDirectory) ?? string.Empty;
        if (!host.IsWindows && !string.IsNullOrEmpty(sourceDir))
        {
            sourceDir = sourceDir.Replace('\\', '/');
        }

        await GoDisplayRuntime.EnsureDisplayPackageAsync(outDir, ct).ConfigureAwait(false);

        var buildArgs = new List<string>
        {
            "build",
            "-o",
            binPath,
            context.SourceFilePath
        };

        var buildEnv = new Dictionary<string, string?>(StringComparer.Ordinal);
        var goCache = Path.Combine(outDir, ".gocache");
        buildEnv["GOCACHE"] = goCache;

        if (File.Exists(Path.Combine(sourceDir, "go.mod")))
        {
            var gowork = Path.Combine(outDir, "go.work");
            var fryDir = Path.Combine(outDir, "fry").Replace('\\', '/');
            var srcDirNormalized = sourceDir.Replace('\\', '/');
            await File.WriteAllTextAsync(gowork, $"go 1.20\n\nuse (\n\t\"{fryDir}\"\n\t\"{srcDirNormalized}\"\n)\n", ct).ConfigureAwait(false);
            buildEnv["GOWORK"] = gowork;
        }
        else
        {
            buildEnv["GOPATH"] = outDir;
            buildEnv["GO111MODULE"] = "auto";
        }

        var steps = new List<ProcessStep>
        {
            new(
                Label: $"go build {Path.GetFileName(context.SourceFilePath)}",
                Spec: new ProcessStartSpec
                {
                    FileName = goExecutable,
                    Arguments = buildArgs,
                    WorkingDirectory = !string.IsNullOrEmpty(sourceDir) ? sourceDir : context.WorkingDirectory,
                    Environment = buildEnv
                },
                IsBuildStep: true),

            new(
                Label: binName,
                Spec: new ProcessStartSpec
                {
                    FileName = binPath,
                    Arguments = Array.Empty<string>(),
                    WorkingDirectory = !string.IsNullOrEmpty(sourceDir) ? sourceDir : context.WorkingDirectory,
                    Environment = buildEnv
                },
                IsBuildStep: false)
        };

        return new ScriptRunPlan(steps);
    }
}
