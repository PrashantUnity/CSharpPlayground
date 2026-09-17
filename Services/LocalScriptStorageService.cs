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
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;

    // A document created with an absolute (rooted) folderPath is saved exactly where the user chose
    // instead of under _libraryRoot. To find it again, this service writes a small project file
    // (".frynbproj" for notebooks, ".frycsproj" for scripts, named after the folder itself) directly
    // INTO that external folder, listing every document of that kind saved there — one project file
    // per external folder per kind, not one index row per document, so several documents saved to the
    // same external folder share a single entry. This mirrors a real IDE's project file (.csproj/.sln):
    // it's a visible, portable artifact that travels with the folder, rather than a hidden id -> path
    // mapping trapped in this plugin's own private data directory. The plugin still needs to remember
    // WHERE each project file is, though — that's the one thing that doesn't survive the folder being
    // moved — so the (much smaller) list of known project-file paths is persisted to its own file
    // here, never matched by the *.frycs/*.frynb library scan.
    private readonly string _externalProjectsIndexPath;
    private List<string> _externalProjectPaths = new();

    private class ExternalProjectFile
    {
        public string Kind { get; set; } = nameof(WorkspaceItemKind.Notebook);
        public List<ExternalProjectDocument> Documents { get; set; } = new();
    }

    private class ExternalProjectDocument
    {
        public string Id { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
    }

    public string LibraryRootPath => _libraryRoot;

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
        _externalProjectsIndexPath = Path.Combine(_baseDir, "external_projects.json");

        Directory.CreateDirectory(_libraryRoot);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            await LoadExternalProjectsIndexAsync();

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

    private async Task LoadExternalProjectsIndexAsync()
    {
        if (!File.Exists(_externalProjectsIndexPath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(_externalProjectsIndexPath);
            var loaded = JsonSerializer.Deserialize<List<string>>(json);
            if (loaded != null)
            {
                _externalProjectPaths = loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to load external projects index: {ex.Message}");
        }
    }

    private async Task SaveExternalProjectsIndexAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_externalProjectPaths, _jsonOptions);
            await File.WriteAllTextAsync(_externalProjectsIndexPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save external projects index: {ex.Message}");
        }
    }

    private async Task<ExternalProjectFile?> ReadProjectFileAsync(string projectFilePath)
    {
        if (!File.Exists(projectFilePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(projectFilePath);
            return JsonSerializer.Deserialize<ExternalProjectFile>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to read external project file '{projectFilePath}': {ex.Message}");
            return null;
        }
    }

    private async Task WriteProjectFileAsync(string projectFilePath, ExternalProjectFile project)
    {
        try
        {
            var json = JsonSerializer.Serialize(project, _jsonOptions);
            await File.WriteAllTextAsync(projectFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to write external project file '{projectFilePath}': {ex.Message}");
        }
    }

    private static string GetProjectFilePath(string targetDir, WorkspaceItemKind kind)
    {
        var folderName = Path.GetFileName(targetDir.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(folderName)) folderName = "Project";
        var ext = kind == WorkspaceItemKind.Notebook ? ".frynbproj" : ".frycsproj";
        return Path.Combine(targetDir, folderName + ext);
    }

    private async Task RegisterExternalDocumentAsync(string id, string extension, string targetDir, string fileName)
    {
        var kind = extension.Equals(".frynb", StringComparison.OrdinalIgnoreCase) ? WorkspaceItemKind.Notebook : WorkspaceItemKind.Script;
        var projectFilePath = GetProjectFilePath(targetDir, kind);

        var project = await ReadProjectFileAsync(projectFilePath) ?? new ExternalProjectFile { Kind = kind.ToString() };
        if (!project.Documents.Any(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            project.Documents.Add(new ExternalProjectDocument { Id = id, File = fileName });
        }
        await WriteProjectFileAsync(projectFilePath, project);

        if (!_externalProjectPaths.Contains(projectFilePath, StringComparer.OrdinalIgnoreCase))
        {
            _externalProjectPaths.Add(projectFilePath);
            await SaveExternalProjectsIndexAsync();
        }
    }

    private async Task RemoveFromExternalProjectIfPresentAsync(string id)
    {
        foreach (var projectFilePath in _externalProjectPaths.ToList())
        {
            var project = await ReadProjectFileAsync(projectFilePath);
            if (project == null) continue;

            var removed = project.Documents.RemoveAll(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) continue;

            if (project.Documents.Count == 0)
            {
                try
                {
                    File.Delete(projectFilePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete empty external project file '{projectFilePath}': {ex.Message}");
                }
                _externalProjectPaths.Remove(projectFilePath);
            }
            else
            {
                await WriteProjectFileAsync(projectFilePath, project);
            }

            await SaveExternalProjectsIndexAsync();
            return; // an id can only ever belong to one project file
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
                    ExecutionMode = "Statements",
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

    /// <summary>
    /// Locates a document's file by id. Tries the fast path first (a file literally named
    /// "{id}{extension}", true for everything created through this service today) and falls back to
    /// scanning file contents for a matching internal "Id" field if that fails — a document's filename
    /// doesn't always match its own Id (e.g. documents carried over from an older storage layout, or
    /// ones that otherwise ended up renamed on disk independently of their content). Load/Save/Delete
    /// all funnel through here, so all three need this fallback, not just the summary list — which
    /// already tolerates filename/Id mismatches since it reads every file's own content rather than
    /// assuming filename == id, but silently failed to ever re-open, re-save, or delete such a file
    /// before this fix (no exception — LoadNotebookAsync/LoadScriptAsync would just return null).
    /// </summary>
    private async Task<string?> FindExistingFilePathAsync(string id, string extension)
    {
        foreach (var projectFilePath in _externalProjectPaths)
        {
            var project = await ReadProjectFileAsync(projectFilePath);
            var doc = project?.Documents.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
            if (doc == null) continue;

            var projectDir = Path.GetDirectoryName(projectFilePath);
            if (projectDir == null) continue;

            var candidatePath = Path.Combine(projectDir, doc.File);
            if (candidatePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        var direct = Directory.EnumerateFiles(_libraryRoot, $"{id}{extension}", SearchOption.AllDirectories).FirstOrDefault();
        if (direct != null) return direct;

        foreach (var file in Directory.EnumerateFiles(_libraryRoot, $"*{extension}", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                if (doc.RootElement.TryGetProperty("Id", out var idProp) &&
                    string.Equals(idProp.GetString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            catch
            {
                // Unreadable/corrupted file — treat the same as "not this one" rather than throwing.
            }
        }

        return null;
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

        // 3. Load externally-saved documents, tracked via .frynbproj/.frycsproj project files that
        // live inside their own external folder (see RegisterExternalDocumentAsync). A project file
        // that no longer exists — its folder moved or deleted from outside the app — is just skipped
        // here rather than pruned, since this is a read path; DeleteItemAsync is what actually prunes
        // the index.
        foreach (var projectFilePath in _externalProjectPaths)
        {
            var project = await ReadProjectFileAsync(projectFilePath);
            if (project == null) continue;

            var folderPath = Path.GetDirectoryName(projectFilePath);
            if (folderPath == null) continue;

            foreach (var doc in project.Documents)
            {
                var path = Path.Combine(folderPath, doc.File);
                if (!File.Exists(path)) continue;

                try
                {
                    var json = await File.ReadAllTextAsync(path);

                    if (path.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase))
                    {
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
                                FolderPath = folderPath
                            });
                        }
                    }
                    else if (path.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase))
                    {
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
                                FolderPath = folderPath
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CSharpEditorPlugin] Skipping corrupted external document '{path}': {ex.Message}");
                }
            }
        }

        return list.OrderByDescending(x => x.LastModified).ToList();
    }

    public async Task<ScriptDocumentItem?> LoadScriptAsync(string id)
    {
        await EnsureInitializedAsync();
        var file = await FindExistingFilePathAsync(id, ".frycs");
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

    public async Task<bool> SaveScriptAsync(ScriptDocumentItem script, string? folderPath = null)
    {
        try
        {
            script.LastModified = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(script, _jsonOptions);

            var existing = await FindExistingFilePathAsync(script.Id, ".frycs");
            if (existing != null)
            {
                await File.WriteAllTextAsync(existing, json);
                return true;
            }

            // Never saved anywhere before (e.g. a fresh copy from Duplicate): route through the same
            // path CreateNewScriptAsync uses, so an explicit external folderPath is honored and
            // registered in that folder's project file instead of silently landing in the library root.
            await WriteNewDocumentAsync(script.Id, ".frycs", folderPath, json, script.Title);
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
        var file = await FindExistingFilePathAsync(id, ".frynb");
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

    public async Task<bool> SaveNotebookAsync(NotebookDocumentItem notebook, string? folderPath = null)
    {
        try
        {
            notebook.LastModified = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(notebook, _jsonOptions);

            var existing = await FindExistingFilePathAsync(notebook.Id, ".frynb");
            if (existing != null)
            {
                await File.WriteAllTextAsync(existing, json);
                return true;
            }

            // Never saved anywhere before (e.g. a fresh copy from Duplicate): route through the same
            // path CreateNewNotebookAsync uses, so an explicit external folderPath is honored and
            // registered in that folder's project file instead of silently landing in the library root.
            await WriteNewDocumentAsync(notebook.Id, ".frynb", folderPath, json, notebook.Title);
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
    ///
    /// A rooted (absolute) folderPath means the caller picked a folder outside the library entirely
    /// (via the OS's own native folder browser) — that document is saved exactly there and registered
    /// into a ".frynbproj"/".frycsproj" project file inside that same folder (see
    /// RegisterExternalDocumentAsync) so every later load/save/delete can still find it; a relative
    /// folderPath keeps the existing library-root-relative behavior.
    /// </summary>
    private async Task WriteNewDocumentAsync(string id, string extension, string? folderPath, string json, string title)
    {
        var isExternal = !string.IsNullOrEmpty(folderPath) && Path.IsPathRooted(folderPath);
        var targetDir = isExternal ? folderPath! : (string.IsNullOrEmpty(folderPath) ? _libraryRoot : Path.Combine(_libraryRoot, folderPath));
        Directory.CreateDirectory(targetDir);

        // Named after the document's title (like every other IDE), not its internal id — the id still
        // lives inside the JSON and is what every lookup (FindExistingFilePathAsync, project-file
        // Documents entries) actually keys off, so this is purely the on-disk display name.
        var safeName = SanitizeName(title, "Untitled");
        var finalName = safeName;
        var suffix = 1;
        while (File.Exists(Path.Combine(targetDir, $"{finalName}{extension}")))
        {
            suffix++;
            finalName = $"{safeName} ({suffix})";
        }

        var file = Path.Combine(targetDir, $"{finalName}{extension}");
        await File.WriteAllTextAsync(file, json);

        if (isExternal)
        {
            await RegisterExternalDocumentAsync(id, extension, targetDir, Path.GetFileName(file));
        }
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
            await WriteNewDocumentAsync(script.Id, ".frycs", folderPath, JsonSerializer.Serialize(script, _jsonOptions), script.Title);
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
            await WriteNewDocumentAsync(notebook.Id, ".frynb", folderPath, JsonSerializer.Serialize(notebook, _jsonOptions), notebook.Title);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create notebook '{notebook.Id}': {ex.Message}");
        }

        return notebook;
    }

    public async Task DeleteItemAsync(string id)
    {
        var scriptFile = await FindExistingFilePathAsync(id, ".frycs");
        if (scriptFile != null)
        {
            File.Delete(scriptFile);
        }

        var nbFile = await FindExistingFilePathAsync(id, ".frynb");
        if (nbFile != null)
        {
            File.Delete(nbFile);
        }

        await RemoveFromExternalProjectIfPresentAsync(id);
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

        var safeName = SanitizeName(desiredName, "New Folder");
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
        var safeName = SanitizeName(newName, "New Folder");
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

    private static string SanitizeName(string name, string fallback)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
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
