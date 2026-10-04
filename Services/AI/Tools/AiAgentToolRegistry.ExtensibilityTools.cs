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

    [Description("Configures the AI assistant's UI presentation, layout docking, floating window opacity, or accent color styling.")]
    public string ConfigureAiAssistant(
        [Description("Dock mode: 'floating', 'sidebar', 'bottom', or 'extracted_window'.")] string? dockMode = null,
        [Description("Window opacity from 0.1 to 1.0 (e.g. 0.85 for translucent).")] double? opacity = null,
        [Description("Width in pixels.")] double? width = null,
        [Description("Height in pixels.")] double? height = null,
        [Description("Accent hex color code (e.g. '#38BDF8', '#A78BFA', '#10B981').")] string? accentColor = null,
        [Description("Background hex color code (e.g. '#161622', '#1E1E2E').")] string? backgroundColor = null,
        [Description("Corner radius in pixels (0-32).")] double? cornerRadius = null,
        [Description("Persona preset name: 'Agent', 'Concise', 'Reviewer', 'TDD'.")] string? persona = null)
    {
        var step = new AgentStepItem
        {
            Title = "Configure AI Assistant Appearance & Layout",
            Status = AgentStepStatus.Running,
            ToolName = "configure_ai_assistant"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var ai = StudioAppContext.Instance.AI;

            if (!string.IsNullOrWhiteSpace(dockMode))
            {
                switch (dockMode.Trim().ToLowerInvariant())
                {
                    case "floating":
                    case "overlay":
                        ai.DockTo(AiDockMode.FloatingOverlay);
                        break;
                    case "sidebar":
                    case "side":
                    case "zone2":
                        ai.DockTo(AiDockMode.DockedSideBar);
                        break;
                    case "bottom":
                    case "deck":
                    case "panel":
                    case "zone4":
                        ai.DockTo(AiDockMode.DockedBottomDeck);
                        break;
                    case "extracted":
                    case "extracted_window":
                    case "window":
                    case "detached":
                    case "tear":
                        ai.ExtractToWindow(new AiWindowOptions
                        {
                            Width = width,
                            Height = height
                        });
                        break;
                }
            }

            if (opacity.HasValue || width.HasValue || height.HasValue || !string.IsNullOrWhiteSpace(accentColor) || !string.IsNullOrWhiteSpace(backgroundColor) || cornerRadius.HasValue)
            {
                ai.SetStyle(new AiStyleOptions
                {
                    Opacity = opacity,
                    Width = width,
                    Height = height,
                    AccentColorHex = accentColor,
                    BackgroundHex = backgroundColor,
                    CornerRadius = cornerRadius
                });
            }

            if (!string.IsNullOrWhiteSpace(persona))
            {
                ai.SetPersona(persona);
            }

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return $"AI Assistant configured successfully! (DockMode: {ai.DockMode}, Opacity: {ai.Opacity})";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to configure AI assistant: {ex.Message}";
        }
    }

    [Description("Extracts ('tears out') the AI assistant into an independent native desktop OS window for multi-monitor setups, with optional topmost pinning.")]
    public string ExtractAiWindow(
        [Description("Window title (default: 'Fry AI Studio Assistant').")] string? title = null,
        [Description("Window width in pixels.")] double? width = null,
        [Description("Window height in pixels.")] double? height = null,
        [Description("Keep the window topmost above all other application windows.")] bool topmost = false)
    {
        var step = new AgentStepItem
        {
            Title = "Extract AI Assistant to Native OS Window",
            Status = AgentStepStatus.Running,
            ToolName = "extract_ai_window"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var ai = StudioAppContext.Instance.AI;
            ai.ExtractToWindow(new AiWindowOptions
            {
                Title = title ?? "Fry AI Studio Assistant",
                Width = width ?? 560,
                Height = height ?? 800,
                Topmost = topmost
            });

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return $"AI Assistant successfully extracted into an independent OS window! (Topmost: {topmost})";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to extract AI assistant window: {ex.Message}";
        }
    }

    [Description("Docks the AI assistant back into the studio layout ('floating', 'sidebar', 'bottom').")]
    public string DockAiAssistant(
        [Description("Target zone: 'floating', 'sidebar', or 'bottom'.")] string targetZone = "floating")
    {
        var step = new AgentStepItem
        {
            Title = $"Dock AI Assistant to {targetZone}",
            Status = AgentStepStatus.Running,
            ToolName = "dock_ai_assistant"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var ai = StudioAppContext.Instance.AI;
            switch (targetZone.Trim().ToLowerInvariant())
            {
                case "sidebar":
                case "side":
                    ai.DockTo(AiDockMode.DockedSideBar);
                    break;
                case "bottom":
                case "deck":
                case "panel":
                    ai.DockTo(AiDockMode.DockedBottomDeck);
                    break;
                case "floating":
                default:
                    ai.DockTo(AiDockMode.FloatingOverlay);
                    break;
            }

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return $"AI Assistant docked to {ai.DockMode}!";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to dock AI assistant: {ex.Message}";
        }
    }

    [Description("Captures a clean screenshot of the studio IDE workspace, active editor canvas, or bottom panel for visual AI inspection. Automatically excludes the AI assistant's own overlay/window to eliminate self-occlusion.")]
    public async Task<string> TakeWorkspaceScreenshot(
        [Description("Target area: 'workspace' (default), 'editor', 'bottom', or 'full'.")] string target = "workspace",
        [Description("Whether to automatically exclude the AI overlay from the screenshot (default true).")] bool excludeSelf = true,
        [Description("Optional custom output path for saving the PNG screenshot.")] string? outputPath = null)
    {
        var step = new AgentStepItem
        {
            Title = $"Capture Studio Screenshot ({target})",
            Status = AgentStepStatus.Running,
            ToolName = "take_workspace_screenshot"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var captureTarget = target.ToLowerInvariant() switch
            {
                "editor" => CaptureTarget.ActiveEditor,
                "bottom" or "deck" or "panel" => CaptureTarget.BottomPanel,
                "full" or "all" => CaptureTarget.FullWindow,
                _ => CaptureTarget.WorkspaceArea
            };

            var ai = StudioAppContext.Instance.AI;
            string savedPath = await ai.CaptureWorkspaceScreenshotToFileAsync(outputPath, captureTarget, excludeSelf);

            step.Status = AgentStepStatus.Completed;
            step.Detail = $"Screenshot saved: {savedPath}";
            OnStepUpdate?.Invoke(step);
            return $"Screenshot successfully captured and saved to: {savedPath}\n(AI self-exclusion active: {excludeSelf})";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to capture workspace screenshot: {ex.Message}";
        }
    }

    [Description("Configures OS-level window display affinity to protect the AI assistant and workspace from being captured by external screen recording apps (Zoom, Microsoft Teams, Discord, OBS).")]
    public string SetAiProtection(
        [Description("Enable or disable OS-level capture protection (default true).")] bool enabled = true)
    {
        var step = new AgentStepItem
        {
            Title = $"Set Capture Protection: {(enabled ? "Enabled" : "Disabled")}",
            Status = AgentStepStatus.Running,
            ToolName = "set_ai_protection"
        };
        OnStepUpdate?.Invoke(step);

        try
        {
            var ai = StudioAppContext.Instance.AI;
            ai.SetCaptureProtection(enabled);

            step.Status = AgentStepStatus.Completed;
            OnStepUpdate?.Invoke(step);
            return $"AI capture protection successfully {(enabled ? "enabled (excluded from screen sharing/recordings)" : "disabled")}!";
        }
        catch (Exception ex)
        {
            step.Status = AgentStepStatus.Failed;
            step.Detail = ex.Message;
            OnStepUpdate?.Invoke(step);
            return $"Failed to set capture protection: {ex.Message}";
        }
    }
}
