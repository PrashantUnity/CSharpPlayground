using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Indentation.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

/// <summary>
/// C# in the studio: <see cref="CSharpEngine"/> (Roslyn, run in-process) with completion, hover, live diagnostics,
/// debugging and the editor's highlighting, indentation and folding.
/// </summary>
public sealed class CSharpLanguage : EngineLanguageDefinition<CSharpEngine>
{
    public CSharpLanguage(IDebuggerProvider? debugger = null)
        : base(new CSharpEngine())
    {
        var compiler = new RoslynCompilerService();
        var exec = new ScriptExecutionEngine();
        Debugger = debugger ?? new CSharpDebuggerProvider(
            compiler,
            new ScriptDebuggerService(compiler, exec),
            exec,
            new ProcessLauncher(),
            new HostEnvironment());
    }

    public override IDebuggerProvider Debugger { get; }

    public override IHighlightingDefinition GetHighlighting(bool isDark) =>
        isDark ? CSharpSyntaxHighlightingTheme.GetDarkTheme() : CSharpSyntaxHighlightingTheme.GetLightTheme();

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new CSharpIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new BraceFolding();

    private sealed class BraceFolding : ILanguageFolding
    {
        private readonly CSharpFoldingStrategy _strategy = new();

        public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset) =>
            _strategy.CreateNewFoldings(document, out firstErrorOffset);
    }
}
