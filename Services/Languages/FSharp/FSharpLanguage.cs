using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// F# in the studio: <see cref="FSharpEngine"/> (F# Interactive, scripts and notebook kernels) with the editor's syntax
/// highlighting, completion, hover, folding and indentation.
/// </summary>
public sealed class FSharpLanguage : EngineLanguageDefinition<FSharpEngine>
{
    public FSharpLanguage(StudioLanguageServices services)
        : base(new FSharpEngine(services.Engines))
    {
    }

    public FSharpToolchainProvider FSharpToolchain => Engine.FSharpToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = FSharpEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => FSharpSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new FSharpIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new FSharpFoldingStrategy();

    public override IDebuggerProvider? Debugger => null;
}
