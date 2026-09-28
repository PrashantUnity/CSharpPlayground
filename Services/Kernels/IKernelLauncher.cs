using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>How to start a language's kernel program: the command, and the name it's shown by ("Python 3.14.6").</summary>
public sealed record KernelLaunchSpec(ProcessStartSpec Process, string DisplayName);

/// <summary>Prepares a kernel program's start for a notebook: finds the toolchain, puts the program where it can run.</summary>
public interface IKernelLauncher
{
    /// <summary>Throws <see cref="KernelUnavailableException"/> with a message for the user when the kernel can't run here.</summary>
    Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct);
}

/// <summary>A kernel can't start, e.g. because its language isn't installed. The message says what to do.</summary>
public sealed class KernelUnavailableException(string message) : Exception(message);

/// <summary>A kernel that can add a folder to its import path while it runs (a new environment's installed packages).</summary>
public interface ISearchPathKernel
{
    Task AddSearchPathAsync(string path, CancellationToken ct = default);
}
