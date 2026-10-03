using System;
using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DartSupportExtension;

/// <summary>
/// Syntax highlighting definitions for Dart code conforming to VS Code Dark+ and Light+ themes.
/// </summary>
public static class DartSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string DocComment, string String, string Escape,
        string Interpolation, string Control, string Storage, string Keyword,
        string Type, string Function, string Number, string Annotation);

    private static readonly Palette Dark = new(
        "Dart Dark", "#6A9955", "#608B4E", "#CE9178", "#D7BA7D",
        "#9CDCFE", "#C586C0", "#569CD6", "#569CD6",
        "#4EC9B0", "#DCDCAA", "#B5CEA8", "#DCDCAA");

    private static readonly Palette Light = new(
        "Dart Light", "#008000", "#006400", "#A31515", "#EE0000",
        "#001080", "#AF00DB", "#0000FF", "#0000FF",
        "#267F99", "#795E26", "#098658", "#795E26");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette p)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(p)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".dart" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="DocComment" foreground="{{{p.DocComment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Interpolation" foreground="{{{p.Interpolation}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Keyword" foreground="{{{p.Keyword}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />
            <Color name="Annotation" foreground="{{{p.Annotation}}}" />

            <RuleSet name="CommentMarkers">
                <Keywords fontWeight="bold" foreground="#F59E0B">
                    <Word>TODO</Word>
                    <Word>FIXME</Word>
                </Keywords>
                <Keywords fontWeight="bold" foreground="#3B82F6">
                    <Word>HACK</Word>
                    <Word>NOTE</Word>
                </Keywords>
            </RuleSet>

            <RuleSet name="Escapes">
                <Span color="Escape" begin="\\" end="." />
            </RuleSet>

            <RuleSet name="StringInterpolation">
                <Span color="Escape" begin="\\" end="." />
                <Span color="Interpolation" begin="\$\{" end="\}" ruleSet="" />
                <Rule color="Interpolation">\$[a-zA-Z_]\w*</Rule>
            </RuleSet>

            <RuleSet>
                <Span color="DocComment" ruleSet="CommentMarkers">
                    <Begin>///</Begin>
                </Span>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>//</Begin>
                </Span>
                <Span color="Comment" multiline="true" ruleSet="CommentMarkers">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>

                <!-- Raw Strings -->
                <Span color="String" multiline="true">
                    <Begin>r"""</Begin>
                    <End>"""</End>
                </Span>
                <Span color="String" multiline="true">
                    <Begin>r'''</Begin>
                    <End>'''</End>
                </Span>
                <Span color="String">
                    <Begin>r"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String">
                    <Begin>r'</Begin>
                    <End>'</End>
                </Span>

                <!-- Multi-line Strings with Interpolation -->
                <Span color="String" multiline="true" ruleSet="StringInterpolation">
                    <Begin>"""</Begin>
                    <End>"""</End>
                </Span>
                <Span color="String" multiline="true" ruleSet="StringInterpolation">
                    <Begin>'''</Begin>
                    <End>'''</End>
                </Span>

                <!-- Standard Strings -->
                <Span color="String" ruleSet="StringInterpolation">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="StringInterpolation">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <Rule color="Annotation">@[a-zA-Z_]\w*</Rule>
                <Rule color="Number">\b0[xX][0-9a-fA-F]+\b|\b\d+(\.\d+)?([eE][+-]?\d+)?\b</Rule>

                <Keywords color="Control">
                    <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>default</Word>
                    <Word>break</Word><Word>continue</Word><Word>return</Word><Word>for</Word><Word>while</Word>
                    <Word>do</Word><Word>try</Word><Word>catch</Word><Word>finally</Word><Word>throw</Word>
                    <Word>rethrow</Word><Word>yield</Word><Word>await</Word><Word>async</Word><Word>when</Word>
                </Keywords>

                <Keywords color="Storage">
                    <Word>var</Word><Word>final</Word><Word>const</Word><Word>late</Word><Word>static</Word>
                    <Word>external</Word><Word>factory</Word><Word>operator</Word><Word>typedef</Word><Word>class</Word>
                    <Word>mixin</Word><Word>extension</Word><Word>enum</Word><Word>interface</Word><Word>abstract</Word>
                    <Word>base</Word><Word>sealed</Word><Word>required</Word><Word>covariant</Word><Word>extends</Word>
                    <Word>with</Word><Word>implements</Word><Word>new</Word><Word>import</Word><Word>export</Word>
                    <Word>part</Word><Word>library</Word><Word>show</Word><Word>hide</Word><Word>as</Word>
                    <Word>is</Word><Word>in</Word><Word>get</Word><Word>set</Word>
                </Keywords>

                <Keywords color="Keyword">
                    <Word>this</Word><Word>super</Word><Word>true</Word><Word>false</Word><Word>null</Word>
                </Keywords>

                <Keywords color="Type">
                    <Word>void</Word><Word>dynamic</Word><Word>Never</Word><Word>Object</Word><Word>int</Word>
                    <Word>double</Word><Word>num</Word><Word>String</Word><Word>bool</Word><Word>List</Word>
                    <Word>Map</Word><Word>Set</Word><Word>Record</Word><Word>Runes</Word><Word>Symbol</Word>
                    <Word>DateTime</Word><Word>Duration</Word><Word>Uri</Word><Word>Future</Word><Word>Stream</Word>
                    <Word>Iterable</Word><Word>Function</Word><Word>Type</Word><Word>BigInt</Word><Word>Pattern</Word>
                    <Word>Match</Word><Word>RegExp</Word><Word>StringBuffer</Word>
                </Keywords>

                <Rule color="Type">\b[A-Z][a-zA-Z0-9_]*\b</Rule>
                <Rule color="Function">\b[a-zA-Z_]\w*(?=\s*\()</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
