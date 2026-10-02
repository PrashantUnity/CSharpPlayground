using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

internal static class DiagramSnapshots
{
    public static void Run(Options options)
    {
        var targetLang = options.Value("lang")?.ToLowerInvariant();
        var targetSnippet = options.Value("snippet")?.ToLowerInvariant();
        var docService = new DocumentationService();
        var category = docService.Categories.FirstOrDefault(c => c.Id == "diagrams_and_visualizations");

        if (category == null)
        {
            Console.Error.WriteLine("Error: 'diagrams_and_visualizations' category not found in DocumentationService.");
            return;
        }

        var results = new List<(string SnippetId, string Language, bool Passed, string Details)>();
        var tempRoot = Path.Combine(Path.GetTempPath(), "FryStudio_DiagramTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            foreach (var article in category.Articles)
            {
                foreach (var snippet in article.CodeSnippets)
                {
                    if (!string.IsNullOrEmpty(targetSnippet) && !snippet.Id.ToLowerInvariant().Contains(targetSnippet))
                        continue;

                    var variants = snippet.Variants.Count > 0
                        ? snippet.Variants.ToList()
                        : new List<DocCodeLanguageVariant>
                        {
                            new() { Language = snippet.Language, DisplayLabel = snippet.Language, Code = snippet.Code }
                        };

                    foreach (var variant in variants)
                    {
                        var langId = variant.Language.ToLowerInvariant();
                        if (!string.IsNullOrEmpty(targetLang) && targetLang != "all" && langId != targetLang)
                            continue;

                        Console.WriteLine($"\n==================================================");
                        Console.WriteLine($"Testing: {snippet.Id} [{variant.DisplayLabel}]");
                        Console.WriteLine($"==================================================");

                        var (passed, details) = TestSnippet(tempRoot, snippet.Id, variant, options);
                        results.Add((snippet.Id, variant.DisplayLabel, passed, details));
                    }
                }
            }

            // Print summary table
            Console.WriteLine("\n\n######################################################################");
            Console.WriteLine("                  DIAGRAM TEST MATRIX SUMMARY                         ");
            Console.WriteLine("######################################################################\n");
            Console.WriteLine($"{"Snippet ID",-32} | {"Lang",-8} | {"Status",-8} | Details");
            Console.WriteLine(new string('-', 85));

            foreach (var r in results)
            {
                var statusStr = r.Passed ? "✅ PASS" : "❌ FAIL";
                Console.WriteLine($"{r.SnippetId,-32} | {r.Language,-8} | {statusStr,-8} | {r.Details}");
            }

            var passCount = results.Count(r => r.Passed);
            var failCount = results.Count(r => !r.Passed);
            Console.WriteLine(new string('-', 85));
            Console.WriteLine($"Total: {results.Count} | Passed: {passCount} | Failed: {failCount}\n");
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static (bool Passed, string Details) TestSnippet(string tempRoot, string snippetId, DocCodeLanguageVariant variant, Options options)
    {
        var langId = variant.Language.ToLowerInvariant();
        var ext = langId switch
        {
            "csharp" or "cs" => ".cs",
            "python" or "py" or "python3" => ".py",
            "java" => ".java",
            "cpp" or "c++" => ".cpp",
            "javascript" or "js" => ".js",
            "rust" or "rs" => ".rs",
            "go" => ".go",
            "fsharp" or "fs" or "fsx" => ".fsx",
            _ => ".txt"
        };

        var fileName = langId == "java" ? "Solution.java" : $"script_{snippetId}{ext}";
        var filePath = Path.Combine(tempRoot, fileName);
        File.WriteAllText(filePath, variant.Code);

        var languages = new StudioLanguageServices(Snapshot.TempFolder("diagram_langs"));
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("diagram_scripts"), languages.Registry);

        var sourceLanguage = languages.Registry.FindSourceFileLanguage(filePath);
        var script = sourceLanguage == null
            ? new ScriptDocumentItem { Title = Path.GetFileName(filePath), Code = variant.Code }
            : new ScriptDocumentItem { Title = Path.GetFileName(filePath), SourceFilePath = filePath };

        var vm = new CSharpCodeStudioViewModel(
            script,
            storage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { },
            blindProgress: new LocalBlindProgressService(Snapshot.TempFolder("blind75-progress")),
            languages: languages);

        if (sourceLanguage != null)
        {
            var copy = Path.Combine(storage.LibraryRootPath, Path.GetFileName(filePath));
            File.Copy(filePath, copy, overwrite: true);
            Snapshot.Wait(vm.RefreshExplorerAsync());
            var item = vm.ExplorerRootItems.First(i => string.Equals(i.Name, Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));
            Snapshot.Wait(vm.SwitchToScriptAsync(item));
        }
        else
        {
            vm.Code = variant.Code;
        }

        var window = Snapshot.Show(new CSharpCodeStudioView { DataContext = vm }, options.Int("width", 1200), options.Int("height", 800));

        try
        {
            var runTask = vm.RunCodeCommand.ExecuteAsync(null);
            Snapshot.Wait(runTask);
            Snapshot.Settle();

            var richCount = vm.RichOutputs.Count;
            var dumpCount = vm.DumpResults.Count;
            var status = vm.CompilerStatusText;
            var errors = vm.ErrorCount;
            var consoleOutput = vm.ConsoleOutput ?? string.Empty;

            var hasVisual = richCount > 0 || dumpCount > 0;

            if (hasVisual)
            {
                vm.SelectedBottomTabIndex = 0;
                vm.IsBottomDeckExpanded = true;
                Snapshot.Settle();

                var imgName = $"diagram_{snippetId}_{langId}";
                Snapshot.Save(window, options, imgName);
                return (true, $"Visual emitted ({richCount} rich, {dumpCount} dumps). Status: {status}");
            }
            else
            {
                string failureReason = "";
                if (errors > 0)
                {
                    var diag = vm.Diagnostics.FirstOrDefault(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
                    failureReason = diag != null ? $"{diag.Message} (Line {diag.Line})" : $"{errors} errors reported";
                }
                else if (status.Contains("Failed", StringComparison.OrdinalIgnoreCase) || status.Contains("Exit", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = consoleOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var errLine = lines.LastOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || l.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                               ?? (lines.Length > 0 ? lines[^1] : status);
                    failureReason = $"{status}: {errLine.Trim()}";
                }
                else
                {
                    failureReason = $"No visual output rendered (Status: {status})";
                }

                return (false, failureReason);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
        finally
        {
            window.Close();
        }
    }
}
