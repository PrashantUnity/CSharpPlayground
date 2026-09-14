using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Core.Plugins;
using PdfEditorApp.Core.Plugins.Descriptors;
using PdfEditorApp.Core.Plugins.Manifests;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor;

/// <summary>
/// FryPDF plugin entry point for C# Code Studio.
/// Mounts as a full-viewport Workspace Studio Navigation page, Status Bar widget,
/// Command Palette action, and Dynamic Ribbon action (No floating shell overlay).
/// </summary>
public class CSharpEditorPlugin : IFryPlugin
{
    public string Id => "com.frypdf.plugin.csharpeditor";
    public string Name => "C# Code Studio";
    public Version Version => new(1, 0, 0);
    public bool AutoOpenOverlay => false;
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema =>
        new Dictionary<string, PluginSettingDefinition>
        {
            ["DefaultTemplate"] = new()
            {
                Label = "Default Starter Script",
                Description = "Initial template loaded when creating a new script.",
                Type = "select",
                Options = new() { "Hello World Console", "PDF Document Automation", "LINQ Benchmark", "JSON Serializer" },
                DefaultValue = "Hello World Console"
            },
            ["EditorFontSize"] = new()
            {
                Label = "Editor Font Size (px)",
                Description = "Code typography size in points/pixels.",
                Type = "integer",
                DefaultValue = 13
            },
            ["ShowLineNumbers"] = new()
            {
                Label = "Show Line Numbers",
                Description = "Display gutter line numbers along the left margin.",
                Type = "boolean",
                DefaultValue = true
            },
            ["AutoCheckErrors"] = new()
            {
                Label = "Live Roslyn Diagnostics",
                Description = "Continuously analyze C# code in the background for syntax and semantic errors.",
                Type = "boolean",
                DefaultValue = true
            },
            ["ExecutionTimeoutSeconds"] = new()
            {
                Label = "Execution Timeout (seconds)",
                Description = "Maximum allowed run time before background script execution is halted.",
                Type = "integer",
                DefaultValue = 10
            }
        };

    public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        // 1. Register Full-Viewport Workspace Studio Page in Left Sidebar Navigation
        var navReg = ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "CSharpStudio",
            Title = "C# Code Studio",
            Group = "Overview",
            IconKind = "CodeBraces",
            BadgeText = "Studio",
            BadgeColorHex = "#75D59A",
            Order = 38,
            DisplayMode = NavigationDisplayMode.FullViewport,
            HideTopSearchBar = true,
            ViewFactory = sp => new CSharpStudioHostView
            {
                DataContext = new CSharpStudioHostViewModel(sp)
            }
        });

        // 2. Command Palette Integration
        var cmdReg = ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.studio.csharpeditor",
            Title = "Open C# Code Studio",
            Subtitle = "Launch in-app C# development studio with project manager, editor, and Roslyn runner",
            Category = "Developer Tools",
            IconKind = "CodeBraces",
            Shortcut = "Ctrl+Alt+E",
            Order = 85,
            Action = _ => { }
        });

        // 3. Status Bar Widget Indicator
        var statusReg = ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
        {
            WidgetId = "frypdf.status.csharpeditor",
            Alignment = StatusBarAlignment.Right,
            Order = 14,
            ToolTip = "C# Code Studio & Automation Hub",
            Factory = _ => new Button
            {
                Content = "{ } C# Studio",
                Classes = { "m3-tonal-btn" }
            }
        });

        // 4. Dynamic Ribbon Action in Plugins Tab
        var ribbonActionReg = ctx.RegisterRibbonAction(new RibbonActionDescriptor
        {
            Id = "frypdf.ribbon.action.csharpeditor",
            TabId = "plugins",
            GroupId = "tools",
            Label = "C# Studio",
            Tooltip = "Open full-featured C# development studio and script automation workspace",
            IconKind = "CodeBraces",
            Order = 30,
            Action = _ => { }
        });

        // 5. Register Keyboard Shortcuts
        var shortcuts = new List<IDisposable>
        {
            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.studio.launch",
                Title = "Launch C# Code Studio",
                Description = "Open in-app C# development studio and script automation workspace.",
                Category = "Editor",
                DefaultGesture = "Ctrl+Alt+E",
                MacGesture = "Cmd+Alt+E",
                Scope = ShortcutScope.Global,
                Action = sp =>
                {
                    var homeType = Type.GetType("PdfEditorApp.ViewModels.HomeViewModel, PdfEditorApp");
                    if (homeType != null)
                    {
                        var home = sp.GetService(homeType);
                        homeType.GetMethod("SelectNavSection")?.Invoke(home, ["CSharpStudio"]);
                    }
                }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.run",
                Title = "Run Script / Debug",
                Description = "Execute active C# script or debug at current breakpoint.",
                Category = "Editor",
                DefaultGesture = "F5",
                MacGesture = "F5",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.stop",
                Title = "Stop Script Execution",
                Description = "Halt currently running C# background script.",
                Category = "Editor",
                DefaultGesture = "Shift+F5",
                MacGesture = "Shift+F5",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.save",
                Title = "Save Script / Notebook",
                Description = "Save active C# script or notebook to local workspace storage.",
                Category = "Editor",
                DefaultGesture = "Ctrl+S",
                MacGesture = "Cmd+S",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.new",
                Title = "New C# Script / Notebook",
                Description = "Create a new C# automation script.",
                Category = "Editor",
                DefaultGesture = "Ctrl+N",
                MacGesture = "Cmd+N",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.editor.show_references",
                Title = "Open NuGet Package Manager",
                Description = "Switch to References tab and manage NuGet package dependencies.",
                Category = "Editor",
                DefaultGesture = "Ctrl+Shift+P",
                MacGesture = "Cmd+Shift+P",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_cell",
                Title = "Run Cell and Select Next",
                Description = "Execute current notebook code cell and advance cursor to next cell.",
                Category = "Notebook",
                DefaultGesture = "Shift+Enter",
                MacGesture = "Shift+Enter",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_cell_stay",
                Title = "Run Cell In Place",
                Description = "Execute current notebook cell without moving cursor focus.",
                Category = "Notebook",
                DefaultGesture = "Ctrl+Enter",
                MacGesture = "Cmd+Enter",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.run_all",
                Title = "Run All Cells",
                Description = "Restart notebook kernel and execute all cells in sequence.",
                Category = "Notebook",
                DefaultGesture = "Ctrl+Shift+Enter",
                MacGesture = "Cmd+Shift+Enter",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.insert_cell_below",
                Title = "Insert Cell Below",
                Description = "Insert a new C# code cell below current cell.",
                Category = "Notebook",
                DefaultGesture = "Ctrl+Shift+B",
                MacGesture = "Cmd+Shift+B",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.insert_cell_above",
                Title = "Insert Cell Above",
                Description = "Insert a new C# code cell above current cell.",
                Category = "Notebook",
                DefaultGesture = "Ctrl+Shift+A",
                MacGesture = "Cmd+Shift+A",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            }),

            ctx.RegisterShortcut(new ShortcutDescriptor
            {
                Id = "csharp.notebook.delete_cell",
                Title = "Delete Active Cell",
                Description = "Remove the selected notebook cell from document.",
                Category = "Notebook",
                DefaultGesture = "Ctrl+Shift+D",
                MacGesture = "Cmd+Shift+D",
                Scope = ShortcutScope.Context,
                ContextId = "CSharpStudio",
                Action = _ => { }
            })
        };

        // 6. Clean Teardown
        ctx.RegisterEffect(() =>
        {
            navReg.Dispose();
            cmdReg.Dispose();
            statusReg.Dispose();
            ribbonActionReg.Dispose();
            foreach (var s in shortcuts) s.Dispose();
        });

        return Task.CompletedTask;
    }
}
