using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Integration tests for <see cref="CSharpCompletionService"/>.
/// Verifies dot-member completion, typo tolerance, and scope ranking.
/// Run with: dotnet test Tests/CSharpEditorPlugin.Tests.csproj
/// </summary>
public class CSharpCompletionServiceTests
{
    // Shared heavy objects — RoslynCompilerService takes ~14s to init;
    // create once per test class, not per test.
    // ⚠️ See POSTMORTEM.md — never construct this on the UI thread.
    private static readonly RoslynCompilerService Compiler = new();
    private static readonly CSharpCompletionService Service = new(Compiler);

    private const string BaseCode = """
        using System.Linq;

        var environment = new {
            Application = "FryPDF Document Studio",
            Version = "2026.1",
            Runtime = ".NET 10 (C# 13)",
            Compiler = "Microsoft.CodeAnalysis.CSharp (Roslyn)",
            Modules = new[] { "PDF Rendering", "Document Automation", "C# Studio" }
        };

        environment.Dump("Studio Environment Metadata");

        var calculations = Enumerable.Range(1, 8)
            .Select(n => new { Number = n, Square = n * n, Cube = n * n * n })
            .ToList();

        calculations.Dump("Calculated Power Sequences");

        Console.WriteLine("Script evaluation completed successfully.");

        environment.
        """;

    [Fact]
    public async Task DotMemberAccess_ReturnsAnonymousTypeProperties()
    {
        var completions = await Service.GetCompletionsAsync(BaseCode, BaseCode.Length, ExecutionLanguageMode.Statements);

        Assert.NotEmpty(completions);
        var appItem = completions.FirstOrDefault(c => c.DisplayText == "Application");
        Assert.NotNull(appItem);
    }

    [Fact]
    public async Task DotMemberAccess_ReturnsAllExpectedAnonymousProperties()
    {
        var completions = await Service.GetCompletionsAsync(BaseCode, BaseCode.Length, ExecutionLanguageMode.Statements);

        var names = completions.Select(c => c.DisplayText).ToHashSet();
        Assert.Contains("Application", names);
        Assert.Contains("Version", names);
        Assert.Contains("Runtime", names);
        Assert.Contains("Compiler", names);
        Assert.Contains("Modules", names);
    }

    [Fact]
    public async Task TypoTolerance_DotAccess_OnMisspelledVariable()
    {
        // Replace 'environment.' with 'enviroment.' (missing 'n')
        var typoCode = BaseCode[..^12] + "enviroment.";

        var completions = await Service.GetCompletionsAsync(typoCode, typoCode.Length, ExecutionLanguageMode.Statements);

        Assert.NotEmpty(completions);
        var appItem = completions.FirstOrDefault(c => c.DisplayText == "Application");
        Assert.NotNull(appItem);
    }

    [Fact]
    public async Task ScopeCompletion_TypoQuery_RanksCorrectVariableFirst()
    {
        var queryCode = BaseCode[..^12] + "enviroment"; // no dot — scope filter

        var completions = await Service.GetCompletionsAsync(queryCode, queryCode.Length, ExecutionLanguageMode.Statements);

        Assert.NotEmpty(completions);
        Assert.Equal("environment", completions[0].DisplayText);
    }

    [Fact]
    public async Task ScopeCompletion_PartialQuery_RanksCorrectVariableFirst()
    {
        var calcCode = BaseCode[..^12] + "calc";

        var completions = await Service.GetCompletionsAsync(calcCode, calcCode.Length, ExecutionLanguageMode.Statements);

        Assert.NotEmpty(completions);
        Assert.Equal("calculations", completions[0].DisplayText);
    }

    [Fact]
    public async Task DotMemberAccess_ConsoleType_ContainsWriteLine()
    {
        var completions = await Service.GetCompletionsAsync("Console.", 8, ExecutionLanguageMode.Statements);

        var wl = completions.FirstOrDefault(c => c.DisplayText == "WriteLine");
        Assert.NotNull(wl);
        Assert.False(string.IsNullOrWhiteSpace(wl.Signature));
    }
}
