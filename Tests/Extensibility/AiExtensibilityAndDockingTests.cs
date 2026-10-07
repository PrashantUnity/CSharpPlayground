using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Extensibility;

// Each test installs its own global studio context (the tools read StudioAppContext.Instance): it must never overlap a test
// that uses the global one (theme engine, state), and the previous context comes back afterwards. Swapping it while the
// theme and notebook tests ran made them read another engine or another state bag.
[Collection(CSharpEditorPlugin.Tests.ExtensionTestsCollection.Name)]
public class AiExtensibilityAndDockingTests : IDisposable
{
    private readonly StudioAppContext _previous = StudioAppContext.Instance;

    public void Dispose() => StudioAppContext.Instance = _previous;

    private (StudioAppContext context, AiComposerViewModel vm) CreateTestContext()
    {
        var context = new StudioAppContext();
        var vm = new AiComposerViewModel();
        context.ComposerVmResolver = () => vm;
        context.AiService.AttachViewModel(vm);
        StudioAppContext.Instance = context;
        return (context, vm);
    }

    [Fact]
    public void StudioAppContext_Exposes_IAiApi()
    {
        var (context, _) = CreateTestContext();
        Assert.NotNull(context.AI);
        Assert.Same(context.AiService, context.AI);
    }

    [Fact]
    public void AiDockMode_DefaultIsFloatingOverlay()
    {
        var (context, vm) = CreateTestContext();
        Assert.Equal(AiDockMode.FloatingOverlay, context.AI.DockMode);
        Assert.False(context.AI.IsExtracted);
        Assert.False(context.AI.IsVisible);
    }

    [Fact]
    public void DockTo_SideBar_RegistersSideBarView_AndFiresEvent()
    {
        var (context, vm) = CreateTestContext();
        AiDockMode? receivedMode = null;
        context.AI.DockModeChanged += mode => receivedMode = mode;

        context.AI.DockTo(AiDockMode.DockedSideBar);

        Assert.Equal(AiDockMode.DockedSideBar, vm.DockMode);
        Assert.Equal(AiDockMode.DockedSideBar, context.AI.DockMode);
        Assert.Equal(AiDockMode.DockedSideBar, receivedMode);
        Assert.Contains(context.UI.SideBarViews, v => v.Id == "ai.sidebar.panel");
    }

    [Fact]
    public void DockTo_BottomDeck_TransitionsCleanlyFromSideBar()
    {
        var (context, vm) = CreateTestContext();

        context.AI.DockTo(AiDockMode.DockedSideBar);
        Assert.Contains(context.UI.SideBarViews, v => v.Id == "ai.sidebar.panel");

        context.AI.DockTo(AiDockMode.DockedBottomDeck);
        Assert.Equal(AiDockMode.DockedBottomDeck, context.AI.DockMode);
        // Previous sidebar view should be disposed/removed
        Assert.DoesNotContain(context.UI.SideBarViews, v => v.Id == "ai.sidebar.panel");
        // Bottom deck tab should be registered
        Assert.Contains(context.UI.BottomDeckTabs, t => t.Id == "ai.bottom.tab");

        // Return to floating overlay
        context.AI.DockTo(AiDockMode.FloatingOverlay);
        Assert.Equal(AiDockMode.FloatingOverlay, context.AI.DockMode);
        Assert.DoesNotContain(context.UI.BottomDeckTabs, t => t.Id == "ai.bottom.tab");
    }

    [Fact]
    public void ExtractToWindow_SetsExtractedState_AndInvokesCallback()
    {
        var (context, vm) = CreateTestContext();
        bool callbackInvoked = false;
        AiWindowOptions? capturedOptions = null;

        vm.WindowExtractionRequested = opts =>
        {
            callbackInvoked = true;
            capturedOptions = opts;
        };

        context.AI.ExtractToWindow(new AiWindowOptions
        {
            Title = "Custom AI Window",
            Width = 600,
            Height = 900,
            Topmost = true
        });

        Assert.True(context.AI.IsExtracted);
        Assert.Equal(AiDockMode.ExtractedWindow, context.AI.DockMode);
        Assert.True(callbackInvoked);
        Assert.NotNull(capturedOptions);
        Assert.Equal("Custom AI Window", capturedOptions.Title);
        Assert.Equal(600, capturedOptions.Width);
        Assert.Equal(900, capturedOptions.Height);
        Assert.True(capturedOptions.Topmost);

        // Re-docking
        bool reDockInvoked = false;
        vm.ReDockRequested = () => reDockInvoked = true;

        context.AI.ReDock();
        Assert.False(context.AI.IsExtracted);
        Assert.Equal(AiDockMode.FloatingOverlay, context.AI.DockMode);
        Assert.True(reDockInvoked);
    }

    [Fact]
    public void SetStyle_UpdatesProperties_AndFiresStyleChangedEvent()
    {
        var (context, vm) = CreateTestContext();
        AiStyleOptions? capturedStyle = null;
        context.AI.StyleChanged += s => capturedStyle = s;

        context.AI.SetStyle(new AiStyleOptions
        {
            AccentColorHex = "#A78BFA",
            BackgroundHex = "#101018",
            Opacity = 0.85,
            CornerRadius = 14,
            Width = 620,
            Height = 780,
            PositionX = 200,
            PositionY = 150
        });

        Assert.Equal(0.85, vm.WindowOpacity);
        Assert.Equal("#A78BFA", vm.CustomAccentColorHex);
        Assert.Equal("#101018", vm.CustomBackgroundHex);
        Assert.Equal(14, vm.CustomCornerRadius);
        Assert.Equal(620, vm.WindowWidth);
        Assert.Equal(780, vm.WindowHeight);
        Assert.Equal(200, vm.PositionX);
        Assert.Equal(150, vm.PositionY);

        Assert.NotNull(capturedStyle);
        Assert.Equal(0.85, capturedStyle.Opacity);
        Assert.Equal("#A78BFA", capturedStyle.AccentColorHex);
    }

    [Fact]
    public void SetPersona_And_ConfigurePolicy_ApplyCorrectly()
    {
        var (context, vm) = CreateTestContext();

        context.AI.SetPersona("TDD");
        Assert.Equal("TDD", vm.ActivePromptPresetName);

        context.AI.ConfigurePolicy(new AiPolicyOptions
        {
            AutoAcceptDiffs = true,
            MaxSteps = 40
        });

        Assert.True(vm.AutoAcceptDiffs);
        Assert.Equal(40, vm.MaxSteps);
    }

    [Fact]
    public void AiAgentTools_CanSelfConfigureAssistant()
    {
        var (context, vm) = CreateTestContext();
        StudioAppContext.Instance = context;

        var registry = new AiAgentToolRegistry();

        // AI modifies its own style and dock mode
        var configResult = registry.ConfigureAiAssistant(
            dockMode: "sidebar",
            opacity: 0.9,
            accentColor: "#38BDF8",
            persona: "Reviewer");

        Assert.Contains("AI Assistant configured successfully", configResult);
        Assert.Equal(AiDockMode.DockedSideBar, context.AI.DockMode);
        Assert.Equal(0.9, vm.WindowOpacity);
        Assert.Equal("#38BDF8", vm.CustomAccentColorHex);
        Assert.Equal("Reviewer", vm.ActivePromptPresetName);

        // AI tears itself out into a standalone window
        bool extractionCalled = false;
        vm.WindowExtractionRequested = _ => extractionCalled = true;

        var extractResult = registry.ExtractAiWindow(title: "My Detached AI", topmost: true);
        Assert.Contains("successfully extracted", extractResult);
        Assert.True(context.AI.IsExtracted);
        Assert.True(extractionCalled);

        // AI docks itself back to bottom panel
        var dockResult = registry.DockAiAssistant(targetZone: "bottom");
        Assert.Contains("AI Assistant docked to DockedBottomDeck", dockResult);
        Assert.Equal(AiDockMode.DockedBottomDeck, context.AI.DockMode);
        Assert.False(context.AI.IsExtracted);
    }

    [Fact]
    public void MetadataService_Lists_AiModule()
    {
        var (context, _) = CreateTestContext();
        var schemas = context.Metadata.GetSdkApiSchemas("AI");

        Assert.NotEmpty(schemas);
        var aiModule = schemas.First();
        Assert.Equal("AI", aiModule.Name);
        Assert.Equal("App.AI", aiModule.AccessPath);
        Assert.Equal(typeof(IAiApi).Name, aiModule.InterfaceType);
    }

    [Fact]
    public void SetCaptureProtection_UpdatesState_AndFiresEvent()
    {
        var (context, vm) = CreateTestContext();
        bool? eventFiredValue = null;
        context.AI.CaptureProtectionChanged += val => eventFiredValue = val;

        Assert.False(context.AI.IsProtectedFromCapture);
        Assert.False(vm.IsProtectedFromCapture);

        context.AI.SetCaptureProtection(true);
        Assert.True(context.AI.IsProtectedFromCapture);
        Assert.True(vm.IsProtectedFromCapture);
        Assert.True(eventFiredValue);

        context.AI.SetCaptureProtection(false);
        Assert.False(context.AI.IsProtectedFromCapture);
        Assert.False(vm.IsProtectedFromCapture);
        Assert.False(eventFiredValue);
    }

    [Fact]
    public async Task CaptureWorkspaceScreenshot_GeneratesValidPngBytes()
    {
        var (context, vm) = CreateTestContext();
        var bytes = await context.AI.CaptureWorkspaceScreenshotAsync(CaptureTarget.WorkspaceArea, excludeSelf: true);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        // PNG magic header: 0x89, 'P', 'N', 'G'
        Assert.True(bytes.Length >= 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public async Task CaptureWorkspaceScreenshotToFile_CreatesFileOnDisk()
    {
        var (context, vm) = CreateTestContext();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_capture_{Guid.NewGuid():N}.png");

        try
        {
            var savedPath = await context.AI.CaptureWorkspaceScreenshotToFileAsync(tempFile, CaptureTarget.WorkspaceArea, excludeSelf: true);

            Assert.Equal(tempFile, savedPath);
            Assert.True(File.Exists(savedPath));
            var fileBytes = await File.ReadAllBytesAsync(savedPath);
            Assert.NotEmpty(fileBytes);
            Assert.Equal(0x89, fileBytes[0]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task AiAgentTools_CanCaptureScreenshot_AndSetProtection()
    {
        var (context, vm) = CreateTestContext();
        StudioAppContext.Instance = context;

        var registry = new AiAgentToolRegistry();

        // 1. Tool set_ai_protection
        var protectResult = registry.SetAiProtection(true);
        Assert.Contains("successfully enabled", protectResult);
        Assert.True(context.AI.IsProtectedFromCapture);

        // 2. Tool take_workspace_screenshot
        var screenshotResult = await registry.TakeWorkspaceScreenshot(target: "workspace", excludeSelf: true);
        Assert.Contains("Screenshot successfully captured and saved to:", screenshotResult);
        Assert.Contains("AI self-exclusion active: True", screenshotResult);

        // Verify tool is registered in BuildToolList
        var tools = registry.BuildToolList();
        Assert.Contains(tools, t => t.Name == "take_workspace_screenshot");
        Assert.Contains(tools, t => t.Name == "set_ai_protection");
    }

    [Fact]
    public void AiWindow_And_FloatingControl_SupportDynamicResizingAndSizeSync()
    {
        var (context, vm) = CreateTestContext();

        // 1. Initial dimensions
        Assert.Equal(520, vm.WindowWidth);
        Assert.Equal(650, vm.WindowHeight);

        // 2. Extensibility SetDimensions
        context.AI.SetDimensions(750, 850);
        Assert.Equal(750, vm.WindowWidth);
        Assert.Equal(850, vm.WindowHeight);

        // 3. Extensibility SetStyle with custom bounds
        context.AI.SetStyle(new AiStyleOptions
        {
            Width = 900,
            Height = 700
        });
        Assert.Equal(900, vm.WindowWidth);
        Assert.Equal(700, vm.WindowHeight);

        // 4. StyleChanged event propagates new dimensions
        AiStyleOptions? capturedStyle = null;
        context.AI.StyleChanged += s => capturedStyle = s;
        context.AI.SetDimensions(620, 820);
        Assert.Equal(620, vm.WindowWidth);
        Assert.Equal(820, vm.WindowHeight);
        Assert.NotNull(capturedStyle);
        Assert.Equal(620, capturedStyle.Width);
        Assert.Equal(820, capturedStyle.Height);
    }

    [Fact]
    public void ToggleCaptureProtection_And_DiffLines_Parsing_WorkCorrectly()
    {
        var (_, vm) = CreateTestContext();

        Assert.False(vm.IsProtectedFromCapture);
        vm.ToggleCaptureProtection();
        Assert.True(vm.IsProtectedFromCapture);
        Assert.Contains("Protected from screen capture", vm.StatusText);

        vm.ToggleCaptureProtection();
        Assert.False(vm.IsProtectedFromCapture);
        Assert.Contains("Capture protection disabled", vm.StatusText);

        var item = new ModifiedFileItem
        {
            FilePath = "Test.cs",
            OriginalContent = "int a = 1;\nint b = 2;\n",
            ModifiedContent = "int a = 1;\nint b = 3;\nint c = 4;\n"
        };
        item.CalculateLineMetrics();

        Assert.NotEmpty(item.DiffLines);
        Assert.Contains(item.DiffLines, l => l.Kind == DiffLineKind.Header && l.Text.StartsWith("@@"));
        Assert.Contains(item.DiffLines, l => l.Kind == DiffLineKind.Deleted && l.Text.StartsWith("-"));
        Assert.Contains(item.DiffLines, l => l.Kind == DiffLineKind.Added && l.Text.StartsWith("+"));
    }
}
