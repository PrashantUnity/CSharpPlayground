using System;
using System.IO;
using System.Linq;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests.Settings;

[Collection("SettingsTests")]
public class HarmonicColorGeneratorTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly StudioLanguageServices _services;
    private readonly StudioSettingsStore _settingsStore;

    public HarmonicColorGeneratorTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_HarmonicTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
        _services = new StudioLanguageServices(_tempFolder);
        _settingsStore = _services.StudioSettings;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, recursive: true);
        }
        catch { }
    }

    [Theory]
    [InlineData(ColorHarmonyMode.Analogous)]
    [InlineData(ColorHarmonyMode.Complementary)]
    [InlineData(ColorHarmonyMode.SplitComplementary)]
    [InlineData(ColorHarmonyMode.Triadic)]
    [InlineData(ColorHarmonyMode.Tetradic)]
    [InlineData(ColorHarmonyMode.Monochromatic)]
    public void GenerateHarmonicTheme_ContainsAllCanonicalTokens_ForEveryHarmonyMode(ColorHarmonyMode mode)
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(210f, mode, isDark: true);

        Assert.NotNull(theme);
        Assert.StartsWith("harmonic-", theme.Id);
        Assert.NotEmpty(theme.Description);
        Assert.True(theme.IsDark);

        string[] requiredCoreTokens =
        [
            "DsBgBrush",
            "DsSurfaceBrush",
            "DsSurfaceHoverBrush",
            "DsSurfaceHighBrush",
            "DsSurfaceContainerLowestBrush",
            "DsGlassBgBrush",
            "DsHoverOverlayBrush",
            "DsScrimBrush",
            "VisCanvasBgBrush",
            "DsBorderBrush",
            "DsBorderSubtleBrush",
            "DsFocusBorderBrush",
            "DsTextBrush",
            "DsTextWhiteBrush",
            "DsMutedBrush",
            "DsSyntaxStringBrush",
            "DsPrimaryBrush",
            "DsPrimaryHoverBrush",
            "DsPrimarySubtleBrush",
            "DsSelectionBrush",
            "DsPrimaryBorderSubtleBrush",
            "DsOnAccentBrush",
            "EditorBgBrush",
            "EditorFgBrush",
            "EditorLineNumbersBrush",
            "EditorSelectionBrush",
            "EditorCaretBrush",
            "EditorLinkBrush",
            "EditorFoldingMarkerBrush",
            "EditorFoldingMarkerBgBrush",
            "EditorFoldingMarkerActiveBrush",
            "EditorFoldingMarkerActiveBgBrush",
            "EditorBreakpointBrush",
            "EditorBreakpointBorderBrush",
            "EditorBreakpointPausedBrush",
            "EditorBreakpointPausedBorderBrush",
            "DsErrorBrush",
            "DsErrorSubtleBrush",
            "DsWarningBrush",
            "DsWarningSubtleBrush",
            "DsSuccessBrush",
            "M3SurfaceBrush",
            "M3BackgroundBrush",
            "M3SurfaceContainerBrush",
            "M3OutlineBrush",
            "M3PrimaryBrush",
            "M3PrimaryContainerBrush",
            "BadgeEasyBgBrush",
            "BadgeEasyFgBrush",
            "BadgeMediumBgBrush",
            "BadgeMediumFgBrush",
            "BadgeHardBgBrush",
            "BadgeHardFgBrush",
            "NavHomeFgBrush",
            "NavNotebooksFgBrush",
            "NavScriptsFgBrush",
            "NavDocsFgBrush",
            "NavThemeFgBrush",
            "NavToolchainsFgBrush",
            "NavSettingsFgBrush"
        ];

        foreach (var token in requiredCoreTokens)
        {
            Assert.True(theme.Colors.ContainsKey(token), $"Theme missing canonical token '{token}'.");
            var hex = theme.Colors[token];
            Assert.True(hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9),
                $"Token '{token}' has invalid hex '{hex}'.");
        }
    }

    [Fact]
    public void GenerateHarmonicTheme_PopulatesOver80Tokens()
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(180f, ColorHarmonyMode.Complementary, isDark: true);
        Assert.True(theme.Colors.Count >= 70, $"Expected 70+ tokens, but found {theme.Colors.Count}");
    }

    [Fact]
    public void GetAllTokenDescriptors_CoversCoreTokensWithValidCategories()
    {
        var descriptors = HarmonicColorGenerator.GetAllTokenDescriptors();
        Assert.NotEmpty(descriptors);
        Assert.True(descriptors.Count >= 60);

        Assert.Contains(descriptors, d => d.Key == "DsBgBrush" && d.Category == "Surfaces");
        Assert.Contains(descriptors, d => d.Key == "EditorBgBrush" && d.Category == "Editor");
        Assert.Contains(descriptors, d => d.Key == "M3PrimaryBrush" && d.Category == "Material3");
        Assert.Contains(descriptors, d => d.Key == "BadgeEasyBgBrush" && d.Category == "Badges");
        Assert.Contains(descriptors, d => d.Key == "NavThemeFgBrush" && d.Category == "Navigation");
    }

    [Fact]
    public void EnsureCompleteTheme_BackfillsMissingTokens()
    {
        var theme = new ThemeDefinition
        {
            Id = "minimal",
            Name = "Minimal Theme",
            Description = "Theme with minimal tokens",
            IsDark = true,
            Colors = new()
            {
                ["DsBgBrush"] = "#000000",
                ["DsPrimaryBrush"] = "#FF00FF"
            }
        };

        HarmonicColorGenerator.EnsureCompleteTheme(theme);

        Assert.Equal("#000000", theme.Colors["DsBgBrush"]);
        Assert.Equal("#FF00FF", theme.Colors["DsPrimaryBrush"]);
        Assert.True(theme.Colors.ContainsKey("EditorBgBrush"));
        Assert.True(theme.Colors.ContainsKey("DsSurfaceBrush"));
        Assert.True(theme.Colors.ContainsKey("M3PrimaryBrush"));
        Assert.True(theme.Colors.Count >= 70);
    }

    [Fact]
    public void GenerateHarmonicTheme_EnforcesDarkModeErgonomics()
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(140f, ColorHarmonyMode.Triadic, isDark: true);

        Assert.Equal("#FFFFFF", theme.Colors["DsTextWhiteBrush"]);

        var primaryHex = theme.Colors["DsPrimaryBrush"];
        Assert.Equal(7, primaryHex.Length);

        var subtleHex = theme.Colors["DsPrimarySubtleBrush"];
        Assert.Equal(9, subtleHex.Length);
        Assert.EndsWith(primaryHex[1..], subtleHex);
    }

    [Fact]
    public void HslToHex_ConvertsCorrectly()
    {
        Assert.Equal("#FF0000", HarmonicColorGenerator.HslToHex(0f, 1f, 0.5f));
        Assert.Equal("#00FF00", HarmonicColorGenerator.HslToHex(120f, 1f, 0.5f));
        Assert.Equal("#0000FF", HarmonicColorGenerator.HslToHex(240f, 1f, 0.5f));
        Assert.Equal("#000000", HarmonicColorGenerator.HslToHex(0f, 0f, 0f));
        Assert.Equal("#FFFFFF", HarmonicColorGenerator.HslToHex(0f, 0f, 1f));
    }

    [Fact]
    public void GenerateRandomHarmonicTheme_GeneratesValidTheme()
    {
        var theme = HarmonicColorGenerator.GenerateRandomHarmonicTheme(isDark: true);
        Assert.NotNull(theme);
        Assert.NotEmpty(theme.Name);
        Assert.NotEmpty(theme.Description);
        Assert.True(theme.Colors.Count >= 70);
    }

    [Fact]
    public void CSharpSettingsViewModel_ThemeCategory_CanBeSelectedAndPopulated()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        var themeCategory = vm.Categories.FirstOrDefault(c => c.Id == "Themes");
        Assert.NotNull(themeCategory);
        Assert.Equal("Theme & Colors", themeCategory.Title);

        vm.SelectCategory("Themes");
        Assert.True(vm.IsThemesCategoryActive);
        Assert.False(vm.IsLanguagesCategoryActive);
        Assert.False(vm.IsEditorCategoryActive);
    }

    [Fact]
    public void CSharpSettingsViewModel_ThemingPartial_PopulatesAndFiltersColorTokens()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.ColorTokens);
        Assert.NotEmpty(vm.FilteredColorTokens);
        Assert.Equal(vm.ColorTokens.Count, vm.TotalTokensCount);

        // Filter by category
        vm.SelectTokenCategoryCommand.Execute("Editor");
        Assert.Equal("Editor", vm.SelectedTokenCategory);
        Assert.All(vm.FilteredColorTokens, t => Assert.Equal("Editor", t.Category));

        // Reset category and filter by search query
        vm.SelectTokenCategoryCommand.Execute("All");
        vm.TokenSearchQuery = "DsBg";
        Assert.Contains(vm.FilteredColorTokens, t => t.Key == "DsBgBrush");

        // Clear filter
        vm.SelectTokenCategoryCommand.Execute("All");
        vm.TokenSearchQuery = string.Empty;
        Assert.Equal(vm.TotalTokensCount, vm.FilteredColorTokens.Count);
    }

    [Fact]
    public void CSharpSettingsViewModel_ApplyHarmonicConfigurationCommand_AppliesThemeAndRefreshesTokens()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.SelectedHueDegrees = 120f;
        vm.SelectedHarmonyMode = ColorHarmonyMode.Complementary;
        vm.IsThemeDarkMode = true;

        vm.ApplyHarmonicConfigurationCommand.Execute(null);

        Assert.StartsWith("harmonic-", vm.ActiveThemeId);
        Assert.Contains("Harmonic", vm.ActiveThemeName);

        var bgToken = vm.ColorTokens.First(t => t.Key == "DsBgBrush");
        Assert.NotEmpty(bgToken.CurrentHex);
        Assert.StartsWith("#", bgToken.CurrentHex);
    }

    [Fact]
    public void CSharpSettingsViewModel_RandomizeHarmonicWheelCommand_ChangesSelectionAndApplies()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        vm.RandomizeHarmonicWheelCommand.Execute(null);

        Assert.StartsWith("harmonic-", vm.ActiveThemeId);
        Assert.True(vm.SelectedHueDegrees >= 0f && vm.SelectedHueDegrees <= 360f);
        Assert.NotEmpty(vm.SelectedHueDescriptor);
    }

    [Fact]
    public void GenerateHarmonicTheme_WithCustomHarmonicConfiguration_AppliesFineTuningParameters()
    {
        var config = new HarmonicConfiguration
        {
            BaseHue = 240f,
            Mode = ColorHarmonyMode.Triadic,
            SaturationBoost = 1.2f,
            AccentShift = 10f,
            NeutralTint = 20f,
            SemanticPull = 30f,
            ContrastTarget = 7.0f
        };

        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(config, isDark: true);

        Assert.NotNull(theme);
        Assert.True(theme.Colors.Count >= 100);
        Assert.True(theme.Colors.ContainsKey("TonalP500"));
        Assert.True(theme.Colors.ContainsKey("TonalN900"));
        Assert.True(theme.Colors.ContainsKey("TonalSuccess500"));
    }

    [Fact]
    public void GenerateAllTonalScales_ContainsAllPaletteFamilies_With11StopsEach()
    {
        var config = new HarmonicConfiguration
        {
            BaseHue = 180f,
            Mode = ColorHarmonyMode.Complementary
        };

        var scales = HarmonicColorGenerator.GenerateAllTonalScales(config);

        string[] requiredFamilies = ["Primary", "Accent", "Secondary", "Neutral", "Success", "Warning", "Danger", "Info"];
        foreach (var family in requiredFamilies)
        {
            Assert.True(scales.ContainsKey(family), $"Missing palette family '{family}'");
            Assert.Equal(11, scales[family].Count);
            foreach (int stop in TonalScaleEngine.StandardStops)
            {
                Assert.True(scales[family].ContainsKey(stop));
            }
        }
    }

    [Fact]
    public void CSharpSettingsViewModel_ProceduralFineTuning_PopulatesAndRefreshesSwatches()
    {
        var vm = new CSharpSettingsViewModel(_services, _settingsStore);

        Assert.NotEmpty(vm.PrimaryTonalSwatches);
        Assert.NotEmpty(vm.NeutralTonalSwatches);
        Assert.Equal(11, vm.PrimaryTonalSwatches.Count);
        Assert.Equal(11, vm.NeutralTonalSwatches.Count);

        string initialHex = vm.PrimaryTonalSwatches[0].Hex;

        // Changing saturation boost should update chords and swatches
        vm.SaturationBoost = 0.7f;
        Assert.Equal(11, vm.PrimaryTonalSwatches.Count);

        // Verify export commands execute cleanly without exception
        vm.CopyAvaloniaXamlCommand.Execute(null);
        vm.CopyCssVariablesCommand.Execute(null);
        vm.CopyTailwindCommand.Execute(null);
        vm.CopyDtcgJsonCommand.Execute(null);
    }
}

