using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Rust in the studio: <see cref="RustEngine"/> (Cargo builds, scripts and the notebook kernel) with the editor's syntax
/// highlighting, static completion and hover, folding and indentation, and the <c>lldb-dap</c> debugger.
/// </summary>
public sealed class RustLanguage : EngineLanguageDefinition<RustEngine>
{
    public RustLanguage(StudioLanguageServices services)
        : base(new RustEngine(services.Engines))
    {
        Debugger = new RustDebuggerProvider(Engine.RustRoot, Engine.RustToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public RustToolchainProvider RustToolchain => Engine.RustToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = RustEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => RustSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new BraceIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new RustFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
