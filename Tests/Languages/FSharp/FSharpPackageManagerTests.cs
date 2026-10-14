using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class FSharpPackageManagerTests
{
    private readonly FSharpPackageManager _manager = new();

    [Theory]
    [InlineData("#r \"nuget: Newtonsoft.Json\"")]
    [InlineData("#r \"nuget: FSharp.Data, 6.4.0\"")]
    [InlineData("%nuget install MathNet.Numerics")]
    [InlineData("printfn \"hello\"")]
    public void NoLineIsAPackageCommandToRunBeforehand_FsiLoadsItsOwnReferences(string line)
    {
        // a notebook that claimed "#r nuget:" ran an installer that did nothing for F#, and removed the line fsi needed
        var parsed = _manager.TryParseDirective(line, out var command);

        Assert.False(parsed);
        Assert.Empty(command.Arguments);
    }

    [Fact]
    public void InstallCommand_GeneratesFSharpNuGetDirective()
    {
        var command = _manager.InstallCommand("FSharpPlus");
        Assert.Equal("#r \"nuget: FSharpPlus\"", command.Text);
        Assert.Equal(new[] { "install", "FSharpPlus" }, command.Arguments);
    }

    [Fact]
    public async Task InstallingAPackage_IsALineForTheFile()
    {
        var said = new List<string>();
        var toolchain = new ToolchainInfo { LanguageId = "fsharp", ExecutablePath = "dotnet", Version = new Version(10, 0), DisplayName = "dotnet fsi", Source = "test" };

        var result = await _manager.RunAsync(_manager.InstallCommand("FSharp.Data, 6.4.0"), toolchain, said.Add);

        Assert.True(result.Success);
        Assert.Equal("#r \"nuget: FSharp.Data, 6.4.0\"", result.DirectiveToInsert);
    }
}
