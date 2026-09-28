using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Compiles and runs a C++ source file (<c>.cpp</c>, <c>.cc</c>) through a two-phase <see cref="ScriptRunPlan"/>:
/// Phase 1 (Build step): <c>clang++ -std=c++20 -O2 -Wall -I&lt;srcDir&gt; -o &lt;outDir&gt;/bin file.cpp</c> (stops on compilation failure).
/// Phase 2 (Run step): <c>&lt;outDir&gt;/bin</c> with interactive terminal standard input streaming.
/// </summary>
public sealed class CppBuildAndRunScriptRunner(IHostEnvironment host) : IScriptRunner
{
    public async Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default)
    {
        var compilerPath = context.Toolchain.ExecutablePath;
        var fileName = Path.GetFileName(compilerPath.Replace('\\', '/'));
        var isCl = fileName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) || fileName.Equals("cl", StringComparison.OrdinalIgnoreCase);

        var normalizedSource = context.SourceFilePath.Replace('\\', '/');
        var fileBaseName = Path.GetFileNameWithoutExtension(normalizedSource);
        if (string.IsNullOrWhiteSpace(fileBaseName)) fileBaseName = "main";

        // Isolated compilation folder based on script path hash
        var hash = Math.Abs(context.SourceFilePath.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "cpp_build", hash);
        try
        {
            Directory.CreateDirectory(outDir);
        }
        catch
        {
            // Ignore directory creation issues; compiler will report errors if unwritable
        }

        var binName = (host.IsWindows || isCl) ? $"{fileBaseName}.exe" : fileBaseName;
        var binPath = Path.Combine(outDir, binName);
        var sourceDir = Path.GetDirectoryName(context.SourceFilePath) ?? context.WorkingDirectory;

        var includeDir = await CppDisplayRuntime.EnsureIncludeDirectoryAsync(outDir, ct).ConfigureAwait(false);

        var compileArgs = new List<string>();
        if (isCl)
        {
            // MSVC compiler arguments
            compileArgs.Add("/std:c++20");
            compileArgs.Add("/EHsc");
            compileArgs.Add("/W4");
            compileArgs.Add($"/Fe:{binPath}");
            compileArgs.Add($"/Fo:{outDir}\\");
            if (!string.IsNullOrEmpty(sourceDir))
            {
                compileArgs.Add($"/I{sourceDir}");
            }
            compileArgs.Add($"/I{includeDir}");
            compileArgs.Add(context.SourceFilePath);
        }
        else
        {
            // Clang / GCC compiler arguments
            compileArgs.Add("-std=c++20");
            compileArgs.Add("-O2");
            compileArgs.Add("-Wall");
            if (!string.IsNullOrEmpty(sourceDir))
            {
                compileArgs.Add($"-I{sourceDir}");
            }
            compileArgs.Add($"-I{includeDir}");
            compileArgs.Add("-o");
            compileArgs.Add(binPath);
            compileArgs.Add(context.SourceFilePath);
        }

        var environment = await CppProcessEnvironment.ForAsync(host, context.Toolchain, ct);

        return new ScriptRunPlan(
        [
            new ProcessStep("Compile", new ProcessStartSpec
            {
                FileName = compilerPath,
                Arguments = compileArgs,
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: true),

            new ProcessStep("Run", new ProcessStartSpec
            {
                FileName = binPath,
                Arguments = Array.Empty<string>(),
                WorkingDirectory = context.WorkingDirectory,
                Environment = environment
            }, IsBuildStep: false)
        ]);
    }
}

/// <summary>The environment variables every C++ compiler and execution process starts with.</summary>
public static class CppProcessEnvironment
{
    public static async Task<IReadOnlyDictionary<string, string?>> ForAsync(IHostEnvironment host, ToolchainInfo compiler, CancellationToken ct = default)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1"
        };

        var path = await host.GetLoginShellPathAsync(ct) ?? host.GetEnvironmentVariable("PATH") ?? string.Empty;
        var bin = Path.GetDirectoryName(compiler.ExecutablePath);
        if (!string.IsNullOrEmpty(bin))
        {
            path = bin + (host.IsWindows ? ";" : ":") + path;
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }
}
