using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Starts the notebook's Java kernel: Java running <c>FryKernel.java</c> embedded in the plugin, speaking
/// the Fry kernel protocol over stdin/stdout.
/// </summary>
public sealed class JavaKernelLauncher(JavaToolchainProvider toolchains, IHostEnvironment host, string kernelFilesRoot) : IKernelLauncher
{
    /// <summary>The embedded resources that make up the kernel program ("JavaKernel.FryKernel.java" ...).</summary>
    public const string ResourcePrefix = "JavaKernel.";

    public async Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct)
    {
        var folder = context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) folder = host.HomeDirectory;

        var resolution = await toolchains.ResolveAsync(new ToolchainQuery(folder, context.WorkspaceRoot?.Invoke()), ct);
        if (resolution.Toolchain is not { } java)
        {
            throw new KernelUnavailableException((resolution.Missing ?? JavaGuidance.NotInstalled(host)).ToText());
        }

        var kernelFolder = EmbeddedKernelFiles.Extract(typeof(JavaKernelLauncher).Assembly, ResourcePrefix, kernelFilesRoot);

        return new KernelLaunchSpec(
            new ProcessStartSpec
            {
                FileName = java.ExecutablePath,
                Arguments = [Path.Combine(kernelFolder, "FryKernel.java"), "--cwd", folder, "--kernel-dir", kernelFolder],
                WorkingDirectory = folder
            },
            java.DisplayName);
    }
}
