using System;
using System.IO;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Storage;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class CustomizationManagerTests : IDisposable
{
    private readonly string _tempDir;

    public CustomizationManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FrySharp_CustomizationTests_" + Guid.NewGuid().ToString("N"));
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
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task StorageService_CreatesStarterInitScript_WhenMissing()
    {
        var storage = new CustomizationStorageService(_tempDir);
        Assert.False(File.Exists(storage.GlobalInitScriptPath));

        string content = await storage.EnsureInitScriptExistsAsync();
        Assert.True(File.Exists(storage.GlobalInitScriptPath));
        Assert.Contains("using FrySharp.Sdk;", content);
        Assert.Contains("App.Theme", content);

        // Second call returns existing file without overwriting
        await storage.SaveInitScriptAsync("// Custom edit");
        string updated = await storage.EnsureInitScriptExistsAsync();
        Assert.Equal("// Custom edit", updated);
    }

    [Fact]
    public async Task CustomizationManager_InitializesAndAppliesStarterScript()
    {
        using var manager = new CustomizationManager(_tempDir);
        bool eventFired = false;
        manager.CustomizationApplied += res =>
        {
            eventFired = true;
            Assert.True(res.Success, res.ErrorMessage ?? string.Join("; ", res.Diagnostics.Select(d => d.Message)));
        };

        await manager.InitializeAsync(enableHotReload: false);

        Assert.True(eventFired);
        Assert.True(manager.Session.HasActiveCustomization);
    }

    [Fact]
    public async Task CustomizationManager_AppliesInMemoryCode_AndTriggersCallback()
    {
        using var manager = new CustomizationManager(_tempDir);
        CustomizationExecutionResult? receivedResult = null;
        manager.CustomizationApplied += res => receivedResult = res;

        string script = @"
App.State.Set(""mgr_test_key"", 777);
";
        var result = await manager.ApplyCodeAsync(script);
        Assert.True(result.Success);
        Assert.NotNull(receivedResult);
        Assert.True(receivedResult.Success);
        Assert.Equal(777, StudioAppContext.Instance.State.Get<int>("mgr_test_key"));
    }

    [Fact]
    public async Task CustomizationManager_LoadsWorkspaceCustomizationScript_WhenPresent()
    {
        using var manager = new CustomizationManager(_tempDir);
        string workspaceDir = Path.Combine(_tempDir, "workspace");
        string workspaceFrysharp = Path.Combine(workspaceDir, ".frysharp");
        Directory.CreateDirectory(workspaceFrysharp);

        string workspaceScript = """
            App.State.Set("workspace_loaded_flag", "yes_from_workspace");
            """;
        await File.WriteAllTextAsync(Path.Combine(workspaceFrysharp, "init.csx"), workspaceScript);

        var result = await manager.LoadWorkspaceCustomizationsAsync(workspaceDir, enableHotReload: false);
        Assert.NotNull(result);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("yes_from_workspace", StudioAppContext.Instance.State.Get<string>("workspace_loaded_flag"));
    }

    [Fact]
    public async Task HotReloadWatcher_DebouncesAndTriggersReload()
    {
        string scriptPath = Path.Combine(_tempDir, "init.csx");
        await File.WriteAllTextAsync(scriptPath, "// v1");

        var tcs = new TaskCompletionSource<string>();

        using var watcher = new HotReloadWatcher(scriptPath, async code =>
        {
            tcs.TrySetResult(code);
            await Task.CompletedTask;
        }, debounceMs: 50);

        Assert.True(watcher.IsWatching);

        // Modify file
        await File.WriteAllTextAsync(scriptPath, "// v2 updated");

        // Wait for debounced reload
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Equal(tcs.Task, completedTask);

        string loaded = await tcs.Task;
        Assert.Equal("// v2 updated", loaded);
    }
}
