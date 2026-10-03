using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class DartExtensionTests : IDisposable
{
    private readonly string _extensionPath;

    public DartExtensionTests()
    {
        try { StudioAppContext.Instance.Languages.Unregister("dart"); } catch { }

        _extensionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../samples/extensions/dart-support"));
        if (!Directory.Exists(_extensionPath))
        {
            // Fallback for direct repo root resolution
            _extensionPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples/extensions/dart-support"));
        }
    }

    public void Dispose()
    {
        // Ensure cleanly unloaded if left registered
        StudioAppContext.Instance.Languages.Unregister("dart");
    }

    [Fact]
    public void DartExtension_DirectoryAndManifest_Exist()
    {
        Assert.True(Directory.Exists(_extensionPath), $"Extension folder not found at: {_extensionPath}");
        var manifestPath = Path.Combine(_extensionPath, "extension.json");
        Assert.True(File.Exists(manifestPath), $"extension.json not found at: {manifestPath}");
    }

    [Fact]
    public async Task DartExtension_CompilesAndLoadsViaExtensionManager()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);

        Assert.True(loadResult.Success, $"Extension load failed: {loadResult.ErrorMessage} - Diagnostics: {string.Join("; ", loadResult.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(loadResult.Extension);

        // Verify the language is registered in the studio SDK
        var app = StudioAppContext.Instance;
        var dartLang = app.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.Equal("Dart", dartLang.DisplayName);
        Assert.Equal("DART", dartLang.ShortName);
        Assert.Equal("#0175C2", dartLang.AccentHex);
        Assert.Contains(".dart", dartLang.FileExtensions);

        // Verify all required IDE capabilities are enabled
        Assert.True(dartLang.Capabilities.HasFlag(LanguageCapabilities.NotebookCells));
        Assert.True(dartLang.Capabilities.HasFlag(LanguageCapabilities.LiveDiagnostics));
        Assert.True(dartLang.Capabilities.HasFlag(LanguageCapabilities.Packages));
        Assert.True(dartLang.Capabilities.HasFlag(LanguageCapabilities.StandardInput));
        Assert.True(dartLang.Capabilities.HasFlag(LanguageCapabilities.Templates));

        // Verify all subsystem hooks are populated
        Assert.NotNull(dartLang.NotebookKernels);
        Assert.NotNull(dartLang.Toolchain);
        Assert.NotNull(dartLang.RunDiagnostics);
        Assert.NotNull(dartLang.Packages);
        Assert.NotNull(dartLang.ScriptRunner);

        // Verify syntax themes
        var darkHighlighting = dartLang.GetHighlighting(isDark: true);
        var lightHighlighting = dartLang.GetHighlighting(isDark: false);
        Assert.NotNull(darkHighlighting);
        Assert.NotNull(lightHighlighting);
        Assert.Equal("Dart Dark", darkHighlighting.Name);
        Assert.Equal("Dart Light", lightHighlighting.Name);

        // Verify clean unregistration on unload
        bool unloaded = await manager.UnloadExtensionAsync("dart-support");
        Assert.True(unloaded);
        Assert.Null(app.Languages.Get("dart"));
        Assert.Null(app.Languages.FindByExtension(".dart"));
    }

    [Fact]
    public async Task DartExtension_DiagnosticParser_ParsesCompilerErrorsAndMissingPackages()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.NotNull(dartLang.RunDiagnostics);

        string sampleOutput = """
            lib/main.dart:15:3: Error: A value of type 'String' can't be assigned to a variable of type 'int'.
              int x = "hello";
                  ^
            Because my_app depends on http any which doesn't exist (could not find package http at https://pub.dev), version solving failed.
            """;

        var result = dartLang.RunDiagnostics.Parse(sampleOutput, "/projects/my_app/lib/main.dart");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(15, diag.Line);
        Assert.Equal(3, diag.Column);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("can't be assigned", diag.Message);
        Assert.Equal("http", result.MissingDependency);

        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_DiagnosticParser_ParsesStackTraceFrames()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.NotNull(dartLang.RunDiagnostics);

        string sampleTrace = """
            Unhandled exception:
            Exception: Something went wrong
            #0      main (/workspace/test.dart:42:10)
            #1      _delayEntrypointInvocation (dart:isolate-patch/isolate_patch.dart:297:19)
            """;

        var result = dartLang.RunDiagnostics.Parse(sampleTrace, "/workspace/test.dart");
        Assert.NotEmpty(result.Diagnostics);
        var frame = result.Diagnostics.FirstOrDefault(d => d.Line == 42);
        Assert.NotNull(frame);
        Assert.Equal(10, frame.Column);
        Assert.Equal(DiagnosticSeverity.Error, frame.Severity);

        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_PackageManager_ParsesDirectives()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        var pkgManager = dartLang.Packages;
        Assert.NotNull(pkgManager);

        Assert.True(pkgManager.TryParseDirective("%pub add dio", out var cmd1));
        Assert.Equal(["pub", "add", "dio"], cmd1.Arguments);

        Assert.True(pkgManager.TryParseDirective("%dart pub add http", out var cmd2));
        Assert.Equal(["pub", "add", "http"], cmd2.Arguments);

        Assert.True(pkgManager.TryParseDirective("// #dart: provider", out var cmd3));
        Assert.Equal(["pub", "add", "provider"], cmd3.Arguments);

        Assert.False(pkgManager.TryParseDirective("var x = 10;", out _));

        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_ScriptRunner_PlansProcessRun()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.NotNull(dartLang.ScriptRunner);

        var toolchainInfo = new ToolchainInfo
        {
            LanguageId = "dart",
            ExecutablePath = "/usr/local/bin/dart",
            Version = new Version(3, 4, 1),
            DisplayName = "Dart 3.4.1",
            Source = "System"
        };

        var plan = await dartLang.ScriptRunner.PlanAsync(new ScriptRunContext(
            "/workspace/main.dart",
            "/workspace",
            toolchainInfo));

        Assert.Single(plan.Steps);
        var step = plan.Steps[0];
        Assert.False(step.IsBuildStep);
        Assert.Equal("/usr/local/bin/dart", step.Spec.FileName);
        Assert.Equal(["run", "/workspace/main.dart"], step.Spec.Arguments);

        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_NotebookKernel_ManagesVariableSharing()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.NotNull(dartLang.NotebookKernels);

        var kernel = dartLang.NotebookKernels.Create(new KernelCreationContext(() => "/tmp"));
        Assert.Equal("dart", kernel.LanguageId);
        Assert.Equal("Dart (SDK)", kernel.DisplayName);
        Assert.True(kernel.CanForceStop);

        // Test cross-kernel JSON variable setting
        await kernel.SetValueFromJsonAsync("myVar", "{\"count\": 42, \"name\": \"FrySharp\"}", CancellationToken.None);
        var retrievedJson = await kernel.GetValueJsonAsync("myVar", CancellationToken.None);
        Assert.Contains("42", retrievedJson);

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Single(variables);
        Assert.Equal("myVar", variables[0].Name);

        kernel.Dispose();
        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_Toolchain_ProducesActionableMissingGuidance()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        Assert.NotNull(dartLang.Toolchain);

        var resolution = await dartLang.Toolchain.ResolveAsync(new ToolchainQuery("/nonexistent_folder_xyz"));
        if (!resolution.IsFound)
        {
            Assert.NotNull(resolution.Missing);
            Assert.Contains("Dart", resolution.Missing.Title);
            Assert.NotEmpty(resolution.Missing.Steps);
            Assert.Equal("https://dart.dev/get-dart", resolution.Missing.DownloadUrl);
        }

        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_ReactiveUI_InvalidatesLanguageChoices()
    {
        var app = StudioAppContext.Instance;
        int registeredEvents = 0;
        int unregisteredEvents = 0;

        void OnReg(ILanguageDefinition l) { if (l.Id == "dart") registeredEvents++; }
        void OnUnreg(ILanguageDefinition l) { if (l.Id == "dart") unregisteredEvents++; }

        app.LanguageServices.Registry.LanguageRegistered += OnReg;
        app.LanguageServices.Registry.LanguageUnregistered += OnUnreg;

        try
        {
            using var manager = new ExtensionManager();
            var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
            Assert.True(loadResult.Success);

            Assert.Equal(1, registeredEvents);
            Assert.Contains(app.Languages.All, l => l.Id == "dart");
            Assert.Contains(app.Languages.SourceFileLanguages, l => l.Id == "dart");

            // Notebook cell choices
            var notebookLangs = app.Languages.All.Where(l => l.NotebookKernels != null && l.Has(LanguageCapabilities.NotebookCells)).ToList();
            Assert.Contains(notebookLangs, l => l.Id == "dart");

            await manager.UnloadExtensionAsync("dart-support");

            Assert.Equal(1, unregisteredEvents);
            Assert.DoesNotContain(app.Languages.All, l => l.Id == "dart");
        }
        finally
        {
            app.LanguageServices.Registry.LanguageRegistered -= OnReg;
            app.LanguageServices.Registry.LanguageUnregistered -= OnUnreg;
        }
    }

    [Fact]
    public async Task DartExtension_NotebookDemoFile_LoadsAsValidNotebookDocument()
    {
        var nbPath = Path.Combine(_extensionPath, "samples/dart_notebook_demo.csnb");
        Assert.True(File.Exists(nbPath), $"Notebook file not found at: {nbPath}");

        var json = await File.ReadAllTextAsync(nbPath);
        var nb = System.Text.Json.JsonSerializer.Deserialize<NotebookDocumentItem>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(nb);
        Assert.Equal("Dart Polyglot Notebook Showcase", nb.Title);
        Assert.Equal("dart", nb.Kernel);
        Assert.Equal(6, nb.Cells.Count);

        var mdCell = nb.Cells[0];
        Assert.Equal(CellType.Markdown, mdCell.Type);

        var codeCell1 = nb.Cells[1];
        Assert.Equal(CellType.Code, codeCell1.Type);
        Assert.Equal("dart", codeCell1.Language);
        Assert.Contains("class Item", codeCell1.Source);

        var codeCell2 = nb.Cells[2];
        Assert.Equal(CellType.Code, codeCell2.Type);
        Assert.Equal("dart", codeCell2.Language);
        Assert.Contains("Display.barChart", codeCell2.Source);

        var codeCell3 = nb.Cells[3];
        Assert.Equal(CellType.Code, codeCell3.Type);
        Assert.Equal("dart", codeCell3.Language);
        Assert.Contains("Display.surface", codeCell3.Source);

        var codeCell4 = nb.Cells[4];
        Assert.Equal(CellType.Code, codeCell4.Type);
        Assert.Equal("dart", codeCell4.Language);
        Assert.Contains("Visualizer.array", codeCell4.Source);

        var codeCell5 = nb.Cells[5];
        Assert.Equal(CellType.Code, codeCell5.Type);
        Assert.Equal("dart", codeCell5.Language);
        Assert.Contains("Display.table", codeCell5.Source);
    }

    [Fact]
    public async Task DartExtension_NotebookKernel_ExecutesVisualsAndRichOutputs()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        var kernel = dartLang.NotebookKernels!.Create(new KernelCreationContext(() => "/tmp"));

        var richOutputs = new System.Collections.Generic.List<RichCellOutput>();
        var consoleOutput = new System.Text.StringBuilder();

        // 1. Test 2D Charting
        var chartCode = """
            var prices = [29.99, 14.50, 129.00, 45.20];
            Display.lineChart(prices, 'Revenue Trend');
            """;
        var res1 = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = chartCode,
            OnRichOutput = richOutputs.Add,
            OnConsole = s => consoleOutput.Append(s)
        }, CancellationToken.None);

        Assert.True(res1.Success, $"Execution failed: {res1.ErrorMessage}");
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Chart, richOutputs[0].Kind);
        Assert.NotNull(richOutputs[0].Visual);
        richOutputs.Clear();

        // 2. Test 3D Surface
        var surfaceCode = """
            Display.surface((x, y) => x * x - y * y, 'Saddle Surface');
            """;
        var res2 = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = surfaceCode,
            OnRichOutput = richOutputs.Add,
            OnConsole = s => consoleOutput.Append(s)
        }, CancellationToken.None);

        Assert.True(res2.Success, $"Execution failed: {res2.ErrorMessage}");
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Plot3D, richOutputs[0].Kind);
        Assert.NotNull(richOutputs[0].Visual);
        richOutputs.Clear();

        // 3. Test Visualizer
        var visualizerCode = """
            Visualizer.array([10, 20, 30, 40], {'i': 1, 'j': 3}, 'Two Pointers');
            """;
        var res3 = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = visualizerCode,
            OnRichOutput = richOutputs.Add,
            OnConsole = s => consoleOutput.Append(s)
        }, CancellationToken.None);

        Assert.True(res3.Success, $"Execution failed: {res3.ErrorMessage}");
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Visualizer, richOutputs[0].Kind);
        Assert.NotNull(richOutputs[0].Visual);
        richOutputs.Clear();

        // 4. Test Table
        var tableCode = """
            var tableData = [
              {'Product': 'Dart Book', 'Qty': 2, 'Price': 29.99},
              {'Product': 'Flutter Mug', 'Qty': 5, 'Price': 14.50}
            ];
            Display.table(tableData, 'Inventory');
            """;
        var res4 = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = tableCode,
            OnRichOutput = richOutputs.Add,
            OnConsole = s => consoleOutput.Append(s)
        }, CancellationToken.None);

        Assert.True(res4.Success, $"Execution failed: {res4.ErrorMessage}");
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Table, richOutputs[0].Kind);
        Assert.NotNull(richOutputs[0].TableResult);
        Assert.Equal("Inventory", richOutputs[0].TableResult!.Title);

        kernel.Dispose();
        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_The18Cases_DrawAsTheFixturesDo()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        var kernel = dartLang.NotebookKernels!.Create(new KernelCreationContext(() => "/tmp"));

        var richOutputs = new System.Collections.Generic.List<RichCellOutput>();
        var consoleOutput = new System.Text.StringBuilder();

        var the18Calls = """
            class T { dynamic val; T? left; T? right; T(this.val, [this.left, this.right]); }
            class L { dynamic val; L? next; L(this.val, [this.next]); }

            Display.lineChart([3, 1, null, 4], "Numbers");
            Display.scatterChart([[1.0, 2], [2.0, 4.5]], "Pairs");
            Display.barChart({"Mon": 3, "Tue": 5}, "Days");
            Display.chart({"a": [1, 2], "b": [3, 4]}, "Series");
            Display.chart([{"name": "Jan", "value": 10}, {"name": "Feb", "value": 12}], "Records");
            Display.histogram([1, 2, 2, 3, 3, 3], "Samples", {"bins": 3});
            Display.pieChart({"A": 1, "B": 2}, "Share");
            Display.scatter3d([[1, 2, 3], [4, 5, 6]], "Points");
            Display.surface3d([[1, 2], [3, 4]], "Heights");
            Display.graph3d({"a": ["b", "c"], "b": ["c"]}, "Links");
            Display.matrix([[1, 0], [0, 1]], "Grid");
            Display.array([1, 3, 5], {"i": 0, "j": 2}, "Two pointers");
            Display.tree(T(2, T(1), T(3)), "Tree");
            Display.tree([1, 2, 3, null, 4], "Level order");
            Display.graph({"a": ["b"], "b": ["c"]}, "Graph");
            Display.linkedList(L(1, L(2, L(3))), "List");
            Display.bars([3, 1, 2], "Bars");
            Display.islands([[1, 0], [1, 1]], "Islands");
            """;

        var res = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = the18Calls,
            OnRichOutput = richOutputs.Add,
            OnConsole = s => consoleOutput.Append(s)
        }, CancellationToken.None);

        Assert.True(res.Success, $"Execution failed: {res.ErrorMessage}\nConsole:\n{consoleOutput}");
        Assert.Equal(18, richOutputs.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.Cases.Length; i++)
        {
            var name = VisualConformanceFixturesTests.Cases[i].Name;
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = richOutputs[i].Visual!;
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }

        kernel.Dispose();
        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_VisualUpdate_RedrawsSingleVisual()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);
        var kernel = dartLang.NotebookKernels!.Create(new KernelCreationContext(() => "/tmp"));

        var richOutputs = new System.Collections.Generic.List<RichCellOutput>();
        var updateCode = """
            final h = Display.lineChart([1, 2], "A");
            h.update("B");
            """;

        var res = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = updateCode,
            OnRichOutput = richOutputs.Add
        }, CancellationToken.None);

        Assert.True(res.Success, $"Execution failed: {res.ErrorMessage}");
        var visual = Assert.Single(richOutputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);

        kernel.Dispose();
        await manager.UnloadExtensionAsync("dart-support");
    }

    [Fact]
    public async Task DartExtension_ScriptRunner_PlansAndInjectsDisplayRuntime()
    {
        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success);

        var dartLang = StudioAppContext.Instance.Languages.Get("dart");
        Assert.NotNull(dartLang);

        var tempDir = Path.Combine(Path.GetTempPath(), "dart_script_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var scriptPath = Path.Combine(tempDir, "main.dart");
            await File.WriteAllTextAsync(scriptPath, "void main() { print('hello'); }");

            var resolution = await dartLang.Toolchain!.ResolveAsync(new ToolchainQuery(tempDir, tempDir));
            Assert.NotNull(resolution.Toolchain);
            var context = new ScriptRunContext(scriptPath, tempDir, resolution.Toolchain!);
            var plan = await dartLang.ScriptRunner!.PlanAsync(context);

            Assert.NotNull(plan);
            Assert.Single(plan.Steps);
            Assert.Equal("run", plan.Steps[0].Label);

            // Verify runtime display files were automatically injected for the script
            Assert.True(File.Exists(Path.Combine(tempDir, "display.dart")));
            Assert.True(File.Exists(Path.Combine(tempDir, "fry.dart")));
            Assert.True(File.Exists(Path.Combine(tempDir, "fry_display.dart")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
            await manager.UnloadExtensionAsync("dart-support");
        }
    }

    [Fact]
    public async Task DartExtension_PopulatesInSettingsViewModel_WhenLoaded()
    {
        using var manager = new ExtensionManager();
        var prevServices = StudioAppContext.Instance.LanguageServices;
        var services = new StudioLanguageServices(Path.GetTempPath());
        StudioAppContext.Instance.LanguageServices = services;
        var settingsVm = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.CSharpSettingsViewModel(services);

        Assert.DoesNotContain(settingsVm.Languages, l => l.Language.Id == "dart");

        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success, loadResult.ErrorMessage);

        try
        {
            settingsVm.SynchronizeLanguagesFromRegistry();
            Assert.Contains(settingsVm.Languages, l => l.Language.Id == "dart");
            var item = settingsVm.Languages.First(l => l.Language.Id == "dart");
            Assert.True(item.IsExtensionLanguage);
            Assert.Equal("Dart", item.DisplayName);
            Assert.Equal("#0175C2", item.AccentHex);
        }
        finally
        {
            await manager.UnloadExtensionAsync("dart-support");
            settingsVm.SynchronizeLanguagesFromRegistry();
            Assert.DoesNotContain(settingsVm.Languages, l => l.Language.Id == "dart");
            StudioAppContext.Instance.LanguageServices = prevServices;
        }
    }

    [Fact]
    public async Task DartExtension_RegistersDocumentationCategory_WithInteractiveSnippets()
    {
        using var manager = new ExtensionManager();
        var docService = PdfEditorApp.Plugins.CSharpEditor.Services.Documentation.DocumentationService.Instance;

        Assert.Null(docService.GetArticle("dart_getting_started"));

        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success, loadResult.ErrorMessage);

        try
        {
            var article = docService.GetArticle("dart_getting_started");
            Assert.NotNull(article);
            Assert.Equal("Dart 3 Interactive Scripting", article.Title);
            Assert.NotEmpty(article.CodeSnippets);
            Assert.Contains(article.CodeSnippets, s => s.Language == "dart");
        }
        finally
        {
            await manager.UnloadExtensionAsync("dart-support");
            Assert.Null(docService.GetArticle("dart_getting_started"));
        }
    }

    [Fact]
    public async Task DartExtension_PopulatesInHubStudioEnvironment_WhenLoaded()
    {
        using var manager = new ExtensionManager();
        var prevServices = StudioAppContext.Instance.LanguageServices;
        var services = new StudioLanguageServices(Path.GetTempPath());
        StudioAppContext.Instance.LanguageServices = services;

        var tempStorage = Path.Combine(Path.GetTempPath(), "storage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempStorage);
        var storage = new PdfEditorApp.Plugins.CSharpEditor.Services.Storage.LocalScriptStorageService(
            tempStorage,
            services.Registry);

        var hubVm = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel(
            storage,
            languages: services);

        Assert.DoesNotContain(hubVm.ToolchainStatuses, t => t.Language.Id == "dart");

        var loadResult = await manager.LoadExtensionAsync(_extensionPath, enableHotReload: false);
        Assert.True(loadResult.Success, loadResult.ErrorMessage);

        try
        {
            Assert.Contains(hubVm.ToolchainStatuses, t => t.Language.Id == "dart");
            var item = hubVm.ToolchainStatuses.First(t => t.Language.Id == "dart");
            Assert.Equal("Dart", item.Title);
            Assert.Equal("#0175C2", item.AccentHex);
        }
        finally
        {
            await manager.UnloadExtensionAsync("dart-support");
            Assert.DoesNotContain(hubVm.ToolchainStatuses, t => t.Language.Id == "dart");
            StudioAppContext.Instance.LanguageServices = prevServices;
            try { Directory.Delete(tempStorage, recursive: true); } catch { }
        }
    }
}

