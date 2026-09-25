using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildDisplayApisCategory()
    {
        return new DocCategory
        {
            Id = "display_apis",
            Title = "Interactive Display & .Dump()",
            IconKind = MaterialIconKind.TelevisionGuide,
            AccentColor = "#E3B341",
            Badge = "Output APIs",
            Description = "Rich visual outputs: interactive tables, object inspectors, HTML, Markdown, images, and live animations.",
            Articles = new List<DocArticle>
            {
                CreateDumpApiArticle(),
                CreateTableAndInspectorArticle(),
                CreateHtmlAndMarkdownArticle(),
                CreateImageDisplayArticle(),
                CreateAnimateAndCancellationArticle()
            }
        };
    }

    private DocCategory BuildShortcutsCategory()
    {
        return new DocCategory
        {
            Id = "shortcuts_category",
            Title = "Keyboard Shortcuts",
            IconKind = MaterialIconKind.KeyboardOutline,
            AccentColor = "#F97316",
            Badge = "Keybindings",
            Description = "Visual Studio Code keyboard shortcuts reference for macOS and Windows/Linux.",
            Articles = new List<DocArticle>
            {
                CreateShortcutsReferenceArticle()
            }
        };
    }

    private DocArticle CreateDumpApiArticle()
    {
        return new DocArticle
        {
            Id = "dump_api",
            Title = "The .Dump() Extension Method",
            Subtitle = "Inspect any variable, array, collection, or object in the interactive results deck.",
            ReadingTime = "3 min read",
            Summary = ".Dump() is the primary exploratory method in C# Code Studio. It turns any C# object into an interactive visual table and prints structured representation to the console.",
            Keywords = new List<string> { "dump", "table", "inspect", "linq", "console", "results" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Usage of .Dump()",
                    Content = "Simply call .Dump() on any expression. You can supply an optional string label to organize multiple outputs:",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = ".Dump() returns the original object, so it can be inserted inline into LINQ method chains without altering the pipeline!"
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new()
                {
                    MethodName = "obj.Dump<T>",
                    ReturnType = "T",
                    Parameters = "this T obj, string? label = null",
                    Description = "Renders an interactive visual table in the Results dock tab and writes formatted representation to console. Returns obj."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_dump_linq",
                    Title = "Inline .Dump() in LINQ Pipeline",
                    Description = "Inspect intermediate and final query results with inline labels.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var topUsers = new[]
{
    new { Id = 1, Name = ""Alice Smith"", Role = ""Admin"", Score = 98 },
    new { Id = 2, Name = ""Bob Jones"", Role = ""User"", Score = 84 },
    new { Id = 3, Name = ""Carol White"", Role = ""Manager"", Score = 92 },
    new { Id = 4, Name = ""David Brown"", Role = ""User"", Score = 76 }
}
.Where(u => u.Score >= 80).Dump(""Filtered High Scorers"")
.OrderByDescending(u => u.Score)
.ToList().Dump(""Sorted Final Ranking"");"
                }
            }
        };
    }

    private DocArticle CreateTableAndInspectorArticle()
    {
        return new DocArticle
        {
            Id = "table_and_inspector",
            Title = "Tables & Object Inspector",
            Subtitle = "Interactive DataGrid tables and hierarchical object property trees.",
            ReadingTime = "3 min read",
            Summary = "Display.Table and Display.Inspector produce rich, interactive UI controls in the Bottom Tool Deck.",
            Keywords = new List<string> { "table", "datagrid", "inspector", "reflection", "properties" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Interactive Data Tables (Display.Table)",
                    Content = "Display.Table creates a high-performance DataGrid with sortable column headers, search filtering, and row count metrics. It accepts any IEnumerable or dictionary."
                },
                new()
                {
                    Heading = "Object Tree Inspector (Display.Inspector)",
                    Content = "Display.Inspector generates an expandable tree showing all public and private properties, field values, and internal nested objects for deep debugging."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_table_demo",
                    Title = "Display.Table with Custom Data",
                    Description = "Generate an interactive data table with custom columns.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"var inventory = new[]
{
    new { SKU = ""PROD-101"", Name = ""Wireless Mouse"", Price = 29.99, Stock = 145 },
    new { SKU = ""PROD-102"", Name = ""Mechanical Keyboard"", Price = 89.99, Stock = 42 },
    new { SKU = ""PROD-103"", Name = ""USB-C Dock"", Price = 119.50, Stock = 18 }
};

Display.Table(inventory, label: ""Warehouse Stock Inventory"");"
                }
            }
        };
    }

    private DocArticle CreateHtmlAndMarkdownArticle()
    {
        return new DocArticle
        {
            Id = "html_and_markdown",
            Title = "HTML & Markdown Rendering",
            Subtitle = "Emit styled HTML cards, SVG vectors, and formatted Markdown.",
            ReadingTime = "3 min read",
            Summary = "Use Display.Html and Display.Markdown to output rich web-style documentation, badges, and vector charts.",
            Keywords = new List<string> { "html", "markdown", "svg", "css", "styling", "cards" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Rich HTML Output (Display.Html)",
                    Content = "Render styled markup with inline CSS, SVG charts, and custom formatting:",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "HTML outputs render safely inside the bottom results panel and notebook output regions."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_html_card",
                    Title = "Render Styled HTML Card",
                    Description = "Create a modern visual status banner with HTML and inline CSS.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"Display.Html(@""
<div style='background: #0d1117; border: 1px solid #30363d; border-radius: 8px; padding: 16px; color: #c9d1d9; font-family: sans-serif;'>
    <h3 style='margin: 0 0 8px 0; color: #58a6ff;'>🚀 Migration Job Succeeded</h3>
    <p style='margin: 0;'>Processed <b>1,420</b> PDF document pages in <b>214 ms</b>.</p>
    <div style='margin-top: 10px; font-size: 12px; color: #8b949e;'>Engine: Roslyn .NET 10 • Status: OK</div>
</div>
"");"
                }
            }
        };
    }

    private DocArticle CreateImageDisplayArticle()
    {
        return new DocArticle
        {
            Id = "image_display",
            Title = "Displaying Images & Bitmaps",
            Subtitle = "Render Avalonia Bitmaps, SkiaSharp surfaces, PNG byte arrays, and data URIs.",
            ReadingTime = "3 min read",
            Summary = "Display.Image renders graphics directly into the output deck with automatic format detection.",
            Keywords = new List<string> { "image", "bitmap", "skiasharp", "png", "jpeg", "graphics" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Supported Image Types",
                    Content = "Display.Image handles multiple graphic representations seamlessly:",
                    BulletPoints = new List<string>
                    {
                        "byte[] containing PNG, JPEG, GIF, or WebP bytes.",
                        "Avalonia.Media.Imaging.Bitmap instances.",
                        "SkiaSharp SKBitmap, SKImage, SKSurface, and SKData objects.",
                        "Base64 data URIs (data:image/png;base64,...).",
                        "File system paths (path/to/chart.png)."
                    }
                }
            }
        };
    }

    private DocArticle CreateAnimateAndCancellationArticle()
    {
        return new DocArticle
        {
            Id = "animate_and_cancellation",
            Title = "Live Animations & Cancellation",
            Subtitle = "60 FPS non-blocking canvas drawing and cooperative cancellation.",
            ReadingTime = "4 min read",
            Summary = "Display.Animate provides smooth immediate-mode rendering without holding execution locks. Display.ThrowIfCancellationRequested ensures loops stop cleanly.",
            Keywords = new List<string> { "animate", "fps", "drawingcontext", "cancellation", "token", "loop" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Live 60 FPS Drawing (Display.Animate)",
                    Content = "Display.Animate((drawingContext, elapsed) => { ... }) invokes your callback on every compositor frame. The script finishes immediately while the canvas continues animating independently.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Prefer Display.Animate over a while(true) loop — it doesn't hold any thread locks or freeze the host."
                },
                new()
                {
                    Heading = "Cooperative Cancellation",
                    Content = "For finite rendering loops or long batch algorithms, call Display.ThrowIfCancellationRequested() inside your loop so the Stop button (Shift+F5) and timeout can interrupt execution."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_live_animation",
                    Title = "Live Rotating Pulsing Radar",
                    Description = "Smooth 60 FPS immediate-mode animation using Display.Animate.",
                    Language = "csharp",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = @"Display.Animate((ctx, elapsed) =>
{
    var center = new Avalonia.Point(200, 150);
    double seconds = elapsed.TotalSeconds;
    double radius = 60 + Math.Sin(seconds * 3) * 15;

    // Draw pulsating ring
    var pen = new Avalonia.Media.Pen(Avalonia.Media.Brushes.DeepSkyBlue, 2.5);
    ctx.DrawEllipse(null, pen, center, radius, radius);

    // Draw sweep line
    double angle = seconds * 2;
    var target = center + new Avalonia.Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    ctx.DrawLine(new Avalonia.Media.Pen(Avalonia.Media.Brushes.Aquamarine, 2), center, target);
}, width: 400, height: 300);"
                }
            }
        };
    }

    private DocArticle CreateShortcutsReferenceArticle()
    {
        return new DocArticle
        {
            Id = "shortcuts_reference",
            Title = "VS Code Shortcuts Cheat Sheet",
            Subtitle = "Complete cross-platform keybindings reference for C# Code Studio.",
            ReadingTime = "2 min read",
            Summary = "Standard Visual Studio Code keyboard shortcuts across macOS and Windows/Linux.",
            Keywords = new List<string> { "shortcuts", "keybindings", "f5", "f10", "cmd+b", "ctrl+b", "ctrl+j", "mac", "windows" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Authentic VS Code Ergonomics",
                    Content = "All major shortcuts are wired into the studio view and can be triggered anywhere in the active window.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "On macOS, the Command key (⌘) is automatically honored wherever Ctrl is used on Windows."
                }
            },
            Shortcuts = new List<DocShortcutItem>
            {
                new() { Action = "Toggle Primary Side Bar", MacKey = "⌘ B", WinKey = "Ctrl+B", Description = "Hide or show Explorer, Search, or Debug sidebar.", Category = "Window" },
                new() { Action = "Toggle Bottom Tool Deck", MacKey = "⌘ J", WinKey = "Ctrl+J", Description = "Hide or show Problems, Terminal, REPL, and Results.", Category = "Window" },
                new() { Action = "Save Document", MacKey = "⌘ S", WinKey = "Ctrl+S", Description = "Save active script or notebook to disk.", Category = "File" },
                new() { Action = "Start Debugging / Continue", MacKey = "F5", WinKey = "F5", Description = "Compile, launch debugger, or continue past breakpoint.", Category = "Debug" },
                new() { Action = "Run without Debugging", MacKey = "Ctrl+F5", WinKey = "Ctrl+F5", Description = "Fast execution without debugger hooks.", Category = "Debug" },
                new() { Action = "Stop Execution / Debug", MacKey = "⇧ F5", WinKey = "Shift+F5", Description = "Cancel running script or terminate debug session.", Category = "Debug" },
                new() { Action = "Step Over", MacKey = "F10", WinKey = "F10", Description = "Execute next statement without stepping into methods.", Category = "Debug" },
                new() { Action = "Step Into", MacKey = "F11", WinKey = "F11", Description = "Step into method under cursor.", Category = "Debug" },
                new() { Action = "Toggle Breakpoint", MacKey = "F9", WinKey = "F9", Description = "Toggle breakpoint on current cursor line.", Category = "Debug" },
                new() { Action = "Format Document", MacKey = "⇧ ⌥ F", WinKey = "Ctrl+K Ctrl+D", Description = "Format C# code using Roslyn formatter.", Category = "Editor" },
                new() { Action = "Show Hover (Quick Info)", MacKey = "⌘ K ⌘ I", WinKey = "Ctrl+K Ctrl+I", Description = "Show the signature and documentation of the symbol at the cursor; resting the mouse on a symbol shows the same card.", Category = "Editor" },
                new() { Action = "Find & Replace", MacKey = "⌘ F", WinKey = "Ctrl+F", Description = "Open editor find and replace overlay.", Category = "Editor" },
                new() { Action = "Run Notebook Cell", MacKey = "⇧ Enter", WinKey = "Shift+Enter", Description = "Execute active cell and advance to next cell.", Category = "Notebook" },
                new() { Action = "Focus Explorer", MacKey = "⇧ ⌘ E", WinKey = "Ctrl+Shift+E", Description = "Open SideBar and focus workspace Explorer.", Category = "Navigation" },
                new() { Action = "Focus Search", MacKey = "⇧ ⌘ F", WinKey = "Ctrl+Shift+F", Description = "Open SideBar and focus workspace text Search.", Category = "Navigation" },
                new() { Action = "Focus Run & Debug", MacKey = "⇧ ⌘ D", WinKey = "Ctrl+Shift+D", Description = "Open SideBar and focus Run & Debug view.", Category = "Navigation" },
                new() { Action = "Focus Problems", MacKey = "⇧ ⌘ M", WinKey = "Ctrl+Shift+M", Description = "Open Bottom Deck and focus Problems tab.", Category = "Navigation" }
            }
        };
    }
}
