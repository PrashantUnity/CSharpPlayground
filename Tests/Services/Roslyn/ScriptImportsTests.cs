using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Code that compiles in one place compiles everywhere: a script, a program with a Main, a notebook cell and a watch
/// expression in the debugger see the same namespaces. The studio's own controls are not among them.
/// </summary>
public class ScriptImportsTests
{
    // One expression per kind of name a script leans on without a using directive.
    public static TheoryData<string> Probes() => new()
    {
        "Check(\"probe\", 1 + 1, \"2\")",          // using static ScriptHelpers
        "Stopwatch.StartNew()",                  // System.Diagnostics
        "new Pen(Brushes.Red, 2)",               // Avalonia.Media, for Display.Animate drawings
        "new ChartSeries()",                     // charts
        "new Point3D(1, 2, 3)",                  // 3D plots
        "typeof(TreeTracker)",                   // visualizers
        "typeof(Display)",
        // The spec types every language's visuals are made of, and the handle a visual call returns: none may clash with
        // a name already in scope.
        "new ChartSpec { Kind = ChartType.Bar, Series = { new ChartSeriesSpec { Y = { 1, 2 } } } }",
        "new Plot3DSpec { Kind = Plot3DType.Surface, ColorMap = ColorMapPreset.Plasma }",
        "new VisualizerSpec { Kind = VisualizerKind.Tree, State = { Tree = new TreeState() } }",
        "new GraphSpec { Nodes = { new GraphNodeSpec { Id = \"a\", State = ElementState.Visited } } }",
        "(ElementRef.Cell(1, 2), ScalarValue.FromText(\"x\"), new AxisSpec(), new LegendSpec())",
        "typeof(DisplayHandle<ChartSpec>)"
    };

    [Theory]
    [MemberData(nameof(Probes))]
    public void AScript_SeesTheName(string expression) =>
        AssertNoErrors(new RoslynCompilerService().CheckDiagnostics($"var probe = {expression};"));

    [Theory]
    [MemberData(nameof(Probes))]
    public void AProgramWithAMain_SeesTheName(string expression) =>
        AssertNoErrors(new RoslynCompilerService().CheckDiagnostics(
            $"public class Program {{ public static void Main() {{ var probe = {expression}; }} }}", ExecutionLanguageMode.Program));

    [Theory]
    [MemberData(nameof(Probes))]
    public async Task ANotebookCell_SeesTheName(string expression)
    {
        var result = await new NotebookExecutionKernel().ExecuteCellAsync($"var probe = {expression};");

        Assert.True(result.Success, result.ErrorMessage);
    }

    [Theory]
    [MemberData(nameof(Probes))]
    public async Task ADebuggerWatch_SeesTheName(string expression)
    {
        var debugger = new ScriptDebuggerService(new RoslynCompilerService(), new ScriptExecutionEngine());

        var (success, result, _) = await debugger.EvaluateExpressionAsync(expression, Array.Empty<DebugVariableItem>());

        Assert.True(success, result);
    }

    [Fact]
    public async Task TheStudiosOwnControls_AreNotImported()
    {
        const string code = "var probe = typeof(StudioTabBarControl);";

        Assert.Contains(new RoslynCompilerService().CheckDiagnostics(code), d => d.Severity == DiagnosticSeverity.Error);
        Assert.False((await new NotebookExecutionKernel().ExecuteCellAsync(code)).Success);
    }

    [Fact]
    public void ExtensibilityScript_WithDispatcherAndDuplicateUsings_CompilesCleanlyWithNoErrors()
    {
        const string scriptCode = """
            using System;
            using System.Collections.Generic;
            using Avalonia.Controls;
            using Avalonia.Threading;

            Dispatcher.UIThread.Post(() =>
            {
                var win = new Window { Title = "Test" };
                win.Show();
            });
            """;

        var compiler = new RoslynCompilerService();
        var diags = compiler.CheckDiagnostics(scriptCode);
        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = diags.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        Assert.Empty(errors);
        Assert.Empty(warnings);
    }

    [Fact]
    public void SnakeShowcaseScript_CompilesCleanlyWithNoErrors()
    {
        var docService = DocumentationService.Instance;
        var extensibilityCat = docService.Categories.First(c => c.Id == "extensibility_customization");
        var ch10 = extensibilityCat.Articles.First(a => a.Id == "extensibility_ch10_extensions");
        var snakeSnippet = ch10.CodeSnippets.First(s => s.Id == "ch10_snake_app_script");

        var compiler = new RoslynCompilerService();
        var diags = compiler.CheckDiagnostics(snakeSnippet.Code);
        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Id}: {d.Message} (Line {d.Line})").ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    private static void AssertNoErrors(System.Collections.Generic.IEnumerable<DiagnosticItem> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => $"{d.Id}: {d.Message}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
