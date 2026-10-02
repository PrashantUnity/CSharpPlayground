using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class FSharpCompilerDiagnosticParserTests
{
    private readonly FSharpCompilerDiagnosticParser _parser = new();

    [Fact]
    public void ParsesCompilerErrorWithLineAndColumn()
    {
        var output = """
            /Users/user/scripts/main.fsx(14,9): error FS0001: This expression was expected to have type 'int' but here has type 'string'
            """;

        var result = _parser.Parse(output, "/Users/user/scripts/main.fsx");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("FS0001", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(14, diag.Line);
        Assert.Equal(9, diag.Column);
        Assert.Contains("expected to have type 'int'", diag.Message);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void ParsesCompilerWarningWithRange()
    {
        var output = """
            /Users/user/scripts/main.fsx(22,5,22,12): warning FS0025: Incomplete pattern matches on this expression.
            """;

        var result = _parser.Parse(output, "/Users/user/scripts/main.fsx");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("FS0025", diag.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
        Assert.Equal(22, diag.Line);
        Assert.Equal(5, diag.Column);
    }

    [Fact]
    public void DetectsMissingModuleAsDependency()
    {
        var output = """
            /Users/user/scripts/main.fsx(3,6): error FS0039: The namespace or module 'Newtonsoft' is not defined.
            """;

        var result = _parser.Parse(output, "/Users/user/scripts/main.fsx");

        Assert.Equal("Newtonsoft", result.MissingDependency);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("FS0039", result.Diagnostics[0].Id);
    }

    [Fact]
    public void ParsesRuntimeStackTrace()
    {
        var output = """
            System.DivideByZeroException: Attempted to divide by zero.
               at <StartupCode$cell>.$FSI_0002.main@() in /tmp/FryStudio/fsharp_cells/abc/cell.fsx:line 8
            """;

        var result = _parser.Parse(output, "/tmp/FryStudio/fsharp_cells/abc/cell.fsx");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("FS-EXC", diag.Id);
        Assert.Equal(8, diag.Line);
        Assert.Contains("DivideByZeroException", diag.Message);
    }

    [Fact]
    public void EmptyOutput_ReturnsEmptyDiagnostics()
    {
        var result = _parser.Parse("", "/path/to/file.fsx");
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }
}
