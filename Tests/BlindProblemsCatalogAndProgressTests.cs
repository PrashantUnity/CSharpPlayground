using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class BlindProblemsCatalogAndProgressTests
{
    [Fact]
    public void Blind75CatalogService_GetAllProblems_ShouldContainCurriculumProblems()
    {
        var problems = Blind75CatalogService.GetAllProblems();
        Assert.NotNull(problems);
        Assert.True(problems.Count >= 75, $"Expected at least 75 problems, found {problems.Count}");
    }

    [Fact]
    public void Blind75CatalogService_AllProblems_ShouldHaveUniqueNumbersAndValidMetadata()
    {
        var problems = Blind75CatalogService.GetAllProblems();

        var numbers = problems.Select(p => p.Number).ToList();
        var duplicateNumbers = numbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicateNumbers);

        foreach (var p in problems)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Id), $"Problem {p.Number} has empty Id");
            Assert.False(string.IsNullOrWhiteSpace(p.Title), $"Problem {p.Number} has empty Title");
            Assert.False(string.IsNullOrWhiteSpace(p.Category), $"Problem {p.Number} has empty Category");
            Assert.InRange(p.AcceptanceRate, 1.0, 100.0);
            Assert.False(string.IsNullOrWhiteSpace(p.SolutionCode), $"Problem {p.Number} has empty SolutionCode");
            Assert.NotEmpty(p.TestCases);
        }
    }

    [Fact]
    public void Blind75CatalogService_GetProblemByNumber_ShouldFindKnownProblems()
    {
        var twoSum = Blind75CatalogService.GetProblemByNumber(1);
        Assert.NotNull(twoSum);
        Assert.Equal("Two Sum", twoSum.Title);
        Assert.Equal(ProblemDifficulty.Easy, twoSum.Difficulty);
        Assert.Equal("Arrays & Hashing", twoSum.Category);

        var invertTree = Blind75CatalogService.GetProblemByNumber(226);
        Assert.NotNull(invertTree);
        Assert.Equal("Invert Binary Tree", invertTree.Title);
        Assert.True(invertTree.HasVisualizer);
        Assert.Equal("Tree", invertTree.VisualizerKind);

        var reverseList = Blind75CatalogService.GetProblemByNumber(206);
        Assert.NotNull(reverseList);
        Assert.Equal("Reverse Linked List", reverseList.Title);
        Assert.True(reverseList.HasVisualizer);
    }

    [Fact]
    public void Blind75CatalogService_ConvertToScript_ShouldProduceRunnableScript()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1)!;
        var script = Blind75CatalogService.ConvertToScript(problem);

        Assert.NotNull(script);
        Assert.Contains("1. Two Sum", script.Title);
        Assert.Contains("Solution", script.Code);
        Assert.NotEmpty(script.TestCases);
    }

    [Fact]
    public void Blind75CatalogService_ConvertToNotebook_ShouldProduceInteractiveNotebook()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1)!;
        var notebook = Blind75CatalogService.ConvertToNotebook(problem);

        Assert.NotNull(notebook);
        Assert.Contains("1. Two Sum (Notebook)", notebook.Title);
        Assert.True(notebook.Cells.Count >= 3);
        Assert.Equal(CellType.Markdown, notebook.Cells[0].Type);
        Assert.Contains(notebook.Cells, c => c.Type == CellType.Code);
    }

    [Fact]
    public async Task LocalBlindProgressService_SetSolvedAndBookmark_ShouldPersistState()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new LocalBlindProgressService(tempDir);

            bool eventFired = false;
            service.SolvedStatusChanged += (num, solved) =>
            {
                if (num == 1 && solved) eventFired = true;
            };

            await service.SetProblemSolvedAsync(1, true);
            await service.SetProblemBookmarkedAsync(1, true);

            Assert.True(eventFired);
            Assert.True(service.IsProblemSolved(1));
            Assert.True(service.IsProblemBookmarked(1));

            // Reload from new service instance pointing to same directory
            var reloadedService = new LocalBlindProgressService(tempDir);
            var solved = await reloadedService.GetSolvedProblemNumbersAsync();
            var bookmarked = await reloadedService.GetBookmarkedProblemNumbersAsync();

            Assert.Contains(1, solved);
            Assert.Contains(1, bookmarked);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // Regression: every LocalBlindProgressService rewrote the whole file from what it had in memory, so two instances
    // on one file (the Blind 75 page's and Code Studio's) undid each other's changes.
    [Fact]
    public async Task LocalBlindProgressService_TwoInstancesOnOneFile_KeepEachOthersChanges()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var page = new LocalBlindProgressService(tempDir);
            await page.SetProblemSolvedAsync(1, true);
            await page.SetProblemSolvedAsync(2, true);

            var studio = new LocalBlindProgressService(tempDir);
            await studio.SetProblemSolvedAsync(3, true);   // reads {1, 2}, adds 3
            await studio.SetProblemSolvedAsync(2, false);  // and takes 2 back
            await page.SetProblemSolvedAsync(4, true);     // the page still holds {1, 2}
            await page.SetProblemBookmarkedAsync(11, true);

            var reloaded = new LocalBlindProgressService(tempDir);
            Assert.Equal(new[] { 1, 3, 4 }, (await reloaded.GetSolvedProblemNumbersAsync()).Order());
            Assert.Equal(new[] { 11 }, await reloaded.GetBookmarkedProblemNumbersAsync());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CSharpBlindProblemsViewModel_SearchAndFiltering_ShouldFilterCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var progressService = new LocalBlindProgressService(tempDir);
            var vm = new CSharpBlindProblemsViewModel(progressService);
            await vm.LoadCatalogAndProgressAsync();

            // 1. Initial State: All problems loaded
            Assert.NotEmpty(vm.FilteredProblems);
            Assert.Equal(vm.AllProblems.Count, vm.FilteredProblems.Count);

            // 2. Search by number "1"
            vm.SearchQuery = "1";
            Assert.Contains(vm.FilteredProblems, p => p.Number == 1);

            // 3. Search by name "Palindrome"
            vm.SearchQuery = "Palindrome";
            Assert.All(vm.FilteredProblems, p => Assert.Contains("Palindrome", p.Title, StringComparison.OrdinalIgnoreCase));

            // 4. Clear Search
            vm.ClearSearch();
            Assert.Equal(vm.AllProblems.Count, vm.FilteredProblems.Count);

            // 5. Category Filter: "Trees"
            vm.SetCategory("Trees");
            Assert.All(vm.FilteredProblems, p => Assert.Equal("Trees", p.Category));

            // 6. Difficulty Filter: "Hard"
            vm.SetCategory("All");
            vm.SetDifficulty("Hard");
            Assert.All(vm.FilteredProblems, p => Assert.Equal(ProblemDifficulty.Hard, p.Difficulty));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CSharpBlindProblemsViewModel_ToggleSolved_ShouldUpdateStats()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FrySharpTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var progressService = new LocalBlindProgressService(tempDir);
            var vm = new CSharpBlindProblemsViewModel(progressService);
            await vm.LoadCatalogAndProgressAsync();

            int initialSolved = vm.TotalSolved;
            var problem1 = vm.AllProblems.First(p => p.Number == 1);

            await vm.ToggleSolvedAsync(problem1);
            Assert.True(problem1.IsSolved);
            Assert.Equal(initialSolved + 1, vm.TotalSolved);
            Assert.True(vm.ProgressPercentage > 0.0);

            // Untoggle
            await vm.ToggleSolvedAsync(problem1);
            Assert.False(problem1.IsSolved);
            Assert.Equal(initialSolved, vm.TotalSolved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CSharpStudioHostViewModel_NavigateToBlindProblems_ShouldSetCurrentPage()
    {
        var hostVm = new CSharpStudioHostViewModel(blindProgress: new InMemoryProgressService());

        Assert.NotNull(hostVm.BlindProblemsViewModel);
        Assert.Equal(hostVm.ManagerViewModel, hostVm.CurrentPage);

        hostVm.NavigateToBlindProblems(226);
        Assert.Equal(hostVm.BlindProblemsViewModel, hostVm.CurrentPage);
        Assert.False(hostVm.IsOnManagerPage);
        Assert.Equal("Blind 75", hostVm.ActiveDocumentTitle);
        Assert.Equal(226, hostVm.BlindProblemsViewModel.SelectedProblem?.Number);
        // The view model's own row, the one its table highlights, not the shared catalog's copy.
        Assert.Contains(hostVm.BlindProblemsViewModel.SelectedProblem, hostVm.BlindProblemsViewModel.AllProblems);
    }

    [Fact]
    public void Blind75CatalogService_AllProblems_ShouldHaveVisualizersAndValidKinds()
    {
        var problems = Blind75CatalogService.GetAllProblems();
        Assert.NotEmpty(problems);

        foreach (var p in problems)
        {
            Assert.True(p.HasVisualizer, $"Problem {p.Number} ({p.Title}) should have HasVisualizer = true");
            Assert.False(string.IsNullOrWhiteSpace(p.VisualizerKind), $"Problem {p.Number} has empty VisualizerKind");
            Assert.False(string.IsNullOrWhiteSpace(p.VisualizationCode), $"Problem {p.Number} has empty VisualizationCode");
        }
    }

    [Fact]
    public void BlindTestDataGeneratorService_GenerateTestCases_ShouldProduceValidEdgeAndStressCases()
    {
        var twoSum = Blind75CatalogService.GetProblemByNumber(1)!;
        var cases = BlindTestDataGeneratorService.GenerateTestCases(twoSum, 3);

        Assert.NotNull(cases);
        Assert.True(cases.Count >= 3);
        foreach (var tc in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(tc.Name));
            Assert.False(string.IsNullOrWhiteSpace(tc.Input));
            Assert.False(string.IsNullOrWhiteSpace(tc.ExpectedOutput));
        }
    }

    [Fact]
    public void Blind75Catalog_KeyProblems_ShouldHaveMultiParadigmApproaches()
    {
        // 1. Two Sum
        var twoSum = Blind75CatalogService.GetProblemByNumber(1)!;
        Assert.False(string.IsNullOrWhiteSpace(twoSum.ThinkingProcessMarkdown));
        Assert.True(twoSum.HasNaiveSolution);
        Assert.NotNull(twoSum.OptimalApproach);
        Assert.NotNull(twoSum.NaiveApproach);

        // 2. Coin Change (has Naive, Greedy showing failure, and DP)
        var coinChange = Blind75CatalogService.GetProblemByNumber(322)!;
        Assert.True(coinChange.HasNaiveSolution);
        Assert.True(coinChange.HasGreedySolution);
        Assert.True(coinChange.HasDpSolution);
        Assert.NotNull(coinChange.DpApproach?.RecurrenceRelation);

        // 3. Jump Game (has Naive, DP, and Greedy optimal)
        var jumpGame = Blind75CatalogService.GetProblemByNumber(55)!;
        Assert.True(jumpGame.HasNaiveSolution);
        Assert.True(jumpGame.HasDpSolution);
        Assert.True(jumpGame.HasGreedySolution);

        // 4. Maximum Subarray (Kadane)
        var maxSub = Blind75CatalogService.GetProblemByNumber(53)!;
        Assert.True(maxSub.HasNaiveSolution);
        Assert.NotNull(maxSub.OptimalApproach);
    }

    [Fact]
    public void Blind75CatalogService_ConvertToNotebook_ShouldIncludeHowToThinkAndVisualizerCells()
    {
        var coinChange = Blind75CatalogService.GetProblemByNumber(322)!;
        var notebook = Blind75CatalogService.ConvertToNotebook(coinChange);

        Assert.NotNull(notebook);
        Assert.True(notebook.Cells.Count >= 6, $"Expected >= 6 cells, got {notebook.Cells.Count}");

        // Cell 0 is header markdown
        Assert.Equal(CellType.Markdown, notebook.Cells[0].Type);
        Assert.Contains("322. Coin Change", notebook.Cells[0].Source);

        // Contains How to Think cell
        Assert.Contains(notebook.Cells, c => c.Source.Contains("How to think", StringComparison.OrdinalIgnoreCase));

        // Contains Interactive Visualizer cell
        Assert.Contains(notebook.Cells, c => c.Source.Contains("Interactive Time-Travel Visualizer") || c.Source.Contains("Display."));
    }

    [Fact]
    public void CSharpBlindProblemsViewModel_GenerateTestData_ShouldUpdateTestCases()
    {
        var vm = new CSharpBlindProblemsViewModel(new InMemoryProgressService());
        var problem = vm.AllProblems.First(p => p.Number == 1);
        int initialCount = problem.TestCases.Count;

        vm.GenerateTestData(problem);
        Assert.NotEmpty(problem.TestCases);
        Assert.True(problem.TestCases.Count >= 3);
    }

    // Regression: the host gave the Blind 75 page and Code Studio a progress store each, on the same file, so a problem
    // Code Studio marked solved (all its cases passing) didn't show on the page, and the two overwrote each other.
    [Fact]
    public async Task CSharpStudioHostViewModel_GivesBothPagesOneProgressStore()
    {
        var progress = new InMemoryProgressService();
        var host = new CSharpStudioHostViewModel(blindProgress: progress);

        // Code Studio is built off the UI thread, after the Roslyn engine warms up.
        for (var waited = 0; host.CodeStudioViewModel == null && waited < 60_000; waited += 50) await Task.Delay(50);
        Assert.NotNull(host.CodeStudioViewModel);
        Assert.Same(progress, host.CodeStudioViewModel.BlindProgress);

        await progress.SetProblemSolvedAsync(3, true); // what Code Studio does when every case passes
        Assert.True(host.BlindProblemsViewModel.AllProblems.First(p => p.Number == 3).IsSolved);
        Assert.Equal(1, host.BlindProblemsViewModel.TotalSolved);
    }

    // Regression: every view model used to put the catalog's shared BlindProblemItem objects in AllProblems and write
    // its own progress into them, so a view model loading one progress store (e.g. a test's CSharpStudioHostViewModel
    // reading the real one) changed what another showed mid-test.
    [Fact]
    public async Task CSharpBlindProblemsViewModel_EachViewModel_KeepsItsOwnProblemState()
    {
        var fresh = new CSharpBlindProblemsViewModel(new InMemoryProgressService());
        var solvedTwoSum = new CSharpBlindProblemsViewModel(new InMemoryProgressService(solved: 1));
        await fresh.LoadCatalogAndProgressAsync();
        await solvedTwoSum.LoadCatalogAndProgressAsync();

        var inFresh = fresh.AllProblems.First(p => p.Number == 1);
        var inSolved = solvedTwoSum.AllProblems.First(p => p.Number == 1);

        Assert.NotSame(inFresh, inSolved);
        Assert.False(inFresh.IsSolved);
        Assert.True(inSolved.IsSolved);
        Assert.Equal(0, fresh.TotalSolved);
        Assert.Equal(1, solvedTwoSum.TotalSolved);
    }

    // Regression: a click that lands while the progress is still being read used to be undone when the read finished.
    [Fact]
    public async Task CSharpBlindProblemsViewModel_ToggleWhileProgressIsLoading_KeepsTheToggle()
    {
        var progress = new InMemoryProgressService { HoldLoads = true };
        var vm = new CSharpBlindProblemsViewModel(progress);
        var initialLoad = vm.LoadCatalogAndProgressAsync(); // the constructor's load, still waiting for its answer
        var problem1 = vm.AllProblems.First(p => p.Number == 1);

        var toggle = vm.ToggleSolvedAsync(problem1);
        progress.ReleaseLoads();
        await initialLoad;
        await toggle;

        Assert.True(problem1.IsSolved);
        Assert.Equal(1, vm.TotalSolved);
        Assert.True(progress.IsProblemSolved(1));
    }

    /// <summary>
    /// Progress kept in memory. With <see cref="HoldLoads"/> a load reads the progress as soon as it's asked, as a file
    /// read would, but answers only on <see cref="ReleaseLoads"/>, so a test can act while a load is in flight.
    /// </summary>
    private sealed class InMemoryProgressService(params int[] solved) : IBlindProgressService
    {
        private readonly HashSet<int> _solved = new(solved);
        private readonly HashSet<int> _bookmarked = new();
        private readonly TaskCompletionSource _loadsReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HoldLoads { get; init; }

        public event Action<int, bool>? SolvedStatusChanged;
        public event Action<int, bool>? BookmarkStatusChanged;

        public void ReleaseLoads() => _loadsReleased.TrySetResult();

        public async Task<IReadOnlySet<int>> GetSolvedProblemNumbersAsync()
        {
            var snapshot = new HashSet<int>(_solved);
            if (HoldLoads) await _loadsReleased.Task;
            return snapshot;
        }

        public Task<IReadOnlySet<int>> GetBookmarkedProblemNumbersAsync() =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>(_bookmarked));

        public Task SetProblemSolvedAsync(int problemNumber, bool isSolved)
        {
            if (isSolved ? _solved.Add(problemNumber) : _solved.Remove(problemNumber)) SolvedStatusChanged?.Invoke(problemNumber, isSolved);
            return Task.CompletedTask;
        }

        public Task SetProblemBookmarkedAsync(int problemNumber, bool isBookmarked)
        {
            if (isBookmarked ? _bookmarked.Add(problemNumber) : _bookmarked.Remove(problemNumber)) BookmarkStatusChanged?.Invoke(problemNumber, isBookmarked);
            return Task.CompletedTask;
        }

        public bool IsProblemSolved(int problemNumber) => _solved.Contains(problemNumber);

        public bool IsProblemBookmarked(int problemNumber) => _bookmarked.Contains(problemNumber);
    }
}
