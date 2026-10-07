using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The palette studio in Settings: lock what you like and Generate the rest, regenerate or set one section, undo and
/// redo, switch engines, and come back after a restart (or to a saved theme) to the same palette, locks included.
/// </summary>
[Collection("SettingsTests")]
public class PaletteStudioViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FryPDF_PaletteStudio_" + Guid.NewGuid().ToString("N"));

    public PaletteStudioViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        StudioAppContext.Instance.ThemeEngine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private CSharpSettingsViewModel NewSettings(out StudioLanguageServices services)
    {
        services = new StudioLanguageServices(_folder);
        return new CSharpSettingsViewModel(services, services.StudioSettings);
    }

    private static PaletteSectionItemViewModel Row(CSharpSettingsViewModel vm, string id) =>
        vm.CoreSections.Concat(vm.SyntaxSections).Concat(vm.ChartSections).Single(r => r.Id == id);

    [Fact]
    public void TheSections_AreListed_WithTheirColoursAndContrast()
    {
        var vm = NewSettings(out _);

        Assert.Equal(PaletteSections.Core, vm.CoreSections.Select(r => r.Id));
        Assert.Equal(PaletteSections.Syntax, vm.SyntaxSections.Select(r => r.Id));
        Assert.Equal(PaletteSections.Charts, vm.ChartSections.Select(r => r.Id));
        Assert.All(vm.CoreSections.Concat(vm.SyntaxSections).Concat(vm.ChartSections), row =>
        {
            Assert.Equal(vm.CurrentPalette.Keys[row.Id].ToHex(), row.Hex);
            Assert.Equal(row.Hex, row.EditHex);
            Assert.NotEmpty(row.ContrastLabel);
        });
        Assert.All(vm.SyntaxSections.Concat(vm.ChartSections), row => Assert.True(row.PassesContrast, row.Id));
    }

    [Fact]
    public void Generate_ChangesWhatIsntLocked_AndUndoRedoStepThroughIt()
    {
        var vm = NewSettings(out _);
        vm.ToggleSectionLock(Row(vm, PaletteSections.Primary));
        vm.ToggleSectionLock(Row(vm, PaletteSections.SyntaxSection("String")));
        var primary = Row(vm, PaletteSections.Primary).Hex;
        var str = Row(vm, PaletteSections.SyntaxSection("String")).Hex;
        var warning = Row(vm, PaletteSections.Warning).Hex;
        Assert.True(Row(vm, PaletteSections.Primary).IsLocked);

        var seen = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            vm.GeneratePalette();
            Assert.Equal(primary, Row(vm, PaletteSections.Primary).Hex);
            Assert.Equal(str, Row(vm, PaletteSections.SyntaxSection("String")).Hex);
            seen.Add(Row(vm, PaletteSections.Chart(2)).Hex);
        }

        Assert.True(seen.Count > 1);
        Assert.True(vm.CanUndoPalette);
        Assert.False(vm.CanRedoPalette);

        var afterFive = Row(vm, PaletteSections.Chart(2)).Hex;
        vm.UndoPalette();
        Assert.True(vm.CanRedoPalette);
        vm.RedoPalette();
        Assert.Equal(afterFive, Row(vm, PaletteSections.Chart(2)).Hex);

        // All the way back: the two locks too.
        while (vm.CanUndoPalette) vm.UndoPalette();
        Assert.Equal(warning, Row(vm, PaletteSections.Warning).Hex);
        Assert.False(Row(vm, PaletteSections.Primary).IsLocked);
    }

    [Fact]
    public void TypingAHex_SetsAndLocksThatSection_AndRegenerateChangesOnlyOne()
    {
        var vm = NewSettings(out _);
        var error = Row(vm, PaletteSections.Error);
        error.EditHex = "c0392b";
        vm.SetSectionColor(error);

        Assert.Equal("#C0392B", Row(vm, PaletteSections.Error).Hex);
        Assert.True(Row(vm, PaletteSections.Error).IsLocked);

        var before = vm.CoreSections.Concat(vm.ChartSections).ToDictionary(r => r.Id, r => r.Hex);
        vm.RegenerateSection(Row(vm, PaletteSections.Chart(4)));
        var after = vm.CoreSections.Concat(vm.ChartSections).ToDictionary(r => r.Id, r => r.Hex);
        Assert.Single(before, kv => after[kv.Key] != kv.Value);
        Assert.NotEqual(before[PaletteSections.Chart(4)], after[PaletteSections.Chart(4)]);

        var bad = Row(vm, PaletteSections.Info);
        bad.EditHex = "not a colour";
        vm.SetSectionColor(bad);
        Assert.False(Row(vm, PaletteSections.Info).IsLocked);
        Assert.Equal(Row(vm, PaletteSections.Info).Hex, Row(vm, PaletteSections.Info).EditHex);
    }

    [Fact]
    public void SwitchingTheEngine_RebuildsThePalette_AndApplyUsesIt()
    {
        var vm = NewSettings(out _);
        var oklchPrimary = Row(vm, PaletteSections.Primary).Hex;

        vm.SelectedColorEngineOption = vm.AvailableColorEngines.Single(o => o.Kind == ColorEngineKind.Hct);

        Assert.Equal(ColorEngineKind.Hct, vm.PaletteSpec.Engine);
        Assert.NotEqual(oklchPrimary, Row(vm, PaletteSections.Primary).Hex);
        vm.ApplyHarmonicConfiguration();
        var theme = StudioAppContext.Instance.ThemeEngine.GetTheme(StudioAppContext.Instance.ThemeEngine.ActiveThemeId)!;
        Assert.Contains("HCT", theme.Description);
        Assert.Equal(Row(vm, PaletteSections.SyntaxSection("Keyword")).Hex, theme.Colors["SyntaxKeywordBrush"]);
        Assert.True(vm.CanUndoPalette);
    }

    [Fact]
    public void ThePalette_ItsLocksAndEngine_AreThereAfterARestart()
    {
        var vm = NewSettings(out var services);
        vm.SelectedColorEngine = ColorEngineKind.Hct;
        vm.ToggleSectionLock(Row(vm, PaletteSections.Tertiary));
        vm.GeneratePalette();
        vm.GeneratePalette();
        var colours = vm.CoreSections.Concat(vm.SyntaxSections).Concat(vm.ChartSections).ToDictionary(r => r.Id, r => r.Hex);
        vm.FlushAutoSave();
        services.StudioSettings.Flush();

        var again = NewSettings(out _);

        Assert.Equal(ColorEngineKind.Hct, again.SelectedColorEngine);
        Assert.Equal(ColorEngineKind.Hct, again.SelectedColorEngineOption!.Kind);
        Assert.True(Row(again, PaletteSections.Tertiary).IsLocked);
        Assert.Equal(colours, again.CoreSections.Concat(again.SyntaxSections).Concat(again.ChartSections).ToDictionary(r => r.Id, r => r.Hex));
    }

    [Fact]
    public void ASavedTheme_KeepsItsPalette_AndReopensItInTheStudio()
    {
        var vm = NewSettings(out _);
        vm.ToggleSectionLock(Row(vm, PaletteSections.Secondary));
        vm.GeneratePalette();
        vm.ApplyHarmonicConfiguration();
        vm.NewThemeName = "Harbour";
        vm.SaveCurrentThemeAs();
        var saved = vm.UserThemes.Single();
        var designed = vm.CoreSections.ToDictionary(r => r.Id, r => r.Hex);
        Assert.NotNull(saved.Palette);

        // Something else in the studio meanwhile.
        vm.GeneratePalette();
        vm.ToggleSectionLock(Row(vm, PaletteSections.Secondary));
        Assert.NotEqual(designed, vm.CoreSections.ToDictionary(r => r.Id, r => r.Hex));

        vm.ApplyThemePreset(saved.Id);

        Assert.Equal(designed, vm.CoreSections.ToDictionary(r => r.Id, r => r.Hex));
        Assert.True(Row(vm, PaletteSections.Secondary).IsLocked);
        Assert.Equal(saved.Id, StudioAppContext.Instance.ThemeEngine.ActiveThemeId);
    }

    [Fact]
    public void TheExports_AreTheDesignedPalette()
    {
        var vm = NewSettings(out _);
        vm.GeneratePalette();
        var light = vm.BuildPaletteTheme(isDark: false);
        var dark = vm.BuildPaletteTheme(isDark: true);

        Assert.False(light.IsDark);
        Assert.True(dark.IsDark);
        Assert.Equal(vm.CurrentPalette.Keys[PaletteSections.Chart(1)].ToHex(), dark.Colors["ChartSeries1Brush"]);
        Assert.NotEqual(light.Colors["DsBgBrush"], dark.Colors["DsBgBrush"]);
    }
}
