using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// F# language module: executes F# scripts (.fsx) via F# Interactive (<c>dotnet fsi</c>) with full
/// interactive standard input, structured compiler diagnostics in Problems, rich Results (.DUMP) output,
/// and polyglot interactive notebook cells.
/// </summary>
public sealed class FSharpLanguage : LanguageDefinition
{
    private static readonly string[] FSharpAliases = ["fsharp", "fs", "fsx"];
    private static readonly string[] FSharpExtensions = [".fsx", ".fs"];

    public FSharpLanguage(StudioLanguageServices services)
    {
        var toolchain = new FSharpToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings);
        FSharpToolchain = toolchain;
        ScriptRunner = new FSharpScriptRunner();
        Packages = new FSharpPackageManager();
        NotebookKernels = new DelegateKernelFactory(context =>
            new FSharpNotebookKernel(toolchain, services.Processes, services.Host, context));
    }

    public FSharpToolchainProvider FSharpToolchain { get; }

    public override string Id => LanguageIds.FSharp;
    public override string DisplayName => "F#";
    public override JupyterLanguageInfo Jupyter { get; } = new("fsharp", "F#", "fsharp", "fsharp", ".fsx", "text/x-fsharp");
    public override string ShortName => "FS";
    public override IReadOnlyList<string> Aliases => FSharpAliases;
    public override IReadOnlyList<string> FileExtensions => FSharpExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override bool IsCompiled => false;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput |
        LanguageCapabilities.QuickInfo |
        LanguageCapabilities.Completion |
        LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing |
        LanguageCapabilities.Packages |
        LanguageCapabilities.Formatting;

    public override string IconKind => "FunctionVariant";
    public override string AccentHex => "#30B9DB";
    public override string LineCommentPrefix => "//";
    public override string RuntimeDescription => "F# (.NET 10 fsi)";

    public override string NewFileTemplate =>
        "// Runs with F# Interactive (.NET SDK dotnet fsi) on this computer (F5).\n" +
        "open System\n\n" +
        "printfn \"🚀 Hello from F# in FrySharp!\"\n\n" +
        "let numbers = [ 1 .. 10 ]\n" +
        "let sumOfSquares =\n" +
        "    numbers\n" +
        "    |> List.map (fun x -> x * x)\n" +
        "    |> List.sum\n\n" +
        "printfn \"Sum of squares: %d\" sumOfSquares\n";

    public override IEditorAssistantFactory EditorAssistants { get; } = FSharpEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => FSharpSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new FSharpIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new FSharpFoldingStrategy();

    public override IToolchainProvider Toolchain => FSharpToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDebuggerProvider? Debugger => null;
    public override IDiagnosticParser RunDiagnostics { get; } = new FSharpCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
