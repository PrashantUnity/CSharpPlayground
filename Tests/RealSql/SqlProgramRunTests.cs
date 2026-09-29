using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealSql;

/// <summary>.sql files executed with real sqlite3 CLI, plus notebook kernel integration.</summary>
[Collection(RealSqlCollection.Name)]
public class SqlProgramRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_SqlRun_" + Guid.NewGuid().ToString("N"));

    public SqlProgramRunTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private async Task<(ScriptRunSession Session, ConcurrentQueue<string> Output)> Start(string path)
    {
        var sqlite = TestSql.Require();
        var services = TestSql.Services(Path.Combine(_dir, ".studio"));
        var language = (SqlLanguage)services.Registry.Get(LanguageIds.Sql)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, sqlite));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [SqlFact]
    public void TheInstalledSqlite3_IsFound()
    {
        var sqlite = TestSql.Require();

        Assert.True(sqlite.Version >= SqlToolchainProvider.MinimumVersion, sqlite.Label);
        Assert.True(File.Exists(sqlite.ExecutablePath), sqlite.ExecutablePath);
    }

    [SqlFact]
    public async Task AFile_CreatesTableAndSelectsRows()
    {
        var path = Write("hello.sql", """
            CREATE TABLE IF NOT EXISTS greetings (
                id   INTEGER PRIMARY KEY,
                msg  TEXT NOT NULL
            );
            INSERT INTO greetings (msg) VALUES ('Hello from SQL run test!');
            INSERT INTO greetings (msg) VALUES ('SQLite works great!');
            SELECT id, msg FROM greetings;
            """);

        var (session, output) = await Start(path);
        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Join("\n", output);
        Assert.Contains("Hello from SQL run test!", joined);
        Assert.Contains("SQLite works great!", joined);
    }

    [SqlFact]
    public async Task AFile_SupportsAggregateQueries()
    {
        var path = Write("aggregate.sql", """
            CREATE TABLE scores (player TEXT, score INTEGER);
            INSERT INTO scores VALUES ('Alice', 95);
            INSERT INTO scores VALUES ('Bob',   80);
            INSERT INTO scores VALUES ('Carol', 90);
            SELECT COUNT(*) AS total, MAX(score) AS top, AVG(score) AS avg FROM scores;
            """);

        var (session, output) = await Start(path);
        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Join("\n", output);
        Assert.Contains("3", joined);   // COUNT = 3
        Assert.Contains("95", joined);  // MAX = 95
    }

    [SqlFact]
    public async Task SyntaxError_IsParsedIntoDiagnostics()
    {
        var path = Write("broken.sql", """
            CREATE TABLE t (id INTEGER, name TEXT);
            SELEC * FORM t;
            """);

        var (session, output) = await Start(path);
        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Diagnostics.Diagnostics);
        Assert.Contains(result.Diagnostics.Diagnostics, d =>
            d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [SqlFact]
    public async Task DatabaseDirective_UsesSpecifiedDatabase()
    {
        var dbPath = Path.Combine(_dir, "mydata.db");
        var path = Write("directive.sql", $"""
            -- :db {dbPath}
            CREATE TABLE IF NOT EXISTS items (name TEXT);
            INSERT INTO items VALUES ('directive_test');
            SELECT name FROM items;
            """);

        var (session, output) = await Start(path);
        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Join("\n", output);
        Assert.Contains("directive_test", joined);
        // DB file should have been created at the specified path
        Assert.True(File.Exists(dbPath));
    }

    [SqlFact]
    public async Task NotebookKernel_ExecutesCellAndQueryResults()
    {
        var sqlite = TestSql.Require();
        var services = TestSql.Services(Path.Combine(_dir, ".studio"));
        var language = (SqlLanguage)services.Registry.Get(LanguageIds.Sql)!;

        var context = new KernelCreationContext(() => _dir, () => null);
        using var kernel = (SqlNotebookKernel)language.NotebookKernels.Create(context);

        var consoleOut = new List<string>();
        var req1 = new KernelExecutionRequest
        {
            Code = """
                CREATE TABLE IF NOT EXISTS products (
                    id   INTEGER PRIMARY KEY,
                    name TEXT    NOT NULL,
                    qty  INTEGER DEFAULT 0
                );
                INSERT INTO products (name, qty) VALUES ('Apples', 50);
                INSERT INTO products (name, qty) VALUES ('Bananas', 30);
                SELECT name, qty FROM products ORDER BY name;
                """,
            OnConsole = s => consoleOut.Add(s)
        };

        var res = await kernel.ExecuteAsync(req1, CancellationToken.None);

        Assert.True(res.Success, res.ErrorMessage ?? res.ConsoleOutput);
        var joined = string.Join("", consoleOut);
        Assert.Contains("Apples", joined);
        Assert.Contains("Bananas", joined);
    }

    [SqlFact]
    public async Task NotebookKernel_AccumulatesTablesAcrossCells()
    {
        var sqlite = TestSql.Require();
        var services = TestSql.Services(Path.Combine(_dir, ".studio"));
        var language = (SqlLanguage)services.Registry.Get(LanguageIds.Sql)!;

        var context = new KernelCreationContext(() => _dir, () => null);
        using var kernel = (SqlNotebookKernel)language.NotebookKernels.Create(context);

        // Cell 1: create + insert
        var req1 = new KernelExecutionRequest
        {
            Code = """
                CREATE TABLE accumtest (val INTEGER);
                INSERT INTO accumtest VALUES (100);
                """,
            OnConsole = _ => { }
        };
        var res1 = await kernel.ExecuteAsync(req1, CancellationToken.None);
        Assert.True(res1.Success, res1.ErrorMessage);

        // Cell 2: query from the same DB session
        var consoleOut = new List<string>();
        var req2 = new KernelExecutionRequest
        {
            Code = "SELECT val FROM accumtest;",
            OnConsole = s => consoleOut.Add(s)
        };
        var res2 = await kernel.ExecuteAsync(req2, CancellationToken.None);

        Assert.True(res2.Success, res2.ErrorMessage);
        Assert.Contains("100", string.Join("", consoleOut));
    }

    [SqlFact]
    public async Task NotebookKernel_SharesVariableOutAsJson()
    {
        var sqlite = TestSql.Require();
        var services = TestSql.Services(Path.Combine(_dir, ".studio"));
        var language = (SqlLanguage)services.Registry.Get(LanguageIds.Sql)!;

        var context = new KernelCreationContext(() => _dir, () => null);
        using var kernel = (SqlNotebookKernel)language.NotebookKernels.Create(context);

        // Create a table to share
        await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "CREATE TABLE colors (name TEXT); INSERT INTO colors VALUES ('red'); INSERT INTO colors VALUES ('blue');",
            OnConsole = _ => { }
        }, CancellationToken.None);

        var json = await kernel.GetValueJsonAsync("colors", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("red", json);
        Assert.Contains("blue", json);
    }
}
