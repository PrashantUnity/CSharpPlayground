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
            AccentColor = "#A8C7FA",
            AccentBackground = "#1E2536",
            AccentBorder = "#2D374D",
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

// 🧪 Test Execution
var sol = new Solution();

int[] test1 = sol.TwoSum(new int[] { 2, 7, 11, 15 }, 9);
test1.Dump(""Case 1 Result (Expect [0, 1])"");

int[] test2 = sol.TwoSum(new int[] { 3, 2, 4 }, 6);
test2.Dump(""Case 2 Result (Expect [1, 2])"");

Console.WriteLine(""\n✅ Algorithm executed successfully!"");",
            TestCases = new List<TestCaseItem>
            {
                new() { Name = "Case 1", Input = "nums = [2,7,11,15], target = 9", ExpectedOutput = "[0, 1]" },
                new() { Name = "Case 2", Input = "nums = [3,2,4], target = 6", ExpectedOutput = "[1, 2]" },
                new() { Name = "Case 3", Input = "nums = [3,3], target = 6", ExpectedOutput = "[0, 1]" }
            }
        },
        new()
        {
            Id = "linqpad_scratchpad",
            Title = "LINQPad Scratchpad (.Dump)",
            Category = "LINQPad",
            Kind = WorkspaceItemKind.Script,
            Description = "Fast expression & statements scratchpad with instant .Dump() inspection and zero boilerplate.",
            IconKind = MaterialIconKind.LightningBoltOutline,
            AccentColor = "#A8C7FA",
            AccentBackground = "#1E2536",
            AccentBorder = "#2D374D",
            CategoryBadge = "Scratchpad • Instant",
            Tags = new List<string> { "LINQ", ".Dump()", "Top-level" },
            Notes = @"# LINQPad C# Scratchpad

Write instant C# statements without `class Program` or `Main()`.
Call `.Dump()` on any object, collection, or calculation to format output immediately.",
            InitialCode = @"// ⚡ LINQPad-style instant C# statements with .Dump()!
using System;
using System.Linq;

var studioInfo = new {
    Title = ""FryPDF Document Studio"",
    Version = ""2026.1"",
    Engine = "".NET 10 (C# 13)"",
    Features = new[] { ""PDF Rendering"", ""Page Manipulation"", ""C# Scripting"", ""M3 Themes"" }
};

studioInfo.Dump(""Document Studio Status"");

// LINQ transformation on numbers:
var powers = Enumerable.Range(1, 8)
    .Select(n => new { Number = n, Square = n * n, Cube = n * n * n })
    .ToList();

powers.Dump(""Calculated Powers"");

Console.WriteLine(""\n✨ Try typing any statement or expression!"");"
        },
        new()
        {
            Id = "pdf_automation",
            Title = "PDF Document Automation",
            Category = "Automation",
            Kind = WorkspaceItemKind.Script,
            Description = "Simulated document automation, file metadata inspection, and batch pipeline.",
            IconKind = MaterialIconKind.FilePdfBox,
            AccentColor = "#A8C7FA",
            AccentBackground = "#1E2536",
            AccentBorder = "#2D374D",
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
        Console.WriteLine(""📄 FryPDF Document Automation Script"");
        Console.WriteLine(""--------------------------------------"");

        var metadata = new
        {
            Title = ""Annual Performance Review 2026"",
            Author = ""FryPDF Document Studio"",
            PageCount = 14,
            Encrypted = false,
            ProcessedAt = DateTime.Now
        };

        metadata.Dump(""PDF Metadata Profile"");
        Console.WriteLine(""✅ Document automation batch completed."");
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
            AccentColor = "#A8C7FA",
            AccentBackground = "#1E2536",
            AccentBorder = "#2D374D",
            CategoryBadge = "Notebook • Interactive",
            Tags = new List<string> { "Markdown", "Multi-cell", "Interactive" },
            InitialCode = @"// [Code Cell 1]
string docTitle = ""Q3 Financial Overview & Audit.pdf"";
int totalPages = 28;
Console.WriteLine($""Pipeline active for '{docTitle}' ({totalPages} pages)"");
new { Title = docTitle, Pages = totalPages, Status = ""Ready"" }.Dump();"
        }
    };
}
