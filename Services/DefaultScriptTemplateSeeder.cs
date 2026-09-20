using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Provides default script and notebook seeding templates to populate the library on first launch.
/// Extracted from LocalScriptStorageService to keep storage engine lean and maintainable.
/// </summary>
public static class DefaultScriptTemplateSeeder
{
    public static async Task SeedDefaultsAsync(IScriptStorageService storage)
    {
        var templates = CodeTemplateLibrary.GetTemplates();

        foreach (var t in templates)
        {
            if (t.Kind == WorkspaceItemKind.Notebook)
            {
                var nb = new NotebookDocumentItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Category = t.Category,
                    Created = DateTime.UtcNow.AddMinutes(-30),
                    LastModified = DateTime.UtcNow.AddMinutes(-10)
                };

                if (t.Id == "skiasharp_image_studio")
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 🎨 SkiaSharp Graphics & Rich Image Display\nThis notebook demonstrates resolving **SkiaSharp** via `#r \"nuget: ...\"`, performing hardware-accelerated 2D vector drawing, and rendering high-resolution graphics directly into the cell output via `Display.Image(...)`.\n\nHit **[ ▶ ]** to run the cell below!",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = t.InitialCode
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 2: Draw a second graphic reusing surface/canvas concepts
var pieInfo = new SKImageInfo(380, 200);
using var pieSurface = SKSurface.Create(pieInfo);
var pieCanvas = pieSurface.Canvas;
pieCanvas.Clear(new SKColor(20, 26, 38));

using var piePaint = new SKPaint { IsAntialias = true };
var rect = new SKRect(40, 20, 200, 180);

piePaint.Color = new SKColor(168, 199, 250);
pieCanvas.DrawArc(rect, 0, 120, true, piePaint);

piePaint.Color = new SKColor(102, 157, 246);
pieCanvas.DrawArc(rect, 120, 150, true, piePaint);

piePaint.Color = new SKColor(234, 134, 143);
pieCanvas.DrawArc(rect, 270, 90, true, piePaint);

// Render rich pie chart image
Display.Image(pieSurface.Snapshot());
Console.WriteLine(""✅ Pie chart generated and displayed in cell output."");"
                    });
                }
                else if (t.Id == "animation_studio")
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 🎞️ Live Animation Studio\nTwo ways to animate inside a notebook cell:\n\n1. **`Display.Animate(...)`** — the recommended way. Hand it a draw callback; it returns a live, self-driving control that keeps animating on its own, without holding up the cell or blocking any other tab. Just call it and move on.\n2. **A cancellable frame loop** — call `Display.Image(...)` yourself in a `for`/`while` loop. This needs an explicit `Display.ThrowIfCancellationRequested()` (or checking `Display.CancellationToken`) every iteration to actually be stoppable — a loop that never checks is just as stuck as a raw `while(true)`. Use this only for short, bounded sequences; reach for `Display.Animate` for anything open-ended.\n\nHit **[ ▶ ]** on each cell below (or **Run All**).",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = t.InitialCode
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Secondary pattern: a plain loop calling Display.Image(...) repeatedly. Unlike Display.Animate
// above, this needs an explicit cancellation check every single iteration to be stoppable at all —
// prefer Display.Animate for anything long-running or open-ended; reach for this only for a short,
// bounded, finite sequence of frames.
#r ""nuget: SkiaSharp, 3.119.4""
using System.Threading.Tasks;
using SkiaSharp;

int totalFrames = 40;
for (int frame = 0; frame < totalFrames; frame++)
{
    Display.ThrowIfCancellationRequested();

    var info = new SKImageInfo(300, 120);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(new SKColor(21, 27, 43));

    using var paint = new SKPaint { Color = new SKColor(244, 114, 182), IsAntialias = true };
    float x = 20 + (260 - 20) * frame / (float)(totalFrames - 1);
    canvas.DrawCircle(x, 60, 14, paint);

    Display.Image(surface.Snapshot());
    await Task.Delay(33, Display.CancellationToken);
}

Console.WriteLine($""Rendered {totalFrames} frames — press Stop mid-run to see this one actually halt (unlike a raw while (true) loop, which can't be)."");"
                    });
                }
                else if (t.Id == "rich_html_reports")
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 📝 Rich Text & Markdown Reports\nFormat cell output as real headings, bold/italic text, inline code, and links with `Display.Markdown(...)` — it reuses the notebook's existing Markdown cell rendering to convert a small Markdown subset into rich output.\n\nFor markup Markdown doesn't reach (like lists), call `Display.Html(...)` directly.\n\nHit **[ ▶ ]** on each cell below!",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = t.InitialCode
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Display.Html accepts raw markup the Markdown shorthand doesn't reach, like lists.
Display.Html(@""<h2>Action Items</h2>
<ul>
  <li><b>Finance:</b> reconcile Q3 automation savings against manual baseline.</li>
  <li><b>Engineering:</b> ship the batch pipeline health dashboard.</li>
  <li><b>Support:</b> publish the updated <code>PdfDocument</code> migration guide.</li>
</ul>
<p>Owner sign-off required by <b>end of week</b>.</p>"");

Console.WriteLine(""Rendered raw HTML directly - useful whenever the Markdown shorthand isn't enough."");"
                    });
                }
                else if (t.Id == "nuget_charting_scottplot")
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 📈 Charting with ScottPlot (NuGet)\nResolves **ScottPlot.Avalonia** via `#r \"nuget: ...\"` and hands the live chart control straight to `Display.Control(...)` — the same generic `#r nuget` + `Display.Control` mechanism works for any Avalonia-based visualization package, not just this one.\n\nHit **[ ▶ ]** on each cell below!",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = t.InitialCode
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 2: A second chart, reusing the ScottPlot.Avalonia package already resolved above.
var barPlot = new AvaPlot { Width = 520, Height = 300 };
var bp = barPlot.Plot;

string[] categories = { ""PDF"", ""DOCX"", ""XLSX"", ""PPTX"", ""Images"" };
double[] counts = { 420, 180, 96, 64, 233 };

for (int i = 0; i < categories.Length; i++)
{
    bp.Add.Bar(position: i + 1, value: counts[i]);
}

bp.Axes.Bottom.SetTicks(new double[] { 1, 2, 3, 4, 5 }, categories);
bp.Title(""Documents Processed by Type"");
bp.YLabel(""Count"");

Display.Control(barPlot);
Console.WriteLine(""A second live chart, reusing ScottPlot.Avalonia resolved by the first cell."");"
                    });
                }
                else if (t.Id == "nuget_dataframe_analysis")
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 📊 Data Science with Microsoft.Data.Analysis\nAnalyze, manipulate, and explore tabular datasets using Microsoft's official DataFrame library for .NET.\nIn FryPDF C# Code Studio, **evaluating a `DataFrame` directly** or calling **`Display.Table(df)`** / **`df.Dump()`** renders a native interactive table with export to Excel/TSV, CSV, and JSON!\n\nHit **[ ▶ ]** to run the cell below!",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = t.InitialCode
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 2: Compute computed metrics and filter rows
df[""Total Value ($)""] = (SingleDataFrameColumn)df[""Unit Price ($)""] * (Int32DataFrameColumn)df[""Stock Qty""];

// Display updated table with calculated inventory values
Display.Table(df, ""Computed Inventory Valuations"");"
                    });
                }
                else
                {
                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Markdown,
                        Source = "# 📓 Polyglot Notebook: Document Automation\nInteractive C# workflow inside FryPDF with stateful variable sharing across cells.\nVariables declared in Cell 1 persist into Cell 2 and beyond!\nHit **[ ▶ ]** on each cell or **Run All** in the toolbar.",
                        IsMarkdownPreviewMode = true
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 1: Initialize document batch
string docName = ""Quarterly_Report_2026.pdf"";
int totalPages = 32;
new { Document = docName, Pages = totalPages, Status = ""Pending"" }.Dump(""Initial State"");"
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 2: Compute page size metrics (Reading totalPages from previous cell!)
var pageWeights = Enumerable.Range(1, totalPages).Select(p => p * 12.4).ToList();
Console.WriteLine($""Total estimated payload for '{docName}': {pageWeights.Sum():F1} KB"");
pageWeights.Take(5).Dump(""First 5 Page Weights (KB)"");"
                    });

                    nb.Cells.Add(new NotebookCellItem
                    {
                        Type = CellType.Code,
                        Source = @"// Step 3: Interactive UI Control
var slider = new Avalonia.Controls.Slider { Minimum = 1, Maximum = 100, Value = totalPages, Width = 320 };
Display.Control(slider);
Console.WriteLine($""Created live interactive Slider control initialized to totalPages = {totalPages}"");"
                    });
                }

                await storage.SaveNotebookAsync(nb);
            }
            else
            {
                var script = new ScriptDocumentItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Category = t.Category,
                    ExecutionMode = "Statements",
                    Code = t.InitialCode,
                    Notes = t.Notes,
                    TestCases = t.TestCases ?? new List<TestCaseItem>(),
                    Created = DateTime.UtcNow.AddMinutes(-45),
                    LastModified = DateTime.UtcNow.AddMinutes(-15)
                };

                await storage.SaveScriptAsync(script);
            }
        }
    }
}
