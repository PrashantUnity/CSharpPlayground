using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The colour science palettes are built on: OKLab/OKLCH against Björn Ottosson's and CSS Color 4's reference values,
/// CSS gamut mapping, Material HCT against material-color-utilities' own test vectors, both engines' scales, and
/// contrast solving.
/// </summary>
public class PerceptualColorTests
{
    private static ColorRgb Hex(string hex) => ColorRgb.FromHex(hex);

    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    [Theory]
    [InlineData("#FFFFFF", 1.0, 0.0, 0.0)]
    [InlineData("#000000", 0.0, 0.0, 0.0)]
    [InlineData("#FF0000", 0.62796, 0.22486, 0.12585)]
    [InlineData("#00FF00", 0.86644, -0.23389, 0.17950)]
    [InlineData("#0000FF", 0.45201, -0.03246, -0.31153)]
    public void Oklab_MatchesTheReferenceValues(string hex, double l, double a, double b)
    {
        var lab = Oklab.FromRgb(Hex(hex));

        Assert.Equal(l, lab.L, 0.0005);
        Assert.Equal(a, lab.A, 0.0005);
        Assert.Equal(b, lab.B, 0.0005);
    }

    [Fact]
    public void Oklch_RoundTripsEverySampledSrgbColourExactly()
    {
        for (var r = 0; r < 256; r += 17)
        for (var g = 0; g < 256; g += 17)
        for (var b = 0; b < 256; b += 17)
        {
            var color = new ColorRgb((byte)r, (byte)g, (byte)b);
            Assert.Equal(color, Oklch.FromRgb(color).ToRgb());
        }
    }

    [Theory]
    [InlineData(0.70, 0.40, 150)]
    [InlineData(0.50, 0.35, 270)]
    [InlineData(0.90, 0.30, 30)]
    [InlineData(0.30, 0.30, 90)]
    [InlineData(0.62, 0.37, 29)]
    public void GamutMapping_KeepsLightnessAndHue_AndOnlyGivesUpChroma(double l, double c, double h)
    {
        var request = new Oklch(l, c, h);
        Assert.False(request.IsInSrgbGamut);

        var mapped = request.GamutMapped();
        var shown = Oklch.FromRgb(request.ToRgb());

        Assert.True(mapped.C < c);
        Assert.InRange(Math.Abs(shown.L - l), 0, 0.02);
        // CSS allows the final clip to move the colour by less than a just-noticeable difference, which can turn the
        // hue of a nearly grey result by several degrees; a clearly coloured one keeps its hue.
        if (shown.C > 0.08) Assert.InRange(HueDistance(shown.H, h), 0, 3);
        Assert.InRange(new Oklch(l, mapped.C, h).ToOklab().DeltaE(mapped.ToOklab()), 0, Oklch.JustNoticeableDifference);
    }

    [Fact]
    public void GamutMapping_LeavesDisplayableColoursAlone_AndSendsTheEndsToBlackAndWhite()
    {
        var inGamut = Oklch.FromRgb(Hex("#3366CC"));
        Assert.Equal(inGamut, inGamut.GamutMapped());
        Assert.Equal(ColorRgb.White, new Oklch(1.2, 0.2, 40).ToRgb());
        Assert.Equal(ColorRgb.Black, new Oklch(-0.1, 0.2, 40).ToRgb());
    }

    // material-color-utilities, Cam16Test.
    [Theory]
    [InlineData("#FF0000", 27.408, 113.357, 46.445)]
    [InlineData("#00FF00", 142.139, 108.410, 79.331)]
    [InlineData("#0000FF", 282.788, 87.230, 25.465)]
    [InlineData("#FFFFFF", 209.492, 2.869, 100.0)]
    public void Cam16_MatchesMaterialColorUtilities(string hex, double hue, double chroma, double j)
    {
        var cam = MaterialCam16.FromArgb(MaterialColorUtils.ToArgb(Hex(hex)));

        Assert.Equal(hue, cam.Hue, 0.001);
        Assert.Equal(chroma, cam.Chroma, 0.001);
        Assert.Equal(j, cam.J, 0.001);
    }

    [Fact]
    public void Cam16_OfBlack_IsAllZero()
    {
        var cam = MaterialCam16.FromArgb(MaterialColorUtils.ToArgb(ColorRgb.Black));
        Assert.Equal(0, cam.Chroma, 6);
        Assert.Equal(0, cam.J, 6);
    }

    // material-color-utilities, TonalPaletteTest "tonesOfBlue".
    [Theory]
    [InlineData(0, "#000000")]
    [InlineData(10, "#00006E")]
    [InlineData(20, "#0001AC")]
    [InlineData(30, "#0000EF")]
    [InlineData(40, "#343DFF")]
    [InlineData(50, "#5A64FF")]
    [InlineData(60, "#7C84FF")]
    [InlineData(70, "#9DA3FF")]
    [InlineData(80, "#BEC2FF")]
    [InlineData(90, "#E0E0FF")]
    [InlineData(95, "#F1EFFF")]
    [InlineData(99, "#FFFBFF")]
    [InlineData(100, "#FFFFFF")]
    public void TonalPalette_OfBlue_MatchesMaterialColorUtilities(int tone, string expected)
    {
        var palette = TonalPalette.FromRgb(Hex("#0000FF"));
        Assert.Equal(expected, palette.Tone(tone).ToHex());
    }

    [Fact]
    public void Hct_SolvesEveryHueChromaAndTone_KeepingToneAndHue_AndOnlyLoweringChromaAtTheGamutEdge()
    {
        for (var hue = 15.0; hue < 360; hue += 30)
        foreach (var chroma in new[] { 0.0, 10, 25, 50, 100 })
        foreach (var tone in new[] { 20.0, 30, 40, 50, 60, 70, 80 })
        {
            var color = new Hct(hue, chroma, tone).ToRgb();
            var back = Hct.FromRgb(color);

            Assert.InRange(Math.Abs(back.Tone - tone), 0, 0.5);
            Assert.True(back.Chroma <= chroma + 2.5, $"h{hue} c{chroma} t{tone}: chroma {back.Chroma}");
            if (back.Chroma > 5) Assert.True(HueDistance(back.Hue, hue) <= 4, $"h{hue} c{chroma} t{tone}: hue {back.Hue}");
            if (back.Chroma < chroma - 2.5)
            {
                // Lowered only because sRGB can't go further: the colour sits on the cube's surface.
                Assert.True(color.R is 0 or 255 || color.G is 0 or 255 || color.B is 0 or 255, $"h{hue} c{chroma} t{tone}: {color.ToHex()}");
            }
        }
    }

    [Theory]
    [InlineData(ColorEngineKind.Oklch)]
    [InlineData(ColorEngineKind.Hct)]
    public void BothEngines_TakeColoursApartAndPutThemBack(ColorEngineKind kind)
    {
        var engine = ColorEngines.Get(kind);
        foreach (var hex in new[] { "#2F81F7", "#D73A49", "#28A745", "#F6C343", "#6F42C1", "#808080", "#0D1117", "#FAFBFC" })
        {
            var color = Hex(hex);
            var parts = engine.Decompose(color);
            var back = engine.Compose(parts.Hue, parts.Chroma, parts.Tone);

            Assert.InRange(Math.Abs(back.R - color.R), 0, 1);
            Assert.InRange(Math.Abs(back.G - color.G), 0, 1);
            Assert.InRange(Math.Abs(back.B - color.B), 0, 1);
        }
    }

    [Theory]
    [InlineData(ColorEngineKind.Oklch)]
    [InlineData(ColorEngineKind.Hct)]
    public void Scales_GetDarkerAtEveryStop_ForEveryHue(ColorEngineKind kind)
    {
        var engine = ColorEngines.Get(kind);
        for (var hue = 0.0; hue < 360; hue += 20)
        {
            var scale = engine.TonalScale(hue, engine.VividChroma);
            Assert.Equal(11, scale.Count);
            var previous = double.MaxValue;
            foreach (var stop in new[] { 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950 })
            {
                var tone = engine.Decompose(scale[stop]).Tone;
                Assert.True(tone < previous, $"{kind} hue {hue}: stop {stop} tone {tone} is not darker than {previous}");
                Assert.InRange(Math.Abs(tone - engine.ToneOfStop(stop)), 0, 1.5);
                previous = tone;
            }
        }
    }

    [Theory]
    [InlineData(ColorEngineKind.Oklch, "#121212")]
    [InlineData(ColorEngineKind.Oklch, "#FFFFFF")]
    [InlineData(ColorEngineKind.Hct, "#121212")]
    [InlineData(ColorEngineKind.Hct, "#FFFFFF")]
    [InlineData(ColorEngineKind.Hct, "#F6F8FA")]
    public void SolveTone_ReachesWcagAndApcaTogether_AsCloseToTheWantedToneAsPossible(ColorEngineKind kind, string backgroundHex)
    {
        var engine = ColorEngines.Get(kind);
        var background = Hex(backgroundHex);
        var backgroundTone = engine.Decompose(background).Tone;
        for (var hue = 0.0; hue < 360; hue += 30)
        {
            // Start from the background's own tone: the worst case.
            var result = ContrastSolver.SolveTone(engine, hue, engine.VividChroma, backgroundTone, background, 4.5f, 60f);

            Assert.True(result.IsSatisfied, $"{kind} hue {hue} on {backgroundHex}");
            Assert.True(result.Ratio >= 4.5f, $"{kind} hue {hue}: ratio {result.Ratio}");
            Assert.True(MathF.Abs(result.ApcaLc) >= 60f, $"{kind} hue {hue}: Lc {result.ApcaLc}");

            // One tone nearer the background no longer reaches both.
            var nearer = result.Tone + (result.Tone > backgroundTone ? -1 : 1);
            Assert.False(ContrastSolver.Meets(engine.Compose(hue, engine.VividChroma, nearer), background, 4.5f, 60f), $"{kind} hue {hue}: tone {nearer} also passes");
        }
    }

    [Fact]
    public void SolveTone_KeepsTheWantedTone_WhenItAlreadyPasses_AndSaysSoWhenNothingCan()
    {
        var engine = ColorEngines.Hct;
        var kept = ContrastSolver.SolveTone(engine, 250, 40, 90, Hex("#121212"));
        Assert.Equal(90, kept.Tone);
        Assert.True(kept.IsSatisfied);

        var impossible = ContrastSolver.SolveTone(engine, 250, 40, 50, Hex("#777777"), minRatio: 15f);
        Assert.False(impossible.IsSatisfied);
        Assert.True(impossible.Ratio > 4f);
    }
}
