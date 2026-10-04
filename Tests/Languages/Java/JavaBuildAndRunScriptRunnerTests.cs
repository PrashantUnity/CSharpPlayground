using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class JavaBuildAndRunScriptRunnerTests
{
    [Fact]
    public async Task PlanAsync_CreatesTwoPhaseExecutionPlan_BuildStepAndRunStep()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        const string javaPath = "/opt/homebrew/opt/openjdk/bin/java";
        host.AddJava(javaPath, "17.0.20");

        var runner = new JavaBuildAndRunScriptRunner(host);
        var toolchain = new ToolchainInfo
        {
            LanguageId = LanguageIds.Java,
            ExecutablePath = javaPath,
            Version = new Version(17, 0, 20),
            DisplayName = "Java 17",
            Source = "Test"
        };

        var context = new ScriptRunContext("/work/Main.java", "/work", toolchain);
        var plan = await runner.PlanAsync(context);

        Assert.Equal(2, plan.Steps.Count);

        var buildStep = plan.Steps[0];
        Assert.Equal("Compile", buildStep.Label);
        Assert.True(buildStep.IsBuildStep);
        Assert.EndsWith("javac", buildStep.Spec.FileName);
        Assert.Contains("-d", buildStep.Spec.Arguments);
        Assert.Contains("/work/Main.java", buildStep.Spec.Arguments);

        var runStep = plan.Steps[1];
        Assert.Equal("Run", runStep.Label);
        Assert.False(runStep.IsBuildStep);
        Assert.Equal(javaPath, runStep.Spec.FileName);
        Assert.Contains("-cp", runStep.Spec.Arguments);
        Assert.Contains("Main", runStep.Spec.Arguments);
    }

    [Fact]
    public void ParsePackage_ExtractsPackageDeclaration()
    {
        const string code = """
            // Some comment
            package com.example.demo;

            public class App {
            }
            """;

        var pkg = JavaBuildAndRunScriptRunner.ParsePackage(code);
        Assert.Equal("com.example.demo", pkg);
    }

    [Fact]
    public void ParsePackage_ReturnsNull_WhenDefaultPackage()
    {
        const string code = """
            public class App {
                public static void main(String[] args) {}
            }
            """;

        var pkg = JavaBuildAndRunScriptRunner.ParsePackage(code);
        Assert.Null(pkg);
    }
}
