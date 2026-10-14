using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
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

        Assert.Contains(shortcuts, s => s.Id == "csharp.studio.launch");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.run" && s.KeyGesture == "F5");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.stop" && s.KeyGesture == "Shift+F5");
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.save" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+S" : "Ctrl+S"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.new" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+N" : "Ctrl+N"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.show_references" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Shift+P" : "Ctrl+Shift+P"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_cell" && s.KeyGesture == "Shift+Enter");
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_cell_stay" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Enter" : "Ctrl+Enter"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.run_all" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Shift+Enter" : "Ctrl+Shift+Enter"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.insert_cell_below" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Shift+B" : "Ctrl+Shift+B"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.insert_cell_above" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Shift+A" : "Ctrl+Shift+A"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.notebook.delete_cell" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+Shift+D" : "Ctrl+Shift+D"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_in" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+=" : "Ctrl+="));
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_out" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+-" : "Ctrl+-"));
        Assert.Contains(shortcuts, s => s.Id == "csharp.editor.zoom_reset" && s.KeyGesture == (OperatingSystem.IsMacOS() ? "Cmd+0" : "Ctrl+0"));

        var saveShortcut = shortcuts.First(s => s.Id == "csharp.editor.save");
        Assert.Equal(OperatingSystem.IsMacOS() ? "Cmd+S" : "Ctrl+S", saveShortcut.KeyGesture);

        var launchShortcut = shortcuts.First(s => s.Id == "csharp.studio.launch");
        Assert.Equal(OperatingSystem.IsMacOS() ? "Cmd+Alt+E" : "Ctrl+Alt+E", launchShortcut.KeyGesture);
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
