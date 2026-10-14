using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// The notebook files shared with the VS Code extension (vscode-frysharp/test/fixtures). <c>desktop.frynb</c> is written
/// here, by the desktop's own writer, with every kind of output; the extension's tests read it, write it back, and the
/// files they write (<c>written/</c>) are read here by the desktop's reader. So neither side can change the format alone.
/// Set FRY_UPDATE_FIXTURES=1 to rewrite <c>desktop.frynb</c> after changing what the desktop writes.
/// </summary>
public sealed class ExtensionFixturesTests
{
    // 1 x 1 pixel PNG
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");

    private static string FixturesFolder
    {
        get
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                if (File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.slnx"))) return Path.Combine(folder.FullName, "vscode-frysharp", "test", "fixtures");
            }
            throw new InvalidOperationException("The repository root (CSharpEditorPlugin.slnx) was not found above the test's folder.");
        }
    }

    private static NotebookDocumentItem DesktopNotebook()
    {
        var notebook = new NotebookDocumentItem
        {
            Id = "fixture-notebook",
            Title = "Desktop fixture",
            Created = new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc),
            LastModified = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc),
            ExecutionCount = 7
        };

        notebook.Cells.Add(new NotebookCellItem { Id = "intro", Type = CellType.Markdown, Source = "# Desktop fixture\nEvery kind of output.", IsMarkdownPreviewMode = true });

        notebook.Cells.Add(new NotebookCellItem { Id = "text", Source = "Console.WriteLine(\"hi\");", OutputText = "hi\n", ExecutionCount = 1, ExecutionTimeText = "3 ms" });

        notebook.Cells.Add(new NotebookCellItem { Id = "failed", Source = "throw new Exception(\"boom\");", OutputText = "❌ boom\n", HasError = true, ExecutionCount = 2 });

        var table = DumpTableBuilder.Create(new[] { new { Name = "Ada", Age = 36, Active = true }, new { Name = "Grace", Age = 45, Active = false } }, "People");
        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "table",
            Source = "people.Dump();",
            OutputText = "before the table\n",
            ExecutionCount = 3,
            TableSnapshot = table.ToSnapshot(),
            HtmlContent = InteractiveTableHtmlRenderer.Render(table)
        });

        var inspector = ObjectInspectorBuilder.Build(new { Name = "Code", Numbers = new[] { 1, 2, 3 }, Inner = new { X = 1, Y = "two" } });
        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "inspector",
            Source = "obj",
            ExecutionCount = 4,
            InspectorSnapshot = inspector.ToSnapshot(),
            HtmlContent = InteractiveTableHtmlRenderer.RenderInspector(inspector)
        });

        var chart = ChartSpecBuilder.FromSeries(ChartType.Line, new[] { ("Sales", (IEnumerable)new[] { 1.0, 3.0, 2.0 }) });
        chart.Title = "Sales";
        Assert.True(VisualSvgRenderer.TryRender(chart, out var chartHtml));
        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "chart",
            Source = "Display.LineChart(sales);",
            ExecutionCount = 5,
            Visuals = [VisualOutputSnapshot.From(VisualOutput.Create(chart, "fixture-chart"))],
            HtmlContent = chartHtml
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "image",
            Source = "Display.Image(png);",
            ExecutionCount = 6,
            ImageBytes = Png,
            ImageFormat = "PNG",
            ImageWidth = 1,
            ImageHeight = 1,
            HtmlContent = "<div class=\"fry-img-card\"><img src=\"data:image/png;base64," + Convert.ToBase64String(Png) + "\" /></div>"
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Id = "collapsed",
            Source = "print('python')",
            Language = "python",
            OutputText = "python\n",
            ExecutionCount = 7,
            ExecutionTimeText = "12 ms",
            IsInputCollapsed = true,
            IsOutputCollapsed = true,
            IsOutputScrolled = true,
            HadInteractiveControl = true
        });

        return notebook;
    }

    // The pages the desktop draws a table or chart with carry a new random element id each time: compare everything else.
    private static JsonNode WithoutPages(string notebook)
    {
        var parsed = JsonNode.Parse(notebook)!.AsObject();
        foreach (var cell in parsed["Cells"]!.AsArray()) cell!.AsObject().Remove("HtmlContent");
        return parsed;
    }

    [Fact]
    public void TheDesktopFixture_IsWhatTheDesktopWritesNow()
    {
        var path = Path.Combine(FixturesFolder, "desktop.frynb");
        var written = NotebookFileFormat.WriteFrynb(DesktopNotebook());

        if (Environment.GetEnvironmentVariable("FRY_UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(FixturesFolder);
            File.WriteAllText(path, written);
        }

        Assert.True(File.Exists(path), $"{path} is missing: run this test with FRY_UPDATE_FIXTURES=1.");
        Assert.True(JsonNode.DeepEquals(WithoutPages(written), WithoutPages(File.ReadAllText(path))),
            "The desktop now writes a different notebook than vscode-frysharp/test/fixtures/desktop.frynb: run this test with FRY_UPDATE_FIXTURES=1, then the extension's tests.");
    }

    [Fact]
    public void ADesktopNotebook_SavedByTheExtensionWithoutChanges_IsTheSameNotebook()
    {
        var path = Path.Combine(FixturesFolder, "written", "desktop.frynb");
        Assert.True(File.Exists(path), $"{path} is missing: run the extension's tests (npm test in vscode-frysharp).");

        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(FixturesFolder, "desktop.frynb")))!.AsObject();
        var returned = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        original.Remove("LastModified"); // the save stamps it
        returned.Remove("LastModified");

        Assert.True(JsonNode.DeepEquals(original, returned), "The extension changed the notebook it read and wrote back.");

        // and the desktop reads it
        var read = NotebookFileFormat.ReadFrynb(File.ReadAllText(path));
        Assert.Equal(DesktopNotebook().Cells.Select(c => c.Id), read.Cells.Select(c => c.Id));
        Assert.Equal(2, read.Cells.Single(c => c.Id == "table").TableSnapshot!.Rows.Count);
        Assert.Equal("Sales", Assert.Single(read.Cells.Single(c => c.Id == "chart").Visuals!).Title);
    }

    [Fact]
    public void WhatTheExtensionSavesAfterARun_ReadsInTheDesktopWithItsTableChartInspectorAndImage()
    {
        var path = Path.Combine(FixturesFolder, "written", "after-a-run.frynb");
        Assert.True(File.Exists(path), $"{path} is missing: run the extension's tests (npm test in vscode-frysharp).");

        var notebook = NotebookFileFormat.ReadFrynb(File.ReadAllText(path));

        var cell = notebook.Cells.Single(c => c.Id == "run");
        Assert.Equal("Result:\n", cell.OutputText);
        Assert.Equal(3, cell.ExecutionCount);
        Assert.Equal("12 ms", cell.ExecutionTimeText);
        Assert.False(cell.HasError);

        var table = cell.TableSnapshot!;
        Assert.Equal("People", table.Title);
        Assert.Equal(["Name", "Age"], table.Columns.Select(c => c.Header));
        Assert.Equal([false, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal("Ada", table.Rows[0].Cells[0].DisplayText);
        Assert.True(table.Rows[1].Cells[1].IsNull);

        var inspector = cell.InspectorSnapshot!;
        Assert.Equal("People", inspector.HeaderTitle);
        Assert.Equal("Code", inspector.Properties[0].SimpleValueText);
        Assert.True(inspector.Properties[1].ChildNode!.IsCircularReference);

        var visual = Assert.Single(cell.Visuals!);
        Assert.Equal("application/vnd.fry.chart.v1+json", visual.MimeType);
        Assert.Equal("Sales", visual.Title);
        Assert.Equal(JsonValueKind.Object, visual.Data!.Value.ValueKind);

        Assert.Equal(Png, cell.ImageBytes);
        Assert.Equal("PNG", cell.ImageFormat);
        Assert.Equal(1, cell.ImageWidth);

        // the desktop keeps one page for a cell, the last output's (the extension also saves the whole list, which the desktop ignores)
        Assert.Contains("<img", cell.HtmlContent);

        var failed = notebook.Cells.Single(c => c.Id == "failed");
        Assert.True(failed.HasError);
        Assert.Equal("boom\n", failed.OutputText);
    }
}
