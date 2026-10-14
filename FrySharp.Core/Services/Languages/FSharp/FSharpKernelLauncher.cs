using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Starts the notebook's F# kernel: <c>fry_kernel.fsx</c>, run by the F# Interactive of the .NET SDK a notebook would use.
/// The script hosts the F# compiler's own evaluation session, so what one cell defines the next one sees.
/// </summary>
public sealed class FSharpKernelLauncher(FSharpToolchainProvider toolchains, IHostEnvironment host, string kernelFilesRoot) : IKernelLauncher
{
    /// <summary>The embedded resources that make up the kernel ("FSharpKernel.fry_kernel.fsx", "FSharpKernel.Display.fsx" ...).</summary>
    public const string ResourcePrefix = "FSharpKernel.";

    public async Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct)
    {
        var folder = context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) folder = host.HomeDirectory;

        var resolution = await toolchains.ResolveAsync(new ToolchainQuery(folder, context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (resolution.Toolchain is not { } fsharp)
        {
            throw new KernelUnavailableException((resolution.Missing ?? FSharpGuidance.NotInstalled(host)).ToText());
        }

        var kernelFolder = EmbeddedKernelFiles.Extract(typeof(FSharpKernelLauncher).Assembly, ResourcePrefix, kernelFilesRoot);
        var isDirectFsi = Path.GetFileNameWithoutExtension(fsharp.ExecutablePath).Equals("fsi", StringComparison.OrdinalIgnoreCase);

        // fsi finds FSharp.Compiler.Service.dll for the script's #r in its own folder, so the session is always the compiler of
        // the fsi that runs it, whichever SDK that is.
        var arguments = new List<string>();
        if (!isDirectFsi) arguments.Add("fsi");
        arguments.AddRange(["--nologo", Path.Combine(kernelFolder, "fry_kernel.fsx"), "--", "--cwd", folder]);

        return new KernelLaunchSpec(
            new ProcessStartSpec
            {
                FileName = fsharp.ExecutablePath,
                Arguments = arguments,
                WorkingDirectory = folder
            },
            fsharp.DisplayName);
    }
}
