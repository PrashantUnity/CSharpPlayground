using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A SQL script's <c>-- :database :memory:</c> (the starter template's line) runs in SQLite's in-memory database. It
/// was joined to the script's folder as a file name, so every run left a database file called ":memory:" in the
/// workspace; with no directive a quoted <c>""</c> reached sqlite3 as a file name too. Runs the real sqlite3.
/// </summary>
public class SqlScriptRunnerTests : IDisposable
{
    private const string Sqlite = "/usr/bin/sqlite3";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FryPDF_SqlRunner_" + Guid.NewGuid().ToString("N"));

    public SqlScriptRunnerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private static ToolchainInfo Toolchain() => new()
    {
        LanguageId = "sql",
        ExecutablePath = Sqlite,
        Version = new Version(3, 51, 0),
        DisplayName = "SQLite",
        Source = "test",
    };

    private async Task<(ScriptRunPlan Plan, string Output)> RunAsync(string script)
    {
        var path = Path.Combine(_folder, "query.sql");
        await File.WriteAllTextAsync(path, script);
        var plan = await new SqlScriptRunner().PlanAsync(new ScriptRunContext(path, _folder, Toolchain()));
        var spec = plan.Steps[^1].Spec;

        var start = new ProcessStartInfo(spec.FileName) { WorkingDirectory = spec.WorkingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in spec.Arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (plan, output);
    }

    private const string Query = "CREATE TABLE t (n INTEGER);\nINSERT INTO t VALUES (42);\nSELECT n FROM t;\n";

    [Theory]
    [InlineData("-- :database :memory:\n")]
    [InlineData("-- :db \":MEMORY:\"\n")]
    [InlineData("")] // no directive and no query.db next to it
    public async Task AnInMemoryScript_RunsInMemory_AndLeavesNoDatabaseFile(string directive)
    {
        if (!File.Exists(Sqlite)) return; // sqlite3 ships with macOS; elsewhere there is nothing to run

        var (plan, output) = await RunAsync(directive + Query);

        Assert.Contains(":memory:", plan.Steps[^1].Spec.Arguments);
        Assert.Contains("42", output);
        Assert.Equal("query.sql", Path.GetFileName(Assert.Single(Directory.GetFiles(_folder))));
    }

    [Fact]
    public async Task ANamedDatabase_IsAFileNextToTheScript()
    {
        if (!File.Exists(Sqlite)) return;

        var (_, output) = await RunAsync("-- :database data.db\n" + Query);

        Assert.Contains("42", output);
        Assert.True(File.Exists(Path.Combine(_folder, "data.db")));
    }
}
