using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpBlindProblemsViewModel
{
    [ObservableProperty]
    private int _totalSolved;

    [ObservableProperty]
    private int _totalProblems;

    [ObservableProperty]
    private int _totalEasySolved;

    [ObservableProperty]
    private int _totalEasy;

    [ObservableProperty]
    private int _totalMediumSolved;

    [ObservableProperty]
    private int _totalMedium;

    [ObservableProperty]
    private int _totalHardSolved;

    [ObservableProperty]
    private int _totalHard;

    [ObservableProperty]
    private double _progressPercentage;

    public string ProgressSummaryText => $"{TotalSolved} / {TotalProblems} Solved ({ProgressPercentage:F1}%)";
    public string EasySummaryText => $"{TotalEasySolved}/{TotalEasy}";
    public string MediumSummaryText => $"{TotalMediumSolved}/{TotalMedium}";
    public string HardSummaryText => $"{TotalHardSolved}/{TotalHard}";

    private void InitializeCategories()
    {
        Categories.Clear();
        var allCats = Blind75CatalogService.GetAllCategories();
        foreach (var cat in allCats)
        {
            int total = cat == "All" ? AllProblems.Count : AllProblems.Count(p => p.Category == cat);
            int solved = cat == "All" ? AllProblems.Count(p => p.IsSolved) : AllProblems.Count(p => p.Category == cat && p.IsSolved);

            Categories.Add(new BlindCategorySummary
            {
                Name = cat,
                IconKind = Blind75CatalogService.GetCategoryIcon(cat),
                TotalCount = total,
                SolvedCount = solved,
                IsSelected = string.Equals(cat, SelectedCategory, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private void UpdateCategorySelectionState()
    {
        foreach (var cat in Categories)
        {
            cat.IsSelected = string.Equals(cat.Name, SelectedCategory, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void RecalculateStats()
    {
        TotalProblems = AllProblems.Count;
        TotalSolved = AllProblems.Count(p => p.IsSolved);

        TotalEasy = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Easy);
        TotalEasySolved = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Easy && p.IsSolved);

        TotalMedium = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Medium);
        TotalMediumSolved = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Medium && p.IsSolved);

        TotalHard = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Hard);
        TotalHardSolved = AllProblems.Count(p => p.Difficulty == ProblemDifficulty.Hard && p.IsSolved);

        ProgressPercentage = TotalProblems > 0 ? (double)TotalSolved / TotalProblems * 100.0 : 0.0;

        OnPropertyChanged(nameof(ProgressSummaryText));
        OnPropertyChanged(nameof(EasySummaryText));
        OnPropertyChanged(nameof(MediumSummaryText));
        OnPropertyChanged(nameof(HardSummaryText));

        // Update category solved counts
        foreach (var cat in Categories)
        {
            cat.SolvedCount = cat.Name == "All"
                ? TotalSolved
                : AllProblems.Count(p => p.Category == cat.Name && p.IsSolved);
        }
    }
}
