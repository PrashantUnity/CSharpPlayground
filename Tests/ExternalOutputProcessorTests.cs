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

    [Theory]
    [InlineData("text/plain", "42\\n", "42\n")]
    [InlineData("text/plain", "no newline", "no newline\n")]
    [InlineData("text/markdown", "**bold**", "**bold**\n")]
    public void ProcessChunk_ATextDisplay_IsShownAsText_NotAsTheProtocolLine(string mime, string value, string expected)
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk("before\n__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + value + "\"},\"metadata\":{}}\nafter\n");
        processor.Flush();

        Assert.Equal(["before\n", expected, "after\n"], consoleLines);
        Assert.Empty(richOutputs);
    }

    [Fact]
    public void ProcessChunk_AShareLine_IsHandedToTheShareCallback_AndNotShown()
    {
        var consoleLines = new List<string>();
        var shared = new List<(string Name, string Json)>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, _ => { }, (name, json) => shared.Add((name, json)));

        processor.ProcessChunk("a\n__FRY_SHARE__ {\"name\":\"nums\",\"json\":\"[1,2]\"}\nb\n__FRY_SHARE__ not json\n");
        processor.Flush();

        Assert.Equal([("nums", "[1,2]")], shared);
        Assert.Equal(["a\n", "b\n", "__FRY_SHARE__ not json\n"], consoleLines);
    }

    [Fact]
    public void ProcessChunk_AShareLine_IsOrdinaryOutput_WhenNobodyListens()
    {
        var consoleLines = new List<string>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, _ => { });

        processor.ProcessChunk("__FRY_SHARE__ {\"name\":\"x\",\"json\":\"1\"}\n");

        Assert.Single(consoleLines);
    }

    [Fact]
    public void ProcessChunk_AShareLineSplitAcrossChunks_AssemblesAndParses()
    {
        var shared = new List<string>();
        var processor = new ExternalOutputProcessor(_ => { }, _ => { }, (name, _) => shared.Add(name));

        processor.ProcessChunk("__FRY_SHA");
        processor.ProcessChunk("RE__ {\"name\":\"split\",\"json\":\"1\"}");
        processor.ProcessChunk("\n");

        Assert.Equal(["split"], shared);
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

    [Theory]
    [InlineData("Picked up JAVA_TOOL_OPTIONS: -Dfile.encoding=UTF-8")]
    [InlineData("Picked up _JAVA_OPTIONS: -Duser.home=/tmp")]
    [InlineData("Picked up JAVA_OPTIONS: -Xmx512m")]
    public void ProcessChunk_JvmLauncherNoise_IsSuppressedFromConsole(string noiseLine)
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        processor.ProcessChunk($"{noiseLine}\nActual Program Output\n");
        processor.Flush();

        Assert.Single(consoleLines);
        Assert.Equal("Actual Program Output\n", consoleLines[0]);
        Assert.Empty(richOutputs);
    }
}

