using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Core.Plugins;
using PdfEditorApp.Core.Plugins.Descriptors;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpEditorShortcutRegistrationTests
{
    [Fact]
    public async Task ApplyAsync_RegistersAllRequiredShortcuts()
    {
        var rootCtx = new FryPluginContext();
        var scope = new PluginScope();
        var scopedCtx = rootCtx.CreateScopedContext(scope);

        var plugin = new CSharpEditorPlugin();
        await plugin.ApplyAsync(scopedCtx);

        var shortcuts = scopedCtx.GetRegisteredShortcuts().ToList();

        // Verify key shortcuts exist
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

        // Verify Mac gestures adapt correctly
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

        var plugin = new CSharpEditorPlugin();
        await plugin.ApplyAsync(scopedCtx);

        Assert.NotEmpty(rootCtx.GetRegisteredShortcuts());

        // Unload plugin
        scope.Dispose();

        // Zero dangling shortcuts
        Assert.Empty(rootCtx.GetRegisteredShortcuts());
    }
}
