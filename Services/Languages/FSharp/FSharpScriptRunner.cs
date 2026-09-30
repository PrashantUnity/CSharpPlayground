using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Executes an F# script (.fsx) using <c>dotnet fsi --nologo --exec &lt;file.fsx&gt;</c> with full
/// interactive terminal standard input streaming and display runtime staging.
/// </summary>
public sealed class FSharpScriptRunner : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var executable = context.Toolchain.ExecutablePath;
        var fileName = Path.GetFileName(context.SourceFilePath);
        var sourceDir = Path.GetDirectoryName(context.SourceFilePath) ?? context.WorkingDirectory;

        // Isolated compilation folder based on script path hash
        var hash = Math.Abs(context.SourceFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "fsharp_build", hash);
        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch
        {
            // Directory creation failure will be caught if unwritable
        }

        await FSharpDisplayRuntime.EnsureDisplayPackageAsync(outDir, ct).ConfigureAwait(false);
        var displayFsx = Path.Combine(outDir, "Display.fsx");
        var fryDir = Path.Combine(outDir, "fry");

        var isDirectFsi = Path.GetFileNameWithoutExtension(executable).Equals("fsi", StringComparison.OrdinalIgnoreCase);
        var runArgs = isDirectFsi
            ? new List<string> { "--nologo", $"--load:{displayFsx}", $"--lib:{outDir}", $"--lib:{fryDir}", "--exec", context.SourceFilePath }
            : new List<string> { "fsi", "--nologo", $"--load:{displayFsx}", $"--lib:{outDir}", $"--lib:{fryDir}", "--exec", context.SourceFilePath };

        var workingDir = !string.IsNullOrEmpty(sourceDir) ? sourceDir : context.WorkingDirectory;

        var steps = new List<ProcessStep>
        {
            new(
                Label: $"dotnet fsi {fileName}",
                Spec: new ProcessStartSpec
                {
                    FileName = executable,
                    Arguments = runArgs,
                    WorkingDirectory = workingDir,
                    Environment = new Dictionary<string, string?>(StringComparer.Ordinal)
                },
                IsBuildStep: false)
        };

        return new ScriptRunPlan(steps);
    }
}
