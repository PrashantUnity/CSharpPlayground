using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoPackageManagerTests
{
    private readonly GoPackageManager _manager = new();

    [Theory]
    [InlineData("%go get github.com/google/uuid", "github.com/google/uuid")]
    [InlineData("%get github.com/gin-gonic/gin", "github.com/gin-gonic/gin")]
    [InlineData("!go add go.uber.org/zap", "go.uber.org/zap")]
    [InlineData("// #go: github.com/stretchr/testify", "github.com/stretchr/testify")]
    [InlineData("// #golang: get gorm.io/gorm", "gorm.io/gorm")]
    [InlineData("#r \"go: github.com/spf13/cobra\"", "github.com/spf13/cobra")]
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
        var parsed = _manager.TryParseDirective("fmt.Println(\"hello\")", out var command);
        Assert.False(parsed);
        Assert.Null(command);
    }

    [Theory]
    [InlineData("uuid", "github.com/google/uuid")]
    [InlineData("gin", "github.com/gin-gonic/gin")]
    [InlineData("testify", "github.com/stretchr/testify")]
    [InlineData("zap", "go.uber.org/zap")]
    [InlineData("gorm", "gorm.io/gorm")]
    [InlineData("github.com/custom/module", "github.com/custom/module")]
    public void PackageMap_ResolvesAliasesToCanonicalModulePaths(string alias, string expected)
    {
        var resolved = GoPackageMap.PackageFor(alias);
        Assert.Equal(expected, resolved);
    }
}
