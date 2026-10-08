using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class EditorContextMenuTests
{
    private static readonly object InitLock = new();

    private static void EnsureAvaloniaInitialized()
    {
        lock (InitLock)
        {
            if (Application.Current != null) return;
            try
            {
                AppBuilder.Configure<PdfEditorApp.Plugins.CSharpEditor.Runner.App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                    .SetupWithoutStarting();
            }
            catch (InvalidOperationException)
            {
                // Already setup by another test runner
            }
        }
    }

    [Fact]
    public void Attach_CreatesContextMenuWithStandardItems()
    {
        EnsureAvaloniaInitialized();
        var editor = new TextEditor();

        var menu = EditorContextMenu.Attach(editor, new EditorContextMenuOptions
        {
            FormatAction = () => { },
            RunAction = () => { },
            RunHeader = "Run Code",
            DebugAction = () => { },
            ToggleBreakpointAction = _ => { }
        });

        Assert.NotNull(menu);
        Assert.Same(menu, editor.ContextMenu);

        var menuItems = menu.Items.OfType<MenuItem>().ToList();
        var headers = menuItems.Select(m => m.Header?.ToString()).ToList();

        Assert.Contains("Cut", headers);
        Assert.Contains("Copy", headers);
        Assert.Contains("Paste", headers);
        Assert.Contains("Select All", headers);
        Assert.Contains("Toggle Line Comment", headers);
        Assert.Contains("Format Document", headers);
        Assert.Contains("Run Code", headers);
        Assert.Contains("Start Debugging", headers);
        Assert.Contains("Toggle Breakpoint", headers);
    }

    [Fact]
    public void Attach_RegistersStandardKeyBindings()
    {
        EnsureAvaloniaInitialized();
        var editor = new TextEditor();
        EditorContextMenu.Attach(editor);

        Assert.Contains(editor.KeyBindings, kb => kb.Gesture?.Key == Key.C && (kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Control) || kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)));
        Assert.Contains(editor.KeyBindings, kb => kb.Gesture?.Key == Key.V && (kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Control) || kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)));
        Assert.Contains(editor.KeyBindings, kb => kb.Gesture?.Key == Key.X && (kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Control) || kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)));
        Assert.Contains(editor.KeyBindings, kb => kb.Gesture?.Key == Key.A && (kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Control) || kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)));
        Assert.Contains(editor.KeyBindings, kb => kb.Gesture?.Key == Key.Z && (kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Control) || kb.Gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)));
    }

    [Fact]
    public void ToggleLineComment_CommentsAndUncommentsLines()
    {
        EnsureAvaloniaInitialized();
        var editor = new TextEditor();
        editor.Text = "int a = 1;\nint b = 2;";

        // Caret on first line
        editor.CaretOffset = 2;
        EditorContextMenu.ToggleLineComment(editor, null);

        Assert.Equal("// int a = 1;\nint b = 2;", editor.Text);

        // Toggle back
        EditorContextMenu.ToggleLineComment(editor, null);
        Assert.Equal("int a = 1;\nint b = 2;", editor.Text);
    }
}
