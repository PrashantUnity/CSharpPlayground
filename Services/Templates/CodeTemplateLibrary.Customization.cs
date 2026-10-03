using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Templates;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetCustomizationTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "studio_custom_theme",
            Title = "IDE Theme & Color Customizer",
            Category = "Customization",
            Kind = WorkspaceItemKind.Script,
            Description = "Dynamically customize IDE brushes, fonts, and layout density in real time using FrySharp.Sdk.",
            IconKind = MaterialIconKind.PaletteOutline,
            AccentColor = "#BD93F9",
            AccentBackground = "#282A36",
            AccentBorder = "#BD93F9",
            CategoryBadge = "Extensibility • Theming",
            Tags = new List<string> { "Extensibility", "Theme", "Brushes", "Live Reload" },
            Notes = """
                # Studio Theme Customizer
                This script demonstrates how to modify the application's appearance dynamically in real time:
                - Change background, surface, and accent brushes on the fly.
                - Adjust UI layout density (Compact, Comfortable, Spacious).
                - Use **Apply as Customization** (`Ctrl+Alt+R`) to run it immediately without restarting.
                """,
            InitialCode = """
                using FrySharp.Sdk;

                // 1. Customize core UI tokens live
                App.Themes.SetColor("DsPrimaryBrush", "#BD93F9");          // Dracula purple accent
                App.Themes.SetColor("DsPrimaryHoverBrush", "#D6ACFF");     // Lighter purple on hover
                App.Themes.SetColor("DsBgBrush", "#1A1A24");               // Dark charcoal background
                App.Themes.SetColor("DsSurfaceBrush", "#222230");          // Surface card background

                // 2. Adjust chrome layout density
                App.Themes.SetDensity(LayoutDensity.Comfortable);

                // 3. Notify the user of successful theme activation
                App.UI.ShowSuccess("Custom IDE Theme applied in real-time!");
                """
        },
        new()
        {
            Id = "studio_custom_commands",
            Title = "IDE Custom Commands & Shortcuts",
            Category = "Customization",
            Kind = WorkspaceItemKind.Script,
            Description = "Register interactive studio commands and keyboard shortcuts that execute without restarting.",
            IconKind = MaterialIconKind.KeyboardOutline,
            AccentColor = "#2F81F7",
            AccentBackground = "#161B22",
            AccentBorder = "#2F81F7",
            CategoryBadge = "Extensibility • Commands",
            Tags = new List<string> { "Extensibility", "Commands", "Shortcuts", "QuickOpen" },
            Notes = """
                # Custom Commands & Shortcuts
                Register commands into the Studio Command Palette and bind global shortcuts:
                - Commands appear in the Command Palette (`Ctrl+P` / `Cmd+P`).
                - Shortcuts trigger directly from the keyboard without losing editor focus.
                """,
            InitialCode = """
                using System;
                using FrySharp.Sdk;

                // Register an interactive command with hotkey Ctrl+Alt+T
                App.Commands.Register(
                    id: "studio.timestamp_insert",
                    title: "Insert ISO Timestamp",
                    action: () =>
                    {
                        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
                        if (App.Editor.ActiveDocument is { } doc)
                        {
                            doc.SelectedText = timestamp;
                        }
                        App.UI.ShowSuccess($"Inserted: {timestamp}");
                    },
                    gesture: "Ctrl+Alt+T"
                );

                // Register a notification test command
                App.Commands.Register(
                    id: "studio.workspace_stats",
                    title: "Show Workspace Telemetry",
                    action: () =>
                    {
                        int count = App.Commands.RegisteredCommands.Count;
                        App.UI.ShowInfo($"Extensibility Engine active with {count} registered commands.");
                    },
                    gesture: "Ctrl+Alt+I"
                );

                App.UI.ShowSuccess("Custom Commands registered! Press Ctrl+Alt+T or Ctrl+Alt+I.");
                """
        },
        new()
        {
            Id = "studio_custom_hooks",
            Title = "Execution Pipeline & Lifecycle Hooks",
            Category = "Customization",
            Kind = WorkspaceItemKind.Script,
            Description = "Intercept script runs, enforce coding rules, and react to document lifecycle events.",
            IconKind = MaterialIconKind.Hook,
            AccentColor = "#3FB950",
            AccentBackground = "#161B22",
            AccentBorder = "#3FB950",
            CategoryBadge = "Extensibility • Hooks",
            Tags = new List<string> { "Extensibility", "Hooks", "Pipeline", "Security" },
            Notes = """
                # Execution Lifecycle Hooks
                Hook into the studio runtime pipeline:
                - `BeforeScriptRun`: Pre-execution validation, code rewriting, or policy enforcement.
                - `AfterScriptRun`: Execution metrics, telemetry, and automated cleanup.
                - `DocumentOpened` / `DocumentSaved`: Trigger workflows when files are edited.
                """,
            InitialCode = """
                using FrySharp.Sdk;

                // 1. Intercept before-run execution: guard against unwanted constructs
                App.Hooks.BeforeScriptRun(ctx =>
                {
                    if (ctx.SourceCode.Contains("Environment.Exit"))
                    {
                        ctx.Cancel("Script execution was blocked: Environment.Exit is not permitted in studio.");
                        return;
                    }

                    App.UI.ShowInfo($"Starting run for: {ctx.DocumentPath ?? "Untitled"} ({ctx.LanguageId})");
                });

                // 2. React to execution finished events
                App.Hooks.AfterScriptRun(ctx =>
                {
                    if (ctx.Success)
                    {
                        App.UI.ShowSuccess($"Script completed in {ctx.Elapsed.TotalMilliseconds:F1}ms");
                    }
                    else
                    {
                        App.UI.ShowError($"Script error: {ctx.Error}");
                    }
                });

                App.UI.ShowSuccess("Execution Lifecycle Hooks registered!");
                """
        },
        new()
        {
            Id = "studio_custom_extension",
            Title = "Multi-File Extension & Plugin Architecture",
            Category = "Customization",
            Kind = WorkspaceItemKind.Script,
            Description = "Template demonstrating how to author modular extensions with manifests, commands, and isolated state.",
            IconKind = MaterialIconKind.ToyBrickOutline,
            AccentColor = "#F2CC60",
            AccentBackground = "#161B22",
            AccentBorder = "#F2CC60",
            CategoryBadge = "Extensibility • Extensions",
            Tags = new List<string> { "Extensibility", "Extensions", "Manifest", "Plugin", "Modular" },
            Notes = """
                # Modular Extensions & Plugins
                Extensions in C# Code Studio are multi-file bundles loaded from `~/.frysharp/extensions/<id>/` or `.frysharp/extensions/<id>/`:
                - `extension.json`: Declares extension ID, name, version, main entry class, and settings schema.
                - `IExtensionEntryPoint`: Lifecycle entry point receiving `IExtensionContext`.
                - Isolated state storage and automatic disposable cleanup on reload/unload.
                """,
            InitialCode = """
                using System;
                using System.Threading.Tasks;
                using FrySharp.Sdk;

                // Example of an Extension Entry Point class
                public class MyPluginExtension : IExtensionEntryPoint
                {
                    public Task InitializeAsync(IExtensionContext context)
                    {
                        var app = context.App;

                        // 1. Register commands tied to this extension
                        app.Commands.Register(
                            id: "plugin.hello",
                            title: "Say Hello from Plugin",
                            action: () =>
                            {
                                app.UI.ShowSuccess($"Hello from {context.Manifest.Name} v{context.Manifest.Version}!");
                            },
                            category: "My Extension"
                        );

                        // 2. Track custom state isolated to this extension
                        context.State.Set("init_time", DateTime.UtcNow);

                        // 3. Register a lifecycle hook and track its disposable for automatic cleanup
                        var hookDisposable = app.Hooks.OnThemeChanged(newTheme =>
                        {
                            app.UI.ShowInfo($"Extension received theme update: {newTheme}");
                        });
                        context.TrackDisposable(hookDisposable);

                        app.UI.ShowSuccess($"Extension '{context.Manifest.Name}' activated!");
                        return Task.CompletedTask;
                    }

                    public Task ShutdownAsync()
                    {
                        // Clean up resources when extension is disabled or unloaded
                        return Task.CompletedTask;
                    }
                }
                """
        }
    };
}
