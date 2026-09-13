using System;
using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test-completion")
        {
            RunCompletionSelfTest();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RunCompletionSelfTest()
    {
        var compiler = new Services.RoslynCompilerService();
        var completionService = new Services.CSharpCompletionService(compiler);

        Console.WriteLine("Running CSharpCompletionService Automated Self-Tests...");

        var userCode = @"using System.Linq;

// Inspect runtime environment and application metadata
var environment = new {
    Application = ""FryPDF Document Studio"",
    Version = ""2026.1"",
    Runtime = "".NET 10 (C# 13)"",
    Compiler = ""Microsoft.CodeAnalysis.CSharp (Roslyn)"",
    Modules = new[] { ""PDF Rendering"", ""Document Automation"", ""C# Studio"" }
};

environment.Dump(""Studio Environment Metadata"");

// LINQ sequence projections and calculations
var calculations = Enumerable.Range(1, 8)
    .Select(n => new { Number = n, Square = n * n, Cube = n * n * n })
    .ToList();

calculations.Dump(""Calculated Power Sequences"");

Console.WriteLine(""Script evaluation completed successfully."");

environment.";

        // Scenario 1: Dot member access on environment.
        int caret = userCode.Length; // Right after 'environment.'
        var completions = completionService.GetCompletionsAsync(userCode, caret, Services.ExecutionLanguageMode.Statements).GetAwaiter().GetResult();

        Console.WriteLine($"[Scenario 1] environment. completions: {completions.Count}");
        foreach (var c in System.Linq.Enumerable.Take(completions, 6))
        {
            Console.WriteLine($"   {c.Kind}: {c.DisplayText} (P={c.Priority:N0}) | Sig: {c.Signature}");
        }

        var appItem = System.Linq.Enumerable.FirstOrDefault(completions, c => c.DisplayText == "Application");
        if (appItem == null) throw new Exception("Scenario 1 failed: Missing Application on environment.");

        // Scenario 2: Dot member access on misspelled 'enviroment.'
        var typoCode = userCode.Substring(0, userCode.Length - 12) + "enviroment.";
        var typoCompletions = completionService.GetCompletionsAsync(typoCode, typoCode.Length, Services.ExecutionLanguageMode.Statements).GetAwaiter().GetResult();
        Console.WriteLine($"[Scenario 2] Typo 'enviroment.' completions: {typoCompletions.Count}");
        var typoAppItem = System.Linq.Enumerable.FirstOrDefault(typoCompletions, c => c.DisplayText == "Application");
        if (typoAppItem == null) throw new Exception("Scenario 2 failed: Typo tolerance failed for enviroment.");
        Console.WriteLine($"   Typo resolved to: {typoAppItem.DisplayText} (P={typoAppItem.Priority:N0})");

        // Scenario 3: Scope completion with query 'env' or 'enviroment'
        var queryCode = userCode.Substring(0, userCode.Length - 12) + "enviroment";
        var scopeCompletions = completionService.GetCompletionsAsync(queryCode, queryCode.Length, Services.ExecutionLanguageMode.Statements).GetAwaiter().GetResult();
        Console.WriteLine($"[Scenario 3] Scope with query 'enviroment': Top item is '{scopeCompletions[0].DisplayText}' (Kind: {scopeCompletions[0].Kind}, P={scopeCompletions[0].Priority:N0})");
        if (scopeCompletions[0].DisplayText != "environment")
        {
            throw new Exception($"Scenario 3 failed: Expected 'environment' at top, got '{scopeCompletions[0].DisplayText}'");
        }

        // Scenario 4: Scope with query 'calc'
        var calcCode = userCode.Substring(0, userCode.Length - 12) + "calc";
        var calcCompletions = completionService.GetCompletionsAsync(calcCode, calcCode.Length, Services.ExecutionLanguageMode.Statements).GetAwaiter().GetResult();
        Console.WriteLine($"[Scenario 4] Scope with query 'calc': Top item is '{calcCompletions[0].DisplayText}' (P={calcCompletions[0].Priority:N0})");
        if (calcCompletions[0].DisplayText != "calculations")
        {
            throw new Exception($"Scenario 4 failed: Expected 'calculations' at top, got '{calcCompletions[0].DisplayText}'");
        }

        // Scenario 5: Console.
        var consoleCode = "Console.";
        var consoleCompletions = completionService.GetCompletionsAsync(consoleCode, consoleCode.Length, Services.ExecutionLanguageMode.Statements).GetAwaiter().GetResult();
        var wl = System.Linq.Enumerable.FirstOrDefault(consoleCompletions, c => c.DisplayText == "WriteLine");
        if (wl == null) throw new Exception("Scenario 5 failed: Missing WriteLine on Console.");
        Console.WriteLine($"[Scenario 5] Console. WriteLine resolved: {wl.Signature}");

        Console.WriteLine("✨ ALL 5 INTELLISENSE & RELEVANCY SCENARIOS PASSED WITH FLYING COLORS!");
        Environment.Exit(0);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
