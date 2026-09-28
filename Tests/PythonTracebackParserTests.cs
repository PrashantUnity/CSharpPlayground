using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A failed Python run's traceback becomes a Problems entry at the right line of the script. The samples are real
/// output of Python 3.9 and 3.14, with the folder they ran in replaced by {DIR}.
/// </summary>
public class PythonTracebackParserTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_Traceback_" + Guid.NewGuid().ToString("N"));
    private readonly PythonTracebackParser _parser = new();

    public PythonTracebackParserTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "runtime.py"), "import helper\n\ndef main():\n    values = [1, 2, 0]\n    for v in values:\n        print(helper.divide(10, v))\n\nmain()\n");
        File.WriteAllText(Path.Combine(_dir, "helper.py"), "def divide(a, b):\n    return a / b\n");
        File.WriteAllText(Path.Combine(_dir, "syntax.py"), "def f():\n    x = (1,\n         2\n    print(\"x\"\n");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string File_(string name) => Path.Combine(_dir, name);

    private Services.Processes.DiagnosticParseResult Parse(string sample, string script) =>
        _parser.Parse(sample.Replace("{DIR}", _dir).Replace("/", Path.DirectorySeparatorChar.ToString()), File_(script));

    private const string Runtime39 = """
        10.0
        5.0
        Traceback (most recent call last):
          File "{DIR}/runtime.py", line 8, in <module>
            main()
          File "{DIR}/runtime.py", line 6, in main
            print(helper.divide(10, v))
          File "{DIR}/helper.py", line 2, in divide
            return a / b
        ZeroDivisionError: division by zero
        """;

    private const string Runtime314 = """
        10.0
        5.0
        Traceback (most recent call last):
          File "{DIR}/runtime.py", line 8, in <module>
            main()
            ~~~~^^
          File "{DIR}/runtime.py", line 6, in main
            print(helper.divide(10, v))
                  ~~~~~~~~~~~~~^^^^^^^
          File "{DIR}/helper.py", line 2, in divide
            return a / b
                   ~~^~~
        ZeroDivisionError: division by zero
        """;

    [Fact]
    public void RuntimeError_IsAtTheScriptsLastLineInTheTraceback_NotInsideTheModuleItCalled()
    {
        var problem = Assert.Single(Parse(Runtime39, "runtime.py").Diagnostics);

        Assert.Equal("ZeroDivisionError", problem.Id);
        Assert.Equal("division by zero", problem.Message);
        Assert.Equal(6, problem.Line);
    }

    [Fact]
    public void Python311Markers_GiveTheColumnsOfTheFailingExpression()
    {
        var problem = Assert.Single(Parse(Runtime314, "runtime.py").Diagnostics);

        Assert.Equal(6, problem.Line);
        // "        print(helper.divide(10, v))": the markers start under "helper" (column 15) and end under ")" of the call.
        Assert.Equal(15, problem.Column);
        Assert.Equal(34, problem.EndColumn);
    }

    [Fact]
    public void SyntaxError_WithoutATracebackHeading_PointsAtTheCaret()
    {
        const string sample314 = """
              File "{DIR}/syntax.py", line 4
                print("x"
                     ^
            SyntaxError: '(' was never closed
            """;
        const string sample39 = """
              File "{DIR}/syntax.py", line 4
                print("x"
                ^
            SyntaxError: invalid syntax
            """;

        var newer = Assert.Single(Parse(sample314, "syntax.py").Diagnostics);
        Assert.Equal("SyntaxError", newer.Id);
        Assert.Equal("'(' was never closed", newer.Message);
        Assert.Equal(4, newer.Line);
        Assert.Equal(10, newer.Column); // the "(" of "    print("x"", with the line's own indentation put back

        var older = Assert.Single(Parse(sample39, "syntax.py").Diagnostics);
        Assert.Equal(4, older.Line);
        Assert.Equal(5, older.Column);
    }

    [Fact]
    public void IndentationError_IsReportedOnItsLine()
    {
        const string sample = """
              File "{DIR}/indent.py", line 3
                y = 2
            IndentationError: unexpected indent
            """;

        var problem = Assert.Single(Parse(sample, "indent.py").Diagnostics);

        Assert.Equal("IndentationError", problem.Id);
        Assert.Equal(3, problem.Line);
    }

    [Fact]
    public void ChainedExceptions_ReportTheLastOne()
    {
        const string sample = """
            Traceback (most recent call last):
              File "{DIR}/chained.py", line 2, in <module>
                {}["k"]
                ~~^^^^^
            KeyError: 'k'

            The above exception was the direct cause of the following exception:

            Traceback (most recent call last):
              File "{DIR}/chained.py", line 4, in <module>
                raise ValueError("bad value") from e
            ValueError: bad value
            """;

        var problem = Assert.Single(Parse(sample, "chained.py").Diagnostics);

        Assert.Equal("ValueError", problem.Id);
        Assert.Equal("bad value", problem.Message);
        Assert.Equal(4, problem.Line);
    }

    [Fact]
    public void ExceptionGroup_IsTheError_NotItsLastMember()
    {
        const string sample = """
              + Exception Group Traceback (most recent call last):
              |   File "{DIR}/group.py", line 1, in <module>
              |     raise ExceptionGroup("many", [ValueError("a"), TypeError("b")])
              | ExceptionGroup: many (2 sub-exceptions)
              +-+---------------- 1 ----------------
                | ValueError: a
                +---------------- 2 ----------------
                | TypeError: b
                +------------------------------------
            """;

        var problem = Assert.Single(Parse(sample, "group.py").Diagnostics);

        Assert.Equal("ExceptionGroup", problem.Id);
        Assert.Equal("many (2 sub-exceptions)", problem.Message);
        Assert.Equal(1, problem.Line);
    }

    [Theory]
    [InlineData("No module named 'numpy'", "numpy")]
    [InlineData("No module named 'numpy.core._multiarray_umath'", "numpy")]
    [InlineData("No module named 'cv2'", "cv2")]
    public void MissingModule_IsReportedForInstalling(string message, string module)
    {
        var sample = $$"""
            Traceback (most recent call last):
              File "{DIR}/missing.py", line 2, in <module>
                import something
            ModuleNotFoundError: {{message}}
            """;

        var result = Parse(sample, "missing.py");

        Assert.Equal(module, result.MissingDependency);
        Assert.Equal(2, Assert.Single(result.Diagnostics).Line);
    }

    [Fact]
    public void AnErrorOutsideTheScript_SaysWhereItWas()
    {
        const string sample = """
            Traceback (most recent call last):
              File "{DIR}/helper.py", line 2, in divide
                return a / b
            ZeroDivisionError: division by zero
            """;

        var problem = Assert.Single(Parse(sample, "runtime.py").Diagnostics);

        Assert.Equal(1, problem.Line);
        Assert.Equal("division by zero (in helper.py, line 2)", problem.Message);
    }

    [Fact]
    public void WindowsPaths_MatchTheScript_WhateverTheirCase()
    {
        const string sample = """
            Traceback (most recent call last):
              File "C:\Users\me\Proj\main.py", line 3, in <module>
                open("missing.txt")
            FileNotFoundError: [Errno 2] No such file or directory: 'missing.txt'
            """;

        var problem = Assert.Single(new PythonTracebackParser().Parse(sample, @"c:\users\ME\proj\main.py").Diagnostics);

        Assert.Equal("FileNotFoundError", problem.Id);
        Assert.Equal(3, problem.Line);
    }

    [Fact]
    public void OutputWithoutATraceback_HasNoProblems()
    {
        Assert.Empty(_parser.Parse("Error: the config file is missing\nexit\n", File_("runtime.py")).Diagnostics);
        Assert.Empty(_parser.Parse(string.Empty, File_("runtime.py")).Diagnostics);
    }
}
