using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string _selectedDifficulty = "All";

    [ObservableProperty]
    private string _selectedStatusFilter = "All";

    [ObservableProperty]
    private bool _hasSearchQuery;

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
        if (!string.Equals(SelectedDifficulty, "All", StringComparison.OrdinalIgnoreCase))
        {
            var targetDiff = SelectedDifficulty.ToLowerInvariant() switch
            {
                "easy" => ProblemDifficulty.Easy,
                "med" or "medium" => ProblemDifficulty.Medium,
                "hard" => ProblemDifficulty.Hard,
                _ => (ProblemDifficulty?)null
            };

            if (targetDiff.HasValue)
            {
                filtered = filtered.Where(p => p.Difficulty == targetDiff.Value);
            }
        }

        // 4. Status Filter
        if (string.Equals(SelectedStatusFilter, "Solved", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(p => p.IsSolved);
        }
        else if (string.Equals(SelectedStatusFilter, "Unsolved", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(p => !p.IsSolved);
        }
        else if (string.Equals(SelectedStatusFilter, "Bookmarked", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(p => p.IsBookmarked);
        }

        FilteredProblems.Clear();
        foreach (var p in filtered)
        {
            FilteredProblems.Add(p);
        }

        if (SelectedProblem == null || !FilteredProblems.Contains(SelectedProblem))
        {
            SelectedProblem = FilteredProblems.FirstOrDefault();
        }
    }
}
