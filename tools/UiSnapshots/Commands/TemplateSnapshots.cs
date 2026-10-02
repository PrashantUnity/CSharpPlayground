using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary><c>templates</c>: runs and verifies that all gallery template code executes correctly without errors.</summary>
internal static class TemplateSnapshots
{
    public static void Run(Options options)
    {
        var allTemplates = CodeTemplateLibrary.GetTemplates();
        string? filter = options.Value("template") ?? options.Value("filter") ?? options.Positional(0);
        string? langFilter = options.Value("language");
        string? catFilter = options.Value("category");
        bool saveShots = options.Flag("shots");
        bool quiet = options.Flag("quiet");

        string? normalizedLangFilter = !string.IsNullOrWhiteSpace(langFilter) ? NormalizeLanguageId(langFilter) : null;

        var templates = allTemplates.Where(t =>
        {
            string tLang = t.LanguageId ?? "csharp";
            if (filter != null)
            {
                string normFilter = NormalizeLanguageId(filter);
                bool matches = t.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                               t.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                               tLang.Equals(normFilter, StringComparison.OrdinalIgnoreCase);
                if (!matches) return false;
            }
            if (normalizedLangFilter != null)
            {
                if (!tLang.Equals(normalizedLangFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (catFilter != null && !t.Category.Contains(catFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }).ToList();

        if (templates.Count == 0)
        {
            Console.WriteLine($"No templates matched the filter criteria.");
            return;
        }

        Console.WriteLine($"Checking {templates.Count} template(s) in CodeTemplateLibrary...\n");

        int passed = 0;
        int failed = 0;
        int skipped = 0;
        var failedTemplates = new List<(CodeTemplate Template, string Error)>();
        var totalSw = Stopwatch.StartNew();

        var languages = new StudioLanguageServices(Snapshot.TempFolder("templates-lang"));
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("templates-storage"), languages.Registry);

        for (int i = 0; i < templates.Count; i++)
        {
            var template = templates[i];
            var sw = Stopwatch.StartNew();
            string lang = template.LanguageId ?? "csharp";
            string indexStr = $"[{i + 1}/{templates.Count}]";

            if (lang == "csharp")
            {
                var result = CheckCSharpTemplate(template, saveShots, options, languages, storage);
                sw.Stop();
                if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"{indexStr,8} [PASS] {template.Id,-42} ({sw.ElapsedMilliseconds}ms) {template.Title}");
                    if (!quiet && !string.IsNullOrWhiteSpace(result.Output))
                    {
                        PrintIndentedOutput(result.Output);
                    }
                }
                else
                {
                    failed++;
                    failedTemplates.Add((template, result.Error ?? "Execution failed"));
                    Console.WriteLine($"{indexStr,8} [FAIL] {template.Id,-42} ({sw.ElapsedMilliseconds}ms) {template.Title}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"         Error: {result.Error}");
                    Console.ResetColor();
                }
            }
            else
            {
                var result = CheckExternalLanguageTemplate(template, saveShots, options, languages, storage);
                sw.Stop();
                if (result.Skipped)
                {
                    skipped++;
                    Console.WriteLine($"{indexStr,8} [SKIP] {template.Id,-42} ({sw.ElapsedMilliseconds}ms) {result.Reason}");
                }
                else if (result.Success)
                {
                    passed++;
                    Console.WriteLine($"{indexStr,8} [PASS] {template.Id,-42} ({sw.ElapsedMilliseconds}ms) {template.Title}");
                    if (!quiet && !string.IsNullOrWhiteSpace(result.Output))
                    {
                        PrintIndentedOutput(result.Output);
                    }
                }
                else
                {
                    failed++;
                    failedTemplates.Add((template, result.Error ?? "Execution failed"));
                    Console.WriteLine($"{indexStr,8} [FAIL] {template.Id,-42} ({sw.ElapsedMilliseconds}ms) {template.Title}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"         Error: {result.Error}");
                    Console.ResetColor();
                }
            }
        }

        totalSw.Stop();
        Console.WriteLine("\n========================================================");
        Console.WriteLine($"Template Verification Summary ({totalSw.Elapsed.TotalSeconds:F2}s total):");
        Console.WriteLine($"  Total:   {templates.Count}");
        Console.WriteLine($"  Passed:  {passed}");
        Console.WriteLine($"  Failed:  {failed}");
        Console.WriteLine($"  Skipped: {skipped}");
        Console.WriteLine("========================================================");

        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nFailed Templates Details:");
            foreach (var (t, err) in failedTemplates)
            {
                Console.WriteLine($"  • [{t.Id}] {t.Title}");
                Console.WriteLine($"    {err.Replace("\n", "\n    ")}");
            }
            Console.ResetColor();
            throw new InvalidOperationException($"{failed} template(s) failed validation.");
        }
    }

    private static (bool Success, string? Error, string? Output) CheckCSharpTemplate(
        CodeTemplate template,
        bool saveShots,
        Options options,
        StudioLanguageServices languages,
        LocalScriptStorageService storage)
    {
        try
        {
            var kernel = new NotebookExecutionKernel();
            var outputs = new List<RichCellOutput>();
            var consoleOutput = new StringBuilder();

            var csharpSnippets = template.Cells != null && template.Cells.Count > 0
                ? template.Cells
                    .Where(c => c.Type == CellType.Code && (c.Language == null || c.Language.Equals("csharp", StringComparison.OrdinalIgnoreCase)))
                    .Select(c => c.Source)
                    .ToList()
                : new List<string> { template.InitialCode };

            foreach (var code in csharpSnippets)
            {
                var result = Snapshot.Wait(Task.Run(() => kernel.ExecuteCellAsync(code, onRichOutput: outputs.Add)));
                if (!string.IsNullOrWhiteSpace(result.ConsoleOutput))
                {
                    consoleOutput.AppendLine(result.ConsoleOutput.Trim());
                }

                if (!result.Success)
                {
                    string err = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "Kernel reported unsuccessful execution"
                        : result.ErrorMessage;
                    return (false, err, consoleOutput.ToString());
                }
            }

            // Also check any polyglot cells in notebook templates
            if (template.Cells != null)
            {
                var nonCSharpCells = template.Cells
                    .Where(c => c.Type == CellType.Code && c.Language != null && !c.Language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var cell in nonCSharpCells)
                {
                    var fakeDoc = new CodeTemplate
                    {
                        Id = $"{template.Id}_{cell.Language}",
                        Title = $"{template.Title} ({cell.Language} cell)",
                        LanguageId = cell.Language,
                        InitialCode = cell.Source
                    };
                    var cellRes = CheckExternalLanguageTemplate(fakeDoc, saveShots, options, languages, storage);
                    if (!cellRes.Success && !cellRes.Skipped)
                    {
                        return (false, $"Cell ({cell.Language}) failed: {cellRes.Error}", consoleOutput.ToString());
                    }
                    if (!string.IsNullOrWhiteSpace(cellRes.Output))
                    {
                        consoleOutput.AppendLine(cellRes.Output.Trim());
                    }
                }
            }

            // If visualizers or visuals are present, check their render models
            int vizIndex = 0;
            var visualizers = outputs
                .Where(o => o.Visual?.Spec is VisualizerSpec)
                .Select(o => VisualizerRenderModelBuilder.Build((VisualizerSpec)o.Visual!.Spec))
                .ToList();

            if (saveShots && visualizers.Count > 0)
            {
                foreach (var visualizer in visualizers)
                {
                    vizIndex++;
                    var sequence = visualizer.Sequence;
                    int total = sequence?.TotalSteps ?? 1;
                    sequence?.SeekStep(0);
                    var content = new ScrollViewer
                    {
                        Content = new Border
                        {
                            Padding = new Thickness(10),
                            Child = new InteractiveVisualizerControl(visualizer)
                        }
                    };
                    var window = Snapshot.Show(content, options.Int("width", 900), options.Int("height", 640));
                    Snapshot.Save(window, $"template_{template.Id}_v{vizIndex}");
                    window.Close();
                }
            }

            return (true, null, consoleOutput.ToString());
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}", null);
        }
    }

    private static (bool Success, bool Skipped, string? Reason, string? Error, string? Output) CheckExternalLanguageTemplate(
        CodeTemplate template,
        bool saveShots,
        Options options,
        StudioLanguageServices languages,
        LocalScriptStorageService storage)
    {
        try
        {
            var script = Snapshot.Wait(storage.CreateNewSourceFileAsync(template.LanguageId!, template.Id, null, template.InitialCode));
            if (script == null || string.IsNullOrEmpty(script.SourceFilePath) || !File.Exists(script.SourceFilePath))
            {
                return (false, false, null, "Failed to create source file for template", null);
            }

            var language = languages.Registry.Get(template.LanguageId!);
            if (language == null)
            {
                return (false, true, $"Language '{template.LanguageId}' not registered", null, null);
            }

            if (language.ScriptRunner == null)
            {
                return (false, true, $"Language '{template.LanguageId}' has no ScriptRunner", null, null);
            }

            var workDir = Path.GetDirectoryName(script.SourceFilePath)!;
            ToolchainInfo? toolchain = null;
            if (language.Toolchain != null)
            {
                var resolution = Snapshot.Wait(language.Toolchain.ResolveAsync(new ToolchainQuery(workDir)));
                toolchain = resolution.Toolchain;
                if (toolchain == null)
                {
                    return (false, true, $"{language.DisplayName} toolchain not found on system", null, null);
                }
            }

            var defaultToolchain = new ToolchainInfo
            {
                LanguageId = language.Id,
                ExecutablePath = "default",
                Version = new Version(1, 0),
                DisplayName = language.DisplayName,
                Source = "default"
            };
            var context = new ScriptRunContext(script.SourceFilePath, workDir, toolchain ?? defaultToolchain);
            var plan = Snapshot.Wait(language.ScriptRunner.PlanAsync(context));

            var outputBuffer = new StringBuilder();
            var executor = new ScriptRunExecutor(languages.Processes);
            var session = executor.Start(plan, script.SourceFilePath, language.RunDiagnostics, text => outputBuffer.Append(text));
            var runResult = Snapshot.Wait(session.Completion);

            if (!runResult.Succeeded)
            {
                var errDetail = new StringBuilder();
                if (runResult.StartError != null) errDetail.AppendLine(runResult.StartError);
                if (runResult.FailedBuildStep != null) errDetail.AppendLine($"Build step failed: {runResult.FailedBuildStep}");
                if (runResult.ExitCode != null && runResult.ExitCode != 0) errDetail.AppendLine($"Process exited with code {runResult.ExitCode}");
                if (outputBuffer.Length > 0) errDetail.AppendLine(outputBuffer.ToString().Trim());

                return (false, false, null, errDetail.ToString().Trim(), outputBuffer.ToString());
            }

            if (saveShots && outputBuffer.Length > 0)
            {
                RenderVisualOutputsFromText(outputBuffer.ToString(), template.Id, options);
            }

            return (true, false, null, null, outputBuffer.ToString());
        }
        catch (Exception ex)
        {
            return (false, false, null, $"{ex.GetType().Name}: {ex.Message}", null);
        }
    }

    private static void RenderVisualOutputsFromText(string text, string templateId, Options options)
    {
        var lines = text.Split('\n');
        int visualIndex = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("__FRY_DISPLAY__", StringComparison.Ordinal)) continue;
            var json = trimmed.Substring("__FRY_DISPLAY__".Length).Trim();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    using var metaDoc = JsonDocument.Parse("{}");
                    var rich = MimeOutputMapper.Map(dataElem, metaDoc.RootElement).Rich;
                    if (rich?.Visual != null)
                    {
                        visualIndex++;
                        var view = new VisualOutputView { Output = rich.Visual };
                        int width = options.Int("width", 900);
                        int height = options.Int("height", 600);
                        var window = Snapshot.Show(new ScrollViewer { Content = new Border { Padding = new Thickness(10), Child = view } }, width, height);
                        Snapshot.WaitFor(() => !view.IsPreparing, TimeSpan.FromSeconds(10));
                        Snapshot.Save(window, $"template_{templateId}_v{visualIndex}");
                        window.Close();
                    }
                }
            }
            catch
            {
                // Ignore rendering failures on secondary display frames
            }
        }
    }

    private static void PrintIndentedOutput(string output)
    {
        var lines = output.Split('\n');
        int count = 0;
        foreach (var line in lines)
        {
            if (count++ > 5)
            {
                Console.WriteLine($"         ... ({lines.Length - 5} more lines)");
                break;
            }
            Console.WriteLine($"         | {line.TrimEnd()}");
        }
    }

    private static string NormalizeLanguageId(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "";
        return lang.Trim().ToLowerInvariant() switch
        {
            "f#" => "fsharp",
            "c#" => "csharp",
            "c++" => "cpp",
            "js" => "javascript",
            "py" => "python",
            "rs" => "rust",
            var other => other
        };
    }
}
