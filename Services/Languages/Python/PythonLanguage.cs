using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Python in the studio: <see cref="PythonEngine"/> (the Python installed on this computer, scripts and notebook kernels)
/// with the editor's highlighting, indentation and folding, and the debugger.
/// </summary>
public sealed class PythonLanguage : EngineLanguageDefinition<PythonEngine>
{
    public PythonLanguage(StudioLanguageServices services)
        : base(new PythonEngine(services.Engines))
    {
        Debugger = new PythonDebuggerProvider(Engine.PythonToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public PythonToolchainProvider PythonToolchain => Engine.PythonToolchain;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => PythonSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new PythonIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new PythonFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
