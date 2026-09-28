using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// JavaScript, run by the Node.js installed on this computer. <c>.js</c>, <c>.mjs</c>, and <c>.cjs</c> files run
/// with <c>node file.js</c>; notebook cells run in a Node kernel process speaking the Fry kernel protocol.
/// </summary>
public sealed class JavaScriptLanguage : LanguageDefinition
{
    private static readonly string[] JavaScriptAliases = ["js", "node", "nodejs"];
    private static readonly string[] JavaScriptExtensions = [".js", ".mjs", ".cjs"];

    public JavaScriptLanguage(StudioLanguageServices services)
    {
        var toolchain = new JavaScriptToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "javascript"));
        JavaScriptToolchain = toolchain;
        ScriptRunner = new JavaScriptScriptRunner(services.Host);
        Packages = new NpmPackageManager(services.Processes, services.Host);
        Debugger = new JavaScriptDebuggerProvider(toolchain, services.Processes, services.Host, services.AdapterManager);

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

    public override IHighlightingDefinition GetHighlighting(bool isDark) => JavaScriptSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new JavaScriptIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new JavaScriptFoldingStrategy();

    public override IToolchainProvider Toolchain => JavaScriptToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new JavaScriptTracebackParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
    public override IDebuggerProvider Debugger { get; }
}
