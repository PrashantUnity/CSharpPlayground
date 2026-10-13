using FrySharp.Sidecar.Protocol;
using PdfEditorApp.Plugins.CSharpEditor.Models;
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
}
