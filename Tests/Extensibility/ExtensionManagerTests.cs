using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using Xunit;

namespace CSharpEditorPlugin.Tests;

[Collection(ExtensionTestsCollection.Name)]
public class ExtensionManagerTests : IDisposable
{
    private readonly string _tempRoot;

    public ExtensionManagerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "FrySharp_ExtMgrTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ExtensionCompiler_CompilesMultiFileDirectory_Successfully()
    {
        string extDir = Path.Combine(_tempRoot, "test-compile-ext");
        Directory.CreateDirectory(extDir);

        File.WriteAllText(Path.Combine(extDir, "Helper.cs"), @"
namespace TestExt;
public static class Helper {
    public static string Greet(string name) => $""Hello, {name}!"";
}
");

        File.WriteAllText(Path.Combine(extDir, "Main.cs"), @"
namespace TestExt;
public class EntryPoint {
    public string Run() => Helper.Greet(""World"");
}
");

        var compiler = new ExtensionCompiler();
        var (success, peBytes, pdbBytes, diags) = compiler.CompileDirectory(extDir);

        Assert.True(success, string.Join("; ", diags.Select(d => d.Message)));
        Assert.NotNull(peBytes);
        Assert.DoesNotContain(diags, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ExtensionManager_LoadsAndInitializesExtension_WithManifest()
    {
        string extDir = Path.Combine(_tempRoot, "sample-ext");
        Directory.CreateDirectory(extDir);

        var manifest = new ExtensionManifest
        {
            Id = "sample.plugin",
            Name = "Sample Plugin",
            Version = "1.0.0",
            Author = "CodeFry",
            Description = "A test extension",
            Settings = new()
            {
                ["greeting"] = new ExtensionSettingDefinition
                {
                    Key = "greeting",
                    Label = "Greeting Message",
                    DefaultValue = "Hello from extension settings!"
                }
            }
        };

        await File.WriteAllTextAsync(Path.Combine(extDir, "extension.json"), JsonSerializer.Serialize(manifest));

        await File.WriteAllTextAsync(Path.Combine(extDir, "Extension.cs"), @"
using System;
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace SampleExtension;

public class SamplePluginEntryPoint : IExtensionEntryPoint
{
    private IExtensionContext? _ctx;

    public Task InitializeAsync(IExtensionContext context)
    {
        _ctx = context;
        context.App.State.Set(""sample_plugin_active"", true);
        string setting = context.GetSetting<string>(""greeting"") ?? ""default"";
        context.App.State.Set(""sample_greeting"", setting);

        var cmdDisposable = context.App.Commands.Register(""sample.action"", ""Sample Action"", () =>
        {
            context.App.State.Set(""sample_action_invoked"", true);
        });
        context.TrackDisposable(cmdDisposable);

        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _ctx?.App.State.Set(""sample_plugin_active"", false);
        return Task.CompletedTask;
    }
}
");

        using var manager = new ExtensionManager();
        var result = await manager.LoadExtensionAsync(extDir, enableHotReload: false);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Extension);
        Assert.Equal("sample.plugin", result.ExtensionId);
        Assert.True(StudioAppContext.Instance.State.Get<bool>("sample_plugin_active"));
        Assert.Equal("Hello from extension settings!", StudioAppContext.Instance.State.Get<string>("sample_greeting"));
        Assert.True(StudioAppContext.Instance.Commands.HasCommand("sample.action"));

        // Execute registered command
        await StudioAppContext.Instance.Commands.ExecuteAsync("sample.action");
        Assert.True(StudioAppContext.Instance.State.Get<bool>("sample_action_invoked"));

        // Unload extension
        bool unloaded = await manager.UnloadExtensionAsync("sample.plugin");
        Assert.True(unloaded);
        Assert.False(StudioAppContext.Instance.State.Get<bool>("sample_plugin_active"));
        Assert.False(StudioAppContext.Instance.Commands.HasCommand("sample.action")); // Command unregistered
    }

    [Fact]
    public async Task ExtensionManager_HotReloadsMultiFileExtension_OnSourceChange()
    {
        string extDir = Path.Combine(_tempRoot, "hotreload-ext");
        Directory.CreateDirectory(extDir);

        var manifest = new ExtensionManifest
        {
            Id = "reloadable.ext",
            Name = "Reloadable Extension",
            Version = "1.0.0"
        };

        await File.WriteAllTextAsync(Path.Combine(extDir, "extension.json"), JsonSerializer.Serialize(manifest));

        // Version 1 of extension code
        await File.WriteAllTextAsync(Path.Combine(extDir, "Main.cs"), @"
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace HotReloadExt;

public class EntryPoint : IExtensionEntryPoint
{
    public Task InitializeAsync(IExtensionContext context)
    {
        context.App.State.Set(""ext_version"", 1);
        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
");

        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(extDir, enableHotReload: false);
        Assert.True(loadResult.Success, loadResult.ErrorMessage);
        Assert.Equal(1, StudioAppContext.Instance.State.Get<int>("ext_version"));

        // Update source code to Version 2
        await File.WriteAllTextAsync(Path.Combine(extDir, "Main.cs"), @"
using System.Threading.Tasks;
using FrySharp.Sdk;

namespace HotReloadExt;

public class EntryPoint : IExtensionEntryPoint
{
    public Task InitializeAsync(IExtensionContext context)
    {
        context.App.State.Set(""ext_version"", 2);
        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
");

        // Reload extension
        var reloadResult = await manager.ReloadExtensionAsync("reloadable.ext");
        Assert.True(reloadResult.Success, reloadResult.ErrorMessage);
        Assert.Equal(2, StudioAppContext.Instance.State.Get<int>("ext_version"));
    }
}
