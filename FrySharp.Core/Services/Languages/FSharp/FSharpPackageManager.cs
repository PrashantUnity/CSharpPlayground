using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// NuGet packages for F#. F# Interactive loads them itself: <c>#r "nuget: Package, 1.2.3"</c> at the top of a script or a
/// notebook cell restores and references the package when it runs, so no line is for the notebook to run beforehand
/// (<see cref="TryParseDirective"/> claims none), and a package that is missing is "installed" by putting that line in the
/// file. (The kernel also reads <c>%nuget install Package</c> as the same line.)
/// </summary>
public sealed class FSharpPackageManager : IPackageManager
{
    private readonly NuGetPackageManager _inner;

    public FSharpPackageManager(NuGetPackageManager? inner = null)
    {
        _inner = inner ?? new NuGetPackageManager();
    }

    public string ToolName => "NuGet";

    /// <summary>No line is a package command to run beforehand: fsi loads <c>#r "nuget:"</c> lines as the code runs.</summary>
    public bool TryParseDirective(string line, out PackageCommand command)
    {
        command = new PackageCommand(string.Empty, Array.Empty<string>());
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"#r \"nuget: {trimmed}\"", ["install", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) =>
        _inner.PackageForMissingDependency(missingName);

    public Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        // The package belongs to the file: its reference is a line at the top, loaded when the file runs.
        var package = command.Arguments.Count > 1 ? string.Join(", ", command.Arguments.Skip(1)) : string.Empty;
        if (package.Length == 0) return Task.FromResult(new PackageCommandResult(false, "Name the package: #r \"nuget: Package\"."));

        var line = $"#r \"nuget: {package}\"";
        output($"F# loads NuGet packages from a line in the file: {line}\n");
        return Task.FromResult(new PackageCommandResult(true, DirectiveToInsert: line));
    }
}
