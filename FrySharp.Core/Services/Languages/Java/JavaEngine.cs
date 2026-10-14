using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Java, built and run with the JDK installed on this computer: source files are compiled by <c>javac</c> and run by
/// <c>java</c>; notebook cells run in a JShell kernel (a program started with <c>java FryKernel.java</c> that speaks the Fry
/// kernel protocol), so what one cell declares the next one uses.
/// </summary>
public sealed class JavaEngine : LanguageEngine
{
    private static readonly string[] JavaAliases = ["java", "jdk"];
    private static readonly string[] JavaExtensions = [".java"];

    public JavaEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var toolchain = new JavaToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "java"));
        JavaToolchain = toolchain;
        ScriptRunner = new JavaBuildAndRunScriptRunner(services.Host);
        Packages = new MavenPackageManager(new MavenCentralResolver(services.Host), services.Host);

        var launcher = new JavaKernelLauncher(toolchain, services.Host, Path.Combine(services.BaseDirectory, "java", "kernel"));
        NotebookKernels = new DelegateKernelFactory(context =>
            new ProtocolKernel(LanguageIds.Java, "Java", launcher, services.Processes, context));
    }

    public JavaToolchainProvider JavaToolchain { get; }

    public override string Id => LanguageIds.Java;
    public override string DisplayName => "Java";
    public override JupyterLanguageInfo Jupyter { get; } = new("java", "Java", "java", "java", ".java", "text/x-java-source");
    public override string ShortName => "JAVA";
    public override IReadOnlyList<string> Aliases => JavaAliases;
    public override IReadOnlyList<string> FileExtensions => JavaExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override bool IsCompiled => true;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput | LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing | LanguageCapabilities.Debugging |
        LanguageCapabilities.Breakpoints | LanguageCapabilities.Packages |
        LanguageCapabilities.Completion;

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

    public override IToolchainProvider Toolchain => JavaToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new JavaCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
