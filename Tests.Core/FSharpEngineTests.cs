using System.Diagnostics;
using CSharpEditorPlugin.Tests;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>F# cells run by the real <c>dotnet fsi</c>, through the engine a headless host has (no Avalonia).</summary>
public class FSharpEngineTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly INotebookKernel _kernel;

    public FSharpEngineTests()
    {
        var engine = new FSharpEngine(LanguageEngineServices.Create(_folder.Path));
        _kernel = engine.NotebookKernels.Create(new KernelCreationContext(() => _folder.Path));
    }

    public void Dispose()
    {
        _kernel.Dispose();
        _folder.Dispose();
    }

    private sealed record Ran(KernelExecutionResult Result, List<string> Text, List<RichCellOutput> Rich)
    {
        public string Console => string.Concat(Text);
    }

    private async Task<Ran> RunAsync(string code, Func<string, bool, string?>? input = null, CancellationToken ct = default)
    {
        var text = new List<string>();
        var rich = new List<RichCellOutput>();
        var result = await _kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            OnConsole = text.Add,
            OnRichOutput = rich.Add,
            OnInputRequest = input is null ? null : (prompt, password, _) => Task.FromResult(input(prompt, password))
        }, ct);
        return new Ran(result, text, rich);
    }

    private async Task<Ran> MustRunAsync(string code)
    {
        var ran = await RunAsync(code);
        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        return ran;
    }

    [RealFSharpFact]
    public async Task WhatACellDefines_IsThereForTheNextCell()
    {
        await MustRunAsync("let x = 5\nlet add a b = a + b\ntype Person = { Name: string; Age: int }");

        var ran = await MustRunAsync("let p = { Name = \"Ada\"; Age = add x 31 }\nprintfn \"%s is %d\" p.Name p.Age");

        Assert.Contains("Ada is 36", ran.Console);
    }

    [RealFSharpFact]
    public async Task TheValueOfTheLastExpression_IsShown_AScalarAsText_AUnitNotAtAll()
    {
        Assert.Equal("6\n", (await MustRunAsync("let x = 5\nx + 1")).Console);
        Assert.Equal("text\n", (await MustRunAsync("\"text\"")).Console);
        Assert.Equal("2024-01-02 03:04:05\n", (await MustRunAsync("System.DateTime(2024, 1, 2, 3, 4, 5)")).Console);
        Assert.Equal("", (await MustRunAsync("let y = 1")).Console);
        Assert.Equal("hi\n", (await MustRunAsync("printfn \"hi\"")).Console); // unit: only what it printed
    }

    [RealFSharpFact]
    public async Task AShortListOfScalars_IsOneLine_AsInTheOtherLanguages()
    {
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync("[1; 2; 3]")).Console);
        Assert.Equal("[ \"a\", \"b\" ]\n", (await MustRunAsync("[| \"a\"; \"b\" |]")).Console);
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync("seq { 1 .. 3 }")).Console);
    }

    [RealFSharpFact]
    public async Task ARecordList_IsATable_ARecord_AnObjectExplorer()
    {
        await MustRunAsync("type Person = { Name: string; Age: int }\nlet people = [ { Name = \"Ada\"; Age = 36 }; { Name = \"Linus\"; Age = 55 } ]");

        var list = await MustRunAsync("people");
        var table = Assert.Single(list.Rich).TableResult!;
        Assert.Equal(["Name", "Age"], table.Columns.Select(c => c.Header));
        Assert.Equal([false, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["Ada", "Linus"], table.Rows.Select(r => r.Cells[0].DisplayText));

        var one = Assert.Single((await MustRunAsync("people.Head")).Rich);
        Assert.Equal(CellOutputKind.ObjectInspector, one.Kind);
        Assert.Equal("Person", one.InspectorNode!.HeaderTitle);
        Assert.Equal(["Name", "Age"], one.InspectorNode.Properties.Select(p => p.Name));
        Assert.Equal("Ada", one.InspectorNode.Properties[0].SimpleValueText);
    }

    [RealFSharpFact]
    public async Task ACollectionOfScalarsPastTheLine_AMap_AClass_AreObjectExplorers()
    {
        var map = Assert.Single((await MustRunAsync("Map.ofList [ \"a\", 1; \"b\", 2 ]")).Rich);
        Assert.Equal(["a", "b"], map.InspectorNode!.Properties.Select(p => p.Name));

        var longList = Assert.Single((await MustRunAsync("[ 1 .. 200 ]")).Rich);
        Assert.Equal(CellOutputKind.ObjectInspector, longList.Kind);

        var instance = Assert.Single((await MustRunAsync("System.Text.StringBuilder(\"abc\")")).Rich);
        Assert.Equal("StringBuilder", instance.InspectorNode!.HeaderTitle);
    }

    [RealFSharpFact]
    public async Task AnObjectThatPointsToItself_IsListedFourLevelsDown_LikeTheDesktopShowsIt()
    {
        var shown = Assert.Single((await MustRunAsync("""
            type People() =
                member val Name = "" with get, set
                member val Another : People = Unchecked.defaultof<People> with get, set
            let person = People()
            person.Name <- "Prashant"
            person.Another <- person
            person
            """)).Rich);

        InspectorChain.AssertFiveLevels(shown.InspectorNode!, "Another", ["Name", "Another"]);
    }

    [RealFSharpFact]
    public async Task AnInfiniteSequence_IsShownFromItsFirstItems_NotEnumeratedForever()
    {
        var shown = Assert.Single((await MustRunAsync("Seq.initInfinite id")).Rich);

        Assert.True(shown.InspectorNode!.Properties.Count <= 101);
    }

    [RealFSharpFact]
    public async Task OutputAndErrorOutput_AreTheCellsOwn_InTheOrderPrinted_WithUnicode()
    {
        var ran = await MustRunAsync("printfn \"héllo ✓ 日本\"\neprintfn \"to stderr\"\nprintfn \"after\"");

        Assert.Equal("héllo ✓ 日本\nto stderr\nafter\n", ran.Console);
    }

    [RealFSharpFact]
    public async Task ACompileError_IsMarkedOnTheCellsLinesAndColumns_AndTheCellFails()
    {
        var ran = await RunAsync("let a = 1\nlet b = a + \"x\"\nlet c = 3");

        Assert.False(ran.Result.Success);
        Assert.All(ran.Result.Diagnostics, d => Assert.Equal(2, d.Line)); // (the compiler says it twice: a type mismatch and its cause)
        var first = ran.Result.Diagnostics[0];
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, first.Severity);
        Assert.Equal(13, first.Column); // the "x"
        Assert.Equal(16, first.EndColumn);
        Assert.StartsWith("FS", first.Id);
    }

    [RealFSharpFact]
    public async Task ASyntaxError_IsWhereTheCompilerFoundItWrong()
    {
        var ran = await RunAsync("let a = 1\nlet b = a +\nlet c = 3");

        Assert.False(ran.Result.Success);
        Assert.NotEmpty(ran.Result.Diagnostics);
        Assert.All(ran.Result.Diagnostics, d => Assert.InRange(d.Line, 2, 3));
    }

    [RealFSharpFact]
    public async Task AnUndefinedName_IsOnItsLine_WithTheCompilersWords()
    {
        var ran = await RunAsync("let a = 1\n\nlet b = a + nope\nb");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics);
        Assert.Equal(3, error.Line);
        Assert.Equal(13, error.Column);
        Assert.Equal("FS0039", error.Id);
        Assert.Contains("nope", error.Message);
    }

    [RealFSharpFact]
    public async Task ARuntimeException_NamesTheKindAndTheLineOfTheCell_AndTheKernelGoesOn()
    {
        var ran = await RunAsync("let f x =\n    if x > 1 then failwith \"boom\" else x\nf 5");

        Assert.False(ran.Result.Success);
        Assert.Equal("Exception", ran.Result.ErrorName);
        Assert.Equal(3, ran.Result.ErrorLine);
        Assert.Contains("boom", ran.Result.ErrorMessage);
        Assert.Contains("at FSI", ran.Console); // the stack
        Assert.DoesNotContain("System.Reflection", ran.Console);

        Assert.Equal("2\n", (await MustRunAsync("1 + 1")).Console); // same session
    }

    [RealFSharpFact]
    public async Task AnExpressionThatOnlyRaises_IsReportedAsTheException_NotAValueRestriction()
    {
        var ran = await RunAsync("failwith \"generic\"");

        Assert.False(ran.Result.Success);
        Assert.Equal("Exception", ran.Result.ErrorName);
        Assert.Contains("generic", ran.Result.ErrorMessage);
        Assert.Empty(ran.Result.Diagnostics);
    }

    [RealFSharpFact]
    public async Task TheVariables_AreListedWithTheirTypes_IncludingFunctions()
    {
        // (a function written `let add a b = ...` is a method to FSI, not a value, and is not listed; a function value is)
        await MustRunAsync("let x = 5\nlet names = [ \"a\"; \"b\" ]\nlet doubleAll = List.map (fun n -> n * 2)\nlet pair = (1, \"two\")\nlet add a b = a + b");

        var variables = (await _kernel.GetVariablesAsync(CancellationToken.None)).ToDictionary(v => v.Name);

        Assert.Equal(["doubleAll", "names", "pair", "x"], variables.Keys.Order());
        Assert.Equal("int", variables["x"].TypeName);
        Assert.Equal("string list", variables["names"].TypeName);
        Assert.Equal("int * string", variables["pair"].TypeName);
        Assert.Equal("Function", variables["doubleAll"].Kind);
    }

    [RealFSharpFact]
    public async Task AValueIsSharedOut_AsJson()
    {
        await MustRunAsync("type Person = { Name: string; Age: int }\nlet people = [ { Name = \"Ada\"; Age = 36 } ]\nlet n = 42\nlet maybe = Some \"x\"\nlet doubleAll = List.map (fun n -> n * 2)");

        Assert.Equal("""[{"Name":"Ada","Age":36}]""", await _kernel.GetValueJsonAsync("people", CancellationToken.None));
        Assert.Equal("42", await _kernel.GetValueJsonAsync("n", CancellationToken.None));
        Assert.Equal("\"x\"", await _kernel.GetValueJsonAsync("maybe", CancellationToken.None));
        var function = await Assert.ThrowsAsync<KernelValueException>(() => _kernel.GetValueJsonAsync("doubleAll", CancellationToken.None));
        Assert.Contains("function", function.Message);
        var missing = await Assert.ThrowsAsync<KernelValueException>(() => _kernel.GetValueJsonAsync("nope", CancellationToken.None));
        Assert.Contains("nope", missing.Message);
    }

    [RealFSharpFact]
    public async Task AValueIsSharedIn_AsAnOrdinaryFSharpValue()
    {
        await _kernel.SetValueFromJsonAsync("rows", """[{"name":"Ada","age":36},{"name":"Linus","age":55}]""", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("count", "42", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("ratio", "2.5", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("nums", "[1,2,3]", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("reals", "[1,2.5]", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("flag", "true", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("tricky", "\"it's \\\"quoted\\\"\\nnew line ✓ \\\\\"", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("odd name", "7", CancellationToken.None);

        var ran = await MustRunAsync(
            "printfn \"%d %d %f %d %f %b\" rows.Length (count * 2) (ratio * 2.0) (Array.sum nums) (Array.sum reals) flag\n" +
            "printfn \"%s\" tricky\n" +
            "rows |> Array.map (fun r -> r.[\"name\"] :?> string) |> printfn \"%A\"\n" +
            "printfn \"%d\" ``odd name``");

        Assert.Contains("2 84 5.000000 6 3.500000 true", ran.Console);
        Assert.Contains("it's \"quoted\"\nnew line ✓ \\", ran.Console);
        Assert.Contains("[|\"Ada\"; \"Linus\"|]", ran.Console);
        Assert.Contains("7", ran.Console);
    }

    [RealFSharpFact]
    public async Task ACellThatReadsALine_AsksTheNotebook_AndGetsTheAnswer()
    {
        var asked = new List<string>();
        var ran = await RunAsync("printf \"Name? \"\nlet name = System.Console.ReadLine()\nprintfn \"Hello %s\" name", (prompt, _) => { asked.Add(prompt); return "Ada"; });

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage);
        Assert.Single(asked);
        Assert.Contains("Hello Ada", ran.Console);
        Assert.Contains("Name? ", ran.Console);
    }

    [RealFSharpFact]
    public async Task TheEndOfInput_IsNull_NotAHang()
    {
        var ran = await RunAsync("let line = System.Console.ReadLine()\nprintfn \"%b\" (isNull line)", (_, _) => null);

        Assert.Contains("true", ran.Console);
    }

    [RealFSharpFact]
    public async Task ATableMarkdownAndAChart_AreDisplayedFromACell()
    {
        var ran = await MustRunAsync(
            "Display.table [ {| A = 1; B = \"x\" |}; {| A = 2; B = \"y\" |} ]\n" +
            "Display.markdown \"# Title\"\n" +
            "let chart = Display.lineChart [ 1.0; 3.0; 2.0 ]\n" +
            "printfn \"after\"");

        Assert.Equal(3, ran.Rich.Count);
        Assert.Equal(["A", "B"], ran.Rich[0].TableResult!.Columns.Select(c => c.Header));
        Assert.Equal("# Title", ran.Rich[1].MarkdownSource);
        Assert.NotNull(ran.Rich[2].Visual);
        Assert.Contains("after", ran.Console);
    }

    [RealFSharpFact]
    public async Task AChartTheLastExpressionNames_IsNotShownTwice()
    {
        var ran = await MustRunAsync("let chart = Display.lineChart [ 1.0; 2.0 ]\nchart");

        Assert.Single(ran.Rich);
    }

    [RealFSharpFact]
    public async Task ARestart_ForgetsEverything_AndTheNextCellStartsAFreshSession()
    {
        await MustRunAsync("let kept = 1");

        _kernel.HardReset();
        var ran = await RunAsync("kept");

        Assert.False(ran.Result.Success);
        Assert.Equal("FS0039", Assert.Single(ran.Result.Diagnostics).Id);
        Assert.Equal("3\n", (await MustRunAsync("1 + 2")).Console);
    }

    [RealFSharpFact]
    public async Task StopEndsACellThatWaits_AndWhatTheNotebookDefinedIsStillThere()
    {
        await MustRunAsync("let kept = 1");
        using var stop = new CancellationTokenSource();
        var clock = Stopwatch.StartNew();
        var running = RunAsync("while true do System.Threading.Thread.Sleep 10", ct: stop.Token);

        await Task.Delay(1500);
        await stop.CancelAsync();
        var ran = await running;

        Assert.True(ran.Result.WasCancelled);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), clock.Elapsed.ToString()); // the kernel stopped it itself
        Assert.Equal("1\n", (await MustRunAsync("kept")).Console);
    }

    [RealFSharpFact]
    public async Task StopEndsACellThatOnlyComputes_BySayingGoodbyeToTheSession_ThenTheNextCellStartsAFreshOne()
    {
        await MustRunAsync("let lost = 1");
        using var stop = new CancellationTokenSource();
        var running = RunAsync("let mutable i = 0L\nwhile true do i <- i + 1L", ct: stop.Token);

        await Task.Delay(1500);
        await stop.CancelAsync();
        var ran = await running;

        Assert.True(ran.Result.WasCancelled);
        Assert.Contains("variables are gone", ran.Result.ErrorMessage); // the notebook says so
        var next = await RunAsync("lost");
        Assert.False(next.Result.Success);
        Assert.Equal("3\n", (await MustRunAsync("1 + 2")).Console);
    }

    [RealFSharpNuGetFact]
    public async Task APackageLineAtTheTopOfACell_LoadsThePackage_AsFsiDoes()
    {
        var ran = await RunAsync("#r \"nuget: Newtonsoft.Json, 13.0.3\"\nopen Newtonsoft.Json\nJsonConvert.SerializeObject([ 1; 2 ])");

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        Assert.Contains("[1,2]", ran.Console);
    }

    [RealFSharpNuGetFact]
    public async Task TheJupyterWayToSayIt_PercentNuget_IsTheSameLine()
    {
        var ran = await RunAsync("%nuget install Newtonsoft.Json 13.0.3\nNewtonsoft.Json.JsonConvert.SerializeObject({| A = 1 |})");

        Assert.True(ran.Result.Success, ran.Result.ErrorMessage + ran.Console);
        Assert.Contains("{\"A\":1}", ran.Console);
    }

    [RealFSharpFact]
    public async Task TheDirectiveLinesOfACell_AreForTheNotebook_NotForFSharp()
    {
        var ran = await MustRunAsync("\n\nlet a = 1\na + 1");

        Assert.Equal("2\n", ran.Console);
    }

    [Fact]
    public void TheEngine_IsOneAHeadlessHostHas()
    {
        using var folder = new TempFolder();
        var languages = HeadlessLanguages.Create(LanguageEngineServices.Create(folder.Path));

        var fsharp = languages.Get(LanguageIds.FSharp);

        Assert.NotNull(fsharp);
        Assert.True(fsharp.Has(LanguageCapabilities.NotebookCells | LanguageCapabilities.ValueSharing));
        Assert.Same(fsharp, languages.FindSourceFileLanguage("script.fsx"));
        Assert.Same(fsharp, languages.Get("fs"));
        Assert.Contains("Values.fsx", FSharpDisplayRuntime.Files.Keys);
        Assert.Contains("Display.fsx", FSharpDisplayRuntime.Files.Keys);
    }
}
