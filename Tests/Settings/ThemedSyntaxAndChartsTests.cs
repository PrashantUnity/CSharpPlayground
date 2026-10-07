using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Syntax highlighting and charts follow the theme: a generated theme recolours every language by role and gives charts
/// its series colours; a theme without them gives every language its own colours back.
/// </summary>
[Collection("SettingsTests")]
public class ThemedSyntaxAndChartsTests : IDisposable
{
    private readonly StudioLanguageServices _services = new(Path.Combine(Path.GetTempPath(), "FryPDF_ThemedSyntax_" + Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        StudioAppContext.Instance.ThemeEngine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        SyntaxPaletteApplier.Refresh();
        // The language definitions are shared by the whole test run: give them back as the languages define them.
        foreach (var (_, definition) in Definitions()) SyntaxPaletteApplier.Forget(definition);
        try { Directory.Delete(_services.BaseDirectory, recursive: true); } catch (IOException) { }
    }

    private static Color ForegroundOf(HighlightingColor color) =>
        color.Foreground?.GetBrush(null!) is ISolidColorBrush brush ? brush.Color : default;

    private IEnumerable<(string Language, IHighlightingDefinition Definition)> Definitions()
    {
        foreach (var language in _services.Registry.All)
        {
            foreach (var dark in new[] { true, false })
            {
                if (language.GetHighlighting(dark) is { } definition) yield return ($"{language.Id} {(dark ? "dark" : "light")}", definition);
            }
        }
    }

    [Theory]
    [InlineData("Comment", "Comment")]
    [InlineData("StringInterpolation", "String")]
    [InlineData("Char", "String")]
    [InlineData("Escape", "String")]
    [InlineData("NumberLiteral", "Number")]
    [InlineData("Preprocessor", "Preprocessor")]
    [InlineData("Annotation", "Preprocessor")]
    [InlineData("Attribute", "Preprocessor")]
    [InlineData("Punctuation", "Punctuation")]
    [InlineData("MethodCall", "Function")]
    [InlineData("Builtin", "Function")]
    [InlineData("Type", "Type")]
    [InlineData("ValueTypeKeywords", "Keyword")]
    [InlineData("OperatorKeywords", "Keyword")]
    [InlineData("ParameterModifiers", "Keyword")]
    [InlineData("ThisOrBaseReference", "Keyword")]
    [InlineData("Control", "Keyword")]
    [InlineData("Storage", "Keyword")]
    [InlineData("GetSetAddRemove", "Keyword")]
    [InlineData("Unheard", null)]
    public void LanguageColourNames_MapToSyntaxRoles(string name, string? role) => Assert.Equal(role, SyntaxPaletteApplier.RoleOf(name));

    [Fact]
    public void EveryNamedColourOfEveryLanguage_HasARole()
    {
        var unmapped = Definitions()
            .SelectMany(d => d.Definition.NamedHighlightingColors.Where(c => SyntaxPaletteApplier.RoleOf(c.Name) == null).Select(c => $"{d.Language}: {c.Name}"))
            .ToList();
        Assert.Empty(unmapped);
        Assert.True(Definitions().Count() >= 16);
    }

    [Fact]
    public void AGeneratedTheme_RecoloursEveryLanguage_AndABuiltInThemeGivesTheirOwnColoursBack()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        SyntaxPaletteApplier.Refresh();
        // Some languages use one definition for both schemes.
        var definitions = Definitions().Select(d => SyntaxPaletteApplier.Themed(d.Definition)!).Distinct().ToList();
        var own = definitions.SelectMany(d => d.NamedHighlightingColors).Distinct().ToDictionary(c => c, ForegroundOf);

        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 20, Mode = ColorHarmonyMode.Triadic });
        engine.RegisterTheme(theme);
        engine.ApplyTheme(theme.Id); // without a running UI the applier follows at once (with one, once per burst)
        SyntaxPaletteApplier.Refresh();

        var keyword = Color.Parse(theme.Colors["SyntaxKeywordBrush"]);
        var comment = Color.Parse(theme.Colors["SyntaxCommentBrush"]);
        foreach (var definition in definitions)
        {
            foreach (var color in definition.NamedHighlightingColors)
            {
                var expected = Color.Parse(theme.Colors[$"Syntax{SyntaxPaletteApplier.RoleOf(color.Name)}Brush"]);
                Assert.Equal(expected, ForegroundOf(color));
            }
        }

        var csharp = _services.Registry.Get("csharp")!.GetHighlighting(true)!;
        Assert.Equal(keyword, ForegroundOf(csharp.GetNamedColor("Keywords")));
        Assert.Equal(comment, ForegroundOf(csharp.GetNamedColor("Comment")));

        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        SyntaxPaletteApplier.Refresh();
        foreach (var (color, foreground) in own) Assert.Equal(foreground, ForegroundOf(color));
        Assert.False(SyntaxPaletteApplier.Refresh());
    }

    [Fact]
    public void Charts_UseTheThemesSeriesColours_AndTheDefaultPaletteOtherwise()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        Assert.Equal(ChartPaletteService.ExpressivePalette[2], ChartPaletteService.GetSeriesColor(2));

        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 200 }, isDark: false);
        engine.RegisterTheme(theme);
        engine.ApplyTheme(theme.Id);

        for (var i = 0; i < 8; i++) Assert.Equal(theme.Colors[$"ChartSeries{i + 1}Brush"], ChartPaletteService.GetSeriesColor(i));
        Assert.Equal(theme.Colors["ChartSeries1Brush"], ChartPaletteService.GetSeriesColor(8));
        Assert.Equal(8, Enumerable.Range(0, 8).Select(ChartPaletteService.GetSeriesColor).Distinct().Count());
    }
}
