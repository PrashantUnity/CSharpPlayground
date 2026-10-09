using System.Text.Json;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using CSharpEditorPlugin.Tests.TestSupport;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The user's preferences survive a restart: settings are written atomically and a moment after the last change, a
/// change to one setting never resets another, a damaged file is kept rather than overwritten, saved themes (as many as
/// the user names) come back, and the studio reopens with the theme, density and colours it was closed with.
/// </summary>
[Collection("SettingsTests")]
public class SettingsPersistenceTests : IDisposable
{
    private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_Persistence_" + Guid.NewGuid().ToString("N"));

    public SettingsPersistenceTests() => Directory.CreateDirectory(_tempFolder);

    public void Dispose()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ResetToDefaults();
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        engine.SetDensity(LayoutDensity.Comfortable);
        try { Directory.Delete(_tempFolder, recursive: true); } catch (IOException) { }
    }

    private string SettingsPath => Path.Combine(_tempFolder, "studio_settings.json");

    private static string Json(StudioSettings settings) => JsonSerializer.Serialize(settings);

    [Fact]
    public void EverySetting_RoundTripsThroughANewStore()
    {
        var store = new StudioSettingsStore(SettingsPath);
        store.Update(s =>
        {
            s.FontSize = 17;
            s.CSharpExecutionEngine = "external";
            s.Appearance.ActiveThemeId = "user-abc";
            s.Appearance.Density = "Compact";
            s.Appearance.PreferDark = false;
            s.Appearance.TokenOverrides["DsPrimaryBrush"] = "#FF8800";
            s.Appearance.Harmony.HueDegrees = 42;
            s.Appearance.Harmony.Mode = "Triadic";
        });
        store.Flush();

        var reloaded = new StudioSettingsStore(SettingsPath).GetSettings();

        Assert.Equal(Json(store.GetSettings()), Json(reloaded));
        Assert.Equal("#FF8800", reloaded.Appearance.TokenOverrides["DsPrimaryBrush"]);
        Assert.Equal(42, reloaded.Appearance.Harmony.HueDegrees);
    }

    [Fact]
    public void TwentyChangesInARow_AreWrittenOnce_ShortlyAfterTheLast()
    {
        var time = new ManualTimeProvider();
        var store = new StudioSettingsStore(SettingsPath, time);
        for (var i = 1; i <= 20; i++)
        {
            var size = 10 + i;
            store.Update(s => s.FontSize = size);
            time.Advance(StudioSettingsStore.WriteDelay / 2);
        }

        Assert.Equal(0, store.WriteCount);
        Assert.True(store.HasUnsavedChanges);

        time.Advance(StudioSettingsStore.WriteDelay);
        Assert.False(store.HasUnsavedChanges);
        time.Advance(StudioSettingsStore.WriteDelay * 4);

        Assert.Equal(1, store.WriteCount);
        Assert.Equal(30, new StudioSettingsStore(SettingsPath).GetSettings().FontSize);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void AnUnreadableFile_IsKeptAside_AndTheStudioStartsFromDefaults()
    {
        File.WriteAllText(SettingsPath, "{ \"FontSize\": 14, this is not json");

        var store = new StudioSettingsStore(SettingsPath);
        var settings = store.GetSettings();

        Assert.Equal(new StudioSettings().FontSize, settings.FontSize);
        var aside = Directory.GetFiles(_tempFolder, "studio_settings.corrupt-*.json");
        Assert.Single(aside);
        Assert.Contains("this is not json", File.ReadAllText(aside[0]));

        store.Update(s => s.FontSize = 18);
        store.Flush();
        Assert.Equal(18, new StudioSettingsStore(SettingsPath).GetSettings().FontSize);
        Assert.Single(Directory.GetFiles(_tempFolder, "studio_settings.corrupt-*.json"));
    }

    [Fact]
    public void SavingTheEditorSettings_LeavesEveryOtherSettingAlone()
    {
        // The editor save used to write a brand new settings object, so anything it didn't copy (the appearance, AI
        // reasoning, any setting added later) was reset each time the user changed, say, the font size.
        var services = new StudioLanguageServices(_tempFolder);
        services.StudioSettings.Update(s =>
        {
            s.Appearance.ActiveThemeId = "user-kept";
            s.Appearance.Density = "Spacious";
            s.Appearance.TokenOverrides["DsBgBrush"] = "#101010";
            s.Ai.EnableReasoning = false;
            s.CSharpExecutionEngine = "external";
        });
        services.StudioSettings.Flush();

        var vm = new CSharpSettingsViewModel(services, services.StudioSettings);
        vm.FontSize = 19;
        vm.Apply();

        var saved = new StudioSettingsStore(SettingsPath).GetSettings();
        Assert.Equal(19, saved.FontSize);
        Assert.Equal("user-kept", saved.Appearance.ActiveThemeId);
        Assert.Equal("Spacious", saved.Appearance.Density);
        Assert.Equal("#101010", saved.Appearance.TokenOverrides["DsBgBrush"]);
        Assert.False(saved.Ai.EnableReasoning);
        Assert.Equal("external", saved.CSharpExecutionEngine);
    }

    [Fact]
    public void AHundredNamedThemes_SurviveAReload_ThroughRenameDuplicateAndDelete()
    {
        var folder = Path.Combine(_tempFolder, "themes");
        var library = new ThemeLibraryStore(folder);
        var ids = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            var theme = new ThemeDefinition
            {
                Id = "whatever",
                Name = "ignored",
                IsDark = i % 2 == 0,
                Colors = new Dictionary<string, string> { ["DsPrimaryBrush"] = $"#{i:X2}8040" },
            };
            ids.Add(library.Save($"Theme {i}", theme).Id);
        }

        Assert.Equal(100, ids.Distinct().Count());
        Assert.All(ids, id => Assert.StartsWith(ThemeLibraryStore.IdPrefix, id));

        for (var i = 0; i < 10; i++) library.Rename(ids[i], $"Renamed {i}");
        var copies = Enumerable.Range(10, 5).Select(i => library.Duplicate(library.Get(ids[i])!.Theme, $"Copy of {i}").Id).ToList();
        for (var i = 80; i < 100; i++) Assert.True(library.Delete(ids[i]));
        // Names don't have to be unique: the id tells two "Ocean"s apart.
        library.Save("Theme 50", new ThemeDefinition { Colors = new() { ["DsPrimaryBrush"] = "#000000" } });

        var reloaded = new ThemeLibraryStore(folder).List();

        Assert.Equal(100 - 20 + 5 + 1, reloaded.Count);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"Renamed {i}"), reloaded.Where(t => t.Name.StartsWith("Renamed")).Select(t => t.Name).Order());
        Assert.All(copies, id => Assert.Contains(reloaded, t => t.Id == id && t.Source == "duplicate"));
        Assert.DoesNotContain(reloaded, t => ids.Skip(80).Contains(t.Id));
        Assert.Equal(2, reloaded.Count(t => t.Name == "Theme 50"));

        var five = reloaded.Single(t => t.Id == ids[5]);
        Assert.Equal("Renamed 5", five.Theme.Name);
        Assert.Equal(ids[5], five.Theme.Id);
        // Read back with the case-insensitive lookups the theme engine uses.
        Assert.Equal("#058040", five.Theme.Colors["dsprimarybrush"]);
    }

    [Fact]
    public void ADamagedThemeFile_IsSkipped_AndTheOthersStillLoad()
    {
        var folder = Path.Combine(_tempFolder, "themes");
        var library = new ThemeLibraryStore(folder);
        library.Save("Good", new ThemeDefinition { Colors = new() { ["DsBgBrush"] = "#111111" } });
        File.WriteAllText(Path.Combine(folder, "user-broken" + ThemeLibraryStore.FileSuffix), "{ not json");

        var themes = new ThemeLibraryStore(folder).List();

        Assert.Single(themes);
        Assert.Equal("Good", themes[0].Name);
        Assert.False(library.Delete(BuiltInThemes.DarkPlus.Id));
    }

    [Fact]
    public void AfterARestart_TheStudioComesBackWithTheSameThemeDensityColoursAndLibrary()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;

        // First run: save one theme by name, then generate another and leave it applied (never saved), make the
        // studio compact and change one colour by hand.
        var first = new StudioLanguageServices(_tempFolder);
        var vm = new CSharpSettingsViewModel(first, first.StudioSettings);
        vm.SelectedHueDegrees = 40;
        vm.ApplyHarmonicConfiguration();
        vm.NewThemeName = "Ocean";
        vm.SaveCurrentThemeAs();
        var oceanId = engine.ActiveThemeId;
        Assert.True(ThemeLibraryStore.IsLibraryId(oceanId));

        vm.SelectedHueDegrees = 300;
        vm.ApplyHarmonicConfiguration();
        var generatedId = engine.ActiveThemeId;
        var generatedPrimary = engine.GetColor("DsPrimaryBrush");
        vm.SetDensity("Compact");
        first.StudioSettings.Update(s => s.Appearance.TokenOverrides["DsAccentLineBrush"] = "#12AB34");
        vm.FlushAutoSave();
        first.StudioSettings.Flush();

        // Second run: a new process has a new theme engine, which knows only the built-in themes.
        var fresh = new DynamicThemeEngine();
        Assert.False(fresh.HasTheme(generatedId));
        Assert.NotEqual(generatedPrimary, fresh.GetColor("DsPrimaryBrush"));

        var second = new StudioLanguageServices(_tempFolder);
        var restored = AppearanceRestorer.Restore(second.StudioSettings, second.ThemeLibrary, fresh);

        Assert.Equal(generatedId, restored);
        Assert.Equal(generatedId, fresh.ActiveThemeId);
        Assert.Equal(generatedPrimary, fresh.GetColor("DsPrimaryBrush"));
        Assert.Equal("#12AB34", fresh.GetColor("DsAccentLineBrush"));
        Assert.Equal(LayoutDensity.Compact, fresh.Layout.Density);
        Assert.True(Convert.ToDouble(fresh.GetResource("DsTabHeight")) < 35, "compact tabs are shorter");

        var vm2 = new CSharpSettingsViewModel(second, second.StudioSettings);
        Assert.Single(vm2.UserThemes);
        Assert.Equal("Ocean", vm2.UserThemes[0].Name);
        Assert.Equal(oceanId, vm2.UserThemes[0].Id);
        Assert.Equal(300f, vm2.SelectedHueDegrees);
        Assert.Equal(LayoutDensity.Compact, vm2.ActiveLayoutDensity);

        // The saved theme comes back too when it was the one in use.
        second.StudioSettings.Update(s => s.Appearance.ActiveThemeId = oceanId);
        second.StudioSettings.Flush();
        var third = new DynamicThemeEngine();
        Assert.Equal(oceanId, AppearanceRestorer.Restore(new StudioSettingsStore(SettingsPath), new ThemeLibraryStore(Path.Combine(_tempFolder, "themes")), third));
        Assert.Equal("Ocean", third.GetTheme(oceanId)!.Name);
    }

    [Fact]
    public void TheLightDarkToggle_IsRemembered_WhenNoThemeWasChosen()
    {
        var engine = new DynamicThemeEngine();
        var store = new StudioSettingsStore(SettingsPath);
        store.Update(s => s.Appearance.PreferDark = false);

        Assert.Equal(BuiltInThemes.LightPlus.Id, AppearanceRestorer.Restore(store, new ThemeLibraryStore(Path.Combine(_tempFolder, "themes")), engine));
        Assert.Equal(BuiltInThemes.LightPlus.Id, engine.ActiveThemeId);
    }

    [Fact]
    public void DeletingTheThemeInUse_GoesBackToDarkPlus_AndThatIsWhatARestartShows()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var services = new StudioLanguageServices(_tempFolder);
        var vm = new CSharpSettingsViewModel(services, services.StudioSettings);
        vm.ApplyThemePreset(BuiltInThemes.DarkPlus.Id);
        vm.NewThemeName = "Short lived";
        vm.SaveCurrentThemeAs();
        var item = vm.UserThemes.Single();

        vm.AskDeleteUserTheme(item);
        Assert.True(item.IsConfirmingDelete);
        vm.DeleteUserTheme(item);
        vm.FlushAutoSave();

        Assert.Empty(vm.UserThemes);
        Assert.Equal(BuiltInThemes.DarkPlus.Id, engine.ActiveThemeId);
        Assert.Equal(BuiltInThemes.DarkPlus.Id, new StudioSettingsStore(SettingsPath).GetSettings().Appearance.ActiveThemeId);
    }

    [Fact]
    public void ASavedTheme_CanBeRenamedInPlace_AndTheNewNameIsWhatComesBack()
    {
        var services = new StudioLanguageServices(_tempFolder);
        var vm = new CSharpSettingsViewModel(services, services.StudioSettings);
        vm.ApplyThemePreset("dracula");
        vm.NewThemeName = "Night";
        vm.SaveCurrentThemeAs();
        var item = vm.UserThemes.Single();

        vm.BeginRenameUserTheme(item);
        Assert.True(item.IsRenaming);
        Assert.Equal("Night", item.EditName);
        item.EditName = "  Deep night ";
        vm.RenameUserTheme(item);

        Assert.False(item.IsRenaming);
        Assert.Equal("Deep night", item.Name);
        Assert.Equal("Deep night", vm.ActiveThemeName);
        Assert.Equal("Deep night", new ThemeLibraryStore(Path.Combine(_tempFolder, "themes")).Get(item.Id)!.Name);

        vm.DuplicateTheme(vm.ThemePresets.First(p => p.Id == BuiltInThemes.LightPlus.Id));
        Assert.Equal(2, vm.UserThemes.Count);
        Assert.Equal("Light+ (Default) copy", vm.UserThemes[0].Name);
    }

    [Fact]
    public void AnImportedDtcgTheme_IsSavedToMyThemes_UnderTheTypedName_AndAppliedAfterARestart()
    {
        var services = new StudioLanguageServices(_tempFolder);
        var vm = new CSharpSettingsViewModel(services, services.StudioSettings);
        vm.NewThemeName = "Brand kit";

        var saved = vm.ImportDtcgJson("""{ "color": { "DsPrimaryBrush": { "$value": "#C0FFEE" }, "DsBgBrush": "#0B0B0B" } }""");
        vm.FlushAutoSave();

        Assert.NotNull(saved);
        Assert.Equal("Brand kit", saved!.Name);
        Assert.Equal("imported", saved.Source);
        Assert.Equal(string.Empty, vm.NewThemeName);
        Assert.Equal(saved.Id, vm.UserThemes.Single().Id);

        var fresh = new DynamicThemeEngine();
        Assert.Equal(saved.Id, AppearanceRestorer.Restore(new StudioSettingsStore(SettingsPath), new ThemeLibraryStore(Path.Combine(_tempFolder, "themes")), fresh));
        Assert.Equal("#FFC0FFEE", fresh.GetColor("DsPrimaryBrush"), ignoreCase: true);
    }

    [Fact]
    public void AThemeChosenOutsideSettings_ByTheToolbarToggleOrAScript_IsRememberedToo()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        var services = new StudioLanguageServices(_tempFolder);
        var vm = new CSharpSettingsViewModel(services, services.StudioSettings);

        // What the light/dark toggle and a customization script do: apply through the engine, not through Settings.
        engine.ApplyTheme(BuiltInThemes.LightPlus.Id);
        vm.FlushAutoSave();

        var appearance = new StudioSettingsStore(SettingsPath).GetSettings().Appearance;
        Assert.Equal(BuiltInThemes.LightPlus.Id, appearance.ActiveThemeId);
        Assert.False(appearance.PreferDark);
        Assert.Null(appearance.ActiveThemeSnapshot);
    }
}
