using System.Collections.Concurrent;
using System.Data;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// #!share with the C# kernel: C# values leave as JSON, and JSON arrives as the C# type that fits it best, declared or
/// assigned like any variable a cell sets.
/// </summary>
public class KernelValueSharingTests
{
    public static TheoryData<string, Type> Inferred => new()
    {
        { "42", typeof(int) },
        { "3000000000", typeof(long) },
        { "1.5", typeof(double) },
        { "1e3", typeof(double) },
        { "\"hi\"", typeof(string) },
        { "true", typeof(bool) },
        { "null", typeof(object) },
        { "[1, 2, 3]", typeof(int[]) },
        { "[1, 2.5]", typeof(double[]) },
        { "[1, 3000000000]", typeof(long[]) },
        { "[\"a\", \"b\"]", typeof(string[]) },
        { "[true, false]", typeof(bool[]) },
        { "[[1, 2], [3]]", typeof(int[][]) },
        { "[[1], [2.5]]", typeof(double[][]) },
        { "[\"a\", 1]", typeof(List<object>) },
        { "[null, 1]", typeof(List<object>) },
        { "[]", typeof(List<object>) },
        { "{\"a\": 1, \"b\": [1, \"x\"]}", typeof(Dictionary<string, object>) },
        { "[{\"a\": 1}, {\"a\": 2}]", typeof(List<object>) }
    };

    [Theory]
    [MemberData(nameof(Inferred))]
    public void AJsonValue_BecomesTheCSharpTypeThatFitsIt(string json, Type expected)
    {
        var (type, value) = KernelValueSharing.Infer(json);

        Assert.Equal(expected, type);
        if (value != null) Assert.IsAssignableFrom(expected, value);
    }

    [Fact]
    public void InferredValues_KeepTheirContents()
    {
        Assert.Equal(new[] { 1.0, 2.5 }, (double[])KernelValueSharing.Infer("[1, 2.5]").Value!);
        Assert.Equal(new[] { 3 }, ((int[][])KernelValueSharing.Infer("[[1, 2], [3]]").Value!)[1]);

        var dictionary = (Dictionary<string, object>)KernelValueSharing.Infer("{\"name\": \"Ada\", \"tags\": [1, \"x\"], \"a\": 1, \"a\": 2}").Value!;
        Assert.Equal("Ada", dictionary["name"]);
        Assert.Equal(new object[] { 1, "x" }, (List<object>)dictionary["tags"]);
        Assert.Equal(2, dictionary["a"]); // a repeated key keeps its last value
    }

    [Fact]
    public void InvalidJson_IsExplained()
    {
        var error = Assert.Throws<KernelValueException>(() => KernelValueSharing.Infer("[1, 2"));

        Assert.Contains("isn't valid JSON", error.Message);
    }

    private sealed record Point(int X, int Y);

    private sealed class Node
    {
        public string Name { get; set; } = "";
        public Node? Next { get; set; }
    }

    [Fact]
    public void CSharpValues_LeaveAsJson()
    {
        var cycle = new Node { Name = "a" };
        cycle.Next = cycle;
        var table = new DataTable();
        table.Columns.Add("n", typeof(int));
        table.Columns.Add("label", typeof(string));
        table.Rows.Add(1, "one");
        table.Rows.Add(2, DBNull.Value);

        Assert.Equal("[1,2,3]", KernelValueSharing.ToJson(new[] { 1, 2, 3 }, typeof(int[]), "nums"));
        Assert.Equal("{\"X\":1,\"Y\":2}", KernelValueSharing.ToJson(new Point(1, 2), typeof(Point), "p"));
        Assert.Equal("{\"Item1\":1,\"Item2\":\"a\"}", KernelValueSharing.ToJson((1, "a"), typeof((int, string)), "pair"));
        Assert.Equal("[1,null,null]", KernelValueSharing.ToJson(new[] { 1.0, double.NaN, double.PositiveInfinity }, typeof(double[]), "d"));
        Assert.Equal("{\"Name\":\"a\",\"Next\":null}", KernelValueSharing.ToJson(cycle, typeof(Node), "cycle"));
        Assert.Equal("[{\"n\":1,\"label\":\"one\"},{\"n\":2,\"label\":null}]", KernelValueSharing.ToJson(table, typeof(DataTable), "table"));
        Assert.Equal("null", KernelValueSharing.ToJson(null, typeof(string), "nothing"));
    }

    [Fact]
    public void WhatIsntData_CantBeShared()
    {
        Func<int, int> function = x => x;

        Assert.Contains("is a function", Assert.Throws<KernelValueException>(() => KernelValueSharing.ToJson(function, function.GetType(), "f")).Message);
        Assert.Contains("is a stream", Assert.Throws<KernelValueException>(() => KernelValueSharing.ToJson(new MemoryStream(), typeof(Stream), "s")).Message);
        Assert.Contains("is a task", Assert.Throws<KernelValueException>(() => KernelValueSharing.ToJson(Task.CompletedTask, typeof(Task), "t")).Message);
    }

    [Theory]
    [InlineData("nums", "nums")]
    [InlineData("class", "@class")]
    [InlineData("await", "@await")]
    [InlineData("@var", "@var")]
    [InlineData("_private", "_private")]
    public void Names_BecomeCSharpNames(string name, string identifier) =>
        Assert.Equal(identifier, KernelValueSharing.Identifier(name));

    [Fact]
    public void ANameCSharpCantHave_IsExplained_WithOneItCould()
    {
        var error = Assert.Throws<KernelValueException>(() => KernelValueSharing.Identifier("my-list"));

        Assert.Contains("--as my_list", error.Message);
    }

    [Fact]
    public void GeneratedCode_NamesTypesFully() =>
        Assert.Equal("global::System.Collections.Generic.Dictionary<global::System.String, global::System.Object>", KernelValueSharing.TypeName(typeof(Dictionary<string, object>)));

    // The C# kernel, through the interface the notebook uses.
    private static INotebookKernel CSharpKernel() => new NotebookExecutionKernel();

    private static async Task<string> Run(INotebookKernel kernel, string code)
    {
        var output = new ConcurrentQueue<string>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = code, OnConsole = output.Enqueue }, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage + string.Concat(output));
        return string.Concat(output).Trim();
    }

    [Fact]
    public async Task AValue_IsDeclaredInCSharp_WithTheTypeThatFitsIt_EvenBeforeAnyCellRan()
    {
        var kernel = CSharpKernel();

        await kernel.SetValueFromJsonAsync("nums", "[1, 2, 3]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("info", "{\"name\": \"Ada\"}", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("class", "7", CancellationToken.None);

        Assert.Equal("6", await Run(kernel, "nums.Sum()"));
        Assert.Equal("\"Int32[]\"", await Run(kernel, "nums.GetType().Name")); // a string result shows in quotes
        Assert.Equal("\"Ada\"", await Run(kernel, "info[\"name\"]"));
        Assert.Equal("14", await Run(kernel, "@class * 2"));
    }

    [Fact]
    public async Task AVariableThatExists_KeepsItsType_AndGetsTheValue()
    {
        var kernel = CSharpKernel();
        await Run(kernel, "List<double> values = new() { 0.5 };\nint count = 0;");

        await kernel.SetValueFromJsonAsync("values", "[1, 2]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("count", "41", CancellationToken.None);
        var assigned = await Run(kernel, "count + 1");
        await kernel.SetValueFromJsonAsync("count", "\"not a number\"", CancellationToken.None);

        Assert.Equal("\"List`1 3\"", await Run(kernel, "$\"{values.GetType().Name} {values.Sum()}\""));
        Assert.Equal("42", assigned);
        Assert.Equal("\"not a number\"", await Run(kernel, "count")); // a value that doesn't fit is declared anew, as a string
    }

    [Fact]
    public async Task AValue_LeavesCSharp_AsJson()
    {
        var kernel = CSharpKernel();
        await Run(kernel, "var nums = new[] { 3, 1, 4 };\nvar nums2 = nums.Select(n => n * 2).ToList();\nFunc<int, int> twice = n => n * 2;");

        Assert.Equal("[3,1,4]", await kernel.GetValueJsonAsync("nums", CancellationToken.None));
        Assert.Equal("[6,2,8]", await kernel.GetValueJsonAsync("nums2", CancellationToken.None));
        Assert.Contains("is a function", (await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("twice", CancellationToken.None))).Message);
        Assert.Contains("no variable named 'missing'", (await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("missing", CancellationToken.None))).Message);
    }

    [Fact]
    public async Task ReadingAValue_BeforeAnyCSharpCellRan_SaysSo()
    {
        var error = await Assert.ThrowsAsync<KernelValueException>(() => CSharpKernel().GetValueJsonAsync("x", CancellationToken.None));

        Assert.Contains("No C# cell has run yet", error.Message);
    }
}
