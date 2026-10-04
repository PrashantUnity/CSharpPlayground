using System;
using System.Linq;
using System.Threading.Tasks;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Extensibility;

public class AiExtensibilityAndDockingTests
{
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
}
