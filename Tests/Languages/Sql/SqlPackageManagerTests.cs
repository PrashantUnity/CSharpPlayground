using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class SqlPackageManagerTests
{
    private readonly SqlPackageManager _manager = new();

    // The regex is: ^\s*(?:\.load|%load|#load)\s+(?<id>[^\s;]+)
    // For ".load ./ext/json1" the id group is "./ext/json1" (full path), not just "json1"
    [Theory]
    [InlineData(".load spatialite", "spatialite")]
    [InlineData("%load fts5", "fts5")]
    [InlineData("%load csv", "csv")]
    [InlineData(".load ./ext/json1", "./ext/json1")]  // full path preserved as-is
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
        // The implementation sets command to an empty PackageCommand when false;
        // TryParseDirective returns false and out-param is not null but empty.
        var parsed = _manager.TryParseDirective("SELECT * FROM users;", out var command);
        Assert.False(parsed);
        // The command is an empty placeholder (not null) when returning false
        Assert.Empty(command.Text);
    }

    [Fact]
    public void InstallCommand_GeneratesSqliteLoadDirective()
    {
        var command = _manager.InstallCommand("spatialite");
        Assert.Equal(".load spatialite", command.Text);
        Assert.Equal(new[] { "load", "spatialite" }, command.Arguments);
    }
}
