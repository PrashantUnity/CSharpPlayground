using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Extensions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class DynamicLanguageExtensibilityTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FrySharp_LangExtTests_" + Guid.NewGuid().ToString("N"));

    public DynamicLanguageExtensibilityTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    private sealed class SamplePluginLanguage : LanguageDefinition
    {
        public override string Id => "samplelang";
        public override string DisplayName => "SampleLang";
        public override IReadOnlyList<string> FileExtensions => [".sml"];
        public override IReadOnlyList<string> Aliases => ["sml"];
        public override string AccentHex => "#123456";
    }

    [Fact]
    public void SdkLanguagesApi_CanRegisterQueryAndUnregister()
    {
        var app = StudioAppContext.Instance;
        var lang = new SamplePluginLanguage();

        // 1. Verify not registered initially
        Assert.Null(app.Languages.Get("samplelang"));
        Assert.Null(app.Languages.FindByExtension(".sml"));

        // 2. Register via SDK
        var token = app.Languages.Register(lang);
        try
        {
            Assert.NotNull(app.Languages.Get("samplelang"));
            Assert.NotNull(app.Languages.Get("sml"));
            Assert.NotNull(app.Languages.FindByExtension(".sml"));
            Assert.Contains(app.Languages.All, l => l.Id == "samplelang");
            Assert.Contains(app.Languages.SourceFileLanguages, l => l.Id == "samplelang");
        }
        finally
        {
            // 3. Unregister via IDisposable token
            token.Dispose();
        }

        // 4. Verify cleanly removed
        Assert.Null(app.Languages.Get("samplelang"));
        Assert.Null(app.Languages.FindByExtension(".sml"));
        Assert.DoesNotContain(app.Languages.All, l => l.Id == "samplelang");
    }

    [Fact]
    public void LanguageRegistry_Events_FireOnRegisterAndUnregister()
    {
        var registry = new LanguageRegistry();
        var lang = new SamplePluginLanguage();

        int registeredCount = 0;
        int unregisteredCount = 0;
        int changedCount = 0;

        registry.LanguageRegistered += l => { if (l.Id == lang.Id) registeredCount++; };
        registry.LanguageUnregistered += l => { if (l.Id == lang.Id) unregisteredCount++; };
        registry.Changed += () => changedCount++;

        using (registry.Register(lang))
        {
            Assert.Equal(1, registeredCount);
            Assert.Equal(1, changedCount);
            Assert.Equal(0, unregisteredCount);
        }

        Assert.Equal(1, registeredCount);
        Assert.Equal(1, unregisteredCount);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public async Task DeclarativeExtension_LoadsWithoutCSharpCode_RegistersLanguageAndUnloadsCleanly()
    {
        var extDir = Path.Combine(_tempDir, "sample-declarative");
        Directory.CreateDirectory(extDir);

        var syntaxesDir = Path.Combine(extDir, "syntaxes");
        Directory.CreateDirectory(syntaxesDir);

        string xshdContent = """
            <SyntaxDefinition name="ToyLang" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                <Color name="Keyword" foreground="#FF0000" />
                <RuleSet>
                    <Keywords color="Keyword">
                        <Word>fn</Word>
                        <Word>let</Word>
                    </Keywords>
                </RuleSet>
            </SyntaxDefinition>
            """;
        await File.WriteAllTextAsync(Path.Combine(syntaxesDir, "toy.xshd"), xshdContent);

        var manifest = new ExtensionManifest
        {
            Id = "toy-lang-ext",
            Name = "Toy Language",
            Version = "1.0.0",
            Description = "Declarative toy language extension",
            Languages =
            [
                new DeclarativeLanguageContribution
                {
                    Id = "toylang",
                    DisplayName = "ToyLang",
                    ShortName = "TOY",
                    Extensions = [".toy"],
                    IconKind = "CodeBraces",
                    AccentHex = "#ABCDEF",
                    LineCommentPrefix = "//",
                    SyntaxFile = "syntaxes/toy.xshd",
                    RunCommand = "toyc run {file}"
                }
            ]
        };

        await File.WriteAllTextAsync(Path.Combine(extDir, "extension.json"), JsonSerializer.Serialize(manifest));

        using var manager = new ExtensionManager();
        var loadResult = await manager.LoadExtensionAsync(extDir, enableHotReload: false);

        Assert.True(loadResult.Success, $"Load failed: {loadResult.ErrorMessage}");
        Assert.NotNull(loadResult.Extension);

        // Verify the language is registered in the studio
        var app = StudioAppContext.Instance;
        var toyLang = app.Languages.Get("toylang");
        Assert.NotNull(toyLang);
        Assert.Equal("ToyLang", toyLang.DisplayName);
        Assert.Equal("TOY", toyLang.ShortName);
        Assert.Contains(".toy", toyLang.FileExtensions);

        // Verify syntax definition loaded
        var highlighting = toyLang.GetHighlighting(isDark: true);
        Assert.NotNull(highlighting);
        Assert.Equal("ToyLang", highlighting.Name);

        // Verify script runner plan generated
        Assert.NotNull(toyLang.ScriptRunner);
        var dummyToolchain = new PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains.ToolchainInfo
        {
            LanguageId = "toylang",
            ExecutablePath = "/bin/toyc",
            Version = new Version(1, 0),
            DisplayName = "Toy Compiler",
            Source = "System"
        };
        var runPlan = await toyLang.ScriptRunner.PlanAsync(new ScriptRunContext(
            "/workspace/main.toy",
            "/workspace",
            dummyToolchain));

        Assert.Single(runPlan.Steps);
        Assert.Equal("toyc", runPlan.Steps[0].Spec.FileName);
        Assert.Equal(["run", "/workspace/main.toy"], runPlan.Steps[0].Spec.Arguments);

        // Unload extension
        bool unloaded = await manager.UnloadExtensionAsync("toy-lang-ext");
        Assert.True(unloaded);

        // Verify the language was cleanly unregistered
        Assert.Null(app.Languages.Get("toylang"));
        Assert.Null(app.Languages.FindByExtension(".toy"));
    }

    [Fact]
    public async Task DeclarativeScriptRunner_SupportsBuildAndRunSteps_WithReplacements()
    {
        var contrib = new DeclarativeLanguageContribution
        {
            Id = "customcompiled",
            DisplayName = "CustomCompiled",
            BuildCommand = "builder -o {workDir}/out {file}",
            RunCommand = "{workDir}/out --arg {fileName}"
        };

        var langDef = new DeclarativeLanguageDefinition(contrib, _tempDir);
        Assert.True(langDef.IsCompiled);
        Assert.NotNull(langDef.ScriptRunner);

        var toolchain = new PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains.ToolchainInfo
        {
            LanguageId = "customcompiled",
            ExecutablePath = "/bin/builder",
            Version = new Version(1, 0),
            DisplayName = "Builder",
            Source = "System"
        };
        var plan = await langDef.ScriptRunner.PlanAsync(new ScriptRunContext("/projects/test/app.src", "/projects/test", toolchain));

        Assert.Equal(2, plan.Steps.Count);

        var buildStep = plan.Steps[0];
        Assert.True(buildStep.IsBuildStep);
        Assert.Equal("builder", buildStep.Spec.FileName);
        Assert.Equal(["-o", "/projects/test/out", "/projects/test/app.src"], buildStep.Spec.Arguments);

        var runStep = plan.Steps[1];
        Assert.False(runStep.IsBuildStep);
        Assert.Equal("/projects/test/out", runStep.Spec.FileName);
        Assert.Equal(["--arg", "app.src"], runStep.Spec.Arguments);
    }

    [Fact]
    public void ReactiveUI_ExplorerNewFileOptions_InvalidatesOnRegistryChange()
    {
        var services = new StudioLanguageServices(
            Path.Combine(_tempDir, "services"),
            registerBuiltInLanguages: true);

        var storage = new PdfEditorApp.Plugins.CSharpEditor.Services.Storage.LocalScriptStorageService(
            Path.Combine(_tempDir, "storage"),
            services.Registry);

        var script = new ScriptDocumentItem
        {
            Id = "test-doc",
            Title = "TestScript",
            Code = "// code",
            LanguageId = LanguageIds.CSharp
        };

        var studio = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel(
            script,
            storage,
            new PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn.RoslynCompilerService(),
            new PdfEditorApp.Plugins.CSharpEditor.Services.Execution.ScriptExecutionEngine(),
            backToHubAction: () => { },
            languages: services,
            postToUiThread: action => action());

        // 1. Initial cached NewFileOptions does not contain SampleLang
        var initialOptions = studio.NewFileOptions;
        Assert.DoesNotContain(initialOptions, o => o.LanguageId == "samplelang");

        // 2. Register new language
        var lang = new SamplePluginLanguage();
        var token = services.Registry.Register(lang);

        // 3. NewFileOptions automatically invalidated and updated
        var updatedOptions = studio.NewFileOptions;
        Assert.Contains(updatedOptions, o => o.LanguageId == "samplelang");
        Assert.Contains(updatedOptions, o => o.Label == "New SampleLang File");

        // 4. Unregister language
        token.Dispose();

        // 5. NewFileOptions automatically invalidated and updated again
        var finalOptions = studio.NewFileOptions;
        Assert.DoesNotContain(finalOptions, o => o.LanguageId == "samplelang");
    }

    [Fact]
    public void GenericLspAssistantFactory_CanBeInstantiatedWithDeclarativeConfig()
    {
        var contrib = new DeclarativeLanguageContribution
        {
            Id = "zig",
            DisplayName = "Zig",
            Lsp = new DeclarativeLspContribution
            {
                Command = "zls",
                Args = ["--stdio"]
            }
        };

        var langDef = new DeclarativeLanguageDefinition(contrib, _tempDir);
        Assert.True(langDef.Has(LanguageCapabilities.Completion));
        Assert.True(langDef.Has(LanguageCapabilities.QuickInfo));
        Assert.NotNull(langDef.EditorAssistants);
        Assert.IsType<PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Lsp.GenericLspEditorAssistantFactory>(langDef.EditorAssistants);
    }

    [Fact]
    public void SettingsViewModel_ReactivelyUpdatesLanguages_WhenRegisteredAndUnregistered()
    {
        var services = new StudioLanguageServices(_tempDir);
        var settingsVm = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings.CSharpSettingsViewModel(services);

        Assert.DoesNotContain(settingsVm.Languages, l => l.Language.Id == "samplelang");
        Assert.DoesNotContain(settingsVm.FilteredLanguages, l => l.Language.Id == "samplelang");

        // Register language
        var lang = new SamplePluginLanguage();
        var token = services.Registry.Register(lang);

        try
        {
            settingsVm.SynchronizeLanguagesFromRegistry();
            Assert.Contains(settingsVm.Languages, l => l.Language.Id == "samplelang");
            Assert.Contains(settingsVm.FilteredLanguages, l => l.Language.Id == "samplelang");
            var item = settingsVm.Languages.First(l => l.Language.Id == "samplelang");
            Assert.True(item.IsExtensionLanguage);
            Assert.Equal("SampleLang", item.DisplayName);
            Assert.Equal("#123456", item.AccentHex);
        }
        finally
        {
            token.Dispose();
            settingsVm.SynchronizeLanguagesFromRegistry();
            Assert.DoesNotContain(settingsVm.Languages, l => l.Language.Id == "samplelang");
            Assert.DoesNotContain(settingsVm.FilteredLanguages, l => l.Language.Id == "samplelang");
        }
    }

    [Fact]
    public void ExplorerItemViewModel_DynamicallyResolvesIconAndAccent_ForExtensionLanguage()
    {
        var app = StudioAppContext.Instance;
        var lang = new SamplePluginLanguage();
        var token = app.Languages.Register(lang);

        try
        {
            var (icon, color) = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer.ExplorerItemViewModel.IconForExtension(".sml");
            Assert.Equal("FileCodeOutline", icon);
            Assert.Equal("#123456", color);
        }
        finally
        {
            token.Dispose();
        }
    }

    [Fact]
    public void WorkspaceItemSummary_DynamicallyResolvesAccentAndIcon_ForExtensionLanguage()
    {
        var app = StudioAppContext.Instance;
        var lang = new SamplePluginLanguage();
        var token = app.Languages.Register(lang);

        try
        {
            var summary = new WorkspaceItemSummary
            {
                Id = "test-sml",
                Title = "Script.sml",
                LanguageId = "samplelang"
            };

            Assert.Equal("#123456", summary.IconForeground);
            Assert.Equal("#123456", summary.AccentColor);
        }
        finally
        {
            token.Dispose();
        }
    }

    [Fact]
    public void DocumentationService_CanRegisterAndUnregisterCategory_AndNotifiesChanged()
    {
        var docService = PdfEditorApp.Plugins.CSharpEditor.Services.Documentation.DocumentationService.Instance;
        bool changedFired = false;
        docService.Changed += () => changedFired = true;

        var category = new DocCategory
        {
            Id = "sample_category",
            Title = "Sample Extension Category",
            Articles = new System.Collections.Generic.List<DocArticle>
            {
                new()
                {
                    Id = "sample_art_1",
                    Title = "Sample Article 1",
                    Summary = "Summary of sample article"
                }
            }
        };

        var token = docService.RegisterCategory(category);
        try
        {
            Assert.True(changedFired);
            Assert.NotNull(docService.GetArticle("sample_art_1"));
            Assert.Contains(docService.Categories, c => c.Id == "sample_category");
        }
        finally
        {
            changedFired = false;
            token.Dispose();
            Assert.True(changedFired);
            Assert.Null(docService.GetArticle("sample_art_1"));
            Assert.DoesNotContain(docService.Categories, c => c.Id == "sample_category");
        }
    }

    [Fact]
    public void ExtensionManager_GetDefaultExtensionSearchDirectories_IncludesMultipleTiers()
    {
        var workspaceExt = Path.Combine(_tempDir, ".frysharp", "extensions");
        Directory.CreateDirectory(workspaceExt);

        var dirs = ExtensionManager.GetDefaultExtensionSearchDirectories(_tempDir);
        Assert.NotEmpty(dirs);
        // Workspace directory included
        Assert.Contains(dirs, d => d.Contains(_tempDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HubManagerViewModel_ReactivelyUpdatesStudioEnvironmentToolchains_WhenLanguageRegistered()
    {
        var services = new StudioLanguageServices(_tempDir);
        var storage = new PdfEditorApp.Plugins.CSharpEditor.Services.Storage.LocalScriptStorageService(
            Path.Combine(_tempDir, "storage"),
            services.Registry);

        var hubVm = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel(
            storage,
            languages: services);

        Assert.DoesNotContain(hubVm.ToolchainStatuses, t => t.Language.Id == CSharpEditorPlugin.Tests.TestSupport.FakeLanguage.LanguageId);

        // Register fake language with toolchain
        var fakeLang = new CSharpEditorPlugin.Tests.TestSupport.FakeLanguage();
        var token = services.Registry.Register(fakeLang);

        try
        {
            Assert.Contains(hubVm.ToolchainStatuses, t => t.Language.Id == CSharpEditorPlugin.Tests.TestSupport.FakeLanguage.LanguageId);
            var item = hubVm.ToolchainStatuses.First(t => t.Language.Id == CSharpEditorPlugin.Tests.TestSupport.FakeLanguage.LanguageId);
            Assert.Equal("FakeLang", item.Title);
        }
        finally
        {
            token.Dispose();
            Assert.DoesNotContain(hubVm.ToolchainStatuses, t => t.Language.Id == CSharpEditorPlugin.Tests.TestSupport.FakeLanguage.LanguageId);
        }
    }
}

