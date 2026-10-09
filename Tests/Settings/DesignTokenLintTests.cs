using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Keeps the studio on its design tokens: no new hard-coded font sizes, weights, radii, borders or code fonts in the
/// views and styles, shared styles set those properties only through token keys, and every key exists. Without this,
/// new UI would quietly ignore the Layout &amp; Typography levers. To fix a failure, use a key:
/// <c>lt:Tokens.FontSize="DsFontSize200"</c>, or <c>python3 tools/token_migrate.py &lt;file&gt;</c>.
/// </summary>
public class DesignTokenLintTests
{
    private static readonly string[] Folders = ["Views", "Controls", "Styles", "Runner", "Charting", "Charting3D"];

    // Literals that are geometry rather than layout: circles and rings stay round whatever the radius lever says.
    private static readonly (string File, string Literal, string Why)[] Allowed =
    [
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "CornerRadius=\"32\"", "spinner ring: a circle"),
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "CornerRadius=\"37\"", "spinner ring: a circle"),
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "CornerRadius=\"34\"", "spinner ring: a circle"),
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "CornerRadius=\"28\"", "spinner ring: a circle"),
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "BorderThickness=\"1.5,0,0,0\"", "spinner arc"),
        ("Controls/Studio/StudioLoadingOverlayControl.axaml", "BorderThickness=\"2.5,2.5,0,0\"", "spinner arc"),
        ("Controls/Hub/HubWorkspacesPanelControl.axaml", "CornerRadius=\"26\"", "52 px avatar: a circle"),
        ("Runner/AboutWindow.axaml", "CornerRadius=\"36\"", "72 px logo: a circle"),
        ("Controls/Visuals/VisualChromeControl.axaml", "CornerRadius=\"1.5\"", "3 px grip bar: a capsule"),
        ("Controls/BlindProblems/BlindProblemsStatsControl.axaml", "CornerRadius=\"2.5\"", "5 px progress bar: a capsule"),
        ("Controls/Settings/ThemeLiveChromePreviewControl.axaml", "BorderThickness=\"0,0,0,2\"", "the preview's active-tab underline"),
    ];

    // Where {DynamicResource <layout token>} is allowed: the Layout page's preview, which previews unapplied values.
    private static readonly string[] PreviewFiles = ["Controls/Settings/LayoutSpecimenControl.axaml"];

    private static readonly HashSet<string> LayoutKeys = LayoutTokenMapper.Map(LayoutSpec.Default, isDark: true).Keys.ToHashSet(StringComparer.Ordinal);

    private static readonly Regex LiteralFontSize = new(@"(?<![\w.:])FontSize=""[0-9.]+""");
    private static readonly Regex LiteralWeight = new(@"(?<![\w.:])FontWeight=""(Medium|SemiBold|DemiBold|Bold|ExtraBold|UltraBold|Black|Heavy)""");
    private static readonly Regex LiteralRadius = new(@"(?<![\w.:])CornerRadius=""(?!0""|0,0,0,0"")[0-9.,\s]+""");
    private static readonly Regex LiteralBorder = new(@"(?<![\w.:])BorderThickness=""(1|1,1,1,1|0,0,0,1|0,1,0,0|1,0,0,0|0,0,1,0|1\.5|1,1,1,0|2,0,0,0)""");
    private static readonly Regex MonoFamily = new(@"(?<![\w.:])FontFamily=""[^""{]*(?i:mono|consolas|menlo|cascadia|courier|fira code|jetbrains)[^""]*""");
    private static readonly Regex DirectStyleSetter = new(@"<Setter\s+Property=""(?:[\w]+\.)?(FontSize|FontWeight|FontFamily|LetterSpacing|CornerRadius|BorderThickness|BoxShadow)""");
    private static readonly Regex LayoutResource = new(@"(?<![\w.:])(FontSize|FontWeight|FontFamily|LetterSpacing|CornerRadius|BorderThickness|Padding|Height|MinHeight|Spacing|BoxShadow)=""\{DynamicResource (\w+)\}""");
    private static readonly Regex KeyAttribute = new(@"lt:Tokens\.(\w+)=""([^""]*)""");
    private static readonly Regex KeySetter = new(@"Property=""lt:Tokens\.(\w+)""\s+Value=""([^""]*)""");

    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.csproj"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Every rule broken in one file's text (line: problem).</summary>
    public static List<string> Check(string relativePath, string text)
    {
        var problems = new List<string>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            void Flag(Regex rule, string what)
            {
                foreach (Match m in rule.Matches(line))
                {
                    if (Allowed.Any(a => a.File == relativePath && m.Value.Contains(a.Literal, StringComparison.Ordinal))) continue;
                    problems.Add($"{relativePath}:{i + 1}: {what}: {m.Value.Trim()}");
                }
            }

            Flag(LiteralFontSize, "hard-coded font size (use lt:Tokens.FontSize)");
            Flag(LiteralWeight, "hard-coded font weight (use lt:Tokens.FontWeight)");
            Flag(LiteralRadius, "hard-coded corner radius (use lt:Tokens.CornerRadius)");
            Flag(LiteralBorder, "hard-coded border width (use lt:Tokens.BorderThickness)");
            Flag(MonoFamily, "hard-coded code font (use lt:Tokens.FontFamily=\"DsCodeFontFamily\")");
            Flag(DirectStyleSetter, "style sets a layout property directly (use Property=\"lt:Tokens.…\", or Value=\"=…\" for a fixed value)");
            if (!PreviewFiles.Contains(relativePath))
            {
                foreach (Match m in LayoutResource.Matches(line))
                {
                    if (LayoutKeys.Contains(m.Groups[2].Value)) problems.Add($"{relativePath}:{i + 1}: layout token as a dynamic resource (use lt:Tokens.{m.Groups[1].Value}): {m.Value}");
                }
            }

            foreach (var rule in new[] { KeyAttribute, KeySetter })
            {
                foreach (Match m in rule.Matches(line))
                {
                    var key = m.Groups[2].Value;
                    if (key.Length == 0 || key.StartsWith('=') || LayoutKeys.Contains(key)) continue;
                    problems.Add($"{relativePath}:{i + 1}: unknown layout token '{key}'");
                }
            }
        }

        return problems;
    }

    [Fact]
    public void TheStudiosViewsAndStyles_UseLayoutTokens()
    {
        var root = Root();
        var problems = new List<string>();
        var files = 0;
        foreach (var folder in Folders)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.EnumerateFiles(path, "*.axaml", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.StartsWith("Styles/Tokens/", StringComparison.Ordinal) || relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                files++;
                problems.AddRange(Check(relative, File.ReadAllText(file)));
            }
        }

        Assert.True(files > 80, $"only {files} files scanned");
        Assert.True(problems.Count == 0, $"{problems.Count} design-token problems:\n" + string.Join("\n", problems.Take(60)));
    }

    [Fact]
    public void EveryAllowedLiteral_StillExists_SoTheListDoesntRot()
    {
        var root = Root();
        foreach (var (file, literal, why) in Allowed)
        {
            Assert.True(File.ReadAllText(Path.Combine(root, file)).Contains(literal, StringComparison.Ordinal), $"{file}: {literal} ({why}) is gone; remove it from the list");
        }
    }

    [Theory]
    [InlineData("<TextBlock FontSize=\"11\" />", "font size")]
    [InlineData("<TextBlock FontWeight=\"SemiBold\" />", "font weight")]
    [InlineData("<Border CornerRadius=\"6\" />", "corner radius")]
    [InlineData("<Border BorderThickness=\"0,0,0,1\" />", "border width")]
    [InlineData("<TextBlock FontFamily=\"Consolas, monospace\" />", "code font")]
    [InlineData("<Setter Property=\"CornerRadius\" Value=\"0\" />", "style sets a layout property directly")]
    [InlineData("<TextBlock FontSize=\"{DynamicResource DsFontSize200}\" />", "dynamic resource")]
    [InlineData("<TextBlock lt:Tokens.FontSize=\"DsFontSize999\" />", "unknown layout token")]
    public void TheLint_CatchesEachKindOfLiteral(string xaml, string expected)
    {
        var problems = Check("Views/Sample.axaml", xaml);
        Assert.Contains(problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("<TextBlock lt:Tokens.FontSize=\"DsFontSize200\" FontWeight=\"Normal\" />")]
    [InlineData("<Border CornerRadius=\"0\" BorderThickness=\"0\" lt:Tokens.CornerRadius=\"=0,2,1,0\" />")]
    [InlineData("<Setter Property=\"lt:Tokens.CornerRadius\" Value=\"DsRadiusMD\" />")]
    [InlineData("<Border Background=\"{DynamicResource DsBorderBrush}\" />")]
    public void TheLint_LetsTokensAndColoursThrough(string xaml) => Assert.Empty(Check("Views/Sample.axaml", xaml));
}
