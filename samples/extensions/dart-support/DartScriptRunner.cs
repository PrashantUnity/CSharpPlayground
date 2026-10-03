#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace DartSupportExtension;

/// <summary>
/// Prepares process run plans for Dart scripts and executables with full interactive stdin support
/// and automatic injection of the FrySharp Display and Visualizer runtime.
/// </summary>
public sealed class DartScriptRunner : IScriptRunner
{
    private readonly DartToolchainProvider _toolchain;

    public DartScriptRunner(DartToolchainProvider toolchain)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
    }

    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var toolchain = context.Toolchain;
        var executable = toolchain?.ExecutablePath ?? "dart";
        var scriptPath = context.SourceFilePath;
        var workDir = context.WorkingDirectory;

        var scriptDir = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(scriptDir) && Directory.Exists(scriptDir))
        {
            await DartDisplayRuntime.EnsureInDirectoryAsync(scriptDir, ct).ConfigureAwait(false);
        }
        if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir) && !string.Equals(scriptDir, workDir, StringComparison.OrdinalIgnoreCase))
        {
            await DartDisplayRuntime.EnsureInDirectoryAsync(workDir, ct).ConfigureAwait(false);
        }

        var step = new ProcessStep(
            "run",
            new ProcessStartSpec
            {
                FileName = executable,
                Arguments = ["run", scriptPath],
                WorkingDirectory = workDir
            },
            IsBuildStep: false);

        return new ScriptRunPlan([step]);
    }
}
