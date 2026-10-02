using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class GoBuildAndRunScriptRunnerTests
{
    [Fact]
    public async Task GeneratesTwoPhaseExecutionPlan()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var runner = new GoBuildAndRunScriptRunner(host);

        var toolchain = new ToolchainInfo
        {
            LanguageId = "go",
            ExecutablePath = "/opt/homebrew/bin/go",
            Version = new Version(1, 22, 4),
            DisplayName = "Go 1.22.4",
            Source = "Homebrew"
        };

        var context = new ScriptRunContext(
            SourceFilePath: "/Users/test/workspace/main.go",
            WorkingDirectory: "/Users/test/workspace",
            Toolchain: toolchain);

        var plan = await runner.PlanAsync(context);

        Assert.NotNull(plan);
        Assert.Equal(2, plan.Steps.Count);

        var buildStep = plan.Steps[0];
        Assert.True(buildStep.IsBuildStep);
        Assert.Equal("/opt/homebrew/bin/go", buildStep.Spec.FileName);
        Assert.Contains("build", buildStep.Spec.Arguments);
        Assert.Contains("-o", buildStep.Spec.Arguments);
        Assert.Contains("/Users/test/workspace/main.go", buildStep.Spec.Arguments);
        Assert.True(buildStep.Spec.Environment.ContainsKey("GOCACHE"));

        var runStep = plan.Steps[1];
        Assert.False(runStep.IsBuildStep);
        Assert.False(string.IsNullOrWhiteSpace(runStep.Spec.FileName));
        Assert.Equal("/Users/test/workspace", runStep.Spec.WorkingDirectory);
    }

    [Fact]
    public async Task OnWindows_GeneratesExeExtension()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        var runner = new GoBuildAndRunScriptRunner(host);

        var toolchain = new ToolchainInfo
        {
            LanguageId = "go",
            ExecutablePath = @"C:\Program Files\Go\bin\go.exe",
            Version = new Version(1, 22, 0),
            DisplayName = "Go 1.22.0",
            Source = "System"
        };

        var context = new ScriptRunContext(
            SourceFilePath: @"C:\Users\test\workspace\server.go",
            WorkingDirectory: @"C:\Users\test\workspace",
            Toolchain: toolchain);

        var plan = await runner.PlanAsync(context);

        var runStep = plan.Steps[1];
        Assert.EndsWith(".exe", runStep.Spec.FileName, StringComparison.OrdinalIgnoreCase);
    }
}
