using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class LocalScriptStorageService : IScriptStorageService
{
    private readonly string _baseDir;
    private readonly string _libraryRoot;
    private readonly string _legacyScriptsDir;
    private readonly string _legacyNotebooksDir;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    public LocalScriptStorageService(string? customBaseDir = null)
    {
        _baseDir = !string.IsNullOrEmpty(customBaseDir)
            ? customBaseDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FryPDF",
                "Plugins",
                "com.frypdf.plugin.csharpeditor");

        _libraryRoot = Path.Combine(_baseDir, "library");
        _legacyScriptsDir = Path.Combine(_baseDir, "scripts");
        _legacyNotebooksDir = Path.Combine(_baseDir, "notebooks");

        Directory.CreateDirectory(_libraryRoot);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            MigrateLegacyLayout();

            var hasAnyDocument = Directory.EnumerateFiles(_libraryRoot, "*", SearchOption.AllDirectories)
                .Any(f => f.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase));

            if (!hasAnyDocument)
            {
                await SeedDefaultsAsync();
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// One-time, idempotent migration from the old flat "scripts/" + "notebooks/" layout into the
    /// unified "library/" root. Legacy filenames are already "{id}.ext" with globally-unique GUID ids,
    /// so a straight move can never collide. Leaves the (now empty) legacy folders in place — their
    /// emptiness is itself the "already migrated" signal, so no separate version flag is needed.
    /// </summary>
    private void MigrateLegacyLayout()
    {
        MigrateLegacyDirectory(_legacyScriptsDir, "*.frycs");
        MigrateLegacyDirectory(_legacyNotebooksDir, "*.frynb");
    }

    private void MigrateLegacyDirectory(string legacyDir, string searchPattern)
    {
        if (!Directory.Exists(legacyDir)) return;

        foreach (var file in Directory.GetFiles(legacyDir, searchPattern))
        {
            try
            {
                var destination = Path.Combine(_libraryRoot, Path.GetFileName(file));
                if (!File.Exists(destination))
                {
                    File.Move(file, destination);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to migrate legacy file '{file}': {ex.Message}");
            }
        }
    }

    private async Task SeedDefaultsAsync()
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

                await SaveNotebookAsync(nb);
            }
            else
            {
                var script = new ScriptDocumentItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Category = t.Category,
                    ExecutionMode = t.Id == "pdf_automation" ? "Program" : "Statements",
                    Code = t.InitialCode,
                    Notes = t.Notes,
                    TestCases = t.TestCases ?? new List<TestCaseItem>(),
                    Created = DateTime.UtcNow.AddMinutes(-45),
                    LastModified = DateTime.UtcNow.AddMinutes(-15)
                };

                await SaveScriptAsync(script);
            }
        }
    }

    private string GetFolderPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? _libraryRoot;
        var rel = Path.GetRelativePath(_libraryRoot, dir);
        return rel == "." ? string.Empty : rel.Replace(Path.DirectorySeparatorChar, '/');
    }

    private string? FindExistingFilePath(string id, string extension)
    {
        return Directory.EnumerateFiles(_libraryRoot, $"{id}{extension}", SearchOption.AllDirectories).FirstOrDefault();
    }

    public async Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync()
    {
        await EnsureInitializedAsync();
        var list = new List<WorkspaceItemSummary>();

        // 1. Load Scripts
        foreach (var file in Directory.EnumerateFiles(_libraryRoot, "*.frycs", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var script = JsonSerializer.Deserialize<ScriptDocumentItem>(json);
                if (script != null)
                {
                    list.Add(new WorkspaceItemSummary
                    {
                        Id = script.Id,
                        Title = script.Title,
                        Description = script.Description,
                        Category = script.Category,
                        Kind = WorkspaceItemKind.Script,
                        LastModified = script.LastModified,
                        ExecutionCount = script.ExecutionCount,
                        ExecutionMode = script.ExecutionMode,
                        FolderPath = GetFolderPath(file)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Skipping corrupted script file '{file}': {ex.Message}");
            }
        }

        // 2. Load Notebooks
        foreach (var file in Directory.EnumerateFiles(_libraryRoot, "*.frynb", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var nb = JsonSerializer.Deserialize<NotebookDocumentItem>(json);
                if (nb != null)
                {
                    list.Add(new WorkspaceItemSummary
                    {
                        Id = nb.Id,
                        Title = nb.Title,
                        Description = nb.Description,
                        Category = nb.Category,
                        Kind = WorkspaceItemKind.Notebook,
                        LastModified = nb.LastModified,
                        ExecutionCount = nb.ExecutionCount,
                        CellCount = nb.Cells.Count,
                        FolderPath = GetFolderPath(file)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Skipping corrupted notebook file '{file}': {ex.Message}");
            }
        }

        return list.OrderByDescending(x => x.LastModified).ToList();
    }

    public async Task<ScriptDocumentItem?> LoadScriptAsync(string id)
    {
        await EnsureInitializedAsync();
        var file = FindExistingFilePath(id, ".frycs");
        if (file == null) return null;

        try
        {
            var json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<ScriptDocumentItem>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to load script '{id}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SaveScriptAsync(ScriptDocumentItem script)
    {
        try
        {
            var file = FindExistingFilePath(script.Id, ".frycs") ?? Path.Combine(_libraryRoot, $"{script.Id}.frycs");
            script.LastModified = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(script, _jsonOptions);
            await File.WriteAllTextAsync(file, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save script '{script.Id}': {ex.Message}");
            return false;
        }
    }

    public async Task<NotebookDocumentItem?> LoadNotebookAsync(string id)
    {
        await EnsureInitializedAsync();
        var file = FindExistingFilePath(id, ".frynb");
        if (file == null) return null;

        try
        {
            var json = await File.ReadAllTextAsync(file);
            var nb = JsonSerializer.Deserialize<NotebookDocumentItem>(json);
            if (nb != null)
            {
                bool migrated = false;
                foreach (var cell in nb.Cells)
                {
                    if (cell.Source?.Contains("4.154.0-preview.1.26454.9") == true)
                    {
                        cell.Source = cell.Source.Replace("4.154.0-preview.1.26454.9", "3.119.4");
                        migrated = true;
                    }
                }
                if (migrated)
                {
                    await SaveNotebookAsync(nb);
                }
            }
            return nb;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to load notebook '{id}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SaveNotebookAsync(NotebookDocumentItem notebook)
    {
        try
        {
            var file = FindExistingFilePath(notebook.Id, ".frynb") ?? Path.Combine(_libraryRoot, $"{notebook.Id}.frynb");
            notebook.LastModified = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(notebook, _jsonOptions);
            await File.WriteAllTextAsync(file, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save notebook '{notebook.Id}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Writes a brand-new document directly into the requested folder. Used only by the
    /// CreateNew*Async factory methods — SaveScriptAsync/SaveNotebookAsync intentionally fall back to
    /// the library root for "not found anywhere" documents, which would silently ignore a requested
    /// folder for a document that has never been written before.
    /// </summary>
    private async Task WriteNewDocumentAsync(string id, string extension, string? folderPath, string json)
    {
        var targetDir = string.IsNullOrEmpty(folderPath) ? _libraryRoot : Path.Combine(_libraryRoot, folderPath);
        Directory.CreateDirectory(targetDir);
        var file = Path.Combine(targetDir, $"{id}{extension}");
        await File.WriteAllTextAsync(file, json);
    }

    public async Task<ScriptDocumentItem> CreateNewScriptAsync(string title = "New Script", string? templateId = null, string? folderPath = null)
    {
        await EnsureInitializedAsync();
        var templates = CodeTemplateLibrary.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == templateId && t.Kind == WorkspaceItemKind.Script)
                       ?? templates.FirstOrDefault(t => t.Kind == WorkspaceItemKind.Script);

        var script = new ScriptDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(title) ? (template?.Title ?? "New Script") : title,
            Description = template?.Description ?? "Custom C# script",
            Category = template?.Category ?? "Custom",
            ExecutionMode = "Statements",
            Code = template?.InitialCode ?? "// Write C# Statements or top-level code here\nConsole.WriteLine(\"Hello from FryPDF!\");",
            Notes = template?.Notes ?? "# Documentation & Notes\nWrite notes, algorithm specs, or test plans here.",
            TestCases = template?.TestCases ?? new List<TestCaseItem>(),
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        try
        {
            await WriteNewDocumentAsync(script.Id, ".frycs", folderPath, JsonSerializer.Serialize(script, _jsonOptions));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create script '{script.Id}': {ex.Message}");
        }

        return script;
    }

    public async Task<NotebookDocumentItem> CreateNewNotebookAsync(string title = "New Notebook", string? templateId = null, string? folderPath = null)
    {
        await EnsureInitializedAsync();
        var templates = CodeTemplateLibrary.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == templateId && t.Kind == WorkspaceItemKind.Notebook);

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) && title != "New Notebook"
            ? title
            : (template?.Title ?? "New Interactive Notebook");

        var notebook = new NotebookDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = resolvedTitle,
            Description = template?.Description ?? "Interactive cell-based notebook",
            Category = template?.Category ?? "Interactive",
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = $"# 📓 {notebook.Title}\nWrite documentation or notes in this cell.",
            IsMarkdownPreviewMode = true
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = !string.IsNullOrWhiteSpace(template?.InitialCode)
                ? template.InitialCode
                : "// C# Code Cell\nConsole.WriteLine(\"Hello from Notebook cell!\");"
        });

        try
        {
            await WriteNewDocumentAsync(notebook.Id, ".frynb", folderPath, JsonSerializer.Serialize(notebook, _jsonOptions));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create notebook '{notebook.Id}': {ex.Message}");
        }

        return notebook;
    }

    public Task DeleteItemAsync(string id)
    {
        var scriptFile = FindExistingFilePath(id, ".frycs");
        if (scriptFile != null)
        {
            File.Delete(scriptFile);
        }

        var nbFile = FindExistingFilePath(id, ".frynb");
        if (nbFile != null)
        {
            File.Delete(nbFile);
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> LoadFolderPathsAsync()
    {
        var result = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(_libraryRoot, "*", SearchOption.AllDirectories))
        {
            result.Add(Path.GetRelativePath(_libraryRoot, dir).Replace(Path.DirectorySeparatorChar, '/'));
        }
        return Task.FromResult(result);
    }

    public Task<string> CreateFolderAsync(string? parentFolderPath, string desiredName)
    {
        var parentDir = string.IsNullOrEmpty(parentFolderPath) ? _libraryRoot : Path.Combine(_libraryRoot, parentFolderPath);
        Directory.CreateDirectory(parentDir);

        var safeName = SanitizeFolderName(desiredName);
        var finalName = safeName;
        var suffix = 1;
        while (Directory.Exists(Path.Combine(parentDir, finalName)))
        {
            suffix++;
            finalName = $"{safeName} ({suffix})";
        }

        Directory.CreateDirectory(Path.Combine(parentDir, finalName));

        var relativePath = string.IsNullOrEmpty(parentFolderPath) ? finalName : $"{parentFolderPath}/{finalName}";
        return Task.FromResult(relativePath);
    }

    public Task<string> RenameFolderAsync(string folderPath, string newName)
    {
        var sourceDir = Path.Combine(_libraryRoot, folderPath);
        var parentRelative = Path.GetDirectoryName(folderPath)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
        var parentDir = string.IsNullOrEmpty(parentRelative) ? _libraryRoot : Path.Combine(_libraryRoot, parentRelative);
        var safeName = SanitizeFolderName(newName);
        var destDir = Path.Combine(parentDir, safeName);
        var newRelativePath = string.IsNullOrEmpty(parentRelative) ? safeName : $"{parentRelative}/{safeName}";

        if (string.Equals(sourceDir, destDir, StringComparison.Ordinal))
        {
            return Task.FromResult(newRelativePath);
        }

        if (string.Equals(sourceDir, destDir, StringComparison.OrdinalIgnoreCase))
        {
            // Case-only rename: case-insensitive-but-preserving filesystems (default on macOS/Windows)
            // need a two-step move through a temp name, or Directory.Move is a silent no-op.
            var tempDir = Path.Combine(parentDir, $"{safeName}__rename_{Guid.NewGuid():N}");
            Directory.Move(sourceDir, tempDir);
            Directory.Move(tempDir, destDir);
            return Task.FromResult(newRelativePath);
        }

        if (Directory.Exists(destDir))
        {
            throw new IOException($"A folder named '{safeName}' already exists here.");
        }

        Directory.Move(sourceDir, destDir);
        return Task.FromResult(newRelativePath);
    }

    public Task DeleteFolderAsync(string folderPath)
    {
        var dir = Path.Combine(_libraryRoot, folderPath);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static string SanitizeFolderName(string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "New Folder" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(c, '_');
        }
        return trimmed;
    }

    // Backward compatibility bridges
    public async Task<List<ScriptProjectItem>> LoadScriptsAsync()
    {
        var summaries = await LoadWorkspaceSummariesAsync();
        var results = new List<ScriptProjectItem>();
        foreach (var s in summaries)
        {
            if (s.IsNotebook)
            {
                var nb = await LoadNotebookAsync(s.Id);
                if (nb != null)
                {
                    results.Add(new ScriptProjectItem
                    {
                        Id = nb.Id,
                        Title = nb.Title,
                        Description = nb.Description,
                        IsNotebook = true,
                        Category = nb.Category,
                        Tag = "NOTEBOOK",
                        Cells = nb.Cells,
                        CreatedAt = nb.Created,
                        LastModified = nb.LastModified
                    });
                }
            }
            else
            {
                var sc = await LoadScriptAsync(s.Id);
                if (sc != null)
                {
                    results.Add(new ScriptProjectItem
                    {
                        Id = sc.Id,
                        Title = sc.Title,
                        Description = sc.Description,
                        IsNotebook = false,
                        Category = sc.Category,
                        Tag = "SCRIPT",
                        Code = sc.Code,
                        ExecutionMode = sc.ExecutionMode,
                        CreatedAt = sc.Created,
                        LastModified = sc.LastModified
                    });
                }
            }
        }
        return results;
    }

    public async Task SaveScriptsAsync(IEnumerable<ScriptProjectItem> scripts)
    {
        foreach (var item in scripts)
        {
            if (item.IsNotebook)
            {
                await SaveNotebookAsync(new NotebookDocumentItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    Category = item.Category,
                    Cells = item.Cells,
                    LastModified = item.LastModified
                });
            }
            else
            {
                await SaveScriptAsync(new ScriptDocumentItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    Category = item.Category,
                    Code = item.Code,
                    ExecutionMode = item.ExecutionMode,
                    LastModified = item.LastModified
                });
            }
        }
    }
}
