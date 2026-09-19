using System;
using System.IO;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class DocumentExportServiceTests
{
    [Fact]
    public void ExportScriptToCs_GeneratesCompilableProgramStructure()
    {
        var script = new ScriptDocumentItem
        {
            Id = "test-script",
            Title = "Invoice Generator",
            Code = "Console.WriteLine(\"Generating invoice...\");",
            ExecutionMode = "Statements",
            References = new() { "System.Text.Json" }
        };

        var csSource = DocumentExportService.ExportScriptToCs(script);

        Assert.NotNull(csSource);
        Assert.Contains("This code was exported from FryPDF C# Code Studio.", csSource);
        Assert.Contains("Invoice Generator", csSource);
        Assert.Contains("System.Text.Json", csSource);
        Assert.Contains("Console.WriteLine(\"Generating invoice...\");", csSource);
    }

    [Fact]
    public void ExportScriptToCs_PreservesProgramModeWithoutDoubleMain()
    {
        var script = new ScriptDocumentItem
        {
            Id = "program-script",
            Title = "Full Program",
            Code = "class Program { static void Main() { Console.WriteLine(\"Hello\"); } }",
            ExecutionMode = "Program"
        };

        var csSource = DocumentExportService.ExportScriptToCs(script);

        Assert.NotNull(csSource);
        Assert.Contains("class Program { static void Main() { Console.WriteLine(\"Hello\"); } }", csSource);
        Assert.DoesNotContain("namespace FryPdfGeneratedScript", csSource);
    }

    [Fact]
    public void ExportScriptToCsx_GeneratesRoslynScriptDirectives()
    {
        var script = new ScriptDocumentItem
        {
            Id = "csx-script",
            Title = "Batch Processor",
            Code = "using System.IO;\nvar sum = 1 + 2;\nConsole.WriteLine(sum);",
            References = new() { "Newtonsoft.Json.dll" }
        };

        var csxSource = DocumentExportService.ExportScriptToCsx(script);

        Assert.NotNull(csxSource);
        Assert.Contains("#r \"Newtonsoft.Json.dll\"", csxSource);
        Assert.Contains("using System.IO;", csxSource);
        Assert.Contains("var sum = 1 + 2;", csxSource);
    }

    [Fact]
    public void ExportNotebookToIpynb_ProducesValidV4JupyterJson()
    {
        var doc = new NotebookDocumentItem
        {
            Id = "nb-1",
            Title = "Data Analysis Notebook",
            Kernel = ".NET 10 (C# Interactive)",
            Cells = new()
            {
                new NotebookCellItem
                {
                    Id = "c1",
                    Type = CellType.Markdown,
                    Source = "# Overview\nThis notebook demonstrates PDF telemetry analysis."
                },
                new NotebookCellItem
                {
                    Id = "c2",
                    Type = CellType.Code,
                    Source = "int pageCount = 42;\nConsole.WriteLine($\"Pages: {pageCount}\");",
                    OutputText = "Pages: 42\n"
                }
            }
        };

        var json = DocumentExportService.ExportNotebookToIpynb(doc);
        Assert.NotNull(json);

        // Verify valid JSON and notebook schema
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;
        Assert.Equal(4, root.GetProperty("nbformat").GetInt32());
        Assert.Equal(5, root.GetProperty("nbformat_minor").GetInt32());

        var cells = root.GetProperty("cells");
        Assert.Equal(2, cells.GetArrayLength());

        var cell1 = cells[0];
        Assert.Equal("markdown", cell1.GetProperty("cell_type").GetString());

        var cell2 = cells[1];
        Assert.Equal("code", cell2.GetProperty("cell_type").GetString());
        var outputs = cell2.GetProperty("outputs");
        Assert.True(outputs.GetArrayLength() > 0);
    }

    [Fact]
    public void ExportNotebookToMarkdown_ProducesCleanMarkdownDocument()
    {
        var doc = new NotebookDocumentItem
        {
            Id = "nb-md",
            Title = "Report Generator",
            Cells = new()
            {
                new NotebookCellItem
                {
                    Id = "cell-1",
                    Type = CellType.Markdown,
                    Source = "### Step 1: Initialize Setup\nHere is how to set up the engine."
                },
                new NotebookCellItem
                {
                    Id = "cell-2",
                    Type = CellType.Code,
                    Source = "var engine = new Engine();\nengine.Start();"
                }
            }
        };

        var md = DocumentExportService.ExportNotebookToMarkdown(doc);
        Assert.NotNull(md);

        Assert.Contains("# Report Generator", md);
        Assert.Contains("### Step 1: Initialize Setup", md);
        Assert.Contains("```csharp", md);
        Assert.Contains("var engine = new Engine();", md);
        Assert.Contains("```", md);
    }
}
