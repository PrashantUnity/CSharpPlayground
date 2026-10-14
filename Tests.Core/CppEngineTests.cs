using System.Text.Json;
using CSharpEditorPlugin.Tests;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>One engine for the class: the staged headers are written once; each test gets its own kernel, as each notebook does.</summary>
public sealed class CppFixture : IDisposable
{
    public CppFixture()
    {
        Folder = new TempFolder();
        Engine = new CppEngine(LanguageEngineServices.Create(Folder.Path));
    }

    public TempFolder Folder { get; }

    public CppEngine Engine { get; }

    public void Dispose() => Folder.Dispose();
}

/// <summary>C++ cells run by a real compiler, through the engine a headless host has (no Avalonia).</summary>
public class CppEngineTests(CppFixture fixture) : IClassFixture<CppFixture>, IDisposable
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
        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Result.ConsoleOutput + ran.Console);
        return ran;
    }

    [RealCppFact]
    public async Task WhatACellMakes_IsThereForTheNextCell()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "struct Person {\n    std::string name;\n    int age;\n};\n\nint double_it(int n) { return n * 2; }\n\nint x = 21;\nstd::vector<std::string> names{\"Ada\"};");

        var ran = await MustRunAsync(kernel, "names.push_back(\"Bob\");\nPerson p{names[1], 36};\nstd::cout << p.name << \" is \" << p.age << \", \" << double_it(x) << \"\\n\";");

        Assert.Equal("Bob is 36, 42\n", ran.Console);
        Assert.Equal("2\n", (await MustRunAsync(kernel, "names.size()")).Console);
        Assert.Equal("\"Bob\"\n", (await MustRunAsync(kernel, "p.name")).Console);
        Assert.Equal("Person", Assert.Single((await MustRunAsync(kernel, "p")).Rich).InspectorNode!.HeaderTitle);
    }

    [RealCppFact]
    public async Task AStaticVariable_KeepsWhatLaterCellsDoToIt_AndAFunctionSeesIt()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "static int counter = 10;\n\nvoid bump() { counter++; }");
        await MustRunAsync(kernel, "bump();\ncounter += 5;");

        Assert.Equal("16\n", (await MustRunAsync(kernel, "counter")).Console);
    }

    [RealCppFact]
    public async Task ADeclarationWrittenAgain_TakesTheOldOnesPlace_AndOverloadsLiveTogether()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "std::string greet() { return \"first\"; }\nint n = 1;\nint f(int a) { return a; }");

        await MustRunAsync(kernel, "std::string greet() { return \"second\"; }\nint n = 2;\ndouble f(double a) { return a * 2; }");

        Assert.Equal("\"second\"\n", (await MustRunAsync(kernel, "greet()")).Console);
        Assert.Equal("2\n", (await MustRunAsync(kernel, "n")).Console);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "f(7)")).Console);
        Assert.Equal("3\n", (await MustRunAsync(kernel, "f(1.5)")).Console);
    }

    [RealCppFact]
    public async Task TheLastExpression_IsText_ForAScalarAShortListAnEmptyListAndACallThatReturnsNothing()
    {
        var kernel = NewKernel();

        Assert.Equal("42\n", (await MustRunAsync(kernel, "int x = 6;\nx * 7")).Console);
        Assert.Equal("\"Ferris\"\n", (await MustRunAsync(kernel, "std::string(\"Ferris\")")).Console);
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync(kernel, "std::vector<int>{1, 2, 3}")).Console);
        Assert.Equal("[ ]\n", (await MustRunAsync(kernel, "std::vector<int>{}")).Console);
        Assert.Equal("3.5\n", (await MustRunAsync(kernel, "1.75 * 2")).Console);

        // a statement that prints (or ends with ;) is not a value; a call that returns nothing is run as it is
        var printed = await MustRunAsync(kernel, "std::cout << \"only this\"");
        Assert.Equal("only this", printed.Console);
        Assert.Empty(printed.Rich);
        Assert.Equal(string.Empty, (await MustRunAsync(kernel, "x * 7;")).Console);
        await MustRunAsync(kernel, "void hello() { std::cout << \"hi\\n\"; }");
        Assert.Equal("hi\n", (await MustRunAsync(kernel, "hello()")).Console);
    }

    [RealCppFact]
    public async Task ADeepValue_IsListedFourLevelsDown_AndTheFifthLevelIsATitle()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "struct E { int v; };\nstruct D { int v; E next; };\nstruct C { int v; D next; };\nstruct B { int v; C next; };\nstruct A { int v; B next; };");

        var shown = Assert.Single((await MustRunAsync(kernel, "A{1, {2, {3, {4, {5}}}}}")).Rich);

        InspectorChain.AssertFiveLevels(shown.InspectorNode!, "next", ["v", "next"], cycle: false);
    }

    [RealCppFact]
    public async Task AListOfStructs_IsATable_AStructAMapAndAPair_AnObjectExplorerOrATable()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "struct Point { int x; int y; };\nstruct Person {\n    std::string name;\n    std::vector<std::string> tags;\n    Point home;\n    std::optional<int> score;\n};");

        var table = Assert.Single((await MustRunAsync(kernel, "std::vector<Point>{{1, 2}, {3, 4}}")).Rich).TableResult!;
        Assert.Equal(["x", "y"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["1", "3"], table.Rows.Select(r => r.Cells[0].DisplayText));

        var person = Assert.Single((await MustRunAsync(kernel, "Person{\"Ada\", {\"a\", \"b\"}, {1, 2}, 7}")).Rich).InspectorNode!;
        Assert.Equal("Person", person.HeaderTitle);
        Assert.Equal(["name", "tags", "home", "score"], person.Properties.Select(p => p.Name));
        Assert.Equal("Ada", person.Properties[0].SimpleValueText);
        Assert.Equal(["[0]", "[1]"], person.Properties[1].ChildNode!.Properties.Select(p => p.Name));
        Assert.Equal(["x", "y"], person.Properties[2].ChildNode!.Properties.Select(p => p.Name));
        Assert.Equal("7", person.Properties[3].SimpleValueText);

        var map = Assert.Single((await MustRunAsync(kernel, "std::map<std::string, int>{{\"b\", 2}, {\"a\", 1}}")).Rich).TableResult!;
        Assert.Equal(["key", "value"], map.Columns.Select(c => c.Header));
        Assert.Equal(["a", "b"], map.Rows.Select(r => r.Cells[0].DisplayText));

        Assert.Equal(["first", "second"], Assert.Single((await MustRunAsync(kernel, "std::make_pair(1, std::string(\"x\"))")).Rich).InspectorNode!.Properties.Select(p => p.Name));
    }

    [RealCppFact]
    public async Task Markdown_AndAChart_AreOutputs()
    {
        var kernel = NewKernel();

        Assert.Contains("# Title", Assert.Single((await MustRunAsync(kernel, "fry::markdown(\"# Title\\n\\nsome **text**\");")).Rich).MarkdownSource);
        Assert.Equal(CellOutputKind.Chart, Assert.Single((await MustRunAsync(kernel, "fry::line_chart(std::vector<double>{3, 1, 4}, \"Numbers\");")).Rich).Kind);
    }

    [RealCppFact]
    public async Task WhatTheProgramPrints_ComesInTheOrderItWasPrinted_StderrAmongIt()
    {
        var ran = await MustRunAsync(NewKernel(), "std::cout << \"a\\n\";\nstd::cerr << \"to stderr\\n\";\nstd::cout << \"b\\n\";\nprintf(\"héllo ✓ 日本\\n\");");

        Assert.Equal("a\nto stderr\nb\nhéllo ✓ 日本\n", ran.Console);
    }

    [RealCppFact]
    public async Task ACompileError_IsOnTheCellsOwnLine_InTheDiagnosticsAndInTheText()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "int helper() { return 1; }");

        var ran = await RunAsync(kernel, "int ok = helper();\n\nint wrong = \"three\";\nstd::cout << ok << wrong;");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(3, error.Line);
        var shown = ran.Result.ConsoleOutput;
        Assert.Contains("cell:3:", shown);
        Assert.DoesNotContain("cell.cpp", shown);
        Assert.Contains("3 | int wrong", shown);
    }

    [RealCppFact]
    public async Task ACompileErrorInADeclaration_IsOnItsLineToo()
    {
        var ran = await RunAsync(NewKernel(), "int f() {\n    return 1;\n}\n\nint g() { return undefined_name; }");

        Assert.False(ran.Result.Success);
        Assert.Equal(5, Assert.Single(ran.Result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Line);
    }

    [RealCppFact]
    public async Task AnUncaughtException_EndsTheCellNamingTheCause_AndTheNextCellStillWorks()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "int kept = 7;");

        var ran = await RunAsync(kernel, "std::cout << \"before\\n\";\nthrow std::runtime_error(\"boom\");");

        Assert.False(ran.Result.Success);
        Assert.Contains("before\n", ran.Console);
        Assert.Contains("boom", ran.Result.ErrorMessage);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "kept")).Console);
    }

    [RealCppFact]
    public async Task FryInput_AsksTheNotebook_AndNoAnswerIsTheEndOfInput()
    {
        var kernel = NewKernel();
        var prompts = new List<string>();
        var answers = new Queue<string?>(["Ada", null]);

        var ran = await RunAsync(kernel, "auto a = fry::input(\"Name? \");\nauto b = fry::input(\"Again? \");\nstd::cout << a.value_or(\"?\") << \" \" << b.has_value() << \"\\n\";", (prompt, password) =>
        {
            prompts.Add(prompt + (password ? " (secret)" : string.Empty));
            return answers.Dequeue();
        });

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Result.ConsoleOutput);
        Assert.Equal(["Name? ", "Again? "], prompts);
        Assert.Equal("Ada 0\n", ran.Console);
    }

    [RealCppFact]
    public async Task FryPassword_AsksForASecret_AndAPlainCinRead_SeesTheEnd()
    {
        var secret = false;
        var ran = await RunAsync(NewKernel(), "auto p = fry::password(\"Key: \");\nstd::cout << p->size() << \"\\n\";", (_, password) =>
        {
            secret = password;
            return "hunter2";
        });
        Assert.True(secret);
        Assert.Equal("7\n", ran.Console);

        var plain = await MustRunAsync(NewKernel(), "std::string line;\nbool ok = static_cast<bool>(std::getline(std::cin, line));\nstd::cout << ok << \"\\n\";");
        Assert.Equal("0\n", plain.Console);
    }

    [RealCppFact]
    public async Task AValueIsSharedOut_AndAValueFromAnotherLanguageIsThereAsATypedVariable()
    {
        var kernel = NewKernel();

        var shared = await MustRunAsync(kernel, "std::vector<int> scores{90, 85, 77};\nfry::share(\"best\", 90);");
        Assert.DoesNotContain("__FRY_SHARE__", shared.Console);
        Assert.Equal("90", await kernel.GetValueJsonAsync("best", CancellationToken.None));
        Assert.Equal("[90,85,77]", await kernel.GetValueJsonAsync("scores", CancellationToken.None)); // any variable that can be saved

        await kernel.SetValueFromJsonAsync("nums", "[1,2,3,4]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("ratio", "0.5", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("label", "\"fry\"", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("config", "{\"name\":\"fry\",\"sizes\":[1,2.5],\"on\":true}", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("rows", "[{\"n\":1},{\"n\":2}]", CancellationToken.None);
        var ran = await MustRunAsync(kernel, "long long sum = 0;\nfor (auto n : nums) sum += n;\nstd::cout << sum << \" \" << ratio * 4 << \" \" << label << \" \" << config[\"name\"].as_string() << \" \" << rows.size() << \" \" << rows[1][\"n\"].as_int() << \" \" << scores[0] << \"\\n\";");
        Assert.Equal("10 2 fry fry 2 2 90\n", ran.Console);

        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("nothing_like_it", CancellationToken.None));
        await Assert.ThrowsAsync<KernelValueException>(() => kernel.SetValueFromJsonAsync("class", "1", CancellationToken.None));
    }

    [RealCppFact]
    public async Task AVariableThatCantBeKept_SaysSoOnce_AndSaysWhyWhenALaterCellNeedsIt()
    {
        var kernel = NewKernel();

        var first = await MustRunAsync(kernel, "int arr[3] = {1, 2, 3};\nint ok = 5;");
        Assert.Contains("note: arr isn't kept for later cells", first.Console);

        var later = await RunAsync(kernel, "std::cout << arr[0];");
        Assert.False(later.Result.Success);
        Assert.Contains("arr was made in an earlier cell", later.Result.ConsoleOutput);
        Assert.Equal("5\n", (await MustRunAsync(kernel, "ok")).Console);
    }

    [RealCppFact]
    public async Task AnIncludeOfACell_IsThereForTheNext()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "#include <bitset>\nint r = 1;");

        Assert.Equal("\"00000101\"\n", (await MustRunAsync(kernel, "std::bitset<8>(5).to_string()")).Console);
    }

    [RealCppFact]
    public async Task TheKernelsVariables_AreWhatTheCellsMade_AndARestartForgetsThem()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "struct T { int a; };\nint add(int a, int b) { return a + b; }\nint answer = 42;");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Contains(variables, v => v.Name == "add" && v.Kind == "Function");
        Assert.Contains(variables, v => v.Name == "T" && v.Kind == "Type");
        Assert.Contains(variables, v => v.Name == "answer" && v.TypeName == "int" && v.ValueDisplay == "42");

        kernel.HardReset();

        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
        Assert.False((await RunAsync(kernel, "answer")).Result.Success);
    }

    [RealCppFact]
    public async Task Stop_EndsTheRunningProgram_AndWhatTheCellsMadeBeforeIsKept()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "int kept = 7;");
        using var cts = new CancellationTokenSource();
        var started = false;

        var running = kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "std::cout << \"started\\n\";\nwhile (true) { std::this_thread::sleep_for(std::chrono::milliseconds(50)); }",
            OnConsole = text => { if (text.Contains("started")) started = true; }
        }, cts.Token);
        Assert.True(SpinWait.SpinUntil(() => started, TimeSpan.FromMinutes(2)), "the program never started");
        cts.Cancel();
        var stopped = await running;

        Assert.True(stopped.WasCancelled);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "kept")).Console);
    }

    [RealCppFact]
    public async Task AWholeProgram_IsRunAsItIs()
    {
        var ran = await MustRunAsync(NewKernel(), "std::string greet() { return \"whole program\"; }\n\nint main() {\n    std::cout << greet() << \"\\n\";\n    return 0;\n}");

        Assert.Equal("whole program\n", ran.Console);
    }
}
