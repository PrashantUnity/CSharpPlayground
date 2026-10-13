using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Starts the notebook's JavaScript kernel: Node.js running <c>fry_kernel.js</c> embedded in the plugin, speaking
/// the Fry kernel protocol over stdin/stdout.
/// </summary>
public sealed class JavaScriptKernelLauncher(JavaScriptToolchainProvider toolchains, IHostEnvironment host, string kernelFilesRoot) : IKernelLauncher
{
    /// <summary>The embedded resources that make up the kernel program ("JavaScriptKernel.fry_kernel.js" ...).</summary>
    public const string ResourcePrefix = "JavaScriptKernel.";

    public async Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct)
    {
        var folder = context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) folder = host.HomeDirectory;

        var resolution = await toolchains.ResolveAsync(new ToolchainQuery(folder, context.WorkspaceRoot?.Invoke()), ct);
        if (resolution.Toolchain is not { } node)
        {
            throw new KernelUnavailableException((resolution.Missing ?? JavaScriptGuidance.NotInstalled(host)).ToText());
        }

        var kernelFolder = EmbeddedKernelFiles.Extract(typeof(JavaScriptKernelLauncher).Assembly, ResourcePrefix, kernelFilesRoot);
        var environment = await JavaScriptProcessEnvironment.ForAsync(host, node, ct);

        return new KernelLaunchSpec(
            new ProcessStartSpec
            {
                FileName = node.ExecutablePath,
                Arguments = [Path.Combine(kernelFolder, "fry_kernel.js"), "--cwd", folder],
                WorkingDirectory = folder,
                Environment = environment
            },
            node.DisplayName);
    }
}
