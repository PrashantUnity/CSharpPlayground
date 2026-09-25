using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Hover (Quick Info) for scripts: what <see cref="CSharpQuickInfoService"/> shows for the symbol under the pointer.
/// The .NET documentation comes from the SDK's reference pack, which every machine that builds this repo has.
/// </summary>
public class CSharpQuickInfoServiceTests
{
    // RoslynCompilerService is slow to create; share one per class (see CSharpCompletionServiceTests).
    private static readonly RoslynCompilerService Compiler = new();
    private static readonly CSharpQuickInfoService Service = new(Compiler);

    private const string TwoSumScript = """
        public class Solution
        {
            /// <summary>Finds the two indices whose values add up to <paramref name="target"/>.</summary>
            /// <param name="nums">The numbers to search.</param>
            /// <param name="target">The sum to reach.</param>
            /// <returns>The two indices, or an empty array when no pair adds up.</returns>
            public int[] TwoSum(int[] nums, int target)
            {
                var seen = new Dictionary<int, int>();
                for (int i = 0; i < nums.Length; i++)
                {
                    if (seen.TryGetValue(target - nums[i], out int j)) return new[] { j, i };
                    seen[nums[i]] = i;
                }
                return Array.Empty<int>();
            }
        }

        var sol = new Solution();
        Check("Example 1", sol.TwoSum(new[] { 2, 7, 11, 15 }, 9), "[0,1]", anyOrder: true);
        var numbers = new List<int> { 3, 1, 2 };
        numbers.Add(4);
        var evens = numbers.Where(n => n % 2 == 0).ToList();
        var sb = new StringBuilder();
        int count = evens.Count;
        Console.WriteLine(count);
        """;

    private static async Task<CSharpQuickInfo> HoverAsync(string code, string target, int skip = 0)
    {
        var info = await TryHoverAsync(code, target, skip);
        Assert.NotNull(info);
        return info!;
    }

    // Hovers the first character of the `skip`-th occurrence of `target`.
    private static Task<CSharpQuickInfo?> TryHoverAsync(string code, string target, int skip = 0)
    {
        var offset = -1;
        for (var i = 0; i <= skip; i++) offset = code.IndexOf(target, offset + 1, System.StringComparison.Ordinal);
        Assert.True(offset >= 0, $"'{target}' is not in the code");
        return Service.GetQuickInfoAsync(code, offset);
    }

    private static string Text(System.Collections.Generic.IEnumerable<QuickInfoTextRun> runs) => string.Concat(runs.Select(r => r.Text));

    [Fact]
    public async Task ConsoleWriteLine_ShowsSignatureOverloadsContainerAndDocumentation()
    {
        var info = await HoverAsync(TwoSumScript, "WriteLine");

        Assert.StartsWith("public static void WriteLine(int value) (+ ", Text(info.Signature));
        Assert.EndsWith(" overloads)", Text(info.Signature));
        Assert.Equal("in class System.Console", Text(info.Container));
        Assert.Contains("followed by the current line terminator", Text(info.Documentation.Summary));
        Assert.Contains(info.Documentation.Parameters, p => p.Name.Text == "value");
    }

    [Fact]
    public async Task Span_IsTheHoveredIdentifierInTheScriptsOwnOffsets()
    {
        var info = await HoverAsync(TwoSumScript, "WriteLine");

        Assert.Equal(TwoSumScript.IndexOf("WriteLine", System.StringComparison.Ordinal), info.SpanStart);
        Assert.Equal("WriteLine".Length, info.SpanLength);
    }

    [Fact]
    public async Task SignatureParts_AreColouredLikeCode()
    {
        var info = await HoverAsync(TwoSumScript, "WriteLine");

        Assert.Contains(info.Signature, r => r is { Text: "WriteLine", Kind: QuickInfoTextKind.Method });
        Assert.Contains(info.Signature, r => r is { Text: "value", Kind: QuickInfoTextKind.Variable });
        Assert.Contains(info.Signature, r => r.Kind == QuickInfoTextKind.Keyword && r.Text.Contains("static"));
        Assert.Contains(info.Signature, r => r.Kind == QuickInfoTextKind.Label && r.Text.Contains("overloads"));
    }

    [Fact]
    public async Task LocalVariable_ShowsItsKindAndType()
    {
        var info = await HoverAsync(TwoSumScript, "numbers", skip: 1);

        Assert.Equal("(local variable) List<int> numbers", Text(info.Signature));
        Assert.Empty(info.Container);
    }

    [Fact]
    public async Task MemberOfConstructedGeneric_ShowsSubstitutedSignatureAndDefinitionDocs()
    {
        var info = await HoverAsync(TwoSumScript, "Add(4)");

        Assert.StartsWith("public void Add(int item)", Text(info.Signature));
        Assert.Equal("in class System.Collections.Generic.List<int>", Text(info.Container));
        Assert.Contains("Adds an object to the end of the", Text(info.Documentation.Summary));
        // <see cref="T:System.Collections.Generic.List`1"/> reads as the type's short name, coloured as a type.
        Assert.Contains(info.Documentation.Summary, r => r is { Text: "List<T>", Kind: QuickInfoTextKind.Type });
    }

    [Fact]
    public async Task Var_ShowsTheInferredTypeAndItsTypeArguments()
    {
        var info = await HoverAsync(TwoSumScript, "var numbers");

        Assert.Equal("public class List<T>", Text(info.Signature));
        Assert.Equal("in namespace System.Collections.Generic", Text(info.Container));
        Assert.Equal("T is int", Text(Assert.Single(info.TypeArguments)));
        Assert.Contains("strongly typed list", Text(info.Documentation.Summary));
    }

    [Fact]
    public async Task PredefinedType_ShowsTheRuntimeType()
    {
        var info = await HoverAsync(TwoSumScript, "int count");

        Assert.Equal("public readonly struct Int32", Text(info.Signature));
        Assert.Equal("in namespace System", Text(info.Container));
        Assert.Contains("32-bit signed integer", Text(info.Documentation.Summary));
    }

    [Fact]
    public async Task ReducedExtensionMethod_ShowsExtensionLabelAndStaticForm()
    {
        var info = await HoverAsync(TwoSumScript, "Where");

        Assert.StartsWith("(extension) public static IEnumerable<int> Where<int>(this IEnumerable<int> source, Func<int, bool> predicate)", Text(info.Signature));
        Assert.Equal("in class System.Linq.Enumerable", Text(info.Container));
        Assert.Contains("Filters a sequence of values based on a predicate", Text(info.Documentation.Summary));
    }

    [Fact]
    public async Task ObjectCreation_ShowsTheConstructorThatRuns()
    {
        var info = await HoverAsync(TwoSumScript, "StringBuilder()");

        Assert.StartsWith("public StringBuilder()", Text(info.Signature));
        Assert.Equal("in class System.Text.StringBuilder", Text(info.Container));
        Assert.Contains("Initializes a new instance of the", Text(info.Documentation.Summary));
    }

    [Fact]
    public async Task ScriptMethodWithDocComment_ShowsItsParsedSections()
    {
        // The statements come after the class, as in every Blind 75 script: the call still binds.
        var info = await HoverAsync(TwoSumScript, "TwoSum(new");

        Assert.Equal("public int[] TwoSum(int[] nums, int target)", Text(info.Signature));
        Assert.Equal("in class Solution", Text(info.Container));
        Assert.Equal("Finds the two indices whose values add up to target.", Text(info.Documentation.Summary));
        Assert.Contains(info.Documentation.Summary, r => r is { Text: "target", Kind: QuickInfoTextKind.Variable });
        Assert.Equal(["nums", "target"], info.Documentation.Parameters.Select(p => p.Name.Text));
        Assert.Equal("The two indices, or an empty array when no pair adds up.", Text(info.Documentation.Returns));
    }

    [Fact]
    public async Task Parameter_ShowsTheDescriptionFromItsMethodsComment()
    {
        var info = await HoverAsync(TwoSumScript, "target - nums");

        Assert.Equal("(parameter) int target", Text(info.Signature));
        Assert.Equal("The sum to reach.", Text(info.Documentation.Summary));
    }

    [Fact]
    public async Task ScriptHelper_Check_ShowsThePluginsOwnDocumentation()
    {
        var info = await HoverAsync(TwoSumScript, "Check(");

        Assert.StartsWith("public static bool Check<int[]>(string name, int[] actual, string expected, bool anyOrder = false)", Text(info.Signature));
        Assert.Equal("in class PdfEditorApp.Plugins.CSharpEditor.Services.ScriptHelpers", Text(info.Container));
        Assert.Contains("LeetCode-style", Text(info.Documentation.Summary));
    }

    [Theory]
    [InlineData("Example 1")]   // inside a string literal
    [InlineData("for (")]       // a keyword that isn't a type
    [InlineData(" = new List")] // whitespace between tokens
    public async Task NonSymbols_ShowNothing(string target)
    {
        Assert.Null(await TryHoverAsync(TwoSumScript, target));
    }

    [Fact]
    public async Task UnresolvedName_ShowsNothing()
    {
        Assert.Null(await TryHoverAsync("var x = missingThing.Value;", "missingThing"));
    }

    [Fact]
    public async Task RangeVariable_ShowsItsElementType()
    {
        const string code = "var q = from item in new[] { 1, 2 } select item * 2;";

        var info = await HoverAsync(code, "item", skip: 1);

        Assert.Equal("(range variable) int item", Text(info.Signature));
    }
}
