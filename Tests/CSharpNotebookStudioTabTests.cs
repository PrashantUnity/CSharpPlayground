using System;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpNotebookStudioTabTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public CSharpNotebookStudioTabTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_TabTests_" + Guid.NewGuid().ToString("N"));
        _testStorage = new LocalScriptStorageService(_testBaseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    private CSharpNotebookStudioViewModel CreateStudio(NotebookDocumentItem? notebook = null)
    {
        var initialNotebook = notebook ?? new NotebookDocumentItem
        {
            Title = "Document Automation Notebook"
        };
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();

        return new CSharpNotebookStudioViewModel(
            initialNotebook,
            _testStorage,
            compiler,
            engine,
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }
    private ExplorerItemViewModel EnsureSecondNotebookItem(CSharpNotebookStudioViewModel studio, string name = "Second_Notebook.frynb")
    {
        var existing = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == name);
        if (existing != null) return existing;

        var secondItem = new ExplorerItemViewModel
        {
            Name = name,
            DocumentId = "test_doc_second",
            IsDirectory = false,
            FileExtension = ".frynb",
            FullPath = name,
            Parent = null
        };
        studio.ExplorerRootItems.Add(secondItem);
        return secondItem;
    }

    [Fact]
    public void Studio_InitialState_HasInitialTab_AndIsActive()
    {
        var studio = CreateStudio();

        Assert.Single(studio.Tabs);
        Assert.NotNull(studio.ActiveTab);
        Assert.True(studio.HasActiveTab);
        Assert.False(studio.HasNoTabs);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
        Assert.NotEmpty(studio.Cells);
    }

    [Fact]
    public async Task OpenDocument_MultipleFiles_DisplaysMultipleTabs()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);

        await studio.OpenDocumentAsync(secondFile);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
        Assert.False(studio.Tabs[0].IsActive);
    }

    [Fact]
    public async Task OpenDocument_SameFileTwice_DoesNotDuplicateTabs()
    {
        var studio = CreateStudio();

        var docFile = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(docFile);

        await studio.OpenDocumentAsync(docFile);

        Assert.Single(studio.Tabs);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab!.Title);
    }

    [Fact]
    public async Task SelectTab_SwitchesActiveTab_AndUpdatesExplorerSelection()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        await studio.OpenDocumentAsync(secondFile);

        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab!.Title);

        studio.SelectTab(studio.Tabs[0]);

        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.Tabs[0].IsActive);
        Assert.False(studio.Tabs[1].IsActive);

        var firstItem = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(firstItem);
        Assert.True(firstItem.IsSelected);
    }

    [Fact]
    public async Task CloseTab_SwitchesToAdjacentTab()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        await studio.OpenDocumentAsync(secondFile);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab!.Title);

        studio.CloseTab(studio.ActiveTab);

        Assert.Single(studio.Tabs);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
    }

    [Fact]
    public void CloseTab_WhenAllTabsClosed_HasNoTabs()
    {
        var studio = CreateStudio();

        Assert.Single(studio.Tabs);

        studio.CloseTab(studio.Tabs[0]);

        Assert.Empty(studio.Tabs);
        Assert.Null(studio.ActiveTab);
        Assert.True(studio.HasNoTabs);
        Assert.False(studio.HasActiveTab);
    }

    [Fact]
    public void BreadcrumbFormatting_MarkdownCell_CleansMarkdownTokensWithoutDuplicatingOrCSharpPrefix()
    {
        var studio = CreateStudio();

        var activeTab = studio.ActiveTab!;
        var mdCellItem = new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = "# 📓 Polyglot Notebook Demo Copy\nWrite documentation or notes in this cell."
        };
        var mdCellVm = activeTab.CreateCellViewModel(mdCellItem);
        activeTab.Cells.Add(mdCellVm);
        activeTab.SelectCell(mdCellVm);

        Assert.Equal("Library", studio.BreadcrumbFolder);
        Assert.Equal("Document Automation Notebook.frynb", studio.BreadcrumbDocument);
        Assert.Equal("Markdown: 📓 Polyglot Notebook Demo Copy", activeTab.ActiveCellBadgeText);
        Assert.Equal("FormatHeaderPound", activeTab.ActiveCellTypeIcon);
        Assert.Equal("#4EC9B0", activeTab.ActiveCellTypeColor);

        Assert.DoesNotContain("C# #", studio.BreadcrumbText);
        Assert.DoesNotContain("> C#", studio.BreadcrumbText);
    }

    [Fact]
    public void BreadcrumbFormatting_CodeCell_FormatsCleanSnippet()
    {
        var studio = CreateStudio();

        var activeTab = studio.ActiveTab!;
        var codeCellItem = new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "public class People\n{\n}"
        };
        var codeCellVm = activeTab.CreateCellViewModel(codeCellItem);
        codeCellVm.ExecutionCount = 1;
        activeTab.Cells.Add(codeCellVm);
        activeTab.SelectCell(codeCellVm);

        Assert.Equal("Cell [1]: public class People", activeTab.ActiveCellBadgeText);
        Assert.Equal("CodeBraces", activeTab.ActiveCellTypeIcon);
        Assert.Equal("#58A6FF", activeTab.ActiveCellTypeColor);
        Assert.Equal("Library › Document Automation Notebook.frynb › Cell [1]: public class People", studio.BreadcrumbText);
    }

    [Fact]
    public async Task NewNotebookTab_CreatesTabAndAddsToExplorer()
    {
        var studio = CreateStudio();

        var initialCount = studio.Tabs.Count;
        await studio.NewNotebookTab();

        Assert.Equal(initialCount + 1, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.StartsWith("Notebook_", studio.ActiveTab.Title);

        Assert.Contains(studio.ExplorerRootItems, x => x.Name == studio.ActiveTab.Title);
    }

    [Fact]
    public async Task DeleteExplorerItem_ClosesOpenTab()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        await studio.OpenDocumentAsync(secondFile);

        Assert.Equal(2, studio.Tabs.Count);

        await studio.DeleteExplorerItemAsync(secondFile);

        Assert.Single(studio.Tabs);
        Assert.DoesNotContain(studio.Tabs, t => t.Title == "Second_Notebook.frynb");
    }

    [Fact]
    public async Task NewFolder_PersistsAcrossRefresh()
    {
        var studio = CreateStudio();

        await studio.NewFolder();

        var folder = studio.ExplorerRootItems.FirstOrDefault(x => x.IsDirectory);
        Assert.NotNull(folder);
        Assert.Equal("New Folder", folder.Name);

        await studio.RefreshExplorer();

        Assert.Contains(studio.ExplorerRootItems, x => x.IsDirectory && x.Name == "New Folder");
    }

    [Fact]
    public async Task RenameFolder_UpdatesDescendantPaths_AndPersistsAcrossRefresh()
    {
        var studio = CreateStudio();

        await studio.NewFolder();
        var folder = studio.ExplorerRootItems.First(x => x.IsDirectory);

        await studio.NewFileUnderItemAsync(folder);
        var childBeforeRename = folder.Children.First();
        var childId = childBeforeRename.DocumentId;

        folder.Name = "Renamed Folder";
        await studio.OnItemRenamedAsync(folder);

        Assert.Equal("Renamed Folder", folder.FullPath);
        Assert.StartsWith("Renamed Folder/", folder.Children.First().FullPath);

        await studio.RefreshExplorer();

        var renamedFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.IsDirectory && x.Name == "Renamed Folder");
        Assert.NotNull(renamedFolder);
        Assert.Contains(renamedFolder.Children, c => c.DocumentId == childId);
    }

    [Fact]
    public async Task DeleteFolder_CascadesToChildrenAndClosesTabs()
    {
        var studio = CreateStudio();

        await studio.NewFolder();
        var folder = studio.ExplorerRootItems.First(x => x.IsDirectory);

        await studio.NewFileUnderItemAsync(folder);
        var child = folder.Children.First();
        var childId = child.DocumentId!;

        Assert.Equal(2, studio.Tabs.Count);

        await studio.DeleteExplorerItemAsync(folder);

        Assert.DoesNotContain(studio.ExplorerRootItems, x => x.IsDirectory && x.Name == folder.Name);
        Assert.DoesNotContain(studio.Tabs, t => t.Notebook.Id == childId);

        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        Assert.DoesNotContain(summaries, s => s.Id == childId);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteSkiaSharp3_ExecutesSuccessfully()
    {
        var kernel = new NotebookExecutionKernel();
        var code = @"#r ""nuget: SkiaSharp, 3.119.4""
using SkiaSharp;

var info = new SKImageInfo(480, 240);
var surface = SKSurface.Create(info);
var canvas = surface.Canvas;

var paint = new SKPaint { Color = new SKColor(102, 157, 246), IsAntialias = true };
canvas.DrawCircle(100, 120, 50, paint);

Display.Image(surface.Snapshot());
Console.WriteLine(""Success!"");";

        RichCellOutput? emittedRich = null;
        var result = await kernel.ExecuteCellAsync(code, onRichOutput: r => emittedRich = r);
        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.NotNull(emittedRich);
        Assert.Equal(CellOutputKind.Image, emittedRich.Kind);
        Assert.NotNull(emittedRich.ImageBytes);
        Assert.True(emittedRich.ImageBytes.Length > 0);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteSkiaSharpNoVersion_ExecutesSuccessfully()
    {
        var kernel = new NotebookExecutionKernel();
        var code = @"#r ""nuget: SkiaSharp""
using SkiaSharp;

var info = new SKImageInfo(480, 240);
var surface = SKSurface.Create(info);
var canvas = surface.Canvas;

var paint = new SKPaint { Color = new SKColor(102, 157, 246), IsAntialias = true };
canvas.DrawCircle(100, 120, 50, paint);

Display.Image(surface.Snapshot());
Console.WriteLine(""Success!"");";

        RichCellOutput? emittedRich = null;
        var result = await kernel.ExecuteCellAsync(code, onRichOutput: r => emittedRich = r);
        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.NotNull(emittedRich);
        Assert.Equal(CellOutputKind.Image, emittedRich.Kind);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteSkiaSharpLegacy4Preview_GracefullyBindsToHostRuntime()
    {
        var kernel = new NotebookExecutionKernel();
        var code = @"#r ""nuget: SkiaSharp, 4.154.0-preview.1.26454.9""
using SkiaSharp;

var info = new SKImageInfo(300, 150);
var surface = SKSurface.Create(info);
var canvas = surface.Canvas;
canvas.Clear(new SKColor(30, 40, 60));

Display.Image(surface.Snapshot());
Console.WriteLine(""Legacy directive executed safely."");";

        var result = await kernel.ExecuteCellAsync(code);
        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.Contains("Legacy directive executed safely.", result.ConsoleOutput);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteTemplate_SkiaSharpGraphics_ExecutesSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "skiasharp_image_studio");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? emittedRich = null;
        var result = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => emittedRich = r);

        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.NotNull(emittedRich);
        Assert.Equal(CellOutputKind.Image, emittedRich.Kind);
        Assert.NotNull(emittedRich.ImageBytes);
        Assert.True(emittedRich.ImageBytes.Length > 0);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteTemplate_RichHtmlReports_ExecutesSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "rich_html_reports");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();
        RichCellOutput? emittedRich = null;
        var result = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => emittedRich = r);

        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.NotNull(emittedRich);
        Assert.Equal(CellOutputKind.Html, emittedRich.Kind);
        Assert.False(string.IsNullOrEmpty(emittedRich.HtmlContent));
        Assert.Contains("<h1>", emittedRich.HtmlContent);
        Assert.Contains("<b>", emittedRich.HtmlContent);
        Assert.Contains("<a href=", emittedRich.HtmlContent);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteAnyPackage_NewtonsoftJson_ExecutesSuccessfully()
    {
        var kernel = new NotebookExecutionKernel();
        var code = @"#r ""nuget: Newtonsoft.Json, 13.0.3""
using Newtonsoft.Json;

var obj = new { Title = ""FryPDF"", Success = true, Number = 42 };
var json = JsonConvert.SerializeObject(obj);
Console.WriteLine($""Serialized: {json}"");";

        var result = await kernel.ExecuteCellAsync(code);
        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.Contains("Serialized: {\"Title\":\"FryPDF\",\"Success\":true,\"Number\":42}", result.ConsoleOutput);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteAnyPackage_YamlDotNet_ExecutesSuccessfully()
    {
        var kernel = new NotebookExecutionKernel();
        var code = @"#r ""nuget: YamlDotNet""
using YamlDotNet.Serialization;

var serializer = new SerializerBuilder().Build();
var yaml = serializer.Serialize(new { App = ""FryPDF"", Category = ""UniversalNuGet"" });
Console.WriteLine(yaml.Trim());";

        var result = await kernel.ExecuteCellAsync(code);
        if (!result.Success)
        {
            throw new Exception($"Kernel execution failed:\nConsole: {result.ConsoleOutput}\nError: {result.ErrorMessage}");
        }
        Assert.True(result.Success);
        Assert.Contains("App: FryPDF", result.ConsoleOutput);
        Assert.Contains("Category: UniversalNuGet", result.ConsoleOutput);
    }

    [Fact]
    public async Task NotebookKernel_AnimationStudioTemplateCells_BothPatternsExecuteSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "animation_studio");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();

        RichCellOutput? animateRich = null;
        var animateResult = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => animateRich = r);
        if (!animateResult.Success)
        {
            throw new Exception($"Display.Animate cell failed:\nConsole: {animateResult.ConsoleOutput}\nError: {animateResult.ErrorMessage}");
        }
        Assert.Equal(CellOutputKind.Control, animateRich?.Kind);
        Assert.NotNull(animateRich?.InteractiveControl);

        var frameLoopCell = @"#r ""nuget: SkiaSharp, 3.119.4""
using System;
using System.Threading.Tasks;
using SkiaSharp;

int totalFrames = 5;
for (int frame = 0; frame < totalFrames; frame++)
{
    Display.ThrowIfCancellationRequested();

    var info = new SKImageInfo(300, 120);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(new SKColor(21, 27, 43));

    using var paint = new SKPaint { Color = new SKColor(244, 114, 182), IsAntialias = true };
    float x = 20 + (260 - 20) * frame / (float)(totalFrames - 1);
    canvas.DrawCircle(x, 60, 14, paint);

    Display.Image(surface.Snapshot());
    await Task.Delay(5, Display.CancellationToken);
}

Console.WriteLine($""Rendered {totalFrames} frames."");";

        RichCellOutput? frameLoopRich = null;
        var frameLoopResult = await kernel.ExecuteCellAsync(frameLoopCell, onRichOutput: r => frameLoopRich = r);
        if (!frameLoopResult.Success)
        {
            throw new Exception($"Frame-loop cell failed:\nConsole: {frameLoopResult.ConsoleOutput}\nError: {frameLoopResult.ErrorMessage}");
        }
        Assert.Equal(CellOutputKind.Image, frameLoopRich?.Kind);
        Assert.True(frameLoopRich?.ImageBytes?.Length > 0);
        Assert.Contains("Rendered 5 frames.", frameLoopResult.ConsoleOutput);
    }

    [Fact]
    public async Task NotebookKernel_ExecuteScottPlotAvalonia_NuGetPackageShippingItsOwnAvaloniaControl_ExecutesSuccessfully()
    {
        var template = CodeTemplateLibrary.GetTemplates().FirstOrDefault(t => t.Id == "nuget_charting_scottplot");
        Assert.NotNull(template);

        var kernel = new NotebookExecutionKernel();

        RichCellOutput? scatterRich = null;
        var scatterResult = await kernel.ExecuteCellAsync(template.InitialCode, onRichOutput: r => scatterRich = r);
        if (!scatterResult.Success)
        {
            throw new Exception($"Scatter-chart cell failed:\nConsole: {scatterResult.ConsoleOutput}\nError: {scatterResult.ErrorMessage}");
        }
        Assert.Equal(CellOutputKind.Control, scatterRich?.Kind);
        Assert.NotNull(scatterRich?.InteractiveControl);
        Assert.Contains("Live, interactive ScottPlot chart rendered", scatterResult.ConsoleOutput);

        var barCell = @"var barPlot = new AvaPlot { Width = 520, Height = 300 };
var bp = barPlot.Plot;

string[] categories = { ""PDF"", ""DOCX"", ""XLSX"", ""PPTX"", ""Images"" };
double[] counts = { 420, 180, 96, 64, 233 };

for (int i = 0; i < categories.Length; i++)
{
    bp.Add.Bar(position: i + 1, value: counts[i]);
}

bp.Axes.Bottom.SetTicks(new double[] { 1, 2, 3, 4, 5 }, categories);
bp.Title(""Documents Processed by Type"");
bp.YLabel(""Count"");

Display.Control(barPlot);
Console.WriteLine(""Second chart rendered!"");";

        RichCellOutput? barRich = null;
        var barResult = await kernel.ExecuteCellAsync(barCell, onRichOutput: r => barRich = r);
        if (!barResult.Success)
        {
            throw new Exception($"Bar-chart cell failed:\nConsole: {barResult.ConsoleOutput}\nError: {barResult.ErrorMessage}");
        }
        Assert.Equal(CellOutputKind.Control, barRich?.Kind);
        Assert.NotNull(barRich?.InteractiveControl);
        Assert.Contains("Second chart rendered!", barResult.ConsoleOutput);
    }

    [Fact]
    public async Task NotebookKernel_AlreadyCancelledToken_ReturnsWasCancelledWithoutRunning()
    {
        var kernel = new NotebookExecutionKernel();
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        var ranCellBody = false;
        var result = await kernel.ExecuteCellAsync(
            "Console.WriteLine(\"should not run\");",
            ct: cts.Token,
            onLiveConsole: _ => ranCellBody = true);

        Assert.True(result.WasCancelled);
        Assert.False(ranCellBody);
    }

    [Fact]
    public async Task NotebookCell_TableAndInspectorOutput_SurvivesSaveAndReload()
    {
        var cellItem = new NotebookCellItem { Type = CellType.Code, Source = "new[] {1,2,3}.Dump();" };
        var cellVm = new NotebookCellViewModel(cellItem);

        var table = new DumpTableResult("Int32[3]");
        table.Columns.Add(new DumpTableColumn { Header = "Item", IsNumeric = true });
        table.Rows.Add(new DumpTableRow(0, new List<DumpTableCell> { new() { DisplayText = "1", IsNumeric = true } }));
        cellVm.SetTableOutput(table);

        var inspector = new ObjectInspectorNode("Person");
        inspector.Properties.Add(new ObjectInspectorPropertyRow { Name = "Name", SimpleValueText = "\"Ada\"" });
        cellVm.SetInspectorOutput(inspector);

        var nb = new NotebookDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = "RichOutputTest" };
        nb.Cells.Add(cellItem);
        await _testStorage.SaveNotebookAsync(nb);

        var reloaded = await _testStorage.LoadNotebookAsync(nb.Id);
        Assert.NotNull(reloaded);
        var reloadedCellVm = new NotebookCellViewModel(reloaded.Cells[0]);

        Assert.True(reloadedCellVm.HasTableOutput);
        Assert.Equal("Int32[3]", reloadedCellVm.TableResult!.Title);
        Assert.Single(reloadedCellVm.TableResult.Rows);
        Assert.Equal("1", reloadedCellVm.TableResult.Rows[0].Cells[0].DisplayText);

        Assert.True(reloadedCellVm.HasInspectorOutput);
        Assert.Equal("Person", reloadedCellVm.InspectorNode!.HeaderTitle);
        Assert.Equal("Name", reloadedCellVm.InspectorNode.Properties[0].Name);
    }

    [Fact]
    public async Task ConsoleRoutingContext_ConcurrentScopes_DoNotBlockOrCrossContaminate()
    {
        async Task<string> RunScopedAsync(string marker, int delayMs)
        {
            var writer = new StringWriter();
            using (ConsoleRoutingContext.EnterScope(writer))
            {
                await Task.Delay(delayMs);
                Console.WriteLine(marker);
            }
            return writer.ToString();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var taskA = RunScopedAsync("FROM-A-ONLY", 400);
        var taskB = RunScopedAsync("FROM-B-ONLY", 400);
        await Task.WhenAll(taskA, taskB);
        sw.Stop();

        var outputA = await taskA;
        var outputB = await taskB;

        Assert.Contains("FROM-A-ONLY", outputA);
        Assert.DoesNotContain("FROM-B-ONLY", outputA);
        Assert.Contains("FROM-B-ONLY", outputB);
        Assert.DoesNotContain("FROM-A-ONLY", outputB);

        Assert.True(sw.ElapsedMilliseconds < 700, $"Expected concurrent execution (~400ms), took {sw.ElapsedMilliseconds}ms — looks serialized.");
    }

    [Fact]
    public async Task NotebookKernel_LoopCheckingDisplayCancellationToken_IsActuallyInterruptibleMidCell()
    {
        var kernel = new NotebookExecutionKernel();
        using var cts = new System.Threading.CancellationTokenSource();

        const string code = @"
for (int i = 0; i < 1000; i++)
{
    Display.ThrowIfCancellationRequested();
    await Task.Delay(5, Display.CancellationToken);
}
Console.WriteLine(""should not be reached"");";

        var executeTask = kernel.ExecuteCellAsync(code, ct: cts.Token);
        await Task.Delay(60);
        cts.Cancel();

        var result = await executeTask;

        Assert.True(result.WasCancelled);
        Assert.DoesNotContain("should not be reached", result.ConsoleOutput);
    }

    [Fact]
    public void NuGetSemanticVersion_ParsingAndComparison_PrefersStableOverPrerelease()
    {
        Assert.True(NuGetSemanticVersion.TryParse("3.119.4", out var vStable));
        Assert.True(NuGetSemanticVersion.TryParse("4.154.0-preview.1.26454.9", out var vPreview));
        Assert.True(NuGetSemanticVersion.TryParse("13.0.3", out var vNewtonsoft));

        Assert.NotNull(vStable);
        Assert.NotNull(vPreview);
        Assert.NotNull(vNewtonsoft);

        Assert.False(vStable.IsPrerelease);
        Assert.True(vPreview.IsPrerelease);

        Assert.Equal(3, vStable.Major);
        Assert.Equal(119, vStable.Minor);
        Assert.Equal(4, vStable.Patch);

        Assert.Equal(13, vNewtonsoft.Major);
        Assert.Equal(0, vNewtonsoft.Minor);
        Assert.Equal(3, vNewtonsoft.Patch);
    }

    [Fact]
    public void UpdateActiveNotebook_AddsDocumentToExplorerAndHighlightsIt()
    {
        var studio = CreateStudio();

        var customNb = new NotebookDocumentItem
        {
            Id = "skiasharp_image_studio",
            Title = "SkiaSharp Graphics & Image Generation Copy"
        };

        studio.UpdateActiveNotebook(customNb);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("SkiaSharp Graphics & Image Generation Copy.frynb", studio.ActiveTab.Title);

        var expItem = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "SkiaSharp Graphics & Image Generation Copy.frynb");
        Assert.NotNull(expItem);
        Assert.True(expItem.IsSelected);
        Assert.Equal("skiasharp_image_studio", expItem.DocumentId);
    }

    [Fact]
    public async Task OpenDocument_WhenSavedInStorage_LoadsSavedCells()
    {
        var studio = CreateStudio();

        var uniqueTitle = $"StorageTest_{Guid.NewGuid():N}";
        var newNb = new NotebookDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = uniqueTitle
        };
        newNb.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = "Console.WriteLine(\"Persistent Storage Cell\");"
        });
        await _testStorage.SaveNotebookAsync(newNb);

        var itemVm = new ExplorerItemViewModel
        {
            Name = $"{uniqueTitle}.frynb",
            DocumentId = newNb.Id,
            IsDirectory = false,
            FileExtension = ".frynb",
            FullPath = $"{uniqueTitle}.frynb",
            Parent = null
        };
        studio.ExplorerRootItems.Add(itemVm);

        await studio.OpenDocumentAsync(itemVm);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.Equal($"{uniqueTitle}.frynb", studio.ActiveTab!.Title);
        Assert.Contains(studio.Cells, c => c.Source?.Contains("Persistent Storage Cell") == true);
    }

    [Fact]
    public async Task DuplicateExplorerItem_CreatesClonedDocumentInStorageAndExplorer()
    {
        var studio = CreateStudio();

        var docFile = studio.ExplorerRootItems.First(x => x.Name == "Document Automation Notebook.frynb");

        await studio.DuplicateExplorerItemAsync(docFile);

        Assert.Contains(studio.ExplorerRootItems, x => x.Name == "Document Automation Notebook Copy.frynb");
        var duplicateItem = studio.ExplorerRootItems.First(x => x.Name == "Document Automation Notebook Copy.frynb");
        Assert.False(string.IsNullOrEmpty(duplicateItem.DocumentId));
        Assert.Equal("Document Automation Notebook Copy.frynb", studio.ActiveTab!.Title);
    }

    [Fact]
    public void ExplorerItem_IndentationAndChevronProperties_CalculateAccurately()
    {
        var folder = new ExplorerItemViewModel
        {
            Name = "RootFolder",
            IsDirectory = true,
            Depth = 0,
            IsExpanded = false
        };

        Assert.Equal("ChevronRight", folder.ChevronKind);
        Assert.Equal(4, folder.IndentPadding.Left);

        folder.IsExpanded = true;
        Assert.Equal("ChevronDown", folder.ChevronKind);

        var child = new ExplorerItemViewModel
        {
            Name = "ChildFile.frynb",
            IsDirectory = false,
            FileExtension = ".frynb",
            Depth = 1,
            Parent = folder
        };

        Assert.Equal(18, child.IndentPadding.Left);
        Assert.Equal("#D97706", child.IconColor);
        Assert.Equal("NotebookOutline", child.IconKind);
    }

    [Fact]
    public async Task SaveAsync_OnUntouchedTab_DoesNotPersistAnything()
    {
        var baseline = (await _testStorage.LoadWorkspaceSummariesAsync()).Count;

        var studio = CreateStudio();
        await studio.SaveAsync();

        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        Assert.Equal(baseline, summaries.Count);
        Assert.DoesNotContain(summaries, s => s.Title == "Document Automation Notebook");
    }

    [Fact]
    public async Task BackToHub_OnUntouchedTab_DoesNotPersistAnything()
    {
        var baseline = (await _testStorage.LoadWorkspaceSummariesAsync()).Count;

        var backToHubCalls = 0;
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();
        var studio = new CSharpNotebookStudioViewModel(
            new NotebookDocumentItem { Title = "Document Automation Notebook" },
            _testStorage,
            compiler,
            engine,
            backToHubAction: () => backToHubCalls++,
            backToHomeAction: () => { });

        studio.BackToHub();
        await Task.Delay(200);

        Assert.Equal(1, backToHubCalls);
        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        Assert.Equal(baseline, summaries.Count);
        Assert.DoesNotContain(summaries, s => s.Title == "Document Automation Notebook");
    }

    [Fact]
    public async Task SaveAsync_OnActuallyModifiedTab_PersistsNormally()
    {
        var baseline = (await _testStorage.LoadWorkspaceSummariesAsync()).Count;

        var studio = CreateStudio();
        studio.ActiveTab!.AddCodeCell();
        Assert.True(studio.ActiveTab.IsModified);

        await studio.SaveAsync();

        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        Assert.Equal(baseline + 1, summaries.Count);
        Assert.Contains(summaries, s => s.Title == "Document Automation Notebook");
        Assert.False(studio.ActiveTab.IsModified);
    }

    [Fact]
    public async Task PopulateExplorerTree_ForExternallySavedDocument_GroupsUnderSingleParentFolderNode()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "MyExternalFolder");
        try
        {
            var doc = await _testStorage.CreateNewNotebookAsync("External Doc", folderPath: externalDir);

            var studio = CreateStudio(doc);

            var directoryRoots = studio.ExplorerRootItems.Where(x => x.IsDirectory).ToList();
            var groupNode = Assert.Single(directoryRoots);
            Assert.Equal("MyExternalFolder", groupNode.Name);
            Assert.True(groupNode.IsExternalGroup);
            Assert.False(groupNode.IsManageableDirectory);
            Assert.Equal(externalDir, groupNode.FullPath);

            var docItem = Assert.Single(groupNode.Children);
            Assert.Equal("External Doc.frynb", docItem.Name);
            Assert.False(docItem.IsDirectory);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_OnExternalGroupNode_LeavesRealFolderAndTreeNodeIntact()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "MyExternalFolder");
        try
        {
            var doc = await _testStorage.CreateNewNotebookAsync("External Doc", folderPath: externalDir);
            var studio = CreateStudio(doc);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);

            await studio.DeleteExplorerItemAsync(groupNode);

            Assert.True(Directory.Exists(externalDir));
            Assert.Contains(studio.ExplorerRootItems, x => ReferenceEquals(x, groupNode));
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateExplorerItemAsync_ForExternallySavedNotebook_KeepsCopyInSameExternalFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "DupFolder");
        try
        {
            var doc = await _testStorage.CreateNewNotebookAsync("Original", folderPath: externalDir);
            var studio = CreateStudio(doc);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);
            var originalItem = groupNode.Children.Single();

            await studio.DuplicateExplorerItemAsync(originalItem);

            var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
            var copy = Assert.Single(summaries, s => s.Title == "Original Copy");
            Assert.Equal(externalDir, copy.FolderPath);
            Assert.Contains(groupNode.Children, c => c.DocumentId == copy.Id);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PopulateExplorerTree_WithUnrelatedExternalProject_DoesNotShowTheUnrelatedOne()
    {
        var helloDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "hello");
        var iGuessDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "I guess");
        try
        {
            var helloDoc = await _testStorage.CreateNewNotebookAsync("h", folderPath: helloDir);
            await _testStorage.CreateNewNotebookAsync("New I", folderPath: iGuessDir);

            var studio = CreateStudio(helloDoc);

            var externalGroups = studio.ExplorerRootItems.Where(x => x.IsExternalGroup).ToList();
            var visible = Assert.Single(externalGroups);
            Assert.Equal("hello", visible.Name);
            Assert.DoesNotContain(studio.ExplorerRootItems, x => x.IsExternalGroup && x.Name == "I guess");
        }
        finally
        {
            foreach (var dir in new[] { helloDir, iGuessDir })
            {
                var root = Path.GetDirectoryName(dir)!;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_LastDocumentInExternalGroup_RemovesTheNowEmptyGroupNode()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "SoloFolder");
        try
        {
            var doc = await _testStorage.CreateNewNotebookAsync("Only Doc", folderPath: externalDir);
            var studio = CreateStudio(doc);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);
            var docItem = groupNode.Children.Single();

            await studio.DeleteExplorerItemAsync(docItem);

            Assert.DoesNotContain(studio.ExplorerRootItems, x => x.IsExternalGroup);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_OneOfMultipleDocumentsInExternalGroup_KeepsGroupNodeWithRemainingDocument()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerExternalTests_" + Guid.NewGuid().ToString("N"), "MultiFolder");
        try
        {
            var first = await _testStorage.CreateNewNotebookAsync("First", folderPath: externalDir);
            await _testStorage.CreateNewNotebookAsync("Second", folderPath: externalDir);

            var studio = CreateStudio(first);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);
            Assert.Equal(2, groupNode.Children.Count);
            var firstItem = groupNode.Children.Single(c => c.Name == "First.frynb");

            await studio.DeleteExplorerItemAsync(firstItem);

            var survivingGroup = Assert.Single(studio.ExplorerRootItems, x => x.IsExternalGroup);
            var remaining = Assert.Single(survivingGroup.Children);
            Assert.Equal("Second.frynb", remaining.Name);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_ValidNotebookFile_LoadsNotebookAndSelectsTab()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_NotebookOpenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var externalFile = Path.Combine(externalDir, "ImportedNotebook.frynb");
            var doc = new NotebookDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "ImportedNotebook",
                Category = "Interactive",
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            doc.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "Console.WriteLine(\"Notebook Imported\");"
            });
            var json = System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(externalFile, json);

            var studio = CreateStudio();

            await studio.OpenExternalProjectAsync(externalFile);

            Assert.Contains(studio.Tabs, t => t.Notebook.Title == "ImportedNotebook");
            Assert.Equal("ImportedNotebook", studio.ActiveTab?.Notebook.Title);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }
}
