using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>SQL cells run by the real <c>sqlite3</c>, through the engine a headless host has (no Avalonia).</summary>
public class SqlEngineTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly INotebookKernel _kernel;

    public SqlEngineTests()
    {
        var engine = new SqlEngine(LanguageEngineServices.Create(_folder.Path));
        _kernel = engine.NotebookKernels.Create(new KernelCreationContext(() => _folder.Path));
    }

    public void Dispose()
    {
        _kernel.Dispose();
        _folder.Dispose();
    }

    /// <summary>What a cell said, in the order it said it: <c>table:Title</c> and <c>text:...</c>.</summary>
    private sealed record Ran(KernelExecutionResult Result, List<string> Said, List<DumpTableResult> Tables)
    {
        public string Text => string.Concat(Said.Where(s => s.StartsWith("text:", StringComparison.Ordinal)).Select(s => s[5..]));
    }

    private async Task<Ran> RunAsync(string code, CancellationToken ct = default)
    {
        var said = new List<string>();
        var tables = new List<DumpTableResult>();
        var result = await _kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            OnConsole = text => said.Add("text:" + text),
            OnRichOutput = output =>
            {
                Assert.Equal(CellOutputKind.Table, output.Kind);
                said.Add("table:" + output.TableResult!.Title);
                tables.Add(output.TableResult);
            }
        }, ct);
        return new Ran(result, said, tables);
    }

    [RealSqliteFact]
    public async Task ASelect_IsATable_NotText()
    {
        var ran = await RunAsync("""
            create table people (id integer primary key, name text, age integer);
            insert into people (name, age) values ('Ada', 36), ('Linus', 55);
            select id, name, age from people order by id;
            """);

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage);
        var table = Assert.Single(ran.Tables);
        Assert.Equal(["id", "name", "age"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, false, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["Ada", "Linus"], table.Rows.Select(r => r.Cells[1].DisplayText));
        Assert.Equal(string.Empty, ran.Text);
    }

    [RealSqliteFact]
    public async Task SeveralResultSets_AreNumbered_AndTheTextBetweenThemKeepsItsPlace()
    {
        var ran = await RunAsync("""
            select 1 as a;
            .print middle
            select 2 as b;
            """);

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage);
        Assert.Equal(["table:Result 1", "text:middle\n", "table:Result 2"], ran.Said);
    }

    [RealSqliteFact]
    public async Task AStatementWithoutRows_ShowsNothing_AndSucceeds()
    {
        var ran = await RunAsync("create table empty (x); select * from empty;");

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage);
        Assert.Empty(ran.Said);
    }

    [RealSqliteFact]
    public async Task WhatACellMade_IsThereForTheNextCell()
    {
        await RunAsync("create table kept (v integer); insert into kept values (100);");

        var ran = await RunAsync("select v from kept;");

        Assert.Equal("100", Assert.Single(Assert.Single(ran.Tables).Rows).Cells[0].DisplayText);
    }

    [RealSqliteFact]
    public async Task AnError_IsMarkedOnTheLineOfTheCell_AndTheCellFails()
    {
        var ran = await RunAsync("select 1 as ok;\n\nselect * from nosuch;\nselect 2 as later;");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics);
        Assert.Equal(3, error.Line);
        Assert.Contains("no such table", error.Message);
        Assert.Contains("no such table", ran.Text); // and it is said in the output too
        Assert.Equal(2, ran.Tables.Count); // the statements around it ran
    }

    [RealSqliteFact]
    public async Task ADirectiveLine_KeepsItsNumber_SoAnErrorStillPointsAtTheRightLine()
    {
        var ran = await RunAsync("#!share --from csharp rows\nselect * from nosuch;");

        Assert.False(ran.Result.Success);
        Assert.Equal(2, Assert.Single(ran.Result.Diagnostics).Line);
    }

    [RealSqliteFact]
    public async Task AnError_IsOnTheCellsLine_EvenWhenTablesWereSharedInFrontOfIt()
    {
        await _kernel.SetValueFromJsonAsync("rows", """[{"a":1},{"a":2}]""", CancellationToken.None);

        var ran = await RunAsync("\n\nselect * from nosuch;");

        Assert.False(ran.Result.Success);
        Assert.Equal(3, Assert.Single(ran.Result.Diagnostics).Line);
    }

    [RealSqliteFact]
    public async Task ASharedList_BecomesATable_ASharedValueATableOfOneRow()
    {
        await _kernel.SetValueFromJsonAsync("rows", """[{"name":"Ada","age":36},{"name":"Linus","age":55}]""", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("answer", "42", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("greeting", "\"it's\"", CancellationToken.None);

        var ran = await RunAsync("select * from rows; select value from answer; select value from greeting;");

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Text);
        Assert.Equal(["Ada", "Linus"], ran.Tables[0].Rows.Select(r => r.Cells[0].DisplayText));
        Assert.Equal("42", ran.Tables[1].Rows[0].Cells[0].DisplayText);
        Assert.Equal("it's", ran.Tables[2].Rows[0].Cells[0].DisplayText);
    }

    [RealSqliteFact]
    public async Task ANameWithAQuoteInIt_IsAnIdentifier_NotCode()
    {
        await _kernel.SetValueFromJsonAsync("we\"ird", "[1,2,3]", CancellationToken.None);

        var ran = await RunAsync("select count(*) as n from \"we\"\"ird\";");

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Text);
        Assert.Equal("3", ran.Tables.Single().Rows[0].Cells[0].DisplayText);
    }

    [RealSqliteFact]
    public async Task ATableCanBeSharedOut_AsJson_EvenWhenItIsEmpty()
    {
        var made = await RunAsync("create table colors (name text); insert into colors values ('red'), ('blue'); create table blank (x);");
        Assert.True(made.Result.Success, made.Result.ErrorMessage + made.Text);

        var json = await _kernel.GetValueJsonAsync("colors", CancellationToken.None);
        var empty = await _kernel.GetValueJsonAsync("blank", CancellationToken.None);

        Assert.Contains("red", json);
        Assert.Contains("blue", json);
        Assert.Equal("[]", empty);
        await Assert.ThrowsAsync<KernelValueException>(() => _kernel.GetValueJsonAsync("missing", CancellationToken.None));
    }

    [RealSqliteFact]
    public async Task TheTablesAreTheVariables()
    {
        await RunAsync("create table one (x); create table two (y);");

        var variables = await _kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Equal(["one", "two"], variables.Select(v => v.Name).Order());
        Assert.All(variables, v => Assert.Equal("Table", v.TypeName));
    }

    [RealSqliteFact]
    public async Task AReset_ForgetsTheTables()
    {
        await RunAsync("create table gone (x);");

        ((SqlNotebookKernel)_kernel).HardReset();
        var variables = await _kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Empty(variables);
    }

    [RealSqliteFact]
    public async Task StopEndsACellThatNeverEnds_AndSqliteGoesWithIt()
    {
        using var stop = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var running = RunAsync("with recursive c(x) as (select 1 union all select x + 1 from c) select count(*) from c;", stop.Token);

        await Task.Delay(500);
        await stop.CancelAsync();
        var ran = await running;

        Assert.True(ran.Result.WasCancelled);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), clock.Elapsed.ToString());
        // and the next cell runs: nothing is left holding the database
        var next = await RunAsync("select 1 as fine;");
        Assert.True(next.Result.Success, next.Result.ErrorMessage);
    }

    [Fact]
    public void TheEngine_IsOneAHeadlessHostHas()
    {
        using var folder = new TempFolder();
        var languages = HeadlessLanguages.Create(LanguageEngineServices.Create(folder.Path));

        var sql = languages.Get(LanguageIds.Sql);

        Assert.NotNull(sql);
        Assert.True(sql.Has(LanguageCapabilities.NotebookCells | LanguageCapabilities.ValueSharing));
        Assert.Same(sql, languages.FindSourceFileLanguage("report.sql"));
        Assert.Same(sql, languages.Get("sqlite"));
    }
}

/// <summary>A machine with nothing installed: what a SQL cell says when there is no sqlite3 to run it.</summary>
public class SqlWithoutSqliteTests : IDisposable
{
    private sealed class BareHost(bool windows) : IHostEnvironment
    {
        public bool IsWindows => windows;
        public bool IsMacOS => !windows;
        public bool IsLinux => false;
        public string HomeDirectory => Path.GetTempPath();
        public string? GetEnvironmentVariable(string name) => null;
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public IReadOnlyList<string> GetDirectories(string path) => [];
        public Task<string?> GetLoginShellPathAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult(-1, string.Empty, "not found", false));
    }

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private INotebookKernel KernelOn(bool windows)
    {
        var services = new LanguageEngineServices(_folder.Path, new BareHost(windows), new ProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_folder.Path, "toolchains.json")));
        return new SqlEngine(services).NotebookKernels.Create(new KernelCreationContext(() => _folder.Path));
    }

    [Theory]
    [InlineData(true, "winget install SQLite.SQLite")]
    [InlineData(false, "brew install sqlite")]
    public async Task ACellWithoutSqlite_SaysHowToGetIt_ForTheMachineItIsOn_AndFails(bool windows, string step)
    {
        using var kernel = KernelOn(windows);
        var said = new List<string>();

        var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = "select 1;", OnConsole = said.Add }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("sqlite3", result.ErrorMessage);
        Assert.Contains(step, string.Concat(said));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task WithoutSqlite_ThereAreNoVariables_AndNothingToShare()
    {
        using var kernel = KernelOn(windows: false);

        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("anything", CancellationToken.None));
    }
}
