using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

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

    [Fact]
    public async Task PlanAsync_OnWindows_WhenMsvcInstalled_SuppliesMsvcIncludesToClang()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.Variables["ProgramFiles(x86)"] = @"C:\Program Files (x86)";

        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\include");
        host.AddDirectory(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\lib\x64");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\shared");
        host.AddDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um");

        const string clangPath = @"C:\Program Files\LLVM\bin\clang++.exe";
        host.AddCpp(clangPath, "18.1.8", "clang");

        var runner = new CppBuildAndRunScriptRunner(host);
        var toolchain = new ToolchainInfo
        {
            LanguageId = LanguageIds.Cpp,
            ExecutablePath = clangPath,
            Version = new Version(18, 1, 8),
            DisplayName = "Clang 18",
            Source = "LLVM"
        };

        var context = new ScriptRunContext(@"C:\work\main.cpp", @"C:\work", toolchain);
        var plan = await runner.PlanAsync(context);

        var buildStep = plan.Steps[0];
        Assert.Contains("-imsvc", buildStep.Spec.Arguments);
        Assert.Contains(buildStep.Spec.Arguments, a => a.Contains("MSVC") && a.EndsWith("include"));
        Assert.Contains(buildStep.Spec.Arguments, a => a.Contains("10.0.22621.0") && a.EndsWith("ucrt"));
        Assert.NotNull(buildStep.Spec.Environment);
        Assert.True(buildStep.Spec.Environment.ContainsKey("INCLUDE"));
        Assert.True(buildStep.Spec.Environment.ContainsKey("LIB"));
    }

    [Fact]
    public async Task PlanAsync_OnWindows_WhenMinGwInstalled_SuppliesMinGwTarget()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.AddDirectory(@"C:\msys64\ucrt64\include\c++");
        host.AddFile(@"C:\msys64\ucrt64\bin\g++.exe");

        const string clangPath = @"C:\Program Files\LLVM\bin\clang++.exe";
        host.AddCpp(clangPath, "18.1.8", "clang");

        var runner = new CppBuildAndRunScriptRunner(host);
        var toolchain = new ToolchainInfo
        {
            LanguageId = LanguageIds.Cpp,
            ExecutablePath = clangPath,
            Version = new Version(18, 1, 8),
            DisplayName = "Clang 18",
            Source = "LLVM"
        };

        var context = new ScriptRunContext(@"C:\work\main.cpp", @"C:\work", toolchain);
        var plan = await runner.PlanAsync(context);

        var buildStep = plan.Steps[0];
        Assert.Contains("--target=x86_64-w64-windows-gnu", buildStep.Spec.Arguments);
        Assert.Contains(@"--sysroot=C:\msys64\ucrt64", buildStep.Spec.Arguments);
        Assert.Contains(@"C:\msys64\ucrt64\bin", buildStep.Spec.Environment?["PATH"]);
    }
}
