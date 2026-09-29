using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>The Blind 75 problems as Rust: LeetCode's signatures, the catalog's tests translated from their C# calls, and the harness around them.</summary>
public class RustProblemLanguageAdapterTests
{
    private static readonly RustProblemLanguageAdapter Adapter = RustProblemLanguageAdapter.Instance;

    private static BlindProblemItem Problem(int number) => Blind75CatalogService.GetProblemByNumber(number)!;

    [Fact]
    public void Rust_IsARegisteredProblemLanguage_AndTheHubCanPickIt()
    {
        Assert.True(ProblemLanguageRegistry.TryGetAdapter(LanguageIds.Rust, out var adapter));
        Assert.Same(Adapter, adapter);
        Assert.Equal(".rs", Adapter.DefaultFileExtension);

        var studio = new CSharpBlindProblemsViewModel(new LocalBlindProgressService(Path.Combine(Path.GetTempPath(), "FryPDF_RustHub_" + Guid.NewGuid().ToString("N"))), () => { }, _ => { }, _ => { }, _ => { });
        studio.SetSelectedLanguageCommand.Execute("rust");

        Assert.Equal("rust", studio.SelectedLanguage);
        Assert.Equal("Rust (Cargo)", studio.SelectedLanguageDisplayName);
        Assert.Contains(LanguageIds.Rust, studio.AvailableLanguages);
    }

    [Fact]
    public void TheHubsLanguageMenu_OffersRust()
    {
        var axaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Controls", "BlindProblemsTopBarControl.axaml"));

        Assert.Contains("CommandParameter=\"rust\"", axaml);
    }

    [Fact]
    public void EveryCallOfTheCatalog_IsTranslatedToRust()
    {
        var untranslated = new List<string>();
        var total = 0;
        foreach (var problem in Blind75CatalogService.GetAllProblems())
        {
            var method = RustProblemSignature.For(problem.SolutionCode, problem.Title, problem.Number);
            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                total++;
                if (!RustProblemLanguageAdapter.TryTranslate(test.Call, method, problem.Number, out _, out var reason))
                {
                    untranslated.Add($"#{problem.Number} {test.Name}: {test.Call} ({reason})");
                }
            }
        }

        Assert.True(total > 400, $"only {total} calls in the catalog");
        Assert.True(untranslated.Count == 0, string.Join("\n", untranslated));
    }

    [Theory]
    [InlineData(1, "sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)", "Solution::two_sum(vec![2, 7, 11, 15], 9)")]
    [InlineData(1, "sol.TwoSum(new[] { -1000000000, 7, 1000000000 }, 0)", "Solution::two_sum(vec![-1000000000, 7, 1000000000], 0)")]
    [InlineData(3, "sol.LengthOfLongestSubstring(\"abcabcbb\")", "Solution::length_of_longest_substring(\"abcabcbb\".to_string())")]
    [InlineData(3, "sol.LengthOfLongestSubstring(\"\")", "Solution::length_of_longest_substring(\"\".to_string())")]
    [InlineData(15, "sol.ThreeSum(new[] { -1, 0, 1, 2, -1, -4 })", "Solution::three_sum(vec![-1, 0, 1, 2, -1, -4])")]
    [InlineData(19, "sol.RemoveNthFromEnd(BuildList(1, 2, 3, 4, 5), 2)", "Solution::remove_nth_from_end(build_list(&[1, 2, 3, 4, 5]), 2)")]
    [InlineData(21, "sol.MergeTwoLists(BuildList(), BuildList(0))", "Solution::merge_two_lists(build_list(&[]), build_list(&[0]))")]
    [InlineData(23, "sol.MergeKLists(new[] { BuildList(1, 4, 5), BuildList(1, 3, 4), BuildList(2, 6) })", "Solution::merge_k_lists(vec![build_list(&[1, 4, 5]), build_list(&[1, 3, 4]), build_list(&[2, 6])])")]
    [InlineData(23, "sol.MergeKLists(new ListNode[0])", "Solution::merge_k_lists(Vec::<Option<Box<ListNode>>>::new())")]
    [InlineData(57, "sol.Insert(new int[0][], new[] { 5, 7 })", "Solution::insert(Vec::<Vec<i32>>::new(), vec![5, 7])")]
    [InlineData(56, "sol.Merge(new[] { new[] { 1, 3 }, new[] { 2, 6 } })", "Solution::merge(vec![vec![1, 3], vec![2, 6]])")]
    [InlineData(48, "Rotated(new[] { new[] { 1, 2 }, new[] { 3, 4 } })", "rotated(vec![vec![1, 2], vec![3, 4]])")]
    [InlineData(73, "Zeroed(new[] { new[] { 1 }, new[] { 0 } })", "zeroed(vec![vec![1], vec![0]])")]
    [InlineData(143, "Reordered(1, 2, 3, 4)", "reordered(&[1, 2, 3, 4])")]
    [InlineData(79, "sol.Exist(Board(\"ABCE\", \"SFCS\"), \"ABCCED\")", "Solution::exist(board(&[\"ABCE\", \"SFCS\"]), \"ABCCED\".to_string())")]
    [InlineData(200, "sol.NumIslands(Grid(\"110\", \"011\"))", "Solution::num_islands(board(&[\"110\", \"011\"]))")]
    [InlineData(102, "sol.LevelOrder(BuildTree(3, 9, 20, null, null, 15, 7))", "Solution::level_order(build_tree(&[Some(3), Some(9), Some(20), None, None, Some(15), Some(7)]))")]
    [InlineData(102, "sol.LevelOrder(BuildTree())", "Solution::level_order(build_tree(&[]))")]
    [InlineData(102, "sol.LevelOrder(BuildTree(-1, -2, -3))", "Solution::level_order(build_tree(&[Some(-1), Some(-2), Some(-3)]))")]
    [InlineData(235, "LcaValue(BuildTree(2, 1), 2, 1)", "lca_value(build_tree(&[Some(2), Some(1)]), 2, 1)")]
    [InlineData(105, "sol.BuildTree(new[] { 3, 9, 20, 15, 7 }, new[] { 9, 3, 15, 20, 7 })", "Solution::build_tree(vec![3, 9, 20, 15, 7], vec![9, 3, 15, 20, 7])")]
    [InlineData(133, "sol.CloneGraph(BuildGraph(new[] { new int[0] }))", "Solution::clone_graph(build_graph(vec![Vec::<i32>::new()]))")]
    [InlineData(133, "sol.CloneGraph(BuildGraph(new int[0][]))", "Solution::clone_graph(build_graph(Vec::<Vec<i32>>::new()))")]
    [InlineData(133, "IsDeepCopy(BuildGraph(new[] { new[] { 2 }, new[] { 1 } }))", "is_deep_copy(build_graph(vec![vec![2], vec![1]]))")]
    [InlineData(139, "sol.WordBreak(\"leetcode\", new[] { \"leet\", \"code\" })", "Solution::word_break(\"leetcode\".to_string(), vec![\"leet\".to_string(), \"code\".to_string()])")]
    [InlineData(190, "sol.reverseBits(43261596u)", "Solution::reverse_bits(43261596u32)")]
    [InlineData(128, "sol.LongestConsecutive(Array.Empty<int>())", "Solution::longest_consecutive(Vec::<i32>::new())")]
    [InlineData(98, "sol.IsValidBST(BuildTree(int.MinValue, null, int.MaxValue))", "Solution::is_valid_bst(build_tree(&[Some(i32::MIN), None, Some(i32::MAX)]))")]
    [InlineData(191, "sol.HammingWeight(int.MaxValue)", "Solution::hamming_weight(i32::MAX)")]
    [InlineData(141, "sol.HasCycle(BuildCycle(new[] { 3, 2, 0, -4 }, 1))", "Solution::has_cycle(build_cycle(&[3, 2, 0, -4], 1))")]
    [InlineData(141, "sol.HasCycle(BuildCycle(new[] { 1 }, -1))", "Solution::has_cycle(build_cycle(&[1], -1))")]
    [InlineData(269, "IsConsistent(new[] { \"wrt\", \"wrf\" }, sol.AlienOrder(new[] { \"wrt\", \"wrf\" }))", "is_consistent(vec![\"wrt\".to_string(), \"wrf\".to_string()], Solution::alien_order(vec![\"wrt\".to_string(), \"wrf\".to_string()]))")]
    [InlineData(271, "RoundTrip(\"lint\", \"code\")", "round_trip(vec![\"lint\".to_string(), \"code\".to_string()])")]
    [InlineData(271, "RoundTrip()", "round_trip(vec![])")]
    [InlineData(271, "new Codec().Encode(new[] { \"lint\", \"code\" })", "Codec::new().encode(vec![\"lint\".to_string(), \"code\".to_string()])")]
    [InlineData(297, "RoundTrip(BuildTree(1, 2, 3))", "round_trip(build_tree(&[Some(1), Some(2), Some(3)]))")]
    [InlineData(297, "codec.serialize(BuildTree(1, 2))", "codec.serialize(build_tree(&[Some(1), Some(2)]))")]
    [InlineData(208, "Replay(new[] { \"Trie\", \"insert\" }, new[] { \"\", \"apple\" })", "replay(vec![\"Trie\".to_string(), \"insert\".to_string()], vec![\"\".to_string(), \"apple\".to_string()])")]
    [InlineData(295, "Replay(new[] { \"MedianFinder\", \"addNum\" }, new[] { 0, -1 })", "replay(vec![\"MedianFinder\".to_string(), \"addNum\".to_string()], vec![0, -1])")]
    public void ACall_IsTranslatedToTheRustThatMakesIt(int number, string call, string expected)
    {
        var problem = Problem(number);

        var ok = RustProblemLanguageAdapter.TryTranslate(call, RustProblemSignature.For(problem.SolutionCode, problem.Title, number), number, out var rust, out var reason);

        Assert.True(ok, reason);
        Assert.Equal(expected, rust);
    }

    [Theory]
    [InlineData("sol.Foo(1 + 2)")]
    [InlineData("sol.Foo(x)")]
    [InlineData("Unknown(1)")]
    [InlineData("sol.Foo(new[] { 1, 2 ")]
    [InlineData("sol.Foo(\"never closed)")]
    [InlineData("sol.Foo(1) extra")]
    public void ACallItCantRead_IsRefused_NotTurnedIntoBrokenRust(string call)
    {
        var ok = RustProblemLanguageAdapter.TryTranslate(call, new RustMethod("foo", [], null), 1, out var rust, out var reason);

        Assert.False(ok);
        Assert.Equal(string.Empty, rust);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void ADoubleParameter_GetsItsWholeNumbersAsDecimals()
    {
        var method = new RustMethod("my_pow", [new RustParameter("x", "f64"), new RustParameter("n", "i32")], "f64");

        Assert.True(RustProblemLanguageAdapter.TryTranslate("sol.MyPow(2, 10)", method, 1, out var rust, out _));
        Assert.Equal("Solution::my_pow(2.0, 10)", rust);
    }

    [Theory]
    [InlineData(1, "pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32>")]
    [InlineData(3, "pub fn length_of_longest_substring(s: String) -> i32")]
    [InlineData(15, "pub fn three_sum(nums: Vec<i32>) -> Vec<Vec<i32>>")]
    [InlineData(19, "pub fn remove_nth_from_end(head: Option<Box<ListNode>>, n: i32) -> Option<Box<ListNode>>")]
    [InlineData(23, "pub fn merge_k_lists(lists: Vec<Option<Box<ListNode>>>) -> Option<Box<ListNode>>")]
    [InlineData(48, "pub fn rotate(matrix: &mut Vec<Vec<i32>>)")]
    [InlineData(49, "pub fn group_anagrams(strs: Vec<String>) -> Vec<Vec<String>>")]
    [InlineData(54, "pub fn spiral_order(matrix: Vec<Vec<i32>>) -> Vec<i32>")]
    [InlineData(79, "pub fn exist(board: Vec<Vec<char>>, word: String) -> bool")]
    [InlineData(98, "pub fn is_valid_bst(root: Option<Rc<RefCell<TreeNode>>>) -> bool")]
    [InlineData(133, "pub fn clone_graph(node: Option<Rc<RefCell<Node>>>) -> Option<Rc<RefCell<Node>>>")]
    [InlineData(141, "pub fn has_cycle(head: Option<Rc<RefCell<ListNode>>>) -> bool")]
    [InlineData(143, "pub fn reorder_list(head: &mut Option<Box<ListNode>>)")]
    [InlineData(190, "pub fn reverse_bits(n: u32) -> u32")]
    [InlineData(235, "pub fn lowest_common_ancestor(root: Option<Rc<RefCell<TreeNode>>>, p: Option<Rc<RefCell<TreeNode>>>, q: Option<Rc<RefCell<TreeNode>>>) -> Option<Rc<RefCell<TreeNode>>>")]
    [InlineData(300, "pub fn length_of_lis(nums: Vec<i32>) -> i32")]
    [InlineData(207, "pub fn can_finish(num_courses: i32, prerequisites: Vec<Vec<i32>>) -> bool")]
    [InlineData(212, "pub fn find_words(board: Vec<Vec<char>>, words: Vec<String>) -> Vec<String>")]
    public void TheSolutionMethod_HasLeetCodesRustSignature(int number, string expected)
    {
        var problem = Problem(number);

        Assert.Equal(expected, RustProblemSignature.For(problem.SolutionCode, problem.Title, problem.Number).Signature);
    }

    [Theory]
    [InlineData("IsValidBST", "is_valid_bst")]
    [InlineData("LengthOfLIS", "length_of_lis")]
    [InlineData("reverseBits", "reverse_bits")]
    [InlineData("TwoSum", "two_sum")]
    [InlineData("HammingWeight", "hamming_weight")]
    [InlineData("list1", "list1")]
    [InlineData("numCourses", "num_courses")]
    [InlineData("text1", "text1")]
    [InlineData("s", "s")]
    public void Names_AreSnakeCase(string csharp, string expected)
    {
        Assert.Equal(expected, RustProblemSignature.SnakeCase(csharp));
    }

    [Fact]
    public void TheScript_IsARunnableProgram_WithTheSolutionTheHarnessAndOneCheckPerCase()
    {
        var problem = Problem(1);

        var script = Adapter.BuildScript(problem);

        Assert.Equal("01. Two Sum.rs", script.Title);
        Assert.Equal(LanguageIds.Rust, script.LanguageId);
        Assert.Contains("struct Solution;", script.Code);
        Assert.Contains("pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32> {", script.Code);
        Assert.Contains("fn check<T: Show, F: FnOnce() -> T>(", script.Code);
        Assert.Contains("fn main() {", script.Code);
        Assert.Contains("all_passed &= check(\"Example 1\", || Solution::two_sum(vec![2, 7, 11, 15], 9), \"[0,1]\", true);", script.Code);
        Assert.Equal(problem.Tests.Count + problem.ExtraTests.Count, script.Code.Split('\n').Count(l => l.TrimStart().StartsWith("all_passed &= check(", StringComparison.Ordinal)));
        Assert.StartsWith("// 1. Two Sum", script.Code);
        Assert.Contains("How to think", script.Notes);
        Assert.NotEmpty(script.TestCases);
    }

    [Fact]
    public void TheScript_OfAProblemThatAllowsAnyOrder_SaysSo()
    {
        var code = Adapter.BuildScript(Problem(49)).Code;

        Assert.Contains(", true);", code);
    }

    [Theory]
    [InlineData(19, true, false, false)]
    [InlineData(98, false, true, false)]
    [InlineData(133, false, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(297, false, true, false)]
    public void TheNodeTypes_AreIncludedOnlyWhereTheProblemNeedsThem(int number, bool list, bool tree, bool graph)
    {
        var code = Adapter.BuildScript(Problem(number)).Code;

        Assert.Equal(list, code.Contains("pub struct ListNode", StringComparison.Ordinal));
        Assert.Equal(tree, code.Contains("pub struct TreeNode", StringComparison.Ordinal));
        Assert.Equal(graph, code.Contains("pub struct Node", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnsolvedProblem_BuildsToo_ItsStarterReturnsAValueOfTheRightType()
    {
        var starter = RustProblemLanguageAdapter.Starter(Problem(3));

        Assert.Contains("pub fn length_of_longest_substring(s: String) -> i32 {", starter);
        Assert.Contains("0\n    }", starter);
        Assert.Contains("Vec::new()", RustProblemLanguageAdapter.Starter(Problem(1)));
        Assert.Contains("None", RustProblemLanguageAdapter.Starter(Problem(21)));
        Assert.Contains("false", RustProblemLanguageAdapter.Starter(Problem(20)));
        Assert.Contains("String::new()", RustProblemLanguageAdapter.Starter(Problem(5)));
        Assert.DoesNotContain("todo!", RustProblemLanguageAdapter.Starter(Problem(1)));

        // an in-place method returns nothing: there is no value to make up
        var rotate = RustProblemLanguageAdapter.Starter(Problem(48));
        Assert.Contains("pub fn rotate(matrix: &mut Vec<Vec<i32>>) {", rotate);
    }

    [Theory]
    [InlineData(208, "struct Trie", "fn starts_with(&self, prefix: String) -> bool")]
    [InlineData(211, "struct WordDictionary", "fn add_word(&mut self, word: String)")]
    [InlineData(295, "struct MedianFinder", "fn find_median(&self) -> f64")]
    [InlineData(271, "struct Codec", "fn decode(&self, s: String) -> Vec<String>")]
    [InlineData(297, "struct Codec", "fn deserialize(&self, data: String) -> Option<Rc<RefCell<TreeNode>>>")]
    public void ADesignProblem_StartsFromItsClass(int number, string type, string method)
    {
        var code = Adapter.BuildScript(Problem(number)).Code;

        Assert.Contains(type, code);
        Assert.Contains(method, code);
        Assert.DoesNotContain("struct Solution", code);
        Assert.Contains("fn replay(", number is 208 or 211 or 295 ? code : "fn replay(" + code);
    }

    [Fact]
    public void TheProblemsWithPythonJavaScriptAndJavaSolutions_HaveARustOneToo()
    {
        var withOthers = Blind75CatalogService.GetAllProblems().Where(p => p.LanguageImplementations.ContainsKey("python")).Select(p => p.Number).ToList();
        var withRust = Blind75CatalogService.GetAllProblems().Where(p => p.LanguageImplementations.ContainsKey(LanguageIds.Rust)).Select(p => p.Number).ToList();

        Assert.Equal(18, withRust.Count);
        Assert.Equal(withOthers.Order(), withRust.Order());
        Assert.All(withRust, number => Assert.True(Problem(number).LanguageImplementations.ContainsKey("python"), $"#{number} lost its Python solution"));
    }

    [Fact]
    public void ACuratedSolution_IsWhatTheScriptAndTheNotebookStartFrom()
    {
        var problem = Problem(1);

        var script = Adapter.BuildScript(problem).Code;
        var solutionCell = Adapter.BuildNotebook(problem).Cells.Where(c => c.Type == CellType.Code).ElementAt(1).Source;

        Assert.Contains("let mut seen: HashMap<i32, i32> = HashMap::new();", script);
        Assert.Contains("let mut seen: HashMap<i32, i32> = HashMap::new();", solutionCell);
        Assert.DoesNotContain("TODO", script);
        Assert.Contains("TODO: Implement solution for 1. Two Sum", problem.LanguageImplementations[LanguageIds.Rust].StarterCode);
    }

    [Fact]
    public void ACuratedSolution_LeavesTheOtherLanguagesSolutionsAlone()
    {
        var bundles = Problem(1).LanguageImplementations;

        Assert.Contains("python", bundles.Keys);
        Assert.Contains("javascript", bundles.Keys);
        Assert.Contains("java", bundles.Keys);
        Assert.Contains(LanguageIds.Rust, bundles.Keys);
    }

    [Fact]
    public void TheNotebook_HasASetupCell_ASolutionCell_AndATestsCellOfChecks()
    {
        var notebook = Adapter.BuildNotebook(Problem(19));

        var code = notebook.Cells.Where(c => c.Type == CellType.Code).ToList();
        Assert.All(code, c => Assert.Equal(LanguageIds.Rust, c.Language));
        Assert.Equal(3, code.Count);
        Assert.Contains("use std::rc::Rc;", code[0].Source);
        Assert.Contains("pub struct ListNode", code[0].Source);
        Assert.Contains("fn check<T: Show", code[0].Source);
        Assert.Contains("struct Solution;", code[1].Source);
        Assert.DoesNotContain("fn main", code[2].Source);
        Assert.Contains("check(\"Example 1\", || Solution::remove_nth_from_end(build_list(&[1, 2, 3, 4, 5]), 2), \"[1,2,3,5]\", false);", code[2].Source);
        Assert.DoesNotContain("all_passed", code[2].Source);
        Assert.Equal("19. Remove Nth Node From End of List (Rust Notebook)", notebook.Title);
    }

    [Fact]
    public void TheTestsCell_OfAProblemWithHelpers_DefinesThemBeforeTheChecks()
    {
        var tests = Adapter.BuildTestCode(Problem(48));

        Assert.True(tests.IndexOf("fn rotated(", StringComparison.Ordinal) < tests.IndexOf("check(", StringComparison.Ordinal));
        Assert.Contains("Solution::rotate(&mut matrix);", tests);
    }

    [Fact]
    public void SerializeAndDeserialize_KeepsOneCodecForItsCases()
    {
        var script = Adapter.BuildScript(Problem(297)).Code;
        var notebookTests = Adapter.BuildTestCode(Problem(297));

        Assert.Contains("let codec = Codec::new();", script);
        Assert.Contains("let codec = Codec::new();", notebookTests);
    }

    [Fact]
    public void AnUntranslatableCase_BecomesALineThatSaysSo_AndTheScriptStillBuilds()
    {
        var problem = new BlindProblemItem
        {
            Number = 9999,
            Title = "Odd One",
            Category = "Arrays & Hashing",
            SolutionCode = "public class Solution { public int Odd(int[] nums) => 0; }",
            Tests = [new BlindTest { Name = "Case A", Call = "sol.Odd(1 + 2)", Expected = "0" }, new BlindTest { Name = "Case B", Call = "sol.Odd(new[] { 1 })", Expected = "0" }]
        };

        var code = Adapter.BuildScript(problem).Code;

        Assert.Contains("// Not translated to Rust: sol.Odd(1 + 2)", code);
        Assert.Contains("println!(\"⚠️ {} → this case isn't available in Rust yet\", \"Case A\");", code);
        Assert.Contains("all_passed &= check(\"Case B\"", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CSharpEditorPlugin.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root wasn't found.");
    }
}
