using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;

/// <summary>
/// C#, the studio's own language: compiled and run in-process by Roslyn. It has no toolchain or script runner; a host runs
/// it on its built-in path (<see cref="Execution.ScriptExecutionEngine"/>), and notebook cells keep their state in a
/// <see cref="NotebookExecutionKernel"/>.
/// </summary>
public sealed class CSharpEngine : LanguageEngine
{
    private static readonly string[] CSharpAliases = ["c#", "cs"];
    private static readonly string[] CSharpExtensions = [".frycs"];
    private static readonly INotebookKernelFactory Kernels = new DelegateKernelFactory(_ => new NotebookExecutionKernel());

    public override string Id => LanguageIds.CSharp;
    public override string DisplayName => "C#";
    public override JupyterLanguageInfo Jupyter { get; } = new(".net-csharp", ".NET (C#)", "C#", "csharp", ".cs", "text/x-csharp", "13.0");
    public override string ShortName => "C#";
    public override IReadOnlyList<string> Aliases => CSharpAliases;
    public override IReadOnlyList<string> FileExtensions => CSharpExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.FryDocument;

    public override IPackageManager Packages { get; } = new NuGetPackageManager();
    public override IDiagnosticParser RunDiagnostics { get; } = new CSharpCompilerDiagnosticParser();

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.Completion | LanguageCapabilities.QuickInfo | LanguageCapabilities.Formatting |
        LanguageCapabilities.Debugging | LanguageCapabilities.Breakpoints | LanguageCapabilities.LiveDiagnostics |
        LanguageCapabilities.TestCases | LanguageCapabilities.Templates | LanguageCapabilities.ExecutionModes |
        LanguageCapabilities.NotebookCells | LanguageCapabilities.ValueSharing |
        LanguageCapabilities.StandardInput | LanguageCapabilities.Packages;

    public override string IconKind => "LanguageCsharp";
    public override string AccentHex => "#58A6FF";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "C# (.NET 10 Roslyn)";
    public override string NewFileTemplate => "// Write C# Statements or top-level code here\nConsole.WriteLine(\"Hello from FryPDF!\");";

    public override INotebookKernelFactory NotebookKernels => Kernels;
}
