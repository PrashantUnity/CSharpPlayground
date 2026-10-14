using System.Text.Json;
using CSharpEditorPlugin.Tests;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>One engine for the class: its build cache (the standard library and the fry package, built once) is shared; each test gets its own kernel, as each notebook does.</summary>
public sealed class GoFixture : IDisposable
{
    public GoFixture()
    {
        Folder = new TempFolder();
        Engine = new GoEngine(LanguageEngineServices.Create(Folder.Path));
    }

    public TempFolder Folder { get; }

    public GoEngine Engine { get; }

    public void Dispose() => Folder.Dispose();
}

/// <summary>Go cells run by the real toolchain, through the engine a headless host has (no Avalonia).</summary>
public class GoEngineTests(GoFixture fixture) : IClassFixture<GoFixture>, IDisposable
{
    private readonly List<INotebookKernel> _kernels = [];

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
    }

    private INotebookKernel NewKernel()
    {
        var kernel = fixture.Engine.NotebookKernels.Create(new KernelCreationContext(() => fixture.Folder.Path));
        _kernels.Add(kernel);
        return kernel;
    }

    private sealed record Ran(KernelExecutionResult Result, List<string> Text, List<RichCellOutput> Rich)
    {
        public string Console => string.Concat(Text);
    }

    private static async Task<Ran> RunAsync(INotebookKernel kernel, string code, Func<string, bool, string?>? input = null, CancellationToken ct = default)
    {
        var text = new List<string>();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            OnConsole = text.Add,
            OnRichOutput = rich.Add,
            OnInputRequest = input is null ? null : (prompt, password, _) => Task.FromResult(input(prompt, password))
        }, ct);
        return new Ran(result, text, rich);
    }

    private static async Task<Ran> MustRunAsync(INotebookKernel kernel, string code)
    {
        var ran = await RunAsync(kernel, code);
        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        return ran;
    }

    [RealGoFact]
    public async Task WhatACellMakes_IsThereForTheNextCell()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "type Person struct {\n\tName string\n\tAge  int\n}\n\nfunc double(n int) int { return n * 2 }\n\nx := 21\nnames := []string{\"Ada\"}");

        var ran = await MustRunAsync(kernel, "names = append(names, \"Bob\")\np := Person{Name: names[1], Age: 36}\nfmt.Printf(\"%s is %d, %d\\n\", p.Name, p.Age, double(x))");

        Assert.Equal("Bob is 36, 42\n", ran.Console);
        Assert.Equal("2\n", (await MustRunAsync(kernel, "len(names)")).Console);
        Assert.Equal("\"Bob\"\n", (await MustRunAsync(kernel, "p.Name")).Console);
    }

    [RealGoFact]
    public async Task AVariableDeclaredAtTheTop_KeepsWhatLaterCellsDoToIt_AndAFunctionSeesIt()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "var counter = 10\n\nfunc bump() { counter++ }");
        await MustRunAsync(kernel, "bump()\ncounter += 5");

        Assert.Equal("16\n", (await MustRunAsync(kernel, "counter")).Console);
    }

    [RealGoFact]
    public async Task ADeclarationWrittenAgain_TakesTheOldOnesPlace()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "func greet() string { return \"first\" }\nn := 1");

        // the function replaced, and the variable made again with := (a block of its own lets it)
        await MustRunAsync(kernel, "func greet() string { return \"second\" }\nn := 2");

        Assert.Equal("\"second\"\n", (await MustRunAsync(kernel, "greet()")).Console);
        Assert.Equal("2\n", (await MustRunAsync(kernel, "n")).Console);
    }

    [RealGoFact]
    public async Task TheLastExpression_IsText_ForAScalarAShortListAnEmptyListAndAnErrorlessCall()
    {
        var kernel = NewKernel();

        Assert.Equal("42\n", (await MustRunAsync(kernel, "x := 6\nx * 7")).Console);
        Assert.Equal("\"Ferris\"\n", (await MustRunAsync(kernel, "\"Ferris\"")).Console);
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync(kernel, "[]int{1, 2, 3}")).Console);
        Assert.Equal("[ ]\n", (await MustRunAsync(kernel, "[]int{}")).Console);
        Assert.Equal("12\n", (await MustRunAsync(kernel, "strconv.Atoi(\"12\")")).Console);
        Assert.Equal("1m30s\n", (await MustRunAsync(kernel, "90 * time.Second")).Console);

        // a statement that prints is not a value; a call that returns nothing is run as it is
        var printed = await MustRunAsync(kernel, "fmt.Println(\"only this\")");
        Assert.Equal("only this\n", printed.Console);
        Assert.Empty(printed.Rich);
        await MustRunAsync(kernel, "func hello() { fmt.Println(\"hi\") }");
        Assert.Equal("hi\n", (await MustRunAsync(kernel, "hello()")).Console);
    }

    [RealGoFact]
    public async Task APointerBackToTheTop_IsListedFourLevelsDown_LikeTheDesktopShowsIt()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "type People struct {\n\tName string\n\tNumbers []int\n\tAnother *People\n}");

        var shown = Assert.Single((await MustRunAsync(kernel, "person := &People{Name: \"Prashant\", Numbers: []int{1, 34, 45}}\nperson.Another = person\nperson")).Rich);

        InspectorChain.AssertFiveLevels(shown.InspectorNode!, "Another", ["Name", "Numbers", "Another"]);
    }

    [RealGoFact]
    public async Task AListOfStructs_IsATable_AStructAMapAndAPointer_AnObjectExplorerOrATable()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "type Point struct{ X, Y int }\ntype Person struct {\n\tName string\n\tTags []string\n\tHome Point\n\tBoss *Person\n}");

        var table = Assert.Single((await MustRunAsync(kernel, "[]Point{{1, 2}, {3, 4}}")).Rich).TableResult!;
        Assert.Equal(["X", "Y"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["1", "3"], table.Rows.Select(r => r.Cells[0].DisplayText));

        var person = Assert.Single((await MustRunAsync(kernel, "Person{Name: \"Ada\", Tags: []string{\"a\", \"b\"}, Home: Point{1, 2}}")).Rich).InspectorNode!;
        Assert.Equal("Person", person.HeaderTitle);
        Assert.Equal(["Name", "Tags", "Home", "Boss"], person.Properties.Select(p => p.Name));
        Assert.Equal("Ada", person.Properties[0].SimpleValueText);
        Assert.Equal(["[0]", "[1]"], person.Properties[1].ChildNode!.Properties.Select(p => p.Name));
        Assert.Equal(["X", "Y"], person.Properties[2].ChildNode!.Properties.Select(p => p.Name));
        Assert.True(person.Properties[3].IsNull);

        Assert.Equal("Person", Assert.Single((await MustRunAsync(kernel, "&Person{Name: \"Bob\"}")).Rich).InspectorNode!.HeaderTitle);

        var map = Assert.Single((await MustRunAsync(kernel, "map[string]int{\"b\": 2, \"a\": 1}")).Rich).TableResult!;
        Assert.Equal(["key", "value"], map.Columns.Select(c => c.Header));
        Assert.Equal(["a", "b"], map.Rows.Select(r => r.Cells[0].DisplayText));
    }

    [RealGoFact]
    public async Task Markdown_AndAChart_AreOutputs()
    {
        var kernel = NewKernel();

        Assert.Contains("# Title", Assert.Single((await MustRunAsync(kernel, "fry.Markdown(\"# Title\\n\\nsome **text**\")")).Rich).MarkdownSource);
        Assert.Equal(CellOutputKind.Chart, Assert.Single((await MustRunAsync(kernel, "fry.LineChart([]float64{3, 1, 4}, fry.Title(\"Numbers\"))")).Rich).Kind);
    }

    [RealGoFact]
    public async Task WhatTheProgramPrints_ComesInTheOrderItWasPrinted_StderrAmongIt()
    {
        var ran = await MustRunAsync(NewKernel(), "fmt.Println(\"a\")\nprintln(\"to stderr\")\nfmt.Println()\nfmt.Println(\"héllo ✓ 日本\")");

        Assert.Equal("a\nto stderr\n\nhéllo ✓ 日本\n", ran.Console);
    }

    [RealGoFact]
    public async Task ACompileError_IsOnTheCellsOwnLine_InTheDiagnosticsAndInTheText()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "func helper() int { return 1 }");

        var ran = await RunAsync(kernel, "ok := helper()\n\nwrong := \"three\" + ok\nfmt.Println(ok, wrong)");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(3, error.Line);
        Assert.Contains("mismatched types", error.Message);
        var shown = ran.Result.ConsoleOutput;
        Assert.Contains("cell:3:", shown);
        Assert.DoesNotContain("main.go", shown);
        Assert.DoesNotContain("# cell", shown);
    }

    [RealGoFact]
    public async Task ACompileErrorInADeclaration_IsOnItsLineToo()
    {
        var ran = await RunAsync(NewKernel(), "func f() int {\n\treturn 1\n}\n\nvar s string = 5");

        Assert.False(ran.Result.Success);
        Assert.Equal(5, Assert.Single(ran.Result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Line);
        Assert.Contains("cell:5:", ran.Result.ConsoleOutput);
    }

    [RealGoFact]
    public async Task APanic_NamesTheCellsLine_EndsTheCellAndLeavesTheNextOneWorking()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "kept := 7");

        var ran = await RunAsync(kernel, "v := []int{}\nfmt.Println(\"before\")\nfmt.Println(v[3])");

        Assert.False(ran.Result.Success);
        Assert.Contains("before\n", ran.Console);
        Assert.Contains("index out of range", ran.Console);
        Assert.Contains("cell:3", ran.Console);
        Assert.DoesNotContain("main.go", ran.Console);
        Assert.Equal(3, Assert.Single(ran.Result.Diagnostics, d => d.Id == "PANIC").Line);
        Assert.Contains("index out of range", ran.Result.ErrorMessage);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "kept")).Console);
    }

    [RealGoFact]
    public async Task FryInput_AsksTheNotebook_AndNoAnswerIsTheEndOfInput()
    {
        var kernel = NewKernel();
        var prompts = new List<string>();
        var answers = new Queue<string?>(["Ada", null]);

        var ran = await RunAsync(kernel, "a, okA := fry.Input(\"Name? \")\nb, okB := fry.Input(\"Again? \")\nfmt.Println(a, okA, b == \"\", okB)", (prompt, password) =>
        {
            prompts.Add(prompt + (password ? " (secret)" : string.Empty));
            return answers.Dequeue();
        });

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        Assert.Equal(["Name? ", "Again? "], prompts);
        Assert.Equal("Ada true true false\n", ran.Console);
    }

    [RealGoFact]
    public async Task FryPassword_AsksForASecret_AndAPlainStdinRead_SeesTheEnd()
    {
        var secret = false;
        var ran = await RunAsync(NewKernel(), "p, _ := fry.Password(\"Key: \")\nfmt.Println(len(p))", (_, password) =>
        {
            secret = password;
            return "hunter2";
        });
        Assert.True(secret);
        Assert.Equal("7\n", ran.Console);

        var plain = await MustRunAsync(NewKernel(), "var line string\nn, err := fmt.Scanln(&line)\nfmt.Println(n, err)");
        Assert.Equal("0 EOF\n", plain.Console);
    }

    [RealGoFact]
    public async Task AValueIsSharedOut_AndAValueFromAnotherLanguageIsThereAsATypedVariable()
    {
        var kernel = NewKernel();

        var shared = await MustRunAsync(kernel, "scores := []int{90, 85, 77}\nfry.Share(\"best\", 90)");
        Assert.DoesNotContain("__FRY_SHARE__", shared.Console);
        Assert.Equal("90", await kernel.GetValueJsonAsync("best", CancellationToken.None));
        Assert.Equal("[90,85,77]", await kernel.GetValueJsonAsync("scores", CancellationToken.None)); // any variable that can be saved

        await kernel.SetValueFromJsonAsync("nums", "[1,2,3,4]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("ratio", "0.5", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("config", "{\"name\":\"fry\",\"sizes\":[1,2.5],\"on\":true}", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("rows", "[{\"n\":1},{\"n\":2}]", CancellationToken.None);
        var ran = await MustRunAsync(kernel, "sum := 0\nfor _, n := range nums {\n\tsum += n\n}\nfmt.Println(sum, ratio*4, config[\"name\"], len(rows), rows[1][\"n\"], scores[0])");
        Assert.Equal("10 2 fry 2 2 90\n", ran.Console);

        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("nothing_like_it", CancellationToken.None));
        await Assert.ThrowsAsync<KernelValueException>(() => kernel.SetValueFromJsonAsync("func", "1", CancellationToken.None));
    }

    [RealGoFact]
    public async Task AVariableThatCantBeKept_SaysSoOnce_AndSaysWhyWhenALaterCellNeedsIt()
    {
        var kernel = NewKernel();

        var first = await MustRunAsync(kernel, "type Secret struct{ hidden int }\nh := Secret{hidden: 1}\nok := 5");
        Assert.Contains("note: h isn't kept for later cells", first.Console);

        var later = await RunAsync(kernel, "fmt.Println(h)");
        Assert.False(later.Result.Success);
        Assert.Contains("h was made in an earlier cell", later.Result.ConsoleOutput);
        Assert.Equal("5\n", (await MustRunAsync(kernel, "ok")).Console);
    }

    [RealGoFact]
    public async Task AnImportOfACell_IsThereForTheNext_AndOnlyWhatIsUsedIsImported()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "import m \"math\"\nimport \"os\"\nr := 1");

        Assert.Equal("4\n", (await MustRunAsync(kernel, "m.Sqrt(16)")).Console);
    }

    [RealGoFact]
    public async Task TheKernelsVariables_AreWhatTheCellsMade_AndARestartForgetsThem()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "type T struct{ A int }\nfunc add(a, b int) int { return a + b }\nanswer := 42");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Contains(variables, v => v.Name == "add" && v.Kind == "Function");
        Assert.Contains(variables, v => v.Name == "T" && v.Kind == "Type");
        Assert.Contains(variables, v => v.Name == "answer" && v.TypeName == "int" && v.ValueDisplay == "42");

        kernel.HardReset();

        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
        Assert.False((await RunAsync(kernel, "answer")).Result.Success);
    }

    [RealGoFact]
    public async Task Stop_EndsTheRunningProgram_AndWhatTheCellsMadeBeforeIsKept()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "kept := 7");
        using var cts = new CancellationTokenSource();
        var started = false;

        var running = kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "fmt.Println(\"started\")\nfor {\n\ttime.Sleep(50 * time.Millisecond)\n}",
            OnConsole = text => { if (text.Contains("started")) started = true; }
        }, cts.Token);
        Assert.True(SpinWait.SpinUntil(() => started, TimeSpan.FromMinutes(2)), "the program never started");
        cts.Cancel();
        var stopped = await running;

        Assert.True(stopped.WasCancelled);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "kept")).Console);
    }

    [RealGoFact]
    public async Task AWholeProgram_IsRunAsItIs()
    {
        var ran = await MustRunAsync(NewKernel(), "package main\n\nfunc greet() string { return \"whole program\" }\n\nfunc main() {\n\tfmt.Println(greet())\n}");

        Assert.Equal("whole program\n", ran.Console);
    }

    [RealGoNetworkFact]
    public async Task AModuleAddedWithGoGet_IsImportedByThisAndLaterCells()
    {
        var packages = (GoPackageManager)fixture.Engine.Packages;
        var kernel = NewKernel();
        var toolchain = (await fixture.Engine.GoToolchain.ResolveAsync(new PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains.ToolchainQuery(), CancellationToken.None)).Toolchain!;

        Assert.True(packages.TryParseDirective("%go get github.com/google/uuid", out var command));
        var log = new List<string>();
        var added = await packages.RunAsync(command, toolchain, log.Add);
        Assert.True(added.Success, string.Concat(log));

        var first = await MustRunAsync(kernel, "import \"github.com/google/uuid\"\nid := uuid.New().String()\nlen(id)");
        var next = await MustRunAsync(kernel, "len(uuid.NewString())");

        Assert.Equal("36\n", first.Console);
        Assert.Equal("36\n", next.Console);
    }
}
