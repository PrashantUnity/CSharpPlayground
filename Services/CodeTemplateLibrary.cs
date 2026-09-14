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
            AccentColor = "#34D399",
            AccentBackground = "#064E3B",
            AccentBorder = "#059669",
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
            AccentColor = "#FBBF24",
            AccentBackground = "#451A03",
            AccentBorder = "#D97706",
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
            Id = "pdf_automation",
            Title = "PDF Document Automation",
            Category = "Automation",
            Kind = WorkspaceItemKind.Script,
            Description = "Simulated document automation, file metadata inspection, and batch pipeline.",
            IconKind = MaterialIconKind.FilePdfBox,
            AccentColor = "#F87171",
            AccentBackground = "#4C0519",
            AccentBorder = "#E11D48",
            CategoryBadge = "Studio API • Batch",
            Tags = new List<string> { "Automation", "Metadata", "Pipeline" },
            Notes = @"# PDF Document Automation Script
Inspect metadata, calculate page budgets, and generate simulation logs.",
            InitialCode = @"using System;
using System.IO;

public static class Program
{
    public static void Main()
    {
        Console.WriteLine(""FryPDF Document Automation Pipeline"");
        Console.WriteLine(""------------------------------------"");

        var metadata = new
        {
            Title = ""Annual Performance Review 2026"",
            Author = ""FryPDF Document Studio"",
            PageCount = 14,
            Encrypted = false,
            ProcessedAt = DateTime.Now
        };

        metadata.Dump(""PDF Metadata Profile"");
        Console.WriteLine(""Document automation pipeline completed successfully."");
    }
}"
        },
        new()
        {
            Id = "polyglot_notebook",
            Title = "Polyglot Notebook Demo",
            Category = "Notebook",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Multi-cell notebook workflow with Markdown explanations and interactive C# cells.",
            IconKind = MaterialIconKind.NotebookOutline,
            AccentColor = "#C084FC",
            AccentBackground = "#261447",
            AccentBorder = "#6B21A8",
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
            AccentColor = "#38BDF8",
            AccentBackground = "#082F49",
            AccentBorder = "#0284C7",
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
        }
    };
}
