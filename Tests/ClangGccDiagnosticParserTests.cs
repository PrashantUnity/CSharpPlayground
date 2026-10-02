using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class ClangGccDiagnosticParserTests
{
    private readonly ClangGccDiagnosticParser _parser = new();

    [Fact]
    public void ParsesClangCompilerError()
    {
        const string output = @"
main.cpp:5:10: error: 'vector' is not a member of 'std'
    std::vector<int> nums;
         ^~~~~~
1 error generated.
";
        var result = _parser.Parse(output, "main.cpp");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(5, diag.Line);
        Assert.Equal(10, diag.Column);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Contains("'vector' is not a member of 'std'", diag.Message);
    }

    [Fact]
    public void ParsesGccCompilerErrorAndWarning()
    {
        const string output = @"
solution.cpp:12:5: error: expected ';' before 'return'
     return 0;
     ^~~~~~
solution.cpp:8:9: warning: unused variable 'x' [-Wunused-variable]
     int x = 42;
         ^
";
        var result = _parser.Parse(output, "solution.cpp");

        Assert.Equal(2, result.Diagnostics.Count);

        var err = result.Diagnostics[0];
        Assert.Equal(12, err.Line);
        Assert.Equal(DiagnosticSeverity.Error, err.Severity);
        Assert.Contains("expected ';'", err.Message);

        var warn = result.Diagnostics[1];
        Assert.Equal(8, warn.Line);
        Assert.Equal(DiagnosticSeverity.Warning, warn.Severity);
        Assert.Contains("unused variable 'x'", warn.Message);
    }

    [Fact]
    public void ParsesMsvcCompilerError()
    {
        const string output = @"
main.cpp(15,8): error C2065: 'cout': undeclared identifier
main.cpp(22): fatal error C1004: unexpected end of file found
";
        var result = _parser.Parse(output, "main.cpp");

        Assert.Equal(2, result.Diagnostics.Count);

        var err1 = result.Diagnostics[0];
        Assert.Equal(15, err1.Line);
        Assert.Equal(8, err1.Column);
        Assert.Equal("C2065", err1.Id);
        Assert.Equal(DiagnosticSeverity.Error, err1.Severity);
        Assert.Contains("'cout': undeclared identifier", err1.Message);

        var err2 = result.Diagnostics[1];
        Assert.Equal(22, err2.Line);
        Assert.Equal("C1004", err2.Id);
    }

    [Fact]
    public void ParsesAppleAssertionFailure()
    {
        const string output = @"
Running tests...
Assertion failed: (val >= 0), function solve, file solution.cpp, line 34.
Abort trap: 6
";
        var result = _parser.Parse(output, "solution.cpp");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("ASSERT_FAIL", diag.Id);
        Assert.Equal(34, diag.Line);
        Assert.Contains("val >= 0", diag.Message);
    }

    [Fact]
    public void ParsesSegmentationFault()
    {
        const string output = @"
Reading inputs...
Segmentation fault: 11
";
        var result = _parser.Parse(output, "main.cpp");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("SIGSEGV", diag.Id);
        Assert.Contains("invalid memory access", diag.Message);
    }

    [Fact]
    public void MissingIostreamHeader_IsNotExtractedAsPackageDependency()
    {
        const string output = @"
C:\Users\prashant\Downloads\CStudio\script_190947.cpp:2:10: fatal error: 'iostream' file not found
    2 | #include <iostream>
      |          ^~~~~~~~~~
1 error generated.
";
        var result = _parser.Parse(output, @"C:\Users\prashant\Downloads\CStudio\script_190947.cpp");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(2, diag.Line);
        Assert.Equal(10, diag.Column);
        Assert.Contains("'iostream' file not found", diag.Message);
        Assert.Null(result.MissingDependency);
    }
}
