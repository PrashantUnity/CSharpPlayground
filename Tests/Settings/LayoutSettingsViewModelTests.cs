using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Settings → Layout &amp; Typography: levers change the studio's layout (with undo), layouts are saved by name, and a
/// restart, a theme saved with its layout, or an old density-only preferences file all come back as they were.
/// </summary>
[Collection("SettingsTests")]
public class LayoutSettingsViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FryPDF_LayoutSettings_" + Guid.NewGuid().ToString("N"));

    public LayoutSettingsViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.ApplyLayout(LayoutSpec.Default);
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private CSharpSettingsViewModel NewSettings(out StudioLanguageServices services)
    {
        services = new StudioLanguageServices(_folder);
        return new CSharpSettingsViewModel(services, services.StudioSettings);
    }

    private StudioSettingsStore Store() => new(Path.Combine(_folder, "studio_settings.json"));

    [Fact]
    public void TheLayoutCategory_IsListed_AndHasEveryPreset()
    {
        var vm = NewSettings(out _);
        Assert.Contains(vm.Categories, c => c.Id == "Layout");
        vm.SelectCategory("Layout");
        Assert.True(vm.IsLayoutCategoryActive);
        Assert.Equal(LayoutPresets.All.Select(p => p.Id), vm.LayoutPresetItems.Select(p => p.Id));
        Assert.Equal("Studio", vm.ActiveLayoutName);
        Assert.Equal(13, vm.TypeRampRows.Count);
    }

    [Fact]
    public void Levers_ChangeTheStudio_AndUndoRedoStepThroughIt()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var vm = NewSettings(out _);

        vm.LayoutBaseFontSize = 14;
        vm.LayoutRadiusScale = 0;
        vm.LayoutCardBorders = false;

        Assert.Equal(14, engine.Layout.BaseFontSize);
        Assert.Equal(0, engine.Layout.RadiusScale);
        Assert.False(engine.Layout.CardBorders);
        Assert.Equal("Custom", vm.ActiveLayoutName);
        Assert.True(vm.CanUndoLayout);

        vm.UndoLayout();
        Assert.True(engine.Layout.CardBorders);
        Assert.Equal(0, engine.Layout.RadiusScale);
        Assert.True(vm.LayoutCardBorders);

        vm.RedoLayout();
        Assert.False(engine.Layout.CardBorders);

        while (vm.CanUndoLayout) vm.UndoLayout();
        Assert.True(engine.Layout.SameLayoutAs(LayoutSpec.Default));
        Assert.Equal(12, vm.LayoutBaseFontSize);
    }

    [Fact]
    public void ComponentOverrides_AndSizes_ReachTheTokens()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var vm = NewSettings(out _);
        var card = vm.RadiusOverrideRows.Single(r => r.Key == LayoutComponents.Card);
        Assert.Equal(10, card.AutoValue);

        card.Value = 3;
        card.IsOverridden = true;
        var tab = vm.SizeOverrideRows.Single(r => r.Key == "TabHeight");
        tab.IsOverridden = true;
        tab.Value = 40;

        Assert.Equal(3, engine.Layout.RadiusOverrides[LayoutComponents.Card]);
        Assert.Equal(40, engine.Layout.SizeOverrides["TabHeight"]);
        Assert.Equal(new Avalonia.CornerRadius(3), LayoutTokens.Get("DsRadiusCard", default(Avalonia.CornerRadius)));
        Assert.Equal(40.0, LayoutTokens.Get("DsTabHeight", 0.0));
    }

    [Fact]
    public void Presets_Apply_AndAreMarkedInUse()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var vm = NewSettings(out _);
        var vscode = vm.LayoutPresetItems.Single(p => p.Id == "layout-vscode");

        vm.ApplyLayoutPreset(vscode);
        vm.FlushAutoSave();

        Assert.Equal(LayoutDensity.Compact, engine.Layout.Density);
        Assert.Equal(LayoutDensity.Compact, vm.LayoutDensity);
        Assert.True(vscode.IsActive);
        Assert.Equal("VS Code", vm.ActiveLayoutName);
        Assert.Equal("layout-vscode", Store().GetSettings().Appearance.ActiveLayoutId);
    }

    [Fact]
    public void SavedLayouts_AreNamed_RenamedCopiedAndDeleted_AndSurviveAReload()
    {
        var vm = NewSettings(out _);
        vm.LayoutBaseFontSize = 13.5;
        vm.NewLayoutName = "Reading";
        vm.SaveLayoutAs();
        var saved = vm.UserLayouts.Single();
        Assert.Equal("Reading", saved.Name);
        Assert.True(saved.IsActive);

        vm.BeginRenameLayout(saved);
        saved.EditName = "Reading room";
        vm.RenameLayout(saved);
        vm.DuplicateLayout(vm.LayoutPresetItems.Single(p => p.Id == "layout-sharp"));
        Assert.Equal(2, vm.UserLayouts.Count);

        var again = NewSettings(out _);
        Assert.Equal(2, again.UserLayouts.Count);
        var reading = again.UserLayouts.Single(l => l.Name == "Reading room");
        Assert.Equal(13.5, reading.Spec.BaseFontSize);
        Assert.Equal("Reading room", again.ActiveLayoutName);

        var copy = again.UserLayouts.Single(l => l.Name == "Sharp copy");
        again.AskDeleteLayout(copy);
        Assert.True(copy.IsConfirmingDelete);
        again.DeleteLayout(copy);
        Assert.Single(again.UserLayouts);
        Assert.Single(new LayoutLibraryStore(Path.Combine(_folder, "layouts")).List());
    }

    [Fact]
    public void AfterARestart_TheStudioHasTheSameLayout()
    {
        var vm = NewSettings(out var services);
        vm.LayoutDensity = LayoutDensity.Spacious;
        vm.LayoutShadowStrength = 0;
        vm.LayoutBodyWeight = "Medium";
        vm.FlushAutoSave();
        services.StudioSettings.Flush();

        var fresh = new DynamicThemeEngine();
        AppearanceRestorer.Restore(Store(), new ThemeLibraryStore(Path.Combine(_folder, "themes")), fresh);
        Assert.Equal(LayoutDensity.Spacious, fresh.Layout.Density);
        Assert.Equal(0, fresh.Layout.ShadowStrength);
        Assert.Equal("Medium", fresh.Layout.BodyWeight);

        var again = NewSettings(out _);
        Assert.Equal(LayoutDensity.Spacious, again.LayoutDensity);
        Assert.Equal("Medium", again.LayoutBodyWeight);
    }

    [Fact]
    public void AnOldDensityOnlyPreferencesFile_BecomesALayout()
    {
        var store = Store();
        store.Update(s => s.Appearance.Density = "Compact");
        store.Flush();

        var fresh = new DynamicThemeEngine();
        AppearanceRestorer.Restore(Store(), new ThemeLibraryStore(Path.Combine(_folder, "themes")), fresh);

        Assert.Equal(LayoutDensity.Compact, fresh.Layout.Density);
    }

    [Fact]
    public void DensityChangedElsewhere_MovesTheLevers_AndIsRemembered()
    {
        var vm = NewSettings(out var services);
        vm.SetDensity("Spacious"); // the Customization page's buttons
        Assert.Equal(LayoutDensity.Spacious, vm.LayoutDensity);
        vm.FlushAutoSave();
        services.StudioSettings.Flush();
        Assert.Equal(LayoutDensity.Spacious, LayoutSpec.FromJson(Store().GetSettings().Appearance.Layout)!.Density);
    }

    [Fact]
    public void ATheme_SavedWithItsLayout_BringsItBack()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var vm = NewSettings(out _);
        vm.ApplyLayoutPreset(vm.LayoutPresetItems.Single(p => p.Id == "layout-soft"));
        vm.ApplyThemePreset("dracula");
        vm.IncludeLayoutInTheme = true;
        vm.NewThemeName = "Soft dracula";
        vm.SaveCurrentThemeAs();
        var theme = vm.UserThemes.Single();

        vm.ResetLayout();
        vm.ApplyThemePreset(BuiltInThemes.DarkPlus.Id);
        Assert.Equal(1, engine.Layout.RadiusScale);

        vm.ApplyThemePreset(theme.Id);
        Assert.Equal(1.6, engine.Layout.RadiusScale);
        Assert.False(engine.Layout.CardBorders);
    }

    [Fact]
    public void ALayout_ExportsAsDesignTokensAndCss_AndImportsBack()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var vm = NewSettings(out _);
        vm.LayoutBaseFontSize = 15;
        vm.LayoutRadiusScale = 2;
        var json = vm.ExportLayoutDtcg();
        var css = vm.ExportLayoutCss();

        Assert.Contains("\"DsFontSize300\"", json);
        Assert.Contains("\"$type\": \"dimension\"", json);
        Assert.Contains("com.frypdf.layout", json);
        Assert.Contains("--ds-font-size300: 15px;", css);
        Assert.Contains("--ds-radius-md: 12px;", css);

        vm.ResetLayout();
        Assert.Equal(12, engine.Layout.BaseFontSize);
        vm.NewLayoutName = "From a friend";
        var imported = vm.ImportLayoutDtcg(json);

        Assert.NotNull(imported);
        Assert.Equal(15, engine.Layout.BaseFontSize);
        Assert.Equal(2, engine.Layout.RadiusScale);
        Assert.Equal("From a friend", vm.UserLayouts.First().Name);
        Assert.Null(vm.ImportLayoutDtcg("{ \"not\": \"a layout\" }"));
    }
}
