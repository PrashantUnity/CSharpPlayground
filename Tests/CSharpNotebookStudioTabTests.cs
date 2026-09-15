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
        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var existing = codeFolder?.Children.FirstOrDefault(x => x.Name == name);
        if (existing != null) return existing;

        var secondItem = new ExplorerItemViewModel
        {
            Name = name,
            DocumentId = "test_doc_second",
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = codeFolder
        };
        codeFolder?.Children.Add(secondItem);
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
    public void OpenDocument_MultipleFiles_DisplaysMultipleTabs()
    {
        var studio = CreateStudio();

        // Add second notebook in Explorer tree
        var secondFile = EnsureSecondNotebookItem(studio);

        // Open second notebook
        studio.OpenDocument(secondFile);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.ActiveTab.IsActive);
        Assert.False(studio.Tabs[0].IsActive);
    }

    [Fact]
    public void OpenDocument_SameFileTwice_DoesNotDuplicateTabs()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var docFile = codeFolder!.Children.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(docFile);

        // Click same file again
        studio.OpenDocument(docFile);

        Assert.Single(studio.Tabs);
        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab!.Title);
    }

    [Fact]
    public void SelectTab_SwitchesActiveTab_AndUpdatesExplorerSelection()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        studio.OpenDocument(secondFile);

        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab!.Title);

        // Switch back to first tab
        studio.SelectTab(studio.Tabs[0]);

        Assert.Equal("Document Automation Notebook.frynb", studio.ActiveTab.Title);
        Assert.True(studio.Tabs[0].IsActive);
        Assert.False(studio.Tabs[1].IsActive);

        // Verify Explorer selection updated
        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        var firstItem = codeFolder!.Children.FirstOrDefault(x => x.Name == "Document Automation Notebook.frynb");
        Assert.NotNull(firstItem);
        Assert.True(firstItem.IsSelected);
    }

    [Fact]
    public void CloseTab_SwitchesToAdjacentTab()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        studio.OpenDocument(secondFile);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.Equal("Second_Notebook.frynb", studio.ActiveTab!.Title);

        // Close the active tab
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

        // Verify breadcrumb segment formatting
        Assert.Equal("Code", studio.BreadcrumbFolder);
        Assert.Equal("Document Automation Notebook.frynb", studio.BreadcrumbDocument);
        Assert.Equal("Markdown: 📓 Polyglot Notebook Demo Copy", activeTab.ActiveCellBadgeText);
        Assert.Equal("FormatHeaderPound", activeTab.ActiveCellTypeIcon);
        Assert.Equal("#4EC9B0", activeTab.ActiveCellTypeColor);

        // Ensure no bizarre "> C# # 📓" concatenation
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
        Assert.Equal("Code › Document Automation Notebook.frynb › Cell [1]: public class People", studio.BreadcrumbText);
    }

    [Fact]
    public void NewNotebookTab_CreatesTabAndAddsToExplorer()
    {
        var studio = CreateStudio();

        var initialCount = studio.Tabs.Count;
        studio.NewNotebookTab();

        Assert.Equal(initialCount + 1, studio.Tabs.Count);
        Assert.NotNull(studio.ActiveTab);
        Assert.StartsWith("Notebook_", studio.ActiveTab.Title);

        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        Assert.NotNull(codeFolder);
        Assert.Contains(codeFolder.Children, x => x.Name == studio.ActiveTab.Title);
    }

    [Fact]
    public void DeleteExplorerItem_ClosesOpenTab()
    {
        var studio = CreateStudio();

        var secondFile = EnsureSecondNotebookItem(studio);
        studio.OpenDocument(secondFile);

        Assert.Equal(2, studio.Tabs.Count);

        // Delete from explorer
        studio.DeleteExplorerItem(secondFile);

        Assert.Single(studio.Tabs);
        Assert.DoesNotContain(studio.Tabs, t => t.Title == "Second_Notebook.frynb");
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
        // Legacy directive that previously triggered TypeInitializationException
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

        // Verify document was added to the Explorer tree and selected
        var codeFolder = studio.ExplorerRootItems.FirstOrDefault(x => x.Name == "Code");
        Assert.NotNull(codeFolder);
        Assert.True(codeFolder.IsExpanded);

        var expItem = codeFolder.Children.FirstOrDefault(x => x.Name == "SkiaSharp Graphics & Image Generation Copy.frynb");
        Assert.NotNull(expItem);
        Assert.True(expItem.IsSelected);
        Assert.Equal("skiasharp_image_studio", expItem.DocumentId);
    }

    [Fact]
    public async Task OpenDocument_WhenSavedInStorage_LoadsSavedCells()
    {
        var studio = CreateStudio();

        // Create and save a notebook in test storage
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

        // Add to explorer
        var codeFolder = studio.ExplorerRootItems.First(x => x.Name == "Code");
        var itemVm = new ExplorerItemViewModel
        {
            Name = $"{uniqueTitle}.frynb",
            DocumentId = newNb.Id,
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = codeFolder
        };
        codeFolder.Children.Add(itemVm);

        // Click to open document
        studio.OpenDocument(itemVm);

        Assert.Equal(2, studio.Tabs.Count);
        Assert.Equal($"{uniqueTitle}.frynb", studio.ActiveTab!.Title);
        Assert.Contains(studio.Cells, c => c.Source?.Contains("Persistent Storage Cell") == true);
    }

    [Fact]
    public void DuplicateExplorerItem_CreatesClonedDocumentInStorageAndExplorer()
    {
        var studio = CreateStudio();

        var codeFolder = studio.ExplorerRootItems.First(x => x.Name == "Code");
        var docFile = codeFolder.Children.First(x => x.Name == "Document Automation Notebook.frynb");

        studio.DuplicateExplorerItem(docFile);

        Assert.Contains(codeFolder.Children, x => x.Name == "Document Automation Notebook Copy.frynb");
        var duplicateItem = codeFolder.Children.First(x => x.Name == "Document Automation Notebook Copy.frynb");
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
        Assert.Equal("#E36C28", child.IconColor);
        Assert.Equal("NotebookOutline", child.IconKind);
    }
}


