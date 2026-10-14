using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// C++ in the studio: <see cref="CppEngine"/> (builds, scripts, the notebook kernel and packages) with the editor's syntax highlighting,
/// static completion and hover, folding and indentation, and the <c>lldb-dap</c> debugger.
/// </summary>
public sealed class CppLanguage : EngineLanguageDefinition<CppEngine>
{
    public CppLanguage(StudioLanguageServices services)
        : base(new CppEngine(services.Engines))
    {
        Debugger = new CppDebuggerProvider(Engine.CppToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public CppToolchainProvider CppToolchain => Engine.CppToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = CppEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => CppSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new CppIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new CppFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
