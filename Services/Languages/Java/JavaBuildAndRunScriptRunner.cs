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

    [GeneratedRegex(@"\bpublic\s+(?:final\s+|abstract\s+)*class\s+(?<name>[a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex PublicClassRegex();

    [GeneratedRegex(@"\bclass\s+(?<name>[a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex AnyClassRegex();

    public static string DetectClassName(string sourceCode, string fallbackFileName)
    {
        if (!string.IsNullOrWhiteSpace(sourceCode))
        {
            var pubMatch = PublicClassRegex().Match(sourceCode);
            if (pubMatch.Success) return pubMatch.Groups["name"].Value;

            var anyMatch = AnyClassRegex().Match(sourceCode);
            if (anyMatch.Success) return anyMatch.Groups["name"].Value;
        }

        var baseName = Path.GetFileNameWithoutExtension(fallbackFileName);
        return !string.IsNullOrWhiteSpace(baseName) ? baseName : "Main";
    }

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
        var declaredClass = DetectClassName(sourceCode, context.SourceFilePath);
        var fqn = !string.IsNullOrEmpty(package) ? $"{package}.{declaredClass}" : declaredClass;

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

        string compileFile = context.SourceFilePath;
        var fileBaseName = Path.GetFileNameWithoutExtension(context.SourceFilePath);

        var srcDir = Path.Combine(outDir, "src");

        // If the declared class does not match the file name (e.g. script_HHmmss.java containing public class Quicksort),
        // stage it under outDir/src/<declaredClass>.java so javac compiles it cleanly without JLS §7.6 filename mismatch error.
        if (!string.Equals(declaredClass, fileBaseName, StringComparison.Ordinal))
        {
            try
            {
                Directory.CreateDirectory(srcDir);
                compileFile = Path.Combine(srcDir, $"{declaredClass}.java");
                await File.WriteAllTextAsync(compileFile, sourceCode, ct).ConfigureAwait(false);
            }
            catch
            {
                compileFile = context.SourceFilePath;
            }
        }

        var compileArgs = new List<string> { "-d", outDir, "-encoding", "UTF-8", compileFile };
        if (!sourceCode.Contains("class Display"))
        {
            var displayFiles = await JavaDisplayRuntime.EnsureSourceFilesAsync(srcDir, package, ct).ConfigureAwait(false);
            compileArgs.AddRange(displayFiles);
        }

        var environment = await JavaProcessEnvironment.ForAsync(host, context.Toolchain, ct);

        return new ScriptRunPlan(
        [
            new ProcessStep("Compile", new ProcessStartSpec
            {
                FileName = javacPath,
                Arguments = compileArgs,
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
