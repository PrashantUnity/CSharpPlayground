using CSharpEditorPlugin.Tests.TestSupport;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class CSharpExternalRunnerTests
{
    private readonly CSharpCompilerDiagnosticParser _parser = new();

    [Fact]
    public void DiagnosticParser_EmptyOutput_ReturnsEmpty()
    {
        var result = _parser.Parse(string.Empty, "/work/Program.cs");
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void DiagnosticParser_ParsesDotNetBuildError_AndExtractsMissingDependency_CS0246()
    {
        const string output = """
            /work/Program.cs(12,5): error CS0246: The type or namespace name 'JObject' could not be found (are you missing a using directive or an assembly reference?) [/tmp/App.csproj]
            """;

        var result = _parser.Parse(output, "/work/Program.cs");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("CS0246", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(12, diag.Line);
        Assert.Equal(5, diag.Column);
        Assert.Contains("The type or namespace name 'JObject' could not be found", diag.Message);
        Assert.Equal("JObject", result.MissingDependency);
    }

    [Fact]
    public void DiagnosticParser_ParsesDotNetBuildWarning_CS0168()
    {
        const string output = """
            /work/Program.cs(5,13): warning CS0168: The variable 'ex' is declared but never used [/tmp/App.csproj]
            """;

        var result = _parser.Parse(output, "/work/Program.cs");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("CS0168", diag.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
        Assert.Equal(5, diag.Line);
        Assert.Equal(13, diag.Column);
    }

    [Fact]
    public void DiagnosticParser_ParsesRuntimeStackTrace_OnUncaughtException()
    {
        const string output = """
            Unhandled exception. System.InvalidOperationException: Operation failed due to invalid state.
               at Program.Main(String[] args) in /work/Program.cs:line 18
            """;

        var result = _parser.Parse(output, "/work/Program.cs");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("System.InvalidOperationException", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(18, diag.Line);
        Assert.Contains("Operation failed due to invalid state", diag.Message);
    }

    [Fact]
    public async Task BuildAndRunScriptRunner_PlansTwoPhaseExecution_WithNuGetBridging()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var code = """
                #r "nuget: Newtonsoft.Json, 13.0.3"
                using Newtonsoft.Json.Linq;
                var obj = new JObject();
                Console.WriteLine("Done");
                """;
            await File.WriteAllTextAsync(tempFile, code);

            var host = new FakeHostEnvironment(FakeOs.MacOS);
            var runner = new CSharpBuildAndRunScriptRunner(host);
            var toolchain = new ToolchainInfo
            {
                LanguageId = LanguageIds.CSharp,
                ExecutablePath = "/usr/local/share/dotnet/dotnet",
                Version = new Version(10, 0, 100),
                DisplayName = ".NET SDK 10.0",
                Source = "Test"
            };

            var context = new ScriptRunContext(tempFile, Path.GetDirectoryName(tempFile)!, toolchain);
            var plan = await runner.PlanAsync(context);

            Assert.Equal(2, plan.Steps.Count);

            var buildStep = plan.Steps[0];
            Assert.Equal("Build", buildStep.Label);
            Assert.True(buildStep.IsBuildStep);
            Assert.Equal(toolchain.ExecutablePath, buildStep.Spec.FileName);
            Assert.Contains("build", buildStep.Spec.Arguments);

            var runStep = plan.Steps[1];
            Assert.Equal("Run", runStep.Label);
            Assert.False(runStep.IsBuildStep);
            Assert.Equal(toolchain.ExecutablePath, runStep.Spec.FileName);
            Assert.Contains("run", runStep.Spec.Arguments);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
