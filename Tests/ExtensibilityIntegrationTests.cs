using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class ExtensibilityIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public ExtensibilityIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryStudioTests_ExtIntegration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ExecutionHook_BeforeScriptRun_CanCancelExecution()
    {
        var app = StudioAppContext.Instance;
        bool cancelledByHook = false;
        string? cancelReason = null;

        using (app.Hooks.OnBeforeScriptRun(ctx =>
        {
            if (ctx.SourceCode.Contains("FORBIDDEN_OPERATION"))
            {
                ctx.CancelExecution = true;
                ctx.CancellationReason = "Operation is blocked by security rule";
            }
        }))
        {
            var ctx = new ExecutionHookContext
            {
                LanguageId = "csharp",
                SourceCode = "var x = 1; FORBIDDEN_OPERATION();"
            };

            app.HookRegistry.InvokeBeforeScriptRun(ctx);

            cancelledByHook = ctx.CancelExecution;
            cancelReason = ctx.CancellationReason;
        }

        Assert.True(cancelledByHook);
        Assert.Equal("Operation is blocked by security rule", cancelReason);
    }

    [Fact]
    public void ExecutionHook_AfterScriptRun_NotifiedOnCompletion()
    {
        var app = StudioAppContext.Instance;
        ExecutionFinishedHookContext? received = null;

        using (app.Hooks.OnAfterScriptRun(ctx =>
        {
            received = ctx;
        }))
        {
            var finishedCtx = new ExecutionFinishedHookContext
            {
                LanguageId = "csharp",
                DocumentPath = "/test/script.csx",
                Success = true,
                Elapsed = TimeSpan.FromMilliseconds(42),
                Output = "Output text",
                Error = string.Empty
            };

            app.HookRegistry.InvokeAfterScriptRun(finishedCtx);
        }

        Assert.NotNull(received);
        Assert.True(received!.Success);
        Assert.Equal(TimeSpan.FromMilliseconds(42), received.Elapsed);
        Assert.Equal("Output text", received.Output);
    }

    [Fact]
    public void EditorApi_DocumentLifecycleHooks_DispatchedProperly()
    {
        var app = StudioAppContext.Instance;
        IDocumentContext? opened = null;
        IDocumentContext? saved = null;

        var mockDoc = new MockDocumentContext
        {
            Title = "MyScript.cs",
            LanguageId = "csharp",
            Text = "Console.WriteLine(123);"
        };

        using (app.Hooks.OnDocumentOpened(d => opened = d))
        using (app.Hooks.OnDocumentSaved(d => saved = d))
        {
            app.HookRegistry.InvokeDocumentOpened(mockDoc);
            app.HookRegistry.InvokeDocumentSaved(mockDoc);
        }

        Assert.NotNull(opened);
        Assert.Equal("MyScript.cs", opened!.Title);
        Assert.NotNull(saved);
        Assert.Equal("Console.WriteLine(123);", saved!.Text);
    }

    [Fact]
    public void ShortcutGestureMatcher_AccuratelyMatchesKeyCombinations()
    {
        // Ctrl+Shift+K
        var keyEvent = new KeyEventArgs
        {
            Key = Key.K,
            KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift
        };
        Assert.True(ShortcutGestureMatcher.Matches(keyEvent, "Ctrl+Shift+K"));
        Assert.False(ShortcutGestureMatcher.Matches(keyEvent, "Ctrl+K"));
        Assert.False(ShortcutGestureMatcher.Matches(keyEvent, "Ctrl+Shift+J"));

        // Cmd+Shift+P (Mac equivalent)
        var macKeyEvent = new KeyEventArgs
        {
            Key = Key.P,
            KeyModifiers = KeyModifiers.Meta | KeyModifiers.Shift
        };
        Assert.True(ShortcutGestureMatcher.Matches(macKeyEvent, "Ctrl+Shift+P"));
        Assert.True(ShortcutGestureMatcher.Matches(macKeyEvent, "Cmd+Shift+P"));

        // Alt+Z
        var altKeyEvent = new KeyEventArgs
        {
            Key = Key.Z,
            KeyModifiers = KeyModifiers.Alt
        };
        Assert.True(ShortcutGestureMatcher.Matches(altKeyEvent, "Alt+Z"));
        Assert.False(ShortcutGestureMatcher.Matches(altKeyEvent, "Ctrl+Alt+Z"));
    }

    [Fact]
    public void ExtensibilityCommandPipeline_FiresCommandsChangedOnRegisterAndUnregister()
    {
        var pipeline = new ExtensibilityCommandPipeline();
        int changedCount = 0;
        pipeline.CommandsChanged += () => changedCount++;

        IDisposable reg;
        using (reg = pipeline.Register("cmd.test.1", "Command 1", () => { }))
        {
            Assert.Equal(1, changedCount);
            Assert.Single(pipeline.Descriptors);
        }

        // Unregistered after dispose
        Assert.Equal(2, changedCount);
        Assert.Empty(pipeline.Descriptors);
    }

    [Fact]
    public async Task CustomizationManager_LiveAppliesThemeAndCommands_FromInMemoryScript()
    {
        using var manager = new CustomizationManager(_tempDir);
        var app = StudioAppContext.Instance;

        string script = @"
App.Theme.RegisterTheme(new ThemeDefinition
{
    Id = ""test-live-theme"",
    Name = ""Test Live Theme"",
    IsDark = true,
    Colors = new()
    {
        [""DsBackgroundBrush""] = ""#101010"",
        [""DsForegroundBrush""] = ""#F0F0F0"",
        [""DsPrimaryBrush""] = ""#FF0055""
    }
});
App.Theme.ApplyTheme(""test-live-theme"");
App.Commands.Register(""cmd.custom.live"", ""Live Command"", () => {
    App.State[""liveRan""] = true;
});
";

        var result = await manager.ApplyCodeAsync(script);

        string diagMsg = string.Join("; ", result.Diagnostics.Select(d => $"{d.Id}: {d.Message}"));
        Assert.True(result.Success, $"{result.ErrorMessage}: {diagMsg}");
        Assert.Equal("test-live-theme", app.Theme.ActiveThemeId);
        Assert.True(app.Commands.HasCommand("cmd.custom.live"));

        // Execute registered command
        await app.Commands.ExecuteAsync("cmd.custom.live");
        Assert.True((bool?)app.State["liveRan"] == true);
    }

    private sealed class MockDocumentContext : IDocumentContext
    {
        public string? FilePath { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string LanguageId { get; set; } = "csharp";
        public bool IsDirty { get; set; }
        public int CaretLine { get; set; } = 1;
        public int CaretColumn { get; set; } = 1;
        public string SelectedText { get; set; } = string.Empty;
        public void Format() { }
        public void Save() { }
    }
}
