using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Packages;

/// <summary>A package command, e.g. <c>%pip install numpy</c> read as its arguments <c>install numpy</c>.</summary>
public sealed record PackageCommand(string Text, IReadOnlyList<string> Arguments);

/// <param name="SwitchedToolchain">A different toolchain to use from now on (e.g. a new studio environment).</param>
/// <param name="AddedSearchPath">A folder a running kernel should add to its import path to see what was installed.</param>
public sealed record PackageCommandResult(bool Success, string? Message = null, ToolchainInfo? SwitchedToolchain = null, string? AddedSearchPath = null);

/// <summary>Installs a language's packages: pip for Python, npm for JavaScript and so on.</summary>
public interface IPackageManager
{
    /// <summary>What it's called in messages, e.g. "pip".</summary>
    string ToolName { get; }

    /// <summary>True for a line at the top of a cell that's a package command (e.g. <c>%pip install numpy</c>).</summary>
    bool TryParseDirective(string line, out PackageCommand command);

    /// <summary>The command that installs <paramref name="package"/>.</summary>
    PackageCommand InstallCommand(string package);

    /// <summary>The package that provides a missing dependency named in an error (<c>cv2</c> → <c>opencv-python</c>).</summary>
    string PackageForMissingDependency(string missingName);

    Task<PackageCommandResult> RunAsync(PackageCommand command, ToolchainInfo toolchain, Action<string> output, CancellationToken ct = default);
}
