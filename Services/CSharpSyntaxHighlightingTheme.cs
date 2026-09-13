using System;
using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static class CSharpSyntaxHighlightingTheme
{
    private static IHighlightingDefinition? _darkDefinition;
    private static readonly object _lock = new();

    public static IHighlightingDefinition GetDarkTheme()
    {
        if (_darkDefinition != null) return _darkDefinition;

        lock (_lock)
        {
            if (_darkDefinition != null) return _darkDefinition;

            using var reader = new XmlTextReader(new StringReader(DarkXshdXml));
            _darkDefinition = HighlightingLoader.Load(reader, HighlightingManager.Instance);

            // Register under both names so any component asking for "C#" or "C# Dark" gets our theme
            HighlightingManager.Instance.RegisterHighlighting("C#", [".cs"], _darkDefinition);
            HighlightingManager.Instance.RegisterHighlighting("C# Dark", [".cs"], _darkDefinition);

            return _darkDefinition;
        }
    }

    private const string DarkXshdXml = $$$$""""
<SyntaxDefinition name="C# Dark" extensions=".cs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
	<!-- Modern VS Code Dark+ Syntax Colors -->
	<Color name="Comment" foreground="#6A9955" exampleText="// comment" />
	<Color name="String" foreground="#CE9178" exampleText="string text = &quot;Hello, World!&quot;"/>
	<Color name="StringInterpolation" foreground="#9CDCFE" exampleText="string text = $&quot;Hello, {name}!&quot;"/>
	<Color name="Char" foreground="#D7BA7D" exampleText="char linefeed = '\n';"/>
	<Color name="Preprocessor" foreground="#C586C0" exampleText="#region Title" />
	<Color name="Punctuation" foreground="#D4D4D4" exampleText="a(b.c);" />
	<Color name="ValueTypeKeywords" foreground="#569CD6" exampleText="bool b = true;" />
	<Color name="ReferenceTypeKeywords" foreground="#4EC9B0" exampleText="object o;" />
	<Color name="MethodCall" foreground="#DCDCAA" exampleText="o.ToString();"/>
	<Color name="NumberLiteral" foreground="#B5CEA8" exampleText="3.1415f"/>
	<Color name="ThisOrBaseReference" foreground="#569CD6" exampleText="this.Do(); base.Do();"/>
	<Color name="NullOrValueKeywords" foreground="#569CD6" exampleText="if (value == null)"/>
	<Color name="Keywords" foreground="#C586C0" exampleText="if (a) {} else {}"/>
	<Color name="GotoKeywords" foreground="#C586C0" exampleText="continue; return null;"/>
	<Color name="ContextKeywords" foreground="#569CD6" exampleText="var a = from x in y select z;"/>
	<Color name="ExceptionKeywords" foreground="#C586C0" exampleText="try {} catch {} finally {}"/>
	<Color name="CheckedKeyword" foreground="#C586C0" exampleText="checked {}"/>
	<Color name="UnsafeKeywords" foreground="#C586C0" exampleText="unsafe { fixed (..) {} }"/>
	<Color name="OperatorKeywords" foreground="#569CD6" exampleText="public static implicit operator..."/>
	<Color name="ParameterModifiers" foreground="#569CD6" exampleText="(ref int a, params int[] b)"/>
	<Color name="Modifiers" foreground="#569CD6" exampleText="static readonly int a;"/>
	<Color name="Visibility" foreground="#569CD6" exampleText="public override void ToString();"/>
	<Color name="NamespaceKeywords" foreground="#C586C0" exampleText="namespace A.B { using System; }"/>
	<Color name="GetSetAddRemove" foreground="#569CD6" exampleText="int Prop { get; set; }"/>
	<Color name="TrueFalse" foreground="#569CD6" exampleText="b = false; a = true;" />
	<Color name="TypeKeywords" foreground="#4EC9B0" exampleText="if (x is int) { a = x as int; type = typeof(int); size = sizeof(int); c = new object(); }" />
	<Color name="SemanticKeywords" foreground="#C586C0" exampleText="if (args == null) throw new ArgumentNullException(nameof(args));" />

	<Property name="DocCommentMarker" value="///" />
	
	<RuleSet name="CommentMarkerSet">
		<Keywords fontWeight="bold" foreground="#F59E0B">
			<Word>TODO</Word>
			<Word>FIXME</Word>
		</Keywords>
		<Keywords fontWeight="bold" foreground="#3B82F6">
			<Word>HACK</Word>
			<Word>UNDONE</Word>
		</Keywords>
	</RuleSet>
	
	<RuleSet>
		<Span color="Preprocessor">
			<Begin>\#</Begin>
			<RuleSet name="PreprocessorSet">
				<Span>
					<Begin fontWeight="bold">
						(define|undef|if|elif|else|endif|line)\b
					</Begin>
					<RuleSet>
						<Span color="Comment" ruleSet="CommentMarkerSet">
							<Begin>//</Begin>
						</Span>
					</RuleSet>
				</Span>
				<Span>
					<Begin fontWeight="bold">
						(region|endregion|error|warning|pragma)\b
					</Begin>
				</Span>
			</RuleSet>
		</Span>
		
		<Span color="Comment" ruleSet="CommentMarkerSet">
			<Begin>//</Begin>
		</Span>
		
		<Span color="Comment" ruleSet="CommentMarkerSet" multiline="true">
			<Begin>/\*</Begin>
			<End>\*/</End>
		</Span>
		
		<!-- Raw string literal: """ ... """ and interpolated: $""" ... """, $$$""" ... """ -->
		<Span color="String" multiline="true">
			<Begin>\$*&quot;&quot;&quot;</Begin>
			<End>&quot;&quot;&quot;</End>
		</Span>

		<!-- Verbatim string: @"..." -->
		<Span color="String" multiline="true">
			<Begin>@"</Begin>
			<End>"</End>
			<RuleSet>
				<Span begin='""' end="" />
			</RuleSet>
		</Span>

		<!-- Interpolated string: $"..." -->
		<Span color="String">
			<Begin>\$"</Begin>
			<End>"</End>
			<RuleSet>
				<Span begin="\\" end="."/>
				<Span begin="\{\{" end=""/>
				<Span begin="{" end="}" color="StringInterpolation" ruleSet=""/>
			</RuleSet>
		</Span>

		<!-- Standard string: "..." -->
		<Span color="String">
			<Begin>"</Begin>
			<End>"</End>
			<RuleSet>
				<Span begin="\\" end="."/>
			</RuleSet>
		</Span>
		
		<Span color="Char">
			<Begin>'</Begin>
			<End>'</End>
			<RuleSet>
				<Span begin="\\" end="."/>
			</RuleSet>
		</Span>
		
		<Keywords color="TrueFalse">
			<Word>true</Word>
			<Word>false</Word>
		</Keywords>
		
		<Keywords color="Keywords">
			<Word>else</Word>
			<Word>if</Word>
			<Word>switch</Word>
			<Word>case</Word>
			<Word>default</Word>
			<Word>do</Word>
			<Word>for</Word>
			<Word>foreach</Word>
			<Word>in</Word>
			<Word>while</Word>
			<Word>lock</Word>
		</Keywords>
		
		<Keywords color="GotoKeywords">
			<Word>break</Word>
			<Word>continue</Word>
			<Word>goto</Word>
			<Word>return</Word>
		</Keywords>
		
		<Keywords color="ContextKeywords">
			<Word>yield</Word>
			<Word>partial</Word>
			<Word>global</Word>
			<Word>where</Word>
			<Word>select</Word>
			<Word>group</Word>
			<Word>by</Word>
			<Word>into</Word>
			<Word>from</Word>
			<Word>ascending</Word>
			<Word>descending</Word>
			<Word>orderby</Word>
			<Word>let</Word>
			<Word>join</Word>
			<Word>on</Word>
			<Word>equals</Word>
			<Word>var</Word>
			<Word>dynamic</Word>
			<Word>await</Word>
			<Word>async</Word>
		</Keywords>
		
		<Keywords color="ExceptionKeywords">
			<Word>try</Word>
			<Word>throw</Word>
			<Word>catch</Word>
			<Word>finally</Word>
		</Keywords>
		
		<Keywords color="CheckedKeyword">
			<Word>checked</Word>
			<Word>unchecked</Word>
		</Keywords>
		
		<Keywords color="UnsafeKeywords">
			<Word>fixed</Word>
			<Word>unsafe</Word>
		</Keywords>
		
		<Keywords color="ValueTypeKeywords">
			<Word>bool</Word>
			<Word>byte</Word>
			<Word>char</Word>
			<Word>decimal</Word>
			<Word>double</Word>
			<Word>enum</Word>
			<Word>float</Word>
			<Word>int</Word>
			<Word>long</Word>
			<Word>sbyte</Word>
			<Word>short</Word>
			<Word>struct</Word>
			<Word>uint</Word>
			<Word>ulong</Word>
			<Word>ushort</Word>
			<Word>void</Word>
		</Keywords>
		
		<Keywords color="ReferenceTypeKeywords">
			<Word>class</Word>
			<Word>interface</Word>
			<Word>delegate</Word>
			<Word>object</Word>
			<Word>string</Word>
			<Word>record</Word>
		</Keywords>
		
		<Keywords color="OperatorKeywords">
			<Word>explicit</Word>
			<Word>implicit</Word>
			<Word>operator</Word>
		</Keywords>
		
		<Keywords color="ParameterModifiers">
			<Word>params</Word>
			<Word>ref</Word>
			<Word>out</Word>
			<Word>in</Word>
		</Keywords>
		
		<Keywords color="Modifiers">
			<Word>abstract</Word>
			<Word>const</Word>
			<Word>event</Word>
			<Word>extern</Word>
			<Word>override</Word>
			<Word>readonly</Word>
			<Word>sealed</Word>
			<Word>static</Word>
			<Word>virtual</Word>
			<Word>volatile</Word>
		</Keywords>
		
		<Keywords color="Visibility">
			<Word>public</Word>
			<Word>protected</Word>
			<Word>private</Word>
			<Word>internal</Word>
			<Word>file</Word>
		</Keywords>
		
		<Keywords color="NamespaceKeywords">
			<Word>namespace</Word>
			<Word>using</Word>
		</Keywords>
		
		<Keywords color="GetSetAddRemove">
			<Word>get</Word>
			<Word>set</Word>
			<Word>init</Word>
			<Word>add</Word>
			<Word>remove</Word>
		</Keywords>
		
		<Keywords color="NullOrValueKeywords">
			<Word>null</Word>
			<Word>value</Word>
		</Keywords>
		
		<Keywords color="TypeKeywords">
			<Word>as</Word>
			<Word>is</Word>
			<Word>new</Word>
			<Word>sizeof</Word>
			<Word>typeof</Word>
			<Word>stackalloc</Word>
		</Keywords>
		
		<Keywords color="SemanticKeywords">
			<Word>nameof</Word>
			<Word>when</Word>
		</Keywords>
		
		<!-- Method Calls: yellow like VS Code #DCDCAA -->
		<Rule color="MethodCall">
			\b
			[\d\w_]+
			(?=\s*\()
		</Rule>
		
		<!-- PascalCase Types / Classes: teal like VS Code #4EC9B0 -->
		<Rule color="ReferenceTypeKeywords">
			\b[A-Z][a-zA-Z0-9_]*\b
		</Rule>

		<!-- Number Literals: soft green #B5CEA8 -->
		<Rule color="NumberLiteral">
			\b0[xX][0-9a-fA-F]+
			|
			\b
			( \d+(\.[0-9]+)?
			| \.[0-9]+
			)
			([eE][+-]?[0-9]+)?
			(f|F|d|D|m|M|u|U|l|L|ul|UL|lu|LU)?
		</Rule>
		
		<!-- Punctuation & Operators: #D4D4D4 -->
		<Rule color="Punctuation">
			[?,.;()\[\]{}+=~!&amp;|^%*&lt;&gt;/\-]+
		</Rule>
	</RuleSet>
</SyntaxDefinition>
"""";
}
