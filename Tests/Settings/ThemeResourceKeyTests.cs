using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Every theme resource a view names exists, in dark and in light. The object inspector's property names, types and
/// values (and the debugger's data tips and watch rows) named <c>Code*Brush</c> keys whose only definition went away with
/// the Runner's old token file: the text fell back to the surrounding foreground and was close to invisible. Value
/// colours now come from the palette per scheme, follow a theme's syntax colours, and are looked up from the control,
/// so they are found wherever the studio is hosted.
/// </summary>
[Collection("SettingsTests")]
public class ThemeResourceKeyTests
{
    private static readonly string[] Folders = ["Views", "Controls", "Styles", "Runner", "Charting", "Charting3D", "Visualizers"];

    private static readonly Regex ResourceUse = new(@"\{(?:Dynamic|Static)Resource\s+(?:ResourceKey=)?([\w.]+)\s*\}");
    private static readonly Regex ResourceKey = new(@"x:Key=""([^""]+)""");

    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.csproj"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static IEnumerable<string> ViewFiles(string root) =>
        Folders.Select(f => Path.Combine(root, f)).Where(Directory.Exists)
            .SelectMany(f => Directory.EnumerateFiles(f, "*.axaml", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    // What the engine writes for a theme's colour key (see DynamicThemeEngine.WriteColor).
    private static IEnumerable<string> ColorVariants(string key) =>
        key.EndsWith("Brush", StringComparison.Ordinal) ? [key, key[..^5] + "Color"] : [key, key + "Brush"];

    /// <summary>Keys every studio has: the palette and styles' own, each built-in theme's, and the layout's.</summary>
    private static HashSet<string> DefinedKeys(string root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ViewFiles(root))
        {
            foreach (Match m in ResourceKey.Matches(File.ReadAllText(file))) keys.Add(m.Groups[1].Value);
        }

        foreach (var theme in BuiltInThemes.All)
        {
            foreach (var key in theme.Colors.Keys.SelectMany(ColorVariants)) keys.Add(key);
        }

        foreach (var key in LayoutTokenMapper.Map(LayoutSpec.Default, isDark: true).Keys) keys.Add(key);
        return keys;
    }

    [Fact]
    public void EveryThemeResourceAViewNames_IsDefined()
    {
        var root = Root();
        var defined = DefinedKeys(root);
        var missing = new List<string>();
        foreach (var file in ViewFiles(root))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in ResourceUse.Matches(lines[i]))
                {
                    if (!defined.Contains(m.Groups[1].Value)) missing.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {m.Groups[1].Value}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Resources named but defined nowhere (the text or fill falls back to whatever is around it):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void ThePalette_DefinesTheSameKeys_InDarkAndLight_IncludingTheValueColours()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "Styles", "Tokens", "StudioPaletteTokens.axaml"));
        int dark = text.IndexOf("<ResourceDictionary x:Key=\"Dark\">", StringComparison.Ordinal);
        int light = text.IndexOf("<ResourceDictionary x:Key=\"Light\">", StringComparison.Ordinal);
        int end = text.IndexOf("</ResourceDictionary.ThemeDictionaries>", StringComparison.Ordinal);
        var darkKeys = ResourceKey.Matches(text[dark..light]).Select(m => m.Groups[1].Value).Where(k => k != "Dark").ToHashSet();
        var lightKeys = ResourceKey.Matches(text[light..end]).Select(m => m.Groups[1].Value).Where(k => k != "Light").ToHashSet();

        Assert.Empty(darkKeys.Except(lightKeys));
        Assert.Empty(lightKeys.Except(darkKeys));
        foreach (var key in new[] { "CodeTypeBrush", "CodeMemberBrush", "CodeStringBrush", "CodeNumberBrush", "CodeKeywordBrush", "CodeNullBrush" })
        {
            Assert.Contains(key, darkKeys);
        }

        // And every key an inspector value can ask for exists.
        foreach (var row in new[] { Row("text", isString: true), Row("true"), Row("42"), Row("[ 1, 2 ]"), Row("Some object"), new ObjectInspectorPropertyRow { IsNull = true } })
        {
            Assert.True(darkKeys.Contains(row.ValueForegroundKey) || BuiltInThemes.DarkPlus.Colors.ContainsKey(row.ValueForegroundKey), row.ValueForegroundKey);
        }
    }

    private static ObjectInspectorPropertyRow Row(string value, bool isString = false) => new() { SimpleValueText = value, IsString = isString };

    private static ThemeDefinition ThemeWithSyntax(string id) => new()
    {
        Id = id,
        Name = id,
        IsDark = true,
        Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SyntaxTypeBrush"] = "#11AA22",
            ["SyntaxVariableBrush"] = "#3344CC",
            ["SyntaxStringBrush"] = "#DD5500",
            ["SyntaxNumberBrush"] = "#66EE77",
            ["SyntaxKeywordBrush"] = "#8800FF",
            ["DsMutedBrush"] = "#999999",
        },
    };

    private static Color LayerColor(DynamicThemeEngine engine, string key) =>
        engine.GetResource(key) is ISolidColorBrush brush ? brush.Color : default;

    [Fact]
    public void ValueColours_FollowATheme_WithSyntaxColours_AndDoNotOutliveIt()
    {
        var engine = new DynamicThemeEngine();
        engine.RegisterTheme(ThemeWithSyntax("syntax-test"));

        Assert.True(engine.ApplyTheme("syntax-test"));
        Assert.Equal(Color.Parse("#11AA22"), LayerColor(engine, "CodeTypeBrush"));
        Assert.Equal(Color.Parse("#3344CC"), LayerColor(engine, "CodeMemberBrush"));
        Assert.Equal(Color.Parse("#DD5500"), LayerColor(engine, "CodeStringBrush"));
        Assert.Equal(Color.Parse("#66EE77"), LayerColor(engine, "CodeNumberBrush"));
        Assert.Equal(Color.Parse("#8800FF"), LayerColor(engine, "CodeKeywordBrush"));
        Assert.Equal(Color.Parse("#999999"), LayerColor(engine, "CodeNullBrush"));

        // Dark+ has no syntax colours: the previous theme's go away (the palette's defaults show again), they don't
        // linger in the layer.
        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        Assert.Null(engine.GetResource("CodeTypeBrush"));
        Assert.Null(engine.GetResource("SyntaxTypeBrush"));
        Assert.Equal(Color.Parse(BuiltInThemes.DarkPlus.Colors["DsMutedBrush"]), LayerColor(engine, "CodeNullBrush"));

        // An override of a syntax colour takes its value colour along; the next theme drops both.
        engine.SetColor("SyntaxTypeBrush", "#123456");
        Assert.Equal(Color.Parse("#123456"), LayerColor(engine, "CodeTypeBrush"));
        engine.ApplyTheme("dracula");
        Assert.Null(engine.GetResource("CodeTypeBrush"));
    }

    [Fact]
    public void ThemeBrush_ResolvesFromTheControl_AndFollowsTheTheme()
    {
        var palette = new ResourceDictionary { ["CodeTypeBrush"] = new SolidColorBrush(Color.Parse("#4EC9B0")) };
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(palette);
        var child = new TextBlock();
        var root = new Border { Resources = resources, Child = child };
        TextElement.SetForeground(root, Brushes.Orange);

        var engine = new DynamicThemeEngine();
        engine.RegisterTheme(ThemeWithSyntax("syntax-test"));
        engine.AttachResourceRoot(root.Resources);

        ThemeBrush.SetForeground(child, "CodeTypeBrush");
        Assert.Equal(Color.Parse("#4EC9B0"), ((ISolidColorBrush)child.Foreground!).Color);

        engine.ApplyTheme("syntax-test");
        Assert.Equal(Color.Parse("#11AA22"), ((ISolidColorBrush)child.Foreground!).Color);

        engine.ApplyTheme(BuiltInThemes.DarkPlus.Id);
        Assert.Equal(Color.Parse("#4EC9B0"), ((ISolidColorBrush)child.Foreground!).Color);

        // A key nothing defines leaves the inherited foreground (it used to give a transparent brush: invisible text).
        ThemeBrush.SetForeground(child, "NoSuchBrush");
        Assert.Same(Brushes.Orange, child.Foreground);
        engine.DetachResourceRoot(root.Resources);
    }

    // ── Server cell badges: keys the view resolves, so they follow the theme ──

    private static IEnumerable<FryServerCellViewModel> EveryKindOfServerCell()
    {
        foreach (var method in new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD", "ANY" })
        {
            yield return new FryServerCellViewModel(new FryServerCellItem { Type = FryServerCellType.Endpoint, Method = method });
        }

        foreach (var type in Enum.GetValues<FryServerCellType>().Where(t => t != FryServerCellType.Endpoint))
        {
            yield return new FryServerCellViewModel(new FryServerCellItem { Type = type });
        }
    }

    [Fact]
    public void EveryServerBadgeColour_IsAThemeResource_InDarkAndLight_AndInGeneratedThemes()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "Styles", "Tokens", "StudioPaletteTokens.axaml"));
        int dark = text.IndexOf("<ResourceDictionary x:Key=\"Dark\">", StringComparison.Ordinal);
        int light = text.IndexOf("<ResourceDictionary x:Key=\"Light\">", StringComparison.Ordinal);
        var darkKeys = ResourceKey.Matches(text[dark..light]).Select(m => m.Groups[1].Value).ToHashSet();
        var generated = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 120 });

        foreach (var cell in EveryKindOfServerCell())
        {
            foreach (var key in new[] { cell.MethodBadgeBgKey, cell.MethodBadgeFgKey, cell.MethodBadgeBorderKey, cell.TypeBadgeBgKey, cell.TypeBadgeFgKey, cell.TypeBadgeBorderKey })
            {
                Assert.True(darkKeys.Contains(key), $"{cell.Type} {cell.Method}: {key} is not in the palette");
                Assert.True(generated.Colors.ContainsKey(key), $"{cell.Type} {cell.Method}: {key} is not in a generated theme");
            }
        }

        Assert.Equal("ServerMethodPostFgBrush", EveryKindOfServerCell().Single(c => c.Method == "POST").TypeBadgeFgKey);
        Assert.Equal("ServerTypeJobBorderBrush", EveryKindOfServerCell().Single(c => c.Type == FryServerCellType.Background).TypeBadgeBorderKey);
    }

    [Fact]
    public void ChangingAServerCellsMethodOrType_AnnouncesEveryBadgeColour()
    {
        var cell = new FryServerCellViewModel(new FryServerCellItem { Type = FryServerCellType.Endpoint, Method = "GET" });
        var changed = new List<string?>();
        cell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        cell.Method = "DELETE";
        Assert.Equal("ServerMethodDeleteBgBrush", cell.MethodBadgeBgKey);
        foreach (var name in new[] { nameof(cell.MethodBadgeBgKey), nameof(cell.MethodBadgeFgKey), nameof(cell.MethodBadgeBorderKey), nameof(cell.TypeBadgeBgKey), nameof(cell.TypeBadgeFgKey), nameof(cell.TypeBadgeBorderKey), nameof(cell.TypeBadgeText) })
        {
            Assert.Contains(name, changed);
        }

        changed.Clear();
        cell.Type = FryServerCellType.Middleware;
        Assert.Equal("ServerTypeMiddlewareBgBrush", cell.TypeBadgeBgKey);
        Assert.Contains(nameof(cell.TypeBadgeBorderKey), changed);
    }

    [Fact]
    public void ThemeBrush_BackgroundAndBorder_FollowTheTheme_AndAMissingKeyLeavesThemUnset()
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            ["ServerMethodGetBgBrush"] = new SolidColorBrush(Color.Parse("#162846")),
            ["ServerMethodGetBorderBrush"] = new SolidColorBrush(Color.Parse("#2563EB")),
        });
        var badge = new Border();
        var root = new Border { Resources = resources, Child = badge };

        var theme = new ThemeDefinition
        {
            Id = "badge-test",
            Name = "badge-test",
            IsDark = true,
            Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ServerMethodGetBgBrush"] = "#101010",
                ["ServerMethodGetBorderBrush"] = "#ABCDEF",
            },
        };
        var engine = new DynamicThemeEngine();
        engine.RegisterTheme(theme);
        engine.AttachResourceRoot(root.Resources);

        ThemeBrush.SetBackground(badge, "ServerMethodGetBgBrush");
        ThemeBrush.SetBorderBrush(badge, "ServerMethodGetBorderBrush");
        Assert.Equal(Color.Parse("#162846"), ((ISolidColorBrush)badge.Background!).Color);
        Assert.Equal(Color.Parse("#2563EB"), ((ISolidColorBrush)badge.BorderBrush!).Color);

        engine.ApplyTheme("badge-test");
        Assert.Equal(Color.Parse("#101010"), ((ISolidColorBrush)badge.Background!).Color);
        Assert.Equal(Color.Parse("#ABCDEF"), ((ISolidColorBrush)badge.BorderBrush!).Color);

        ThemeBrush.SetBackground(badge, "NoSuchBrush");
        Assert.Null(badge.Background);
        engine.DetachResourceRoot(root.Resources);
    }
}
