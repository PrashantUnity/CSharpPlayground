using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// C++ syntax colors in the studio's two themes, VS Code's Dark+ and Light+: keywords, preprocessor directives,
/// types, strings, raw string literals, Doxygen comments, functions, and numbers.
/// </summary>
public static class CppSyntaxHighlighting
{
    private sealed record Palette(
        string Name, string Comment, string String, string Escape, string Control,
        string Storage, string Self, string Function, string Type, string Number, string Preprocessor);

    private static readonly Palette Dark = new("C++ Dark", "#6A9955", "#CE9178", "#D7BA7D", "#C586C0",
        "#569CD6", "#569CD6", "#DCDCAA", "#4EC9B0", "#B5CEA8", "#9CDCFE");

    private static readonly Palette Light = new("C++ Light", "#008000", "#A31515", "#EE0000", "#AF00DB",
        "#0000FF", "#0000FF", "#795E26", "#267F99", "#098658", "#0000FF");

    private static readonly Lazy<IHighlightingDefinition> DarkDefinition = new(() => Load(Dark));
    private static readonly Lazy<IHighlightingDefinition> LightDefinition = new(() => Load(Light));

    public static IHighlightingDefinition Get(bool isDark) => isDark ? DarkDefinition.Value : LightDefinition.Value;

    private static IHighlightingDefinition Load(Palette palette)
    {
        using var reader = new XmlTextReader(new StringReader(Xshd(palette)));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static string Xshd(Palette p) => $$$""""
        <SyntaxDefinition name="{{{p.Name}}}" extensions=".cpp;.cc;.cxx;.hpp;.h" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
            <Color name="Comment" foreground="{{{p.Comment}}}" />
            <Color name="String" foreground="{{{p.String}}}" />
            <Color name="Escape" foreground="{{{p.Escape}}}" />
            <Color name="Control" foreground="{{{p.Control}}}" />
            <Color name="Storage" foreground="{{{p.Storage}}}" />
            <Color name="Self" foreground="{{{p.Self}}}" />
            <Color name="Function" foreground="{{{p.Function}}}" />
            <Color name="Type" foreground="{{{p.Type}}}" />
            <Color name="Number" foreground="{{{p.Number}}}" />
            <Color name="Preprocessor" foreground="{{{p.Preprocessor}}}" />

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

            <RuleSet name="DoxygenTags">
                <Keywords fontWeight="bold" foreground="{{{p.Preprocessor}}}">
                    <Word>@brief</Word>
                    <Word>@param</Word>
                    <Word>@return</Word>
                    <Word>@note</Word>
                    <Word>@warning</Word>
                    <Word>@see</Word>
                    <Word>@deprecated</Word>
                    <Word>\brief</Word>
                    <Word>\param</Word>
                    <Word>\return</Word>
                </Keywords>
            </RuleSet>

            <RuleSet name="Escapes">
                <Span color="Escape" begin="\\" end="." />
            </RuleSet>

            <RuleSet>
                <Span color="Comment" multiline="true" ruleSet="DoxygenTags">
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

                <!-- C++ Raw string literal R"(...)" -->
                <Span color="String" multiline="true">
                    <Begin>R"\(</Begin>
                    <End>\)"</End>
                </Span>

                <Span color="String" ruleSet="Escapes">
                    <Begin>"</Begin>
                    <End>"</End>
                </Span>
                <Span color="String" ruleSet="Escapes">
                    <Begin>'</Begin>
                    <End>'</End>
                </Span>

                <!-- Preprocessor directives -->
                <Span color="Preprocessor">
                    <Begin>\#</Begin>
                    <RuleSet>
                        <Span color="Comment" ruleSet="CommentMarkers">
                            <Begin>//</Begin>
                        </Span>
                    </RuleSet>
                </Span>

                <Keywords color="Control">
                    <Word>if</Word><Word>else</Word><Word>switch</Word><Word>case</Word><Word>default</Word>
                    <Word>break</Word><Word>continue</Word><Word>return</Word><Word>for</Word><Word>while</Word>
                    <Word>do</Word><Word>try</Word><Word>catch</Word><Word>throw</Word><Word>goto</Word>
                    <Word>co_await</Word><Word>co_yield</Word><Word>co_return</Word>
                </Keywords>

                <Keywords color="Storage">
                    <Word>namespace</Word><Word>using</Word><Word>class</Word><Word>struct</Word><Word>union</Word><Word>enum</Word>
                    <Word>template</Word><Word>typename</Word><Word>concept</Word><Word>requires</Word>
                    <Word>constexpr</Word><Word>consteval</Word><Word>constinit</Word><Word>decltype</Word>
                    <Word>explicit</Word><Word>friend</Word><Word>inline</Word><Word>virtual</Word><Word>override</Word><Word>final</Word>
                    <Word>static</Word><Word>const</Word><Word>volatile</Word><Word>mutable</Word>
                    <Word>public</Word><Word>protected</Word><Word>private</Word>
                    <Word>export</Word><Word>import</Word><Word>module</Word><Word>operator</Word><Word>new</Word><Word>delete</Word>
                </Keywords>

                <Keywords color="Self">
                    <Word>this</Word><Word>nullptr</Word><Word>true</Word><Word>false</Word>
                    <Word>static_cast</Word><Word>dynamic_cast</Word><Word>const_cast</Word><Word>reinterpret_cast</Word>
                    <Word>sizeof</Word><Word>alignof</Word><Word>typeid</Word><Word>noexcept</Word>
                </Keywords>

                <Keywords color="Type">
                    <Word>void</Word><Word>bool</Word><Word>char</Word><Word>char8_t</Word><Word>char16_t</Word><Word>char32_t</Word><Word>wchar_t</Word>
                    <Word>short</Word><Word>int</Word><Word>long</Word><Word>float</Word><Word>double</Word><Word>signed</Word><Word>unsigned</Word><Word>auto</Word>
                    <Word>size_t</Word><Word>int8_t</Word><Word>int16_t</Word><Word>int32_t</Word><Word>int64_t</Word>
                    <Word>uint8_t</Word><Word>uint16_t</Word><Word>uint32_t</Word><Word>uint64_t</Word>
                    <Word>string</Word><Word>string_view</Word><Word>vector</Word><Word>map</Word><Word>unordered_map</Word>
                    <Word>set</Word><Word>unordered_set</Word><Word>pair</Word><Word>tuple</Word><Word>unique_ptr</Word>
                    <Word>shared_ptr</Word><Word>weak_ptr</Word><Word>optional</Word><Word>variant</Word><Word>span</Word><Word>array</Word>
                </Keywords>

                <Rule color="Number">\b0[xX][0-9a-fA-F_]+[uUlL]*\b|\b0[bB][01_]+[uUlL]*\b|(\b\d[\d_]*(\.[\d_]*)?|\.\d[\d_]*)([eE][+-]?\d[\d_]*)?[fFlLuU]*\b</Rule>
                <Rule color="Function">\b[A-Za-z_]\w*(?=\s*\()</Rule>
            </RuleSet>
        </SyntaxDefinition>
        """";
}
