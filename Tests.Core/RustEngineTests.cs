using System.Text.Json;
using CSharpEditorPlugin.Tests;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>
/// One engine for the class: its crate cache (the <c>fry</c> display crate, built once) is shared, each test gets its own kernel, as
/// each notebook does. Real <c>cargo</c> builds every cell.
/// </summary>
public sealed class RustFixture : IDisposable
{
    public RustFixture()
    {
        Folder = new TempFolder();
        Engine = new RustEngine(LanguageEngineServices.Create(Folder.Path));
    }

    public TempFolder Folder { get; }

    public RustEngine Engine { get; }

    public void Dispose() => Folder.Dispose();
}

/// <summary>Rust cells run by the real toolchain, through the engine a headless host has (no Avalonia).</summary>
public class RustEngineTests(RustFixture fixture) : IClassFixture<RustFixture>, IDisposable
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

    [RealRustFact]
    public async Task WhatACellMakes_IsThereForTheNextCell()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "#[derive(Debug)]\nstruct Person { name: String, age: u32 }\nfn double(n: i32) -> i32 { n * 2 }\nlet x = 21;\nlet mut names = vec![\"Ada\".to_string()];");

        var ran = await MustRunAsync(kernel, "names.push(\"Bob\".to_string());\nlet p = Person { name: names[1].clone(), age: 36 };\nprintln!(\"{} is {}, {}\", p.name, p.age, double(x));");

        Assert.Equal("Bob is 36, 42\n", ran.Console);
        Assert.Equal("2\n", (await MustRunAsync(kernel, "names.len()")).Console);
    }

    [RealRustFact]
    public async Task ShowingAVariableByItsName_DoesNotMoveItAwayFromTheNextCell()
    {
        var kernel = NewKernel();

        var first = await MustRunAsync(kernel, "let words = vec![\"a\".to_string(), \"b\".to_string()];\nlet name = String::from(\"Ferris\");\nname");

        Assert.Equal("\"Ferris\"\n", first.Console);
        Assert.DoesNotContain("isn't kept", first.Console);
        Assert.Equal("2 6\n", (await MustRunAsync(kernel, "println!(\"{} {}\", words.len(), name.len());")).Console);
    }

    [RealRustFact]
    public async Task TheLastExpression_IsText_ForAScalarAShortListAndAnEmptyList()
    {
        var kernel = NewKernel();

        Assert.Equal("42\n", (await MustRunAsync(kernel, "let x = 6;\nx * 7")).Console);
        Assert.Equal("\"Ferris\"\n", (await MustRunAsync(kernel, "String::from(\"Ferris\")")).Console);
        Assert.Equal("Some(3)\n", (await MustRunAsync(kernel, "Some(3)")).Console);
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync(kernel, "vec![1, 2, 3]")).Console);
        Assert.Equal("[ ]\n", (await MustRunAsync(kernel, "Vec::<i32>::new()")).Console);

        var nothing = await MustRunAsync(kernel, "println!(\"only this\")");
        Assert.Equal("only this\n", nothing.Console);
        Assert.Empty(nothing.Rich);
    }

    [RealRustFact]
    public async Task ADeepValue_IsListedFourLevelsDown_AndTheFifthLevelIsATitle()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "#[derive(Debug)]\nstruct E { v: i32 }\n#[derive(Debug)]\nstruct D { v: i32, next: E }\n#[derive(Debug)]\nstruct C { v: i32, next: D }\n#[derive(Debug)]\nstruct B { v: i32, next: C }\n#[derive(Debug)]\nstruct A { v: i32, next: B }");

        var shown = Assert.Single((await MustRunAsync(kernel, "A { v: 1, next: B { v: 2, next: C { v: 3, next: D { v: 4, next: E { v: 5 } } } } }")).Rich);

        InspectorChain.AssertFiveLevels(shown.InspectorNode!, "next", ["v", "next"], cycle: false);
    }

    [RealRustFact]
    public async Task AListOfStructs_IsATable_AStructATupleAndAnEnumWithData_AnObjectExplorer()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "#[derive(Debug)]\nstruct Point { x: i32, y: i32 }\n#[derive(Debug)]\nstruct Person { name: String, tags: Vec<String>, home: Point }\n#[derive(Debug)]\nenum Shape { Circle(f64), Rect { w: f64, h: f64 }, Unit }");

        var table = Assert.Single((await MustRunAsync(kernel, "vec![Point { x: 1, y: 2 }, Point { x: 3, y: 4 }]")).Rich).TableResult!;
        Assert.Equal(["x", "y"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["1", "3"], table.Rows.Select(r => r.Cells[0].DisplayText));

        var person = Assert.Single((await MustRunAsync(kernel, "Person { name: \"Ada\".into(), tags: vec![\"a\".into(), \"b\".into()], home: Point { x: 1, y: 2 } }")).Rich).InspectorNode!;
        Assert.Equal("Person", person.HeaderTitle);
        Assert.Equal(["name", "tags", "home"], person.Properties.Select(p => p.Name));
        Assert.Equal("Ada", person.Properties[0].SimpleValueText);
        Assert.Equal(["[0]", "[1]"], person.Properties[1].ChildNode!.Properties.Select(p => p.Name));
        Assert.Equal(["x", "y"], person.Properties[2].ChildNode!.Properties.Select(p => p.Name));

        Assert.Equal("Rect", Assert.Single((await MustRunAsync(kernel, "Shape::Rect { w: 1.0, h: 2.0 }")).Rich).InspectorNode!.HeaderTitle);
        Assert.Equal("Tuple", Assert.Single((await MustRunAsync(kernel, "(1, \"two\", 3.0)")).Rich).InspectorNode!.HeaderTitle);

        // A variant with nothing in it is a name, as a number is: text.
        var unit = await MustRunAsync(kernel, "Shape::Unit");
        Assert.Equal("Unit\n", unit.Console);
        Assert.Empty(unit.Rich);
    }

    [RealRustFact]
    public async Task Markdown_IsAMarkdownOutput()
    {
        var ran = await MustRunAsync(NewKernel(), "fry::markdown(\"# Title\\n\\nsome **text**\");");

        Assert.Contains("# Title", Assert.Single(ran.Rich).MarkdownSource);
    }

    [RealRustFact]
    public async Task AChart_IsAChartOutput()
    {
        var ran = await MustRunAsync(NewKernel(), "fry::line_chart(&vec![3.0, 1.0, 4.0]).title(\"Numbers\").show();");

        Assert.Equal(CellOutputKind.Chart, Assert.Single(ran.Rich).Kind);
    }

    [RealRustFact]
    public async Task WhatTheProgramPrints_ComesInTheOrderItWasPrinted_StderrAmongIt()
    {
        var ran = await MustRunAsync(NewKernel(), "println!(\"a\");\neprintln!(\"to stderr\");\nprintln!();\nprintln!(\"héllo ✓ 日本\");");

        Assert.Equal("a\nto stderr\n\nhéllo ✓ 日本\n", ran.Console);
    }

    [RealRustFact]
    public async Task ACompileError_IsOnTheCellsOwnLine_InTheDiagnosticsAndInTheText()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "fn helper() -> i32 { 1 }");

        var ran = await RunAsync(kernel, "let ok = helper();\n\nlet wrong: i32 = \"three\";\nprintln!(\"{} {}\", ok, wrong);");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal("E0308", error.Id);
        Assert.Equal(3, error.Line);
        Assert.Equal(18, error.Column);
        var shown = ran.Result.ConsoleOutput;
        Assert.Contains("--> cell:3:18", shown);
        Assert.Contains("3 | let wrong: i32", shown);
        Assert.DoesNotContain("cell.rs", shown);
        Assert.DoesNotContain("cell_", shown);
    }

    [RealRustFact]
    public async Task APanic_NamesTheCellsLine_EndsTheCellAndLeavesTheNextOneWorking()
    {
        var kernel = NewKernel();

        var ran = await RunAsync(kernel, "let v: Vec<i32> = Vec::new();\nprintln!(\"before\");\nprintln!(\"{}\", v[3]);");

        Assert.False(ran.Result.Success);
        Assert.Contains("before\n", ran.Console);
        Assert.Contains("index out of bounds", ran.Console);
        Assert.Contains("panicked at cell:3:", ran.Console);
        Assert.DoesNotContain("cell.rs", ran.Console);
        Assert.Contains(ran.Result.Diagnostics, d => d.Line == 3);
        Assert.Equal("still here\n", (await MustRunAsync(kernel, "println!(\"still here\");")).Console);
    }

    [RealRustFact]
    public async Task AnErrorFromAnEarlierCellsCode_SaysWhereItIs()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "fn fails() { let v: Vec<i32> = Vec::new(); println!(\"{}\", v[1]); }");

        var ran = await RunAsync(kernel, "fails();");

        Assert.False(ran.Result.Success);
        Assert.Contains("panicked at", ran.Console);
        Assert.DoesNotContain("cell.rs", ran.Console);
    }

    [RealRustFact]
    public async Task FryInput_AsksTheNotebook_AndNoAnswerIsTheEndOfInput()
    {
        var kernel = NewKernel();
        var prompts = new List<string>();
        var answers = new Queue<string?>(["Ada", null]);

        var ran = await RunAsync(kernel, "let a = fry::input(\"Name? \");\nlet b = fry::input(\"Again? \");\nprintln!(\"{:?} {:?}\", a, b);", (prompt, password) =>
        {
            prompts.Add(prompt + (password ? " (secret)" : string.Empty));
            return answers.Dequeue();
        });

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        Assert.Equal(["Name? ", "Again? "], prompts);
        Assert.Equal("Some(\"Ada\") None\n", ran.Console);
    }

    [RealRustFact]
    public async Task FryPassword_AsksForASecret()
    {
        var secret = false;

        var ran = await RunAsync(NewKernel(), "let p = fry::password(\"Key: \").unwrap();\nprintln!(\"{}\", p.len());", (_, password) =>
        {
            secret = password;
            return "hunter2";
        });

        Assert.True(secret);
        Assert.Equal("7\n", ran.Console);
    }

    [RealRustFact]
    public async Task APlainStdinRead_SeesTheEnd_InsteadOfWaitingForever()
    {
        var ran = await MustRunAsync(NewKernel(), "let mut line = String::new();\nlet n = std::io::stdin().read_line(&mut line)?;\nprintln!(\"read {} bytes\", n);");

        Assert.Equal("read 0 bytes\n", ran.Console);
    }

    [RealRustFact]
    public async Task AValueIsSharedOut_AndAValueFromAnotherLanguageIsThereAsAVariable()
    {
        var kernel = NewKernel();

        var shared = await MustRunAsync(kernel, "let scores = vec![90, 85, 77];\nfry::share!(scores);");
        Assert.DoesNotContain("__FRY_SHARE__", shared.Console);
        Assert.Equal("[90,85,77]", await kernel.GetValueJsonAsync("scores", CancellationToken.None));

        await kernel.SetValueFromJsonAsync("config", "{\"name\":\"fry\",\"sizes\":[1,2.5],\"on\":true}", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("nums", "[1,2,3,4]", CancellationToken.None);
        var ran = await MustRunAsync(kernel, "println!(\"{} {} {}\", config[\"name\"].as_str().unwrap(), nums.iter().sum::<i64>(), scores.iter().sum::<i64>());");
        Assert.Equal("fry 10 252\n", ran.Console);

        await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("nothing_like_it", CancellationToken.None));
    }

    [RealRustFact]
    public async Task AStructShared_ArrivesAsAnObject()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "#[derive(Debug)]\nstruct Point { x: i32, y: i32 }\nlet p = Point { x: 1, y: 2 };\nfry::share!(p);");

        using var json = JsonDocument.Parse(await kernel.GetValueJsonAsync("p", CancellationToken.None));

        Assert.Equal(1, json.RootElement.GetProperty("x").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("y").GetInt32());
    }

    [RealRustFact]
    public async Task TheKernelsVariables_AreWhatTheCellsMade_AndARestartForgetsThem()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "fn add(a: i32, b: i32) -> i32 { a + b }\nlet answer = 42;");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Contains(variables, v => v.Name == "add" && v.Kind == "Function");
        Assert.Contains(variables, v => v.Name == "answer" && v.TypeName == "i32" && v.ValueDisplay == "42i32");

        kernel.HardReset();

        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
        Assert.False((await RunAsync(kernel, "answer")).Result.Success);
    }

    [RealRustFact]
    public async Task Stop_EndsTheRunningProgram_AndWhatTheCellsMadeBeforeIsKept()
    {
        var kernel = NewKernel();
        await MustRunAsync(kernel, "let kept = 7;");
        using var cts = new CancellationTokenSource();
        var started = false;

        var running = kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "println!(\"started\");\nloop { std::thread::sleep(std::time::Duration::from_millis(50)); }",
            OnConsole = text => { if (text.Contains("started")) started = true; }
        }, cts.Token);
        Assert.True(SpinWait.SpinUntil(() => started, TimeSpan.FromMinutes(2)), "the program never started");
        cts.Cancel();
        var stopped = await running;

        Assert.True(stopped.WasCancelled);
        Assert.Equal("7\n", (await MustRunAsync(kernel, "kept")).Console);
    }

    [RealRustFact]
    public async Task AWholeProgram_IsRunAsItIs()
    {
        var ran = await MustRunAsync(NewKernel(), "fn greet() -> &'static str { \"whole program\" }\n\nfn main() {\n    println!(\"{}\", greet());\n}");

        Assert.Equal("whole program\n", ran.Console);
    }

    [RealRustNetworkFact]
    public async Task ACrateNamedInAComment_IsFetched_AndUsedByThisAndLaterCells()
    {
        var kernel = NewKernel();

        var first = await MustRunAsync(kernel, "// #crate: itoa = \"1\"\nlet mut buffer = itoa::Buffer::new();\nprintln!(\"{}\", buffer.format(12345));");
        var next = await MustRunAsync(kernel, "let mut buffer = itoa::Buffer::new();\nprintln!(\"{}\", buffer.format(678));");

        Assert.Equal("12345\n", first.Console);
        Assert.Equal("678\n", next.Console);
    }
}
