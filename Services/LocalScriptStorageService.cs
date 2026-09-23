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

    private readonly string _workspaceStatePath;
    private string? _activeWorkspaceRootPath;

    // In-memory only: lets a document opened from outside the active workspace root (a loose file,
    // or a tab left open across a root switch) still be found and saved in place via Ctrl+S, without
    // resurrecting a persistent cross-root registry.
    private readonly Dictionary<string, string> _knownFileLocations = new(StringComparer.OrdinalIgnoreCase);

    private class WorkspaceState
    {
        public string? ActiveRootPath { get; set; }
    }

    public string LibraryRootPath => _libraryRoot;

    private string EffectiveWorkspaceRoot => _activeWorkspaceRootPath ?? _libraryRoot;
    public string ActiveWorkspaceRootPath => EffectiveWorkspaceRoot;
    public bool IsExternalWorkspaceActive => _activeWorkspaceRootPath != null;
    public event Action? ActiveWorkspaceChanged;

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
        _workspaceStatePath = Path.Combine(_baseDir, "workspace_state.json");

        Directory.CreateDirectory(_libraryRoot);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            await LoadWorkspaceStateAsync();

            // Starter templates are offered one at a time from the "New from Template" gallery
            // (CodeTemplateLibrary via CSharpManagerViewModel.StarterTemplates); the library is never
            // auto-populated with all of them, so a first-run Explorer starts empty until the user
            // creates something.
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task LoadWorkspaceStateAsync()
    {
        if (!File.Exists(_workspaceStatePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(_workspaceStatePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json);
            if (state?.ActiveRootPath != null && Directory.Exists(state.ActiveRootPath))
            {
                _activeWorkspaceRootPath = state.ActiveRootPath;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to load workspace state: {ex.Message}");
        }
    }

    private async Task SaveWorkspaceStateAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(new WorkspaceState { ActiveRootPath = _activeWorkspaceRootPath }, _jsonOptions);
            await File.WriteAllTextAsync(_workspaceStatePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save workspace state: {ex.Message}");
        }
    }

    private string GetFolderPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? EffectiveWorkspaceRoot;
        var rel = Path.GetRelativePath(EffectiveWorkspaceRoot, dir);
        return rel == "." ? string.Empty : rel.Replace(Path.DirectorySeparatorChar, '/');
    }

    private async Task<string?> FindExistingFilePathAsync(string id, string extension)
    {
        if (_knownFileLocations.TryGetValue(id, out var cached) &&
            cached.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && File.Exists(cached))
        {
            return cached;
        }

        var root = EffectiveWorkspaceRoot;
        var direct = Directory.EnumerateFiles(root, $"{id}{extension}", SearchOption.AllDirectories).FirstOrDefault();
        if (direct != null)
        {
            _knownFileLocations[id] = direct;
            return direct;
        }

        foreach (var file in Directory.EnumerateFiles(root, $"*{extension}", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                if (doc.RootElement.TryGetProperty("Id", out var idProp) &&
                    string.Equals(idProp.GetString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    _knownFileLocations[id] = file;
                    return file;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public async Task<List<WorkspaceItemSummary>> LoadWorkspaceSummariesAsync()
    {
        await EnsureInitializedAsync();
        var root = EffectiveWorkspaceRoot;
        var list = new List<WorkspaceItemSummary>();

        foreach (var file in Directory.EnumerateFiles(root, "*.frycs", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var script = JsonSerializer.Deserialize<ScriptDocumentItem>(json);
                if (script != null)
                {
                    _knownFileLocations[script.Id] = file;
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
                        FolderPath = GetFolderPath(file),
                        IsExternalRoot = IsExternalWorkspaceActive,
                        WorkspaceRootName = IsExternalWorkspaceActive ? Path.GetFileName(root.TrimEnd('/', '\\')) : null
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Skipping corrupted script file '{file}': {ex.Message}");
            }
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.frynb", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var nb = JsonSerializer.Deserialize<NotebookDocumentItem>(json);
                if (nb != null)
                {
                    _knownFileLocations[nb.Id] = file;
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
                        FolderPath = GetFolderPath(file),
                        IsExternalRoot = IsExternalWorkspaceActive,
                        WorkspaceRootName = IsExternalWorkspaceActive ? Path.GetFileName(root.TrimEnd('/', '\\')) : null
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

            await WriteNewDocumentAsync(notebook.Id, ".frynb", folderPath, json, notebook.Title);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save notebook '{notebook.Id}': {ex.Message}");
            return false;
        }
    }

    private async Task WriteNewDocumentAsync(string id, string extension, string? folderPath, string json, string title)
    {
        var root = EffectiveWorkspaceRoot;
        var targetDir = string.IsNullOrEmpty(folderPath) ? root : Path.Combine(root, folderPath);
        Directory.CreateDirectory(targetDir);

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
        _knownFileLocations[id] = file;
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

        _knownFileLocations.Remove(id);
    }

    public Task<List<string>> LoadFolderPathsAsync()
    {
        var root = EffectiveWorkspaceRoot;
        var result = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            result.Add(Path.GetRelativePath(root, dir).Replace(Path.DirectorySeparatorChar, '/'));
        }
        return Task.FromResult(result);
    }

    public Task<string> CreateFolderAsync(string? parentFolderPath, string desiredName)
    {
        var root = EffectiveWorkspaceRoot;
        var parentDir = string.IsNullOrEmpty(parentFolderPath) ? root : Path.Combine(root, parentFolderPath);
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
        var root = EffectiveWorkspaceRoot;
        var sourceDir = Path.Combine(root, folderPath);
        var parentRelative = Path.GetDirectoryName(folderPath)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
        var parentDir = string.IsNullOrEmpty(parentRelative) ? root : Path.Combine(root, parentRelative);
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
        var dir = Path.Combine(EffectiveWorkspaceRoot, folderPath);
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

    public async Task<OpenProjectResult> OpenExternalProjectAsync(string rawPath)
    {
        await EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return new OpenProjectResult(false, "Project path cannot be empty.");
        }

        var path = rawPath.Trim();

        if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var extractDirName = Path.GetFileNameWithoutExtension(path);
                var parentDir = Path.GetDirectoryName(path) ?? _libraryRoot;
                var extractDir = Path.Combine(parentDir, extractDirName);
                if (Directory.Exists(extractDir))
                {
                    extractDir = Path.Combine(parentDir, $"{extractDirName}_{DateTime.UtcNow:yyyyMMddHHmmss}");
                }
                Directory.CreateDirectory(extractDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(path, extractDir);
                path = extractDir;
            }
            catch (Exception ex)
            {
                return new OpenProjectResult(false, $"Failed to unpack project archive: {ex.Message}");
            }
        }

        if (Directory.Exists(path))
        {
            return await OpenFolderAsync(path);
        }

        if (!File.Exists(path))
        {
            return new OpenProjectResult(false, $"File or directory not found: '{path}'");
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // Legacy .frycsproj/.frynbproj files from older versions: just open their containing folder —
        // the real-filesystem walk in LoadWorkspaceSummariesAsync picks up everything in it directly.
        if (ext is ".frycsproj" or ".frynbproj")
        {
            var folder = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(folder) && Directory.Exists(folder)
                ? await OpenFolderAsync(folder)
                : new OpenProjectResult(false, $"Could not resolve folder for '{path}'.");
        }

        if (ext == ".frynb")
        {
            return await OpenLooseNotebookAsync(path);
        }

        if (ext == ".frycs")
        {
            return await OpenLooseScriptAsync(path);
        }

        if (ext is ".cs" or ".csx")
        {
            return await OpenCsSourceFileAsync(path);
        }

        if (ext == ".csproj")
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                return await OpenFolderAsync(folder);
            }
        }

        return new OpenProjectResult(false, $"Unsupported project file format: '{ext}'. Supported formats: .frycsproj, .frynbproj, .frycs, .frynb, .cs, .csx, .csproj, .zip");
    }

    private async Task<OpenProjectResult> OpenFolderAsync(string dirPath)
    {
        var full = Path.GetFullPath(dirPath.TrimEnd('/', '\\'));
        if (!Directory.Exists(full))
        {
            return new OpenProjectResult(false, $"Folder not found: '{full}'");
        }

        _activeWorkspaceRootPath = string.Equals(full, _libraryRoot, StringComparison.OrdinalIgnoreCase) ? null : full;
        await SaveWorkspaceStateAsync();
        ActiveWorkspaceChanged?.Invoke();

        var summaries = await LoadWorkspaceSummariesAsync();
        var primary = summaries.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        var folderName = Path.GetFileName(full) is { Length: > 0 } n ? n : full;

        return new OpenProjectResult(
            Success: true,
            Message: $"Opened folder '{folderName}' ({summaries.Count} document{(summaries.Count == 1 ? "" : "s")})",
            PrimaryDocumentId: primary?.Id,
            PrimaryDocumentKind: primary?.Kind,
            DocumentsLoadedCount: summaries.Count);
    }

    private async Task<OpenProjectResult> OpenLooseNotebookAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var nb = JsonSerializer.Deserialize<NotebookDocumentItem>(json);
            if (nb == null)
            {
                return new OpenProjectResult(false, $"Failed to parse notebook JSON in '{Path.GetFileName(filePath)}'.");
            }

            _knownFileLocations[nb.Id] = filePath;

            return new OpenProjectResult(
                Success: true,
                Message: $"Loaded notebook '{nb.Title}'",
                PrimaryDocumentId: nb.Id,
                PrimaryDocumentKind: WorkspaceItemKind.Notebook,
                DocumentsLoadedCount: 1);
        }
        catch (Exception ex)
        {
            return new OpenProjectResult(false, $"Failed to open notebook: {ex.Message}");
        }
    }

    private async Task<OpenProjectResult> OpenLooseScriptAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var script = JsonSerializer.Deserialize<ScriptDocumentItem>(json);
            if (script == null)
            {
                return new OpenProjectResult(false, $"Failed to parse script JSON in '{Path.GetFileName(filePath)}'.");
            }

            _knownFileLocations[script.Id] = filePath;

            return new OpenProjectResult(
                Success: true,
                Message: $"Loaded script '{script.Title}'",
                PrimaryDocumentId: script.Id,
                PrimaryDocumentKind: WorkspaceItemKind.Script,
                DocumentsLoadedCount: 1);
        }
        catch (Exception ex)
        {
            return new OpenProjectResult(false, $"Failed to open script: {ex.Message}");
        }
    }

    private async Task<OpenProjectResult> OpenCsSourceFileAsync(string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var dir = Path.GetDirectoryName(filePath) ?? _libraryRoot;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var frycsPath = Path.Combine(dir, $"{baseName}.frycs");

            ScriptDocumentItem script;
            if (File.Exists(frycsPath))
            {
                var existingJson = await File.ReadAllTextAsync(frycsPath);
                script = JsonSerializer.Deserialize<ScriptDocumentItem>(existingJson) ?? new ScriptDocumentItem();
                script.Code = code;
                script.LastModified = DateTime.UtcNow;
                await File.WriteAllTextAsync(frycsPath, JsonSerializer.Serialize(script, _jsonOptions));
            }
            else
            {
                script = new ScriptDocumentItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = baseName,
                    Category = "Imported",
                    Description = $"Imported from {Path.GetFileName(filePath)}",
                    Code = code,
                    ExecutionMode = code.Contains("static void Main") || code.Contains("class ") ? "Program" : "Statements",
                    Created = File.GetCreationTimeUtc(filePath),
                    LastModified = File.GetLastWriteTimeUtc(filePath)
                };
                await File.WriteAllTextAsync(frycsPath, JsonSerializer.Serialize(script, _jsonOptions));
            }

            _knownFileLocations[script.Id] = frycsPath;

            return new OpenProjectResult(
                Success: true,
                Message: $"Imported C# script '{script.Title}'",
                PrimaryDocumentId: script.Id,
                PrimaryDocumentKind: WorkspaceItemKind.Script,
                DocumentsLoadedCount: 1);
        }
        catch (Exception ex)
        {
            return new OpenProjectResult(false, $"Failed to import C# file: {ex.Message}");
        }
    }
}
