using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class LocalScriptStorageService : IScriptStorageService
{
    private readonly string _baseDir;
    private readonly string _scriptsDir;
    private readonly string _notebooksDir;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private bool _initialized;

    public LocalScriptStorageService(string? customBaseDir = null)
    {
        if (!string.IsNullOrEmpty(customBaseDir))
        {
            _baseDir = customBaseDir;
        }
        else
        {
            _baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FryPDF",
                "Plugins",
                "com.frypdf.plugin.csharpeditor");
        }

        _scriptsDir = Path.Combine(_baseDir, "scripts");
        _notebooksDir = Path.Combine(_baseDir, "notebooks");

        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_scriptsDir);
        Directory.CreateDirectory(_notebooksDir);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        var hasScripts = Directory.GetFiles(_scriptsDir, "*.frycs").Length > 0;
        var hasNotebooks = Directory.GetFiles(_notebooksDir, "*.frynb").Length > 0;

        if (!hasScripts && !hasNotebooks)
        {
            await SeedDefaultsAsync();
        }

        _initialized = true;
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

                nb.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Markdown,
                    Source = "# 📓 Polyglot Notebook: Document Automation\nInteractive C# workflow inside FryPDF with isolated cell execution.\nHit **[ ▶ ]** on any cell or **Run All** in the toolbar.",
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
                    Source = @"// Step 2: Compute page size metrics
var pageWeights = Enumerable.Range(1, totalPages).Select(p => p * 12.4).ToList();
Console.WriteLine($""Total estimated payload: {pageWeights.Sum():F1} KB"");
pageWeights.Take(5).Dump(""First 5 Page Weights (KB)"");"
                });

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

    public async Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync()
    {
        await EnsureInitializedAsync();
        var list = new List<WorkspaceItemSummary>();

        // 1. Load Scripts
        foreach (var file in Directory.GetFiles(_scriptsDir, "*.frycs"))
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
                        ExecutionMode = script.ExecutionMode
                    });
                }
            }
            catch { }
        }

        // 2. Load Notebooks
        foreach (var file in Directory.GetFiles(_notebooksDir, "*.frynb"))
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
                        CellCount = nb.Cells.Count
                    });
                }
            }
            catch { }
        }

        return list.OrderByDescending(x => x.LastModified).ToList();
    }

    public async Task<ScriptDocumentItem?> LoadScriptAsync(string id)
    {
        await EnsureInitializedAsync();
        var file = Path.Combine(_scriptsDir, $"{id}.frycs");
        if (!File.Exists(file)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<ScriptDocumentItem>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveScriptAsync(ScriptDocumentItem script)
    {
        var file = Path.Combine(_scriptsDir, $"{script.Id}.frycs");
        script.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(script, _jsonOptions);
        await File.WriteAllTextAsync(file, json);
    }

    public async Task<NotebookDocumentItem?> LoadNotebookAsync(string id)
    {
        await EnsureInitializedAsync();
        var file = Path.Combine(_notebooksDir, $"{id}.frynb");
        if (!File.Exists(file)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize<NotebookDocumentItem>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveNotebookAsync(NotebookDocumentItem notebook)
    {
        var file = Path.Combine(_notebooksDir, $"{notebook.Id}.frynb");
        notebook.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(notebook, _jsonOptions);
        await File.WriteAllTextAsync(file, json);
    }

    public async Task<ScriptDocumentItem> CreateNewScriptAsync(string title = "New Script", string? templateId = null)
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

        await SaveScriptAsync(script);
        return script;
    }

    public async Task<NotebookDocumentItem> CreateNewNotebookAsync(string title = "New Notebook", string? templateId = null)
    {
        await EnsureInitializedAsync();
        var notebook = new NotebookDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(title) ? "New Interactive Notebook" : title,
            Description = "Interactive cell-based notebook",
            Category = "Interactive",
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
            Source = "// C# Code Cell\nConsole.WriteLine(\"Hello from Notebook cell!\");"
        });

        await SaveNotebookAsync(notebook);
        return notebook;
    }

    public async Task DeleteItemAsync(string id)
    {
        var scriptFile = Path.Combine(_scriptsDir, $"{id}.frycs");
        if (File.Exists(scriptFile))
        {
            File.Delete(scriptFile);
        }

        var nbFile = Path.Combine(_notebooksDir, $"{id}.frynb");
        if (File.Exists(nbFile))
        {
            File.Delete(nbFile);
        }

        await Task.CompletedTask;
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
