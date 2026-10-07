using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Palettes from both engines, in dark and light, over hundreds of generations: text and accents always readable
/// (WCAG and APCA), syntax colours readable and apart, chart series distinct with colour-vision deficiencies, the same
/// spec always the same palette, and locked sections never changing.
/// </summary>
public class PaletteGeneratorTests
{
    public static TheoryData<ColorEngineKind, bool> EnginesAndSchemes => new()
    {
        { ColorEngineKind.Oklch, true },
        { ColorEngineKind.Oklch, false },
        { ColorEngineKind.Hct, true },
        { ColorEngineKind.Hct, false },
    };

    private static ColorRgb Token(IReadOnlyDictionary<string, string> tokens, string key) => ColorRgb.FromHex(tokens[key]);

    [Theory]
    [MemberData(nameof(EnginesAndSchemes))]
    public void EveryGeneratedPalette_IsReadable_AndItsSyntaxAndChartColoursStayApart(ColorEngineKind engine, bool dark)
    {
        var spec = new PaletteSpec { Engine = engine, IsDark = dark };
        for (var i = 0; i < 200; i++)
        {
            spec = PaletteGenerator.Regenerate(spec);
            var palette = PaletteGenerator.Generate(spec);
            var tokens = ThemeTokenMapper.Map(palette);
            var surface = Token(tokens, "DsSurfaceBrush");
            var background = Token(tokens, "DsBgBrush");
            var context = $"{engine} dark={dark} seed={spec.Seed} hue={spec.BaseHue:0}";

            foreach (var key in new[] { "DsTextBrush", "DsMutedBrush" })
            {
                foreach (var on in new[] { surface, background })
                {
                    var color = Token(tokens, key);
                    Assert.True(color.ContrastRatio(on) >= 4.5f, $"{context}: {key} {color.ToHex()} on {on.ToHex()} is {color.ContrastRatio(on):0.00}:1");
                    Assert.True(MathF.Abs(ApcaEngine.ContrastLc(color, on)) >= 60f, $"{context}: {key} Lc {ApcaEngine.ContrastLc(color, on):0}");
                }
            }

            Assert.True(Token(tokens, "DsTextBrush").ContrastRatio(surface) >= 7f, context);
            Assert.True(Token(tokens, "EditorFgBrush").ContrastRatio(Token(tokens, "EditorBgBrush")) >= 7f, context);
            foreach (var key in new[] { "DsPrimaryBrush", "DsErrorBrush", "DsWarningBrush", "DsSuccessBrush", "DsInfoBrush", "NavThemeFgBrush", "ServerMethodGetFgBrush" })
            {
                var on = key.StartsWith("ServerMethodGet", StringComparison.Ordinal) ? Token(tokens, "ServerMethodGetBgBrush") : surface;
                Assert.True(Token(tokens, key).ContrastRatio(on) >= 4.5f, $"{context}: {key} is {Token(tokens, key).ContrastRatio(on):0.00}:1");
            }

            var onAccent = Token(tokens, "DsOnAccentBrush");
            Assert.True(onAccent.ContrastRatio(Token(tokens, "DsPrimaryBrush")) >= 3f, $"{context}: text on the primary button");

            var editor = palette.EditorBackground;
            var syntax = new List<Oklab>();
            foreach (var role in PaletteSections.SyntaxRoles)
            {
                var color = Token(tokens, $"Syntax{role}Brush");
                var minimum = role == "Comment" ? 3f : 4.5f;
                Assert.True(color.ContrastRatio(editor) >= minimum, $"{context}: syntax {role} {color.ToHex()} is {color.ContrastRatio(editor):0.00}:1");
                if (role is not ("Comment" or "Punctuation")) syntax.Add(Oklab.FromRgb(color));
            }

            for (var a = 0; a < syntax.Count; a++)
            for (var b = a + 1; b < syntax.Count; b++)
            {
                Assert.True(syntax[a].DeltaE(syntax[b]) >= 0.05, $"{context}: syntax colours {a} and {b} are only {syntax[a].DeltaE(syntax[b]):0.000} apart");
            }

            for (var series = 1; series <= 8; series++)
            {
                Assert.True(Token(tokens, $"ChartSeries{series}Brush").ContrastRatio(surface) >= 3f, $"{context}: chart series {series}");
            }

            Assert.True(PaletteGenerator.MinimumChartDistance(palette) >= 0.035, $"{context}: chart series too alike with a colour-vision deficiency");
        }
    }

    [Theory]
    [MemberData(nameof(EnginesAndSchemes))]
    public void AtAAA_TextAndMutedTextReachSevenToOne(ColorEngineKind engine, bool dark)
    {
        var spec = new PaletteSpec { Engine = engine, IsDark = dark, ContrastTarget = 7 };
        for (var i = 0; i < 50; i++)
        {
            spec = PaletteGenerator.Regenerate(spec);
            var tokens = ThemeTokenMapper.Map(PaletteGenerator.Generate(spec));
            var surface = Token(tokens, "DsSurfaceBrush");
            Assert.True(Token(tokens, "DsMutedBrush").ContrastRatio(surface) >= 7f);
            Assert.True(Token(tokens, "DsPrimaryBrush").ContrastRatio(surface) >= 7f);
            Assert.True(MathF.Abs(ApcaEngine.ContrastLc(Token(tokens, "DsTextBrush"), surface)) >= 90f);
        }
    }

    [Theory]
    [MemberData(nameof(EnginesAndSchemes))]
    public void TheSameSpec_AlwaysGivesTheSamePalette(ColorEngineKind engine, bool dark)
    {
        var spec = PaletteGenerator.Regenerate(PaletteGenerator.Regenerate(new PaletteSpec { Engine = engine, IsDark = dark, Seed = 42 }));

        var first = ThemeTokenMapper.Map(PaletteGenerator.Generate(spec));
        var second = ThemeTokenMapper.Map(PaletteGenerator.Generate(spec));
        var fromJson = ThemeTokenMapper.Map(PaletteGenerator.Generate(PaletteSpec.FromJson(spec.ToJson())!));

        Assert.Equal(first, second);
        Assert.Equal(first, fromJson);
        Assert.Equal(PaletteGenerator.Regenerate(spec).Seed, PaletteGenerator.Regenerate(spec).Seed);
    }

    [Theory]
    [MemberData(nameof(EnginesAndSchemes))]
    public void LockedSections_NeverChange_WhenEverythingElseIsRegenerated(ColorEngineKind engine, bool dark)
    {
        var spec = new PaletteSpec { Engine = engine, IsDark = dark };
        var palette = PaletteGenerator.Generate(spec);
        string[] locked = [PaletteSections.Secondary, PaletteSections.Error, PaletteSections.SyntaxSection("String"), PaletteSections.Chart(3)];
        foreach (var id in locked) spec = PaletteGenerator.SetLocked(spec, id, true, palette);
        var kept = locked.ToDictionary(id => id, id => palette.Keys[id]);
        var changed = new HashSet<string>();

        for (var i = 0; i < 50; i++)
        {
            var before = PaletteGenerator.Generate(spec);
            spec = PaletteGenerator.Regenerate(spec);
            var after = PaletteGenerator.Generate(spec);
            foreach (var id in locked) Assert.Equal(kept[id], after.Keys[id]);
            foreach (var id in PaletteSections.All.Except(locked))
            {
                if (before.Keys[id] != after.Keys[id]) changed.Add(id);
            }
        }

        // Everything that isn't locked did move at some point (the primary follows the locked secondary's harmony).
        Assert.Contains(PaletteSections.Primary, changed);
        Assert.Contains(PaletteSections.Warning, changed);
        Assert.Contains(PaletteSections.SyntaxSection("Keyword"), changed);
        Assert.Contains(PaletteSections.Chart(1), changed);
    }

    [Theory]
    [InlineData(ColorEngineKind.Oklch)]
    [InlineData(ColorEngineKind.Hct)]
    public void RegeneratingOneSection_ChangesThatSectionOnly(ColorEngineKind engine)
    {
        var spec = PaletteGenerator.Regenerate(new PaletteSpec { Engine = engine, Seed = 7 });
        foreach (var id in new[] { PaletteSections.Primary, PaletteSections.Neutral, PaletteSections.Warning, PaletteSections.SyntaxSection("Keyword"), PaletteSections.Chart(5) })
        {
            var before = PaletteGenerator.Generate(spec);
            var next = PaletteGenerator.RegenerateSection(spec, id);
            var after = PaletteGenerator.Generate(next);

            Assert.NotEqual(before.Keys[id], after.Keys[id]);
            Assert.False(next.IsLocked(id));
            if (id is PaletteSections.Neutral) continue; // the greys carry the neutral's tint by design
            foreach (var other in PaletteSections.All.Where(o => o != id))
            {
                Assert.True(before.Keys[other] == after.Keys[other], $"Regenerating {id} changed {other}");
            }
        }
    }

    [Theory]
    [InlineData(ColorEngineKind.Oklch)]
    [InlineData(ColorEngineKind.Hct)]
    public void ASectionSetByHand_IsLocked_AndTheHarmonyIsBuiltAroundIt(ColorEngineKind kind)
    {
        var engine = ColorEngines.Get(kind);
        var spec = new PaletteSpec { Engine = kind, Harmony = ColorHarmonyMode.Triadic };
        var teal = ColorRgb.FromHex("#0F9D8A");

        spec = PaletteGenerator.SetSection(spec, PaletteSections.Secondary, teal);
        var palette = PaletteGenerator.Generate(spec);

        Assert.True(spec.IsLocked(PaletteSections.Secondary));
        Assert.Equal(teal, palette.Keys[PaletteSections.Secondary]);
        var secondaryHue = engine.Decompose(teal).Hue;
        Assert.InRange(Math.Abs(engine.RotateHue(palette.Parts[PaletteSections.Primary].Hue, 120) - secondaryHue) % 360, 0, 0.5);

        // Unlocking keeps every colour where it is until the next Generate.
        var unlocked = PaletteGenerator.SetLocked(spec, PaletteSections.Secondary, false, palette);
        Assert.Equal(palette.Keys, PaletteGenerator.Generate(unlocked).Keys);
    }

    [Fact]
    public void TheHarmonyWheelFacade_KeepsItsApi_AndUsesTheChosenEngine()
    {
        var oklch = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 30, Mode = ColorHarmonyMode.Complementary });
        var hct = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 30, Mode = ColorHarmonyMode.Complementary, Engine = ColorEngineKind.Hct });

        Assert.StartsWith("harmonic-complementary-", oklch.Id);
        Assert.Contains("OKLCH", oklch.Description);
        Assert.Contains("HCT", hct.Description);
        Assert.NotEqual(oklch.Colors["DsPrimaryBrush"], hct.Colors["DsPrimaryBrush"]);
        Assert.True(oklch.Colors.ContainsKey("SyntaxKeywordBrush"));
        Assert.True(oklch.Colors.ContainsKey("ChartSeries8Brush"));

        // The wheel's 210° (a complementary accent of 30°) is a blue in both engines.
        foreach (var theme in new[] { oklch, hct })
        {
            var primary = ColorRgb.FromHex(theme.Colors["DsPrimaryBrush"]);
            Assert.True(primary.B > primary.R, $"{theme.Description}: {primary.ToHex()}");
        }
    }

    [Fact]
    public void ThemesThatDontSetSyntaxOrChartColours_AreNotGivenThem_SoTheyKeepTheirOwn()
    {
        var theme = new FrySharp.Sdk.ThemeDefinition { Id = "x", Name = "x", IsDark = true, Colors = new(StringComparer.OrdinalIgnoreCase) { ["DsBgBrush"] = "#000000" } };

        HarmonicColorGenerator.EnsureCompleteTheme(theme);

        Assert.True(theme.Colors.ContainsKey("DsPrimaryBrush"));
        Assert.DoesNotContain(theme.Colors.Keys, k => k.StartsWith("Syntax", StringComparison.Ordinal) || k.StartsWith("ChartSeries", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratingAndMappingAPalette_TakesAFewMilliseconds()
    {
        var spec = new PaletteSpec { Engine = ColorEngineKind.Hct, IsDark = false };
        ThemeTokenMapper.Map(PaletteGenerator.Generate(spec));
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            spec = PaletteGenerator.Regenerate(spec);
            ThemeTokenMapper.Map(PaletteGenerator.Generate(spec));
        }

        // Budget 10 ms each (measured about 1–3 ms); generous here so a busy test machine doesn't fail it.
        Assert.True(watch.Elapsed.TotalMilliseconds / 20 < 40, $"{watch.Elapsed.TotalMilliseconds / 20:0.0} ms per palette");
    }
}
