using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Java, compiled with javac and executed by java for source files, and run in a stateful JShell kernel
/// speaking the Fry kernel protocol for notebook cells and cross-language variable sharing.
/// </summary>
public sealed class JavaLanguage : LanguageDefinition
{
    private static readonly string[] JavaAliases = ["java", "jdk"];
    private static readonly string[] JavaExtensions = [".java"];

    public JavaLanguage(StudioLanguageServices services)
    {
        var toolchain = new JavaToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "java"));
        JavaToolchain = toolchain;
        ScriptRunner = new JavaBuildAndRunScriptRunner(services.Host);
        Debugger = new JavaDebuggerProvider(toolchain, services.Processes, services.Host, services.AdapterManager);

        var launcher = new JavaKernelLauncher(toolchain, services.Host, Path.Combine(services.BaseDirectory, "java", "kernel"));
        NotebookKernels = new DelegateKernelFactory(context =>
            new ProtocolKernel(LanguageIds.Java, "Java", launcher, services.Processes, context));
    }

    public JavaToolchainProvider JavaToolchain { get; }

    public override string Id => LanguageIds.Java;
    public override string DisplayName => "Java";
    public override JupyterLanguageInfo Jupyter { get; } = new("java", "Java", "java", "text/x-java-source", ".java", "text/x-java-source");
    public override string ShortName => "JAVA";
    public override IReadOnlyList<string> Aliases => JavaAliases;
    public override IReadOnlyList<string> FileExtensions => JavaExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput | LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing | LanguageCapabilities.Debugging | LanguageCapabilities.Breakpoints;

    public override string IconKind => "LanguageJava";
    public override string AccentHex => "#EA2D2E";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "Java (JDK)";

    public override string NewFileTemplate =>
        "// Runs with the Java Development Kit (JDK) installed on this computer (F5).\n" +
        "public class Main {\n" +
        "    public static void main(String[] args) {\n" +
        "        System.out.println(\"Hello from Java \" + System.getProperty(\"java.version\") + \"!\");\n" +
        "    }\n" +
        "}\n";

    public override IHighlightingDefinition GetHighlighting(bool isDark) => JavaSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new JavaIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new JavaFoldingStrategy();

    public override IToolchainProvider Toolchain => JavaToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDebuggerProvider? Debugger { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new JavaCompilerDiagnosticParser();
    public override INotebookKernelFactory NotebookKernels { get; }
}
