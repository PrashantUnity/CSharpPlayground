using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// F# syntax colors in the studio's two themes, VS Code's Dark+ and Light+: keywords, types,
/// expressions, pipeline operators, strings, triple-quoted strings, comments, and numbers.
/// </summary>
public static class FSharpSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Control,
        string Storage, string Function, string Type, string Number, string Builtin);

    private static readonly Palette Dark = new(
        "F# Dark",
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
        "F# Light",
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
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".fsx;.fs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />
            <Color name="Builtin" foreground="{{{p.Builtin}}}" />

            <RuleSet>
                <!-- Line comments -->
                <Span color="Comment">
                    <Begin>//</Begin>
                </Span>

                <!-- Block comments (nested) -->
                <Span color="Comment" multiline="true">
                    <Begin>\(\*</Begin>
                    <End>\*\)</End>
                </Span>

                <!-- Triple-quoted strings -->
                <Span color="String" multiline="true">
                    <Begin>"""</Begin>
                    <End>"""</End>
                </Span>

                <!-- Verbatim strings -->
                <Span color="String" multiline="true">
                    <Begin>@"</Begin>
                    <End>"</End>
                    <RuleSet>
                        <Span color="Escape">
                            <Begin>""</Begin>
                        </Span>
                    </RuleSet>
                </Span>

                <!-- Interpolated strings -->
                <Span color="String">
                    <Begin>\$"</Begin>
                    <End>"</End>
                    <RuleSet>
                        <Span color="Escape">
                            <Begin>\\.</Begin>
                        </Span>
                    </RuleSet>
                </Span>

                <!-- Standard strings -->
                <Span color="String">
                    <Begin>"</Begin>
                    <End>"</End>
                    <RuleSet>
                        <Span color="Escape">
                            <Begin>\\.</Begin>
                        </Span>
                    </RuleSet>
                </Span>

                <!-- Character literals -->
                <Span color="String">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <!-- Numbers -->
                <Rule color="Number">
                    \b0[xX][0-9a-fA-F]+[uU]?[lL]?\b
                    |
                    \b0[bB][01]+[uU]?[lL]?\b
                    |
                    \b0[oO][0-7]+[uU]?[lL]?\b
                    |
                    \b[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?[fFmMdD]?\b
                    |
                    \b[0-9]+[uU]?[lL]?\b
                </Rule>

                <!-- Control flow keywords -->
                <Keywords color="Control">
                    <Word>if</Word>
                    <Word>then</Word>
                    <Word>else</Word>
                    <Word>elif</Word>
                    <Word>match</Word>
                    <Word>with</Word>
                    <Word>for</Word>
                    <Word>in</Word>
                    <Word>to</Word>
                    <Word>downto</Word>
                    <Word>do</Word>
                    <Word>while</Word>
                    <Word>yield</Word>
                    <Word>yield!</Word>
                    <Word>return</Word>
                    <Word>return!</Word>
                    <Word>try</Word>
                    <Word>finally</Word>
                    <Word>when</Word>
                    <Word>begin</Word>
                    <Word>end</Word>
                    <Word>raise</Word>
                    <Word>failwith</Word>
                </Keywords>

                <!-- Storage & definition keywords -->
                <Keywords color="Storage">
                    <Word>let</Word>
                    <Word>rec</Word>
                    <Word>mutable</Word>
                    <Word>fun</Word>
                    <Word>function</Word>
                    <Word>type</Word>
                    <Word>of</Word>
                    <Word>open</Word>
                    <Word>module</Word>
                    <Word>namespace</Word>
                    <Word>use</Word>
                    <Word>use!</Word>
                    <Word>member</Word>
                    <Word>static</Word>
                    <Word>val</Word>
                    <Word>inherit</Word>
                    <Word>interface</Word>
                    <Word>abstract</Word>
                    <Word>default</Word>
                    <Word>override</Word>
                    <Word>new</Word>
                    <Word>as</Word>
                    <Word>inline</Word>
                    <Word>lazy</Word>
                    <Word>and</Word>
                    <Word>struct</Word>
                    <Word>class</Word>
                </Keywords>

                <!-- Builtin types -->
                <Keywords color="Type">
                    <Word>int</Word>
                    <Word>int32</Word>
                    <Word>int64</Word>
                    <Word>int16</Word>
                    <Word>uint</Word>
                    <Word>uint32</Word>
                    <Word>uint64</Word>
                    <Word>float</Word>
                    <Word>float32</Word>
                    <Word>double</Word>
                    <Word>decimal</Word>
                    <Word>bool</Word>
                    <Word>string</Word>
                    <Word>char</Word>
                    <Word>byte</Word>
                    <Word>sbyte</Word>
                    <Word>unit</Word>
                    <Word>obj</Word>
                    <Word>exn</Word>
                    <Word>list</Word>
                    <Word>seq</Word>
                    <Word>array</Word>
                    <Word>option</Word>
                    <Word>voption</Word>
                    <Word>Result</Word>
                    <Word>Choice</Word>
                    <Word>Async</Word>
                    <Word>Task</Word>
                </Keywords>

                <!-- Builtins & Directives -->
                <Keywords color="Builtin">
                    <Word>true</Word>
                    <Word>false</Word>
                    <Word>null</Word>
                    <Word>printfn</Word>
                    <Word>sprintf</Word>
                    <Word>eprintfn</Word>
                    <Word>printf</Word>
                    <Word>async</Word>
                    <Word>task</Word>
                    <Word>#r</Word>
                    <Word>#load</Word>
                    <Word>#I</Word>
                    <Word>#time</Word>
                </Keywords>

                <!-- Function calls: identifier followed by space or pipe -->
                <Rule color="Function">
                    \b[a-zA-Z_][a-zA-Z0-9_']*(?=\s*\()
                </Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
