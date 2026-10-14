using FrySharp.Sidecar.Protocol;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;

namespace FrySharp.Sidecar.Tests;

public sealed class SidecarTests : IAsyncLifetime
{
    private SidecarHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await SidecarHarness.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task Workspace_ListFolder_ReturnsEntries()
    {
        var testFile = Path.Combine(_harness.Workspace.Path, "hello.txt");
        await File.WriteAllTextAsync(testFile, "Hello world");

        var listing = await _harness.ClientRpc.InvokeWithParameterObjectAsync<FolderListing>(
            "fry/workspace/listFolder", new PathParams(""));

        Assert.NotNull(listing);
        Assert.Contains(listing.Entries, e => e.Name == "hello.txt" && !e.IsFolder);
    }

    [Fact]
    public async Task Notebook_OpenExecuteAndSave_Succeeds()
    {
        var notebook = new NotebookDocumentItem { Title = "Test Notebook", Kernel = "csharp" };
        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "cell-1",
            Type = CellType.Code,
            Source = "Console.WriteLine(\"Output from cell\");"
        });

        var nbPath = Path.Combine(_harness.Workspace.Path, "test.frynb");
        await File.WriteAllTextAsync(nbPath, NotebookFileFormat.WriteFrynb(notebook));

        var content = await _harness.ClientRpc.InvokeWithParameterObjectAsync<NotebookContent>(
            "fry/notebook/open", new PathParams("test.frynb"));

        Assert.NotNull(content);
        Assert.Single(content.Cells);
        Assert.Equal("cell-1", content.Cells[0].Id);

        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>(
            "fry/notebook/execute", new ExecuteCellParams("test.frynb", "cell-1", "Console.WriteLine(\"Output from cell\");"));

        Assert.True(result.Success);
        Assert.Equal(1, result.ExecutionCount);

        var saveResult = await _harness.ClientRpc.InvokeWithParameterObjectAsync<SaveResult>(
            "fry/notebook/save", new NotebookSaveParams("test.frynb"));

        Assert.NotNull(saveResult);
    }

    [Fact]
    public async Task Notebook_Execute_DisplayHtml_EmitsOutputNotification()
    {
        var notebook = new NotebookDocumentItem { Title = "Test Notebook", Kernel = "csharp" };
        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "cell-disp",
            Type = CellType.Code,
            Source = "Display.Html(\"<b>Hello HTML</b>\");"
        });

        var nbPath = Path.Combine(_harness.Workspace.Path, "disp.frynb");
        await File.WriteAllTextAsync(nbPath, NotebookFileFormat.WriteFrynb(notebook));

        var content = await _harness.ClientRpc.InvokeWithParameterObjectAsync<NotebookContent>(
            "fry/notebook/open", new PathParams("disp.frynb"));
        Assert.NotNull(content);

        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>(
            "fry/notebook/execute", new ExecuteCellParams("disp.frynb", "cell-disp", "Display.Html(\"<b>Hello HTML</b>\");"));

        Assert.True(result.Success);

        var outputNotif = _harness.Notifications
            .FirstOrDefault(n => n.Method == ClientMethods.NotebookOutput && n.Payload is NotebookOutputNotification outNotif && outNotif.Output.Kind == "display")
            .Payload as NotebookOutputNotification;

        Assert.NotNull(outputNotif);
        Assert.Equal("display", outputNotif.Output.Kind);
        Assert.NotNull(outputNotif.Output.Data);
    }

    [Fact]
    public async Task Run_CSharpScript_StreamsOutputAndExits()
    {
        var scriptPath = Path.Combine(_harness.Workspace.Path, "script.cs");
        await File.WriteAllTextAsync(scriptPath, "System.Console.WriteLine(\"Hello from script\");");

        var startResult = await _harness.ClientRpc.InvokeWithParameterObjectAsync<RunStartResult>(
            "fry/run/start", new RunStartParams("run-1", Path: "script.cs"));

        Assert.Equal("run-1", startResult.RunId);

        // Wait a short bit for the run to complete
        var tries = 0;
        IReadOnlyList<RunInfo> runs;
        do
        {
            await Task.Delay(50);
            runs = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<RunInfo>>(
                "fry/run/list", new EmptyParams());
            tries++;
        } while (runs.Any(r => r.Running) && tries < 40);

        var run = Assert.Single(runs);
        Assert.False(run.Running);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Hello from script", run.Output);
    }

    [Fact]
    public async Task Document_Completions_ReturnCSharpSuggestions()
    {
        var filePath = Path.Combine(_harness.Workspace.Path, "test.cs");
        await File.WriteAllTextAsync(filePath, "class C { void M() { System.Con } }");

        var doc = await _harness.ClientRpc.InvokeWithParameterObjectAsync<DocumentContent>(
            "fry/document/open", new PathParams("test.cs"));
        Assert.NotNull(doc);

        await _harness.ClientRpc.InvokeWithParameterObjectAsync(
            "fry/document/didOpen", new DidOpenParams("test.cs", 1, doc.Text, doc.DiskVersion));

        var offset = doc.Text.IndexOf("System.Con", StringComparison.Ordinal) + "System.Con".Length;
        var completions = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CompletionResult>(
            "fry/language/completion", new PositionParams("test.cs", 1, offset));

        Assert.NotNull(completions);
        Assert.Contains(completions.Items, c => c.Label == "Console");
    }

    // The bundle a VS Code notebook gets for an object: the Object Explorer's data, and the page that draws it until a
    // renderer for the data exists.
    private async Task<Newtonsoft.Json.Linq.JObject> LastObjectDisplayAsync(string notebook, string language, string source)
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>(
            "fry/notebook/execute", new ExecuteCellParams(notebook, "cell-1", source, language));
        Assert.True(result.Success);

        var display = _harness.Notifications
            .Select(n => n.Payload).OfType<NotebookOutputNotification>()
            .Single(n => n.Output.Kind == "display").Output;
        return Assert.IsType<Newtonsoft.Json.Linq.JObject>(display.Data);
    }

    private static void AssertObjectExplorerBundle(Newtonsoft.Json.Linq.JObject data, string title, string firstProperty)
    {
        var node = Assert.IsType<Newtonsoft.Json.Linq.JObject>(data[RichOutputMime.InspectorMime]);
        Assert.EndsWith(title, (string?)node["title"]); // (a C# script class is "Submission#3+People")
        Assert.Equal(firstProperty, (string?)node["properties"]![0]!["name"]);
        Assert.Contains("fry-inspector-card", (string?)data["text/html"]);
        Assert.NotNull(data["text/plain"]);
    }

    [Fact]
    public async Task Notebook_CSharp_LastObject_ArrivesAsAnObjectExplorerBundle()
    {
        var data = await LastObjectDisplayAsync("objects.frynb", "csharp",
            "public class People { public string Name { get; set; } = \"Code\"; }\nnew People()");

        AssertObjectExplorerBundle(data, "People", "Name");
    }

    [RealPythonFact]
    public async Task Notebook_Python_LastObject_ArrivesAsAnObjectExplorerBundle()
    {
        var data = await LastObjectDisplayAsync("objects-py.frynb", "python",
            "class People:\n    def __init__(self):\n        self.name = 'Code'\n\nPeople()");

        AssertObjectExplorerBundle(data, "People", "name");
    }

    // ── A notebook the client holds (VS Code): named by an id, not by a file in the workspace ─────────────────────────────

    // The reply can arrive before some of the run's notifications: a client waits for as many as the reply says there were (lastSeq),
    // as the extension does, so what a test reads afterwards is all of it and nothing is still being added.
    private async Task<ExecuteCellResult> RunHeldAsync(string notebookId, string source, string language = "csharp", string? workingDirectory = null)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>(
            "fry/notebook/execute", new ExecuteCellParams("", Guid.NewGuid().ToString("N"), source, language, notebookId, workingDirectory, executionId));

        Assert.True(SpinWait.SpinUntil(() => SeqNotificationsOf(executionId) >= result.LastSeq, TimeSpan.FromSeconds(30)), "the run's notifications did not all arrive");
        return result;
    }

    private int SeqNotificationsOf(string executionId)
    {
        lock (_harness.Notifications)
        {
            return _harness.Notifications.Count(n => n.Payload is NotebookOutputNotification o && o.ExecutionId == executionId
                || n.Payload is CellStateNotification c && c.ExecutionId == executionId);
        }
    }

    private string TextOutput() => string.Concat(_harness.Notifications
        .Select(n => n.Payload).OfType<NotebookOutputNotification>().Where(n => n.Output.Kind == "stream").Select(n => n.Output.Text));

    [Fact]
    public async Task HeldNotebook_UntitledOrNot_KeepsItsStateBetweenCells()
    {
        Assert.True((await RunHeldAsync("untitled-1", "int x = 41;")).Success);
        Assert.True((await RunHeldAsync("untitled-1", "Console.WriteLine(x + 1);")).Success);

        Assert.Contains("42", TextOutput());
    }

    [Fact]
    public async Task HeldNotebooks_WithTheSameFileName_ButDifferentIds_DoNotShareState()
    {
        // two "notebook.frynb" in different folders are two notebooks: the id says which, not the name
        Assert.True((await RunHeldAsync("file:///a/notebook.frynb", "int shared = 1;")).Success);

        var other = await RunHeldAsync("file:///b/notebook.frynb", "Console.WriteLine(shared);");

        Assert.False(other.Success);
    }

    [Fact]
    public async Task HeldNotebook_Restart_ForgetsItsVariables_AndVariablesAreListedById()
    {
        await RunHeldAsync("nb-restart", "int kept = 7;");
        var before = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>("fry/notebook/variables", new NotebookRef(NotebookId: "nb-restart"));
        Assert.Contains(before, v => v.Name == "kept");

        await _harness.ClientRpc.InvokeWithParameterObjectAsync("fry/notebook/restart", new NotebookRef(NotebookId: "nb-restart"));

        var after = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>("fry/notebook/variables", new NotebookRef(NotebookId: "nb-restart"));
        Assert.DoesNotContain(after, v => v.Name == "kept");
        Assert.False((await RunHeldAsync("nb-restart", "Console.WriteLine(kept);")).Success);
    }

    [Fact]
    public async Task HeldNotebook_Interrupt_StopsARunningCell()
    {
        var running = RunHeldAsync("nb-interrupt", "System.Threading.Thread.Sleep(30000);");
        await Task.Delay(1500);

        await _harness.ClientRpc.InvokeWithParameterObjectAsync("fry/notebook/interrupt", new NotebookRef(NotebookId: "nb-interrupt"));
        var result = await running.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task HeldNotebook_Close_ForgetsIt()
    {
        await RunHeldAsync("nb-close", "int a = 1;");
        await _harness.ClientRpc.InvokeWithParameterObjectAsync("fry/notebook/close", new NotebookRef(NotebookId: "nb-close"));

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>("fry/notebook/variables", new NotebookRef(NotebookId: "nb-close")));
        Assert.Contains("not open", error.Message);
    }

    [Fact]
    public async Task ARequestThatNamesNoNotebook_IsRefused()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            _harness.ClientRpc.InvokeWithParameterObjectAsync("fry/notebook/restart", new NotebookRef()));
        Assert.Contains("notebookId or path", error.Message);
    }

    [RealPythonFact]
    public async Task HeldNotebook_RunsInTheFolderItIsGiven_EvenOutsideTheWorkspace()
    {
        using var elsewhere = new FrySharp.Core.Tests.TempFolder();

        var result = await RunHeldAsync("nb-folder", "import os\nprint(os.getcwd())", "python", elsewhere.Path);

        Assert.True(result.Success, TextOutput());
        // (Python reports the real path: /private/var for /var on macOS, so compare the folder's own, unique name)
        Assert.EndsWith(Path.GetFileName(elsewhere.Path), TextOutput().Trim());
        Assert.DoesNotContain(_harness.Workspace.Path, TextOutput());
    }

    // ── What a run tells the client: numbered, in order, with where it went wrong ──────────────────────────────────────────

    private IEnumerable<NotebookOutputNotification> Outputs() => _harness.Notifications.Select(n => n.Payload).OfType<NotebookOutputNotification>();

    [Fact]
    public async Task Execute_NumbersEveryNotification_FromOne_AndTheReplySaysHowManyThereWere()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "c1", "Console.WriteLine(\"a\"); Display.Html(\"<b>b</b>\"); Console.WriteLine(\"c\");", "csharp", "nb-seq", ExecutionId: "run-1"));

        var seqs = _harness.Notifications.Select(n => n.Payload switch
        {
            NotebookOutputNotification o => (o.ExecutionId, o.Seq),
            CellStateNotification c => (c.ExecutionId, c.Seq),
            _ => (null, -1)
        }).Where(x => x.Item2 >= 0).ToList();

        Assert.All(seqs, x => Assert.Equal("run-1", x.Item1));
        Assert.Equal(Enumerable.Range(1, seqs.Count), seqs.Select(x => x.Item2)); // 1, 2, 3...: none missing, in order
        Assert.Equal(seqs.Count, result.LastSeq);
        var state = _harness.Notifications.Select(n => n.Payload).OfType<CellStateNotification>().ToList();
        Assert.True(state.First().Running);
        Assert.False(state.Last().Running); // the end is the last thing said
        Assert.Equal(result.LastSeq, state.Last().Seq);
    }

    [Fact]
    public async Task Execute_ALotOfOutput_ArrivesWhole_BeforeTheReply()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "c1", "for (var i = 0; i < 20000; i++) Console.WriteLine(\"line \" + i);", "csharp", "nb-flood", ExecutionId: "flood"));

        Assert.True(result.Success);
        var text = string.Concat(Outputs().Where(o => o.Output.Kind == "stream").Select(o => o.Output.Text));
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(20000, lines.Length);
        Assert.Equal("line 0", lines[0].Trim());
        Assert.Equal("line 19999", lines[^1].Trim());
        Assert.True(Outputs().Count() < 2000, "output is sent in batches, not a line at a time");
    }

    [Fact]
    public async Task Execute_ACompileError_SaysWhereInTheCell()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "c1", "int ok = 1;\nint bad = ;", "csharp", "nb-error", ExecutionId: "e1"));

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics!);
        Assert.Equal("error", diagnostic.Severity);
        Assert.StartsWith("CS", diagnostic.Code);
        Assert.Equal(2, diagnostic.StartLine);
    }

    [RealPythonFact]
    public async Task Execute_APythonError_NamesTheLineOfTheCell()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "c1", "x = 1\ny = 0\nz = x / y", "python", "nb-py-error", ExecutionId: "p1"));

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics!);
        Assert.Equal("ZeroDivisionError", diagnostic.Code);
        Assert.Equal(3, diagnostic.StartLine);
    }

    [Fact]
    public async Task Execute_ASuccessfulCell_HasNoDiagnostics()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "c1", "var a = 1;", "csharp", "nb-fine", ExecutionId: "f1"));

        Assert.True(result.Success);
        Assert.True(result.Diagnostics is null || result.Diagnostics.Count == 0);
    }

    [Fact]
    public async Task Execute_ACellThatReadsALine_AsksTheClient_WithWhichCellIsAsking()
    {
        _harness.InputAnswer = _ => "Ada";

        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "ask-cell", "var name = Console.ReadLine();\nConsole.WriteLine(\"Hello \" + name);", "csharp", "nb-input", ExecutionId: "i1"));

        Assert.True(result.Success);
        var request = Assert.Single(_harness.InputRequests);
        Assert.Equal("ask-cell", request.CellId);
        Assert.Equal("i1", request.ExecutionId);
        Assert.Equal("nb-input", request.Path);
        Assert.Contains("Hello Ada", string.Concat(Outputs().Where(o => o.Output.Kind == "stream").Select(o => o.Output.Text)));
    }

    [Fact]
    public async Task Execute_AChartTheProgramRedraws_IsSentAgainAsAnUpdateOfTheSameDisplay()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<ExecuteCellResult>("fry/notebook/execute",
            new ExecuteCellParams("", "chart", "var h = Display.LineChart(new[] { 1.0, 2.0 }, \"Before\");\nh.Update(s => s.Title = \"After\");", "csharp", "nb-update", ExecutionId: "u1"));

        Assert.True(result.Success);
        var displays = Outputs().Where(o => o.Output.Kind == "display").Select(o => o.Output).ToList();
        Assert.True(displays.Count >= 2, $"{displays.Count} display notification(s)");
        Assert.False(displays[0].Update);
        Assert.NotNull(displays[0].DisplayId);
        var update = displays.Last();
        Assert.True(update.Update);
        Assert.Equal(displays[0].DisplayId, update.DisplayId);
        Assert.Contains("After", update.Data!.ToString());
    }

    // ── Language features in a notebook cell: what the earlier cells define is in scope ──────────────────────────────────────

    private async Task<string[]> CompleteAsync(string source, string marker, string[]? context = null, string language = "csharp")
    {
        var offset = source.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CompletionResult>("fry/language/completeCell",
            new CellPositionParams(language, source, offset, context));
        return result.Items.Select(i => i.Label).ToArray();
    }

    [Fact]
    public async Task CellCompletion_OffersTheMembersOfAVariableAnEarlierCellDefined()
    {
        var labels = await CompleteAsync("greeting.", "greeting.", context: ["var greeting = \"hi\";"]);

        Assert.Contains("ToUpper", labels);
        Assert.Contains("Length", labels);
    }

    [Fact]
    public async Task CellCompletion_KnowsATypeAnEarlierCellDeclared_EvenThoughStatementsFollowIt()
    {
        var labels = await CompleteAsync("var p = new Person();\np.", "p.", context: ["class Person { public string Name { get; set; } public int Age { get; set; } }"]);

        Assert.Contains("Name", labels);
        Assert.Contains("Age", labels);
    }

    [Fact]
    public async Task CellCompletion_ReadsTheUsingsOfEarlierCells()
    {
        var labels = await CompleteAsync("var x = new XElement(\"a\");\nx.", "x.", context: ["using System.Xml.Linq;", "int unrelated = 1;"]);

        Assert.Contains("Add", labels);
    }

    [Fact]
    public async Task CellCompletion_IgnoresTheDirectivesOfTheNotebook()
    {
        var labels = await CompleteAsync("n.", "n.", context: ["#!csharp\n#!share --from python things\nint n = 5;"]);

        Assert.Contains("ToString", labels);
    }

    [Fact]
    public async Task CellCompletion_WithNoEarlierCells_StillWorks()
    {
        var labels = await CompleteAsync("Console.Wr", "Console.Wr");

        Assert.Contains("WriteLine", labels);
    }

    // ── SQL cells: the rows of a query are a table the client draws, not text ───────────────────────────────────────────────

    private IEnumerable<Newtonsoft.Json.Linq.JObject> TableBundles() => Outputs()
        .Where(o => o.Output.Kind == "display").Select(o => o.Output.Data).OfType<Newtonsoft.Json.Linq.JObject>()
        .Where(data => data[PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table] is not null);

    [Fact]
    public async Task Initialize_ListsSql_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var sql = Assert.Single(result.Languages, l => l.Id == "sql");
        Assert.True(sql.CanRunCells);
        Assert.Contains("sqlite", sql.Aliases);
        Assert.Contains(".sql", sql.Extensions);
        // the languages the server completes in a cell are said to the client (it asks only for those)
        Assert.Equal(["csharp", "sql"], result.Languages.Where(l => l.HasCompletion).Select(l => l.Id).Order());
    }

    [FrySharp.Core.Tests.RealSqliteFact]
    public async Task Notebook_Sql_Rows_ArriveAsATableBundle_WithItsHtmlAndTextFallbacks()
    {
        var result = await RunHeldAsync("nb-sql", "create table people (id integer primary key, name text, age integer);\n" +
            "insert into people (name, age) values ('Ada', 36), ('Linus', 55);\nselect id, name, age from people order by id;", "sql");

        Assert.True(result.Success, TextOutput());
        var data = Assert.Single(TableBundles());
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(data[PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["id", "name", "age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Ada", (string?)table["rows"]![0]![1]);
        Assert.Equal(2, table["rows"]!.Count());
        Assert.NotNull(data["text/html"]);
        Assert.NotNull(data["text/plain"]);
        Assert.Equal(string.Empty, TextOutput()); // no text table next to it
    }

    [FrySharp.Core.Tests.RealSqliteFact]
    public async Task Notebook_Sql_AnError_NamesTheLineOfTheCell_AndWhatItSays()
    {
        var result = await RunHeldAsync("nb-sql-error", "select 1 as fine;\nselect * from nosuch;", "sql");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics!);
        Assert.Equal(2, diagnostic.StartLine);
        Assert.Contains("no such table", diagnostic.Message);
    }

    [FrySharp.Core.Tests.RealSqliteFact]
    public async Task Notebook_Sql_KeepsItsTables_AndTheyAreTheVariables()
    {
        Assert.True((await RunHeldAsync("nb-sql-vars", "create table kept (v integer); insert into kept values (7);", "sql")).Success);

        var variables = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>(
            "fry/notebook/variables", new NotebookRef(NotebookId: "nb-sql-vars"));

        var kept = Assert.Single(variables, v => v.Name == "kept");
        Assert.Equal("SQL", kept.Kernel);
    }

    [FrySharp.Core.Tests.RealSqliteFact]
    public async Task Notebook_Sql_CanBeGivenAListFromCSharp_AndQueryIt()
    {
        Assert.True((await RunHeldAsync("nb-sql-share", "var rows = new[] { new { Name = \"Ada\", Age = 36 }, new { Name = \"Linus\", Age = 55 } };")).Success);

        var result = await RunHeldAsync("nb-sql-share", "#!share --from csharp rows\nselect Name from rows where Age > 40;", "sql");

        Assert.True(result.Success, TextOutput());
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(Assert.Single(TableBundles())[PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal("Linus", (string?)table["rows"]![0]![0]);
    }

    // ── F# cells: one session for the notebook, the value of the last expression shown, errors on the cell's lines ───────────

    private IEnumerable<Newtonsoft.Json.Linq.JObject> DisplayBundles() => Outputs()
        .Where(o => o.Output.Kind == "display").Select(o => o.Output.Data).OfType<Newtonsoft.Json.Linq.JObject>();

    [Fact]
    public async Task Initialize_ListsFSharp_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var fsharp = Assert.Single(result.Languages, l => l.Id == "fsharp");
        Assert.True(fsharp.CanRunCells);
        Assert.Contains("fs", fsharp.Aliases);
        Assert.Contains(".fsx", fsharp.Extensions);
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_KeepsWhatACellDefines_AndShowsTheLastValue()
    {
        Assert.True((await RunHeldAsync("nb-fs", "let x = 5\nlet twice n = n * 2", "fsharp")).Success, TextOutput());

        var result = await RunHeldAsync("nb-fs", "twice x + 1", "fsharp");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("11", TextOutput());
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_RecordsArriveAsATable_ARecordAsAnObjectExplorer_WithTheirFallbacks()
    {
        Assert.True((await RunHeldAsync("nb-fs-bundles", "type Person = { Name: string; Age: int }\nlet people = [ { Name = \"Ada\"; Age = 36 }; { Name = \"Linus\"; Age = 55 } ]", "fsharp")).Success);

        Assert.True((await RunHeldAsync("nb-fs-bundles", "people", "fsharp")).Success, TextOutput());
        Assert.True((await RunHeldAsync("nb-fs-bundles", "people.Head", "fsharp")).Success, TextOutput());

        var bundles = DisplayBundles().ToList();
        Assert.Equal(2, bundles.Count);
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(bundles[0][PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["Name", "Age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Linus", (string?)table["rows"]![1]![0]);
        Assert.NotNull(bundles[0]["text/html"]);
        AssertObjectExplorerBundle(bundles[1], "Person", "Name");
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_ACompileError_IsOnTheCellsLine_WithItsColumns()
    {
        var result = await RunHeldAsync("nb-fs-error", "let a = 1\n\nlet b = a + nope\nb", "fsharp");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!);
        Assert.Equal("FS0039", error.Code);
        Assert.Equal(3, error.StartLine);
        Assert.Equal(13, error.StartColumn);
        Assert.Contains("nope", error.Message);
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_ARuntimeError_NamesTheLineOfTheCell()
    {
        var result = await RunHeldAsync("nb-fs-throw", "let f x =\n    if x > 1 then failwith \"boom\" else x\nf 5", "fsharp");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!);
        Assert.Equal("Exception", error.Code);
        Assert.Equal(3, error.StartLine);
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_TakesAListFromCSharp_AndGivesOneBack()
    {
        Assert.True((await RunHeldAsync("nb-fs-share", "var rows = new[] { new { Name = \"Ada\", Age = 36 }, new { Name = \"Linus\", Age = 55 } };")).Success);

        var inFs = await RunHeldAsync("nb-fs-share", "#!share --from csharp rows\nrows |> Array.map (fun r -> r.[\"Name\"] :?> string) |> String.concat \", \"", "fsharp");
        Assert.True(inFs.Success, TextOutput());
        Assert.Contains("Ada, Linus", TextOutput());

        Assert.True((await RunHeldAsync("nb-fs-share", "let squares = [ for i in 1 .. 4 -> i * i ]", "fsharp")).Success);
        var back = await RunHeldAsync("nb-fs-share", "#!share --from fsharp squares\nConsole.WriteLine(string.Join(\",\", squares));");
        Assert.True(back.Success, TextOutput());
        Assert.Contains("1,4,9,16", TextOutput());
    }

    [FrySharp.Core.Tests.RealFSharpNuGetFact]
    public async Task Notebook_FSharp_APackageLine_IsNotSwallowedByTheNotebook_FsiLoadsIt()
    {
        var result = await RunHeldAsync("nb-fs-nuget", "#r \"nuget: Newtonsoft.Json, 13.0.3\"\nopen Newtonsoft.Json\nJsonConvert.SerializeObject([ 1; 2 ])", "fsharp");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("[1,2]", TextOutput());
    }

    [FrySharp.Core.Tests.RealFSharpFact]
    public async Task Notebook_FSharp_TheNotebooksValues_AreTheVariables()
    {
        Assert.True((await RunHeldAsync("nb-fs-vars", "let kept = 7\nlet names = [ \"a\" ]", "fsharp")).Success);

        var variables = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>(
            "fry/notebook/variables", new NotebookRef(NotebookId: "nb-fs-vars"));

        Assert.Equal(["kept", "names"], variables.Select(v => v.Name).Order());
        Assert.Equal("int", variables.Single(v => v.Name == "kept").Type);
    }

    // ── Java cells: a JShell session for the notebook, errors on the cell's lines and columns ─────────────────────────────────

    [Fact]
    public async Task Initialize_ListsJava_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var java = Assert.Single(result.Languages, l => l.Id == "java");
        Assert.True(java.CanRunCells);
        Assert.Contains("jdk", java.Aliases);
        Assert.Contains(".java", java.Extensions);
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_KeepsWhatACellDeclares_AndShowsTheLastValue()
    {
        Assert.True((await RunHeldAsync("nb-java", "int x = 5;\nint twice(int n) { return n * 2; }", "java")).Success, TextOutput());

        var result = await RunHeldAsync("nb-java", "twice(x) + 1", "java");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("11", TextOutput());
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_RecordsArriveAsATable_ARecordAsAnObjectExplorer()
    {
        Assert.True((await RunHeldAsync("nb-java-bundles", "record Person(String name, int age) {}\nvar people = List.of(new Person(\"Ada\", 36), new Person(\"Linus\", 55));", "java")).Success);

        Assert.True((await RunHeldAsync("nb-java-bundles", "people", "java")).Success, TextOutput());
        Assert.True((await RunHeldAsync("nb-java-bundles", "people.get(0)", "java")).Success, TextOutput());

        var bundles = DisplayBundles().ToList();
        Assert.Equal(2, bundles.Count);
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(bundles[0][PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["name", "age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Linus", (string?)table["rows"]![1]![0]);
        AssertObjectExplorerBundle(bundles[1], "Person", "name");
    }

    // What VS Code draws for an object that points at itself: the same five levels the desktop shows (the members of the top object and of
    // three levels below it, then a title), each reference back flagged and still listing its members, so it opens.
    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_AnObjectThatPointsAtItself_ArrivesFiveLevelsDeep_EachOneOpenable()
    {
        var data = await LastObjectDisplayAsync("cycle.frynb", "java",
            "public class People { public String name = \"Prashant\"; public People another; }\nPeople person = new People();\nperson.another = person;\nperson");

        var chain = new List<Newtonsoft.Json.Linq.JObject> { Assert.IsType<Newtonsoft.Json.Linq.JObject>(data[RichOutputMime.InspectorMime]) };
        while (chain[^1]["properties"]!.FirstOrDefault(p => (string?)p["name"] == "another")?["child"] is Newtonsoft.Json.Linq.JObject next) chain.Add(next);

        Assert.Equal(5, chain.Count);
        Assert.All(chain.Take(4), node => Assert.Equal(2, node["properties"]!.Count()));
        Assert.Equal([false, true, true, true, false], chain.Select(node => (bool?)node["circular"] == true));
        Assert.Empty(chain[4]["properties"]!);
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_ACompileError_IsOnTheCellsLine_WithItsColumns()
    {
        var result = await RunHeldAsync("nb-java-error", "int a = 1;\n\nString s = 5;\nint c = a;", "java");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!);
        Assert.Equal(3, error.StartLine);
        Assert.Equal(12, error.StartColumn);
        Assert.Contains("incompatible types", error.Message);
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_ARuntimeError_NamesItsKindAndTheLineOfTheCell()
    {
        var result = await RunHeldAsync("nb-java-throw", "void f(int x) {\n    if (x > 1) throw new IllegalStateException(\"boom\");\n}\n\nf(5);", "java");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!);
        Assert.Equal("IllegalStateException", error.Code);
        Assert.Equal(2, error.StartLine);
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_TakesAListFromCSharp_AndGivesOneBack()
    {
        Assert.True((await RunHeldAsync("nb-java-share", "var rows = new[] { new { Name = \"Ada\", Age = 36 }, new { Name = \"Linus\", Age = 55 } };")).Success);

        var inJava = await RunHeldAsync("nb-java-share", "#!share --from csharp rows\nSystem.out.println(rows.size());", "java");
        Assert.True(inJava.Success, TextOutput());
        Assert.Contains("2", TextOutput());

        Assert.True((await RunHeldAsync("nb-java-share", "int[] squares = {1, 4, 9};", "java")).Success);
        var back = await RunHeldAsync("nb-java-share", "#!share --from java squares\nConsole.WriteLine(string.Join(\",\", squares));");
        Assert.True(back.Success, TextOutput());
        Assert.Contains("1,4,9", TextOutput());
    }

    [FrySharp.Core.Tests.RealJavaFact]
    public async Task Notebook_Java_AScannerAndABufferedReader_AskTheClient_AndNoAnswerIsTheEndOfInput()
    {
        var answers = new Queue<string?>(["Ada", "36", null]);
        _harness.InputAnswer = _ => answers.Dequeue();

        var result = await RunHeldAsync("nb-java-input",
            "var in = new java.util.Scanner(System.in);\n" +
            "System.out.print(\"Name? \");\n" +
            "String name = in.nextLine();\n" +
            "var reader = new java.io.BufferedReader(new java.io.InputStreamReader(System.in));\n" +
            "String age = reader.readLine();\n" +
            "String rest = reader.readLine();\n" +
            "System.out.println(name + \" is \" + age + \", then \" + (rest == null ? \"end of input\" : rest));", "java");

        Assert.True(result.Success, TextOutput());
        Assert.Equal(3, _harness.InputRequests.Count);
        Assert.All(_harness.InputRequests, request => Assert.Equal("nb-java-input", request.Path));
        Assert.Contains("Ada is 36, then end of input", TextOutput());
    }

    [FrySharp.Core.Tests.RealJavaNetworkFact]
    public async Task Notebook_Java_ADepsLine_RestoresThePackage_AndItsClassesAreUsableAtOnce()
    {
        var result = await RunHeldAsync("nb-java-deps", "//DEPS com.google.code.gson:gson:2.11.0\nnew com.google.gson.Gson().toJson(List.of(1, 2))", "java");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("[1,2]", TextOutput());
    }

    // ── Rust cells: each cell built by Cargo, what a cell makes kept for the next, errors on the cell's own lines ─────────────

    [Fact]
    public async Task Initialize_ListsRust_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var rust = Assert.Single(result.Languages, l => l.Id == "rust");
        Assert.True(rust.CanRunCells);
        Assert.Contains("rs", rust.Aliases);
        Assert.Contains(".rs", rust.Extensions);
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_KeepsWhatACellMakes_AndShowsTheLastValue()
    {
        Assert.True((await RunHeldAsync("nb-rust", "fn twice(n: i32) -> i32 { n * 2 }\nlet x = 5;", "rust")).Success, TextOutput());

        var result = await RunHeldAsync("nb-rust", "twice(x) + 1", "rust");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("11", TextOutput());
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_AListOfStructsArrivesAsATable_AStructAsAnObjectExplorer()
    {
        Assert.True((await RunHeldAsync("nb-rust-bundles", "#[derive(Debug)]\nstruct Person { name: String, age: u32 }", "rust")).Success, TextOutput());

        Assert.True((await RunHeldAsync("nb-rust-bundles", "vec![Person { name: \"Ada\".into(), age: 36 }, Person { name: \"Linus\".into(), age: 55 }]", "rust")).Success, TextOutput());
        Assert.True((await RunHeldAsync("nb-rust-bundles", "Person { name: \"Grace\".into(), age: 85 }", "rust")).Success, TextOutput());

        var bundles = DisplayBundles().ToList();
        Assert.Equal(2, bundles.Count);
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(bundles[0][PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["name", "age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Linus", (string?)table["rows"]![1]![0]);
        AssertObjectExplorerBundle(bundles[1], "Person", "name");
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_ACompileError_IsOnTheCellsLine_WithItsColumn()
    {
        var result = await RunHeldAsync("nb-rust-error", "let a = 1;\n\nlet s: String = 5;\nlet c = a;", "rust");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!, d => d.Severity == "error");
        Assert.Equal(3, error.StartLine);
        Assert.Equal(17, error.StartColumn);
        Assert.Contains("mismatched types", error.Message);
        Assert.Contains("--> cell:3:17", TextOutput());
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_APanic_NamesTheLineOfTheCell()
    {
        var result = await RunHeldAsync("nb-rust-panic", "let v: Vec<i32> = Vec::new();\n\nprintln!(\"{}\", v[3]);", "rust");

        Assert.False(result.Success);
        Assert.Equal(3, Assert.Single(result.Diagnostics!).StartLine);
        Assert.Contains("panicked at cell:3:", TextOutput());
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_FryInput_AsksTheClient()
    {
        _harness.InputAnswer = _ => "Ada";

        var result = await RunHeldAsync("nb-rust-input", "let name = fry::input(\"Name? \").unwrap_or_default();\nprintln!(\"Hello, {}\", name);", "rust");

        Assert.True(result.Success, TextOutput());
        Assert.Equal("Name? ", Assert.Single(_harness.InputRequests).Prompt);
        Assert.Contains("Hello, Ada", TextOutput());
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_TakesAListFromCSharp_AndGivesOneBack()
    {
        Assert.True((await RunHeldAsync("nb-rust-share", "var rows = new[] { 1, 2, 3 };")).Success);

        var inRust = await RunHeldAsync("nb-rust-share", "#!share --from csharp rows\nprintln!(\"{}\", rows.iter().sum::<i64>());", "rust");
        Assert.True(inRust.Success, TextOutput());
        Assert.Contains("6", TextOutput());

        Assert.True((await RunHeldAsync("nb-rust-share", "let squares = vec![1, 4, 9];\nfry::share!(squares);", "rust")).Success, TextOutput());
        var back = await RunHeldAsync("nb-rust-share", "#!share --from rust squares\nConsole.WriteLine(string.Join(\",\", squares));");
        Assert.True(back.Success, TextOutput());
        Assert.Contains("1,4,9", TextOutput());
    }

    [FrySharp.Core.Tests.RealRustFact]
    public async Task Notebook_Rust_TheNotebooksValues_AreTheVariables()
    {
        Assert.True((await RunHeldAsync("nb-rust-vars", "let kept = 7;\nlet names = vec![\"a\"];", "rust")).Success, TextOutput());

        var variables = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>(
            "fry/notebook/variables", new NotebookRef(NotebookId: "nb-rust-vars"));

        Assert.Equal(["kept", "names"], variables.Select(v => v.Name).Order());
    }

    // ── Go cells: each cell built by the Go toolchain, what a cell makes kept for the next, errors on the cell's own lines ─────

    [Fact]
    public async Task Initialize_ListsGo_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var go = Assert.Single(result.Languages, l => l.Id == "go");
        Assert.True(go.CanRunCells);
        Assert.Contains("golang", go.Aliases);
        Assert.Contains(".go", go.Extensions);
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_KeepsWhatACellMakes_AndShowsTheLastValue()
    {
        Assert.True((await RunHeldAsync("nb-go", "func twice(n int) int { return n * 2 }\nx := 5", "go")).Success, TextOutput());

        var result = await RunHeldAsync("nb-go", "twice(x) + 1", "go");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("11", TextOutput());
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_AListOfStructsArrivesAsATable_AStructAsAnObjectExplorer()
    {
        Assert.True((await RunHeldAsync("nb-go-bundles", "type Person struct {\n\tName string\n\tAge  int\n}", "go")).Success, TextOutput());

        Assert.True((await RunHeldAsync("nb-go-bundles", "[]Person{{\"Ada\", 36}, {\"Linus\", 55}}", "go")).Success, TextOutput());
        Assert.True((await RunHeldAsync("nb-go-bundles", "Person{\"Grace\", 85}", "go")).Success, TextOutput());

        var bundles = DisplayBundles().ToList();
        Assert.Equal(2, bundles.Count);
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(bundles[0][PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["Name", "Age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Linus", (string?)table["rows"]![1]![0]);
        AssertObjectExplorerBundle(bundles[1], "Person", "Name");
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_ACompileError_IsOnTheCellsLine_WithItsColumn()
    {
        var result = await RunHeldAsync("nb-go-error", "a := 1\n\nvar s string = 5\nfmt.Println(a, s)", "go");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!, d => d.Severity == "error");
        Assert.Equal(3, error.StartLine);
        Assert.Equal(16, error.StartColumn);
        Assert.Contains("cannot use 5", error.Message);
        Assert.Contains("cell:3:16", TextOutput());
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_APanic_NamesTheLineOfTheCell()
    {
        var result = await RunHeldAsync("nb-go-panic", "v := []int{}\n\nfmt.Println(v[3])", "go");

        Assert.False(result.Success);
        Assert.Equal(3, Assert.Single(result.Diagnostics!).StartLine);
        Assert.Contains("cell:3", TextOutput());
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_FryInput_AsksTheClient()
    {
        _harness.InputAnswer = _ => "Ada";

        var result = await RunHeldAsync("nb-go-input", "name, _ := fry.Input(\"Name? \")\nfmt.Println(\"Hello,\", name)", "go");

        Assert.True(result.Success, TextOutput());
        Assert.Equal("Name? ", Assert.Single(_harness.InputRequests).Prompt);
        Assert.Contains("Hello, Ada", TextOutput());
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_TakesAListFromCSharp_AndGivesOneBack()
    {
        Assert.True((await RunHeldAsync("nb-go-share", "var rows = new[] { 1, 2, 3 };")).Success);

        var inGo = await RunHeldAsync("nb-go-share", "#!share --from csharp rows\ntotal := 0\nfor _, n := range rows {\n\ttotal += n\n}\nfmt.Println(len(rows), \"rows, sum\", total)", "go");
        Assert.True(inGo.Success, TextOutput());
        Assert.Contains("3 rows, sum 6", TextOutput());

        Assert.True((await RunHeldAsync("nb-go-share", "squares := []int{1, 4, 9}", "go")).Success, TextOutput());
        var back = await RunHeldAsync("nb-go-share", "#!share --from go squares\nConsole.WriteLine(string.Join(\",\", squares));");
        Assert.True(back.Success, TextOutput());
        Assert.Contains("1,4,9", TextOutput());
    }

    [FrySharp.Core.Tests.RealGoFact]
    public async Task Notebook_Go_TheNotebooksValues_AreTheVariables()
    {
        Assert.True((await RunHeldAsync("nb-go-vars", "kept := 7\nnames := []string{\"a\"}", "go")).Success, TextOutput());

        var variables = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>(
            "fry/notebook/variables", new NotebookRef(NotebookId: "nb-go-vars"));

        Assert.Equal(["kept", "names"], variables.Select(v => v.Name).Order());
        Assert.Equal("int", variables.Single(v => v.Name == "kept").Type);
    }

    // ── C++ cells: each cell built by the compiler, what a cell makes kept for the next, errors on the cell's own lines ─────

    [Fact]
    public async Task Initialize_ListsCpp_AsALanguageThatRunsCells()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var cpp = Assert.Single(result.Languages, l => l.Id == "cpp");
        Assert.True(cpp.CanRunCells);
        Assert.Contains("c++", cpp.Aliases);
        Assert.Contains(".cpp", cpp.Extensions);
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_KeepsWhatACellMakes_AndShowsTheLastValue()
    {
        Assert.True((await RunHeldAsync("nb-cpp", "int twice(int n) { return n * 2; }\nint x = 5;", "cpp")).Success, TextOutput());

        var result = await RunHeldAsync("nb-cpp", "twice(x) + 1", "cpp");

        Assert.True(result.Success, TextOutput());
        Assert.Contains("11", TextOutput());
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_AListOfStructsArrivesAsATable_AStructAsAnObjectExplorer()
    {
        Assert.True((await RunHeldAsync("nb-cpp-bundles", "struct Person {\n    std::string name;\n    int age;\n};", "cpp")).Success, TextOutput());

        Assert.True((await RunHeldAsync("nb-cpp-bundles", "std::vector<Person>{{\"Ada\", 36}, {\"Linus\", 55}}", "cpp")).Success, TextOutput());
        Assert.True((await RunHeldAsync("nb-cpp-bundles", "Person{\"Grace\", 85}", "cpp")).Success, TextOutput());

        var bundles = DisplayBundles().ToList();
        Assert.Equal(2, bundles.Count);
        var table = Assert.IsType<Newtonsoft.Json.Linq.JObject>(bundles[0][PdfEditorApp.Plugins.CSharpEditor.Visuals.Json.VisualMimeTypes.Table]);
        Assert.Equal(["name", "age"], table["columns"]!.Select(c => (string?)c));
        Assert.Equal("Linus", (string?)table["rows"]![1]![0]);
        AssertObjectExplorerBundle(bundles[1], "Person", "name");
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_ACompileError_IsOnTheCellsLine_WithItsColumn()
    {
        var result = await RunHeldAsync("nb-cpp-error", "int a = 1;\n\nint s = \"text\";\nstd::cout << a << s;", "cpp");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics!, d => d.Severity == "error");
        Assert.Equal(3, error.StartLine);
        Assert.Equal(5, error.StartColumn); // the name the compiler points at: int s = "text";
        Assert.Contains("cell:3:5", TextOutput());
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_AnUncaughtException_NamesItsCause()
    {
        var result = await RunHeldAsync("nb-cpp-throw", "throw std::runtime_error(\"boom\");", "cpp");

        Assert.False(result.Success);
        Assert.Contains("boom", TextOutput());
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_FryInput_AsksTheClient()
    {
        _harness.InputAnswer = _ => "Ada";

        var result = await RunHeldAsync("nb-cpp-input", "auto name = fry::input(\"Name? \");\nstd::cout << \"Hello, \" << name.value_or(\"?\") << \"\\n\";", "cpp");

        Assert.True(result.Success, TextOutput());
        Assert.Equal("Name? ", Assert.Single(_harness.InputRequests).Prompt);
        Assert.Contains("Hello, Ada", TextOutput());
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_TakesAListFromCSharp_AndGivesOneBack()
    {
        Assert.True((await RunHeldAsync("nb-cpp-share", "var rows = new[] { 1, 2, 3 };")).Success);

        var inCpp = await RunHeldAsync("nb-cpp-share", "#!share --from csharp rows\nlong long total = 0;\nfor (auto n : rows) total += n;\nstd::cout << rows.size() << \" rows, sum \" << total << \"\\n\";", "cpp");
        Assert.True(inCpp.Success, TextOutput());
        Assert.Contains("3 rows, sum 6", TextOutput());

        Assert.True((await RunHeldAsync("nb-cpp-share", "std::vector<int> squares{1, 4, 9};", "cpp")).Success, TextOutput());
        var back = await RunHeldAsync("nb-cpp-share", "#!share --from cpp squares\nConsole.WriteLine(string.Join(\",\", squares));");
        Assert.True(back.Success, TextOutput());
        Assert.Contains("1,4,9", TextOutput());
    }

    [FrySharp.Core.Tests.RealCppFact]
    public async Task Notebook_Cpp_TheNotebooksValues_AreTheVariables()
    {
        Assert.True((await RunHeldAsync("nb-cpp-vars", "int kept = 7;\nstd::vector<std::string> names{\"a\"};", "cpp")).Success, TextOutput());

        var variables = await _harness.ClientRpc.InvokeWithParameterObjectAsync<IReadOnlyList<VariableDto>>(
            "fry/notebook/variables", new NotebookRef(NotebookId: "nb-cpp-vars"));

        Assert.Equal(["kept", "names"], variables.Select(v => v.Name).Order());
        Assert.Equal("int", variables.Single(v => v.Name == "kept").Type);
    }

    [Fact]
    public async Task Execute_AFailedCell_SaysWhyInALine_AndAStoppedOrSuccessfulOneSaysNothing()
    {
        var compile = await RunHeldAsync("nb-error-line", "int x = \"text\";\nstring y = 5;");
        Assert.False(compile.Success);
        Assert.NotNull(compile.Error);
        Assert.StartsWith("CS", compile.Error!.Name);
        Assert.Contains("(and 1 more)", compile.Error.Message);

        var thrown = await RunHeldAsync("nb-error-line", "throw new InvalidOperationException(\"boom\");");
        Assert.False(thrown.Success);
        Assert.Contains("boom", thrown.Error!.Message);

        Assert.Null((await RunHeldAsync("nb-error-line", "1 + 1")).Error);
    }

    [Fact]
    public async Task EngineInfo_SaysWhatItRunsOnAndWhichToolEachLanguageFound()
    {
        await _harness.ClientRpc.InvokeWithParameterObjectAsync<InitializeResult>("fry/initialize", new InitializeParams("test-client"));

        var info = await _harness.ClientRpc.InvokeWithParameterObjectAsync<EngineInfo>("fry/engine/info", new EmptyParams());

        Assert.Equal(ProtocolVersion.Current, info.ProtocolVersion);
        Assert.StartsWith(".NET", info.Runtime);
        Assert.False(string.IsNullOrEmpty(info.OperatingSystem));
        Assert.Equal(_harness.Workspace.Path, info.WorkspaceRoot);
        // C# is in the engine, so it is always found; every other language says what it found, or what is missing and how to get it
        var csharp = Assert.Single(info.Toolchains, t => t.LanguageId == "csharp");
        Assert.True(csharp.Found);
        Assert.Contains(info.Toolchains, t => t.LanguageId == "python");
        Assert.Contains(info.Toolchains, t => t.LanguageId == "java");
        Assert.All(info.Toolchains, t =>
        {
            if (t.Found) Assert.False(string.IsNullOrEmpty(t.Name));
            else Assert.False(string.IsNullOrWhiteSpace(t.Missing), $"{t.DisplayName} is missing and does not say how to get it");
        });
        Assert.Equal(info.Toolchains.Select(t => t.DisplayName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase), info.Toolchains.Select(t => t.DisplayName));
    }

    [Fact]
    public async Task CellDiagnostics_ASqlDirectiveThatIsNotOne_IsReported_AsInAnyLanguage()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("sql", "#!sqll\nselect 1;", null));

        Assert.Equal("FRY0001", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CellCompletion_IsForCSharp_OtherLanguagesGetNone()
    {
        Assert.Empty(await CompleteAsync("import os\nos.", "os.", language: "python"));
    }

    [Fact]
    public async Task CellDiagnostics_AreInTheCellsOwnLines_NotInTheJoinedText()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("csharp", "int fine = 1;\nint bad = ;", ["using System.Text;", "int a = 1;\nint b = 2;\nint c = 3;"]));

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(2, error.StartLine);
        Assert.StartsWith("CS", error.Code);
    }

    [Fact]
    public async Task CellDiagnostics_AVariableAnEarlierCellDefines_IsNotUndefined_AndRedefiningItIsAllowed()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("csharp", "var total = count + 1;\nstring name = \"again\";\nConsole.WriteLine(total + name);", ["int count = 41;", "string name = \"first\";"]));

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CellDiagnostics_AnUndefinedName_IsReported_WhenNoCellDefinesIt()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("csharp", "Console.WriteLine(missing);", ["int other = 1;"]));

        Assert.Contains(result.Diagnostics, d => d.Code == "CS0103" && d.StartLine == 1);
    }

    [Fact]
    public async Task CellDiagnostics_ADirectiveTypo_IsReported_InAnyLanguage()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("python", "#!pyton\nprint(1)"));

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(1, error.StartLine);
        Assert.Contains("isn't a directive", error.Message);
    }

    [Fact]
    public async Task CellDiagnostics_ACellWithNoProblems_HasNone()
    {
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<CellDiagnosticsResult>("fry/language/diagnoseCell",
            new CellDiagnosticsParams("python", "#!python\nprint(1)"));

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task CellHover_SaysWhatAMemberOfAnEarlierCellsVariableIs()
    {
        var source = "s.Length";
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<HoverResult?>("fry/language/hoverCell",
            new CellPositionParams("csharp", source, source.IndexOf("Length", StringComparison.Ordinal) + 2, ["var s = \"abc\";"]));

        Assert.NotNull(result);
        Assert.Contains("int", string.Concat(result!.Markdown));
    }

    [Fact]
    public async Task CellCompletion_Sql_OffersKeywordsFunctionsAndTheTablesTheCellsBeforeCreated()
    {
        var keywords = await CompleteAsync("SEL", "SEL", language: "sql");
        var tables = await CompleteAsync("select * from peo", "peo", context: ["create table people (id integer);", "create view people_names as select 1;"], language: "sql");
        var functions = await CompleteAsync("select CO", "CO", language: "sql");

        Assert.Contains("SELECT", keywords);
        Assert.Equal(["people", "people_names"], tables.Order());
        Assert.Contains("COUNT", functions);
        Assert.Contains("COALESCE", functions);
    }

    [Fact]
    public async Task CellCompletion_Sql_OffersTheDotCommandsOfSqlite()
    {
        var labels = await CompleteAsync(".ta", ".ta", language: "sql");

        Assert.Equal([".tables"], labels);
    }

    [Fact]
    public async Task CellHover_Sql_SaysWhatAKeywordIs()
    {
        var source = "select name from people group by name";
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<HoverResult?>("fry/language/hoverCell",
            new CellPositionParams("sql", source, source.IndexOf("group", StringComparison.Ordinal) + 2, null));

        Assert.NotNull(result);
        var text = string.Concat(result!.Markdown);
        Assert.Contains("GROUP BY column1, column2", text);
        Assert.Contains("Groups summary rows", text);
        Assert.StartsWith("```sql", result.Markdown[0]);
    }

    [Fact]
    public async Task CellHover_Sql_SaysNothingAboutAWordThatIsNotKnown()
    {
        var source = "select anything from somewhere";
        var result = await _harness.ClientRpc.InvokeWithParameterObjectAsync<HoverResult?>("fry/language/hoverCell",
            new CellPositionParams("sql", source, source.IndexOf("anything", StringComparison.Ordinal) + 2, null));

        Assert.Null(result);
    }
}
