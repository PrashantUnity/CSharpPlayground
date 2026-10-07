using Avalonia.Controls;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Applying a theme: the whole theme reaches the screen in one resource change (it used to be ~700, which froze the
/// studio for ~8 s per theme), its colours win over the palette the studio includes (they used to be shadowed and never
/// showed), and the Settings page recolours its rows instead of rebuilding them.
/// </summary>
[Collection("SettingsTests")]
public class ThemeApplyTests : IDisposable
{
    private readonly string _tempFolder = Path.Combine(Path.GetTempPath(), "FryPDF_ThemeApply_" + Guid.NewGuid().ToString("N"));

    public ThemeApplyTests() => Directory.CreateDirectory(_tempFolder);

    public void Dispose()
    {
        try { Directory.Delete(_tempFolder, recursive: true); } catch (IOException) { }
    }

    // A studio root as the host view builds it: the palette included in its resources, controls below it.
    private static (Border Root, TextBlock Child) StudioRoot()
    {
        var palette = new ResourceDictionary
        {
            ["DsBgBrush"] = new SolidColorBrush(Color.Parse("#0D1117")),
            ["DsPrimaryBrush"] = new SolidColorBrush(Color.Parse("#2F81F7")),
        };
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(palette);
        var child = new TextBlock();
        var root = new Border { Resources = resources, Child = child };
        return (root, child);
    }

    private static Color ResolvedColor(Control control, string key) =>
        control.TryFindResource(key, out var value) && value is ISolidColorBrush brush ? brush.Color : default;

    [Fact]
    public void AWholeTheme_ReachesTheScreenInOneResourceChange_AndWinsOverThePalette()
    {
        var engine = new DynamicThemeEngine();
        var (root, child) = StudioRoot();
        engine.AttachResourceRoot(root.Resources);
        int changes = 0;
        root.ResourcesChanged += (_, _) => changes++;
        int commitsBefore = engine.LayerCommits;

        Assert.True(engine.ApplyTheme("dracula"));

        Assert.Equal(1, engine.LayerCommits - commitsBefore);
        Assert.Equal(1, changes);
        Assert.Equal(Color.Parse("#21222C"), ResolvedColor(child, "DsBgBrush"));
        Assert.Equal("#FF21222C", engine.GetColor("DsBgBrush"));

        engine.ApplyTheme("cyberpunk");
        Assert.Equal(2, changes);
        Assert.NotEqual(Color.Parse("#21222C"), ResolvedColor(child, "DsBgBrush"));
    }

    [Fact]
    public void SingleTokens_AndDensity_AreOneChangeEach_AndDetachingARootTakesTheLayerAway()
    {
        var engine = new DynamicThemeEngine();
        var (root, child) = StudioRoot();
        engine.AttachResourceRoot(root.Resources);
        int changes = 0;
        root.ResourcesChanged += (_, _) => changes++;

        engine.SetColor("DsPrimaryBrush", "#FF7043");
        Assert.Equal(1, changes);
        Assert.Equal(Color.Parse("#FF7043"), ResolvedColor(child, "DsPrimaryBrush"));

        engine.SetDensity(FrySharp.Sdk.LayoutDensity.Compact);
        Assert.Equal(2, changes);
        Assert.True(child.TryFindResource("DensityTabHeight", out var height));
        Assert.Equal(PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutTokenMapper.Map(
            PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.LayoutSpec.Default with { Density = FrySharp.Sdk.LayoutDensity.Compact }, isDark: true)["DsTabHeight"], height);
        Assert.True((double)height! < 35);

        engine.DetachResourceRoot(root.Resources);
        Assert.Equal(Color.Parse("#2F81F7"), ResolvedColor(child, "DsPrimaryBrush"));
    }

    [Fact]
    public void ApplyingAThemeInSettings_RecoloursTheTokenRows_WithoutRebuildingTheList()
    {
        var vm = new CSharpSettingsViewModel(new StudioLanguageServices(_tempFolder));
        var rowsBefore = vm.FilteredColorTokens.ToList();
        int listChanges = 0;
        vm.FilteredColorTokens.CollectionChanged += (_, _) => listChanges++;

        vm.ApplyThemePreset("dracula");
        vm.ApplyThemePreset("cyberpunk");

        Assert.Equal(0, listChanges);
        Assert.Equal(rowsBefore, vm.FilteredColorTokens);
        var background = vm.FilteredColorTokens.FirstOrDefault(t => t.Key == "DsBgBrush");
        if (background != null) Assert.Equal(PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine.GetColor("DsBgBrush"), background.CurrentHex);

        // A search still filters the list (in one replacement).
        vm.TokenSearchQuery = "Primary";
        Assert.Equal(1, listChanges);
        Assert.All(vm.FilteredColorTokens, t => Assert.True(
            t.Key.Contains("Primary", StringComparison.OrdinalIgnoreCase) || t.DisplayName.Contains("Primary", StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains("Primary", StringComparison.OrdinalIgnoreCase) || t.CurrentHex.Contains("Primary", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MovingTheHueWheel_RecoloursTheSwatchAndContrastRows_InPlace()
    {
        var vm = new CSharpSettingsViewModel(new StudioLanguageServices(_tempFolder));
        var swatches = vm.PrimaryTonalSwatches.ToList();
        var contrast = vm.ContrastPairings.ToList();
        var oldHexes = swatches.Select(s => s.Hex).ToList();

        vm.SelectedHueDegrees = (vm.SelectedHueDegrees + 120f) % 360f;

        Assert.Equal(swatches, vm.PrimaryTonalSwatches);
        Assert.Equal(contrast, vm.ContrastPairings);
        Assert.NotEqual(oldHexes, vm.PrimaryTonalSwatches.Select(s => s.Hex).ToList());
    }
}
