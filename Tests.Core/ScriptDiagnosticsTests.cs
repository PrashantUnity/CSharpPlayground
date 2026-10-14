using Microsoft.CodeAnalysis;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>Live diagnostics judge statements as the kernel runs them: a C# script.</summary>
public sealed class ScriptDiagnosticsTests
{
    [Theory]
    [InlineData("var x = 40 + 2;\nx")] // the last expression is the result
    [InlineData("Console.WriteLine(1)")] // a void call without a semicolon
    [InlineData("var p = new P(1);\nrecord P(int A);\np")] // a type after statements
    [InlineData("using var ms = new System.IO.MemoryStream();\nms.Length")] // a using declaration, then the result
    [InlineData("#r \"nuget: Humanizer, 2.14.1\"\nvar y = 1;\ny")] // a package reference line
    [InlineData("var t = await Task.FromResult(3);\nt")] // top-level await
    public void ValidScriptCode_HasNoErrors(string code)
    {
        var errors = Headless.Diagnostics(code).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("; ", errors.Select(e => $"{e.Id}@{e.Line}:{e.Column} {e.Message}")));
    }

    [Theory]
    [InlineData("int x = \"s\";", "CS0029", 1, 9)]
    [InlineData("var a = 1;\nundefinedName", "CS0103", 2, 1)]
    [InlineData("namespace N; class P { static void Main() { int z = \"q\"; } }", "CS0029", 1, 53)] // a program is checked as one
    public void Errors_AreReportedWhereTheyAre(string code, string id, int line, int column)
    {
        var error = Assert.Single(Headless.Diagnostics(code), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal((id, line, column), (error.Id, error.Line, error.Column));
    }
}
