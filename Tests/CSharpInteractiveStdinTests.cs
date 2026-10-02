using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class CSharpInteractiveStdinTests
{
    [Fact]
    public void ReadLine_ReturnsPostedLines_InFifoOrder()
    {
        using var reader = new InteractiveStdinReader();
        reader.PostInput("First line");
        reader.PostInput("Second line");

        Assert.Equal("First line", reader.ReadLine());
        Assert.Equal("Second line", reader.ReadLine());
    }

    [Fact]
    public async Task ReadLine_BlocksUntilInputPosted_ThenUnblocks()
    {
        using var reader = new InteractiveStdinReader();
        var startedWaiting = false;
        reader.WaitingStarted += () => startedWaiting = true;

        var readTask = Task.Run(() => reader.ReadLine());

        // Wait briefly for readTask to block
        for (var i = 0; i < 50 && !startedWaiting; i++)
        {
            await Task.Delay(10);
        }

        Assert.True(startedWaiting);
        Assert.False(readTask.IsCompleted);

        reader.PostInput("Hello from background");
        var result = await readTask;

        Assert.Equal("Hello from background", result);
    }

    [Fact]
    public void ReadLine_WhenCompleted_ReturnsNullEof()
    {
        using var reader = new InteractiveStdinReader();
        reader.PostInput("one");
        reader.Complete();

        Assert.Equal("one", reader.ReadLine());
        Assert.Null(reader.ReadLine());
    }

    [Fact]
    public void ReadLine_WhenCancelled_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        using var reader = new InteractiveStdinReader(cts.Token);

        cts.Cancel();
        var result = reader.ReadLine();

        Assert.Null(result);
    }

    [Fact]
    public void Read_ReadsSingleCharacters_AndConsumesInput()
    {
        using var reader = new InteractiveStdinReader();
        reader.PostInput("AB");

        Assert.Equal('A', (char)reader.Read());
        Assert.Equal('B', (char)reader.Read());
        Assert.Equal('\n', (char)reader.Read());
    }

    [Fact]
    public void ConsoleRoutingContext_ScopesStdinReader_Correctly()
    {
        using var reader = new InteractiveStdinReader();
        reader.PostInput("User input text");

        using (ConsoleRoutingContext.EnterScope(TextWriter.Null, reader))
        {
            var line = Console.ReadLine();
            Assert.Equal("User input text", line);
        }
    }

    [Fact]
    public async Task ScriptExecutionEngine_ExecutesCodeWithConsoleReadLine()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        const string code = """
            Console.Write("Enter name: ");
            var name = Console.ReadLine();
            Console.WriteLine($"Hello, {name}!");
            """;

        var (success, bytes, _) = compiler.CompileToAssembly(code);
        Assert.True(success, "Compilation must succeed");
        Assert.NotNull(bytes);

        using var stdinReader = new InteractiveStdinReader();
        stdinReader.PostInput("World");

        var output = new System.Text.StringBuilder();
        var result = await engine.ExecuteAsync(
            bytes!,
            live => output.Append(live),
            CancellationToken.None,
            stdinReader);

        Assert.True(result.Success, $"Execution failed: {result.Error}");
        var finalOutput = output.ToString();
        Assert.Contains("Hello, World!", finalOutput);
    }
}
