using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Go in the studio: <see cref="GoEngine"/> (builds, scripts, the notebook kernel and packages) with the editor's syntax highlighting,
/// static completion and hover, folding and indentation, and the Delve debugger.
/// </summary>
public sealed class GoLanguage : EngineLanguageDefinition<GoEngine>
{
    public GoLanguage(StudioLanguageServices services)
        : base(new GoEngine(services.Engines))
    {
        Debugger = new GoDebuggerProvider(Engine.GoToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public GoToolchainProvider GoToolchain => Engine.GoToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = GoEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => GoSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new GoIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new GoFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
