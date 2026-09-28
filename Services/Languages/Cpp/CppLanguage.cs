using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// C++, compiled with clang++, g++, or MSVC cl.exe and executed natively for source files,
/// supporting two-phase compilation, structured compiler diagnostics in Problems, and interactive standard input.
/// </summary>
public sealed class CppLanguage : LanguageDefinition
{
    private static readonly string[] CppAliases = ["cpp", "c++", "cc", "cplusplus"];
    private static readonly string[] CppExtensions = [".cpp", ".cc", ".cxx", ".hpp", ".h"];

    public CppLanguage(StudioLanguageServices services)
    {
        var toolchain = new CppToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings,
            Path.Combine(services.BaseDirectory, "cpp"));
        CppToolchain = toolchain;
        ScriptRunner = new CppBuildAndRunScriptRunner(services.Host);
        Packages = new CppPackageManager(toolchain, services.Processes, services.Host);
        Debugger = new CppDebuggerProvider(toolchain, services.Processes, services.Host, services.AdapterManager);
        NotebookKernels = new DelegateKernelFactory(context =>
            new CppNotebookKernel(toolchain, services.Processes, services.Host, context));
    }

    public CppToolchainProvider CppToolchain { get; }

    public override string Id => LanguageIds.Cpp;
    public override string DisplayName => "C++";
    public override JupyterLanguageInfo Jupyter { get; } = new("cpp", "C++", "c++", "text/x-c++src", ".cpp", "text/x-c++src");
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

    public override IEditorAssistantFactory EditorAssistants { get; } = CppEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => CppSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new CppIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new CppFoldingStrategy();

    public override IToolchainProvider Toolchain => CppToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDebuggerProvider? Debugger { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new ClangGccDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory? NotebookKernels { get; }
}
