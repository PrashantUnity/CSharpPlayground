using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>One line of a file that matched: where, how long, and what the line says (trimmed, and cut short when it is huge).</summary>
/// <param name="Cell">The 1-based cell of a notebook the line is in; 0 for anything that is not a notebook.</param>
public readonly record struct TextHit(int Line, int Column, int Length, string Text, int Cell);

/// <summary>Everything one file matched.</summary>
/// <param name="RelativePath">Path from the workspace root, always with <c>/</c> separators.</param>
public sealed record FileHits(string RelativePath, IReadOnlyList<TextHit> Hits);

/// <summary>How a search of the workspace went.</summary>
public sealed class WorkspaceSearchSummary
{
    public int FilesSearched { get; internal set; }
    public int FilesWithMatches { get; internal set; }
    public int Matches { get; internal set; }

    /// <summary>True when the search stopped early because it reached the limit of matches it lists.</summary>
    public bool HitLimit { get; internal set; }
}

/// <summary>
/// "Find in Files": looks for text in every file of the workspace, the way an IDE does. It reads the files on background
/// threads (several at once), never touches the UI thread, streams what it finds file by file in the order of the file list,
/// stops when cancelled or when it has enough matches, and skips what is not text (binary files, huge files). Documents that
/// are open with edits the disk does not have yet are searched as they are in the editor.
/// </summary>
public static class WorkspaceTextSearch
{
    /// <summary>A search lists at most this many matches; a query that finds more is too broad to read through.</summary>
    public const int MaxMatches = 2_000;

    /// <summary>One file lists at most this many matches, so a single generated file cannot fill the whole list.</summary>
    public const int MaxMatchesPerFile = 500;

    /// <summary>Files bigger than this are not read (a log, a data dump).</summary>
    public const long MaxFileBytes = 16L * 1024 * 1024;

    private const int Chunk = 32;
    private const int MaxSnippet = 240;

    /// <param name="relativePaths">The files to search, from the workspace index.</param>
    /// <param name="root">The workspace folder the paths are relative to.</param>
    /// <param name="matcher">What to look for.</param>
    /// <param name="openDocuments">The text of the open documents by document id, which is searched instead of the file's saved text.</param>
    /// <param name="onFile">Called on a background thread, one file at a time and in file-list order, for each file with matches.</param>
    /// <param name="onProgress">Called on a background thread with how many files have been searched so far.</param>
    /// <exception cref="OperationCanceledException">The search was cancelled.</exception>
    public static Task<WorkspaceSearchSummary> RunAsync(
        IReadOnlyList<string> relativePaths,
        string root,
        TextMatcher matcher,
        IReadOnlyDictionary<string, string>? openDocuments,
        Action<FileHits> onFile,
        CancellationToken cancellationToken,
        Action<int>? onProgress = null,
        int maxMatches = MaxMatches)
    {
        return Task.Run(() =>
        {
            var summary = new WorkspaceSearchSummary();
            if (!matcher.IsValid) return summary;

            var degree = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
            var options = new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken };
            var results = new FileHits?[Chunk];
            for (int start = 0; start < relativePaths.Count; start += Chunk)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(Chunk, relativePaths.Count - start);
                Array.Clear(results);
                Parallel.For(0, count, options, i =>
                {
                    results[i] = SearchFile(root, relativePaths[start + i], matcher, openDocuments, cancellationToken);
                });

                summary.FilesSearched += count;
                for (int i = 0; i < count; i++)
                {
                    if (results[i] is not { } file) continue;

                    var room = maxMatches - summary.Matches;
                    var hits = file.Hits.Count <= room ? file : file with { Hits = file.Hits.Take(room).ToList() };
                    summary.Matches += hits.Hits.Count;
                    summary.FilesWithMatches++;
                    onFile(hits);

                    if (summary.Matches >= maxMatches)
                    {
                        summary.HitLimit = true;
                        onProgress?.Invoke(summary.FilesSearched);
                        return summary;
                    }
                }

                onProgress?.Invoke(summary.FilesSearched);
            }

            return summary;
        }, cancellationToken);
    }

    private static FileHits? SearchFile(string root, string relativePath, TextMatcher matcher, IReadOnlyDictionary<string, string>? openDocuments, CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.Combine(root, relativePath);
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > MaxFileBytes) return null;

            var hits = new List<TextHit>();
            void Collect(string text, int cell)
            {
                var room = MaxMatchesPerFile - hits.Count;
                if (room <= 0) return;
                matcher.Scan(text, (line, column, length, lineText) => hits.Add(new TextHit(line, column, length, Snippet(lineText, column - 1), cell)), room, cancellationToken);
            }

            if (relativePath.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(fullPath));
                var json = document.RootElement;
                var code = json.TryGetProperty("Code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String ? codeElement.GetString() : null;
                if (openDocuments != null && json.TryGetProperty("Id", out var idElement) && idElement.GetString() is { } id && openDocuments.TryGetValue(id, out var open))
                {
                    code = open;
                }

                if (!string.IsNullOrEmpty(code)) Collect(code, cell: 0);
            }
            else if (relativePath.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(fullPath));
                if (document.RootElement.TryGetProperty("Cells", out var cells) && cells.ValueKind == JsonValueKind.Array)
                {
                    int cell = 0;
                    foreach (var element in cells.EnumerateArray())
                    {
                        cell++;
                        if (element.TryGetProperty("Source", out var source) && source.ValueKind == JsonValueKind.String && source.GetString() is { Length: > 0 } text)
                        {
                            Collect(text, cell);
                        }

                        if (hits.Count >= MaxMatchesPerFile) break;
                    }
                }
            }
            else
            {
                var bytes = File.ReadAllBytes(fullPath);
                // A NUL byte early in the file means it is not text.
                if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8192)) >= 0) return null;

                var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
                if (openDocuments is { Count: > 0 } && openDocuments.TryGetValue(LocalScriptStorageService.SourceFileId(fullPath), out var open))
                {
                    text = open;
                }

                Collect(text, cell: 0);
            }

            return hits.Count == 0 ? null : new FileHits(relativePath, hits);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // A file that cannot be read (locked, gone since the index was built, not the JSON it claims to be) has no matches.
            Debug.WriteLine($"[CSharpEditorPlugin] Find in Files skipped '{relativePath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>The line as the results list shows it: trimmed, and when it is enormous (a minified file) only the part around the match.</summary>
    private static string Snippet(ReadOnlySpan<char> line, int matchStart)
    {
        var trimmed = line.Trim();
        if (trimmed.Length <= MaxSnippet) return trimmed.ToString();

        int leading = line.Length - line.TrimStart().Length;
        int from = Math.Max(0, matchStart - leading - 60);
        int to = Math.Min(trimmed.Length, from + MaxSnippet);
        return (from > 0 ? "…" : string.Empty) + trimmed[from..to].ToString() + (to < trimmed.Length ? "…" : string.Empty);
    }
}
