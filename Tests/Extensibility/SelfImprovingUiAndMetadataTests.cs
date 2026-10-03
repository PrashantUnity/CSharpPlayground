using System;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Metadata;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class SelfImprovingUiAndMetadataTests
{
    [Fact]
    public void MetadataService_GetSdkApiSchemas_ReturnsAllExpectedModules()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var schemas = metadata.GetSdkApiSchemas();
        Assert.NotNull(schemas);
        Assert.True(schemas.Count >= 7);

        var moduleNames = schemas.Select(s => s.Name).ToList();
        Assert.Contains("UI", moduleNames);
        Assert.Contains("Theme", moduleNames);
        Assert.Contains("Commands", moduleNames);
        Assert.Contains("Editor", moduleNames);
        Assert.Contains("Hooks", moduleNames);
        Assert.Contains("State", moduleNames);
        Assert.Contains("Metadata", moduleNames);

        // Verify specific category filtering
        var uiSchema = metadata.GetSdkApiSchemas("ui");
        Assert.Single(uiSchema);
        Assert.Equal("UI", uiSchema[0].Name);
        Assert.Contains(uiSchema[0].Methods, m => m.Name == nameof(IUiApi.RegisterEditorToolbarItem));
        Assert.Contains(uiSchema[0].Methods, m => m.Name == nameof(IUiApi.RegisterFloatingOverlay));
        Assert.Contains(uiSchema[0].Methods, m => m.Name == nameof(IUiApi.RegisterComposerAction));
    }

    [Fact]
    public void MetadataService_GetRegisteredCommands_ReturnsCommands()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var commands = metadata.GetRegisteredCommands();
        Assert.NotNull(commands);
        Assert.Contains(commands, c => c.Id == "game.snake");
    }

    [Fact]
    public void MetadataService_GetRegisteredThemeTokens_ReturnsCoreBrushes()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var tokens = metadata.GetRegisteredThemeTokens();
        Assert.NotNull(tokens);
        Assert.Contains(tokens, t => t.Key == "M3PrimaryBrush");
        Assert.Contains(tokens, t => t.Key == "M3SurfaceBrush");
    }

    [Fact]
    public void MetadataService_GetActiveUiSlots_IncludesAllZones()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var slots = metadata.GetActiveUiSlots();
        Assert.NotNull(slots);
        Assert.True(slots.Count >= 7);

        var zones = slots.Select(s => s.Zone).ToList();
        Assert.Contains("Zone 1", zones);
        Assert.Contains("Zone 2", zones);
        Assert.Contains("Zone 3", zones);
        Assert.Contains("Zone 4", zones);
        Assert.Contains("Zone 5", zones);
        Assert.Contains("AI Composer", zones);
    }

    [Fact]
    public void MetadataService_ExportCatalogJson_ReturnsParsableJson()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var json = metadata.ExportCatalogJson();
        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("SdkModules", out var sdkModules));
        Assert.True(sdkModules.GetArrayLength() >= 7);
        Assert.True(root.TryGetProperty("UiSlots", out var uiSlots));
        Assert.True(uiSlots.GetArrayLength() >= 7);
    }

    [Fact]
    public void MetadataService_ExportCatalogMarkdown_HasValidHeaders()
    {
        var app = new StudioAppContext();
        var metadata = new StudioApiMetadataService(app);

        var md = metadata.ExportCatalogMarkdown();
        Assert.Contains("# FrySharp Studio Extensibility & Runtime API Catalog", md);
        Assert.Contains("## UI Contribution Slots (Zones 1-5)", md);
        Assert.Contains("App.UI", md);
    }

    [Fact]
    public void UiService_RegisterEditorToolbarItem_RegistersAndDisposes()
    {
        var ui = new ExtensibilityUiService();
        int changeNotified = 0;
        ui.ContributionsChanged += () => changeNotified++;

        var item = new EditorToolbarItemDescriptor
        {
            Id = "quick_export",
            Title = "Quick Export",
            IconKind = "Export"
        };

        var reg = ui.RegisterEditorToolbarItem(item);
        Assert.Single(ui.EditorToolbarItems);
        Assert.Equal("quick_export", ui.EditorToolbarItems[0].Id);

        reg.Dispose();
        Assert.Empty(ui.EditorToolbarItems);
    }

    [Fact]
    public void UiService_RegisterFloatingOverlay_RegistersAndDisposes()
    {
        var ui = new ExtensibilityUiService();
        bool closedFired = false;

        var overlay = new FloatingOverlayDescriptor
        {
            Id = "live_hud",
            Title = "HUD",
            ContentFactory = () => new Border(),
            OnClosed = () => closedFired = true
        };

        var reg = ui.RegisterFloatingOverlay(overlay);
        Assert.Single(ui.FloatingOverlays);
        Assert.Equal("live_hud", ui.FloatingOverlays[0].Id);

        reg.Dispose();
        Assert.Empty(ui.FloatingOverlays);
        Assert.True(closedFired);
    }

    [Fact]
    public void UiService_RegisterComposerAction_RegistersAndDisposes()
    {
        var ui = new ExtensibilityUiService();

        var action = new ComposerActionDescriptor
        {
            Id = "git_commit_btn",
            Title = "Git Commit",
            IconKind = "Git"
        };

        var reg = ui.RegisterComposerAction(action);
        Assert.Single(ui.ComposerActions);
        Assert.Equal("git_commit_btn", ui.ComposerActions[0].Id);

        reg.Dispose();
        Assert.Empty(ui.ComposerActions);
    }

    [Fact]
    public void DynamicViewFactory_CreateFromFactory_ShieldsErrorsGracefully()
    {
        bool threw = false;
        var ctrl = DynamicViewFactory.CreateFromFactory(() =>
        {
            threw = true;
            throw new InvalidOperationException("Simulated crash in user visual component");
        }, "CrashingWidget");

        Assert.True(threw);
        Assert.NotNull(ctrl);
        Assert.IsType<ExtensibilityErrorBoundaryControl>(ctrl);

        var boundary = (ExtensibilityErrorBoundaryControl)ctrl;
        Assert.True(boundary.HasError);
        Assert.Contains("Simulated crash in user visual component", boundary.ErrorMessage);
    }

    [Fact]
    public void ExtensibilityErrorBoundary_Retry_RecoversWhenFixed()
    {
        int attempts = 0;
        var boundary = new ExtensibilityErrorBoundaryControl(() =>
        {
            attempts++;
            if (attempts == 1) throw new InvalidOperationException("First attempt failed");
            return new TextBlock { Text = "Success on retry" };
        }, "RecoverableWidget");

        Assert.True(boundary.HasError);
        Assert.Equal(1, attempts);

        // Re-mount / retry
        boundary.MountFactory();
        Assert.False(boundary.HasError);
        Assert.Equal(2, attempts);
        Assert.IsType<TextBlock>(boundary.Content);
        Assert.Equal("Success on retry", ((TextBlock)boundary.Content).Text);
    }

    [Fact]
    public void AiAgentToolRegistry_ExtensibilityTools_ExecuteDeterministically()
    {
        var registry = new AiAgentToolRegistry();

        // 1. GetStudioApiMetadata — returns markdown catalog containing App.UI section
        var apiMeta = registry.GetStudioApiMetadata("ui");
        Assert.False(string.IsNullOrWhiteSpace(apiMeta));
        Assert.Contains("App.UI", apiMeta);

        // 2. GetRuntimeExtensionPoints — returns JSON catalog with expected top-level keys
        var extensionPoints = registry.GetRuntimeExtensionPoints();
        Assert.False(string.IsNullOrWhiteSpace(extensionPoints));
        Assert.Contains("UiSlots", extensionPoints);
        Assert.Contains("ThemeTokens", extensionPoints);

        // 3. InspectStudioUi (returns summary or "unavailable" fallback in headless test environment)
        var visualTree = registry.InspectStudioUi(3);
        Assert.False(string.IsNullOrWhiteSpace(visualTree));

        // 4. InjectStudioWidget with invalid slot returns a descriptive error message
        var invalidResult = registry.InjectStudioWidget("UnknownSlot", "id1", "Title", "<Border />");
        Assert.Contains("Unknown slot 'UnknownSlot'", invalidResult);
    }
}
