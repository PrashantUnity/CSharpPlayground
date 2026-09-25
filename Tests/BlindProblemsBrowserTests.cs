using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>The Blind 75 problem list: LeetCode links, sorting, filter states and the resizable table columns.</summary>
public class BlindProblemsBrowserTests : IDisposable
{
    private readonly string _progressDir = Path.Combine(Path.GetTempPath(), "FrySharpBrowserTest_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_progressDir)) Directory.Delete(_progressDir, true);
    }

    private CSharpBlindProblemsViewModel NewViewModel(Action<string>? openUrl = null) =>
        new(new LocalBlindProgressService(_progressDir), openUrlAction: openUrl ?? (_ => { }));

    // ── LeetCode links ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "two-sum")]
    [InlineData(15, "3sum")]
    [InlineData(23, "merge-k-sorted-lists")]
    [InlineData(191, "number-of-1-bits")]
    [InlineData(208, "implement-trie-prefix-tree")]
    [InlineData(211, "design-add-and-search-words-data-structure")]
    [InlineData(230, "kth-smallest-element-in-a-bst")]
    [InlineData(435, "non-overlapping-intervals")]
    public void LeetCodeUrl_UsesLeetCodesSlugForTheTitle(int number, string slug)
    {
        var problem = Blind75CatalogService.GetProblemByNumber(number)!;

        Assert.Equal(slug, problem.LeetCodeSlug);
        Assert.Equal($"https://leetcode.com/problems/{slug}/", problem.LeetCodeUrl);
    }

    [Fact]
    public void EveryProblem_HasAWellFormedUniqueLeetCodeLink()
    {
        var problems = Blind75CatalogService.GetAllProblems();

        Assert.All(problems, p => Assert.Matches(new Regex("^[a-z0-9]+(-[a-z0-9]+)*$"), p.LeetCodeSlug));
        Assert.Equal(problems.Count, problems.Select(p => p.LeetCodeSlug).Distinct().Count());
    }

    [Fact]
    public void PremiumProblems_SayTheLinkNeedsPremium()
    {
        Assert.Contains("Premium", Blind75CatalogService.GetProblemByNumber(252)!.LeetCodeTooltip);
        Assert.DoesNotContain("Premium", Blind75CatalogService.GetProblemByNumber(1)!.LeetCodeTooltip);
    }

    [Fact]
    public void OpenOnLeetCode_OpensTheProblemsPage_OrTheSelectedOne()
    {
        var opened = new List<string>();
        var vm = NewViewModel(opened.Add);

        vm.OpenOnLeetCode(vm.AllProblems.First(p => p.Number == 200));
        vm.SelectedProblem = vm.AllProblems.First(p => p.Number == 417);
        vm.OpenOnLeetCodeCommand.Execute(null);

        Assert.Equal(new[] { "https://leetcode.com/problems/number-of-islands/", "https://leetcode.com/problems/pacific-atlantic-water-flow/" }, opened);
    }

    // ── Sorting ───────────────────────────────────────────────────────────────

    [Fact]
    public void Problems_AreInNumberOrderByDefault()
    {
        var vm = NewViewModel();

        Assert.True(vm.IsSortedByTitle);
        Assert.Equal(vm.FilteredProblems.Select(p => p.Number).OrderBy(n => n), vm.FilteredProblems.Select(p => p.Number));
    }

    [Fact]
    public void SortBy_OrdersByTheColumn_AndFlipsWhenRepeated()
    {
        var vm = NewViewModel();

        vm.SortBy("Difficulty");
        Assert.True(vm.IsSortedByDifficulty);
        Assert.False(vm.IsSortDescending);
        AssertOrdered(vm, p => (int)p.Difficulty);

        vm.SortBy("Difficulty");
        Assert.True(vm.IsSortDescending);
        Assert.Equal(ProblemDifficulty.Hard, vm.FilteredProblems[0].Difficulty);
        Assert.Equal(ProblemDifficulty.Easy, vm.FilteredProblems[^1].Difficulty);

        vm.SortBy("Acceptance");
        Assert.False(vm.IsSortDescending);
        AssertOrdered(vm, p => p.AcceptanceRate);
    }

    [Fact]
    public void Sorting_KeepsTiesInNumberOrder_AndSurvivesFiltering()
    {
        var vm = NewViewModel();
        vm.SortBy("Difficulty");
        vm.SetCategory("Trees");

        var mediums = vm.FilteredProblems.Where(p => p.Difficulty == ProblemDifficulty.Medium).Select(p => p.Number).ToList();
        Assert.Equal(mediums.OrderBy(n => n), mediums);
        Assert.All(vm.FilteredProblems, p => Assert.Equal("Trees", p.Category));
        AssertOrdered(vm, p => (int)p.Difficulty);
    }

    private static void AssertOrdered(CSharpBlindProblemsViewModel vm, Func<BlindProblemItem, double> key)
    {
        var keys = vm.FilteredProblems.Select(key).ToList();
        Assert.Equal(keys.OrderBy(k => k), keys);
    }

    // ── Filters ───────────────────────────────────────────────────────────────

    [Fact]
    public void FilterButtons_KnowWhichOneIsSelected()
    {
        var vm = NewViewModel();
        Assert.True(vm.IsDifficultyAll);
        Assert.True(vm.IsStatusAll);

        vm.SetDifficulty("Med");
        vm.SetStatusFilter("Bookmarked");

        Assert.True(vm.IsDifficultyMedium);
        Assert.False(vm.IsDifficultyAll || vm.IsDifficultyEasy || vm.IsDifficultyHard);
        Assert.True(vm.IsStatusBookmarked);
        Assert.False(vm.IsStatusAll || vm.IsStatusSolved || vm.IsStatusUnsolved);
    }

    [Fact]
    public void NoMatches_IsReported_AndClearFiltersBringsEverythingBack()
    {
        var vm = NewViewModel();

        vm.SetCategory("Tries");
        vm.SetDifficulty("Easy");
        Assert.Empty(vm.FilteredProblems);
        Assert.True(vm.HasNoResults);

        vm.SearchQuery = "trie";
        vm.ClearFilters();

        Assert.False(vm.HasNoResults);
        Assert.Equal(vm.AllProblems.Count, vm.FilteredProblems.Count);
        Assert.Equal(("All", "All", "All", ""), (vm.SelectedCategory, vm.SelectedDifficulty, vm.SelectedStatusFilter, vm.SearchQuery));
        Assert.True(vm.Categories.Single(c => c.Name == "All").IsSelected);
    }

    [Fact]
    public void CategoryBadge_UpdatesWhenItsSolvedCountChanges()
    {
        var category = new BlindCategorySummary { Name = "Trees", TotalCount = 12 };
        var changed = new List<string?>();
        category.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        category.SolvedCount = 3;

        Assert.Equal("3/12", category.BadgeText);
        Assert.Contains(nameof(BlindCategorySummary.BadgeText), changed);
        Assert.Equal("Trees: 3 of 12 solved", category.ToolTipText);
    }

    [Fact]
    public void TheProblemShownInDetails_IsHighlighted_OnlyWhileThePanelIsOpen()
    {
        var vm = NewViewModel();
        var first = vm.AllProblems.First(p => p.Number == 300);
        var second = vm.AllProblems.First(p => p.Number == 322);

        vm.OpenDetailFlyout(first);
        Assert.True(first.IsHighlighted);

        vm.OpenDetailFlyout(second);
        Assert.False(first.IsHighlighted);
        Assert.True(second.IsHighlighted);

        vm.CloseDetailFlyout();
        Assert.False(second.IsHighlighted);
    }

    // ── Resizable columns ─────────────────────────────────────────────────────

    // Status 56 + actions 156, then 900 shared by Title (532) and the defaults 168 + 104 + 96.
    private const double RoomyWidth = 56 + 156 + 900;

    private static BlindProblemTableColumns LaidOut(double width) => new() { AvailableWidth = width };

    [Fact]
    public void Columns_StartAtTheirDefaults_WithTitleTakingTheRest()
    {
        var columns = LaidOut(RoomyWidth);

        Assert.Equal(new[] { 532.0, 168, 104, 96 }, columns.Widths());
        Assert.Equal(168, columns.CategoryColumn.Value);
        Assert.True(columns.IsCategoryVisible);
    }

    [Fact]
    public void DraggingADivider_MovesOnlyThatDivider()
    {
        var columns = LaidOut(RoomyWidth);

        columns.BeginResize();
        columns.Resize(1, -100); // Title | Category, to the left
        columns.EndResize();
        Assert.Equal(new[] { 432.0, 268, 104, 96 }, columns.Widths());

        columns.BeginResize();
        columns.Resize(2, 20); // Category | Acceptance, to the right
        columns.EndResize();
        Assert.Equal(new[] { 432.0, 288, 84, 96 }, columns.Widths());
    }

    [Fact]
    public void Dragging_IsMeasuredFromWhereTheDragBegan()
    {
        var columns = LaidOut(RoomyWidth);

        columns.BeginResize();
        columns.Resize(1, -100);
        columns.Resize(1, -150);
        columns.Resize(1, -40);
        columns.EndResize();

        Assert.Equal(new[] { 492.0, 208, 104, 96 }, columns.Widths());
    }

    [Fact]
    public void AColumnAtItsMinimum_PassesTheRestToTheNextOne()
    {
        var columns = LaidOut(RoomyWidth);

        columns.BeginResize();
        columns.Resize(3, -60); // Difficulty grows 60: Acceptance gives 32 (down to 72), Category the other 28
        columns.EndResize();

        Assert.Equal(new[] { 532.0, 140, 72, 156 }, columns.Widths());
    }

    [Fact]
    public void Dragging_StopsWhenEveryColumnOnThatSideIsAtItsMinimum()
    {
        var columns = LaidOut(RoomyWidth);

        columns.BeginResize();
        columns.Resize(1, 5000);
        columns.EndResize();
        Assert.Equal(new[] { 900.0 - 76 - 72 - 72, 76, 72, 72 }, columns.Widths());

        columns.BeginResize();
        columns.Resize(3, -5000);
        columns.EndResize();
        Assert.Equal(new[] { BlindProblemTableColumns.MinTitleWidth, 76, 72, 900 - 180 - 76 - 72 }, columns.Widths());
    }

    [Fact]
    public void ANarrowTable_ShrinksTheColumnsSoTheTitleStaysReadable()
    {
        var columns = LaidOut(56 + 156 + 180 + 300); // room for 300 of the 368 the three columns want

        var widths = columns.Widths();

        Assert.Equal(BlindProblemTableColumns.MinTitleWidth, widths[0], 6);
        Assert.Equal(300, widths.Skip(1).Sum(), 6);
        Assert.True(widths[1] > 76 && widths[2] > 72 && widths[3] > 72);
        Assert.True(columns.IsCategoryVisible);
    }

    [Fact]
    public void ATooNarrowTable_DropsCategory_AndBringsItBackAtItsWidthWhenThereIsRoom()
    {
        var columns = LaidOut(RoomyWidth);
        columns.BeginResize();
        columns.Resize(1, -50);
        columns.EndResize();

        columns.AvailableWidth = 554;
        Assert.False(columns.IsCategoryVisible);
        Assert.Equal(0, columns.CategoryColumn.Value);
        Assert.Equal(BlindProblemTableColumns.MinTitleWidth, columns.TitleWidth, 6);

        columns.AvailableWidth = RoomyWidth;
        Assert.True(columns.IsCategoryVisible);
        Assert.Equal(218, columns.CategoryColumn.Value);
    }

    [Fact]
    public void WithCategoryHidden_TheDividerBesideTitleResizesAcceptance()
    {
        var columns = LaidOut(56 + 156 + 390); // Category hidden; Title 190, Acceptance 104, Difficulty 96
        Assert.False(columns.IsCategoryVisible);
        Assert.Equal(new[] { 190.0, 0, 104, 96 }, columns.Widths());

        columns.BeginResize();
        columns.Resize(2, 20); // Title | Acceptance: Title grows, Acceptance shrinks
        columns.EndResize();
        Assert.Equal(new[] { 210.0, 0, 84, 96 }, columns.Widths());

        columns.BeginResize();
        columns.Resize(2, -30); // Acceptance grows; Title gives, down to its minimum
        columns.EndResize();
        Assert.Equal(new[] { 180.0, 0, 114, 96 }, columns.Widths());
    }

    [Fact]
    public void Reset_PutsTheDefaultsBack_AndTellsTheTable()
    {
        var columns = LaidOut(RoomyWidth);
        var changed = new HashSet<string?>();
        columns.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        columns.BeginResize();
        columns.Resize(2, 40);
        columns.EndResize();
        columns.Reset();

        Assert.Equal(new[] { 532.0, 168, 104, 96 }, columns.Widths());
        Assert.Contains(nameof(BlindProblemTableColumns.CategoryColumn), changed);
        Assert.Contains(nameof(BlindProblemTableColumns.AcceptanceColumn), changed);
        Assert.Contains(nameof(BlindProblemTableColumns.DifficultyColumn), changed);
    }

    [Fact]
    public void TheViewModel_KeepsTheColumnWidths_ForWhenThePageIsShownAgain()
    {
        var vm = NewViewModel();
        vm.Columns.AvailableWidth = RoomyWidth;
        vm.Columns.BeginResize();
        vm.Columns.Resize(1, -60);
        vm.Columns.EndResize();

        // A new table (the page is rebuilt each time it's shown) measures itself and gets the same widths back.
        vm.Columns.AvailableWidth = 0;
        vm.Columns.AvailableWidth = RoomyWidth;
        Assert.Equal(228, vm.Columns.CategoryColumn.Value);
    }
}
