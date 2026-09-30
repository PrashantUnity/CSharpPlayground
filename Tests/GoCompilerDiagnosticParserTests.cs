using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoCompilerDiagnosticParserTests
{
    private readonly GoCompilerDiagnosticParser _parser = new();

    [Fact]
    public void ParsesCompilerError_WithLineAndColumn()
    {
        var output = "main.go:12:5: undefined: myVariable\n";
        var result = _parser.Parse(output, "main.go");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(12, diag.Line);
        Assert.Equal(5, diag.Column);
        Assert.Equal("undefined: myVariable", diag.Message);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    [Fact]
    public void ParsesCompilerError_LineOnly()
    {
        var output = "main.go:15: syntax error: unexpected semicolon, expecting comma or )\n";
        var result = _parser.Parse(output, "/path/to/main.go");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(15, diag.Line);
        Assert.Equal(1, diag.Column);
        Assert.Contains("unexpected semicolon", diag.Message);
    }

    [Fact]
    public void DetectsMissingPackage_AndExtractsMissingDependency()
    {
        var output = """
            main.go:4:2: no required module provides package github.com/google/uuid; to add it:
            	go get github.com/google/uuid
            """;

        var result = _parser.Parse(output, "main.go");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("github.com/google/uuid", result.MissingDependency);
    }

    [Fact]
    public void DetectsCannotFindPackage_AndExtractsMissingDependency()
    {
        var output = """
            main.go:5:2: cannot find package "github.com/gin-gonic/gin" in any of:
            	/usr/local/go/src/github.com/gin-gonic/gin (from $GOROOT)
            """;

        var result = _parser.Parse(output, "main.go");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("github.com/gin-gonic/gin", result.MissingDependency);
    }

    [Fact]
    public void ParsesRuntimePanic_WithStackTracePointingToSourceLine()
    {
        var output = """
            panic: runtime error: index out of range [5] with length 3

            goroutine 1 [running]:
            main.main()
            	/Users/fry/workspace/main.go:18 +0x48
            exit status 2
            """;

        var result = _parser.Parse(output, "/Users/fry/workspace/main.go");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(18, diag.Line);
        Assert.Contains("runtime error: index out of range [5] with length 3", diag.Message);
    }
}
