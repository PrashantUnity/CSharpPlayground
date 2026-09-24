using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpBlindProblemsViewModel : ObservableObject
{
    private readonly IBlindProgressService _progressService;
    private readonly Action? _backToHubAction;
    private readonly Action<ScriptDocumentItem>? _openScriptAction;
    private readonly Action<NotebookDocumentItem>? _openNotebookAction;

    [ObservableProperty]
    private BlindProblemItem? _selectedProblem;

    [ObservableProperty]
    private bool _isDetailFlyoutOpen;

    [ObservableProperty]
    private bool _isLoading = true;

    public ObservableCollection<BlindProblemItem> AllProblems { get; } = new();
    public ObservableCollection<BlindProblemItem> FilteredProblems { get; } = new();
    public ObservableCollection<BlindCategorySummary> Categories { get; } = new();

    public CSharpBlindProblemsViewModel(
        IBlindProgressService? progressService = null,
        Action? backToHubAction = null,
        Action<ScriptDocumentItem>? openScriptAction = null,
        Action<NotebookDocumentItem>? openNotebookAction = null)
    {
        _progressService = progressService ?? new LocalBlindProgressService();
        _backToHubAction = backToHubAction;
        _openScriptAction = openScriptAction;
        _openNotebookAction = openNotebookAction;

        _progressService.SolvedStatusChanged += OnExternalSolvedStatusChanged;
        _progressService.BookmarkStatusChanged += OnExternalBookmarkStatusChanged;

        PopulateCatalogSynchronously();
        _ = LoadCatalogAndProgressAsync();
    }

    private void PopulateCatalogSynchronously()
    {
        var problems = Blind75CatalogService.GetAllProblems();
        AllProblems.Clear();
        foreach (var p in problems)
        {
            p.IsSolved = _progressService.IsProblemSolved(p.Number);
            p.IsBookmarked = _progressService.IsProblemBookmarked(p.Number);
            AllProblems.Add(p);
        }

        if (AllProblems.Count > 0 && SelectedProblem == null)
        {
            SelectedProblem = AllProblems[0];
        }

        InitializeCategories();
        RecalculateStats();
        ApplyFilters();
        IsLoading = false;
    }

    public async Task LoadCatalogAndProgressAsync()
    {
        try
        {
            var solvedSet = await _progressService.GetSolvedProblemNumbersAsync();
            var bookmarkedSet = await _progressService.GetBookmarkedProblemNumbersAsync();

            foreach (var p in AllProblems)
            {
                p.IsSolved = solvedSet.Contains(p.Number);
                p.IsBookmarked = bookmarkedSet.Contains(p.Number);
            }

            RecalculateStats();
            ApplyFilters();
            IsLoading = false;
        }
        catch
        {
            IsLoading = false;
        }
    }

    private void OnExternalSolvedStatusChanged(int number, bool isSolved)
    {
        var p = AllProblems.FirstOrDefault(x => x.Number == number);
        if (p != null) p.IsSolved = isSolved;
        RecalculateStats();
    }

    private void OnExternalBookmarkStatusChanged(int number, bool isBookmarked)
    {
        var p = AllProblems.FirstOrDefault(x => x.Number == number);
        if (p != null) p.IsBookmarked = isBookmarked;
    }

    [RelayCommand]
    public void BackToHub() => _backToHubAction?.Invoke();

    [RelayCommand]
    public async Task ToggleSolvedAsync(BlindProblemItem? problem)
    {
        if (problem == null) return;
        problem.IsSolved = !problem.IsSolved;
        await _progressService.SetProblemSolvedAsync(problem.Number, problem.IsSolved);
        RecalculateStats();
        if (SelectedStatusFilter == "Solved" || SelectedStatusFilter == "Unsolved")
        {
            ApplyFilters();
        }
    }

    [RelayCommand]
    public async Task ToggleBookmarkAsync(BlindProblemItem? problem)
    {
        if (problem == null) return;
        problem.IsBookmarked = !problem.IsBookmarked;
        await _progressService.SetProblemBookmarkedAsync(problem.Number, problem.IsBookmarked);
        if (SelectedStatusFilter == "Bookmarked")
        {
            ApplyFilters();
        }
    }

    [RelayCommand]
    public void OpenScriptStudio(BlindProblemItem? problem)
    {
        var target = problem ?? SelectedProblem;
        if (target == null) return;
        var script = Blind75CatalogService.ConvertToScript(target);
        _openScriptAction?.Invoke(script);
    }

    [RelayCommand]
    public void OpenNotebookStudio(BlindProblemItem? problem)
    {
        var target = problem ?? SelectedProblem;
        if (target == null) return;
        var notebook = Blind75CatalogService.ConvertToNotebook(target);
        _openNotebookAction?.Invoke(notebook);
    }

    [RelayCommand]
    public void SelectProblem(BlindProblemItem? problem)
    {
        if (problem == null) return;
        SelectedProblem = problem;
    }

    [RelayCommand]
    public void OpenDetailFlyout(BlindProblemItem? problem)
    {
        if (problem != null) SelectedProblem = problem;
        IsDetailFlyoutOpen = true;
    }

    [RelayCommand]
    public void CloseDetailFlyout() => IsDetailFlyoutOpen = false;

    [RelayCommand]
    public void PickRandomUnsolvedProblem()
    {
        var candidates = AllProblems.Where(p => !p.IsSolved).ToList();
        if (candidates.Count == 0) candidates = AllProblems.ToList();
        if (candidates.Count == 0) return;

        var random = new Random();
        var chosen = candidates[random.Next(candidates.Count)];
        SelectedProblem = chosen;
        OpenScriptStudio(chosen);
    }

    [RelayCommand]
    public void GenerateTestData(BlindProblemItem? problem)
    {
        var target = problem ?? SelectedProblem;
        if (target == null) return;
        var freshCases = BlindTestDataGeneratorService.GenerateTestCases(target);
        target.TestCases.Clear();
        foreach (var tc in freshCases)
        {
            target.TestCases.Add(tc);
        }
    }
}
