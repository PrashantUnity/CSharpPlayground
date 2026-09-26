using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Indentation.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// C#, the studio's own language: compiled and run inside the studio by Roslyn, with completion, hover, debugging, test
/// cases and live diagnostics. It has no toolchain or script runner; the Code Studio runs it on its built-in path.
/// </summary>
public sealed class CSharpLanguage : LanguageDefinition
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

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.Completion | LanguageCapabilities.QuickInfo | LanguageCapabilities.Formatting |
        LanguageCapabilities.Debugging | LanguageCapabilities.Breakpoints | LanguageCapabilities.LiveDiagnostics |
        LanguageCapabilities.TestCases | LanguageCapabilities.Templates | LanguageCapabilities.ExecutionModes |
        LanguageCapabilities.NotebookCells | LanguageCapabilities.ValueSharing;

    public override string IconKind => "FileCodeOutline";
    public override string AccentHex => "#58A6FF";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "C# (.NET 10 Roslyn)";
    public override string NewFileTemplate => "// Write C# Statements or top-level code here\nConsole.WriteLine(\"Hello from FryPDF!\");";

    public override IHighlightingDefinition GetHighlighting(bool isDark) =>
        isDark ? CSharpSyntaxHighlightingTheme.GetDarkTheme() : CSharpSyntaxHighlightingTheme.GetLightTheme();

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new CSharpIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new BraceFolding();

    public override INotebookKernelFactory NotebookKernels => Kernels;

    private sealed class BraceFolding : ILanguageFolding
    {
        private readonly CSharpFoldingStrategy _strategy = new();

        public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset) =>
            _strategy.CreateNewFoldings(document, out firstErrorOffset);
    }
}
