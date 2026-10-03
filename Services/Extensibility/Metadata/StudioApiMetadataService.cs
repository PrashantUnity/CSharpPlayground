using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Metadata;

/// <summary>
/// Service implementing <see cref="IApiMetadataApi"/> providing runtime reflection,
/// command introspection, live theme tokens, active UI slots, and schema catalogs.
/// </summary>
public class StudioApiMetadataService : IApiMetadataApi
{
    private readonly StudioAppContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StudioApiMetadataService(StudioAppContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<ApiModuleMetadata> GetSdkApiSchemas(string? category = null)
    {
        var modules = new List<ApiModuleMetadata>
        {
            BuildModuleMetadata("UI", "App.UI", typeof(IUiApi), "UI contribution points for Zones 1-5, dialogs, overlays, and AI Composer actions."),
            BuildModuleMetadata("Theme", "App.Theme", typeof(IThemeApi), "Dynamic color tokens, brushes, layout density, fonts, and XAML styles."),
            BuildModuleMetadata("Commands", "App.Commands", typeof(ICommandApi), "Command pipeline, execution, parameter passing, and shortcut bindings."),
            BuildModuleMetadata("Editor", "App.Editor", typeof(IEditorApi), "Active editor document, selection, caret manipulation, formatting, and file opening."),
            BuildModuleMetadata("Hooks", "App.Hooks", typeof(IHookApi), "Execution lifecycle hooks, compilation interception, document events, and event bus."),
            BuildModuleMetadata("State", "App.State", typeof(IStateBag), "Thread-safe cross-session key-value state bag persisting across hot-reloads."),
            BuildModuleMetadata("Metadata", "App.Metadata", typeof(IApiMetadataApi), "Runtime reflection, live command enumeration, UI slot discovery, and schema catalog.")
        };

        if (string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return modules;
        }

        return modules.Where(m => m.Name.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<CommandMetadata> GetRegisteredCommands()
    {
        try
        {
            var descriptors = _context.CommandPipeline.Descriptors;
            return descriptors.Select(c => new CommandMetadata
            {
                Id = c.Id,
                Title = c.Title,
                Category = c.Category ?? "General",
                ShortcutGesture = c.Shortcut,
                Description = c.Description ?? string.Empty
            }).ToList();
        }
        catch
        {
            return Array.Empty<CommandMetadata>();
        }
    }

    public IReadOnlyList<ThemeTokenMetadata> GetRegisteredThemeTokens()
    {
        var list = new List<ThemeTokenMetadata>
        {
            new() { Key = "M3PrimaryBrush", Category = "Brush", CurrentValue = "#007ACC", Description = "Primary accent color for active indicators, selections, and primary buttons." },
            new() { Key = "M3SurfaceBrush", Category = "Brush", CurrentValue = "#1E1E1E", Description = "Surface background for panels, sidebars, and tool decks." },
            new() { Key = "M3BackgroundBrush", Category = "Brush", CurrentValue = "#181818", Description = "Canvas background for editors and document canvas." },
            new() { Key = "DsTextPrimaryBrush", Category = "Brush", CurrentValue = "#CCCCCC", Description = "High-contrast primary foreground text." },
            new() { Key = "DsTextSecondaryBrush", Category = "Brush", CurrentValue = "#858585", Description = "Subdued secondary foreground text." },
            new() { Key = "DsBorderBrush", Category = "Brush", CurrentValue = "#2D2D2D", Description = "Subtle border and divider brush." },
            new() { Key = "DsHighlightBrush", Category = "Brush", CurrentValue = "#264F78", Description = "Selection and search match highlight." }
        };

        try
        {
            foreach (var kvp in _context.ThemeEngine.Overrides)
            {
                list.Add(new ThemeTokenMetadata
                {
                    Key = kvp.Key,
                    Category = "Override",
                    CurrentValue = kvp.Value,
                    Description = "Live runtime theme token override."
                });
            }
        }
        catch
        {
            // Ignore if theme engine is unavailable
        }

        return list;
    }

    public IReadOnlyList<UiSlotMetadata> GetActiveUiSlots()
    {
        var ui = _context.UiService;

        return new List<UiSlotMetadata>
        {
            new()
            {
                Zone = "Zone 1",
                SlotId = "zone1.activitybar",
                Title = "Activity Bar",
                Description = "Leftmost 48px vertical rail hosting primary tool launcher icons.",
                MountedCount = ui.ActivityBarItems.Count,
                MountedItemIds = ui.ActivityBarItems.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterActivityBarItem(ActivityBarDescriptor descriptor)",
                DescriptorType = nameof(ActivityBarDescriptor),
                ExampleSnippet = "App.UI.RegisterActivityBarItem(new ActivityBarDescriptor { Id = \"my_tool\", Title = \"My Tool\", IconKind = \"PuzzleOutline\", OnClick = () => { } });"
            },
            new()
            {
                Zone = "Zone 2",
                SlotId = "zone2.sidebar",
                Title = "Primary Side Bar Panel",
                Description = "Resizable side bar rendered when an Activity Bar item is active.",
                MountedCount = ui.SideBarViews.Count,
                MountedItemIds = ui.SideBarViews.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterSideBarView(SideBarViewDescriptor descriptor)",
                DescriptorType = nameof(SideBarViewDescriptor),
                ExampleSnippet = "App.UI.RegisterSideBarView(new SideBarViewDescriptor { Id = \"my_panel\", Title = \"PANEL\", ContentFactory = () => new TextBlock { Text = \"Hello\" } });"
            },
            new()
            {
                Zone = "Zone 3",
                SlotId = "zone3.editor.toolbar",
                Title = "Editor Area Toolbar",
                Description = "Top-right editor header action bar for script execution and tools.",
                MountedCount = ui.EditorToolbarItems.Count,
                MountedItemIds = ui.EditorToolbarItems.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterEditorToolbarItem(EditorToolbarItemDescriptor descriptor)",
                DescriptorType = nameof(EditorToolbarItemDescriptor),
                ExampleSnippet = "App.UI.RegisterEditorToolbarItem(new EditorToolbarItemDescriptor { Id = \"quick_run\", Title = \"Quick Run\", IconKind = \"Play\", OnClick = () => { } });"
            },
            new()
            {
                Zone = "Zone 3",
                SlotId = "zone3.editor.overlays",
                Title = "Workspace Floating Overlays",
                Description = "Draggable or anchored floating HUDs and tool windows over the editor canvas.",
                MountedCount = ui.FloatingOverlays.Count,
                MountedItemIds = ui.FloatingOverlays.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterFloatingOverlay(FloatingOverlayDescriptor descriptor)",
                DescriptorType = nameof(FloatingOverlayDescriptor),
                ExampleSnippet = "App.UI.RegisterFloatingOverlay(new FloatingOverlayDescriptor { Id = \"hud\", Title = \"Stats\", ContentFactory = () => new Border { Child = new TextBlock { Text = \"Live\" } } });"
            },
            new()
            {
                Zone = "Zone 4",
                SlotId = "zone4.bottomdeck",
                Title = "Bottom Tool Deck",
                Description = "Resizable bottom dock (Problems, Output, Terminal, Results).",
                MountedCount = ui.BottomDeckTabs.Count,
                MountedItemIds = ui.BottomDeckTabs.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterBottomDeckTab(BottomDeckTabDescriptor descriptor)",
                DescriptorType = nameof(BottomDeckTabDescriptor),
                ExampleSnippet = "App.UI.RegisterBottomDeckTab(new BottomDeckTabDescriptor { Id = \"custom_tab\", Header = \"CUSTOM\", ContentFactory = () => new TextBlock { Text = \"Output\" } });"
            },
            new()
            {
                Zone = "Zone 5",
                SlotId = "zone5.statusbar",
                Title = "Status Bar",
                Description = "Bottom 22px bar showing diagnostics, runtime, and custom widgets.",
                MountedCount = ui.StatusBarWidgets.Count,
                MountedItemIds = ui.StatusBarWidgets.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor)",
                DescriptorType = nameof(StatusBarWidgetDescriptor),
                ExampleSnippet = "App.UI.RegisterStatusBarWidget(new StatusBarWidgetDescriptor { Id = \"mem_widget\", Text = \"RAM: 42MB\", Priority = 80 });"
            },
            new()
            {
                Zone = "AI Composer",
                SlotId = "composer.actions",
                Title = "Floating AI Composer Action Slot",
                Description = "Action bar in the AI floating chat window for quick prompts and tools.",
                MountedCount = ui.ComposerActions.Count,
                MountedItemIds = ui.ComposerActions.Select(x => x.Id).ToList(),
                RegistrationMethod = "App.UI.RegisterComposerAction(ComposerActionDescriptor descriptor)",
                DescriptorType = nameof(ComposerActionDescriptor),
                ExampleSnippet = "App.UI.RegisterComposerAction(new ComposerActionDescriptor { Id = \"export_btn\", Title = \"Export\", OnClick = () => { } });"
            }
        };
    }

    public IReadOnlyList<HookMetadata> GetRegisteredHooks()
    {
        return new List<HookMetadata>
        {
            new()
            {
                Name = "Before Run",
                HookId = "studio.beforerun",
                PayloadType = "ScriptRunContext",
                Description = "Fires immediately before a script or notebook cell begins compilation and execution.",
                ExampleSnippet = "App.Hooks.BeforeRun.Subscribe(ctx => { App.UI.ShowInfo($\"Starting {ctx.ScriptName}\"); });"
            },
            new()
            {
                Name = "After Run",
                HookId = "studio.afterrun",
                PayloadType = "ScriptRunResult",
                Description = "Fires when execution completes, reporting elapsed time, success, and any exceptions.",
                ExampleSnippet = "App.Hooks.AfterRun.Subscribe(res => { App.UI.ShowInfo($\"Finished in {res.ElapsedMs}ms\"); });"
            },
            new()
            {
                Name = "Document Saved",
                HookId = "studio.documentsaved",
                PayloadType = "IDocumentContext",
                Description = "Fires when the active script or notebook document is saved to disk.",
                ExampleSnippet = "App.Hooks.DocumentSaved.Subscribe(doc => { App.Commands.ExecuteAsync(\"code.format\"); });"
            },
            new()
            {
                Name = "Theme Changed",
                HookId = "studio.themechanged",
                PayloadType = "ThemeChangedEventArgs",
                Description = "Fires when the active theme, color palette, or layout density changes.",
                ExampleSnippet = "App.Hooks.ThemeChanged.Subscribe(e => { /* update custom visual brushes */ });"
            }
        };
    }

    public string ExportCatalogJson()
    {
        var catalog = new
        {
            SdkModules = GetSdkApiSchemas(),
            Commands = GetRegisteredCommands(),
            ThemeTokens = GetRegisteredThemeTokens(),
            UiSlots = GetActiveUiSlots(),
            Hooks = GetRegisteredHooks()
        };

        return JsonSerializer.Serialize(catalog, JsonOptions);
    }

    public string ExportCatalogMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# FrySharp Studio Extensibility & Runtime API Catalog");
        sb.AppendLine();
        sb.AppendLine("Use ambient `App` or `Studio` to interact with all subsystems.");
        sb.AppendLine();

        sb.AppendLine("## UI Contribution Slots (Zones 1-5)");
        foreach (var slot in GetActiveUiSlots())
        {
            sb.AppendLine($"### {slot.Title} ({slot.Zone} - `{slot.SlotId}`)");
            sb.AppendLine(slot.Description);
            sb.AppendLine($"- **Registration Method**: `{slot.RegistrationMethod}`");
            sb.AppendLine($"- **Mounted Items ({slot.MountedCount})**: {string.Join(", ", slot.MountedItemIds.DefaultIfEmpty("None"))}");
            sb.AppendLine("```csharp");
            sb.AppendLine(slot.ExampleSnippet);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## Core SDK Modules");
        foreach (var mod in GetSdkApiSchemas())
        {
            sb.AppendLine($"### `{mod.AccessPath}` ({mod.InterfaceType})");
            sb.AppendLine(mod.Description);
            sb.AppendLine();
            sb.AppendLine("**Key Methods & Properties:**");
            foreach (var prop in mod.Properties.Take(6))
            {
                sb.AppendLine($"- `.{prop.Name}`: {prop.Type} - {prop.Description}");
            }
            foreach (var method in mod.Methods.Take(10))
            {
                var args = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                sb.AppendLine($"- `.{method.Name}({args})` -> `{method.ReturnType}`: {method.Description}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static ApiModuleMetadata BuildModuleMetadata(string name, string accessPath, Type interfaceType, string description)
    {
        var properties = interfaceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new ApiPropertyMetadata
            {
                Name = p.Name,
                Type = FormatTypeName(p.PropertyType),
                Description = $"Property returning {FormatTypeName(p.PropertyType)}.",
                CanRead = p.CanRead,
                CanWrite = p.CanWrite
            }).ToList();

        var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => new ApiMethodMetadata
            {
                Name = m.Name,
                ReturnType = FormatTypeName(m.ReturnType),
                Description = $"Method on {interfaceType.Name}.",
                Parameters = m.GetParameters().Select(p => new ApiParameterMetadata
                {
                    Name = p.Name ?? "param",
                    Type = FormatTypeName(p.ParameterType),
                    IsOptional = p.IsOptional,
                    DefaultValue = p.DefaultValue?.ToString()
                }).ToList()
            }).ToList();

        return new ApiModuleMetadata
        {
            Name = name,
            AccessPath = accessPath,
            InterfaceType = interfaceType.Name,
            Description = description,
            Properties = properties,
            Methods = methods
        };
    }

    private static string FormatTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(bool)) return "bool";
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition().Name.Split('`')[0];
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{genDef}<{args}>";
        }
        return type.Name;
    }
}
