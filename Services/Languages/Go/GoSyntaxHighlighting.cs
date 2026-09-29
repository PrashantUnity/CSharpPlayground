using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Go syntax colors in the studio's two themes, VS Code's Dark+ and Light+: keywords, types,
/// built-in functions, strings, raw string literals, comments, and numbers.
/// </summary>
public static class GoSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Control,
        string Storage, string Function, string Type, string Number, string Builtin);

    private static readonly Palette Dark = new(
        "Go Dark",
        Comment: "#6A9955",
        String: "#CE9178",
        Escape: "#D7BA7D",
        Control: "#C586C0",
        Storage: "#569CD6",
        Function: "#DCDCAA",
        Type: "#4EC9B0",
        Number: "#B5CEA8",
        Builtin: "#569CD6");

    private static readonly Palette Light = new(
        "Go Light",
        Comment: "#008000",
        String: "#A31515",
        Escape: "#EE0000",
        Control: "#AF00DB",
        Storage: "#0000FF",
        Function: "#795E26",
        Type: "#267F99",
        Number: "#098658",
        Builtin: "#0000FF");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".go" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />
            <Color name="Builtin" foreground="{{{p.Builtin}}}" />

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

            <RuleSet>
                <Span color="Comment" multiline="true" ruleSet="CommentMarkers">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>//</Begin>
                </Span>

                <!-- Go Raw String Literals (backticks, multiline) -->
                <Span color="String" multiline="true">
                    <Begin>`</Begin>
                    <End>`</End>
                </Span>

                <!-- Go Double-quoted Strings -->
                <Span color="String" ruleSet="Escapes">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>

                <!-- Go Rune Literals -->
                <Span color="String" ruleSet="Escapes">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <!-- Control Keywords -->
                <Keywords color="Control">
                    <Word>break</Word>
                    <Word>case</Word>
                    <Word>continue</Word>
                    <Word>default</Word>
                    <Word>defer</Word>
                    <Word>else</Word>
                    <Word>fallthrough</Word>
                    <Word>for</Word>
                    <Word>go</Word>
                    <Word>goto</Word>
                    <Word>if</Word>
                    <Word>range</Word>
                    <Word>return</Word>
                    <Word>select</Word>
                    <Word>switch</Word>
                </Keywords>

                <!-- Storage / Declaration Keywords -->
                <Keywords color="Storage">
                    <Word>chan</Word>
                    <Word>const</Word>
                    <Word>func</Word>
                    <Word>import</Word>
                    <Word>interface</Word>
                    <Word>map</Word>
                    <Word>package</Word>
                    <Word>struct</Word>
                    <Word>type</Word>
                    <Word>var</Word>
                </Keywords>

                <!-- Built-in Constants & Literals -->
                <Keywords color="Builtin">
                    <Word>true</Word>
                    <Word>false</Word>
                    <Word>iota</Word>
                    <Word>nil</Word>
                </Keywords>

                <!-- Built-in Functions -->
                <Keywords color="Function">
                    <Word>append</Word>
                    <Word>cap</Word>
                    <Word>clear</Word>
                    <Word>close</Word>
                    <Word>complex</Word>
                    <Word>copy</Word>
                    <Word>delete</Word>
                    <Word>imag</Word>
                    <Word>len</Word>
                    <Word>make</Word>
                    <Word>max</Word>
                    <Word>min</Word>
                    <Word>new</Word>
                    <Word>panic</Word>
                    <Word>print</Word>
                    <Word>println</Word>
                    <Word>real</Word>
                    <Word>recover</Word>
                </Keywords>

                <!-- Primitive & Built-in Types -->
                <Keywords color="Type">
                    <Word>any</Word>
                    <Word>bool</Word>
                    <Word>byte</Word>
                    <Word>comparable</Word>
                    <Word>complex64</Word>
                    <Word>complex128</Word>
                    <Word>error</Word>
                    <Word>float32</Word>
                    <Word>float64</Word>
                    <Word>int</Word>
                    <Word>int8</Word>
                    <Word>int16</Word>
                    <Word>int32</Word>
                    <Word>int64</Word>
                    <Word>rune</Word>
                    <Word>string</Word>
                    <Word>uint</Word>
                    <Word>uint8</Word>
                    <Word>uint16</Word>
                    <Word>uint32</Word>
                    <Word>uint64</Word>
                    <Word>uintptr</Word>
                </Keywords>

                <!-- Numbers -->
                <Rule color="Number">
                    \b0[xX][0-9a-fA-F_]+(\.[0-9a-fA-F_]+)?([pP][+-]?[0-9_]+)?\b
                    | \b0[bB][01_]+\b
                    | \b0[oO]?[0-7_]+\b
                    | \b[0-9_]+(\.[0-9_]+)?([eE][+-]?[0-9_]+)?i?\b
                </Rule>

                <!-- Function Declarations / Calls -->
                <Rule color="Function">
                    \b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\()
                </Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
