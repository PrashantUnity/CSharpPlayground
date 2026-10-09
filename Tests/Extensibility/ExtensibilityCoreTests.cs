using System;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Hooks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.State;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using Xunit;

namespace CSharpEditorPlugin.Tests;

[Collection(ExtensionTestsCollection.Name)]
public class ExtensibilityCoreTests
{
    [Fact]
    public void StateBag_PersistsAcrossAccesses_AndSupportsTypes()
    {
        var bag = new InMemoryStateBag();

        bag.Set("counter", 42);
        Assert.Equal(42, bag.Get<int>("counter"));

        var generated = bag.GetOrSet("cache", () => "initial-value");
        Assert.Equal("initial-value", generated);

        // Subsequent call does not invoke factory
        var cached = bag.GetOrSet("cache", () => "other-value");
        Assert.Equal("initial-value", cached);

        bag["dynamicKey"] = "hello";
        Assert.Equal("hello", bag["dynamicKey"]);

        Assert.True(bag.Contains("counter"));
        Assert.True(bag.Remove("counter"));
        Assert.False(bag.Contains("counter"));
    }

    [Fact]
    public async Task CommandPipeline_RegistersExecutesAndDisposesCommands()
    {
        var pipeline = new ExtensibilityCommandPipeline();
        bool executed = false;

        using (pipeline.Register("test.customCmd", "Custom Test", () => executed = true))
        {
            Assert.True(pipeline.HasCommand("test.customCmd"));
            Assert.Contains("test.customCmd", pipeline.RegisteredCommands);

            var result = await pipeline.ExecuteAsync("test.customCmd");
            Assert.True(result);
            Assert.True(executed);
        }

        // After disposing, command is unregistered
        Assert.False(pipeline.HasCommand("test.customCmd"));
    }

    [Fact]
    public async Task CommandPipeline_MiddlewareCanCancelExecution()
    {
        var pipeline = new ExtensibilityCommandPipeline();
        bool executed = false;

        pipeline.Register("test.guardedCmd", "Guarded", () => executed = true);

        using (pipeline.Use(async (ctx, next) =>
        {
            if (ctx.CommandId == "test.guardedCmd")
            {
                ctx.IsCancelled = true;
                ctx.CancellationReason = "Blocked by security policy";
                return;
            }
            await next();
        }))
        {
            var result = await pipeline.ExecuteAsync("test.guardedCmd");
            Assert.False(result);
            Assert.False(executed); // Command was intercepted and cancelled
        }

        // Once middleware is disposed, command can run again
        var secondRun = await pipeline.ExecuteAsync("test.guardedCmd");
        Assert.True(secondRun);
        Assert.True(executed);
    }

    [Fact]
    public void HookRegistry_FiresBeforeAndAfterRunHooks()
    {
        var hooks = new ExtensibilityHookRegistry();
        bool beforeFired = false;
        bool afterFired = false;

        using (hooks.OnBeforeScriptRun(ctx =>
        {
            beforeFired = true;
            Assert.Equal("csharp", ctx.LanguageId);
        }))
        using (hooks.OnAfterScriptRun(ctx =>
        {
            afterFired = true;
            Assert.True(ctx.Success);
        }))
        {
            hooks.InvokeBeforeScriptRun(new ExecutionHookContext
            {
                LanguageId = "csharp",
                SourceCode = "Console.WriteLine(1);"
            });

            hooks.InvokeAfterScriptRun(new ExecutionFinishedHookContext
            {
                LanguageId = "csharp",
                Success = true,
                Elapsed = TimeSpan.FromMilliseconds(15)
            });

            Assert.True(beforeFired);
            Assert.True(afterFired);
        }

        // Once disposed, hooks don't fire
        beforeFired = false;
        hooks.InvokeBeforeScriptRun(new ExecutionHookContext { LanguageId = "csharp" });
        Assert.False(beforeFired);
    }

    [Fact]
    public void DynamicThemeEngine_RegistersAndAppliesThemes()
    {
        var engine = new DynamicThemeEngine();
        Assert.NotNull(engine.GetAvailableThemes());
        Assert.Contains(engine.GetAvailableThemes(), t => t.Id == "dark-plus");
        Assert.Contains(engine.GetAvailableThemes(), t => t.Id == "dracula");
        Assert.Contains(engine.GetAvailableThemes(), t => t.Id == "cyberpunk");

        var customTheme = new ThemeDefinition
        {
            Id = "my-custom-theme",
            Name = "My Custom Theme",
            Colors = new()
            {
                ["DsPrimaryBrush"] = "#FF5722",
                ["DsBgBrush"] = "#121212"
            }
        };

        engine.RegisterTheme(customTheme);
        Assert.Contains(engine.GetAvailableThemes(), t => t.Id == "my-custom-theme");

        bool applied = engine.ApplyTheme("my-custom-theme");
        Assert.True(applied);
        Assert.Equal("my-custom-theme", engine.ActiveThemeId);
    }

    [Fact]
    public void DynamicThemeEngine_OverridesColorTokens_AndNotifiesThemeChanged()
    {
        var engine = new DynamicThemeEngine();
        string? changedTheme = null;
        engine.ThemeChanged += id => changedTheme = id;

        engine.SetColor("DsPrimaryBrush", "#FF7043");
        Assert.Equal("#FF7043", engine.GetColor("DsPrimaryBrush"));
        Assert.Equal("dark-plus", changedTheme);
    }

    [Fact]
    public void StudioAppContext_ProvidesUnifiedAccess()
    {
        var context = StudioAppContext.Instance;
        Assert.NotNull(context);
        Assert.NotNull(context.Theme);
        Assert.NotNull(context.Commands);
        Assert.NotNull(context.Editor);
        Assert.NotNull(context.UI);
        Assert.NotNull(context.Hooks);
        Assert.NotNull(context.State);

        // Verify Theme -> Hook forwarding
        string? notifiedTheme = null;
        try
        {
            using (context.Hooks.OnThemeChanged(id => notifiedTheme = id))
            {
                context.Theme.ApplyTheme("cyberpunk");
                Assert.Equal("cyberpunk", notifiedTheme);
            }
        }
        finally
        {
            context.Theme.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        }
    }

    [Fact]
    public void CustomizationCompiler_CompilesValidScript_WithAmbientApp()
    {
        var compiler = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.CustomizationCompiler();
        string script = @"
App.Theme.SetColor(""DsPrimaryBrush"", ""#00FFCC"");
App.State.Set(""test_val"", 12345);
";
        var (success, peBytes, pdbBytes, diags) = compiler.Compile(script);
        Assert.True(success, string.Join("; ", diags.Select(d => d.Message)));
        Assert.NotNull(peBytes);
        Assert.DoesNotContain(diags, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void CustomizationCompiler_ReportsAccurateDiagnostics_ForErrors()
    {
        var compiler = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.CustomizationCompiler();
        string script = @"
int a = ""not an int"";
";
        var diags = compiler.CheckDiagnostics(script);
        Assert.NotEmpty(diags);
        var err = diags.First(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(2, err.Line); // Matches line 2 in script!
    }

    [Fact]
    public async Task ScriptCustomizationSession_AppliesScript_AndMutatesHostLive()
    {
        using var session = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.ScriptCustomizationSession();
        string script = @"
App.Theme.SetColor(""DsPrimaryBrush"", ""#123456"");
App.State.Set(""live_key"", ""live_value"");
";
        var result = await session.ApplyScriptAsync(script);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(session.HasActiveCustomization);
        Assert.Equal("live_value", StudioAppContext.Instance.State.Get<string>("live_key"));
    }

    [Fact]
    public async Task ScriptCustomizationSession_SupportsHotReload()
    {
        using var session = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host.ScriptCustomizationSession();

        // 1. Initial script sets version 1
        var res1 = await session.ApplyScriptAsync(@"App.State.Set(""ver"", 1);");
        Assert.True(res1.Success);
        Assert.Equal(1, StudioAppContext.Instance.State.Get<int>("ver"));

        // 2. Hot-reload: recompile and apply version 2
        var res2 = await session.ApplyScriptAsync(@"App.State.Set(""ver"", 2);");
        Assert.True(res2.Success);
        Assert.Equal(2, StudioAppContext.Instance.State.Get<int>("ver"));

        // 3. Unload session
        session.Unload();
        Assert.False(session.HasActiveCustomization);
    }
}
