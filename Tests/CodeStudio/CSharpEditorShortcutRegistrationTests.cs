using PdfEditorApp.Core.Plugins;
using PdfEditorApp.Core.Plugins.Descriptors;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Tests that apply the real plugin and let go of it. Letting go of the plugin ends every program it started
/// (<c>ProcessRegistry.KillAll()</c>), and that registry is the test process's own: so while one of these runs, no other test
/// may be running a program, or its <c>dotnet fsi</c>, <c>python</c> or <c>java</c> is killed under it (exit code 137). A
/// collection that doesn't run in parallel runs by itself, after the others.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginLifecycleCollection
{
    public const string Name = "PluginLifecycle";
}

[Collection(PluginLifecycleCollection.Name)]
public class CSharpEditorShortcutRegistrationTests
{
    [Fact]
    public async Task ApplyAsync_RegistersAllRequiredShortcuts()
    {
        var rootCtx = new FryPluginContext();
        var scope = new PluginScope();
        var scopedCtx = rootCtx.CreateScopedContext(scope);

        var plugin = new PdfEditorApp.Plugins.CSharpEditor.CSharpEditorPlugin();
        await plugin.ApplyAsync(scopedCtx);

        var shortcuts = scopedCtx.GetRegisteredShortcuts().ToList();

        Assert.Contains(shortcuts, s => s.Id == "csharp.studio.launch" && s.Scope == ShortcutScope.Global);
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.run" && s.DefaultGesture == "F5");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.stop" && s.DefaultGesture == "Shift+F5");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.save" && s.DefaultGesture == "Ctrl+S");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.new" && s.DefaultGesture == "Ctrl+N");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.show_references" && s.DefaultGesture == "Ctrl+Shift+P");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_cell" && s.DefaultGesture == "Shift+Enter");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_cell_stay" && s.DefaultGesture == "Ctrl+Enter");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_all" && s.DefaultGesture == "Ctrl+Shift+Enter");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.insert_cell_below" && s.DefaultGesture == "Ctrl+Shift+B");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.insert_cell_above" && s.DefaultGesture == "Ctrl+Shift+A");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.delete_cell" && s.DefaultGesture == "Ctrl+Shift+D");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_in" && s.DefaultGesture == "Ctrl+=" && s.MacGesture == "Cmd+=");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_out" && s.DefaultGesture == "Ctrl+-" && s.MacGesture == "Cmd+-");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_reset" && s.DefaultGesture == "Ctrl+0" && s.MacGesture == "Cmd+0");

        var saveShortcut = shortcuts.First(s => s.Id == "csharp.editor.save");
        Assert.Equal("Cmd+S", saveShortcut.MacGesture);

        var launchShortcut = shortcuts.First(s => s.Id == "csharp.studio.launch");
        Assert.Equal("Cmd+Alt+E", launchShortcut.MacGesture);
    }

    [Fact]
    public async Task ScopeDisposal_UnregistersAllShortcutsCleanly()
    {
        var rootCtx = new FryPluginContext();
        var scope = new PluginScope();
        var scopedCtx = rootCtx.CreateScopedContext(scope);

        var plugin = new PdfEditorApp.Plugins.CSharpEditor.CSharpEditorPlugin();
        await plugin.ApplyAsync(scopedCtx);

        Assert.NotEmpty(rootCtx.GetRegisteredShortcuts());

        scope.Dispose();

        Assert.Empty(rootCtx.GetRegisteredShortcuts());
    }
}
