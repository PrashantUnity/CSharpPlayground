using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpBlindProblemsViewModel
{
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDifficultyAll), nameof(IsDifficultyEasy), nameof(IsDifficultyMedium), nameof(IsDifficultyHard))]
    private string _selectedDifficulty = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusAll), nameof(IsStatusSolved), nameof(IsStatusUnsolved), nameof(IsStatusBookmarked))]
    private string _selectedStatusFilter = "All";

    [ObservableProperty]
    private bool _hasSearchQuery;

    /// <summary>True when the filters leave no problems, so the table can say so and offer to clear them.</summary>
    [ObservableProperty]
    private bool _hasNoResults;

    public bool IsDifficultyAll => ParseDifficulty(SelectedDifficulty) == null;
    public bool IsDifficultyEasy => ParseDifficulty(SelectedDifficulty) == ProblemDifficulty.Easy;
    public bool IsDifficultyMedium => ParseDifficulty(SelectedDifficulty) == ProblemDifficulty.Medium;
    public bool IsDifficultyHard => ParseDifficulty(SelectedDifficulty) == ProblemDifficulty.Hard;

    public bool IsStatusAll => !IsStatusSolved && !IsStatusUnsolved && !IsStatusBookmarked;
    public bool IsStatusSolved => string.Equals(SelectedStatusFilter, "Solved", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusUnsolved => string.Equals(SelectedStatusFilter, "Unsolved", StringComparison.OrdinalIgnoreCase);
    public bool IsStatusBookmarked => string.Equals(SelectedStatusFilter, "Bookmarked", StringComparison.OrdinalIgnoreCase);

    /// <summary>The column the table is sorted by: "Title" (by number), "Category", "Acceptance" or "Difficulty".</summary>
    public string SortColumn { get; private set; } = "Title";
    public bool IsSortDescending { get; private set; }
    public MaterialIconKind SortIcon => IsSortDescending ? MaterialIconKind.ArrowDown : MaterialIconKind.ArrowUp;
    public bool IsSortedByTitle => SortColumn == "Title";
    public bool IsSortedByCategory => SortColumn == "Category";
    public bool IsSortedByAcceptance => SortColumn == "Acceptance";
    public bool IsSortedByDifficulty => SortColumn == "Difficulty";

    partial void OnSearchQueryChanged(string value)
    {
        HasSearchQuery = !string.IsNullOrWhiteSpace(value);
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        UpdateCategorySelectionState();
        ApplyFilters();
    }

    partial void OnSelectedDifficultyChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        ApplyFilters();
    }

    [RelayCommand]
    public void SetCategory(string category)
    {
        SelectedCategory = category;
    }

    [RelayCommand]
    public void SetDifficulty(string difficulty)
    {
        SelectedDifficulty = difficulty;
    }

    [RelayCommand]
    public void SetStatusFilter(string status)
    {
        SelectedStatusFilter = status;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    public void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectedCategory = "All";
        SelectedDifficulty = "All";
        SelectedStatusFilter = "All";
    }

    /// <summary>Sorts by <paramref name="column"/>; sorting by the current column again flips the direction.</summary>
    [RelayCommand]
    public void SortBy(string column)
    {
        IsSortDescending = SortColumn == column && !IsSortDescending;
        SortColumn = column;
        OnPropertyChanged(nameof(SortColumn));
        OnPropertyChanged(nameof(IsSortDescending));
        OnPropertyChanged(nameof(SortIcon));
        OnPropertyChanged(nameof(IsSortedByTitle));
        OnPropertyChanged(nameof(IsSortedByCategory));
        OnPropertyChanged(nameof(IsSortedByAcceptance));
        OnPropertyChanged(nameof(IsSortedByDifficulty));
        ApplyFilters();
    }

    public void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = AllProblems.AsEnumerable();

        // 1. Search Query (Number, Title, Tags)
        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(p =>
                p.Number.ToString().Equals(query, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        // 2. Category
        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(p => string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Difficulty
        if (ParseDifficulty(SelectedDifficulty) is { } targetDiff)
        {
            filtered = filtered.Where(p => p.Difficulty == targetDiff);
        }

        // 4. Status Filter
        if (IsStatusSolved)
        {
            filtered = filtered.Where(p => p.IsSolved);
        }
        else if (IsStatusUnsolved)
        {
            filtered = filtered.Where(p => !p.IsSolved);
        }
        else if (IsStatusBookmarked)
        {
            filtered = filtered.Where(p => p.IsBookmarked);
        }

        FilteredProblems.Clear();
        foreach (var p in Sort(filtered))
        {
            FilteredProblems.Add(p);
        }
        HasNoResults = FilteredProblems.Count == 0;

        if (SelectedProblem == null || !FilteredProblems.Contains(SelectedProblem))
        {
            SelectedProblem = FilteredProblems.FirstOrDefault();
        }
    }

    // Ties (same category, difficulty or acceptance) stay in problem-number order.
    private IOrderedEnumerable<BlindProblemItem> Sort(IEnumerable<BlindProblemItem> problems)
    {
        Func<BlindProblemItem, IComparable> key = SortColumn switch
        {
            "Category" => p => p.Category,
            "Acceptance" => p => p.AcceptanceRate,
            "Difficulty" => p => p.Difficulty,
            _ => p => p.Number
        };
        var sorted = IsSortDescending ? problems.OrderByDescending(key) : problems.OrderBy(key);
        return sorted.ThenBy(p => p.Number);
    }

    private static ProblemDifficulty? ParseDifficulty(string? difficulty) => difficulty?.Trim().ToLowerInvariant() switch
    {
        "easy" => ProblemDifficulty.Easy,
        "med" or "medium" => ProblemDifficulty.Medium,
        "hard" => ProblemDifficulty.Hard,
        _ => null
    };
}
