using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models.AI;
using PdfEditorApp.Plugins.CSharpEditor.Services.AI.Tools;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class AiAgentToolTests : IDisposable
{
    private readonly string _testDir;

    public AiAgentToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FryAiToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public void BuildToolList_ReturnsAllTools()
    {
        var registry = new AiAgentToolRegistry();
        var tools = registry.BuildToolList();

        Assert.NotNull(tools);
        Assert.Equal(22, tools.Count);
        Assert.Contains(tools, t => t.Name == "read_file");
        Assert.Contains(tools, t => t.Name == "write_file");
        Assert.Contains(tools, t => t.Name == "modify_file");
        Assert.Contains(tools, t => t.Name == "run_command");
        Assert.Contains(tools, t => t.Name == "glob_files");
        Assert.Contains(tools, t => t.Name == "grep_search");
        Assert.Contains(tools, t => t.Name == "read_project_rules");
        Assert.Contains(tools, t => t.Name == "git_status");
        Assert.Contains(tools, t => t.Name == "git_diff");
        Assert.Contains(tools, t => t.Name == "compile_and_get_diagnostics");
        Assert.Contains(tools, t => t.Name == "apply_studio_customization");
        Assert.Contains(tools, t => t.Name == "get_active_file_context");
        Assert.Contains(tools, t => t.Name == "get_studio_api_metadata");
        Assert.Contains(tools, t => t.Name == "get_runtime_extension_points");
        Assert.Contains(tools, t => t.Name == "inspect_studio_ui");
        Assert.Contains(tools, t => t.Name == "inject_studio_widget");
    }

    [Fact]
    public void WriteFile_And_ReadFile_WorkCorrectly()
    {
        ModifiedFileItem? capturedItem = null;
        var registry = new AiAgentToolRegistry(onFileModified: item => capturedItem = item);

        var filePath = Path.Combine(_testDir, "TestScript.cs");
        var content = "line 1\nline 2\nline 3\nline 4\nline 5";

        var writeResult = registry.WriteFile(filePath, content);
        Assert.Contains("written successfully", writeResult);
        Assert.NotNull(capturedItem);
        Assert.Equal(5, capturedItem.LinesAdded);

        var readResult = registry.ReadFile(filePath, startLine: 2, endLine: 4);
        Assert.Contains("2: line 2", readResult);
        Assert.Contains("3: line 3", readResult);
        Assert.Contains("4: line 4", readResult);
        Assert.DoesNotContain("1: line 1", readResult);
    }

    [Fact]
    public void ModifyFile_ReplacesSnippetAndCalculatesMetrics()
    {
        ModifiedFileItem? capturedItem = null;
        var registry = new AiAgentToolRegistry(onFileModified: item => capturedItem = item);

        var filePath = Path.Combine(_testDir, "Program.cs");
        var initial = "int a = 1;\nint b = 2;\nConsole.WriteLine(a + b);";
        File.WriteAllText(filePath, initial);

        var result = registry.ModifyFile(filePath, "int b = 2;", "int b = 42;");

        Assert.Contains("Successfully modified", result);
        Assert.NotNull(capturedItem);
        Assert.Equal("int a = 1;\nint b = 42;\nConsole.WriteLine(a + b);", File.ReadAllText(filePath));
        Assert.Equal(1, capturedItem.LinesAdded);
        Assert.Equal(1, capturedItem.LinesDeleted);
    }

    [Fact]
    public void CompileAndGetDiagnostics_DetectsErrorsAndSuccess()
    {
        var compiler = new RoslynCompilerService();
        var registry = new AiAgentToolRegistry(compilerService: compiler);

        var cleanCode = "int x = 10; Console.WriteLine(x);";
        var cleanResult = registry.CompileAndGetDiagnostics(cleanCode);
        Assert.Contains("Compilation succeeded with 0 errors", cleanResult);

        var brokenCode = "int x = ; Console.WriteLine(x);";
        var brokenResult = registry.CompileAndGetDiagnostics(brokenCode);
        Assert.Contains("Compilation failed", brokenResult);
    }

    [Fact]
    public void QueryStudioApiDocs_ReturnsMatchingDocumentation()
    {
        var registry = new AiAgentToolRegistry();
        var result = registry.QueryStudioApiDocs("scripting");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GlobFiles_FindsMatchingFiles()
    {
        var subDir = Path.Combine(_testDir, "SubDir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_testDir, "Root.cs"), "// Root");
        File.WriteAllText(Path.Combine(subDir, "Child.cs"), "// Child");
        File.WriteAllText(Path.Combine(subDir, "Ignore.txt"), "// Ignore");

        var registry = new AiAgentToolRegistry();
        var result = registry.GlobFiles("**/*.cs", directory: _testDir);

        Assert.Contains("Root.cs", result);
        Assert.Contains("Child.cs", result);
        Assert.DoesNotContain("Ignore.txt", result);
    }

    [Fact]
    public void GrepSearch_FindsMatchesWithLineNumbers()
    {
        var filePath = Path.Combine(_testDir, "GrepTarget.cs");
        File.WriteAllLines(filePath, new[]
        {
            "namespace Test;",
            "public class GrepTarget",
            "{",
            "    public void UniqueTargetMethod() => Console.WriteLine(123);",
            "}"
        });

        var registry = new AiAgentToolRegistry();
        var result = registry.GrepSearch("UniqueTargetMethod", path: _testDir);

        Assert.Contains("GrepTarget.cs:4:", result);
        Assert.Contains("UniqueTargetMethod", result);
    }

    [Fact]
    public void ReadProjectRules_ReadsAgentsOrGeminiMd()
    {
        var agentsMdPath = Path.Combine(_testDir, "AGENTS.md");
        File.WriteAllText(agentsMdPath, "# Mandate\nAlways use raw string literals.");

        var registry = new AiAgentToolRegistry();
        var rules = registry.ReadProjectRules(directory: _testDir);

        Assert.Contains("AGENTS.md", rules);
        Assert.Contains("Always use raw string literals", rules);
    }

    [Fact]
    public void ModifyFile_AmbiguousTargetSnippetFailsGracefully()
    {
        var filePath = Path.Combine(_testDir, "Ambiguous.cs");
        File.WriteAllText(filePath, "int x = 1;\nint x = 1;\nint x = 1;");

        var registry = new AiAgentToolRegistry();
        var result = registry.ModifyFile(filePath, "int x = 1;", "int x = 2;");

        Assert.Contains("appears multiple times", result);
        Assert.Contains("Provide more surrounding lines of context", result);
    }

    [Fact]
    public async Task RunCommand_ExecutesAndCapturesOutput()
    {
        var registry = new AiAgentToolRegistry();
        var cmd = OperatingSystem.IsWindows() ? "echo HelloFryAi" : "echo 'HelloFryAi'";

        var result = await registry.RunCommand(cmd, directory: _testDir);

        Assert.Contains("Exit Code: 0", result);
        Assert.Contains("HelloFryAi", result);
    }
}
