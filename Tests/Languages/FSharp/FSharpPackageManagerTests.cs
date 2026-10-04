using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class FSharpPackageManagerTests
{
    private readonly FSharpPackageManager _manager = new();

    [Theory]
    [InlineData("#r \"nuget: Newtonsoft.Json\"", "Newtonsoft.Json")]
    [InlineData("#r \"nuget: FSharp.Data, 6.4.0\"", "FSharp.Data")]
    [InlineData("#r \"nuget: Plotly.NET\"", "Plotly.NET")]
    [InlineData("%nuget install MathNet.Numerics", "MathNet.Numerics")]
    [InlineData("!nuget add Serilog", "Serilog")]
    public void ParsesDirectivesSuccessfully(string line, string expectedPkg)
    {
        var parsed = _manager.TryParseDirective(line, out var command);

        Assert.True(parsed);
        Assert.NotNull(command);
        Assert.Contains(expectedPkg, command.Arguments);
    }

    [Fact]
    public void NonPackageDirective_ReturnsFalse()
    {
        var parsed = _manager.TryParseDirective("printfn \"hello\"", out var command);
        Assert.False(parsed);
        Assert.Null(command);
    }

    [Fact]
    public void InstallCommand_GeneratesFSharpNuGetDirective()
    {
        var command = _manager.InstallCommand("FSharpPlus");
        Assert.Equal("#r \"nuget: FSharpPlus\"", command.Text);
        Assert.Equal(new[] { "install", "FSharpPlus" }, command.Arguments);
    }
}
