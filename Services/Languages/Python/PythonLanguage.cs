using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Python, run by the Python installed on this computer, so every package installed for it works. <c>.py</c> files run
/// like <c>python file.py</c>; notebook cells run in a kernel process that keeps their state.
/// </summary>
public sealed class PythonLanguage : LanguageDefinition
{
    private static readonly string[] PythonAliases = ["py", "python3"];
    private static readonly string[] PythonExtensions = [".py"];

    public PythonLanguage(StudioLanguageServices services)
    {
        var toolchain = new PythonToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "python"));
        PythonToolchain = toolchain;
        ScriptRunner = new PythonScriptRunner(services.Host);
        Packages = new PipPackageManager(toolchain, services.Processes, services.Host);

        // Each notebook gets its own kernel program, started with its first Python cell.
        var launcher = new PythonKernelLauncher(toolchain, services.Host, Path.Combine(services.BaseDirectory, "python", "kernel"));
        NotebookKernels = new DelegateKernelFactory(context =>
            new ProtocolKernel(LanguageIds.Python, "Python", launcher, services.Processes, context));
    }

    public PythonToolchainProvider PythonToolchain { get; }

    public override string Id => LanguageIds.Python;
    public override string DisplayName => "Python";
    public override JupyterLanguageInfo Jupyter { get; } = new("python3", "Python 3", "python", "python", ".py", "text/x-python");
    public override string ShortName => "PY";
    public override IReadOnlyList<string> Aliases => PythonAliases;
    public override IReadOnlyList<string> FileExtensions => PythonExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput | LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing | LanguageCapabilities.Packages;

    public override string IconKind => "LanguagePython";
    public override string AccentHex => "#4B8BBE";
    public override string LineCommentPrefix => "#";

    public override string NewFileTemplate =>
        "# Runs with the Python installed on this computer (F5), so any package you've installed works here.\n" +
        "import sys\n" +
        "\n" +
        "print(f\"Hello from Python {sys.version.split()[0]}!\")\n";

    public override IHighlightingDefinition GetHighlighting(bool isDark) => PythonSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new PythonIndentationStrategy(options);

    public override IToolchainProvider Toolchain => PythonToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new PythonTracebackParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
