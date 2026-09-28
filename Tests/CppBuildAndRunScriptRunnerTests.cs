using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CppBuildAndRunScriptRunnerTests
{
    [Fact]
    public async Task PlanAsync_CreatesTwoPhaseExecutionPlan_CompileStepAndRunStep()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        const string clangPath = "/opt/homebrew/opt/llvm/bin/clang++";
        host.AddCpp(clangPath, "17.0.6", "clang");

        var runner = new CppBuildAndRunScriptRunner(host);
        var toolchain = new ToolchainInfo
        {
            LanguageId = LanguageIds.Cpp,
            ExecutablePath = clangPath,
            Version = new Version(17, 0, 6),
            DisplayName = "Clang 17",
            Source = "Test"
        };

        var context = new ScriptRunContext("/work/main.cpp", "/work", toolchain);
        var plan = await runner.PlanAsync(context);

        Assert.Equal(2, plan.Steps.Count);

        var buildStep = plan.Steps[0];
        Assert.Equal("Compile", buildStep.Label);
        Assert.True(buildStep.IsBuildStep);
        Assert.Equal(clangPath, buildStep.Spec.FileName);
        Assert.Contains("-std=c++20", buildStep.Spec.Arguments);
        Assert.Contains("-O2", buildStep.Spec.Arguments);
        Assert.Contains("-Wall", buildStep.Spec.Arguments);
        Assert.Contains("-o", buildStep.Spec.Arguments);
        Assert.Contains("/work/main.cpp", buildStep.Spec.Arguments);

        var runStep = plan.Steps[1];
        Assert.Equal("Run", runStep.Label);
        Assert.False(runStep.IsBuildStep);
        Assert.EndsWith("main", runStep.Spec.FileName);
    }

    [Fact]
    public async Task PlanAsync_ForMsvc_GeneratesMsvcCompilerFlags()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        const string clPath = @"C:\VS\VC\Tools\MSVC\bin\Hostx64\x64\cl.exe";
        host.AddCpp(clPath, "19.38.33134", "msvc");

        var runner = new CppBuildAndRunScriptRunner(host);
        var toolchain = new ToolchainInfo
        {
            LanguageId = LanguageIds.Cpp,
            ExecutablePath = clPath,
            Version = new Version(19, 38, 33134),
            DisplayName = "MSVC 19.38",
            Source = "Test"
        };

        var context = new ScriptRunContext(@"C:\work\solve.cpp", @"C:\work", toolchain);
        var plan = await runner.PlanAsync(context);

        Assert.Equal(2, plan.Steps.Count);

        var buildStep = plan.Steps[0];
        Assert.Equal("Compile", buildStep.Label);
        Assert.True(buildStep.IsBuildStep);
        Assert.Equal(clPath, buildStep.Spec.FileName);
        Assert.Contains("/std:c++20", buildStep.Spec.Arguments);
        Assert.Contains("/EHsc", buildStep.Spec.Arguments);
        Assert.Contains(buildStep.Spec.Arguments, a => a.StartsWith("/Fe:"));

        var runStep = plan.Steps[1];
        Assert.False(runStep.IsBuildStep);
        Assert.EndsWith("solve.exe", runStep.Spec.FileName);
    }
}
