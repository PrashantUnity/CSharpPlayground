using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Java in the studio: <see cref="JavaEngine"/> (the JDK, source files and the JShell notebook kernel) with the editor's
/// syntax highlighting, completion, folding and indentation, and the debugger.
/// </summary>
public sealed class JavaLanguage : EngineLanguageDefinition<JavaEngine>
{
    public JavaLanguage(StudioLanguageServices services)
        : base(new JavaEngine(services.Engines))
    {
        Debugger = new JavaDebuggerProvider(Engine.JavaToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public JavaToolchainProvider JavaToolchain => Engine.JavaToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = JavaEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => JavaSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new JavaIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new JavaFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
