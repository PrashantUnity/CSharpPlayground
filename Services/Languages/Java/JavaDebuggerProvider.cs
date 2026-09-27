using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Discovers JDK's native <c>jdb</c> debugger, compiles Java source with debug symbols (<c>javac -g</c>),
/// and launches headless interactive <see cref="JavaDebugSession"/>.
/// </summary>
public sealed class JavaDebuggerProvider : IDebuggerProvider
{
    private readonly JavaToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;

    public JavaDebuggerProvider(JavaToolchainProvider toolchain, IProcessLauncher processes, IHostEnvironment host)
    {
        _toolchain = toolchain;
        _processes = processes;
        _host = host;
    }

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        var resolved = toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            return new DebuggerResolution(
                IsAvailable: false,
                DebuggerName: "jdb (Java SE Debugger)",
                ExecutablePath: null,
                Version: null,
                MissingGuidance: resolved.Missing);
        }

        var javaExe = resolved.Toolchain.ExecutablePath;
        var binDir = Path.GetDirectoryName(javaExe);
        var jdbName = _host.IsWindows ? "jdb.exe" : "jdb";
        var jdbPath = !string.IsNullOrEmpty(binDir) && _host.FileExists(Path.Combine(binDir, jdbName))
            ? Path.Combine(binDir, jdbName)
            : jdbName;

        try
        {
            var result = await _host.RunAsync(jdbPath, ["-version"], TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            if (result.ExitCode == 0 || !string.IsNullOrWhiteSpace(result.StandardOutput) || !string.IsNullOrWhiteSpace(result.StandardError))
            {
                var versionText = (result.StandardOutput + " " + result.StandardError).Trim();
                var version = JavaToolchainProvider.ParseJavaVersion(versionText) ?? resolved.Toolchain.Version;

                return new DebuggerResolution(
                    IsAvailable: true,
                    DebuggerName: "jdb (Java SE Debugger)",
                    ExecutablePath: jdbPath,
                    Version: version.ToString(),
                    MissingGuidance: null);
            }
        }
        catch
        {
            // Probe failed
        }

        var missing = new MissingToolchainGuidance(
            "Java Debugger (jdb) not found",
            "jdb is included with standard JDK distributions. Ensure JDK 11+ is installed.",
            [_host.IsMacOS ? "brew install openjdk@17" : "sudo apt install default-jdk"],
            "https://adoptium.net");

        return new DebuggerResolution(
            IsAvailable: false,
            DebuggerName: "jdb",
            ExecutablePath: null,
            Version: null,
            MissingGuidance: missing);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var docFolder = string.IsNullOrEmpty(context.SourceFilePath) ? null : Path.GetDirectoryName(context.SourceFilePath);
        var resolved = context.Toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(docFolder), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "Java runtime not found.");
        }

        var javaExe = resolved.Toolchain.ExecutablePath;
        var binDir = Path.GetDirectoryName(javaExe);
        var javacPath = !string.IsNullOrEmpty(binDir) && _host.FileExists(Path.Combine(binDir, _host.IsWindows ? "javac.exe" : "javac"))
            ? Path.Combine(binDir, _host.IsWindows ? "javac.exe" : "javac")
            : (_host.IsWindows ? "javac.exe" : "javac");
        var jdbPath = !string.IsNullOrEmpty(binDir) && _host.FileExists(Path.Combine(binDir, _host.IsWindows ? "jdb.exe" : "jdb"))
            ? Path.Combine(binDir, _host.IsWindows ? "jdb.exe" : "jdb")
            : (_host.IsWindows ? "jdb.exe" : "jdb");

        // Write source code to temporary file if not already on disk
        string scriptFile = context.SourceFilePath;
        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            var detectedClass = Path.GetFileNameWithoutExtension(scriptFile);
            if (string.IsNullOrEmpty(detectedClass) || detectedClass.Equals("script", StringComparison.OrdinalIgnoreCase))
            {
                detectedClass = "Main";
            }
            scriptFile = Path.Combine(Path.GetTempPath(), $"{detectedClass}.java");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }
        scriptFile = Path.GetFullPath(scriptFile);

        var package = JavaBuildAndRunScriptRunner.ParsePackage(context.SourceCode);
        var declaredClass = JavaBuildAndRunScriptRunner.DetectClassName(context.SourceCode, scriptFile);
        var fqn = !string.IsNullOrEmpty(package) ? $"{package}.{declaredClass}" : declaredClass;

        var hash = Math.Abs(scriptFile.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "java_debug", hash);
        Directory.CreateDirectory(outDir);

        string compileFile = scriptFile;
        var fileBaseName = Path.GetFileNameWithoutExtension(scriptFile);
        if (!string.Equals(declaredClass, fileBaseName, StringComparison.Ordinal))
        {
            var srcDir = Path.Combine(outDir, "src");
            Directory.CreateDirectory(srcDir);
            compileFile = Path.Combine(srcDir, $"{declaredClass}.java");
            await File.WriteAllTextAsync(compileFile, context.SourceCode, ct).ConfigureAwait(false);
        }

        var env = await JavaProcessEnvironment.ForAsync(_host, resolved.Toolchain, ct).ConfigureAwait(false);

        // Compile with -g (full debug symbols for locals, lines, and source)
        var compileResult = await _host.RunAsync(javacPath, ["-g", "-d", outDir, "-encoding", "UTF-8", compileFile], TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        if (compileResult.ExitCode != 0)
        {
            var parser = new JavaCompilerDiagnosticParser();
            var parseResult = parser.Parse(compileResult.StandardOutput + "\n" + compileResult.StandardError, scriptFile);
            throw new DebugCompilationException(parseResult.Diagnostics);
        }

        var workingDir = Path.GetDirectoryName(scriptFile) ?? Directory.GetCurrentDirectory();

        var spec = new ProcessStartSpec
        {
            FileName = jdbPath,
            Arguments = ["-classpath", outDir, fqn],
            WorkingDirectory = workingDir,
            Environment = env
        };

        JavaDebugSession? session = null;
        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text =>
            {
                context.OnLiveOutput?.Invoke(text);
                session?.OnProcessOutput(text);
            },
            onStandardError: text =>
            {
                context.OnLiveOutput?.Invoke(text);
                session?.OnProcessOutput(text);
            });

        session = new JavaDebugSession(managedProcess, scriptFile, fqn);

        // Set initial breakpoints and start run
        if (context.Breakpoints.Count > 0)
        {
            await session.SetBreakpointsAsync(scriptFile, context.Breakpoints, ct).ConfigureAwait(false);
        }

        await session.StartRunAsync(ct).ConfigureAwait(false);

        return session;
    }
}
