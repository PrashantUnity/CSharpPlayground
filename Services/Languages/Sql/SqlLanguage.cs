using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// SQL in the studio: <see cref="SqlEngine"/> (the SQLite 3 CLI, scripts and notebook kernels) with the editor's syntax
/// highlighting, completion, hover, folding and indentation.
/// </summary>
public sealed class SqlLanguage : EngineLanguageDefinition<SqlEngine>
{
    public SqlLanguage(StudioLanguageServices services)
        : base(new SqlEngine(services.Engines))
    {
    }

    public SqlToolchainProvider SqlToolchain => Engine.SqlToolchain;

    public override IEditorAssistantFactory EditorAssistants { get; } = SqlEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => SqlSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new SqlIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new SqlFoldingStrategy();

    public override IDebuggerProvider? Debugger => null;
}
