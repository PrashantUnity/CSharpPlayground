using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class MavenPackageManagerTests
{
    private readonly MavenPackageManager _manager = new();

    [Theory]
    [InlineData("//DEPS com.google.code.gson:gson:2.11.0", new[] { "install", "com.google.code.gson:gson:2.11.0" })]
    [InlineData("// DEPS org.slf4j:slf4j-api:2.0.12", new[] { "install", "org.slf4j:slf4j-api:2.0.12" })]
    [InlineData("//deps com.fasterxml.jackson.core:jackson-databind:2.17.0", new[] { "install", "com.fasterxml.jackson.core:jackson-databind:2.17.0" })]
    [InlineData("%maven install com.google.guava:guava:33.1.0-jre", new[] { "install", "com.google.guava:guava:33.1.0-jre" })]
    [InlineData("!maven add org.apache.commons:commons-lang3:3.14.0", new[] { "install", "org.apache.commons:commons-lang3:3.14.0" })]
    [InlineData("#r \"maven: org.xerial:sqlite-jdbc, 3.45.1.0\"", new[] { "install", "org.xerial:sqlite-jdbc:3.45.1.0" })]
    [InlineData("#r \"maven:com.google.code.gson:gson:2.11.0\"", new[] { "install", "com.google.code.gson:gson:2.11.0" })]
    public void ValidDirectives_ParseCorrectly(string line, string[] expectedArgs)
    {
        var ok = _manager.TryParseDirective(line, out var command);

        Assert.True(ok);
        Assert.Equal(expectedArgs, command.Arguments);
    }

    [Theory]
    [InlineData("%pip install numpy")]
    [InlineData("%npm install lodash")]
    [InlineData("#r \"nuget: Newtonsoft.Json\"")]
    [InlineData("int x = 10;")]
    [InlineData("// standard comment")]
    [InlineData("%maven")]
    public void InvalidDirectives_AreRejected(string line)
    {
        var ok = _manager.TryParseDirective(line, out _);
        Assert.False(ok);
    }

    [Fact]
    public void InstallCommand_GeneratesProperMavenDirective()
    {
        var cmd = _manager.InstallCommand("com.google.code.gson:gson:2.11.0");

        Assert.Equal("//DEPS com.google.code.gson:gson:2.11.0", cmd.Text);
        Assert.Equal(new[] { "install", "com.google.code.gson:gson:2.11.0" }, cmd.Arguments);
    }

    [Theory]
    [InlineData("Gson", "com.google.code.gson:gson:2.11.0")]
    [InlineData("JsonElement", "com.google.code.gson:gson:2.11.0")]
    [InlineData("JSONObject", "org.json:json:20240303")]
    [InlineData("ObjectMapper", "com.fasterxml.jackson.core:jackson-databind:2.17.0")]
    [InlineData("JsonNode", "com.fasterxml.jackson.core:jackson-databind:2.17.0")]
    [InlineData("StringUtils", "org.apache.commons:commons-lang3:3.14.0")]
    [InlineData("FileUtils", "commons-io:commons-io:2.16.1")]
    [InlineData("ImmutableList", "com.google.guava:guava:33.1.0-jre")]
    [InlineData("LoggerFactory", "org.slf4j:slf4j-api:2.0.12")]
    [InlineData("org.sqlite.JDBC", "org.xerial:sqlite-jdbc:3.45.1.0")]
    [InlineData("XYChart", "org.knowm.xchart:xchart:3.8.7")]
    [InlineData("CustomUnknownClass", "CustomUnknownClass")]
    public void PackageForMissingDependency_ResolvesCoordinates(string missingSymbol, string expectedCoordinate)
    {
        Assert.Equal(expectedCoordinate, _manager.PackageForMissingDependency(missingSymbol));
    }

    [Fact]
    public void JavaCompilerDiagnosticParser_ExtractsMissingDependency_FromCannotFindSymbol()
    {
        var parser = new JavaCompilerDiagnosticParser();
        const string output = """
            /work/Main.java:6: error: cannot find symbol
                Gson gson = new Gson();
                ^
              symbol:   class Gson
              location: class Main
            1 error
            """;

        var result = parser.Parse(output, "/work/Main.java");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("Gson", result.MissingDependency);
    }

    [Fact]
    public void JavaCompilerDiagnosticParser_ExtractsMissingDependency_FromPackageDoesNotExist()
    {
        var parser = new JavaCompilerDiagnosticParser();
        const string output = """
            /work/Main.java:2: error: package com.google.gson does not exist
            import com.google.gson.Gson;
                                  ^
            1 error
            """;

        var result = parser.Parse(output, "/work/Main.java");

        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("com.google.gson", result.MissingDependency);
    }
}
