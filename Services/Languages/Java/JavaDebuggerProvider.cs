using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Discovers JDK tools / java-debug, compiles Java source with debug symbols (<c>javac -g</c>),
/// and launches an interactive DAP session running through <see cref="DapDebugSession"/>.
/// </summary>
public sealed class JavaDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly JavaToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public JavaDebuggerProvider(
        JavaToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        DapAdapterManager? adapterManager = null)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _adapterManager = adapterManager ?? new DapAdapterManager(processes, host);
        _adapterManager.RegisterAdapter(this);
    }

    public string LanguageId => LanguageIds.Java;
    public string AdapterName => "java-debug (jdb / Eclipse JDT)";

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        var resolved = toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            return new DebuggerResolution(
                IsAvailable: false,
                DebuggerName: "java-debug (jdb / Eclipse JDT)",
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
                    DebuggerName: "java-debug (jdb / Eclipse JDT)",
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
            "Java Debugger not found",
            "jdb or Eclipse JDT java-debug is included with standard JDK distributions. Ensure JDK 11+ is installed.",
            [_host.IsMacOS ? "brew install openjdk@17" : "sudo apt install default-jdk"],
            "https://adoptium.net");

        return new DebuggerResolution(
            IsAvailable: false,
            DebuggerName: "java-debug",
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

        var earlyBuffer = new List<string>();
        JavaDapAdapter? adapter = null;
        var bufferLock = new object();

        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text =>
            {
                lock (bufferLock)
                {
                    if (adapter != null) adapter.OnProcessOutput(text);
                    else earlyBuffer.Add(text);
                }
            },
            onStandardError: text =>
            {
                lock (bufferLock)
                {
                    if (adapter != null) adapter.OnProcessOutput(text);
                    else earlyBuffer.Add(text);
                }
            });

        adapter = new JavaDapAdapter(managedProcess, scriptFile, fqn, context.OnLiveOutput);
        lock (bufferLock)
        {
            foreach (var text in earlyBuffer)
            {
                adapter.OnProcessOutput(text);
            }
            earlyBuffer.Clear();
        }

        var session = await _adapterManager.LaunchBridgeAdapterAsync(
            LanguageIds.Java,
            adapter.ClientInputStream,
            adapter.ClientOutputStream,
            managedProcess,
            context,
            postHandshake: null,
            ct).ConfigureAwait(false);

        return session;
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
