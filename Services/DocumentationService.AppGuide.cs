using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildAppGuideCategory()
    {
        return new DocCategory
        {
            Id = "app_guide",
            Title = "Application Guide",
            IconKind = MaterialIconKind.ApplicationOutline,
            AccentColor = "#38BDF8",
            Badge = "Getting Started",
            Description = "Learn how to use C# Code Studio, navigate the 5-zone VS Code layout, manage scripts, and debug .NET code.",
            Articles = new List<DocArticle>
            {
                CreateWelcomeArticle(),
                CreateVsCodeLayoutArticle(),
                CreateScriptsVsNotebooksArticle(),
                CreateDebuggingGuideArticle(),
                CreateNuGetGuideArticle()
            }
        };
    }

    private DocArticle CreateWelcomeArticle()
    {
        return new DocArticle
        {
            Id = "welcome_guide",
            Title = "Welcome to C# Code Studio",
            Subtitle = "A high-performance .NET 10 document scripting, algorithm, and interactive notebook IDE.",
            ReadingTime = "3 min read",
            Summary = "C# Code Studio brings Visual Studio Code ergonomics to FryPDF for document scripting, algorithmic exploration, and dynamic data visualization.",
            Keywords = new List<string> { "overview", "intro", "architecture", "roslyn", "net10", "welcome", "getting started" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Core Paradigm & Purpose",
                    Content = "C# Code Studio is a premier, in-app development environment built for .NET 10 within FryPDF. It provides Roslyn-powered C# scripting, interactive polyglot-style notebooks, algorithm visualizers, and interactive inspection decks without requiring an external IDE.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "All compilation and reflection operations execute off the UI thread via background task workers, guaranteeing zero UI-thread freezes."
                },
                new()
                {
                    Heading = "Key Capabilities",
                    Content = "Everything you need for rapid C# document automation and algorithm prototyping is integrated:",
                    BulletPoints = new List<string>
                    {
                        "Roslyn C# 13 & .NET 10 script execution engine with live diagnostics.",
                        "Interactive Polyglot Notebooks (.ipynb / .csnb) with cell reordering, collapsing, and rich outputs.",
                        "VisualizerRecorder framework for step-by-step playback of sorting, trees, matrices, and graphs.",
                        "One-click .Dump() inspection for collections, objects, dataframes, images, and tables.",
                        "Visual Studio Code Dark+ theme, gutter breakpoints, stepping debugger, and hover data inspection."
                    }
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_hello_world",
                    Title = "Your First C# Studio Script",
                    Description = "Write modern top-level C# code and use .Dump() to view output in the bottom deck.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"// Welcome to C# Code Studio!
var message = ""Hello from .NET 10 Roslyn Scripting!"";
Console.WriteLine(message);

var numbers = Enumerable.Range(1, 10).Select(n => new { Number = n, Square = n * n, Cube = n * n * n });
numbers.Dump(""Number Powers Table"");"
                }
            }
        };
    }

    private DocArticle CreateVsCodeLayoutArticle()
    {
        return new DocArticle
        {
            Id = "vscode_layout",
            Title = "The 5-Zone VS Code Layout",
            Subtitle = "Master the authentic Visual Studio Code chrome, zones, panels, and shortcuts.",
            ReadingTime = "4 min read",
            Summary = "Every studio window adheres to a strict 5-zone layout structure mirroring VS Code for familiar, lightning-fast navigation.",
            Keywords = new List<string> { "layout", "5-zone", "activity bar", "sidebar", "deck", "status bar", "dock", "chrome" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Zone 1: Activity Bar (Leftmost 48px)",
                    Content = "The leftmost vertical rail hosts tool icons: Explorer (file tree), Search (workspace grep), Run & Debug, Dependencies & NuGet, Scratchpad & Notes, and Problems (with error badges). The bottom features Settings and Return to Hub.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Clicking the currently active icon toggles the Primary Side Bar closed or open (Ctrl+B / Cmd+B)."
                },
                new()
                {
                    Heading = "Zone 2: Primary Side Bar (Resizable ~270px)",
                    Content = "Dynamically renders the view selected in the Activity Bar. The Explorer includes New File, New Folder, Open Project, Refresh, and Collapse All controls.",
                    BulletPoints = new List<string>
                    {
                        "Explorer: Full workspace directory tree with inline rename and delete.",
                        "Search: Workspace-wide text search with regex and match navigation.",
                        "Debug: Call stack, local variables, and watch expressions.",
                        "NuGet: Live package search, install, and dependency tree inspection.",
                        "Problems: Diagnostic errors and warnings with click-to-line navigation."
                    }
                },
                new()
                {
                    Heading = "Zone 3: Editor Area",
                    Content = "The central canvas hosts the multi-tab bar (dirty status indicators, close buttons, tab switching), breadcrumbs trail, and the AvaloniaEdit syntax canvas with line numbers, code folding, and breakpoint gutter. Rest the pointer on any symbol (or press Ctrl+K Ctrl+I) to see its signature, where it's declared and its documentation."
                },
                new()
                {
                    Heading = "Zone 4: Bottom Tool Deck (Ctrl+J / Cmd+J)",
                    Content = "Collapsible and resizable bottom panel featuring 6 specialized tabs:",
                    BulletPoints = new List<string>
                    {
                        "PROBLEMS: Roslyn diagnostic errors, warnings, and code analysis.",
                        "OUTPUT: Compiler messages and build logs.",
                        "TERMINAL / CONSOLE: Standard stdout/stderr streams.",
                        "DEBUG CONSOLE (REPL): Immediate expression evaluation prompt.",
                        "RESULTS (.DUMP): Interactive tables, HTML viewer, and object tree inspector.",
                        "TEST CASES: Unit test discovery, execution runners, and assertions."
                    }
                },
                new()
                {
                    Heading = "Zone 5: Status Bar (Bottom 22px)",
                    Content = "Displays workspace status, diagnostic problem count, Roslyn compiler state, execution elapsed timer, cursor line/col, encoding (UTF-8), and language mode."
                }
            }
        };
    }

    private DocArticle CreateScriptsVsNotebooksArticle()
    {
        return new DocArticle
        {
            Id = "scripts_vs_notebooks",
            Title = "Scripts (.csx) vs Interactive Notebooks",
            Subtitle = "Understand the differences between single-file scripts and cell-based notebooks.",
            ReadingTime = "3 min read",
            Summary = "Choose between linear script automation and interactive exploratory computing with live cell execution.",
            Keywords = new List<string> { "scripts", "notebooks", "cells", "csx", "ipynb", "interactive", "polyglot" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "C# Scripts (.csx / .linq)",
                    Content = "Scripts are ideal for document automation pipelines, batch PDF utilities, algorithm implementations, and standalone tools. They execute from top to bottom and support top-level statements without boilerplate classes.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Press F5 or Ctrl+F5 to compile and run the active script immediately."
                },
                new()
                {
                    Heading = "Interactive Notebooks (.ipynb / .csnb)",
                    Content = "Notebooks divide code into independent, sequential cells. You can mix rich Markdown documentation with executable C# code cells. Variables and imports defined in one cell persist into subsequent cell executions.",
                    BulletPoints = new List<string>
                    {
                        "Run individual cell: Shift+Enter",
                        "Run all cells: Click 'Run All' in the notebook toolbar.",
                        "Cell collapsing: Double-click a cell header or click the collapse chevron to tuck away large code blocks.",
                        "Rich outputs: Visualizers, charts, tables, and images render directly beneath each cell."
                    }
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_notebook_cell",
                    Title = "Interactive Notebook Cell Example",
                    Description = "Define data in one cell, compute in another, and display rich results.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Notebook,
                    Code = @"// Cell 1: Define dataset
var sales = new[]
{
    new { Quarter = ""Q1"", Revenue = 125000, Growth = 0.12 },
    new { Quarter = ""Q2"", Revenue = 142000, Growth = 0.14 },
    new { Quarter = ""Q3"", Revenue = 168000, Growth = 0.18 },
    new { Quarter = ""Q4"", Revenue = 210000, Growth = 0.25 },
};

// Render interactive table
sales.Dump(""2026 Fiscal Performance"");"
                }
            }
        };
    }

    private DocArticle CreateDebuggingGuideArticle()
    {
        return new DocArticle
        {
            Id = "debugging_guide",
            Title = "Debugging & Diagnostics",
            Subtitle = "Set breakpoints, step through code, inspect variables, and evaluate expressions.",
            ReadingTime = "4 min read",
            Summary = "Full stepping debugger with breakpoints, hover data tips, watch variables, and immediate REPL.",
            Keywords = new List<string> { "debugging", "breakpoints", "f5", "f10", "f11", "step", "repl", "variables", "watch" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Setting Breakpoints",
                    Content = "Click in the editor gutter margin next to any executable line to set a breakpoint (red circular dot), or place your cursor on a line and press F9.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "When debugging starts (F5), execution automatically pauses when hitting a breakpoint, highlighting the current line in amber."
                },
                new()
                {
                    Heading = "Stepping Controls",
                    Content = "Use standard VS Code stepping shortcuts:",
                    BulletPoints = new List<string>
                    {
                        "F5: Start Debugging / Continue Execution to next breakpoint.",
                        "F10: Step Over current statement.",
                        "F11: Step Into method call.",
                        "Shift+F5: Stop Debugging / Terminate execution process."
                    }
                },
                new()
                {
                    Heading = "Variable Inspection & Immediate REPL",
                    Content = "While paused, hover over any variable in the code canvas to see its current value in a floating inspection tip. The Run & Debug sidebar lists all local variables, and the Bottom Deck's REPL prompt lets you evaluate arbitrary expressions in the current scope."
                }
            }
        };
    }

    private DocArticle CreateNuGetGuideArticle()
    {
        return new DocArticle
        {
            Id = "nuget_dependencies",
            Title = "NuGet Packages & Directives",
            Subtitle = "Reference external packages, assemblies, and shared scripts using Roslyn directives.",
            ReadingTime = "3 min read",
            Summary = "Import packages directly into scripts and notebooks with automatic resolution and assembly metadata.",
            Keywords = new List<string> { "nuget", "packages", "dependencies", "r directive", "load", "restore" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Referencing NuGet Packages (#r directive)",
                    Content = "You can add any NuGet package directly in your script or notebook cell using the standard Roslyn directive at the top of your document:",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "The background NuGet resolver automatically fetches and restores dependencies upon compilation."
                },
                new()
                {
                    Heading = "Referencing Local Assemblies & Scripts",
                    Content = "Reference local DLLs or include shared script files using:",
                    BulletPoints = new List<string>
                    {
                        "#r \"nuget: Newtonsoft.Json, 13.0.3\" — Pulls package from NuGet.org.",
                        "#r \"path/to/MyCustomLibrary.dll\" — References a precompiled assembly.",
                        "#load \"common/Helpers.csx\" — Inlines another script file before execution."
                    }
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_nuget_usage",
                    Title = "Using NuGet Packages in Scripts",
                    Description = "Reference external libraries like Newtonsoft.Json or Humanizer directly in your script.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"#r ""nuget: Newtonsoft.Json, 13.0.3""
using Newtonsoft.Json;

var payload = new { Project = ""FryPDF"", Version = 10.0, Features = new[] { ""Roslyn"", ""Visualizer"", ""Notebooks"" } };
string json = JsonConvert.SerializeObject(payload, Formatting.Indented);

Console.WriteLine(json);"
                }
            }
        };
    }
}
