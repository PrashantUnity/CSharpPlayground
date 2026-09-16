using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static class CodeTemplateLibrary
{
    public static IReadOnlyList<CodeTemplate> GetTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "leetcode_two_sum",
            Title = "1. Two Sum (Algorithm Workspace)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Script,
            Description = "Classic Two-Sum problem workspace with test cases, scratchpad notes, and Roslyn execution.",
            IconKind = MaterialIconKind.CodeBraces,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "LeetCode • Easy",
            Tags = new List<string> { "Algorithms", "Two-Sum", "Roslyn" },
            Notes = @"# 1. Two Sum

**Difficulty**: Easy | **Tags**: Array, Hash Table

Given an array of integers `nums` and an integer `target`, return indices of the two numbers such that they add up to `target`.

You may assume that each input would have **exactly one solution**, and you may not use the same element twice. You can return the answer in any order.

### Example 1:
- **Input**: `nums = [2,7,11,15], target = 9`
- **Output**: `[0,1]`
- **Explanation**: Because `nums[0] + nums[1] == 9`, we return `[0, 1]`.

### Example 2:
- **Input**: `nums = [3,2,4], target = 6`
- **Output**: `[1,2]`

### Constraints:
- `2 <= nums.Length <= 10^4`
- `-10^9 <= nums[i] <= 10^9`
- Only one valid answer exists.",
            InitialCode = @"using System;
using System.Collections.Generic;

public class Solution 
{
    public int[] TwoSum(int[] nums, int target) 
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < nums.Length; i++) 
        {
            int complement = target - nums[i];
            if (map.TryGetValue(complement, out int index)) 
            {
                return new int[] { index, i };
            }
            map[nums[i]] = i;
        }
        return Array.Empty<int>();
    }
}

// Execute test cases
var sol = new Solution();

int[] test1 = sol.TwoSum(new int[] { 2, 7, 11, 15 }, 9);
test1.Dump(""Test Case 1 (Target = 9)"");

int[] test2 = sol.TwoSum(new int[] { 3, 2, 4 }, 6);
test2.Dump(""Test Case 2 (Target = 6)"");

Console.WriteLine(""All test cases evaluated successfully."");",
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Case 1", Input = "nums = [2,7,11,15], target = 9", ExpectedOutput = "[0, 1]" },
                new() { Name = "Case 2", Input = "nums = [3,2,4], target = 6", ExpectedOutput = "[1, 2]" },
                new() { Name = "Case 3", Input = "nums = [3,3], target = 6", ExpectedOutput = "[0, 1]" }
            }
        },
        new()
        {
            Id = "csharp_scratchpad",
            Title = "C# Interactive Scratchpad",
            Category = "Scratchpad",
            Kind = WorkspaceItemKind.Script,
            Description = "Fast expression & statements scratchpad with instant .Dump() inspection and zero boilerplate.",
            IconKind = MaterialIconKind.LightningBoltOutline,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "Scratchpad • Instant",
            Tags = new List<string> { "LINQ", ".Dump()", "Top-level" },
            Notes = @"# Interactive C# Scratchpad

Write instant C# statements without `class Program` or `Main()`.
Call `.Dump()` on any object, collection, or calculation to format output immediately.",
            InitialCode = @"// C# Top-Level Statements with Interactive .Dump()
using System;
using System.Linq;

// Inspect runtime environment and application metadata
var environment = new {
    Application = ""FryPDF Document Studio"",
    Version = ""2026.1"",
    Runtime = "".NET 10 (C# 13)"",
    Compiler = ""Microsoft.CodeAnalysis.CSharp (Roslyn)"",
    Modules = new[] { ""PDF Rendering"", ""Document Automation"", ""C# Studio"", ""Vector Canvas"" }
};

environment.Dump(""Studio Environment Metadata"");

// LINQ sequence projections and calculations
var calculations = Enumerable.Range(1, 8)
    .Select(n => new { Number = n, Square = n * n, Cube = n * n * n })
    .ToList();

calculations.Dump(""Calculated Power Sequences"");

Console.WriteLine(""Script evaluation completed successfully."");"
        },
        new()
        {
            Id = "polyglot_notebook",
            Title = "Polyglot Notebook Demo",
            Category = "Notebook",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Multi-cell notebook workflow with Markdown explanations and interactive C# cells.",
            IconKind = MaterialIconKind.NotebookOutline,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "Notebook • Interactive",
            Tags = new List<string> { "Markdown", "Multi-cell", "Interactive" },
            InitialCode = @"// [Code Cell 1]
string docTitle = ""Q3 Financial Overview & Audit.pdf"";
int totalPages = 28;
Console.WriteLine($""Pipeline active for '{docTitle}' ({totalPages} pages)"");
new { Title = docTitle, Pages = totalPages, Status = ""Ready"" }.Dump();"
        },
        new()
        {
            Id = "skiasharp_image_studio",
            Title = "SkiaSharp Graphics & Image Generation",
            Category = "Graphics",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Generate 2D vector graphics, charts, and visual renderings using SkiaSharp (#r) and Display.Image.",
            IconKind = MaterialIconKind.ImageOutline,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "SkiaSharp • Visual",
            Tags = new List<string> { "SkiaSharp", "Images", "#r nuget", "Graphics" },
            InitialCode = @"#r ""nuget: SkiaSharp, 3.119.4""
using SkiaSharp;

var info = new SKImageInfo(480, 240);
var surface = SKSurface.Create(info);
var canvas = surface.Canvas;

// Draw gradient background
var bgPaint = new SKPaint
{
    Shader = SKShader.CreateLinearGradient(
        new SKPoint(0, 0),
        new SKPoint(480, 240),
        new[] { new SKColor(20, 26, 38), new SKColor(32, 45, 72) },
        SKShaderTileMode.Clamp)
};
canvas.DrawRect(0, 0, 480, 240, bgPaint);

// Draw stylized circular emblem
var circlePaint = new SKPaint { Color = new SKColor(102, 157, 246), IsAntialias = true };
canvas.DrawCircle(100, 120, 50, circlePaint);

var innerCircle = new SKPaint { Color = new SKColor(168, 199, 250), IsAntialias = true };
canvas.DrawCircle(100, 120, 28, innerCircle);

// Draw bar accents
var barPaint = new SKPaint { Color = new SKColor(234, 134, 143), IsAntialias = true };
for (int i = 0; i < 5; i++)
{
    canvas.DrawRoundRect(200 + i * 45, 170 - (i * 22), 30, (i + 1) * 22, 6, 6, barPaint);
}

// Display the rendered image directly in the cell output!
Display.Image(surface.Snapshot());
Console.WriteLine(""Rendered SkiaSharp graphics successfully."");"
        },
        new()
        {
            Id = "animation_studio",
            Title = "Live Animation Studio",
            Category = "Animation",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Live, self-animating visuals via Display.Animate, plus the cancellable frame-loop pattern for short bounded sequences.",
            IconKind = MaterialIconKind.MotionOutline,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "Animation • Live",
            Tags = new List<string> { "Animation", "Display.Animate", "DrawingContext" },
            InitialCode = @"using System;
using Avalonia;
using Avalonia.Media;

double canvasWidth = 380, canvasHeight = 220;
double ballRadius = 16;
var ballBrush = new SolidColorBrush(Color.Parse(""#F472B6""));
var trackBrush = new SolidColorBrush(Color.Parse(""#151B2B""));

Display.Animate((ctx, elapsed) =>
{
    ctx.FillRectangle(trackBrush, new Rect(0, 0, canvasWidth, canvasHeight));

    double t = elapsed.TotalSeconds;
    double x = ballRadius + (canvasWidth - 2 * ballRadius) * (0.5 + 0.5 * Math.Sin(t * 1.7));
    double y = ballRadius + (canvasHeight - 2 * ballRadius) * Math.Abs(Math.Sin(t * 2.3));

    ctx.DrawEllipse(ballBrush, null, new Point(x, y), ballRadius, ballRadius);
}, width: canvasWidth, height: canvasHeight);

Console.WriteLine(""Animating at ~60fps. Try opening another tab or running another cell - this never blocks."");"
        },
        new()
        {
            Id = "rich_html_reports",
            Title = "Rich Text & Markdown Reports",
            Category = "Reporting",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Format notebook output as headings, bold/italic text, links, and lists via Display.Markdown and Display.Html.",
            IconKind = MaterialIconKind.LanguageMarkdownOutline,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "Reporting • Markdown",
            Tags = new List<string> { "Markdown", "Html", "Reports" },
            InitialCode = @"Display.Markdown(@""# Quarterly Report
## Revenue Summary

**Total revenue** increased by *18%* this quarter, driven by the new automation pipeline.

Key figures were pulled via `PdfDocument.GetMetadata()`.

See the [full methodology](https://example.com/methodology) for details."");

Console.WriteLine(""Rendered via Display.Markdown -> Display.Html -> RichHtmlView."");"
        },
        new()
        {
            Id = "nuget_charting_scottplot",
            Title = "Charting with ScottPlot (NuGet)",
            Category = "Charting",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Resolve ScottPlot.Avalonia via #r nuget and display a live, interactive chart control with Display.Control.",
            IconKind = MaterialIconKind.ChartLine,
            AccentColor = "#9BA1AD",
            AccentBackground = "#252C36",
            AccentBorder = "#3D4450",
            CategoryBadge = "ScottPlot • #r nuget",
            Tags = new List<string> { "ScottPlot", "#r nuget", "Charts" },
            InitialCode = @"#r ""nuget: ScottPlot.Avalonia, 5.1.59""
using ScottPlot.Avalonia;

var avaPlot = new AvaPlot { Width = 520, Height = 300 };
var plot = avaPlot.Plot;

double[] months = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
double[] revenue = { 12, 15, 14, 18, 22, 26, 24, 28, 31, 29, 34, 38 };

var scatter = plot.Add.Scatter(months, revenue);
scatter.LineWidth = 2;
scatter.MarkerSize = 6;

plot.Title(""Monthly Revenue (Thousands, USD)"");
plot.XLabel(""Month"");
plot.YLabel(""Revenue ($K)"");

Display.Control(avaPlot);
Console.WriteLine(""Live, interactive ScottPlot chart rendered via #r nuget + Display.Control."");"
        }
    };
}
