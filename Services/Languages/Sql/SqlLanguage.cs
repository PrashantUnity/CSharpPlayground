using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Indentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// SQL language module: executes SQL scripts (.sql) and polyglot notebook cells via SQLite CLI (sqlite3)
/// with formatted table output, interactive stdin streaming, syntax highlighting, and cross-kernel variable sharing.
/// </summary>
public sealed class SqlLanguage : LanguageDefinition
{
    private static readonly string[] SqlAliases = ["sql", "sqlite", "sqlite3"];
    private static readonly string[] SqlExtensions = [".sql"];

    public SqlLanguage(StudioLanguageServices services)
    {
        var toolchain = new SqlToolchainProvider(
            services.Host,
            services.Processes,
            services.ToolchainSettings);
        SqlToolchain = toolchain;
        ScriptRunner = new SqlScriptRunner();
        Packages = new SqlPackageManager();
        NotebookKernels = new DelegateKernelFactory(context =>
            new SqlNotebookKernel(toolchain, services.Processes, services.Host, context));
    }

    public SqlToolchainProvider SqlToolchain { get; }

    public override string Id => LanguageIds.Sql;
    public override string DisplayName => "SQL";
    public override JupyterLanguageInfo Jupyter { get; } = new("sql", "SQL", "sql", "application/sql", ".sql", "application/sql");
    public override string ShortName => "SQL";
    public override IReadOnlyList<string> Aliases => SqlAliases;
    public override IReadOnlyList<string> FileExtensions => SqlExtensions;
    public override LanguageStorageKind Storage => LanguageStorageKind.SourceFile;
    public override bool IsCompiled => false;

    public override LanguageCapabilities Capabilities =>
        LanguageCapabilities.StandardInput |
        LanguageCapabilities.QuickInfo |
        LanguageCapabilities.Completion |
        LanguageCapabilities.NotebookCells |
        LanguageCapabilities.ValueSharing |
        LanguageCapabilities.Packages |
        LanguageCapabilities.Formatting;

    public override string IconKind => "Database";
    public override string AccentHex => "#F29111";
    public override string LineCommentPrefix => "--";
    public override string RuntimeDescription => "SQLite 3 Engine";

    public override string NewFileTemplate =>
        "-- Runs with SQLite 3 (sqlite3) on this computer (F5).\n" +
        "-- :database :memory:\n\n" +
        "CREATE TABLE IF NOT EXISTS developers (\n" +
        "    id INTEGER PRIMARY KEY AUTOINCREMENT,\n" +
        "    name TEXT NOT NULL,\n" +
        "    role TEXT NOT NULL,\n" +
        "    rating REAL DEFAULT 5.0\n" +
        ");\n\n" +
        "INSERT INTO developers (name, role, rating) VALUES\n" +
        "    ('Antigravity', 'AI Architect', 5.0),\n" +
        "    ('Ada Lovelace', 'First Programmer', 5.0),\n" +
        "    ('Alan Turing', 'Computer Pioneer', 5.0);\n\n" +
        "SELECT \n" +
        "    id,\n" +
        "    name,\n" +
        "    role,\n" +
        "    printf('★ %.1f', rating) AS score\n" +
        "FROM developers\n" +
        "ORDER BY id ASC;\n";

    public override IEditorAssistantFactory EditorAssistants { get; } = SqlEditorAssistantFactory.Instance;

    public override IHighlightingDefinition GetHighlighting(bool isDark) => SqlSyntaxHighlighting.Get(isDark);

    public override IIndentationStrategy CreateIndentationStrategy(TextEditorOptions options) => new SqlIndentationStrategy(options);

    public override ILanguageFolding Folding { get; } = new SqlFoldingStrategy();

    public override IToolchainProvider Toolchain => SqlToolchain;
    public override IScriptRunner ScriptRunner { get; }
    public override IDebuggerProvider? Debugger => null;
    public override IDiagnosticParser RunDiagnostics { get; } = new SqlCompilerDiagnosticParser();
    public override IPackageManager Packages { get; }
    public override INotebookKernelFactory NotebookKernels { get; }
}
