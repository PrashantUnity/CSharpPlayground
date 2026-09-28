using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// JavaScript syntax colors in the studio's two themes, VS Code's Dark+ and Light+: template literals with expressions,
/// single and double quoted strings, regexes, keywords, functions, classes, built-ins, and numbers.
/// </summary>
public static class JavaScriptSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Interpolation, string Control,
        string Storage, string Self, string Function, string Type, string Number, string Regex);

    private static readonly Palette Dark = new("JavaScript Dark", "#6A9955", "#CE9178", "#D7BA7D", "#9CDCFE", "#C586C0",
        "#569CD6", "#569CD6", "#DCDCAA", "#4EC9B0", "#B5CEA8", "#D16969");

    private static readonly Palette Light = new("JavaScript Light", "#008000", "#A31515", "#EE0000", "#001080", "#AF00DB",
        "#0000FF", "#0000FF", "#795E26", "#267F99", "#098658", "#811F3F");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".js;.mjs;.cjs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
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
            <Color name="Regex" foreground="{{{p.Regex}}}" />

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

            <RuleSet name="TemplateLiteral">
                <Span color="Escape" begin="\\" end="." />
                <Span color="Interpolation" begin="\$\{" end="\}" ruleSet="" />
            </RuleSet>

            <RuleSet>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>//</Begin>
                </Span>
                <Span color="Comment" multiline="true" ruleSet="CommentMarkers">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>

                <Span color="String" multiline="true" ruleSet="TemplateLiteral">
                    <Begin>`</Begin>
                    <End>`</End>
                </Span>

                <Span color="String" ruleSet="Escapes">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="Escapes">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <Span color="Regex" ruleSet="Escapes">
                    <Begin>(?&lt;=[=(,;:!&amp;\|?\[{]\s*)/(?![/*])</Begin>
                    <End>/[gimsuy]*</End>
                </Span>

                <Rule color="Function">(?&lt;=\bfunction\s+)[A-Za-z_$]\w*</Rule>
                <Rule color="Type">(?&lt;=\bclass\s+)[A-Za-z_$]\w*</Rule>

                <Keywords color="Control">
                    <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>default</Word>
                    <Word>break</Word><Word>continue</Word><Word>return</Word><Word>for</Word><Word>while</Word>
                    <Word>do</Word><Word>try</Word><Word>catch</Word><Word>finally</Word><Word>throw</Word>
                    <Word>yield</Word><Word>await</Word><Word>async</Word><Word>import</Word><Word>export</Word>
                    <Word>from</Word><Word>as</Word><Word>with</Word><Word>debugger</Word>
                </Keywords>

                <Keywords color="Storage">
                    <Word>var</Word><Word>let</Word><Word>const</Word><Word>function</Word><Word>class</Word>
                    <Word>new</Word><Word>delete</Word><Word>typeof</Word><Word>void</Word><Word>instanceof</Word>
                    <Word>in</Word><Word>of</Word>
                </Keywords>

                <Keywords color="Self">
                    <Word>this</Word><Word>super</Word>
                    <Word>true</Word><Word>false</Word><Word>null</Word><Word>undefined</Word><Word>NaN</Word><Word>Infinity</Word>
                </Keywords>

                <Keywords color="Type">
                    <Word>Object</Word><Word>Array</Word><Word>String</Word><Word>Number</Word><Word>Boolean</Word>
                    <Word>Symbol</Word><Word>BigInt</Word><Word>Function</Word><Word>Math</Word><Word>Date</Word>
                    <Word>RegExp</Word><Word>Map</Word><Word>Set</Word><Word>WeakMap</Word><Word>WeakSet</Word>
                    <Word>Promise</Word><Word>Proxy</Word><Word>Reflect</Word><Word>JSON</Word><Word>Error</Word>
                    <Word>TypeError</Word><Word>RangeError</Word><Word>SyntaxError</Word><Word>ReferenceError</Word>
                    <Word>EvalError</Word><Word>URIError</Word><Word>ArrayBuffer</Word><Word>DataView</Word>
                    <Word>Int8Array</Word><Word>Uint8Array</Word><Word>Int16Array</Word><Word>Uint16Array</Word>
                    <Word>Int32Array</Word><Word>Uint32Array</Word><Word>Float32Array</Word><Word>Float64Array</Word>
                    <Word>console</Word><Word>process</Word><Word>globalThis</Word><Word>window</Word><Word>document</Word>
                </Keywords>

                <Rule color="Number">\b0[xX][0-9a-fA-F_]+n?\b|\b0[bB][01_]+n?\b|\b0[oO][0-7_]+n?\b|(\b\d[\d_]*(\.[\d_]*)?|\.\d[\d_]*)([eE][+-]?\d[\d_]*)?n?\b</Rule>
                <Rule color="Function">\b[A-Za-z_$]\w*(?=\s*\()</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
