using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// JavaScript, run by the Node.js installed on this computer: <c>.js</c> files run like <c>node file.js</c>; notebook
/// cells run in a kernel process that keeps their state.
/// </summary>
public sealed class JavaScriptEngine : LanguageEngine
{
    private static readonly string[] JavaScriptAliases = ["js", "node", "nodejs"];
    private static readonly string[] JavaScriptExtensions = [".js", ".mjs", ".cjs"];

    public JavaScriptEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var toolchain = new JavaScriptToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "javascript"));
        JavaScriptToolchain = toolchain;
        ScriptRunner = new JavaScriptScriptRunner(services.Host);
        Packages = new NpmPackageManager(services.Processes, services.Host);

        var launcher = new JavaScriptKernelLauncher(toolchain, services.Host, Path.Combine(services.BaseDirectory, "javascript", "kernel"));
        NotebookKernels = new DelegateKernelFactory(context =>
            new ProtocolKernel(LanguageIds.JavaScript, "JavaScript", launcher, services.Processes, context));
    }

    public JavaScriptToolchainProvider JavaScriptToolchain { get; }

    public override string Id => LanguageIds.JavaScript;
    public override string DisplayName => "JavaScript";
    public override JupyterLanguageInfo Jupyter { get; } = new("javascript", "JavaScript", "javascript", "javascript", ".js", "application/javascript");
    public override string ShortName => "JS";
    public override IReadOnlyList<string> Aliases => JavaScriptAliases;
    public override IReadOnlyList<string> FileExtensions => JavaScriptExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput | LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing | LanguageCapabilities.Packages |
        LanguageCapabilities.Debugging | LanguageCapabilities.Breakpoints;

    public override string IconKind => "LanguageJavascript";
    public override string AccentHex => "#F7DF1E";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "JavaScript (Node.js)";

    public override string NewFileTemplate =>
        "// Runs with the Node.js installed on this computer (F5).\n" +
        "console.log(`Hello from JavaScript on Node.js ${process.version}!`);\n";

    public override IToolchainProvider Toolchain => JavaScriptToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new JavaScriptTracebackParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
