using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;

public partial class AiAgentToolRegistry
{
    [Description("Retrieves structured API documentation, method signatures, and code examples for App.UI, App.Theme, App.Commands, App.Editor, App.Hooks, and App.State.")]
    public string GetStudioApiMetadata(
        [Description("Optional category: 'ui', 'theme', 'commands', 'editor', 'hooks', 'state', or 'all' (default).")] string? category = null)
    {
        var step = new AgentStepItem
        {
            Title = $"Query Studio API: {category ?? "all"}",
            Status = AgentStepStatus.Running,
            ToolName = "get_studio_api_metadata"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var metadata = StudioAppContext.Instance.Metadata;
            string result = string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? metadata.ExportCatalogMarkdown()
                : string.Join("\n\n", metadata.GetSdkApiSchemas(category).Select(m => $"### {m.AccessPath} ({m.InterfaceType})\n{m.Description}"));

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return result;
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to retrieve API metadata: {ex.Message}";
        }
    }

    [Description("Discovers active UI contribution slots, registered commands, active theme tokens, and mounted items in the running application.")]
    public string GetRuntimeExtensionPoints()
    {
        var step = new AgentStepItem
        {
            Title = "Discover Runtime Extension Points",
            Status = AgentStepStatus.Running,
            ToolName = "get_runtime_extension_points"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var json = StudioAppContext.Instance.Metadata.ExportCatalogJson();
            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return json;
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to discover runtime extension points: {ex.Message}";
        }
    }

    [Description("Inspects the live Avalonia visual tree of the running studio window, returning visible control hierarchy, names, types, and bounds.")]
    public string InspectStudioUi(
        [Description("Maximum visual tree traversal depth (default 5, maximum 10).")] int maxDepth = 5)
    {
        int clampedDepth = Math.Clamp(maxDepth, 1, 10);
        var step = new AgentStepItem
        {
            Title = $"Inspect Visual Tree (Depth {clampedDepth})",
            Status = AgentStepStatus.Running,
            ToolName = "inspect_studio_ui"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var summary = StudioAppContext.Instance.UI.VisualTree.DumpVisualTreeSummary(clampedDepth);
            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return summary;
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to inspect studio UI: {ex.Message}";
        }
    }

    [Description("Dynamically injects a custom widget or declarative XAML control into a studio slot ('EditorToolbar', 'FloatingOverlay', 'ComposerAction', 'BottomDeck', 'SideBar').")]
    public string InjectStudioWidget(
        [Description("Target slot: 'EditorToolbar', 'FloatingOverlay', 'ComposerAction', 'BottomDeck', 'SideBar'.")] string slot,
        [Description("Unique ID for the widget registration.")] string id,
        [Description("Display title or tooltip for the widget.")] string title,
        [Description("Declarative Avalonia XAML snippet representing the visual control.")] string xaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);

        var step = new AgentStepItem
        {
            Title = $"Inject Widget: {title} into {slot}",
            Status = AgentStepStatus.Running,
            ToolName = "inject_studio_widget"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var ui = StudioAppContext.Instance.UI;
            Func<object> factory = () => DynamicViewFactory.CreateFromXaml(xaml, title);

            switch (slot.ToLowerInvariant())
            {
                case "editortoolbar":
                case "editor":
                case "toolbar":
                    ui.RegisterEditorToolbarItem(new EditorToolbarItemDescriptor
                    {
                        Id = id,
                        Title = title,
                        CustomContentFactory = factory
                    });
                    break;

                case "floatingoverlay":
                case "overlay":
                case "hud":
                    ui.RegisterFloatingOverlay(new FloatingOverlayDescriptor
                    {
                        Id = id,
                        Title = title,
                        ContentFactory = factory
                    });
                    break;

                case "composeraction":
                case "composer":
                    ui.RegisterComposerAction(new ComposerActionDescriptor
                    {
                        Id = id,
                        Title = title,
                        CustomContentFactory = factory
                    });
                    break;

                case "bottomdeck":
                case "bottom":
                case "dock":
                    ui.RegisterBottomDeckTab(new BottomDeckTabDescriptor
                    {
                        Id = id,
                        Header = title.ToUpperInvariant(),
                        ContentFactory = factory
                    });
                    break;

                case "sidebar":
                case "panel":
                    ui.RegisterSideBarView(new SideBarViewDescriptor
                    {
                        Id = id,
                        Title = title.ToUpperInvariant(),
                        ContentFactory = factory
                    });
                    break;

                default:
                    step.Status = AgentStepStatus.Failed;
                    step.Detail = $"Unknown slot '{slot}'. Valid slots: 'EditorToolbar', 'FloatingOverlay', 'ComposerAction', 'BottomDeck', 'SideBar'.";
                    OnStepUpdate?.Invoke(step);
                    return step.Detail;
            }

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return $"Widget '{title}' successfully injected into {slot}! (ID: {id})";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to inject widget: {ex.Message}";
        }
    }
}
