using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Plain source files (<c>main.py</c>): listed with the workspace's documents, opened and saved as their text so other
/// tools keep working on them, and never overwritten behind the user's back. A document's id is derived from its path,
/// so the same file has the same id in every session.
/// </summary>
public partial class LocalScriptStorageService
{
    private const string SourceFileIdPrefix = "src-";

    private readonly LanguageRegistry _languages;
    private readonly object _sourceGate = new();
    private readonly Dictionary<string, SourceFileState> _sourceFiles = new(StringComparer.OrdinalIgnoreCase);

    // What the file held when it was last read or written here: an implicit save only writes over that.
    private sealed class SourceFileState
    {
        public required string Path { get; set; }
        public required string LastSavedText { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public bool HasByteOrderMark { get; set; }
    }

    public LanguageRegistry Languages => _languages;

    public static bool IsSourceFileId(string? id) => id != null && id.StartsWith(SourceFileIdPrefix, StringComparison.Ordinal);

    /// <summary>The id of the source file at <paramref name="path"/>: the same path always gives the same id.</summary>
    public static string SourceFileId(string path)
    {
        var full = Path.GetFullPath(path);
        // macOS and Windows file systems ignore case by default: MAIN.PY and main.py are one file.
        if (!OperatingSystem.IsLinux()) full = full.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full));
        return SourceFileIdPrefix + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }

    private bool IsWorkspaceFile(string path) =>
        path.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase) ||
        _languages.FindSourceFileLanguage(path) != null;

    private string RegisterSourceFile(string path)
    {
        var full = Path.GetFullPath(path);
        var id = SourceFileId(full);
        _knownFileLocations[id] = full;
        return id;
    }

    private WorkspaceItemSummary SourceFileSummary(string file, ILanguageDefinition language, string root) => new()
    {
        Id = RegisterSourceFile(file),
        Title = Path.GetFileNameWithoutExtension(file),
        Description = $"{language.DisplayName} file",
        Category = language.DisplayName,
        Kind = WorkspaceItemKind.Script,
        LanguageId = language.Id,
        LanguageName = language.DisplayName,
        FileExtension = Path.GetExtension(file),
        IsSourceFile = true,
        LastModified = LastWriteTimeUtc(file),
        FolderPath = GetFolderPath(file),
        IsExternalRoot = IsExternalWorkspaceActive,
        WorkspaceRootName = IsExternalWorkspaceActive ? Path.GetFileName(root.TrimEnd('/', '\\')) : null
    };

    private async Task<string?> FindSourceFilePathAsync(string id)
    {
        if (_knownFileLocations.TryGetValue(id, out var known) && File.Exists(known)) return known;

        var root = EffectiveWorkspaceRoot;
        var files = await Task.Run(() => WorkspaceWalker.Files(root, f => _languages.FindSourceFileLanguage(f) != null)).ConfigureAwait(false);
        foreach (var file in files)
        {
            if (RegisterSourceFile(file) == id) return _knownFileLocations[id];
        }

        return null;
    }

    private async Task<ScriptDocumentItem?> LoadSourceFileAsync(string id)
    {
        var path = await FindSourceFilePathAsync(id);
        var language = path == null ? null : _languages.FindSourceFileLanguage(path);
        if (path == null || language == null) return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            var info = new FileInfo(path);
            lock (_sourceGate)
            {
                _sourceFiles[id] = new SourceFileState
                {
                    Path = path,
                    LastSavedText = text,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    HasByteOrderMark = hasBom
                };
            }

            return new ScriptDocumentItem
            {
                Id = id,
                Title = Path.GetFileName(path),
                Description = $"{language.DisplayName} file",
                Category = language.DisplayName,
                Code = text,
                Notes = string.Empty,
                LanguageId = language.Id,
                SourceFilePath = path,
                Created = info.CreationTimeUtc,
                LastModified = info.LastWriteTimeUtc
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to read '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes a source file's text. Ctrl+S and Run pass <paramref name="overwriteChangesOnDisk"/>; the saves the studio
    /// makes on its own (switching files, leaving the studio) don't, and then write only when the text changed and
    /// nothing else has changed the file since the studio read it, so another editor's work is never lost.
    /// </summary>
    public async Task<bool> SaveSourceFileAsync(ScriptDocumentItem document, bool overwriteChangesOnDisk)
    {
        var path = document.SourceFilePath ?? await FindSourceFilePathAsync(document.Id);
        if (path == null) return false;

        SourceFileState? state;
        lock (_sourceGate) _sourceFiles.TryGetValue(document.Id, out state);
        var text = document.Code ?? string.Empty;

        try
        {
            if (!overwriteChangesOnDisk && state != null)
            {
                if (text == state.LastSavedText) return true;
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) != state.LastWriteTimeUtc)
                {
                    Debug.WriteLine($"[CSharpEditorPlugin] Not saving '{path}': it changed on disk since it was opened.");
                    return false;
                }
            }

            var hasBom = state?.HasByteOrderMark == true;
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(hasBom));
            var written = File.GetLastWriteTimeUtc(path);
            lock (_sourceGate)
            {
                _sourceFiles[document.Id] = new SourceFileState
                {
                    Path = path,
                    LastSavedText = text,
                    LastWriteTimeUtc = written,
                    HasByteOrderMark = hasBom
                };
            }

            document.SourceFilePath = path;
            document.LastModified = written;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to save '{path}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Creates <c>script_HHmmss.py</c> (or <paramref name="fileName"/>) with the language's starter text.</summary>
    public async Task<ScriptDocumentItem?> CreateNewSourceFileAsync(string languageId, string? fileName = null, string? folderPath = null)
    {
        await EnsureInitializedAsync();
        var language = _languages.Get(languageId);
        if (language == null || language.Storage != LanguageStorageKind.SourceFile) return null;

        var root = EffectiveWorkspaceRoot;
        var folder = string.IsNullOrEmpty(folderPath) ? root : Path.Combine(root, folderPath);
        Directory.CreateDirectory(folder);

        var extension = language.DefaultExtension();
        var baseName = SanitizeName(
            string.IsNullOrWhiteSpace(fileName) ? $"script_{DateTime.Now:HHmmss}" : Path.GetFileNameWithoutExtension(fileName.Trim()),
            "script");
        // "_2" rather than " (2)": a Python module whose name has spaces or brackets can't be imported.
        var name = baseName;
        for (var n = 2; File.Exists(Path.Combine(folder, name + extension)); n++) name = $"{baseName}_{n}";

        var path = Path.Combine(folder, name + extension);
        await File.WriteAllTextAsync(path, language.NewFileTemplate, new UTF8Encoding(false));
        return await LoadSourceFileAsync(RegisterSourceFile(path));
    }

    /// <summary>
    /// Renames a source file on disk and returns its new id (ids follow the path). The file keeps its language: renaming
    /// <c>main.py</c> to <c>main</c> gives <c>main.py</c>.
    /// </summary>
    public Task<SourceFileRename> RenameSourceFileAsync(string id, string newFileName)
    {
        if (!_knownFileLocations.TryGetValue(id, out var path) || !File.Exists(path))
        {
            throw new FileNotFoundException("That file no longer exists.", path);
        }

        var language = _languages.FindSourceFileLanguage(path);
        var name = SanitizeName(Path.GetFileName(newFileName.Trim()), Path.GetFileName(path));
        if (language != null && !language.FileExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
        {
            name += Path.GetExtension(path);
        }

        var target = Path.Combine(Path.GetDirectoryName(path) ?? EffectiveWorkspaceRoot, name);
        if (string.Equals(path, target, StringComparison.Ordinal)) return Task.FromResult(new SourceFileRename(id, path));

        if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
        {
            // Case-only rename: case-insensitive file systems need a two-step move, or File.Move does nothing.
            var temporary = target + $".rename_{Guid.NewGuid():N}";
            File.Move(path, temporary);
            File.Move(temporary, target);
        }
        else
        {
            if (File.Exists(target)) throw new IOException($"A file named '{name}' already exists here.");
            File.Move(path, target);
        }

        _knownFileLocations.TryRemove(id, out _);
        SourceFileState? state;
        lock (_sourceGate) _sourceFiles.Remove(id, out state);

        var newId = RegisterSourceFile(target);
        if (state != null)
        {
            state.Path = target;
            state.LastWriteTimeUtc = LastWriteTimeUtc(target);
            lock (_sourceGate) _sourceFiles[newId] = state;
        }

        return Task.FromResult(new SourceFileRename(newId, _knownFileLocations[newId]));
    }

    private void DeleteSourceFile(string id)
    {
        if (_knownFileLocations.TryGetValue(id, out var path) && File.Exists(path)) File.Delete(path);
        _knownFileLocations.TryRemove(id, out _);
        lock (_sourceGate) _sourceFiles.Remove(id);
    }

    private static DateTime LastWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DateTime.UtcNow;
        }
    }
}
