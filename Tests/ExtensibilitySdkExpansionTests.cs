using System;
using System.IO;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Dialogs;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Events;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Results;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Terminal;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Workspace;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class ExtensibilitySdkExpansionTests : IDisposable
{
    private readonly string _tempDir;

    public ExtensibilitySdkExpansionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FrySdkTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task WorkspaceService_PerformsFileOperationsAndGlobbing()
    {
        var ws = new ExtensibilityWorkspaceService
        {
            RootPathResolver = () => _tempDir
        };

        Assert.True(ws.HasWorkspace);
        Assert.Equal(_tempDir, ws.RootPath);

        // 1. Write text
        await ws.WriteTextAsync("src/code.cs", "class Foo {}");
        await ws.WriteTextAsync("docs/readme.md", "# Readme");

        // 2. FileExists
        Assert.True(ws.FileExists("src/code.cs"));
        Assert.True(ws.FileExists("docs/readme.md"));
        Assert.False(ws.FileExists("nonexistent.txt"));

        // 3. Read text
        var content = await ws.ReadTextAsync("src/code.cs");
        Assert.Equal("class Foo {}", content);

        // 4. Find files
        var csFiles = ws.FindFiles("*.cs");
        Assert.Single(csFiles);
        Assert.Contains("code.cs", csFiles[0]);

        // 5. Delete file
        Assert.True(ws.DeleteFile("src/code.cs"));
        Assert.False(ws.FileExists("src/code.cs"));
    }

    [Fact]
    public void EventBus_PubSub_DeliversPayloadsDecoupled()
    {
        var bus = new ExtensibilityEventBus();

        int receivedValue = 0;
        bool parameterlessCalled = false;

        using (bus.Subscribe<int>("game.score", val => receivedValue = val))
        using (bus.Subscribe("game.reset", () => parameterlessCalled = true))
        {
            bus.Publish("game.score", 150);
            bus.Publish("game.reset");

            Assert.Equal(150, receivedValue);
            Assert.True(parameterlessCalled);
        }

        // After disposing subscription, events should no longer trigger
        bus.Publish("game.score", 999);
        Assert.Equal(150, receivedValue);
    }

    [Fact]
    public void TerminalService_WritesAndClearsConsoleBuffer()
    {
        var term = new ExtensibilityTerminalService();
        var buffer = new System.Text.StringBuilder();

        term.OutputWriter = s => buffer.Append(s);
        term.ClearHandler = () => buffer.Clear();

        term.Write("hello ");
        term.WriteLine("world");
        Assert.Contains("hello world", buffer.ToString());

        term.Clear();
        Assert.Empty(buffer.ToString());
    }

    [Fact]
    public void ResultsService_DispatchesShowAndClearCalls()
    {
        var results = new ExtensibilityResultsService();

        object? capturedItem = null;
        string? capturedTitle = null;
        bool cleared = false;
        bool focused = false;

        results.ShowHandler = (item, title) =>
        {
            capturedItem = item;
            capturedTitle = title;
        };
        results.ClearHandler = () => cleared = true;
        results.FocusHandler = () => focused = true;

        results.Show(42, "Answer");
        Assert.Equal(42, capturedItem);
        Assert.Equal("Answer", capturedTitle);

        results.Clear();
        Assert.True(cleared);

        results.Focus();
        Assert.True(focused);
    }

    [Fact]
    public async Task DialogService_RoutesToHandlers()
    {
        var dialogs = new ExtensibilityDialogService();

        dialogs.PromptHandler = (msg, title, def, ph) => Task.FromResult<string?>("UserAnswer");
        dialogs.ConfirmHandler = (msg, title) => Task.FromResult(true);

        var promptRes = await dialogs.PromptAsync("Question?", defaultValue: "default");
        Assert.Equal("UserAnswer", promptRes);

        var confirmRes = await dialogs.ConfirmAsync("Proceed?");
        Assert.True(confirmRes);
    }

    [Fact]
    public void UiService_RegistersSideBarViews()
    {
        var ui = new ExtensibilityUiService();

        using (ui.RegisterSideBarView(new SideBarViewDescriptor
        {
            Id = "kanban.panel",
            Title = "Kanban Board",
            IconKind = "ViewKanban",
            ContentFactory = () => new object()
        }))
        {
            Assert.Single(ui.SideBarViews);
            Assert.Equal("kanban.panel", ui.SideBarViews[0].Id);
        }

        Assert.Empty(ui.SideBarViews);
    }

    [Fact]
    public void StudioAppContext_ExposesAllSdkSurfaces()
    {
        var app = StudioAppContext.Instance;

        Assert.NotNull(app.Theme);
        Assert.NotNull(app.Themes);
        Assert.NotNull(app.Commands);
        Assert.NotNull(app.Editor);
        Assert.NotNull(app.UI);
        Assert.NotNull(app.Hooks);
        Assert.NotNull(app.State);
        Assert.NotNull(app.Workspace);
        Assert.NotNull(app.Terminal);
        Assert.NotNull(app.Results);
        Assert.NotNull(app.Events);
        Assert.NotNull(app.Dialogs);
    }

    private sealed class DummyDocument : IDocumentContext
    {
        public string? FilePath => "/test/file.cs";
        public string Title => "file.cs";
        public string Text { get; set; } = "line 1\nline 2\nline 3";
        public string LanguageId => "csharp";
        public bool IsDirty => false;
        public int CaretLine { get; set; } = 1;
        public int CaretColumn { get; set; } = 1;
        public string SelectedText { get; set; } = "";
        public void Format() { }
        public void Save() { }
    }

    [Fact]
    public void DocumentContext_DefaultMethods_ComputeLinesAndInserts()
    {
        IDocumentContext doc = new DummyDocument();

        Assert.Equal(3, doc.LineCount);
        Assert.Equal("line 1", doc.GetLineText(1));
        Assert.Equal("line 2", doc.GetLineText(2));
        Assert.Equal("line 3", doc.GetLineText(3));
        Assert.Empty(doc.GetLineText(10));

        doc.InsertText("hello");
        Assert.Equal("hello", doc.SelectedText);
    }
}
