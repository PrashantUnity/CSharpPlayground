using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    // =========================================================================
    // CHAPTER 6: EDITOR & DOCUMENT MANIPULATION
    // =========================================================================
    private DocArticle CreateEditorManipulationArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch6_editor",
            Title = "6. Editor & Document Manipulation",
            Subtitle = "Inspect and modify code, selections, carets, and open files programmatically.",
            ReadingTime = "4 min read",
            Summary = "Discover how to read and write active code, format documents, programmatically create source files, and open workspace documents.",
            Keywords = new List<string> { "editor", "document", "formatting", "active cell", "source code", "open file" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "The Unified Document Context",
                    Content = "Through 'App.Editor.ActiveDocument', your scripts interact with whatever file is currently active. The studio automatically adapts this interface across both Script Studio (entire script file) and Notebook Studio (currently active code cell).",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "You can programmatically trigger formatting or saving on the active document with a single call."
                },
                new()
                {
                    Heading = "Creating & Opening Files",
                    Content = "The Editor API also supports opening arbitrary files in the workspace via 'OpenFileAsync' or creating brand-new source files populated with initial starter code via 'CreateDocumentAsync'."
                },
                new()
                {
                    Heading = "Multi-Language & Notebook Parity",
                    Content = "Whether modifying a C# script (.frycs, .csx), a Python script (.py), or a multi-language notebook (.ipynb, .csnb), 'App.Editor.ActiveDocument' provides identical text manipulation, saving, and formatting capabilities across all supported languages."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IDocumentContext?", MethodName = "App.Editor.ActiveDocument", Parameters = "", Description = "Gets the context of the currently active document or notebook cell." },
                new() { ReturnType = "string", MethodName = "doc.Text", Parameters = "", Description = "Gets or sets the complete source code text of the active document." },
                new() { ReturnType = "void", MethodName = "doc.Format()", Parameters = "", Description = "Runs the language-specific document formatter on the active document." },
                new() { ReturnType = "void", MethodName = "doc.Save()", Parameters = "", Description = "Persists the active document or notebook changes to disk." },
                new() { ReturnType = "Task", MethodName = "App.Editor.OpenFileAsync", Parameters = "string filePath", Description = "Opens a file from disk or workspace in an editor tab." },
                new() { ReturnType = "Task", MethodName = "App.Editor.CreateDocumentAsync", Parameters = "string languageId = \"csharp\", string? initialCode = null", Description = "Creates a new editor document with optional starter code." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch6_editor_snippet",
                    Title = "Automated License Header & Formatting",
                    Description = "Prepend a standard copyright header to the active document and format it.",
                    Code = """
                        var doc = App.Editor.ActiveDocument;
                        if (doc != null && !doc.Text.StartsWith("// <copyright"))
                        {
                            string header = "// <copyright file=\"Generated.cs\" company=\"FryPDF\">\n" +
                                            "// All rights reserved. Built with C# Code Studio.\n" +
                                            "// </copyright>\n\n";
                            doc.Text = header + doc.Text;
                            doc.Format();
                            App.UI.ShowSuccess("Header added and document formatted!");
                        }
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 7: EXECUTION PIPELINE & LIFECYCLE HOOKS
    // =========================================================================
    private DocArticle CreateExecutionLifecycleHooksArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch7_hooks",
            Title = "7. Execution Pipeline & Lifecycle Hooks",
            Subtitle = "Hook into compiler runs, file saves, tab switches, and theme updates.",
            ReadingTime = "5 min read",
            Summary = "Intercept the execution lifecycle to inspect code before execution, benchmark runtime duration, and react to document events.",
            Keywords = new List<string> { "hooks", "lifecycle", "before run", "after run", "audit", "security", "events" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Pre-Execution Guarding",
                    Content = "The 'OnBeforeScriptRun' hook fires immediately before Roslyn compiles code or a notebook cell executes. You can inspect the source code, inject parameters, or cancel execution with a clear reason message.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Cancelling a run stops the compiler immediately and displays your custom reason in the studio status bar."
                },
                new()
                {
                    Heading = "Post-Execution Telemetry",
                    Content = "'OnAfterScriptRun' provides comprehensive execution telemetry, including total elapsed time, success or failure status, standard console output, and error messages."
                },
                new()
                {
                    Heading = "Document & Theme Events",
                    Content = "React to user actions in the studio using 'OnDocumentOpened', 'OnDocumentSaved', and 'OnThemeChanged'."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IDisposable", MethodName = "App.Hooks.OnBeforeScriptRun", Parameters = "Action<ExecutionHookContext> hook", Description = "Subscribes to pre-execution events. Set 'ctx.Cancel()' to abort run." },
                new() { ReturnType = "IDisposable", MethodName = "App.Hooks.OnAfterScriptRun", Parameters = "Action<ExecutionFinishedHookContext> hook", Description = "Subscribes to post-execution events with elapsed time and output." },
                new() { ReturnType = "IDisposable", MethodName = "App.Hooks.OnDocumentOpened", Parameters = "Action<IDocumentContext> hook", Description = "Fires whenever a document or notebook tab becomes active." },
                new() { ReturnType = "IDisposable", MethodName = "App.Hooks.OnDocumentSaved", Parameters = "Action<IDocumentContext> hook", Description = "Fires whenever a script or notebook is saved to disk." },
                new() { ReturnType = "IDisposable", MethodName = "App.Hooks.OnThemeChanged", Parameters = "Action<string> hook", Description = "Fires whenever the active theme preset or palette changes." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch7_hooks_snippet",
                    Title = "Execution Audit & Telemetry Guard",
                    Description = "Intercept scripts before execution and record execution duration upon finish.",
                    Code = """
                        // 1. Guard against accidental dangerous commands
                        App.Hooks.OnBeforeScriptRun(ctx =>
                        {
                            if (ctx.SourceCode.Contains("rm -rf") || ctx.SourceCode.Contains("FormatDrive"))
                            {
                                ctx.Cancel("Execution blocked: dangerous operation detected.");
                            }
                        });

                        // 2. Log execution duration and outcome
                        App.Hooks.OnAfterScriptRun(ctx =>
                        {
                            var status = ctx.Success ? "SUCCEEDED" : "FAILED";
                            App.UI.ShowInfo($"Run {status} in {ctx.Elapsed.TotalMilliseconds:N0}ms");
                        });
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 8: UI CONTRIBUTIONS ACROSS 5 VS CODE ZONES
    // =========================================================================
    private DocArticle CreateUiContributionsArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch8_ui_zones",
            Title = "8. UI Contributions Across 5 VS Code Zones",
            Subtitle = "Add custom items to Activity Bar, Status Bar, and Bottom Tool Deck.",
            ReadingTime = "4 min read",
            Summary = "Learn how to inject controls, status widgets, and tool tabs into the 5-zone Visual Studio Code interface.",
            Keywords = new List<string> { "ui", "activity bar", "status bar", "bottom deck", "widgets", "notifications" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "The 5 VS Code Zones",
                    Content = "The studio layout follows authentic VS Code ergonomics: Zone 1 (Activity Bar), Zone 2 (Side Bar), Zone 3 (Editor Area), Zone 4 (Bottom Tool Deck), and Zone 5 (Status Bar). The UI API allows extending Zones 1, 4, and 5.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Bottom Deck tabs can return ANY Avalonia control (e.g. DataGrid, Canvas, SkiaVisual, or custom UserControl)."
                },
                new()
                {
                    Heading = "Status Bar Widgets & Notifications",
                    Content = "Register clickable status bar widgets with priority ordering, tooltips, and click actions, or show standard desktop toast notifications."
                },
                new()
                {
                    Heading = "Activity Bar & Tool Deck Customization",
                    Content = "Use 'App.UI.RegisterActivityBarItem' to introduce new primary tool views into Zone 1, and 'App.UI.RegisterBottomDeckTab' to present interactive analytical dashboards, REPLs, or live graphs directly inside Zone 4."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "IDisposable", MethodName = "App.UI.RegisterStatusBarWidget", Parameters = "StatusBarWidgetDescriptor descriptor", Description = "Inserts a widget into the bottom Status Bar (Zone 5) with click handling." },
                new() { ReturnType = "IDisposable", MethodName = "App.UI.RegisterActivityBarItem", Parameters = "ActivityBarDescriptor descriptor", Description = "Adds an icon item to the leftmost Activity Bar rail (Zone 1)." },
                new() { ReturnType = "IDisposable", MethodName = "App.UI.RegisterBottomDeckTab", Parameters = "BottomDeckTabDescriptor descriptor", Description = "Adds a tab to the Bottom Tool Deck (Zone 4) with a custom Avalonia control factory." },
                new() { ReturnType = "void", MethodName = "App.UI.ShowSuccess", Parameters = "string message, string title = \"Customization\"", Description = "Displays a green success toast notification." },
                new() { ReturnType = "void", MethodName = "App.UI.ShowError", Parameters = "string message, string title = \"Customization\"", Description = "Displays a red error toast notification." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch8_ui_snippet",
                    Title = "Live Status Bar Widget & Notifications",
                    Description = "Register a clickable widget in the Status Bar that tracks execution counts.",
                    Code = """
                        int clickCount = 0;

                        // Register a status bar widget
                        App.UI.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
                        {
                            Id = "custom.counter",
                            Text = "⚡ Clicks: 0",
                            Tooltip = "Click to increment counter",
                            Priority = 100,
                            OnClick = () =>
                            {
                                clickCount++;
                                App.UI.ShowSuccess($"Widget clicked {clickCount} times!");
                            }
                        });
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 9: CROSS-SESSION STATE & HOT RELOAD BAGS
    // =========================================================================
    private DocArticle CreateCrossSessionStateArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch9_state",
            Title = "9. Cross-Session State & Hot Reload Bags",
            Subtitle = "Persist state, connection pools, and caches across hot-reloads.",
            ReadingTime = "3 min read",
            Summary = "Use App.State to preserve objects, database pools, and session variables across script recompilations.",
            Keywords = new List<string> { "state", "hot-reload", "cache", "persistence", "singleton", "state bag", "IStateBag", "App.State" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "The Hot-Reload State Problem",
                    Content = "When you edit and re-evaluate 'init.csx', normal local variables are destroyed when the old assembly unloads. 'App.State' provides a persistent thread-safe storage bag that survives recompilations.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Use 'App.State.GetOrAdd' to instantiate long-lived resources (e.g. database connections or timers) exactly once."
                },
                new()
                {
                    Heading = "Thread-Safe Key-Value Operations",
                    Content = "The 'IStateBag' interface provides generic 'Get<T>', 'Set<T>', and 'Remove' methods. Values stored here remain alive in memory for the duration of the host application process, completely unaffected by assembly reloads."
                },
                new()
                {
                    Heading = "Singleton Resources & Connection Pools",
                    Content = "Avoid leaking network sockets, background worker threads, or database connections during rapid iterative development by encapsulating factory construction inside 'App.State.GetOrAdd'."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "T?", MethodName = "App.State.Get<T>", Parameters = "string key", Description = "Retrieves a stored value by key, or returns default if not found." },
                new() { ReturnType = "void", MethodName = "App.State.Set<T>", Parameters = "string key, T value", Description = "Stores or updates a value in the persistent state bag." },
                new() { ReturnType = "T", MethodName = "App.State.GetOrAdd<T>", Parameters = "string key, Func<T> factory", Description = "Retrieves an existing value or calls factory to initialize it on first access." },
                new() { ReturnType = "bool", MethodName = "App.State.Remove", Parameters = "string key", Description = "Removes a value from the state bag." },
                new() { ReturnType = "void", MethodName = "App.State.Clear", Parameters = "", Description = "Removes all stored values from the state bag." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch9_state_snippet",
                    Title = "Persistent Counter & HTTP Client",
                    Description = "Preserve a single shared HttpClient and execution count across script re-evaluations.",
                    Code = """
                        using System.Net.Http;

                        // 1. Get or create a single shared HttpClient instance
                        var http = App.State.GetOrAdd("shared.http", () => new HttpClient());

                        // 2. Track persistent execution counter
                        int runs = App.State.Get<int>("custom.run_count") + 1;
                        App.State.Set("custom.run_count", runs);

                        App.UI.ShowInfo($"Evaluation #{runs} (Shared HTTP Client ready)");
                        """
                }
            }
        };
    }

    // =========================================================================
    // CHAPTER 10: MULTI-FILE EXTENSIONS & MANIFEST ARCHITECTURE
    // =========================================================================
    private DocArticle CreateMultiFileExtensionsArticle()
    {
        return new DocArticle
        {
            Id = "extensibility_ch10_extensions",
            Title = "10. Multi-File Extensions & Manifest Architecture",
            Subtitle = "Package, distribute, and load multi-file plugins with isolated contexts.",
            ReadingTime = "5 min read",
            Summary = "Package multi-file C# extensions with an extension.json manifest, isolated assembly loading, and clean activation lifecycles.",
            Keywords = new List<string> { "extensions", "manifest", "extension.json", "plugins", "packaging", "assemblyloadcontext", "entryPoint", "lifecycle" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Extension Package Structure",
                    Content = "For larger plugins, multi-file extensions reside in '.frysharp/extensions/<PackageName>/' or '~/.frysharp/extensions/'. Each package contains an 'extension.json' manifest and one or more C# source files.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Multi-file extensions are compiled into isolated AssemblyLoadContexts and watch their directory for live hot-reload."
                },
                new()
                {
                    Heading = "The Manifest Schema",
                    Content = "The 'extension.json' manifest defines package metadata and the entry point class:",
                    BulletPoints = new List<string>
                    {
                        "id: Unique identifier (e.g. 'com.example.git_tools').",
                        "name: Human-readable display name.",
                        "version: Semantic version (e.g. '1.0.0').",
                        "entryPoint: Fully qualified class implementing IExtensionEntryPoint.",
                        "description: Summary of what the extension provides."
                    }
                },
                new()
                {
                    Heading = "Implementing IExtensionEntryPoint",
                    Content = "Your entry class implements 'OnActivate(IExtensionContext context)' and 'OnDeactivate()'. The context provides isolated state storage, package storage paths, and the ambient 'App' facade."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { ReturnType = "void", MethodName = "IExtensionEntryPoint.OnActivate", Parameters = "IExtensionContext context", Description = "Called when the extension is loaded or hot-reloaded into memory." },
                new() { ReturnType = "void", MethodName = "IExtensionEntryPoint.OnDeactivate", Parameters = "", Description = "Called before the extension assembly unloads to release unmanaged resources." },
                new() { ReturnType = "IStudioApp", MethodName = "context.App", Parameters = "", Description = "The ambient studio application facade." },
                new() { ReturnType = "ExtensionManifest", MethodName = "context.Manifest", Parameters = "", Description = "Metadata loaded from extension.json." },
                new() { ReturnType = "IStateBag", MethodName = "context.State", Parameters = "", Description = "Isolated state store scoped exclusively to this extension package." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "ch10_manifest_snippet",
                    Title = "extension.json Manifest Example",
                    Description = "Configuration manifest placed at the root of the extension folder.",
                    Code = """
                        {
                          "id": "com.frypdf.extension.tools",
                          "name": "Developer Power Tools",
                          "version": "1.0.0",
                          "author": "Codefry",
                          "entryPoint": "MyExtension.PowerToolsPlugin",
                          "description": "Custom developer commands, themes, and bottom deck widgets."
                        }
                        """
                },
                new()
                {
                    Id = "ch10_entrypoint_snippet",
                    Title = "C# Entry Point Implementation",
                    Description = "The main class implementing IExtensionEntryPoint inside the package.",
                    Code = """
                        using FrySharp.Sdk;

                        namespace MyExtension;

                        public class PowerToolsPlugin : IExtensionEntryPoint
                        {
                            private IDisposable? _commandRegistration;

                            public void OnActivate(IExtensionContext context)
                            {
                                // Register command using ambient App
                                _commandRegistration = context.App.Commands.Register(
                                    "powertools.greet",
                                    "PowerTools: Greet",
                                    () => context.App.UI.ShowSuccess("PowerTools extension activated!")
                                );

                                context.App.UI.ShowSuccess($"Extension {context.Manifest.Name} v{context.Manifest.Version} loaded!");
                            }

                            public void OnDeactivate()
                            {
                                _commandRegistration?.Dispose();
                            }
                        }
                        """
                }
            }
        };
    }
}
