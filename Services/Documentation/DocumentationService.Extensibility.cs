using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocCategory BuildExtensibilityAndCustomizationCategory()
    {
        return new DocCategory
        {
            Id = "extensibility_customization",
            Title = "Extensibility & Customization",
            IconKind = MaterialIconKind.PuzzleOutline,
            AccentColor = "#A855F7",
            Badge = "SDK & API",
            Description = "Comprehensive guide to in-app C# scripting, dynamic theme modifications, commands, lifecycle hooks, and extensions.",
            Articles = new List<DocArticle>
            {
                CreateExtensibilityFundamentalsArticle(),
                CreateDynamicThemingArticle(),
                CreateLayoutDensityAndDimensionsArticle(),
                CreateCustomCommandsArticle(),
                CreateCommandMiddlewareArticle(),
                CreateEditorManipulationArticle(),
                CreateExecutionLifecycleHooksArticle(),
                CreateUiContributionsArticle(),
                CreateCrossSessionStateArticle(),
                CreateMultiFileExtensionsArticle()
            }
        };
    }

    // =========================================================================
    // CHAPTER 1: EXTENSIBILITY FUNDAMENTALS & AMBIENT APP
    // =========================================================================
    private DocArticle CreateExtensibilityFundamentalsArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch1_fundamentals",
            Title = "1. Extensibility Fundamentals & Ambient App",
            Subtitle = "Write C# scripts to customize your IDE with hot-reload and zero compilation restarts.",
            ReadingTime = "4 min read",
            Summary = "Understand the in-app scripting runtime, the three script scopes, hot-reload behavior, and the global ambient App object.",
            Keywords = new List<string> { "extensibility", "init.csx", "scripting", "hot-reload", "ambient app", "sdk" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Core Philosophy: In-Process Scripting",
                    Content = "Unlike traditional IDEs where plugins require external compilation, bundling, and restarts, C# Code Studio provides first-class in-process Roslyn scripting. You write standard modern C# 13 code that compiles and takes effect immediately.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "You have full access to .NET 10 BCL, Avalonia visual controls, NuGet packages, and the open FrySharp SDK."
                },
                new()
                {
                    Heading = "The Three Customization Scopes",
                    Content = "Customization scripts can be placed and executed in three convenient locations:",
                    BulletPoints = new List<string>
                    {
                        "Global User Script (~/.frysharp/init.csx): Runs automatically when the studio starts up and applies across all workspaces.",
                        "Workspace Project Script (.frysharp/init.csx): Placed inside a project root to configure workspace-specific themes, commands, and hooks.",
                        "Active Tab / Notebook Cell (Ctrl+Alt+R): Evaluate whatever code is currently open in your editor tab or notebook cell directly into the live IDE runtime."
                    }
                },
                new()
                {
                    Heading = "The Ambient App Global",
                    Content = "All customization scripts automatically import the static 'App' (or 'Studio') facade. You never need to instantiate managers or import internal namespaces — 'App' gives you direct access to Themes, Commands, Editor, UI, Hooks, and State."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IThemeApi", MethodName = "App.Themes", Parameters = "", Description = "Access dynamic runtime theming, color tokens, layout density, and Avalonia resources." },
                new() { ReturnType = "ICommandApi", MethodName = "App.Commands", Parameters = "", Description = "Register custom commands, assign keyboard shortcuts, and configure middleware." },
                new() { ReturnType = "IEditorApi", MethodName = "App.Editor", Parameters = "", Description = "Interact with active documents, selections, caret positions, lines, and file opening." },
                new() { ReturnType = "IUiApi", MethodName = "App.UI", Parameters = "", Description = "Contribute to Activity Bar, Side Bar views, Status Bar, Bottom Deck tabs, and modals." },
                new() { ReturnType = "IHookApi", MethodName = "App.Hooks", Parameters = "", Description = "Intercept compiler execution, document save, text change, and theme changes." },
                new() { ReturnType = "IStateBag", MethodName = "App.State", Parameters = "", Description = "Thread-safe key-value store that persists data across script hot-reloads." },
                new() { ReturnType = "IWorkspaceApi", MethodName = "App.Workspace", Parameters = "", Description = "Active project folder root, file search globbing, text I/O, and explorer refresh." },
                new() { ReturnType = "ITerminalApi", MethodName = "App.Terminal", Parameters = "", Description = "Write to Studio terminal buffer, clear console, and run CLI tools with real-time output." },
                new() { ReturnType = "IResultsApi", MethodName = "App.Results", Parameters = "", Description = "Display rich Avalonia controls, interactive data tables, and objects in Results (.Dump)." },
                new() { ReturnType = "IEventBusApi", MethodName = "App.Events", Parameters = "", Description = "Decoupled publish/subscribe messaging across scripts, extensions, and notebooks." },
                new() { ReturnType = "IDialogApi", MethodName = "App.Dialogs", Parameters = "", Description = "Native input prompts (PromptAsync), confirmation dialogs, and file/folder pickers." }
            },
            Shortcuts = new List<DocShortcutItem>
            {
                new() { Action = "Apply Active Tab/Cell as Customization", MacKey = "Cmd+Alt+R", WinKey = "Ctrl+Alt+R", Description = "Evaluates the current editor code directly into the customization engine.", Category = "Customization" },
                new() { Action = "Reload Customizations", MacKey = "Cmd+Shift+R", WinKey = "Ctrl+Shift+R", Description = "Recompiles and reloads ~/.frysharp/init.csx from disk.", Category = "Customization" }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch1_starter_snippet",
                    Title = "Hello Customization World",
                    Description = "A minimal starter script demonstrating notifications, commands, and theme tokens.",
                    Code = """
                        // ~/.frysharp/init.csx
                        using FrySharp.Sdk;

                        // 1. Show a welcoming desktop toast
                        App.UI.ShowSuccess("Welcome to customized C# Code Studio!");

                        // 2. Register a quick action with a keyboard shortcut
                        App.Commands.Register("quick.hello", "Say Hello", () =>
                        {
                            App.UI.ShowInfo($"Studio ready on .NET {Environment.Version}");
                        }, gesture: "Ctrl+Alt+H");

                        // 3. Customize the primary accent color
                        App.Themes.SetColor("DsPrimaryBrush", "#00FFCC");
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 2: DYNAMIC THEMING & COLOR TOKENS
    // =========================================================================
    private DocArticle CreateDynamicThemingArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch2_theming",
            Title = "2. Dynamic Theming & Color Tokens",
            Subtitle = "Mutate color palettes and Avalonia resource brushes live on the UI thread.",
            ReadingTime = "4 min read",
            Summary = "Learn how to switch themes, override individual brush and color tokens, and customize studio visuals without restarting.",
            Keywords = new List<string> { "theme", "colors", "dracula", "cyberpunk", "palette", "tokens", "brush" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Live Color System",
                    Content = "C# Code Studio uses a dynamic tokenized resource system. The DynamicThemeEngine modifies Avalonia Application resources on the fly, immediately updating all open tabs, margins, syntax themes, and toolbars.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Setting a token like 'DsPrimaryBrush' automatically creates and syncs the corresponding 'DsPrimaryColor' Color struct for controls requiring raw colors."
                },
                new()
                {
                    Heading = "Built-In Theme Presets",
                    Content = "You can activate pre-configured, high-contrast modern themes at any time:",
                    BulletPoints = new List<string>
                    {
                        "'dark-plus': Visual Studio Code Dark+ classic default.",
                        "'light-plus': Clean high-contrast modern light theme.",
                        "'dracula': Iconic vibrant purple, cyan, and pink dark palette.",
                        "'cyberpunk': Electric yellow, neon cyan, and dark carbon palette.",
                        "'monokai': Warm high-contrast code studio classic.",
                        "'one-dark': Balanced Atom One Dark palette."
                    }
                },
                new()
                {
                    Heading = "Key Theme Color Tokens",
                    Content = "Common brush tokens available for real-time override include:",
                    BulletPoints = new List<string>
                    {
                        "'DsPrimaryBrush': Accent color for active tabs, focused borders, and highlights.",
                        "'DsBackgroundBrush': Background for the main canvas, activity bar, and docks.",
                        "'DsSurfaceBrush': Surface tint for dialogs, sidebars, and tab headers.",
                        "'DsBorderBrush': Subdued divider lines and panel splitters.",
                        "'DsForegroundBrush': Main readable text across editors and panels."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "void", MethodName = "App.Themes.ApplyTheme", Parameters = "string themeId", Description = "Activates a built-in theme preset by ID (e.g. 'dracula', 'cyberpunk', 'dark-plus')." },
                new() { ReturnType = "void", MethodName = "App.Themes.SetColor", Parameters = "string tokenName, string hexOrRgb", Description = "Overrides a specific color token live (e.g. '#00FFCC', 'rgb(0, 255, 204)')." },
                new() { ReturnType = "void", MethodName = "App.Themes.ResetToDefaults", Parameters = "", Description = "Clears all token overrides and restores the active theme's defaults." },
                new() { ReturnType = "string", MethodName = "App.Themes.ActiveThemeId", Parameters = "", Description = "Gets the identifier of the currently active theme preset." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch2_theme_snippet",
                    Title = "Applying Themes & Fine-Tuning Accents",
                    Description = "Switch to Dracula and inject a custom neon cyan accent and dark background.",
                    Code = """
                        // Apply the base Dracula theme preset
                        App.Themes.ApplyTheme("dracula");

                        // Override specific interface tokens live
                        App.Themes.SetColor("DsPrimaryBrush", "#00FFCC");    // Neon cyan highlight
                        App.Themes.SetColor("DsBgBrush", "#0D1117");         // Deep GitHub carbon
                        App.Themes.SetColor("DsSurfaceBrush", "#161B22");    // Sidebar & deck card surface
                        App.Themes.SetColor("DsBorderBrush", "#30363D");     // Subtle divider borders

                        App.UI.ShowSuccess("Customized theme applied!");
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 3: LAYOUT SPACING, DIMENSIONS & DENSITIES
    // =========================================================================
    private DocArticle CreateLayoutDensityAndDimensionsArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch3_spacing",
            Title = "3. Layout Spacing, Dimensions & Densities",
            Subtitle = "Control layout density, padding, tab heights, and arbitrary Avalonia resources.",
            ReadingTime = "3 min read",
            Summary = "Tailor the studio interface for compact laptops or spacious 4K monitors using density modes and dimension tokens.",
            Keywords = new List<string> { "layout", "density", "spacing", "dimensions", "padding", "compact" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Density Presets",
                    Content = "Layout density dynamically adjusts standard margins, tab sizes, and line heights across both Script Studio and Notebook Studio:",
                    BulletPoints = new List<string>
                    {
                        "LayoutDensity.Compact: Minimized padding (2-4px), 30px tabs, maximized code canvas for small screens.",
                        "LayoutDensity.Comfortable: Balanced standard spacing (4-8px), 34px tabs, default ergonomic layout.",
                        "LayoutDensity.Spacious: Relaxed generous padding (8-16px), 38px tabs, touch and large display friendly."
                    }
                },
                new()
                {
                    Heading = "Arbitrary Resource Manipulation",
                    Content = "Beyond predefined tokens, 'SetResource' allows injecting any arbitrary object into Avalonia's Application.Current.Resources dictionary, enabling advanced customization of brushes, templates, and styles."
                },
                new()
                {
                    Heading = "Key Layout & Spacing Tokens",
                    Content = "Adjust specific interface element dimensions individually with 'SetDimension' and 'SetSpacing':",
                    BulletPoints = new List<string>
                    {
                        "'DensityTabHeight': Height of editor tabs in the tab strip (default: 34.0).",
                        "'DensityRailWidth': Width of the leftmost Activity Bar rail (default: 48.0).",
                        "'DensityPaddingSmall': Internal padding inside tool deck cards and lists (default: 4.0).",
                        "'DensityPaddingMedium': Standard layout spacing between editor controls (default: 8.0).",
                        "'DensityCornerRadius': Rounding radius for cards, buttons, and flyouts."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "void", MethodName = "App.Themes.SetDensity", Parameters = "LayoutDensity density", Description = "Applies a predefined density preset: Compact, Comfortable, or Spacious." },
                new() { ReturnType = "void", MethodName = "App.Themes.SetSpacing", Parameters = "string tokenName, double value", Description = "Dynamically adjusts spacing tokens like 'DensityPaddingSmall' or 'DensityPaddingMedium'." },
                new() { ReturnType = "void", MethodName = "App.Themes.SetDimension", Parameters = "string tokenName, double value", Description = "Adjusts component dimensions like 'DensityTabHeight' or 'DensityRailWidth'." },
                new() { ReturnType = "void", MethodName = "App.Themes.SetResource", Parameters = "string tokenName, object value", Description = "Inserts or replaces an arbitrary resource in Avalonia's resource dictionary." },
                new() { ReturnType = "object?", MethodName = "App.Themes.GetResource", Parameters = "string tokenName", Description = "Retrieves an active resource by name from the Avalonia resource dictionary." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch3_density_snippet",
                    Title = "Configuring Ultra-Compact Density",
                    Description = "Set compact mode and fine-tune tab height and padding for maximum screen real estate.",
                    Code = """
                        // Set layout density to Compact
                        App.Themes.SetDensity(LayoutDensity.Compact);

                        // Fine-tune specific dimension tokens
                        App.Themes.SetDimension("DensityTabHeight", 28.0);
                        App.Themes.SetSpacing("DensityPaddingSmall", 2.0);
                        App.Themes.SetSpacing("DensityPaddingMedium", 6.0);

                        App.UI.ShowInfo("Ultra-compact layout activated.");
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 4: CUSTOM COMMANDS & KEYBOARD SHORTCUTS
    // =========================================================================
    private DocArticle CreateCustomCommandsArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch4_commands",
            Title = "4. Custom Commands & Keyboard Shortcuts",
            Subtitle = "Register custom actions, hotkeys, and Command Palette entries.",
            ReadingTime = "4 min read",
            Summary = "Discover how to register actions that appear automatically in the Command Palette (Ctrl+Shift+P) and bind custom global hotkeys.",
            Keywords = new List<string> { "commands", "shortcuts", "hotkeys", "palette", "keybindings" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Command Palette Integration",
                    Content = "Commands registered via 'App.Commands.Register' instantly appear in the searchable Command Palette (Ctrl+Shift+P / Cmd+Shift+P) across both Script Studio and Notebook Studio without restarting the application.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Commands automatically dispatch to the Avalonia UI thread, making it completely safe to interact with UI controls directly inside your action callbacks."
                },
                new()
                {
                    Heading = "Keyboard Gestures",
                    Content = "Assign standard modifier shortcuts using string gestures such as 'Ctrl+Alt+H', 'Ctrl+Shift+U', or 'F9'. The studio handles platform translation between Command on macOS and Ctrl on Windows/Linux automatically."
                },
                new()
                {
                    Heading = "Command Categories & Disposables",
                    Content = "Organize commands logically in the palette using category prefixes ('Editor', 'Tools', 'Diagnostic'). Registering returns an IDisposable handle; disposing it cleanly tears down the command upon script reload."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IDisposable", MethodName = "App.Commands.Register", Parameters = "string id, string title, Action action, string? gesture = null, string? category = null", Description = "Registers a command into the pipeline and palette with an optional keyboard gesture." },
                new() { ReturnType = "Task<bool>", MethodName = "App.Commands.ExecuteAsync", Parameters = "string commandId, object? parameter = null", Description = "Executes a registered command programmatically by its unique ID." },
                new() { ReturnType = "bool", MethodName = "App.Commands.HasCommand", Parameters = "string commandId", Description = "Checks whether a command with the given ID is currently registered." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch4_commands_snippet",
                    Title = "Registering Custom Commands",
                    Description = "Register a timestamp insertion command and an interactive greeting with shortcuts.",
                    Code = """
                        // Register a command with a hotkey
                        using var cmd1 = App.Commands.Register("editor.insert_timestamp", "Insert Current Timestamp", () =>
                        {
                            var doc = App.Editor.ActiveDocument;
                            if (doc != null)
                            {
                                doc.Text += $"\n// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                                App.UI.ShowSuccess("Timestamp appended!");
                            }
                        }, gesture: "Ctrl+Alt+T", category: "Editor");

                        // Register a notification trigger
                        using var cmd2 = App.Commands.Register("studio.status_check", "Check Studio Status", () =>
                        {
                            var mem = GC.GetTotalMemory(false) / 1024 / 1024;
                            App.UI.ShowInfo($"Memory: {mem} MB • .NET {Environment.Version}");
                        }, gesture: "Ctrl+Alt+S", category: "Diagnostic");
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 5: COMMAND PIPELINE MIDDLEWARE
    // =========================================================================
    private DocArticle CreateCommandMiddlewareArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch5_middleware",
            Title = "5. Command Pipeline Middleware",
            Subtitle = "Intercept, log, time, and guard command execution with onion middleware.",
            ReadingTime = "4 min read",
            Summary = "Learn how to wrap command execution with middleware to implement audit logging, execution timing, and security gates.",
            Keywords = new List<string> { "middleware", "pipeline", "interception", "audit", "security" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "The Onion Pipeline Pattern",
                    Content = "All command executions pass through a chain of asynchronous middleware delegates. Each middleware receives the 'CommandInvocationContext' and a 'next' delegate, allowing execution logic before and after the inner command runs.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Calling 'context.Cancel()' halts the pipeline immediately and prevents the target command from executing."
                },
                new()
                {
                    Heading = "Intercepting & Guarding Execution",
                    Content = "Middleware can inspect incoming command IDs or arguments and call 'context.Cancel(\"Reason\")' to halt the command, protecting sensitive files or enforcing confirmation before destructive actions."
                },
                new()
                {
                    Heading = "Execution Timing & Telemetry",
                    Content = "Because middleware wraps execution with 'await next()', you can measure high-precision execution durations using Stopwatch and record metrics or logs across all studio interactions."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IDisposable", MethodName = "App.Commands.Use", Parameters = "Func<CommandInvocationContext, Func<Task>, Task> middleware", Description = "Appends a middleware step to the global command execution pipeline." },
                new() { ReturnType = "string", MethodName = "context.CommandId", Parameters = "", Description = "The identifier of the command being invoked." },
                new() { ReturnType = "void", MethodName = "context.Cancel", Parameters = "string? reason = null", Description = "Cancels execution of the command and prevents subsequent middlewares from running." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch5_middleware_snippet",
                    Title = "Execution Timing & Safety Middleware",
                    Description = "Measure the elapsed time of all commands and log them to debug output.",
                    Code = """
                        using System.Diagnostics;

                        // Install execution timing middleware
                        App.Commands.Use(async (context, next) =>
                        {
                            var sw = Stopwatch.StartNew();
                            
                            // Execute the command (and inner middlewares)
                            await next();
                            
                            sw.Stop();
                            if (sw.ElapsedMilliseconds > 50)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Telemetry] Command '{context.CommandId}' took {sw.ElapsedMilliseconds}ms");
                            }
                        });
                        """
                }
            }
        };
    }
}
