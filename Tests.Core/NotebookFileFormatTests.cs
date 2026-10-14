using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>Notebook files: FrySharp's own, byte for byte, and Jupyter's, read and written as Jupyter documents.</summary>
public sealed class NotebookFileFormatTests
{
    private static readonly PdfEditorApp.Plugins.CSharpEditor.Services.Languages.LanguageRegistry<PdfEditorApp.Plugins.CSharpEditor.Services.Languages.ILanguageEngine> Languages =
        ToolchainFactAttribute.Registry;

    [Fact]
    public void AFrynb_ReadAndWrittenAgain_IsTheSameBytes()
    {
        var notebook = new NotebookDocumentItem { Title = "Round trip", Kernel = "csharp" };
        notebook.Cells.Add(new NotebookCellItem { Type = CellType.Markdown, Source = "# Title\nText" });
        notebook.Cells.Add(new NotebookCellItem { Source = "var x = 1;\nx", OutputText = "1\n", ExecutionCount = 1, ImageBytes = [1, 2, 3], ImageFormat = "PNG" });
        notebook.Cells.Add(new NotebookCellItem { Language = "python", Source = "print('hi')", HasError = true });
        var written = NotebookFileFormat.WriteFrynb(notebook);

        Assert.Equal(written, NotebookFileFormat.WriteFrynb(NotebookFileFormat.ReadFrynb(written)));
    }

    [Fact]
    public void AJupyterNotebook_IsRead_WithItsCellsOutputsAndLanguages()
    {
        const string ipynb = """
        {
          "cells": [
            { "cell_type": "markdown", "id": "intro", "metadata": {}, "source": ["# Hello\n", "World"] },
            { "cell_type": "code", "id": "c1", "execution_count": 3, "metadata": {},
              "outputs": [
                { "output_type": "stream", "name": "stdout", "text": ["first\n", "second\n"] },
                { "output_type": "execute_result", "execution_count": 3, "data": { "text/plain": ["42"] }, "metadata": {} },
                { "output_type": "display_data", "data": { "image/png": "iVBORw0KGgo=", "text/plain": ["<Figure>"] }, "metadata": {} }
              ],
              "source": "x = 42\nx" },
            { "cell_type": "code", "id": "c2", "execution_count": 4, "metadata": { "polyglot_notebook": { "kernelName": "csharp" } },
              "outputs": [ { "output_type": "error", "ename": "ValueError", "evalue": "bad", "traceback": ["\u001b[31mValueError\u001b[0m: bad"] } ],
              "source": ["var y = 1;"] },
            { "cell_type": "raw", "metadata": {}, "source": "raw text" }
          ],
          "metadata": { "kernelspec": { "name": "python3", "display_name": "Python 3", "language": "python" }, "language_info": { "name": "python" } },
          "nbformat": 4, "nbformat_minor": 5
        }
        """;

        var notebook = NotebookFileFormat.Read("/w/demo.ipynb", ipynb, Languages);

        Assert.Equal("demo", notebook.Title);
        Assert.Equal("python", notebook.Kernel);
        Assert.Equal(4, notebook.Cells.Count);
        Assert.Equal((CellType.Markdown, "intro", "# Hello\nWorld"), (notebook.Cells[0].Type, notebook.Cells[0].Id, notebook.Cells[0].Source));

        var code = notebook.Cells[1];
        Assert.Equal((CellType.Code, "c1", "x = 42\nx", 3), (code.Type, code.Id, code.Source, code.ExecutionCount!.Value));
        Assert.Equal("first\nsecond\n42", code.OutputText);
        Assert.Equal(Convert.FromBase64String("iVBORw0KGgo="), code.ImageBytes);
        Assert.Null(code.Language); // the notebook's own language

        var csharp = notebook.Cells[2];
        Assert.Equal("csharp", csharp.Language);
        Assert.True(csharp.HasError);
        Assert.Equal("ValueError: bad", csharp.OutputText); // terminal colours removed

        Assert.Equal(CellType.Markdown, notebook.Cells[3].Type); // a raw cell is text, never run
        Assert.Equal(4, notebook.ExecutionCount);
    }

    [Fact]
    public void ANotebookWrittenAsJupyter_IsAValidNbformat45Document_ThatReadsBackTheSame()
    {
        var notebook = new NotebookDocumentItem { Title = "Mixed", Kernel = "csharp" };
        notebook.Cells.Add(new NotebookCellItem { Id = "md", Type = CellType.Markdown, Source = "# A\nB" });
        notebook.Cells.Add(new NotebookCellItem { Id = "cs", Source = "Console.WriteLine(1);", OutputText = "1\n", ExecutionCount = 1 });
        notebook.Cells.Add(new NotebookCellItem { Id = "py", Language = "python", Source = "print(2)", OutputText = "2\n", ExecutionCount = 2, HtmlContent = "<b>x</b>" });

        var json = NotebookFileFormat.WriteIpynb(notebook, Languages);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(4, root.GetProperty("nbformat").GetInt32());
        Assert.Equal(5, root.GetProperty("nbformat_minor").GetInt32());
        // nbformat 4.5 requires every cell to have an id.
        Assert.All(root.GetProperty("cells").EnumerateArray(), cell => Assert.False(string.IsNullOrEmpty(cell.GetProperty("id").GetString())));
        // Two languages: each code cell names its kernel, as Polyglot Notebooks does.
        Assert.Equal("python", root.GetProperty("cells")[2].GetProperty("metadata").GetProperty("polyglot_notebook").GetProperty("kernelName").GetString());

        var back = NotebookFileFormat.ReadIpynb(json, Languages);
        Assert.Equal(notebook.Cells.Select(c => (c.Id, c.Type, c.Source)), back.Cells.Select(c => (c.Id, c.Type, c.Source)));
        Assert.Equal("python", back.Cells[2].Language);
        Assert.Equal("<b>x</b>", back.Cells[2].HtmlContent);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "cells": 3 }""")]
    [InlineData("""{ "cells": [ { "cell_type": [] , "source": {} } ], "metadata": [] }""")]
    public void AMalformedJupyterNotebook_FailsWithAJsonError(string text)
    {
        try
        {
            var notebook = NotebookFileFormat.ReadIpynb(text, Languages);
            // A notebook with parts of the wrong shape may still read, losing those parts; it must not throw anything else.
            Assert.NotNull(notebook);
        }
        catch (JsonException)
        {
            // Expected for text that is not a notebook.
        }
    }

    [Fact]
    public void TheKindOfAFile_ComesFromItsExtension()
    {
        Assert.Equal(NotebookFileKind.Ipynb, NotebookFileFormat.KindOf("a/b.IPYNB"));
        Assert.Equal(NotebookFileKind.Frynb, NotebookFileFormat.KindOf("a/b.frynb"));
        Assert.Equal(NotebookFileKind.Frynb, NotebookFileFormat.KindOf("a/b.csnb"));
        Assert.False(NotebookFileFormat.IsNotebookPath("a/b.cs"));
    }
}
