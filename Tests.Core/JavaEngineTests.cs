using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>Java cells run by the real JDK (a JShell kernel), through the engine a headless host has (no Avalonia).</summary>
public class JavaEngineTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly INotebookKernel _kernel;
    private readonly JavaEngine _engine;

    public JavaEngineTests()
    {
        _engine = new JavaEngine(LanguageEngineServices.Create(_folder.Path));
        _kernel = _engine.NotebookKernels.Create(new KernelCreationContext(() => _folder.Path));
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

    [RealJavaFact]
    public async Task WhatACellDeclares_IsThereForTheNextCell()
    {
        await MustRunAsync("int x = 5;\nint twice(int n) { return n * 2; }\nrecord Person(String name, int age) {}");

        var ran = await MustRunAsync("var p = new Person(\"Ada\", twice(x) + 26);\nSystem.out.println(p.name() + \" is \" + p.age());");

        Assert.Contains("Ada is 36", ran.Console);
    }

    [RealJavaFact]
    public async Task AnExpressionsValue_IsShown_ADeclarationsIsNot()
    {
        await MustRunAsync("int kept = 7;");

        Assert.Equal("", (await MustRunAsync("int y = 1;")).Console);
        Assert.Equal("", (await MustRunAsync("kept = 9;")).Console);
        Assert.Equal("10\n", (await MustRunAsync("kept + 1")).Console);
        Assert.Equal("9\n", (await MustRunAsync("kept")).Console);
        Assert.Equal("\"text\"\n", (await MustRunAsync("\"text\"")).Console);
    }

    [RealJavaFact]
    public async Task AShortListOfScalars_IsOneLine()
    {
        Assert.Equal("[ 1, 2, 3 ]\n", (await MustRunAsync("new int[]{1, 2, 3}")).Console);
        Assert.Equal("[ a, b ]\n", (await MustRunAsync("List.of(\"a\", \"b\")")).Console);
    }

    [RealJavaFact]
    public async Task ARecordList_IsATable_ARecordAndABean_AnObjectExplorer()
    {
        await MustRunAsync("record Point(int x, int y) {}\npublic class Person { public String getName() { return \"Ada\"; } public boolean isAdmin() { return true; } }");

        var table = Assert.Single((await MustRunAsync("List.of(new Point(1, 2), new Point(3, 4))")).Rich).TableResult!;
        Assert.Equal(["x", "y"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, true], table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(["1", "3"], table.Rows.Select(r => r.Cells[0].DisplayText));

        var record = Assert.Single((await MustRunAsync("new Point(3, 4)")).Rich).InspectorNode!;
        Assert.Equal("Point", record.HeaderTitle);

        var bean = Assert.Single((await MustRunAsync("new Person()")).Rich).InspectorNode!;
        Assert.Equal(["admin", "name"], bean.Properties.Select(p => p.Name));
    }

    [RealJavaFact]
    public async Task AListOfMaps_IsATable_Too()
    {
        var table = Assert.Single((await MustRunAsync("List.of(new java.util.LinkedHashMap<>(java.util.Map.of(\"name\", \"Ada\")))")).Rich).TableResult!;

        Assert.Equal(["name"], table.Columns.Select(c => c.Header));
        Assert.Equal("Ada", table.Rows[0].Cells[0].DisplayText);
    }

    [RealJavaFact]
    public async Task Output_KeepsBlankLines_ErrorOutputIsItsOwn_AndTheOrderIsThePrintedOrder()
    {
        var ran = await MustRunAsync("System.out.println(\"a\");\nSystem.out.println();\nSystem.err.println(\"to stderr\");\nSystem.out.println(\"héllo ✓ 日本\");");

        Assert.Equal("a\n\nto stderr\nhéllo ✓ 日本\n", ran.Console);
    }

    [RealJavaFact]
    public async Task ALotOfOutput_ArrivesWhole()
    {
        var ran = await MustRunAsync("for (int i = 1; i <= 20000; i++) System.out.println(\"line \" + i);");

        var lines = ran.Console.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(20000, lines.Length);
        Assert.Equal("line 20000", lines[^1]);
    }

    [RealJavaFact]
    public async Task ACompileError_IsMarkedOnTheCellsLinesAndColumns()
    {
        var ran = await RunAsync("int a = 1;\n\n\n   int b = a +;");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics);
        Assert.Equal(4, error.Line);
        Assert.Equal(15, error.Column); // the ";"
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("illegal start of expression", error.Message);
        Assert.Equal("CompileError", ran.Result.ErrorName);
    }

    [RealJavaFact]
    public async Task ATypeError_IsOnItsLine_WhateverCameBeforeIt()
    {
        var ran = await RunAsync("int a = 1;\n\nString s = 5;\nint c = a;");

        Assert.False(ran.Result.Success);
        var error = Assert.Single(ran.Result.Diagnostics);
        Assert.Equal(3, error.Line);
        Assert.Contains("incompatible types", error.Message);
    }

    [RealJavaFact]
    public async Task ARuntimeError_NamesItsRealKind_AndTheLineOfTheCell()
    {
        var ran = await RunAsync("void f(int x) {\n    if (x > 1) throw new IllegalStateException(\"boom\");\n}\n\nf(5);");

        Assert.False(ran.Result.Success);
        Assert.Equal("IllegalStateException", ran.Result.ErrorName); // not "EvalException"
        Assert.Equal(2, ran.Result.ErrorLine); // inside f, where it was thrown
        Assert.Contains("boom", ran.Result.ErrorMessage);
        Assert.Contains("at f (line 2)", ran.Console);
        Assert.Contains("at <cell> (line 5)", ran.Console);

        Assert.Equal("2\n", (await MustRunAsync("1 + 1")).Console); // the session goes on
    }

    [RealJavaFact]
    public async Task ABlankLineBeforeAnImport_DoesNotMoveTheLinesAfterIt()
    {
        var ran = await RunAsync("int a = 1;\n\n\nimport fry.*;\n\nString s = 5;");

        Assert.Equal(6, Assert.Single(ran.Result.Diagnostics).Line);
    }

    [RealJavaFact]
    public async Task TheVariables_AreListedWithTheirTypes()
    {
        await MustRunAsync("int x = 5;\nList<String> names = List.of(\"a\");");

        var variables = (await _kernel.GetVariablesAsync(CancellationToken.None)).ToDictionary(v => v.Name);

        Assert.Equal("int", variables["x"].TypeName);
        Assert.Equal("List<String>", variables["names"].TypeName);
    }

    [RealJavaFact]
    public async Task AValueIsSharedOut_AsJson_AndInAsAJavaValue()
    {
        await MustRunAsync("record Person(String name, int age) {}\nvar people = List.of(new Person(\"Ada\", 36));\nint n = 42;");

        Assert.Equal("42", await _kernel.GetValueJsonAsync("n", CancellationToken.None));
        Assert.Contains("Ada", await _kernel.GetValueJsonAsync("people", CancellationToken.None));
        await Assert.ThrowsAsync<KernelValueException>(() => _kernel.GetValueJsonAsync("nope", CancellationToken.None));

        await _kernel.SetValueFromJsonAsync("rows", """[{"name":"Ada","age":36},{"name":"Linus","age":55}]""", CancellationToken.None);
        await _kernel.SetValueFromJsonAsync("count", "42", CancellationToken.None);
        var ran = await MustRunAsync("System.out.println(rows.size() + \" \" + (count + 1));");
        Assert.Contains("2 43", ran.Console);
    }

    [RealJavaFact]
    public async Task ACellThatReadsALine_ShowsItsPromptFirst_ThenGetsTheAnswer()
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var asked = new List<string>();
            var ran = await RunAsync("System.out.print(\"Name? \");\nString name = new java.util.Scanner(System.in).nextLine();\nSystem.out.println(\"Hello \" + name);", (prompt, _) => { asked.Add(prompt); return "Ada"; });

            if ((!ran.Result.Success || asked.Count != 1) && attempt < maxAttempts)
            {
                await Task.Delay(200);
                continue;
            }

            Assert.True(ran.Result.Success, ran.Result.ErrorMessage);
            Assert.Single(asked);
            Assert.Equal("Name? Ada\nHello Ada\n", ran.Console); // the notebook echoes the answer after the prompt, as a terminal does
            break;
        }
    }

    [RealJavaFact]
    public async Task ATableMarkdownAndAChart_AreDisplayedFromACell_InTheOrderMade()
    {
        var ran = await MustRunAsync(
            "record P(String a, int b) {}\n" +
            "Display.table(List.of(new P(\"x\", 1), new P(\"y\", 2)));\n" +
            "System.out.println(\"between\");\n" +
            "Display.markdown(\"# Title\");\n" +
            "var chart = Display.lineChart(new double[]{1, 3, 2}, \"Chart\");");

        Assert.Equal(["a", "b"], ran.Rich[0].TableResult!.Columns.Select(c => c.Header));
        Assert.Equal([false, true], ran.Rich[0].TableResult!.Columns.Select(c => c.IsNumeric));
        Assert.Equal("# Title", ran.Rich[1].MarkdownSource);
        Assert.NotNull(ran.Rich[2].Visual);
        Assert.Contains("between", ran.Console);
    }

    [RealJavaFact]
    public async Task StopEndsACellThatWaits_OrOnlyComputes_AndTheNotebooksValuesStay()
    {
        await MustRunAsync("int kept = 5;");

        foreach (var loop in new[] { "while (true) { try { Thread.sleep(10); } catch (InterruptedException e) { } }", "long i = 0; while (true) { i++; }" })
        {
            using var stop = new CancellationTokenSource();
            var clock = Stopwatch.StartNew();
            var running = RunAsync(loop, ct: stop.Token);

            await Task.Delay(1200);
            await stop.CancelAsync();
            var ran = await running;

            Assert.True(ran.Result.WasCancelled, loop);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), clock.Elapsed.ToString()); // the kernel stopped it itself
        }

        Assert.Equal("6\n", (await MustRunAsync("kept + 1")).Console);
    }

    [RealJavaFact]
    public async Task ARestart_ForgetsEverything()
    {
        await MustRunAsync("int kept = 1;");

        _kernel.HardReset();
        var ran = await RunAsync("kept");

        Assert.False(ran.Result.Success);
        Assert.Equal("3\n", (await MustRunAsync("1 + 2")).Console);
    }

    [RealJavaNetworkFact]
    public async Task AMavenPackage_RestoredForACell_IsOnTheClasspathFromTheNextCell()
    {
        var restored = await _engine.Packages.RunAsync(_engine.Packages.InstallCommand("com.google.code.gson:gson:2.11.0"), TestToolchain(), _ => { });
        Assert.True(restored.Success, restored.Message);
        Assert.NotNull(restored.AddedSearchPath);

        await MustRunAsync("int warm = 1;"); // the kernel is running
        await ((ISearchPathKernel)_kernel).AddSearchPathAsync(restored.AddedSearchPath!);
        var ran = await MustRunAsync("new com.google.gson.Gson().toJson(List.of(1, 2))");

        Assert.Contains("[1,2]", ran.Console);
    }

    private static PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains.ToolchainInfo TestToolchain() => new()
    {
        LanguageId = "java", ExecutablePath = "java", Version = new Version(17, 0), DisplayName = "java", Source = "test"
    };

    [Fact]
    public void TheEngine_IsOneAHeadlessHostHas()
    {
        using var folder = new TempFolder();
        var languages = HeadlessLanguages.Create(LanguageEngineServices.Create(folder.Path));

        var java = languages.Get(LanguageIds.Java);

        Assert.NotNull(java);
        Assert.True(java.Has(LanguageCapabilities.NotebookCells | LanguageCapabilities.ValueSharing));
        Assert.Same(java, languages.FindSourceFileLanguage("Main.java"));
        Assert.Same(java, languages.Get("jdk"));
    }
}
