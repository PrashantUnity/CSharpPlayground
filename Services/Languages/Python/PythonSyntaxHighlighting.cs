using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Python syntax colors in the studio's two themes, VS Code's Dark+ and Light+ like the C# ones: strings with their
/// prefixes (r, b, f…) and f-string replacement fields, triple-quoted strings over several lines, decorators, the names
/// after <c>def</c> and <c>class</c>, calls, builtins, numbers with underscores, and TODO markers in comments.
/// </summary>
public static class PythonSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Interpolation, string Control,
        string Storage, string Self, string Function, string Type, string Number);

    private static readonly Palette Dark = new("Python Dark", "#6A9955", "#CE9178", "#D7BA7D", "#9CDCFE", "#C586C0",
        "#569CD6", "#9CDCFE", "#DCDCAA", "#4EC9B0", "#B5CEA8");

    private static readonly Palette Light = new("Python Light", "#008000", "#A31515", "#EE0000", "#001080", "#AF00DB",
        "#0000FF", "#001080", "#795E26", "#267F99", "#098658");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".py" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Interpolation" foreground="{{{p.Interpolation}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Self" foreground="{{{p.Self}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />

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

            <!-- f"{value!r:>10}": a replacement field is code; "{{" is a literal brace. -->
            <RuleSet name="FString">
                <Span color="Escape" begin="\\" end="." />
                <Span begin="\{\{" end="" />
                <Span color="Interpolation" begin="\{" end="\}" ruleSet="" />
            </RuleSet>

            <RuleSet>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>\#</Begin>
                </Span>

                <!-- Triple-quoted first: they start with the same quote as the one-line kinds. -->
                <Span color="String" multiline="true" ruleSet="FString">
                    <Begin>\b(?i:rf|fr|f)"""</Begin>
                    <End>"""</End>
                </Span>
                <Span color="String" multiline="true" ruleSet="FString">
                    <Begin>\b(?i:rf|fr|f)'''</Begin>
                    <End>'''</End>
                </Span>
                <Span color="String" multiline="true" ruleSet="Escapes">
                    <Begin>(?:\b(?i:rb|br|r|b|u))?"""</Begin>
                    <End>"""</End>
                </Span>
                <Span color="String" multiline="true" ruleSet="Escapes">
                    <Begin>(?:\b(?i:rb|br|r|b|u))?'''</Begin>
                    <End>'''</End>
                </Span>

                <Span color="String" ruleSet="FString">
                    <Begin>\b(?i:rf|fr|f)"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="FString">
                    <Begin>\b(?i:rf|fr|f)'</Begin>
                    <End>'</End>
                </Span>
                <Span color="String" ruleSet="Escapes">
                    <Begin>(?:\b(?i:rb|br|r|b|u))?"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="Escapes">
                    <Begin>(?:\b(?i:rb|br|r|b|u))?'</Begin>
                    <End>'</End>
                </Span>

                <Rule color="Function">@[A-Za-z_][\w.]*</Rule>
                <Rule color="Function">(?&lt;=\bdef\s+)[A-Za-z_]\w*</Rule>
                <Rule color="Type">(?&lt;=\bclass\s+)[A-Za-z_]\w*</Rule>

                <Keywords color="Control">
                    <Word>if</Word><Word>elif</Word><Word>else</Word><Word>for</Word><Word>while</Word><Word>break</Word>
                    <Word>continue</Word><Word>return</Word><Word>pass</Word><Word>raise</Word><Word>try</Word><Word>except</Word>
                    <Word>finally</Word><Word>with</Word><Word>yield</Word><Word>await</Word><Word>async</Word><Word>match</Word>
                    <Word>case</Word><Word>del</Word><Word>assert</Word><Word>import</Word><Word>from</Word><Word>as</Word>
                    <Word>global</Word><Word>nonlocal</Word>
                </Keywords>
                <Keywords color="Storage">
                    <Word>def</Word><Word>class</Word><Word>lambda</Word>
                    <Word>and</Word><Word>or</Word><Word>not</Word><Word>in</Word><Word>is</Word>
                    <Word>True</Word><Word>False</Word><Word>None</Word><Word>NotImplemented</Word><Word>Ellipsis</Word><Word>__debug__</Word>
                </Keywords>
                <Keywords color="Self">
                    <Word>self</Word><Word>cls</Word>
                </Keywords>
                <Keywords color="Type">
                    <Word>int</Word><Word>float</Word><Word>complex</Word><Word>str</Word><Word>bytes</Word><Word>bytearray</Word>
                    <Word>memoryview</Word><Word>list</Word><Word>tuple</Word><Word>dict</Word><Word>set</Word><Word>frozenset</Word>
                    <Word>bool</Word><Word>object</Word><Word>type</Word><Word>range</Word><Word>slice</Word>
                    <Word>BaseException</Word><Word>Exception</Word><Word>ValueError</Word><Word>TypeError</Word><Word>KeyError</Word>
                    <Word>IndexError</Word><Word>AttributeError</Word><Word>RuntimeError</Word><Word>StopIteration</Word>
                    <Word>ZeroDivisionError</Word><Word>NotImplementedError</Word><Word>OSError</Word><Word>FileNotFoundError</Word>
                    <Word>ImportError</Word><Word>ModuleNotFoundError</Word><Word>NameError</Word><Word>AssertionError</Word>
                    <Word>ArithmeticError</Word><Word>LookupError</Word><Word>PermissionError</Word><Word>TimeoutError</Word>
                    <Word>KeyboardInterrupt</Word><Word>SystemExit</Word>
                </Keywords>
                <Keywords color="Function">
                    <Word>abs</Word><Word>all</Word><Word>any</Word><Word>ascii</Word><Word>bin</Word><Word>breakpoint</Word>
                    <Word>callable</Word><Word>chr</Word><Word>classmethod</Word><Word>compile</Word><Word>delattr</Word><Word>dir</Word>
                    <Word>divmod</Word><Word>enumerate</Word><Word>eval</Word><Word>exec</Word><Word>filter</Word><Word>format</Word>
                    <Word>getattr</Word><Word>globals</Word><Word>hasattr</Word><Word>hash</Word><Word>help</Word><Word>hex</Word>
                    <Word>id</Word><Word>input</Word><Word>isinstance</Word><Word>issubclass</Word><Word>iter</Word><Word>len</Word>
                    <Word>locals</Word><Word>map</Word><Word>max</Word><Word>min</Word><Word>next</Word><Word>oct</Word><Word>open</Word>
                    <Word>ord</Word><Word>pow</Word><Word>print</Word><Word>property</Word><Word>repr</Word><Word>reversed</Word>
                    <Word>round</Word><Word>setattr</Word><Word>sorted</Word><Word>staticmethod</Word><Word>sum</Word><Word>super</Word>
                    <Word>vars</Word><Word>zip</Word><Word>__import__</Word>
                </Keywords>

                <Rule color="Number">\b0[xX][0-9a-fA-F_]+\b|\b0[bB][01_]+\b|\b0[oO][0-7_]+\b|(\b\d[\d_]*(\.[\d_]*)?|\.\d[\d_]*)([eE][+-]?\d[\d_]*)?[jJ]?\b</Rule>
                <Rule color="Function">\b[A-Za-z_]\w*(?=\s*\()</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
