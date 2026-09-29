using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RustDirectivesTests
{
    [Theory]
    [InlineData("rand", "rand", "\"*\"")]
    [InlineData("rand@0.8", "rand", "\"0.8\"")]
    [InlineData("rand = \"0.8\"", "rand", "\"0.8\"")]
    [InlineData("rand = 0.8", "rand", "\"0.8\"")]
    [InlineData("serde_json=1", "serde_json", "\"1\"")]
    [InlineData("tokio = \">=1, <2\"", "tokio", "\">=1, <2\"")]
    [InlineData("serde = { version = \"1\", features = [\"derive\"] }", "serde", "{ version = \"1\", features = [\"derive\"] }")]
    [InlineData("local = { path = '../local' }", "local", "{ path = '../local' }")]
    public void ACrateSpec_IsReadInEveryForm(string spec, string name, string tomlValue)
    {
        Assert.True(RustDirectives.TryParseCrate(spec, out var crate));
        Assert.Equal(name, crate.Name);
        Assert.Equal(tomlValue, crate.TomlValue);
        Assert.Equal($"{name} = {tomlValue}", crate.ToManifestLine());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("= 1")]
    [InlineData("9lives")]
    [InlineData("two words")]
    [InlineData("rand = ")]
    [InlineData("rand = nonsense!")]
    [InlineData("rand@")]
    [InlineData("a.b = \"1\"")]
    public void ANonsenseCrateSpec_IsRefused(string spec)
    {
        Assert.False(RustDirectives.TryParseCrate(spec, out _));
    }

    [Fact]
    public void Parse_ReadsCratesEditionAndProfileFromComments()
    {
        var directives = RustDirectives.Parse("""
            // #crate: rand = "0.8"
            // #crate: serde = { version = "1", features = ["derive"] }
            // #edition: 2024
            // #profile: release
            fn main() {}
            """);

        Assert.Equal(["rand", "serde"], directives.Crates.Select(c => c.Name));
        Assert.Equal("\"0.8\"", directives.Crates[0].TomlValue);
        Assert.Equal("2024", directives.Edition);
        Assert.True(directives.Release);
    }

    [Fact]
    public void Parse_OfPlainCode_ChangesNothing()
    {
        var directives = RustDirectives.Parse("fn main() { println!(\"# not a directive\"); }");

        Assert.Empty(directives.Crates);
        Assert.Equal("2021", directives.Edition);
        Assert.False(directives.Release);
        Assert.Same(RustDirectives.None, RustDirectives.Parse(null));
    }

    [Fact]
    public void Parse_TheLaterLineForACrateWins_AndTheDisplayCrateCannotBeRedefined()
    {
        var directives = RustDirectives.Parse("""
            // #crate: rand = "0.7"
            // #crate: fry = "9"
            // #crate: rand = "0.8"
            """);

        var rand = Assert.Single(directives.Crates);
        Assert.Equal("rand", rand.Name);
        Assert.Equal("\"0.8\"", rand.TomlValue);
    }

    [Theory]
    [InlineData("// #edition: 1999")]
    [InlineData("// #edition: twenty")]
    public void Parse_AnUnknownEdition_KeepsTheDefault(string line)
    {
        Assert.Equal("2021", RustDirectives.Parse(line).Edition);
    }

    [Theory]
    [InlineData("// #profile: debug", false)]
    [InlineData("// #profile: dev", false)]
    [InlineData("// #PROFILE: Release", true)]
    public void Parse_ReadsTheProfile(string line, bool release)
    {
        Assert.Equal(release, RustDirectives.Parse(line).Release);
    }
}
