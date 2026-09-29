using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Rust syntax colors in the studio's two themes, VS Code's Dark+ and Light+: keywords, primitive and standard types,
/// <c>name!</c> macros, lifetimes, attributes, raw and byte strings, char literals, numbers with their suffixes, doc
/// comments, and block comments that nest.
/// </summary>
public static class RustSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Control,
        string Storage, string Function, string Type, string Number, string Builtin, string Attribute);

    private static readonly Palette Dark = new(
        "Rust Dark",
        Comment: "#6A9955",
        String: "#CE9178",
        Escape: "#D7BA7D",
        Control: "#C586C0",
        Storage: "#569CD6",
        Function: "#DCDCAA",
        Type: "#4EC9B0",
        Number: "#B5CEA8",
        Builtin: "#569CD6",
        Attribute: "#9CDCFE");

    private static readonly Palette Light = new(
        "Rust Light",
        Comment: "#008000",
        String: "#A31515",
        Escape: "#EE0000",
        Control: "#AF00DB",
        Storage: "#0000FF",
        Function: "#795E26",
        Type: "#267F99",
        Number: "#098658",
        Builtin: "#0000FF",
        Attribute: "#001080");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    // A raw string's end depends on how many # opened it, and a regex can't count, so each depth from 0 to 3 has its own span.
    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".rs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />
            <Color name="Builtin" foreground="{{{p.Builtin}}}" />
            <Color name="Attribute" foreground="{{{p.Attribute}}}" />

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

            <RuleSet name="BlockComment">
                <Span color="Comment" multiline="true" ruleSet="BlockComment">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>
                <Import ruleSet="CommentMarkers" />
            </RuleSet>

            <RuleSet name="Escapes">
                <Span color="Escape" begin="\\" end="." />
            </RuleSet>

            <RuleSet name="AttributeBody">
                <Span color="String" multiline="true" ruleSet="Escapes">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>
            </RuleSet>

            <RuleSet>
                <!-- Comments: block comments nest, and /// and //! are documentation -->
                <Span color="Comment" multiline="true" ruleSet="BlockComment">
                    <Begin>/\*</Begin>
                    <End>\*/</End>
                </Span>
                <Span color="Comment" ruleSet="CommentMarkers">
                    <Begin>//</Begin>
                </Span>

                <!-- Attributes: #[derive(Debug)] and #![allow(unused)] -->
                <Span color="Attribute" multiline="true" ruleSet="AttributeBody">
                    <Begin>\#!?\[</Begin>
                    <End>\]</End>
                </Span>

                <!-- Raw strings (r"…", r#"…"#, r##"…"##, r###"…"###), also byte and C strings -->
                <Span color="String" multiline="true">
                    <Begin>\b(?:br|cr|r)"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" multiline="true">
                    <Begin>\b(?:br|cr|r)\#"</Begin>
                    <End>"\#</End>
                </Span>
                <Span color="String" multiline="true">
                    <Begin>\b(?:br|cr|r)\#\#"</Begin>
                    <End>"\#\#</End>
                </Span>
                <Span color="String" multiline="true">
                    <Begin>\b(?:br|cr|r)\#\#\#"</Begin>
                    <End>"\#\#\#</End>
                </Span>

                <!-- Strings (also b"…" and c"…"); they may span lines -->
                <Span color="String" multiline="true" ruleSet="Escapes">
                    <Begin>(?&lt;![A-Za-z0-9_])[bc]?"</Begin>
                    <End>"</End>
                </Span>

                <!-- Char and byte literals: 'a' '\n' '\u{1F600}' b'a' -->
                <Rule color="String">
                    (?&lt;![A-Za-z0-9_])b?'(?:\\(?:x[0-9a-fA-F]{2}|u\{[0-9a-fA-F_]+\}|.)|[^\\'\r\n])'
                </Rule>

                <!-- Lifetimes and loop labels: 'a 'static '_ 'outer -->
                <Rule color="Storage">
                    '[a-zA-Z_][a-zA-Z0-9_]*\b(?!')
                </Rule>

                <!-- Macros: println! vec! macro_rules! -->
                <Rule color="Function">
                    \b[a-zA-Z_][a-zA-Z0-9_]*!(?=\s*[\(\[\{])
                </Rule>

                <!-- Control Keywords -->
                <Keywords color="Control">
                    <Word>break</Word>
                    <Word>continue</Word>
                    <Word>else</Word>
                    <Word>for</Word>
                    <Word>if</Word>
                    <Word>in</Word>
                    <Word>loop</Word>
                    <Word>match</Word>
                    <Word>return</Word>
                    <Word>while</Word>
                    <Word>yield</Word>
                    <Word>await</Word>
                </Keywords>

                <!-- Storage / Declaration Keywords -->
                <Keywords color="Storage">
                    <Word>as</Word>
                    <Word>async</Word>
                    <Word>const</Word>
                    <Word>crate</Word>
                    <Word>dyn</Word>
                    <Word>enum</Word>
                    <Word>extern</Word>
                    <Word>fn</Word>
                    <Word>impl</Word>
                    <Word>let</Word>
                    <Word>mod</Word>
                    <Word>move</Word>
                    <Word>mut</Word>
                    <Word>pub</Word>
                    <Word>ref</Word>
                    <Word>self</Word>
                    <Word>Self</Word>
                    <Word>static</Word>
                    <Word>struct</Word>
                    <Word>super</Word>
                    <Word>trait</Word>
                    <Word>type</Word>
                    <Word>unsafe</Word>
                    <Word>use</Word>
                    <Word>where</Word>
                </Keywords>

                <!-- Built-in Constants & Literals -->
                <Keywords color="Builtin">
                    <Word>true</Word>
                    <Word>false</Word>
                    <Word>None</Word>
                    <Word>Some</Word>
                    <Word>Ok</Word>
                    <Word>Err</Word>
                </Keywords>

                <!-- Primitive Types -->
                <Keywords color="Type">
                    <Word>bool</Word>
                    <Word>char</Word>
                    <Word>f32</Word>
                    <Word>f64</Word>
                    <Word>i8</Word>
                    <Word>i16</Word>
                    <Word>i32</Word>
                    <Word>i64</Word>
                    <Word>i128</Word>
                    <Word>isize</Word>
                    <Word>str</Word>
                    <Word>u8</Word>
                    <Word>u16</Word>
                    <Word>u32</Word>
                    <Word>u64</Word>
                    <Word>u128</Word>
                    <Word>usize</Word>
                </Keywords>

                <!-- Numbers: 0xFF 0o77 0b1010 1_000u32 2.5e-3f64 -->
                <Rule color="Number">
                    \b0x[0-9a-fA-F_]+(?:[iu](?:8|16|32|64|128|size))?\b
                    | \b0o[0-7_]+(?:[iu](?:8|16|32|64|128|size))?\b
                    | \b0b[01_]+(?:[iu](?:8|16|32|64|128|size))?\b
                    | \b[0-9][0-9_]*(?:\.[0-9][0-9_]*)?(?:[eE][+-]?[0-9_]+)?(?:f32|f64|[iu](?:8|16|32|64|128|size))?\b
                </Rule>

                <!-- Types, traits and enum variants: CamelCase names (SCREAMING_CASE constants are left plain) -->
                <Rule color="Type">
                    \b[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*\b
                </Rule>

                <!-- Function Declarations / Calls (also turbofish: parse::&lt;i32&gt;() ) -->
                <Rule color="Function">
                    \b[a-z_][a-zA-Z0-9_]*(?=\s*(?:::&lt;[^&gt;\n]*&gt;)?\s*\()
                </Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
