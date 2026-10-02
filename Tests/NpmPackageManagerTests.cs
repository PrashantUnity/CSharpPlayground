using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class NpmPackageManagerTests
{
    private readonly NpmPackageManager _manager = new(new FakeProcessLauncher(), new FakeHostEnvironment());

    [Theory]
    [InlineData("%npm install lodash", new[] { "install", "lodash" })]
    [InlineData("%npm i -D typescript", new[] { "i", "-D", "typescript" })]
    [InlineData("!npm install express --save", new[] { "install", "express", "--save" })]
    [InlineData("  %npm   install   axios   ", new[] { "install", "axios" })]
    public void ValidDirectives_ParseCorrectly(string line, string[] expectedArgs)
    {
        var ok = _manager.TryParseDirective(line, out var command);

        Assert.True(ok);
        Assert.Equal(line.Trim(), command.Text);
        Assert.Equal(expectedArgs, command.Arguments);
    }

    [Theory]
    [InlineData("%pip install numpy")]
    [InlineData("const x = 1;")]
    [InlineData("#!javascript")]
    [InlineData("%npm")]
    public void InvalidDirectives_AreRejected(string line)
    {
        var ok = _manager.TryParseDirective(line, out _);
        Assert.False(ok);
    }

    [Fact]
    public void InstallCommand_GeneratesProperNpmInstallDirective()
    {
        var cmd = _manager.InstallCommand("lodash");

        Assert.Equal("%npm install lodash", cmd.Text);
        Assert.Equal(new[] { "install", "lodash" }, cmd.Arguments);
    }

    [Theory]
    [InlineData("lodash", "lodash")]
    [InlineData("lodash/debounce", "lodash")]
    [InlineData("@angular/core", "@angular/core")]
    [InlineData("@angular/core/testing", "@angular/core")]
    public void PackageForMissingDependency_ResolvesPackageName(string input, string expected)
    {
        Assert.Equal(expected, _manager.PackageForMissingDependency(input));
    }
}
