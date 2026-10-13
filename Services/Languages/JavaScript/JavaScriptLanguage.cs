using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// JavaScript in the studio: <see cref="JavaScriptEngine"/> (Node.js, scripts and notebook kernels) with the editor's
/// highlighting, indentation and folding, and the debugger.
/// </summary>
public sealed class JavaScriptLanguage : EngineLanguageDefinition<JavaScriptEngine>
{
    public JavaScriptLanguage(StudioLanguageServices services)
        : base(new JavaScriptEngine(services.Engines))
    {
        Debugger = new JavaScriptDebuggerProvider(Engine.JavaScriptToolchain, services.Processes, services.Host, services.AdapterManager);
    }

    public JavaScriptToolchainProvider JavaScriptToolchain => Engine.JavaScriptToolchain;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => JavaScriptSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new JavaScriptIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new JavaScriptFoldingStrategy();

    public override IDebuggerProvider Debugger { get; }
}
