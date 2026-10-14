using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;

namespace FrySharp.Core.Tests;

public sealed class AtomicFileTests
{
    [Fact]
    public async Task ASave_ReplacesTheFile_AndKeepsItsEncodingMark()
    {
        using var folder = new TempFolder();
        var path = folder.File("a.txt", "old");

        await AtomicFile.WriteAllTextAsync(path, "new text", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("new text", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Single(Directory.GetFiles(folder.Path)); // no temporary file left behind
    }

    [Fact]
    public async Task ASave_KeepsAnExecutableScriptExecutable()
    {
        if (OperatingSystem.IsWindows()) return;
        using var folder = new TempFolder();
        var path = folder.File("run.sh", "#!/bin/sh\necho old\n");
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(path, mode);

        await AtomicFile.WriteAllTextAsync(path, "#!/bin/sh\necho new\n", new UTF8Encoding(false));

        Assert.Equal(mode, File.GetUnixFileMode(path));
    }

    [Fact]
    public async Task ASaveThroughALink_WritesTheFileItPointsTo_AndTheLinkStaysALink()
    {
        using var folder = new TempFolder();
        var target = folder.File("real.txt", "old");
        var link = Path.Combine(folder.Path, "link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (IOException ex) when (OperatingSystem.IsWindows() && ex.Message.Contains("privilege"))
        {
            // Windows developer mode or elevation is required for symbolic links
            return;
        }

        await AtomicFile.WriteAllTextAsync(link, "through the link", new UTF8Encoding(false));

        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("through the link", await File.ReadAllTextAsync(target));
    }
}

public sealed class TextFileReaderTests
{
    [Fact]
    public void TheEncoding_ComesFromTheByteOrderMark()
    {
        Assert.Equal("é", TextFileReader.Decode([0xEF, 0xBB, 0xBF, 0xC3, 0xA9]).Text);
        Assert.Equal(3, TextFileReader.Decode([0xEF, 0xBB, 0xBF, 0x41]).Encoding.GetPreamble().Length);
        Assert.Empty(TextFileReader.Decode("plain"u8.ToArray()).Encoding.GetPreamble());
        Assert.Equal("hi", TextFileReader.Decode([0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0]).Text);
    }

    [Fact]
    public void ANulByte_MarksABinaryFile_ButUtf16TextIsText()
    {
        Assert.True(TextFileReader.LooksBinary([0x89, 0x50, 0x4E, 0x47, 0x00, 0x01]));
        Assert.False(TextFileReader.LooksBinary("text\n"u8));
        Assert.False(TextFileReader.LooksBinary([0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0]));
    }
}

public sealed class RichOutputMimeTests
{
    [Fact]
    public void ATable_IsTheStudiosTableJson_WithEveryBundleHavingText()
    {
        var table = DumpTableBuilder.Create(new[] { new { Name = "a", Value = 1 }, new { Name = "b", Value = 2 } }, "items");
        var bundle = RichOutputMime.ToBundle(new RichCellOutput { Kind = CellOutputKind.Table, TableResult = table }, out _)!;

        var json = bundle["application/vnd.fry.table+json"]!;
        Assert.Equal(["Name", "Value"], json["columns"]!.AsArray().Select(c => c!.GetValue<string>()));
        Assert.Equal(2, json["totalRows"]!.GetValue<int>());
        Assert.Equal("b", json["rows"]![1]![0]!.GetValue<string>());
        Assert.NotNull(bundle["text/plain"]);
    }

    [Fact]
    public void AnImage_IsBase64_WithItsSizeInTheMetadata()
    {
        // A 1×1 PNG header: the size is read from it, not decoded.
        var png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C489");
        var bundle = RichOutputMime.ToBundle(RichCellOutput.ForImage(png), out var metadata)!;

        Assert.Equal(Convert.ToBase64String(png), bundle["image/png"]!.GetValue<string>());
        Assert.Equal(1, metadata["image/png"]!["width"]!.GetValue<int>());
    }

    [Fact]
    public void ALiveDesktopControl_HasNoBundle()
    {
        Assert.Null(RichOutputMime.ToBundle(new RichCellOutput { Kind = CellOutputKind.Control, InteractiveControl = new object() }, out _));
    }
}

public sealed class MarkdownOutputTests
{
    [Fact]
    public void Markdown_KeepsItsText_ForHostsThatRenderMarkdown_AndIsSentAsTextMarkdown()
    {
        var output = MarkdownOutput.Create("# Title\n```csharp\nvar x = 1;\n```");
        var bundle = RichOutputMime.ToBundle(output, out _)!;

        Assert.Equal("# Title\n```csharp\nvar x = 1;\n```", bundle["text/markdown"]!.GetValue<string>());
        Assert.Null(bundle["text/html"]); // Jupyter ranks HTML first: the basic rendering would win over the real one
        Assert.StartsWith("<h1>Title</h1>", output.HtmlContent);
    }

    [Theory]
    [InlineData("[docs](https://example.com/a?b=1&c=2)", "<a href=\"https://example.com/a?b=1&amp;c=2\">docs</a>")]
    [InlineData("[mail](mailto:a@b.c)", "<a href=\"mailto:a@b.c\">mail</a>")]
    [InlineData("[top](#top)", "<a href=\"#top\">top</a>")]
    [InlineData("[click](javascript:alert(1))", "click")]
    [InlineData("[x](JavaScript:alert(1))", "x")]
    [InlineData("[x](data:text/html,hi)", "x")]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("[x](\" onclick=\"alert(1))", "[x](&quot; onclick=&quot;alert(1))")] // not a link: an address has no spaces
    [InlineData("[C](https://en.wikipedia.org/wiki/C_(programming_language))", "<a href=\"https://en.wikipedia.org/wiki/C_(programming_language)\">C</a>")]
    public void TheBasicHtml_HasOnlySafeLinksAndNoMarkup(string markdown, string html)
    {
        Assert.Equal(html, MarkdownOutput.ToBasicHtml(markdown));
    }

    [Fact]
    public void AJupyterMarkdownOutput_IsReadAsMarkdown_AndWrittenBackAsMarkdown()
    {
        const string ipynb = """
        { "cells": [ { "cell_type": "code", "metadata": {}, "source": "", "execution_count": 1,
            "outputs": [ { "output_type": "display_data", "metadata": {}, "data": { "text/markdown": ["**bold**\n", "text"], "text/plain": ["<IPython.core.display.Markdown object>"] } } ] } ],
          "metadata": {}, "nbformat": 4, "nbformat_minor": 5 }
        """;
        var notebook = NotebookFileFormat.ReadIpynb(ipynb, ToolchainFactAttribute.Registry);
        var cell = Assert.Single(notebook.Cells);
        Assert.Equal("**bold**\ntext", cell.MarkdownContent);
        Assert.Equal("<b>bold</b><br/>text", cell.HtmlContent);

        using var written = System.Text.Json.JsonDocument.Parse(NotebookFileFormat.WriteIpynb(notebook, ToolchainFactAttribute.Registry));
        var data = written.RootElement.GetProperty("cells")[0].GetProperty("outputs")[0].GetProperty("data");
        Assert.True(data.TryGetProperty("text/markdown", out _));
        Assert.False(data.TryGetProperty("text/html", out _));
    }

    [Fact]
    public void ANotebookWithoutMarkdownOutputs_IsSavedWithoutTheNewField()
    {
        var notebook = new NotebookDocumentItem();
        notebook.Cells.Add(new NotebookCellItem { Source = "1", HtmlContent = "<b>x</b>" });
        Assert.DoesNotContain("MarkdownContent", NotebookFileFormat.WriteFrynb(notebook));
    }
}

public sealed class InputRequestReaderTests
{
    [Fact]
    public void EachLineRead_IsAsked_AndNoAnswerEndsTheInput()
    {
        var answers = new Queue<string?>(["Ada", "42", null]);
        var asked = 0;
        using var reader = new PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.InputRequestReader((_, _, _) =>
        {
            asked++;
            return Task.FromResult(answers.Dequeue());
        }, CancellationToken.None);

        Assert.Equal('A', (char)reader.Peek());
        Assert.Equal('A', (char)reader.Read());
        Assert.Equal("da", reader.ReadLine()); // the rest of the line Read started
        Assert.Equal("42", reader.ReadLine());
        Assert.Null(reader.ReadLine());
        Assert.Equal(-1, reader.Read());
        Assert.Equal(3, asked); // nothing is asked once the input has ended
    }

    [Fact]
    public async Task ACSharpCell_ReadsWhatThePersonTyped()
    {
        var kernel = (PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.INotebookKernel)new PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.NotebookExecutionKernel();
        var output = new StringBuilder();
        var result = await kernel.ExecuteAsync(new PdfEditorApp.Plugins.CSharpEditor.Services.Kernels.KernelExecutionRequest
        {
            Code = "var name = Console.ReadLine();\nConsole.WriteLine($\"hi {name}\");",
            OnConsole = text => { lock (output) output.Append(text); },
            OnInputRequest = (_, _, _) => Task.FromResult<string?>("Grace"),
        }, CancellationToken.None);

        Assert.True(result.Success, output.ToString());
        Assert.Contains("hi Grace", output.ToString());
    }
}

public sealed class InteractiveTableAndInspectorTests
{
    [Fact]
    public void InteractiveTableHtmlRenderer_RendersInteractiveComponents_WithBadgesAndSort()
    {
        var table = new DumpTableResult("Cluster Health", "Telemetry");
        table.Columns.Add(new DumpTableColumn { Header = "Node", IsNumeric = false });
        table.Columns.Add(new DumpTableColumn { Header = "Latency", IsNumeric = true });
        table.Columns.Add(new DumpTableColumn { Header = "Status", IsNumeric = false });

        table.Rows.Add(new DumpTableRow(0, [
            new DumpTableCell { DisplayText = "node-01" },
            new DumpTableCell { DisplayText = "14.2", IsNumeric = true },
            new DumpTableCell { DisplayText = "Online" }
        ]));

        var html = InteractiveTableHtmlRenderer.Render(table);

        Assert.Contains("fry-tbl-container", html);
        Assert.Contains("📊 Cluster Health", html);
        Assert.Contains("fry-badge-green", html);
        Assert.Contains("Online", html);
        Assert.Contains("fry-expander-btn", html);
        Assert.Contains("fryTblSort", html);
        Assert.Contains("fryTblFilter", html);
        Assert.Contains("fryTblCopy", html);
    }

    [Fact]
    public void InteractiveTableHtmlRenderer_InspectorRendersExpandableDetails()
    {
        var node = new ObjectInspectorNode("ServerConfig");
        node.Properties.Add(new ObjectInspectorPropertyRow
        {
            Name = "Host",
            SimpleValueText = "localhost",
            IsString = true
        });
        node.Properties.Add(new ObjectInspectorPropertyRow
        {
            Name = "IsActive",
            SimpleValueText = "true"
        });

        var html = InteractiveTableHtmlRenderer.RenderInspector(node);

        Assert.Contains("fry-inspector-card", html);
        Assert.Contains("ServerConfig", html);
        Assert.Contains("Host", html);
        Assert.Contains("localhost", html);
        Assert.Contains("fry-badge-green", html);
    }

    [Fact]
    public void RichOutputMime_TableBundleIncludesInteractiveHtml()
    {
        var table = new DumpTableResult("Test Table");
        table.Columns.Add(new DumpTableColumn { Header = "Name" });
        table.Rows.Add(new DumpTableRow(0, [new DumpTableCell { DisplayText = "Alpha" }]));

        var richOutput = new RichCellOutput { Kind = CellOutputKind.Table, TableResult = table };
        var bundle = RichOutputMime.ToBundle(richOutput, out _);

        Assert.NotNull(bundle);
        Assert.True(bundle.ContainsKey("text/html"));
        Assert.Contains("fry-tbl-container", bundle["text/html"]!.ToString());
    }

    [Fact]
    public void RichOutputMime_InspectorBundleIncludesInteractiveHtml()
    {
        var node = new ObjectInspectorNode("TestNode");
        node.Properties.Add(new ObjectInspectorPropertyRow { Name = "Key", SimpleValueText = "Value" });

        var richOutput = new RichCellOutput { Kind = CellOutputKind.ObjectInspector, InspectorNode = node };
        var bundle = RichOutputMime.ToBundle(richOutput, out _);

        Assert.NotNull(bundle);
        Assert.True(bundle.ContainsKey("text/html"));
        Assert.Contains("fry-inspector-card", bundle["text/html"]!.ToString());
    }
}

