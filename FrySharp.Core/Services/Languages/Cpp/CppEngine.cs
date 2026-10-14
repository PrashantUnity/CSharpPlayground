using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// C++, compiled with clang++, g++ or MSVC <c>cl.exe</c> and run natively for source files: a two-phase build and run, the compiler's messages
/// in Problems, interactive standard input, and packages from vcpkg. Notebook cells are built as small programs and run, with what earlier
/// cells declared and made carried over (see <see cref="CppNotebookKernel"/>).
/// </summary>
public sealed class CppEngine : LanguageEngine
{
    private static readonly string[] CppAliases = ["cpp", "c++", "cc", "cplusplus"];
    private static readonly string[] CppExtensions = [".cpp", ".cc", ".cxx", ".hpp", ".h"];

    public CppEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        CppRoot = Path.Combine(services.BaseDirectory, "cpp");
        var toolchain = new CppToolchainProvider(services.Host, services.Processes, services.ToolchainSettings, CppRoot);
        CppToolchain = toolchain;
        ScriptRunner = new CppBuildAndRunScriptRunner(services.Host);
        Packages = new CppPackageManager(toolchain, services.Processes, services.Host);
        NotebookKernels = new DelegateKernelFactory(context =>
            new CppNotebookKernel(toolchain, services.Processes, services.Host, CppRoot, context));
    }

    /// <summary>Where the engine keeps what it builds: the staged headers and the notebooks' cells.</summary>
    public string CppRoot { get; }

    public CppToolchainProvider CppToolchain { get; }

    public override string Id => LanguageIds.Cpp;
    public override string DisplayName => "C++";
    public override JupyterLanguageInfo Jupyter { get; } = new("cpp", "C++", "c++", "c++", ".cpp", "text/x-c++src");
    public override string ShortName => "CPP";
    public override IReadOnlyList<string> Aliases => CppAliases;
    public override IReadOnlyList<string> FileExtensions => CppExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override bool IsCompiled => true;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput |
        LanguageCapabilities.QuickInfo |
        LanguageCapabilities.Completion |
        LanguageCapabilities.Debugging |
        LanguageCapabilities.Breakpoints |
        LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing |
        LanguageCapabilities.Packages;

    public override string IconKind => "LanguageCpp";
    public override string AccentHex => "#00599C";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "C++ (Clang / GCC / MSVC)";

    public override string NewFileTemplate =>
        "// Runs with the C++ compiler (clang++, g++, or cl.exe) installed on this computer (F5).\n" +
        "#include <iostream>\n\n" +
        "int main() {\n" +
        "    std::cout << \"Hello from C++20!\" << std::endl;\n" +
        "    return 0;\n" +
        "}\n";

    public override IToolchainProvider Toolchain => CppToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new ClangGccDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
