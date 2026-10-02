using System.Text;
using System.Text.Json;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealRust;

/// <summary>Rust notebook cells built with the real Cargo and run: items kept between cells, shown values, sharing, diagnostics and stopping.</summary>
[Collection(RealRustCollection.Name)]
public class RustNotebookTests : IClassFixture<RustStudioFixture>, IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly RustStudioFixture _studio;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustNotebook_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public RustNotebookTests(RustStudioFixture studio)
    {
        _studio = studio;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private INotebookKernel Kernel()
    {
        TestRust.Require();
        var language = _studio.Services.Registry.Get(LanguageIds.Rust)!;
        var kernel = language.NotebookKernels!.Create(new KernelCreationContext(() => _dir));
        _kernels.Add(kernel);
        return kernel;
    }

    private static async Task<(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich)> Run(INotebookKernel kernel, string code, CancellationToken ct = default)
    {
        var console = new StringBuilder();
        var rich = new List<RichCellOutput>();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(ct);
        patience.CancelAfter(Patience);
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            OnConsole = text => { lock (console) console.Append(text); },
            OnRichOutput = rich.Add
        }, patience.Token);
        return (result, console.ToString(), rich);
    }

    // Crates come from the network; a machine without it can't run those tests.
    private static bool LooksOffline(string text) =>
        new[] { "Could not resolve host", "failed to download", "spurious network error", "Unable to update registry", "failed to get", "timed out", "Network is unreachable" }
            .Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    [RustFact]
    public async Task ACell_IsBuiltAndRun_WithItsOutput()
    {
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "println!(\"hello from a rust cell\");");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("hello from a rust cell", console);
    }

    [RustFact]
    public async Task AFunctionAndAType_DefinedInOneCell_AreUsedInTheNext()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "use std::collections::HashMap;\n\n#[derive(Debug)]\nstruct Point { x: i32, y: i32 }\n\nimpl Point {\n    fn norm2(&self) -> i32 { self.x * self.x + self.y * self.y }\n}\n\nfn double(n: i32) -> i32 { n * 2 }");
        var (second, console, _) = await Run(kernel, "let p = Point { x: double(3), y: 8 };\nlet mut counts: HashMap<&str, i32> = HashMap::new();\ncounts.insert(\"n\", p.norm2());\nprintln!(\"{:?} {}\", p, counts[\"n\"]);");

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("Point { x: 6, y: 8 } 100", console);
    }

    [RustFact]
    public async Task AVariableMadeWithLet_IsThereInTheNextCell_WithItsTypeAndAnyChangesToIt()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "let small: u8 = 250;\nlet name = String::from(\"fry\");\nlet mut scores = vec![90, 85];\nscores.push(77);\nlet pair = (1.5, 'x');\nlet maybe: Option<i64> = Some(-3);");
        var (second, console, _) = await Run(kernel, "let bigger = small + 5;\nscores.push(60);\nprintln!(\"{} {} {} {:?} {:?} {:?}\", name, bigger, scores.len(), pair, maybe, scores);");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("fry 255 4 (1.5, 'x') Some(-3) [90, 85, 77, 60]", console);

        // The change one cell made is still there in the third.
        var (third, thirdConsole, _) = await Run(kernel, "println!(\"{}\", scores.len());");
        Assert.True(third.Success, third.ErrorMessage + third.ConsoleOutput);
        Assert.Contains("4", thirdConsole);
    }

    [RustFact]
    public async Task MapsSetsTuplesAndEmptyListsAreKept()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "use std::collections::{BTreeMap, HashSet};\nlet mut ages: BTreeMap<String, u32> = BTreeMap::new();\nages.insert(\"ada\".to_string(), 36);\nlet seen: HashSet<char> = \"hello\".chars().collect();\nlet empty: Vec<String> = Vec::new();\nlet (a, mut b) = (1u16, 2u16);");
        var (second, console, _) = await Run(kernel, "b += a;\nprintln!(\"{:?} {} {} {} {}\", ages, seen.len(), seen.contains(&'l'), empty.len(), b);");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("{\"ada\": 36} 4 true 0 3", console);
    }

    [RustFact]
    public async Task AVariableThatWasMovedAway_DoesNotBreakItsCell_AndIsNotKept()
    {
        var kernel = Kernel();

        var (first, console, _) = await Run(kernel, "let text = String::from(\"moved\");\nlet taken = text;\nprintln!(\"{}\", taken);");
        var (second, secondConsole, _) = await Run(kernel, "println!(\"{}\", taken);");
        var (third, _, _) = await Run(kernel, "println!(\"{}\", text);");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.Contains("moved", console);
        Assert.Contains("isn't kept", console);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("moved", secondConsole);
        Assert.False(third.Success);
        Assert.Contains("cannot find value `text`", third.ConsoleOutput);
    }

    [RustFact]
    public async Task AVariableOfATypeThatCantBeKept_SaysSoWhenALaterCellNeedsIt()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "#[derive(Debug)]\nstruct Point { x: i32 }\nlet p = Point { x: 1 };\nlet n = 5;");
        var (second, console, _) = await Run(kernel, "println!(\"{} {:?}\", n, p);");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.False(second.Success);
        Assert.Contains("`p` was made with let in an earlier cell", second.ConsoleOutput);
    }

    [RustFact]
    public async Task ALetThatShadowsAnEarlierOne_ReplacesIt_AndACellThatFailsKeepsTheOldValues()
    {
        var kernel = Kernel();
        await Run(kernel, "let x = 1;");
        await Run(kernel, "let x = \"now text\".to_string();");
        var (failed, _, _) = await Run(kernel, "let x = 99;\nlet oops: i32 = \"no\";");
        var (result, console, _) = await Run(kernel, "println!(\"{}\", x);");

        Assert.False(failed.Success);
        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("now text", console);
    }

    [RustFact]
    public async Task ARedefinedFunction_TakesEffect_AndAStaleImplIsDropped()
    {
        var kernel = Kernel();
        await Run(kernel, "fn value() -> i32 { 1 }");
        await Run(kernel, "fn value() -> i32 { 2 }");

        var (result, console, _) = await Run(kernel, "println!(\"{}\", value());");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("2", console);

        await Run(kernel, "struct Cell { v: i32 }\nimpl Cell { fn get(&self) -> i32 { self.v } }");
        var (redefined, _, _) = await Run(kernel, "struct Cell { v: i32, w: i32 }\nimpl Cell { fn total(&self) -> i32 { self.v + self.w } }");
        var (use, useConsole, _) = await Run(kernel, "println!(\"{}\", Cell { v: 1, w: 2 }.total());");

        Assert.True(redefined.Success, redefined.ErrorMessage + redefined.ConsoleOutput);
        Assert.True(use.Success, use.ErrorMessage + use.ConsoleOutput);
        Assert.Contains("3", useConsole);
    }

    [RustFact]
    public async Task TheLastExpression_IsShown_AsANotebookDoes()
    {
        var kernel = Kernel();

        var (number, numberConsole, numberRich) = await Run(kernel, "let x = 6;\nx * 7");
        var (text, textConsole, textRich) = await Run(kernel, "String::from(\"Ferris\")");
        var (option, optionConsole, _) = await Run(kernel, "Some(3)");
        var (list, _, listRich) = await Run(kernel, "vec![(1, \"one\"), (2, \"two\")]");
        var (unit, unitConsole, unitRich) = await Run(kernel, "println!(\"no value\")");

        // A number, a string or a Some(3) is text, as in a REPL; a list is a table; a value that is nothing is not shown.
        Assert.True(number.Success, number.ErrorMessage + number.ConsoleOutput);
        Assert.Equal("42", numberConsole.Trim());
        Assert.Empty(numberRich);
        Assert.Equal("\"Ferris\"", textConsole.Trim());
        Assert.Empty(textRich);
        Assert.Equal("Some(3)", optionConsole.Trim());
        Assert.Equal(CellOutputKind.Table, Assert.Single(listRich).Kind);
        Assert.Equal(2, listRich[0].TableResult!.Rows.Count);
        Assert.True(unit.Success);
        Assert.Equal("no value", unitConsole.Trim());
        Assert.Empty(unitRich);
        Assert.True(text.Success && option.Success && list.Success);
    }

    [RustFact]
    public async Task AValueWithoutDebug_IsNotAnError_WhenItIsTheLastExpression()
    {
        var kernel = Kernel();

        var (result, _, rich) = await Run(kernel, "struct Opaque(i32);\nOpaque(1)");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Empty(rich);
    }

    [RustFact]
    public async Task TheQuestionMarkOperator_WorksInACell_AndAnErrorEndsTheCellWithItsMessage()
    {
        var kernel = Kernel();

        var (ok, console, _) = await Run(kernel, "let n: i32 = \"42\".parse()?;\nprintln!(\"{}\", n + 1);");
        var (failed, failedConsole, _) = await Run(kernel, "let n: i32 = \"forty-two\".parse()?;\nprintln!(\"{}\", n);");

        Assert.True(ok.Success, ok.ErrorMessage + ok.ConsoleOutput);
        Assert.Contains("43", console);
        Assert.False(failed.Success);
        Assert.Contains("ParseIntError", failedConsole);
    }

    [RustFact]
    public async Task ACompileError_IsOnTheLineAndColumnOfTheCell()
    {
        var kernel = Kernel();
        await Run(kernel, "fn helper() -> i32 { 1 }");

        var (result, console, _) = await Run(kernel, "let ok = helper();\n\nlet wrong: i32 = \"three\";\nprintln!(\"{} {}\", ok, wrong);");

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal("E0308", error.Id);
        Assert.Equal(3, error.Line);
        Assert.Equal(18, error.Column); // the opening quote of "three", counting from 1
        Assert.Contains("mismatched types", console);
        Assert.DoesNotContain("Compiling", console);
    }

    [RustFact]
    public async Task APanic_IsAFailureWithItsLine_AndTheCellsThatFollowStillRun()
    {
        var kernel = Kernel();

        var (panicked, console, _) = await Run(kernel, "let v: Vec<i32> = Vec::new();\nprintln!(\"before\");\nprintln!(\"{}\", v[3]);");
        var (after, afterConsole, _) = await Run(kernel, "println!(\"still here\");");

        Assert.False(panicked.Success);
        Assert.Contains("101", panicked.ErrorMessage);
        Assert.Contains("before", console);
        Assert.Contains("index out of bounds", console);
        Assert.Contains(panicked.Diagnostics, d => d.Line == 3);
        Assert.True(after.Success);
        Assert.Contains("still here", afterConsole);
    }

    [RustFact]
    public async Task AValueSharedByACell_IsKeptForLaterCells_AndForOtherKernels()
    {
        var kernel = Kernel();

        var (shared, sharedConsole, _) = await Run(kernel, "let scores = vec![90, 85, 77];\nlet best = scores.iter().max().unwrap().clone();\nfry::share!(scores);\nfry::share!(best, \"top\");");

        Assert.True(shared.Success, shared.ErrorMessage + shared.ConsoleOutput);
        Assert.DoesNotContain("__FRY_SHARE__", sharedConsole);
        Assert.Equal("[90,85,77]", await kernel.GetValueJsonAsync("scores", CancellationToken.None));
        Assert.Equal("90", await kernel.GetValueJsonAsync("top", CancellationToken.None));

        var (next, console, _) = await Run(kernel, "println!(\"{} {}\", scores.iter().sum::<i64>(), top + 1);");
        Assert.True(next.Success, next.ErrorMessage + next.ConsoleOutput);
        Assert.Contains("252 91", console);
    }

    [RustFact]
    public async Task ValuesSharedFromOtherKernels_ArriveAsTypedVariables()
    {
        var kernel = Kernel();
        await kernel.SetValueFromJsonAsync("nums", "[1,2,3,4]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("ratio", "0.5", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("label", "\"a \\\"quoted\\\" line\\nnext\"", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("grid", "[[1,2],[3,4]]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("config", "{\"name\":\"fry\",\"sizes\":[1,2.5],\"on\":true,\"none\":null}", CancellationToken.None);

        var (result, console, _) = await Run(kernel, "println!(\"{}\", nums.iter().sum::<i64>());\nprintln!(\"{}\", ratio * 4.0);\nprintln!(\"{:?}\", label);\nprintln!(\"{}\", grid[1][0]);\nprintln!(\"{} {:?} {:?} {}\", config[\"name\"].as_str().unwrap(), config[\"sizes\"][1].as_f64(), config[\"on\"].as_bool(), config[\"none\"].is_null());");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("10", console);
        Assert.Contains("2", console);
        Assert.Contains("\"a \\\"quoted\\\" line\\nnext\"", console);
        Assert.Contains("3", console);
        Assert.Contains("fry Some(2.5) Some(true) true", console);
    }

    [RustFact]
    public async Task ADirectiveKeywordAsAName_AndAStructShared_RoundTrip()
    {
        var kernel = Kernel();
        await kernel.SetValueFromJsonAsync("type", "7", CancellationToken.None);

        var (result, console, _) = await Run(kernel, "#[derive(Debug)]\nstruct Point { x: i32, y: i32 }\nlet p = Point { x: 1, y: 2 };\nfry::share!(p);\nprintln!(\"{}\", r#type);");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("7", console);
        using var json = JsonDocument.Parse(await kernel.GetValueJsonAsync("p", CancellationToken.None));
        Assert.Equal(1, json.RootElement.GetProperty("x").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("y").GetInt32());
    }

    [RustFact]
    public async Task ACellThatReadsInput_SeesTheEnd_InsteadOfWaitingForever()
    {
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "let mut line = String::new();\nlet n = std::io::stdin().read_line(&mut line)?;\nprintln!(\"read {} bytes\", n);");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("read 0 bytes", console);
    }

    [RustFact]
    public async Task ACellThatIsAWholeProgram_IsRunAsItIs()
    {
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "fn greet() -> &'static str { \"whole program\" }\n\nfn main() {\n    println!(\"{}\", greet());\n}");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("whole program", console);
    }

    [RustFact]
    public async Task StoppingALongCell_EndsItsProgram_AndTheKernelWorksAfterwards()
    {
        var kernel = Kernel();
        using var cts = new CancellationTokenSource();

        var running = Run(kernel, "println!(\"started\");\nloop { std::thread::sleep(std::time::Duration::from_millis(50)); }", cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(12));
        cts.Cancel();
        var (stopped, _, _) = await running;
        var (after, console, _) = await Run(kernel, "println!(\"alive\");");

        Assert.True(stopped.WasCancelled);
        Assert.True(after.Success, after.ErrorMessage);
        Assert.Contains("alive", console);
    }

    [RustFact]
    public async Task ACrateNamedInAComment_IsFetchedAndUsed_ByThisAndLaterCells()
    {
        var kernel = Kernel();

        var (first, firstConsole, _) = await Run(kernel, "// #crate: itoa = \"1\"\nuse itoa::Buffer;\n\nfn as_text(n: i64) -> String { Buffer::new().format(n).to_string() }\nprintln!(\"{}\", as_text(12345));");
        if (!first.Success && LooksOffline(first.ConsoleOutput + firstConsole)) return;

        var (second, console, _) = await Run(kernel, "println!(\"{}\", as_text(-7));");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.Contains("12345", firstConsole);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("-7", console);
    }

    [RustFact]
    public async Task AMissingCrate_IsReported_SoTheCellCanOfferToInstallIt()
    {
        var kernel = Kernel();

        var (result, _, _) = await Run(kernel, "use fastrand_that_does_not_exist::Rng;");

        Assert.False(result.Success);
        Assert.Equal("fastrand_that_does_not_exist", result.MissingDependency);
    }

    [RustFact]
    public async Task TheKernelsVariables_ListWhatTheCellsDefined()
    {
        var kernel = Kernel();
        await Run(kernel, "fn add(a: i32, b: i32) -> i32 { a + b }\nstruct Point { x: i32 }");
        await kernel.SetValueFromJsonAsync("n", "5", CancellationToken.None);

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Contains(variables, v => v.Name == "add" && v.Kind == "Function");
        Assert.Contains(variables, v => v.Name == "Point" && v.Kind == "Type");
        Assert.Contains(variables, v => v.Name == "n" && v.TypeName == "i64" && v.ValueDisplay == "5");
    }

    [RustFact]
    public async Task AMacroThatMakesImpls_IsKept_AndACodeMacroStillRuns()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "trait Answer {\n    fn answer(&self) -> i32;\n}\n\nmacro_rules! make_answers {\n    ($($t:ty),*) => { $(impl Answer for $t { fn answer(&self) -> i32 { 42 } })* };\n}\n\nmake_answers!(i32, u8);");
        var (second, console, _) = await Run(kernel, "println!(\"{} {}\", 5i32.answer(), 7u8.answer());");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("42 42", console);
    }

    [RustFact]
    public async Task AThreadLocal_DeclaredInOneCell_IsThereInTheNext()
    {
        var kernel = Kernel();

        var (first, _, _) = await Run(kernel, "use std::cell::RefCell;\n\nthread_local! {\n    static COUNTER: RefCell<i32> = RefCell::new(10);\n}");
        var (second, console, _) = await Run(kernel, "COUNTER.with(|c| *c.borrow_mut() += 5);\nprintln!(\"{}\", COUNTER.with(|c| *c.borrow()));");

        Assert.True(first.Success, first.ErrorMessage + first.ConsoleOutput);
        Assert.True(second.Success, second.ErrorMessage + second.ConsoleOutput);
        Assert.Contains("15", console);
    }

    [RustFact]
    public async Task AnUnknownMacroThatIsJustCode_StillRuns_WithoutAnErrorFromTheFirstTry()
    {
        var kernel = Kernel();
        await Run(kernel, "macro_rules! twice {\n    ($e:expr) => { println!(\"{}\", $e * 2) };\n}");

        var (result, console, _) = await Run(kernel, "twice!(21);");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Equal("42", console.Trim());
    }

    [RustFact]
    public async Task ACrateAddedWithCargoAdd_IsThereForTheNextCell_WithoutAComment()
    {
        // What the notebook does with "%cargo add itoa": the package manager is run first, and the cell's code follows.
        var language = _studio.Services.Registry.Get(LanguageIds.Rust)!;
        Assert.True(language.Packages!.TryParseDirective("%cargo add itoa", out var command));
        var installed = new StringBuilder();

        var added = await language.Packages.RunAsync(command, TestRust.Require(), text => installed.Append(text)).WaitAsync(Patience);
        if (!added.Success && LooksOffline(installed.ToString() + added.Message)) return;
        Assert.True(added.Success, added.Message + installed);

        var kernel = Kernel();
        var (result, console, _) = await Run(kernel, "use itoa::Buffer;\nprintln!(\"{}\", Buffer::new().format(99u32));");

        Assert.True(result.Success, result.ErrorMessage + result.ConsoleOutput);
        Assert.Contains("99", console);
    }
}
