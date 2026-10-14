using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Starts the notebook's Python kernel: the Python a notebook would run with (its folder's virtual environment, the
/// saved choice, the studio environment, or what's installed) running fry_kernel.py, which ships in the plugin.
/// </summary>
public sealed class PythonKernelLauncher(PythonToolchainProvider toolchains, IHostEnvironment host, string kernelFilesRoot) : IKernelLauncher
{
    /// <summary>The embedded resources that make up the kernel program ("PythonKernel.fry_kernel.py" …).</summary>
    public const string ResourcePrefix = "PythonKernel.";

    public async Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct)
    {
        var folder = context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) folder = host.HomeDirectory;

        var resolution = await toolchains.ResolveAsync(new ToolchainQuery(folder, context.WorkspaceRoot?.Invoke()), ct);
        if (resolution.Toolchain is not { } python)
        {
            throw new KernelUnavailableException((resolution.Missing ?? PythonGuidance.NotInstalled(host)).ToText());
        }

        var kernelFolder = EmbeddedKernelFiles.Extract(typeof(PythonKernelLauncher).Assembly, ResourcePrefix, kernelFilesRoot);
        var environment = await PythonProcessEnvironment.ForAsync(host, python, ct);
        return new KernelLaunchSpec(
            new ProcessStartSpec
            {
                FileName = python.ExecutablePath,
                Arguments = ["-u", Path.Combine(kernelFolder, "fry_kernel.py"), "--cwd", folder],
                WorkingDirectory = folder,
                Environment = environment
            },
            python.DisplayName);
    }
}
