using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

public partial class CSharpCodeStudioViewModel
{
    // ── VS Code Search in Script ──
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _replaceQuery = string.Empty;

    [ObservableProperty]
    private bool _searchMatchCase;

    [ObservableProperty]
    private bool _searchWholeWord;

    [ObservableProperty]
    private bool _searchUseRegex;

    [ObservableProperty]
    private string _searchStatusText = string.Empty;

    /// <summary>What the Search panel lists: matches of the open document, or (searching the workspace) a header row per file followed by its matches.</summary>
    public RangeObservableCollection<SearchResultItem> SearchMatches { get; } = new();

    /// <summary>True to search every file of the workspace ("Find in Files") instead of the open document.</summary>
    [ObservableProperty]
    private bool _searchAllFiles;

    /// <summary>True while a search of the workspace is still reading files.</summary>
    [ObservableProperty]
    private bool _isSearching;

    public string SearchPlaceholder => SearchAllFiles ? "Search in workspace..." : "Search in script...";

    /// <summary>What the list says while it has no rows.</summary>
    public string SearchEmptyText => string.IsNullOrEmpty(SearchQuery)
        ? "Type a search query above to inspect occurrences."
        : IsSearching ? string.Empty : SearchStatusText.StartsWith("Invalid", StringComparison.Ordinal) ? string.Empty : "No results found.";

    partial void OnSearchQueryChanged(string value) => ExecuteSearch();
    partial void OnSearchMatchCaseChanged(bool value) => ExecuteSearch();
    partial void OnSearchWholeWordChanged(bool value) => ExecuteSearch();
    partial void OnSearchUseRegexChanged(bool value) => ExecuteSearch();
    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(SearchEmptyText));

    partial void OnSearchAllFilesChanged(bool value)
    {
        OnPropertyChanged(nameof(SearchPlaceholder));
        ExecuteSearch();
    }

    partial void OnSearchStatusTextChanged(string value) => OnPropertyChanged(nameof(SearchEmptyText));

    // ── VS Code Code Templates ──
    [ObservableProperty]
    private string _templateFilterQuery = string.Empty;

    public ObservableCollection<CodeTemplate> FilteredTemplates { get; } = new();
    private readonly List<CodeTemplate> _allTemplates = new();

    partial void OnTemplateFilterQueryChanged(string value) => RefreshFilteredTemplates();

    [ObservableProperty]
    private bool _isWordWrap;

    [ObservableProperty]
    private bool _isSyntaxHighlightingEnabled = true;

    [ObservableProperty]
    private bool _isAutoCompletionEnabled = true;

    public event Action? RequestFoldAll;
    public event Action? RequestUnfoldAll;
    public event Action? RequestToggleSearch;

    [RelayCommand]
    public void FoldAll() => RequestFoldAll?.Invoke();

    [RelayCommand]
    public void UnfoldAll() => RequestUnfoldAll?.Invoke();

    [RelayCommand]
    public void ToggleSearch() => RequestToggleSearch?.Invoke();

    [RelayCommand]
    public void ToggleWordWrap() => IsWordWrap = !IsWordWrap;

    [RelayCommand]
    public void FormatCode()
    {
        if (!SupportsFormatting)
        {
            CompilerStatusText = $"Formatting isn't available for {ActiveLanguage.DisplayName} yet";
            return;
        }

        if (string.IsNullOrWhiteSpace(Code)) return;
        try
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Code);
            var root = tree.GetRoot();
            Code = Microsoft.CodeAnalysis.SyntaxNodeExtensions.NormalizeWhitespace(root).ToFullString();
        }
        catch
        {
        }
    }

    // The open document is searched on the spot up to this size (a few milliseconds); a bigger one, like the workspace, in the
    // background after a pause in typing.
    private const int SynchronousSearchLength = 200_000;
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(250);
    private CancellationTokenSource? _searchCts;

    [RelayCommand]
    public void ExecuteSearch()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        IsSearching = false;
        if (SearchMatches.Count > 0) SearchMatches.ReplaceAll(Array.Empty<SearchResultItem>());

        if (string.IsNullOrEmpty(SearchQuery))
        {
            SearchStatusText = string.Empty;
            return;
        }

        var matcher = new TextMatcher(SearchQuery, SearchMatchCase, SearchWholeWord, SearchUseRegex);
        if (matcher.Error != null)
        {
            SearchStatusText = "Invalid regular expression";
            return;
        }

        var cts = _searchCts = new CancellationTokenSource();
        if (SearchAllFiles)
        {
            _ = SearchWorkspaceAsync(matcher, cts.Token);
            return;
        }

        var code = Code ?? string.Empty;
        if (code.Length <= SynchronousSearchLength)
        {
            var rows = SearchDocument(code, matcher, CancellationToken.None);
            SearchMatches.ReplaceAll(rows);
            SearchStatusText = rows.Count == 1 ? "1 result" : $"{rows.Count} results";
            return;
        }

        _ = SearchDocumentInBackgroundAsync(code, matcher, cts.Token);
    }

    private static List<SearchResultItem> SearchDocument(string code, TextMatcher matcher, CancellationToken token)
    {
        var rows = new List<SearchResultItem>();
        matcher.Scan(code, (line, column, length, lineText) => rows.Add(new SearchResultItem
        {
            LineNumber = line,
            Column = column,
            Length = length,
            LineText = lineText.Trim().ToString()
        }), cancellationToken: token);
        return rows;
    }

    private async Task SearchDocumentInBackgroundAsync(string code, TextMatcher matcher, CancellationToken token)
    {
        try
        {
            IsSearching = true;
            SearchStatusText = "Searching...";
            await Task.Delay(SearchDebounce, token);
            var rows = await Task.Run(() => SearchDocument(code, matcher, token), token);
            if (token.IsCancellationRequested) return;

            SearchMatches.ReplaceAll(rows);
            SearchStatusText = rows.Count == 1 ? "1 result" : $"{rows.Count} results";
            IsSearching = false;
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one, which reports for itself.
        }
    }

    /// <summary>
    /// Find in Files: reads the workspace's files on background threads and adds their matches to the list as they come, a header
    /// row per file. Typing again cancels the search that is running and starts another after a pause.
    /// </summary>
    private async Task SearchWorkspaceAsync(TextMatcher matcher, CancellationToken token)
    {
        // What the editor holds for the open documents, taken now (on the UI thread): unsaved edits are searched too.
        var openDocuments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tab in OpenTabs) openDocuments[tab.Document.Id] = tab.Id == Script.Id ? Code ?? string.Empty : tab.Document.Code ?? string.Empty;

        try
        {
            IsSearching = true;
            SearchStatusText = "Searching...";
            await Task.Delay(SearchDebounce, token);

            // Reading the index starts its walk when it is out of date; a search right after opening a folder waits a moment for
            // the first files to be listed rather than report that there are none.
            var index = _storageService.FileIndex;
            for (int waited = 0; index.IsBuilding && index.Count == 0 && waited < 60; waited++) await Task.Delay(50, token);
            var wasBuilding = index.IsBuilding;
            var paths = index.Paths;
            var root = _storageService.ActiveWorkspaceRootPath;

            var pending = new List<SearchResultItem>();
            long lastFlush = Stopwatch.GetTimestamp();
            int filesWithMatches = 0;
            int matches = 0;

            void Flush(int filesSearched)
            {
                var batch = pending.ToArray();
                pending.Clear();
                int shownFiles = filesWithMatches;
                int shownMatches = matches;
                _postToUiThread(() =>
                {
                    if (token.IsCancellationRequested) return;
                    if (batch.Length > 0) SearchMatches.InsertRange(SearchMatches.Count, batch);
                    SearchStatusText = $"Searching... {shownMatches} in {shownFiles} {(shownFiles == 1 ? "file" : "files")} ({filesSearched:N0} of {paths.Count:N0} searched)";
                });
                lastFlush = Stopwatch.GetTimestamp();
            }

            var summary = await WorkspaceTextSearch.RunAsync(paths, root, matcher, openDocuments, file =>
            {
                var name = file.RelativePath[(file.RelativePath.LastIndexOf('/') + 1)..];
                var folder = file.RelativePath.Length > name.Length ? file.RelativePath[..^(name.Length + 1)] : string.Empty;
                pending.Add(new SearchResultItem { IsFileHeader = true, FilePath = file.RelativePath, FileName = name, LineText = folder, MatchCount = file.Hits.Count });
                foreach (var hit in file.Hits)
                {
                    pending.Add(new SearchResultItem
                    {
                        FilePath = file.RelativePath,
                        FileName = name,
                        LineNumber = hit.Line,
                        Column = hit.Column,
                        Length = hit.Length,
                        LineText = hit.Text,
                        Cell = hit.Cell
                    });
                }

                filesWithMatches++;
                matches += file.Hits.Count;
            }, token, onProgress: searched =>
            {
                // Rows reach the list about ten times a second, however fast the files are read.
                if (pending.Count > 0 && Stopwatch.GetElapsedTime(lastFlush) > TimeSpan.FromMilliseconds(100)) Flush(searched);
            });

            if (pending.Count > 0) Flush(summary.FilesSearched);
            var note = summary.HitLimit ? $" (stopped at {WorkspaceTextSearch.MaxMatches:N0}; narrow the search)" : string.Empty;
            if (wasBuilding || _storageService.FileIndex.IsBuilding) note += " - the file list was still being built";
            else if (_storageService.FileIndex.IsTruncated) note += " - the workspace has more files than are searched";

            var status = summary.Matches == 0
                ? $"No results in {summary.FilesSearched:N0} {(summary.FilesSearched == 1 ? "file" : "files")}{note}"
                : $"{summary.Matches:N0} {(summary.Matches == 1 ? "result" : "results")} in {summary.FilesWithMatches:N0} {(summary.FilesWithMatches == 1 ? "file" : "files")}{note}";
            _postToUiThread(() =>
            {
                if (token.IsCancellationRequested) return;
                SearchStatusText = status;
                IsSearching = false;
            });
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one, which reports for itself.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Find in Files failed: {ex.Message}");
            _postToUiThread(() =>
            {
                if (token.IsCancellationRequested) return;
                SearchStatusText = "The search could not be completed";
                IsSearching = false;
            });
        }
    }

    [RelayCommand]
    public void NavigateToSearchMatch(SearchResultItem? match)
    {
        if (match == null) return;
        if (match.FilePath != null)
        {
            _ = NavigateToWorkspaceMatchAsync(match);
            return;
        }

        CaretLine = match.LineNumber;
        CaretColumn = match.Column;
        RequestNavigateToCaret?.Invoke(match.LineNumber, match.Column);
    }

    // A match in another file: open it (a notebook opens its own page), then go to the line.
    private async Task NavigateToWorkspaceMatchAsync(SearchResultItem match)
    {
        try
        {
            var fullPath = Path.Combine(_storageService.ActiveWorkspaceRootPath, match.FilePath!.Replace('/', Path.DirectorySeparatorChar));
            await OpenWorkspaceFileAsync(fullPath);
            if (match.IsFileHeader || match.Cell > 0) return;

            CaretLine = match.LineNumber;
            CaretColumn = match.Column;
            RequestNavigateToCaret?.Invoke(match.LineNumber, match.Column);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Opening a search result failed: {ex.Message}");
            CompilerStatusText = $"Could not open {match.FileName}";
        }
    }

    [RelayCommand]
    public void ReplaceNext()
    {
        if (SearchAllFiles || string.IsNullOrEmpty(SearchQuery)) return;
        var matcher = new TextMatcher(SearchQuery, SearchMatchCase, SearchWholeWord, SearchUseRegex);
        var replaced = matcher.Replace(Code ?? string.Empty, ReplaceQuery ?? string.Empty, out var count, limit: 1);
        if (count == 0) return;

        Code = replaced;
        ExecuteSearch();
    }

    [RelayCommand]
    public void ReplaceAll()
    {
        if (SearchAllFiles || string.IsNullOrEmpty(SearchQuery)) return;
        var matcher = new TextMatcher(SearchQuery, SearchMatchCase, SearchWholeWord, SearchUseRegex);
        var replaced = matcher.Replace(Code ?? string.Empty, ReplaceQuery ?? string.Empty, out var count);
        if (count == 0) return;

        Code = replaced;
        ExecuteSearch();
    }

    public void RefreshFilteredTemplates()
    {
        FilteredTemplates.Clear();
        var q = TemplateFilterQuery?.Trim();
        foreach (var t in _allTemplates)
        {
            if (string.IsNullOrEmpty(q) ||
                t.Title.Contains((string)q, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains((string)q, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains((string)q, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTemplates.Add(t);
            }
        }
    }

    [RelayCommand]
    public void InsertTemplate(CodeTemplate? template)
    {
        // The templates are C#: pasting one into another language's file would only break it.
        if (template == null) return;
        if (!ActiveLanguage.Has(LanguageCapabilities.Templates)) return;
        // Notes that are all template (none typed yet) read better rendered.
        if (string.IsNullOrWhiteSpace(Notes) && !string.IsNullOrWhiteSpace(template.Notes)) IsNotesPreviewMode = true;
        if (string.IsNullOrWhiteSpace(Code))
        {
            Code = template.InitialCode;
            Notes = template.Notes;
        }
        else
        {
            Code += "\n\n" + template.InitialCode;
            if (!string.IsNullOrWhiteSpace(template.Notes))
            {
                Notes = string.IsNullOrWhiteSpace(Notes) ? template.Notes : Notes + "\n\n" + template.Notes;
            }
        }
    }
}
