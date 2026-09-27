using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class JavaCompilerDiagnosticParserTests
{
    private readonly JavaCompilerDiagnosticParser _parser = new();

    [Fact]
    public void EmptyOutput_ReturnsEmptyResult()
    {
        var result = _parser.Parse(string.Empty, "/work/Main.java");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void JavacCompileError_IsParsedWithLineAndCaretColumn()
    {
        const string output = """
            /work/Main.java:5: error: cannot find symbol
                System.out.printl("Hello");
                          ^
              symbol:   method printl(String)
              location: variable out of type PrintStream
            1 error
            """;

        var result = _parser.Parse(output, "/work/Main.java");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("JAVAC", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(5, diag.Line);
        Assert.Equal(15, diag.Column); // Column where '^' was located
        Assert.Contains("cannot find symbol", diag.Message);
        Assert.Contains("method printl(String)", diag.Message);
    }

    [Fact]
    public void JavacWarning_IsParsedWithWarningSeverity()
    {
        const string output = """
            /work/Main.java:8: warning: [deprecation] Date() in Date has been deprecated
                Date d = new Date();
                         ^
            1 warning
            """;

        var result = _parser.Parse(output, "/work/Main.java");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("JAVAC_WARN", diag.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diag.Severity);
        Assert.Equal(8, diag.Line);
        Assert.Contains("deprecated", diag.Message);
    }

    [Fact]
    public void MultipleJavacErrors_AreAllParsed()
    {
        const string output = """
            /work/Main.java:3: error: ';' expected
                int a = 1
                         ^
            /work/Main.java:5: error: cannot find symbol
                int b = c;
                        ^
            2 errors
            """;

        var result = _parser.Parse(output, "/work/Main.java");

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(3, result.Diagnostics[0].Line);
        Assert.Equal(5, result.Diagnostics[1].Line);
    }

    [Fact]
    public void JavaRuntimeUncaughtException_IsParsedFromStackTrace()
    {
        const string output = """
            Exception in thread "main" java.lang.ArithmeticException: / by zero
            	at Main.main(Main.java:6)
            """;

        var result = _parser.Parse(output, "/work/Main.java");

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("java.lang.ArithmeticException", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(6, diag.Line);
        Assert.Contains("/ by zero", diag.Message);
    }
}
