using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class ExternalOutputProcessorTests
{
    [Fact]
    public void ProcessChunk_StandardConsoleOutput_ForwardsToConsoleCallback()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk("Hello World\nLine 2\n");
        processor.Flush();

        Assert.Equal(2, consoleLines.Count);
        Assert.Equal("Hello World\n", consoleLines[0]);
        Assert.Equal("Line 2\n", consoleLines[1]);
        Assert.Empty(richOutputs);
    }

    [Fact]
    public void ProcessChunk_FryDisplayMarkerWithTable_EmitsRichOutputAndSuppressesFromConsole()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        var tableJson = """
            __FRY_DISPLAY__ {"type":"display","data":{"application/vnd.fry.table+json":{"title":"Sorted Array","columns":["Index","Value"],"numeric":[true,true],"rows":[[0,7],[1,11]],"totalRows":2,"totalColumns":2}},"metadata":{}}
            """;

        processor.ProcessChunk("Before dump\n" + tableJson + "\nAfter dump\n");
        processor.Flush();

        Assert.Equal(2, consoleLines.Count);
        Assert.Equal("Before dump\n", consoleLines[0]);
        Assert.Equal("After dump\n", consoleLines[1]);

        Assert.Single(richOutputs);
        var rich = richOutputs[0];
        Assert.Equal(CellOutputKind.Table, rich.Kind);
        Assert.NotNull(rich.TableResult);
        Assert.Equal("Sorted Array", rich.TableResult.Title);
        Assert.Equal(2, rich.TableResult.Columns.Count);
        Assert.Equal(2, rich.TableResult.Rows.Count);
    }

    [Fact]
    public void ProcessChunk_ChunkSplitAcrossNewline_AssemblesProperly()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk("He");
        processor.ProcessChunk("llo ");
        processor.ProcessChunk("World\n");
        processor.Flush();

        Assert.Equal("Hello World\n", string.Concat(consoleLines));
    }

    [Fact]
    public void ProcessChunk_FryDisplaySplitAcrossChunks_AssemblesAndParses()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        var payload = """
            __FRY_DISPLAY__ {"type":"display","data":{"text/html":"<h1>Title</h1>"},"metadata":{}}

            """;
        var splitIndex = 40;
        var part1 = payload[..splitIndex];
        var part2 = payload[splitIndex..];

        processor.ProcessChunk(part1);
        processor.ProcessChunk(part2);
        processor.Flush();

        Assert.Empty(consoleLines);
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Html, richOutputs[0].Kind);
        Assert.Equal("<h1>Title</h1>", richOutputs[0].HtmlContent);
    }

    [Fact]
    public void Flush_ProcessesTrailingOutputWithoutNewline()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk("Trailing without newline");
        processor.Flush();

        Assert.Single(consoleLines);
        Assert.Equal("Trailing without newline", consoleLines[0]);
    }

    [Fact]
    public void ProcessChunk_PromptWithoutNewline_ForwardsImmediately()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk("Name? ");

        Assert.Single(consoleLines);
        Assert.Equal("Name? ", consoleLines[0]);
    }
}

