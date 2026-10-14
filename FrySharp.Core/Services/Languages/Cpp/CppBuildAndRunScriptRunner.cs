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
        var vcpkgInclude = FindVcpkgIncludeDirectory(context.WorkingDirectory, host);

        var includeDirectories = new List<string>();
        if (!string.IsNullOrEmpty(sourceDir)) includeDirectories.Add(sourceDir);
        includeDirectories.Add(includeDir);
        if (!string.IsNullOrEmpty(vcpkgInclude)) includeDirectories.Add(vcpkgInclude);
        var compileArgs = CppCompilerCommand.Arguments(host, context.Toolchain, context.SourceFilePath, binPath, outDir, includeDirectories);

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

    public static string? FindVcpkgIncludeDirectory(string workingDirectory, IHostEnvironment host)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            candidates.Add(Path.Combine(workingDirectory, "vcpkg_installed"));
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(userHome, ".vcpkg", "installed"));
        candidates.Add(Path.Combine(userHome, "vcpkg", "installed"));

        foreach (var candidate in candidates)
        {
            if (host.DirectoryExists(candidate))
            {
                try
                {
                    var subdirs = Directory.GetDirectories(candidate);
                    foreach (var sub in subdirs)
                    {
                        var inc = Path.Combine(sub, "include");
                        if (host.DirectoryExists(inc)) return inc;
                    }
                }
                catch
                {
                    // Ignore filesystem enumeration errors
                }
            }
        }

        return null;
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

        if (host.IsWindows)
        {
            var windowsSdk = CppWindowsSdkResolver.Resolve(host);
            if (windowsSdk.HasMsvcHeaders)
            {
                var existingInclude = host.GetEnvironmentVariable("INCLUDE");
                var msvcIncludes = string.Join(";", windowsSdk.IncludeDirectories);
                environment["INCLUDE"] = string.IsNullOrEmpty(existingInclude) ? msvcIncludes : msvcIncludes + ";" + existingInclude;

                var existingLib = host.GetEnvironmentVariable("LIB");
                var msvcLibs = string.Join(";", windowsSdk.LibDirectories);
                environment["LIB"] = string.IsNullOrEmpty(existingLib) ? msvcLibs : msvcLibs + ";" + existingLib;
            }
            else if (windowsSdk.HasMinGw && !string.IsNullOrEmpty(windowsSdk.MinGwSysroot))
            {
                var minGwBin = host.IsWindows
                    ? windowsSdk.MinGwSysroot.TrimEnd('\\', '/') + "\\bin"
                    : Path.Combine(windowsSdk.MinGwSysroot, "bin");
                if (host.DirectoryExists(minGwBin))
                {
                    path = minGwBin + ";" + path;
                }
            }
        }

        if (path.Length > 0) environment["PATH"] = path;
        return environment;
    }
}

/// <summary>The compiler's command line for one C++ source file: the same for a script and for a notebook cell.</summary>
public static class CppCompilerCommand
{
    public static bool IsCl(string compilerPath)
    {
        var fileName = Path.GetFileName(compilerPath.Replace('\\', '/'));
        return fileName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) || fileName.Equals("cl", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> Arguments(IHostEnvironment host, ToolchainInfo toolchain, string sourcePath, string binaryPath, string outDir, IReadOnlyList<string> includeDirectories)
    {
        var compilerPath = toolchain.ExecutablePath;
        var isCl = IsCl(compilerPath);
        var isClang = Path.GetFileName(compilerPath.Replace('\\', '/')).Contains("clang", StringComparison.OrdinalIgnoreCase);
        var windowsSdk = host.IsWindows ? CppWindowsSdkResolver.Resolve(host) : null;
        var arguments = new List<string>();

        if (isCl)
        {
            // MSVC compiler arguments
            arguments.Add("/std:c++20");
            arguments.Add("/EHsc");
            arguments.Add("/W4");
            arguments.Add($"/Fe:{binaryPath}");
            arguments.Add($"/Fo:{outDir}\\");
            foreach (var directory in includeDirectories) arguments.Add($"/I{directory}");
            if (windowsSdk?.HasMsvcHeaders == true)
            {
                foreach (var inc in windowsSdk.IncludeDirectories) arguments.Add($"/I{inc}");
            }

            arguments.Add(sourcePath);
            return arguments;
        }

        // Clang / GCC compiler arguments
        arguments.Add("-std=c++20");
        arguments.Add("-O2");
        arguments.Add("-Wall");

        if (host.IsWindows && windowsSdk != null)
        {
            if (isClang && windowsSdk.HasMsvcHeaders)
            {
                foreach (var inc in windowsSdk.IncludeDirectories)
                {
                    arguments.Add("-imsvc");
                    arguments.Add(inc);
                }
            }
            else if (isClang && windowsSdk.HasMinGw)
            {
                arguments.Add("--target=x86_64-w64-windows-gnu");
                arguments.Add($"--sysroot={windowsSdk.MinGwSysroot}");
            }
        }

        foreach (var directory in includeDirectories) arguments.Add($"-I{directory}");
        arguments.Add("-o");
        arguments.Add(binaryPath);
        arguments.Add(sourcePath);
        if (host.IsWindows) arguments.Add("-lws2_32");
        return arguments;
    }
}
