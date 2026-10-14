using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// F#, run by the .NET SDK's F# Interactive (<c>dotnet fsi</c>): <c>.fsx</c> files run like <c>dotnet fsi file.fsx</c>; notebook
/// cells run in a kernel process whose evaluation session keeps what they define, shows the value of a cell's last
/// expression, and shares values with the other languages' kernels.
/// </summary>
public sealed class FSharpEngine : LanguageEngine
{
    private static readonly string[] FSharpAliases = ["fsharp", "fs", "fsx"];
    private static readonly string[] FSharpExtensions = [".fsx", ".fs"];

    public FSharpEngine(LanguageEngineServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var toolchain = new FSharpToolchainProvider(services.Host, services.Processes, services.ToolchainSettings);
        FSharpToolchain = toolchain;
        ScriptRunner = new FSharpScriptRunner();
        Packages = new FSharpPackageManager();

        // Each notebook gets its own kernel program, started with its first F# cell.
        var launcher = new FSharpKernelLauncher(toolchain, services.Host, Path.Combine(services.BaseDirectory, "fsharp", "kernel"));
        NotebookKernels = new DelegateKernelFactory(context =>
            new ProtocolKernel(LanguageIds.FSharp, "F#", launcher, services.Processes, context));
    }

    public FSharpToolchainProvider FSharpToolchain { get; }

    public override string Id => LanguageIds.FSharp;
    public override string DisplayName => "F#";
    public override JupyterLanguageInfo Jupyter { get; } = new("fsharp", "F#", "fsharp", "fsharp", ".fsx", "text/x-fsharp");
    public override string ShortName => "FS";
    public override IReadOnlyList<string> Aliases => FSharpAliases;
    public override IReadOnlyList<string> FileExtensions => FSharpExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;

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

    public override IToolchainProvider Toolchain => FSharpToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDiagnosticParser RunDiagnostics { get; } = new FSharpCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
