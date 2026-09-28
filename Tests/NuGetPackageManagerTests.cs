using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NuGetPackageManagerTests
{
    private readonly NuGetPackageManager _manager = new();

    [Theory]
    [InlineData("#r \"nuget: Newtonsoft.Json\"", new[] { "install", "Newtonsoft.Json" })]
    [InlineData("#r \"nuget: Newtonsoft.Json, 13.0.3\"", new[] { "install", "Newtonsoft.Json", "13.0.3" })]
    [InlineData("#r \"nuget:SkiaSharp, 2.88.8\";", new[] { "install", "SkiaSharp", "2.88.8" })]
    [InlineData("%nuget install Dapper", new[] { "install", "Dapper" })]
    [InlineData("!nuget add ScottPlot 5.0.0", new[] { "install", "ScottPlot", "5.0.0" })]
    [InlineData("dotnet add package Spectre.Console", new[] { "install", "Spectre.Console" })]
    [InlineData("dotnet add package YamlDotNet -v 15.1.0", new[] { "install", "YamlDotNet", "15.1.0" })]
    public void ValidDirectives_ParseCorrectly(string line, string[] expectedArgs)
    {
        var ok = _manager.TryParseDirective(line, out var command);

        Assert.True(ok);
        Assert.Equal(expectedArgs, command.Arguments);
    }

    [Theory]
    [InlineData("%pip install numpy")]
    [InlineData("%npm install lodash")]
    [InlineData("var x = 10;")]
    [InlineData("// just a comment")]
    [InlineData("%nuget")]
    public void InvalidDirectives_AreRejected(string line)
    {
        var ok = _manager.TryParseDirective(line, out _);
        Assert.False(ok);
    }

    [Fact]
    public void InstallCommand_GeneratesProperNuGetDirective()
    {
        var cmd = _manager.InstallCommand("Newtonsoft.Json");

        Assert.Equal("#r \"nuget: Newtonsoft.Json\"", cmd.Text);
        Assert.Equal(new[] { "install", "Newtonsoft.Json" }, cmd.Arguments);
    }

    [Theory]
    [InlineData("JObject", "Newtonsoft.Json")]
    [InlineData("SKBitmap", "SkiaSharp")]
    [InlineData("ScottPlot", "ScottPlot")]
    [InlineData("Plot", "ScottPlot")]
    [InlineData("Dapper", "Dapper")]
    [InlineData("YamlDotNet", "YamlDotNet")]
    [InlineData("AnsiConsole", "Spectre.Console")]
    [InlineData("UnknownType", "UnknownType")]
    [InlineData("MyNamespace.SubNamespace.JObject", "Newtonsoft.Json")]
    public void PackageForMissingDependency_ResolvesPackageName(string input, string expected)
    {
        Assert.Equal(expected, _manager.PackageForMissingDependency(input));
    }
}
