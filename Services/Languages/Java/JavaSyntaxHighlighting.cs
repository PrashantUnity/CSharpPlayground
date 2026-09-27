using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Java syntax colors in the studio's two themes, VS Code's Dark+ and Light+: keywords, annotations,
/// types, strings, text blocks, Javadoc, methods, and numbers.
/// </summary>
public static class JavaSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Control,
        string Storage, string Self, string Function, string Type, string Number, string Annotation);

    private static readonly Palette Dark = new("Java Dark", "#6A9955", "#CE9178", "#D7BA7D", "#C586C0",
        "#569CD6", "#569CD6", "#DCDCAA", "#4EC9B0", "#B5CEA8", "#DCDCAA");

    private static readonly Palette Light = new("Java Light", "#008000", "#A31515", "#EE0000", "#AF00DB",
        "#0000FF", "#0000FF", "#795E26", "#267F99", "#098658", "#795E26");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".java" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Self" foreground="{{{p.Self}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
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

            <RuleSet name="JavadocTags">
                <Keywords fontWeight="bold" foreground="{{{p.Annotation}}}">
                    <Word>@param</Word>
                    <Word>@return</Word>
                    <Word>@throws</Word>
                    <Word>@exception</Word>
                    <Word>@see</Word>
                    <Word>@since</Word>
                    <Word>@author</Word>
                    <Word>@version</Word>
                    <Word>@deprecated</Word>
                </Keywords>
            </RuleSet>

            <RuleSet name="Escapes">
                <Span color="Escape" begin="\\" end="." />
            </RuleSet>

            <RuleSet>
                <Span color="Comment" multiline="true" ruleSet="JavadocTags">
                    <Begin>/\*\*</Begin>
                    <End>\*/</End>
                </Span>
                <Span color="Comment" multiline="true" ruleSet="CommentMarkers">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>//</Begin>
                </Span>

                <!-- Java 15+ Text Blocks -->
                <Span color="String" multiline="true" ruleSet="Escapes">
                    <Begin>"""</Begin>
                    <End>"""</End>
                </Span>

                <Span color="String" ruleSet="Escapes">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="Escapes">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <Rule color="Annotation">@[A-Za-z_$]\w*</Rule>

                <Keywords color="Control">
                    <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>default</Word>
                    <Word>break</Word><Word>continue</Word><Word>return</Word><Word>for</Word><Word>while</Word>
                    <Word>do</Word><Word>try</Word><Word>catch</Word><Word>finally</Word><Word>throw</Word><Word>throws</Word>
                    <Word>yield</Word><Word>assert</Word>
                </Keywords>

                <Keywords color="Storage">
                    <Word>public</Word><Word>protected</Word><Word>private</Word><Word>static</Word><Word>final</Word>
                    <Word>abstract</Word><Word>synchronized</Word><Word>native</Word><Word>transient</Word><Word>volatile</Word>
                    <Word>strictfp</Word><Word>sealed</Word><Word>non-sealed</Word><Word>permits</Word>
                    <Word>class</Word><Word>interface</Word><Word>record</Word><Word>enum</Word>
                    <Word>extends</Word><Word>implements</Word><Word>package</Word><Word>import</Word>
                    <Word>module</Word><Word>requires</Word><Word>exports</Word><Word>provides</Word><Word>opens</Word><Word>uses</Word><Word>to</Word><Word>with</Word>
                    <Word>new</Word><Word>instanceof</Word>
                </Keywords>

                <Keywords color="Self">
                    <Word>this</Word><Word>super</Word>
                    <Word>true</Word><Word>false</Word><Word>null</Word>
                </Keywords>

                <Keywords color="Type">
                    <Word>void</Word><Word>boolean</Word><Word>byte</Word><Word>char</Word><Word>short</Word><Word>int</Word><Word>long</Word><Word>float</Word><Word>double</Word><Word>var</Word>
                    <Word>String</Word><Word>Object</Word><Word>Class</Word><Word>System</Word><Word>Thread</Word><Word>Runnable</Word>
                    <Word>Exception</Word><Word>RuntimeException</Word><Word>Throwable</Word><Word>Error</Word>
                    <Word>List</Word><Word>ArrayList</Word><Word>Map</Word><Word>HashMap</Word><Word>Set</Word><Word>HashSet</Word>
                    <Word>Optional</Word><Word>Stream</Word><Word>Arrays</Word><Word>Collections</Word><Word>Objects</Word>
                </Keywords>

                <Rule color="Number">\b0[xX][0-9a-fA-F_]+[lL]?\b|\b0[bB][01_]+[lL]?\b|(\b\d[\d_]*(\.[\d_]*)?|\.\d[\d_]*)([eE][+-]?\d[\d_]*)?[fFdDlL]?\b</Rule>
                <Rule color="Function">\b[A-Za-z_$]\w*(?=\s*\()</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
