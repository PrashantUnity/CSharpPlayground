using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class JavaScriptTracebackParserTests
{
    private readonly JavaScriptTracebackParser _parser = new();

    [Fact]
    public void EmptyOutput_ReturnsEmptyDiagnostics()
    {
        var result = _parser.Parse("", "/workspace/main.js");
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void SyntaxError_IsParsedWithLineAndColumn()
    {
        const string output = """
            /workspace/main.js:3
            const x = ;
                      ^

            SyntaxError: Unexpected token ';'
                at makeContextifyScript (node:internal/vm:194:14)
                at compileScript (node:internal/process/execution:420:10)
            """;

        var result = _parser.Parse(output, "/workspace/main.js");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("SyntaxError", diag.Id);
        Assert.Equal("Unexpected token ';'", diag.Message);
        Assert.Equal(3, diag.Line);
        Assert.Equal(11, diag.Column);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void RuntimeError_InUserScript_IsLocatedAtActiveFrame()
    {
        const string output = """
            /workspace/main.js:15
            throw new TypeError("Invalid parameter");
            ^

            TypeError: Invalid parameter
                at calculate (/workspace/main.js:15:7)
                at Object.<anonymous> (/workspace/main.js:20:1)
                at Module._compile (node:internal/modules/cjs/loader:1520:14)
            """;

        var result = _parser.Parse(output, "/workspace/main.js");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("TypeError", diag.Id);
        Assert.Equal("Invalid parameter", diag.Message);
        Assert.Equal(20, diag.Line);
        Assert.Equal(1, diag.Column);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void MissingModule_IsIdentifiedAsMissingDependency()
    {
        const string output = """
            node:internal/modules/cjs/loader:1522
              throw err;
              ^

            Error: Cannot find module 'lodash/debounce'
            Require stack:
            - /workspace/main.js
                at Module._resolveFilename (node:internal/modules/cjs/loader:1519:15)
                at Module._load (node:internal/modules/cjs/loader:1296:5)
                at Module.require (node:internal/modules/cjs/loader:1619:12)
                at require (node:internal/modules/helpers:191:16)
                at Object.<anonymous> (/workspace/main.js:1:16)
            """;

        var result = _parser.Parse(output, "/workspace/main.js");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal(1, diag.Line);
        Assert.Equal("lodash", result.MissingDependency);
    }

    [Fact]
    public void ScopedMissingModule_ResolvesCorrectScopeAndPackage()
    {
        const string output = """
            Error [ERR_MODULE_NOT_FOUND]: Cannot find package '@angular/core' imported from /workspace/main.js
                at packageResolve (node:internal/modules/esm/resolve:768:81)
                at moduleResolve (node:internal/modules/esm/resolve:859:18)
                at Object.<anonymous> (/workspace/main.js:2:1)
            """;

        var result = _parser.Parse(output, "/workspace/main.js");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal(2, diag.Line);
        Assert.Equal("@angular/core", result.MissingDependency);
    }
}
