using System;
using System.Collections.Generic;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using Xunit;

namespace CSharpEditorPlugin.Tests.Settings;

public class ColorMathTests
{
    [Fact]
    public void ColorHsl_ToRgbAndHex_ProducesCorrectStandardValues()
    {
        var red = new ColorHsl(0f, 1f, 0.5f).ToRgb();
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);
        Assert.Equal("#FF0000", red.ToHex());

        var green = new ColorHsl(120f, 1f, 0.5f).ToRgb();
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);
        Assert.Equal("#00FF00", green.ToHex());

        var blue = new ColorHsl(240f, 1f, 0.5f).ToRgb();
        Assert.Equal(0, blue.R);
        Assert.Equal(0, blue.G);
        Assert.Equal(255, blue.B);
        Assert.Equal("#0000FF", blue.ToHex());
    }

    [Fact]
    public void ColorRgb_RelativeLuminance_FollowsWcagStandard()
    {
        Assert.Equal(0f, ColorRgb.Black.RelativeLuminance, 0.001f);
        Assert.Equal(1f, ColorRgb.White.RelativeLuminance, 0.001f);

        // Standard sRGB green has highest luminance weight (~0.7152)
        var green = new ColorRgb(0, 255, 0);
        Assert.True(green.RelativeLuminance > 0.7f);

        // Standard sRGB blue has lowest luminance weight (~0.0722)
        var blue = new ColorRgb(0, 0, 255);
        Assert.True(blue.RelativeLuminance < 0.1f);
    }

    [Fact]
    public void ColorRgb_ContrastRatio_CalculatesBlackOnWhiteAs21()
    {
        float ratio = ColorRgb.Black.ContrastRatio(ColorRgb.White);
        Assert.Equal(21f, ratio, 0.1f);

        float selfRatio = ColorRgb.White.ContrastRatio(ColorRgb.White);
        Assert.Equal(1f, selfRatio, 0.01f);
    }

    [Fact]
    public void TonalScaleEngine_GeneratesAll11Stops_WithDecreasingLightness()
    {
        var scale = TonalScaleEngine.GenerateScale(210f, 0.8f);

        Assert.Equal(11, scale.Count);
        foreach (int stop in TonalScaleEngine.StandardStops)
        {
            Assert.True(scale.ContainsKey(stop));
        }

        // Verify stops 50 to 950 are strictly monotonic in decreasing luminance
        float prevLuminance = float.MaxValue;
        foreach (int stop in TonalScaleEngine.StandardStops)
        {
            float lum = scale[stop].RelativeLuminance;
            Assert.True(lum <= prevLuminance, $"Stop {stop} luminance {lum} is greater than previous stop {prevLuminance}");
            prevLuminance = lum;
        }
    }

    [Fact]
    public void ContrastSolver_PickStop_FindsCompliantStop()
    {
        var scale = TonalScaleEngine.GenerateScale(210f, 0.8f);
        var white = ColorRgb.White;

        // On white, dark stops (600, 700, 800, 900) should satisfy AA 4.5:1
        int[] candidates = [500, 600, 700, 800, 900];
        var result = ContrastSolver.PickStop(scale, candidates, white, minRatio: 4.5f);

        Assert.True(result.IsSatisfied);
        Assert.True(result.Ratio >= 4.5f);
        Assert.True(result.Stop >= 500);
    }

    [Fact]
    public void ColorBlindnessEngine_Simulate_PreservesAchromatopsiaGrayLevels()
    {
        var original = new ColorRgb(255, 0, 0);
        var sim = ColorBlindnessEngine.Simulate(original, VisionDeficiency.Achromatopsia);

        Assert.Equal(sim.R, sim.G);
        Assert.Equal(sim.G, sim.B);
    }

    [Fact]
    public void ColorBlindnessEngine_DeltaE_MeasuresPerceptualDistance()
    {
        var c1 = new ColorRgb(255, 0, 0);
        var c2 = new ColorRgb(255, 0, 0);
        Assert.Equal(0f, ColorBlindnessEngine.DeltaE(c1, c2), 0.001f);

        var c3 = new ColorRgb(0, 255, 0);
        float delta = ColorBlindnessEngine.DeltaE(c1, c3);
        Assert.True(delta > 50f, $"Expected large Delta E between pure red and green, got {delta}");
    }

    [Fact]
    public void ThemeExportService_ExportsValidFormats()
    {
        var theme = new ThemeDefinition
        {
            Id = "test-harmonic",
            Name = "Test Harmonic Theme",
            Description = "Unit test theme",
            IsDark = true,
            Colors = new()
            {
                ["DsBgBrush"] = "#0D1117",
                ["DsPrimaryBrush"] = "#2F81F7",
                ["DsTextBrush"] = "#C9D1D9"
            }
        };

        string xaml = ThemeExportService.ExportToAvaloniaXaml(theme);
        Assert.Contains("<ResourceDictionary", xaml);
        Assert.Contains("x:Key=\"DsBgBrush\"", xaml);
        Assert.Contains("Color=\"#0D1117\"", xaml);

        string css = ThemeExportService.ExportToCssVariables(theme, theme);
        Assert.Contains(":root", css);
        Assert.Contains("--dsbg: #0D1117;", css);

        string dtcg = ThemeExportService.ExportToDtcgJson(theme);
        Assert.Contains("\"$value\": \"#0D1117\"", dtcg);
        Assert.Contains("\"$type\": \"color\"", dtcg);

        string tw = ThemeExportService.ExportToTailwindV4(theme);
        Assert.Contains("@theme", tw);
        Assert.Contains("--color-dsbg: #0D1117;", tw);
    }

    [Fact]
    public void ColorBlindnessEngine_Simulate_TransformsAcrossAllDeficiencies()
    {
        var red = new ColorRgb(220, 38, 38);
        var green = new ColorRgb(22, 163, 74);
        var blue = new ColorRgb(37, 99, 235);

        // Normal preserves identity
        Assert.Equal(red, ColorBlindnessEngine.Simulate(red, VisionDeficiency.Normal));

        // Protanopia shifts red significantly
        var protanRed = ColorBlindnessEngine.Simulate(red, VisionDeficiency.Protanopia);
        Assert.NotEqual(red.R, protanRed.R);

        // Deuteranopia shifts green significantly
        var deuterGreen = ColorBlindnessEngine.Simulate(green, VisionDeficiency.Deuteranopia);
        Assert.NotEqual(green.G, deuterGreen.G);

        // Tritanopia shifts blue significantly
        var tritanBlue = ColorBlindnessEngine.Simulate(blue, VisionDeficiency.Tritanopia);
        Assert.NotEqual(blue.B, tritanBlue.B);
    }

    [Fact]
    public void ColorBlindnessEngine_IsDistinct_DetectsRedGreenConfusionInDeuteranopia()
    {
        var red = new ColorRgb(200, 40, 40);
        var green = new ColorRgb(40, 160, 40);

        // Under normal vision, red and green are unmistakably distinct
        Assert.True(ColorBlindnessEngine.IsDistinct(red, green, VisionDeficiency.Normal));

        // Under Deuteranopia, both project to similar yellowish hues, with very low distinction
        float deuterDelta = ColorBlindnessEngine.DeltaE(
            ColorBlindnessEngine.Simulate(red, VisionDeficiency.Deuteranopia),
            ColorBlindnessEngine.Simulate(green, VisionDeficiency.Deuteranopia));

        Assert.True(deuterDelta < 35f, $"Expected compressed delta for red/green in Deuteranopia, got {deuterDelta}");
    }

    [Fact]
    public void ApcaEngine_ContrastLc_CalculatesPolarityAndMagnitude()
    {
        var black = ColorRgb.Black;
        var white = ColorRgb.White;

        // Dark text on light background produces positive Lc
        float darkOnLight = ApcaEngine.ContrastLc(black, white);
        Assert.True(darkOnLight > 100f, $"Expected Lc > 100 for black on white, got {darkOnLight}");

        // Light text on dark background produces negative Lc
        float lightOnDark = ApcaEngine.ContrastLc(white, black);
        Assert.True(lightOnDark < -100f, $"Expected Lc < -100 for white on black, got {lightOnDark}");

        // Same color yields 0 contrast
        float sameColor = ApcaEngine.ContrastLc(black, black);
        Assert.Equal(0f, sameColor, 0.01f);
    }

    [Fact]
    public void ApcaEngine_Ratings_FollowWcag3Guidelines()
    {
        Assert.Equal("Preferred Body (14px+)", ApcaEngine.GetRating(95f));
        Assert.Equal("Standard Body (16px+)", ApcaEngine.GetRating(78f));
        Assert.Equal("Large Text (24px / 18px Bold)", ApcaEngine.GetRating(62f));
        Assert.Equal("Sub-Headings & Non-Text UI", ApcaEngine.GetRating(50f));
        Assert.Equal("Placeholder / Decorative", ApcaEngine.GetRating(35f));
        Assert.Equal("Fail (Insufficient Contrast)", ApcaEngine.GetRating(15f));

        Assert.True(ApcaEngine.IsBodyTextCompliant(80f));
        Assert.False(ApcaEngine.IsBodyTextCompliant(65f));
        Assert.True(ApcaEngine.IsLargeTextCompliant(65f));
    }
}
