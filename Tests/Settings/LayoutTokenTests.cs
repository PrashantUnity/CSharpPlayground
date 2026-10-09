using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The layout design tokens: the default layout is the studio exactly as it was drawn before it had tokens, every lever
/// moves its tokens the right way, keys on controls follow layout changes, and Density finally changes sizes.
/// </summary>
[Collection("SettingsTests")]
public class LayoutTokenTests : IDisposable
{
    public void Dispose()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyLayout(LayoutSpec.Default);
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
    }

    private static IReadOnlyDictionary<string, object> Map(LayoutSpec spec, bool dark = true) => LayoutTokenMapper.Map(spec, dark);

    private static string RepositoryFile(string relative)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.csproj"))) folder = folder.Parent;
        Assert.NotNull(folder);
        return Path.Combine(folder!.FullName, relative);
    }

    [Fact]
    public void TheGeneratedTokenFile_IsInStepWithTheMapper()
    {
        // If this fails: dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll layout-tokens
        var onDisk = File.ReadAllText(RepositoryFile(LayoutTokenXaml.RelativePath)).Replace("\r\n", "\n");
        Assert.Equal(LayoutTokenXaml.Generate(), onDisk);

        // A code font keeps all its fallbacks: written as its first name only, it fell back to a proportional font on
        // every machine without Cascadia Code, and notebook cells lost their monospace.
        Assert.Contains($"<FontFamily x:Key=\"DsCodeFontFamily\">{LayoutTokenMapper.DefaultCodeFont}</FontFamily>", onDisk);
    }

    [Fact]
    public void TheDefaultLayout_IsTheStudiosOriginalSizes()
    {
        var t = Map(LayoutSpec.Default);

        Assert.Equal(11.0, t["DsFontSize200"]);
        Assert.Equal(12.0, t["DsFontSize300"]);
        Assert.Equal(26.0, t["DsFontSize800"]);
        Assert.Equal(FontWeight.Bold, t["DsWeightStrong"]);
        Assert.Equal(FontWeight.SemiBold, t["DsWeightEmphasis"]);
        Assert.Equal(0.8, t["DsTracking080"]);
        Assert.Equal(new CornerRadius(6), t["DsRadiusMD"]);
        Assert.Equal(new CornerRadius(6, 6, 0, 0), t["DsRadiusMDTop"]);
        Assert.Equal(new CornerRadius(10), t["DsRadiusCard"]);
        Assert.Equal(new Thickness(0, 0, 0, 1), t["DsBorderBottom"]);
        Assert.Equal(new Thickness(1), t["DsCardBorder"]);
        Assert.Equal(new Thickness(18, 16, 18, 16), t["DsCardPadding"]);
        Assert.Equal(32.0, t["DsControlHeight300"]);
        Assert.Equal(16.0, t["DsSpace16"]);
        Assert.Equal(FontFamily.Default, t["DsUiFontFamily"]);
        Assert.Equal(BoxShadows.Parse("0 1 3 0 #14000000, 0 1 2 0 #1E000000"), t["M3ElevationLevel1"]);
        Assert.Equal(BoxShadows.Parse("0 24 64 0 #B0000000"), t["DsModalShadow"]);
        Assert.Equal(BoxShadows.Parse("0 24 64 0 #40000000"), Map(LayoutSpec.Default, dark: false)["DsModalShadow"]);
    }

    [Fact]
    public void TheTypeRamp_StaysInOrder_ForEveryBaseSizeAndHierarchy()
    {
        for (var baseSize = 10.0; baseSize <= 16; baseSize += 0.5)
        for (var hierarchy = 0.85; hierarchy <= 1.26; hierarchy += 0.05)
        {
            var t = Map(LayoutSpec.Default with { BaseFontSize = baseSize, Hierarchy = hierarchy });
            var sizes = LayoutTokenMapper.TypeRamp.Select(step => (double)t["DsFontSize" + step.Step]).ToList();
            for (var i = 1; i < sizes.Count; i++) Assert.True(sizes[i] >= sizes[i - 1], $"base {baseSize} hierarchy {hierarchy:0.00}: step {i}");
            Assert.InRange(Math.Abs(sizes[6] - baseSize), 0, 0.25); // body text is the base size
        }

        var bigger = Map(LayoutSpec.Default with { BaseFontSize = 14 });
        Assert.True((double)bigger["DsFontSize200"] > 11);
        var bolder = Map(LayoutSpec.Default with { Hierarchy = 1.25 });
        Assert.True((double)bolder["DsFontSize800"] > 26 && (double)bolder["DsFontSize050"] < 8.5);
    }

    [Fact]
    public void RadiusZero_SquaresEverything_ButPillsStayRound_AndOverridesWin()
    {
        var t = Map((LayoutSpec.Default with { RadiusScale = 0 }).WithRadiusOverride(LayoutComponents.Dialog, 20));

        foreach (var (step, _) in LayoutTokenMapper.RadiusSteps) Assert.Equal(new CornerRadius(0), t["DsRadius" + step]);
        Assert.Equal(new CornerRadius(0), t["DsRadiusCard"]);
        Assert.Equal(new CornerRadius(9999), t["DsRadiusFull"]);
        Assert.Equal(new CornerRadius(20), t["DsRadiusDialog"]);

        var round = Map(LayoutSpec.Default with { RadiusScale = 2 });
        Assert.Equal(new CornerRadius(12), round["DsRadiusMD"]);
    }

    [Fact]
    public void BordersAndCardsFollowTheirLevers()
    {
        var t = Map(LayoutSpec.Default with { BorderWidth = 2, CardBorders = false, Dividers = false, AccentBarWidth = 4 });

        Assert.Equal(new Thickness(2), t["DsBorderThin"]);
        Assert.Equal(new Thickness(0), t["DsCardBorder"]);
        Assert.Equal(new Thickness(0), t["DsBorderBottom"]);
        Assert.Equal(new Thickness(4, 0, 0, 0), t["DsBorderAccentLeft"]);
    }

    [Fact]
    public void ShadowStrengthZero_RemovesEveryShadow_AndCardsCanBeLifted()
    {
        var none = Map(LayoutSpec.Default with { ShadowStrength = 0 });
        foreach (var (key, value) in none.Where(kv => kv.Value is BoxShadows))
        {
            var shadows = (BoxShadows)value;
            for (var i = 0; i < shadows.Count; i++) Assert.True(shadows[i].Color.A == 0, $"{key} still casts a shadow");
        }

        var lifted = Map(LayoutSpec.Default with { CardElevation = 2 });
        Assert.Equal(lifted["DsShadowLevel2"], lifted["DsCardShadow"]);
        Assert.Equal(Map(LayoutSpec.Default)["DsShadowLevel0"], Map(LayoutSpec.Default)["DsCardShadow"]);
    }

    [Fact]
    public void Density_ScalesSpacingAndHeights_ButNotTheActivityBar()
    {
        double Height(LayoutDensity d) => (double)Map(LayoutSpec.Default with { Density = d })["DsControlHeight300"];
        double Tab(LayoutDensity d) => (double)Map(LayoutSpec.Default with { Density = d })["DsTabHeight"];
        double PaddingX(LayoutDensity d) => ((Thickness)Map(LayoutSpec.Default with { Density = d })["DsCardPadding"]).Left;

        Assert.True(Height(LayoutDensity.Compact) < Height(LayoutDensity.Comfortable) && Height(LayoutDensity.Comfortable) < Height(LayoutDensity.Spacious));
        Assert.True(Tab(LayoutDensity.Compact) < Tab(LayoutDensity.Spacious));
        Assert.True(PaddingX(LayoutDensity.Compact) < PaddingX(LayoutDensity.Spacious));
        foreach (var d in Enum.GetValues<LayoutDensity>()) Assert.Equal(48.0, Map(LayoutSpec.Default with { Density = d })["DensityRailWidth"]);
    }

    [Fact]
    public void EveryPreset_MapsEveryToken_AndRoundTripsThroughJson()
    {
        var keys = Map(LayoutSpec.Default).Keys.ToHashSet();
        foreach (var preset in LayoutPresets.All)
        {
            var t = Map(preset.Spec);
            Assert.True(keys.SetEquals(t.Keys), preset.Name);
            var back = LayoutSpec.FromJson(preset.Spec.ToJson());
            Assert.NotNull(back);
            Assert.True(back!.SameLayoutAs(preset.Spec), preset.Name);
        }

        var custom = LayoutSpec.Default.WithRadiusOverride(LayoutComponents.Card, 3).WithSizeOverride("TabHeight", 40);
        Assert.True(LayoutSpec.FromJson(custom.ToJson())!.SameLayoutAs(custom));
        Assert.False(custom.SameLayoutAs(LayoutSpec.Default));
        Assert.Null(LayoutSpec.FromJson(null));
    }

    [Fact]
    public void AKeyOnAControl_GetsTheTokensValue_AndFollowsTheLayout()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyLayout(LayoutSpec.Default);
        var text = new TextBlock();
        var border = new Border();
        var tab = new Border();
        Tokens.SetFontSize(text, "DsFontSize200");
        Tokens.SetCornerRadius(border, "DsRadiusMD");
        Tokens.SetBorderThickness(tab, "=0,2,1,0");

        Assert.Equal(11, text.FontSize);
        Assert.Equal(new CornerRadius(6), border.CornerRadius);
        Assert.Equal(new Thickness(0, 2, 1, 0), tab.BorderThickness);

        engine.ApplyLayout(LayoutSpec.Default with { BaseFontSize = 14, RadiusScale = 0 });
        Assert.Equal(12.75, text.FontSize);
        Assert.Equal(new CornerRadius(0), border.CornerRadius);
        Assert.Equal(new Thickness(0, 2, 1, 0), tab.BorderThickness);

        Tokens.SetCornerRadius(border, null);
        Assert.Equal(default, border.CornerRadius);
        engine.ApplyLayout(LayoutSpec.Default);
        Assert.Equal(default, border.CornerRadius);
        Assert.Equal(11, text.FontSize);
    }

    [Fact]
    public void TheDensityButtons_NowChangeSizes()
    {
        // Density used to write tokens nothing read, so the Compact/Comfortable/Spacious buttons changed nothing.
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyLayout(LayoutSpec.Default);
        var button = new Button();
        Tokens.SetHeight(button, "DsControlHeight300");
        Assert.Equal(32, button.Height);

        engine.SetDensity(LayoutDensity.Compact);
        Assert.Equal(LayoutDensity.Compact, engine.Layout.Density);
        Assert.True(button.Height < 32, $"compact height {button.Height}");

        engine.SetDensity(LayoutDensity.Spacious);
        Assert.True(button.Height > 32, $"spacious height {button.Height}");
        engine.SetDensity(LayoutDensity.Comfortable);
        Assert.Equal(32, button.Height);
    }

    [Fact]
    public void AColourThemeSwitch_KeepsTheLayout_AndALayoutChangeIsOneLayerSwap()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var square = LayoutSpec.Default with { RadiusScale = 0 };
        var border = new Border();
        Tokens.SetCornerRadius(border, "DsRadiusLG");

        var commits = engine.LayerCommits;
        engine.ApplyLayout(square);
        Assert.Equal(commits + 1, engine.LayerCommits);
        engine.ApplyTheme("dracula");

        Assert.Same(square, engine.Layout);
        Assert.Equal(new CornerRadius(0), border.CornerRadius);
        Assert.Equal(new CornerRadius(0), engine.GetResource("DsRadiusLG"));
    }

    [Fact]
    public void Scripts_CanApplyAndReadLayouts()
    {
        IThemeApi themes = StudioAppContext.Instance.ThemeEngine;
        var material = LayoutPresets.Get("layout-material")!.Spec;

        Assert.True(themes.ApplyLayout(material.ToJson().GetRawText()));
        Assert.Equal(13.0, themes.GetLayoutToken("DsFontSize300"));
        Assert.True(LayoutSpec.FromJson(System.Text.Json.JsonDocument.Parse(themes.GetLayoutJson()).RootElement)!.SameLayoutAs(material));

        // The design tokens Settings exports work too (the layout rides along in $extensions).
        var exported = "{ \"$extensions\": { \"com.frypdf.layout\": " + LayoutSpec.Default.ToJson().GetRawText() + " } }";
        Assert.True(themes.ApplyLayout(exported));
        Assert.Equal(12.0, themes.GetLayoutToken("DsFontSize300"));
        Assert.False(themes.ApplyLayout("{ \"not\": 1 }"));
        Assert.Null(LayoutSpec.FromJson(System.Text.Json.JsonDocument.Parse("{ \"BaseFontSize\": 30 }").RootElement));
        Assert.Null(themes.GetLayoutToken("NoSuchToken"));
    }
}
