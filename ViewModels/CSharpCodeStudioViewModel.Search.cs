using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

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

    public ObservableCollection<SearchResultItem> SearchMatches { get; } = new();

    partial void OnSearchQueryChanged(string value) => ExecuteSearch();
    partial void OnSearchMatchCaseChanged(bool value) => ExecuteSearch();
    partial void OnSearchWholeWordChanged(bool value) => ExecuteSearch();
    partial void OnSearchUseRegexChanged(bool value) => ExecuteSearch();

    // ── VS Code Code Templates ──
    [ObservableProperty]
    private string _templateFilterQuery = string.Empty;

    public ObservableCollection<CodeTemplate> FilteredTemplates { get; } = new();
    private readonly List<CodeTemplate> _allTemplates = new();

    partial void OnTemplateFilterQueryChanged(string value) => RefreshFilteredTemplates();

    [ObservableProperty]
    private bool _isWordWrap;

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

    [RelayCommand]
    public void ExecuteSearch()
    {
        SearchMatches.Clear();
        if (string.IsNullOrEmpty(SearchQuery))
        {
            SearchStatusText = string.Empty;
            return;
        }

        var lines = (Code ?? string.Empty).Split('\n');
        var query = SearchQuery;
        var comparison = SearchMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            int idx = 0;
            while (idx < line.Length)
            {
                int foundIdx = line.IndexOf(query, idx, comparison);
                if (foundIdx < 0) break;

                SearchMatches.Add(new SearchResultItem
                {
                    LineNumber = i + 1,
                    Column = foundIdx + 1,
                    Length = query.Length,
                    LineText = line.Trim()
                });

                idx = foundIdx + Math.Max(1, query.Length);
            }
        }

        SearchStatusText = SearchMatches.Count == 1 ? "1 result" : $"{SearchMatches.Count} results";
    }

    [RelayCommand]
    public void NavigateToSearchMatch(SearchResultItem? match)
    {
        if (match == null) return;
        CaretLine = match.LineNumber;
        CaretColumn = match.Column;
        RequestNavigateToCaret?.Invoke(match.LineNumber, match.Column);
    }

    [RelayCommand]
    public void ReplaceNext()
    {
        if (string.IsNullOrEmpty(SearchQuery) || SearchMatches.Count == 0) return;
        var match = SearchMatches[0];
        var lines = (Code ?? string.Empty).Split('\n');
        if (match.LineNumber - 1 < lines.Length)
        {
            var line = lines[match.LineNumber - 1];
            var colIdx = match.Column - 1;
            if (colIdx >= 0 && colIdx + match.Length <= line.Length)
            {
                lines[match.LineNumber - 1] = line.Remove(colIdx, match.Length).Insert(colIdx, ReplaceQuery ?? string.Empty);
                Code = string.Join("\n", lines);
                ExecuteSearch();
            }
        }
    }

    [RelayCommand]
    public void ReplaceAll()
    {
        if (string.IsNullOrEmpty(SearchQuery)) return;
        if (SearchMatchCase)
        {
            Code = (Code ?? string.Empty).Replace(SearchQuery, ReplaceQuery ?? string.Empty);
        }
        else
        {
            Code = System.Text.RegularExpressions.Regex.Replace(
                Code ?? string.Empty,
                System.Text.RegularExpressions.Regex.Escape(SearchQuery),
                ReplaceQuery ?? string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        ExecuteSearch();
    }

    public void RefreshFilteredTemplates()
    {
        FilteredTemplates.Clear();
        var q = TemplateFilterQuery?.Trim();
        foreach (var t in _allTemplates)
        {
            if (string.IsNullOrEmpty(q) ||
                t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTemplates.Add(t);
            }
        }
    }

    [RelayCommand]
    public void InsertTemplate(CodeTemplate? template)
    {
        if (template == null) return;
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
