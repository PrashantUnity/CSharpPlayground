using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private DocCategory BuildPolyglotVisualsCategory()
    {
        return new DocCategory
        {
            Id = "polyglot_visuals",
            Title = "Charts, 3D and visualizers in every language",
            IconKind = MaterialIconKind.ChartBoxOutline,
            AccentColor = "#38BDF8",
            Badge = "Polyglot Visuals",
            Description = "Unified interactive visual SDK for 2D charts, 3D plots, and data structure visualizers across C#, Python, JavaScript, Java, Go, Rust, C++, and F#.",
            Articles = new List<DocArticle>
            {
                CreatePolyglotOverviewArticle(),
                CreatePolyglotChartsArticle(),
                CreatePolyglot3DPlotsArticle(),
                CreatePolyglotVisualizersArticle(),
                CreatePolyglotInteractiveEventsArticle()
            }
        };
    }

    private DocArticle CreatePolyglotOverviewArticle()
    {
        return new DocArticle
        {
            Id = "polyglot_visuals_overview",
            Title = "Polyglot Visual Architecture",
            Subtitle = "One canonical API, MIME bundle protocol, and universal visual chrome across 8 languages.",
            ReadingTime = "4 min read",
            Summary = "C# Code Studio provides first-class visual output in every supported language through a unified runtime protocol. Calling Display.Chart, Display.Plot3D, or Display.Visualizer produces rich interactive controls with zoom, pan, CSV export, spec copying, and live two-way interaction.",
            Keywords = new List<string> { "polyglot", "visuals", "chart", "3d", "visualizer", "python", "javascript", "java", "go", "rust", "cpp", "fsharp", "dart" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Universal Visual Chrome",
                    Content = "Every visualizer, 2D chart, and 3D plot is rendered inside VisualChromeControl. It provides a standardized toolbar containing Fullscreen, Copy spec (a portable MIME bundle accepted by Display.show in any language), Copy data (CSV), Save PNG, Reset/Fit, plus kind-specific buttons.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Specs are immutable: inline and fullscreen views use decoupled ViewState records (ChartViewState, Plot3DViewState, VisualizerViewState) so views never fight over zoom, pan, or camera angles."
                },
                new()
                {
                    Heading = "Supported Language SDKs",
                    Content = "The unified visual runtime is built into all toolchains:\n• C#: Display.Chart(), Display.Plot3D(), Display.Visualizer()\n• Python: import fry_display as display\n• JavaScript: const { Display } = require('fry_display')\n• Java: import com.frypdf.display.Display\n• Go: import \"fry_display\"\n• Rust: use fry_display::prelude::*\n• C++: #include <fry_display.hpp>\n• F#: open FryDisplay\n• Dart: Display.barChart(), Display.plot3D(), Display.arrayVisualizer()"
                }
            }
        };
    }

    private DocArticle CreatePolyglotChartsArticle()
    {
        return new DocArticle
        {
            Id = "polyglot_charts",
            Title = "2D Charts Across All Languages",
            Subtitle = "Render line, area, bar, scatter, pie, and donut charts in any language.",
            ReadingTime = "6 min read",
            Summary = "Explore examples of creating responsive 2D charts with customized palettes, dual axes, tooltips, and click callbacks in Python, JavaScript, Java, Go, Rust, C++, F#, and C#.",
            Keywords = new List<string> { "charts", "line", "bar", "scatter", "polyglot", "python", "javascript" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Python Example",
                    Content = "Use fry_display to plot line or bar charts with automatic NaN/infinite filtering."
                },
                new()
                {
                    Heading = "JavaScript Example",
                    Content = "Create interactive charts with live click handling in Node.js scripts."
                },
                new()
                {
                    Heading = "Go & Rust Examples",
                    Content = "Native compiled languages generate visual MIME bundles written directly to stdout."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Language = "python",
                    Title = "Python Line Chart",
                    Code = "import math\nfrom fry_display import Display\n\nx = [i * 0.1 for i in range(50)]\ny = [math.sin(val) for val in x]\n\nDisplay.chart(x=x, y=y, title=\"Sine Wave\", chart_type=\"line\")"
                },
                new()
                {
                    Language = "javascript",
                    Title = "JavaScript Bar Chart",
                    Code = "const { Display } = require('fry_display');\n\nDisplay.chart({\n  type: 'bar',\n  title: 'Quarterly Revenue',\n  labels: ['Q1', 'Q2', 'Q3', 'Q4'],\n  series: [{ name: '2026', values: [120, 190, 240, 310] }]\n});"
                },
                new()
                {
                    Language = "rust",
                    Title = "Rust Scatter Chart",
                    Code = "use fry_display::prelude::*;\n\nfn main() {\n    let chart = Chart::new()\n        .title(\"Normal Distribution\")\n        .scatter_series(\"Samples\", vec![(1.0, 2.1), (2.0, 3.8), (3.0, 2.9)]);\n    Display::show(chart);\n}"
                }
            }
        };
    }

    private DocArticle CreatePolyglot3DPlotsArticle()
    {
        return new DocArticle
        {
            Id = "polyglot_3d_plots",
            Title = "Interactive 3D Visualizations",
            Subtitle = "Surfaces, wireframes, 3D scatter plots, and network graphs with orbit controls.",
            ReadingTime = "5 min read",
            Summary = "Render mathematical functions, voxel landscapes, and force-directed 3D networks. Supports turntable auto-rotation, isometric/orthographic projections, and viridis/plasma color maps.",
            Keywords = new List<string> { "plot3d", "surface", "wireframe", "graph3d", "scatter3d" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "C# 3D Surface Plot",
                    Content = "Plot 3D mathematical surfaces with one line of code using Display.Plot3D."
                },
                new()
                {
                    Heading = "Python 3D Plot",
                    Content = "Generate surfaces and scatter plots easily from Python lists or numpy arrays."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Language = "csharp",
                    Title = "C# Sombrero Surface",
                    Code = "Display.Plot3D(\n    (x, y) => Math.Sin(Math.Sqrt(x * x + y * y)) / (Math.Sqrt(x * x + y * y) + 0.001),\n    xRange: (-8, 8),\n    yRange: (-8, 8),\n    resolution: 40,\n    title: \"Sombrero Surface\");"
                },
                new()
                {
                    Language = "python",
                    Title = "Python 3D Surface",
                    Code = "from fry_display import Display\nimport math\n\ndef sinc(x, y):\n    r = math.sqrt(x*x + y*y) + 1e-6\n    return math.sin(r) / r\n\nDisplay.plot3d_surface(sinc, x_range=(-6, 6), y_range=(-6, 6), res=35, colormap=\"viridis\")"
                }
            }
        };
    }

    private DocArticle CreatePolyglotVisualizersArticle()
    {
        return new DocArticle
        {
            Id = "polyglot_visualizers",
            Title = "Algorithm & Data Structure Visualizers",
            Subtitle = "Interactive stepping timelines for arrays, trees, graphs, linked lists, and matrices.",
            ReadingTime = "6 min read",
            Summary = "Step-by-step visualizers allow stepping forward, backwards, zooming, and full-screen scrubbing. Visualizers automatically highlight the corresponding source code lines in the editor as they step.",
            Keywords = new List<string> { "visualizer", "algorithms", "array", "tree", "graph", "matrix", "playback" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Matrix & Island Exploration in Java",
                    Content = "Record matrix transformations or island searches step-by-step."
                },
                new()
                {
                    Heading = "C++ Binary Tree Visualizer",
                    Content = "Visualize tree traversals and dynamic node additions."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Language = "java",
                    Title = "Java Grid Visualizer",
                    Code = "import com.frypdf.display.Display;\nimport com.frypdf.display.Visualizer;\n\npublic class Solution {\n    public static void main(String[] args) {\n        int[][] grid = {\n            {1, 1, 0, 0},\n            {1, 1, 0, 1},\n            {0, 0, 1, 1}\n        };\n        Visualizer.grid(grid).title(\"Island Traversal\").show();\n    }\n}"
                },
                new()
                {
                    Language = "cpp",
                    Title = "C++ Tree Visualizer",
                    Code = "#include <fry_display.hpp>\n\nint main() {\n    fry::TreeVisualizer tree(\"Binary Search Tree\");\n    tree.insert(50);\n    tree.insert(30);\n    tree.insert(70);\n    tree.insert(20);\n    tree.show();\n    return 0;\n}"
                }
            }
        };
    }

    private DocArticle CreatePolyglotInteractiveEventsArticle()
    {
        return new DocArticle
        {
            Id = "polyglot_interactive_events",
            Title = "Live Two-Way Interaction & Updates",
            Subtitle = "Responding to clicks, throttling state updates, and disconnected visual states.",
            ReadingTime = "5 min read",
            Summary = "Visual handles returned by Display calls allow in-place incremental updates without re-rendering the cell. Clicking a chart element or visualizer node dispatches an event back to the running process.",
            Keywords = new List<string> { "events", "callback", "handle", "update", "interaction" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Live Handle Updates in Python",
                    Content = "Update existing visuals in-place without producing new output cells."
                },
                new()
                {
                    Heading = "Interactive Click Callbacks in C#",
                    Content = "Attach click callbacks that receive element metadata and modifier keys."
                }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Language = "python",
                    Title = "Python In-Place Update",
                    Code = "from fry_display import Display\nimport time\n\nhandle = Display.chart([1, 2, 3], title=\"Live Monitor\")\n\nfor i in range(4, 10):\n    time.sleep(0.5)\n    handle.update([1, 2, 3, i], title=f\"Live Monitor (Step {i})\")"
                },
                new()
                {
                    Language = "csharp",
                    Title = "C# Click Callback",
                    Code = "var chart = Display.Chart(new[] { 10, 25, 40, 15 }, title: \"Clickable Bar Chart\");\nchart.OnClick(e => {\n    Console.WriteLine($\"Clicked point index {e.Index} with value {e.Value}\");\n});"
                }
            }
        };
    }
}
