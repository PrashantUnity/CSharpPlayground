using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Compiles and runs a <c>.java</c> file through a two-phase <see cref="ScriptRunPlan"/>:
/// Phase 1 (Build step): <c>javac -d &lt;outDir&gt; -encoding UTF-8 File.java</c> (stops on compiler error).
/// Phase 2 (Run step): <c>java -cp &lt;outDir&gt; Package.ClassName</c> with interactive terminal input streaming.
/// </summary>
public sealed partial class JavaBuildAndRunScriptRunner(IHostEnvironment host) : IScriptRunner
{
    [GeneratedRegex(@"^\s*package\s+(?<pkg>[a-zA-Z0-9_.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex PackageDeclarationRegex();

    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var binDir = Path.GetDirectoryName(context.Toolchain.ExecutablePath);
        var javacPath = !string.IsNullOrEmpty(binDir)
            ? Path.Combine(binDir, host.IsWindows ? "javac.exe" : "javac")
            : (host.IsWindows ? "javac.exe" : "javac");

        var sourceCode = string.Empty;
        if (host.FileExists(context.SourceFilePath))
        {
            try
            {
                sourceCode = await File.ReadAllTextAsync(context.SourceFilePath, ct);
            }
            catch
            {
                // Fallback to empty if read fails
            }
        }

        var package = ParsePackage(sourceCode);
        var className = Path.GetFileNameWithoutExtension(context.SourceFilePath);
        var fqn = !string.IsNullOrEmpty(package) ? $"{package}.{className}" : className;

        // Isolated compilation folder based on script path hash
        var hash = Math.Abs(context.SourceFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "java_build", hash);
        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch
        {
            // Ignore directory creation issues; javac will report errors if unwritable
        }

        var environment = await JavaProcessEnvironment.ForAsync(host, context.Toolchain, ct);

        return new ScriptRunPlan(
        [
            new ProcessStep("Compile", new ProcessStartSpec
            {
                FileName = javacPath,
                Arguments = ["-d", outDir, "-encoding", "UTF-8", context.SourceFilePath],
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: true),

            new ProcessStep("Run", new ProcessStartSpec
            {
                FileName = context.Toolchain.ExecutablePath,
                Arguments = ["-cp", outDir, fqn],
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: false)
        ]);
    }

    public static string? ParsePackage(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return null;
        var match = PackageDeclarationRegex().Match(sourceCode);
        return match.Success ? match.Groups["pkg"].Value : null;
    }
}

/// <summary>The environment variables every Java process started by the studio gets.</summary>
public static class JavaProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(IHostEnvironment host, ToolchainInfo java, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["JAVA_TOOL_OPTIONS"] = "-Dfile.encoding=UTF-8"
        };

        var path = await host.GetLoginShellPathAsync(ct) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;
        var bin = Path.GetDirectoryName(java.ExecutablePath);
        if (!string.IsNullOrEmpty(bin))
        {
            path = bin + (host.IsWindows ? ";" : ":") + path;
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }
}
