using AvaloniaEdit.Document;
using FrySharp.Sdk;
using Microsoft.CodeAnalysis.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Code is coloured the way VS Code colours it: each language by VS Code's TextMate grammar under VS Code's theme (Dark+,
/// Light+, Dracula, ...), a generated theme through its syntax colours, and C# by the role Roslyn's syntax tree gives
/// each word (the grammar can't follow top-level code: scripts and notebook cells). The expected colours are VS Code's.
/// </summary>
[Collection("SettingsTests")]
public class SyntaxColoringTests : IDisposable
{
    private readonly StudioLanguageServices _services = new(Path.Combine(Path.GetTempPath(), "FryPDF_SyntaxColoring_" + Guid.NewGuid().ToString("N")));

    public void Dispose() => StudioAppContext.Instance.ThemeEngine.ApplyTheme(BuiltInThemes.DarkPlus.Id);

    private static readonly IRawTheme DarkPlus = StudioTextMate.Load(ThemeName.DarkPlus);

    /// <summary>The colour of the <paramref name="occurrence"/>-th token whose text is <paramref name="token"/>.</summary>
    private static string? ColorOf(string scope, string code, string token, int occurrence = 0, IRawTheme? theme = null)
    {
        var matches = StudioTextMate.Colorize(scope, code, theme ?? DarkPlus).Where(t => t.Text.Trim() == token).ToList();
        Assert.True(matches.Count > occurrence, $"'{token}' is not a token of the {scope} code");
        return matches[occurrence].Color?.ToUpperInvariant();
    }

    private const string Keyword = "#569CD6";
    private const string Control = "#C586C0";
    private const string Type = "#4EC9B0";
    private const string Function = "#DCDCAA";
    private const string Variable = "#9CDCFE";
    private const string Constant = "#4FC1FF";
    private const string String = "#CE9178";
    private const string Escape = "#D7BA7D";
    private const string Number = "#B5CEA8";
    private const string Comment = "#6A9955";

    [Theory]
    [InlineData(LanguageIds.CSharp, "source.cs")]
    [InlineData(LanguageIds.Cpp, "source.cpp")]
    [InlineData(LanguageIds.Go, "source.go")]
    [InlineData(LanguageIds.Python, "source.python")]
    [InlineData(LanguageIds.Rust, "source.rust")]
    [InlineData(LanguageIds.Java, "source.java")]
    [InlineData(LanguageIds.JavaScript, "source.js")]
    [InlineData(LanguageIds.Sql, "source.sql")]
    [InlineData(LanguageIds.FSharp, "source.fsharp")]
    public void EveryLanguage_IsColouredByVsCodesGrammarForIt(string languageId, string scope)
    {
        Assert.Equal(scope, StudioTextMate.ScopeFor(_services.Registry.Get(languageId)));
        Assert.Equal("source.cs", StudioTextMate.ScopeFor(null));
    }

    [Fact]
    public void AFileOpenedAsText_IsColouredByItsOwnType_AndPlainTextIsNot()
    {
        var text = _services.Registry.Get(LanguageIds.Text);
        Assert.Equal("source.json", StudioTextMate.ScopeFor(text, ".json"));
        Assert.Equal("text.html.markdown", StudioTextMate.ScopeFor(text, ".md"));
        Assert.Equal("source.yaml", StudioTextMate.ScopeFor(text, ".YML"));
        Assert.Equal("source.shell", StudioTextMate.ScopeFor(text, ".sh"));
        Assert.Null(StudioTextMate.ScopeFor(text, ".txt"));      // plain text stays plain (it was about to be JSON)
        Assert.Null(StudioTextMate.ScopeFor(text));
        Assert.Equal("source.cpp", StudioTextMate.ScopeFor(_services.Registry.Get(LanguageIds.Cpp), ".h")); // a header is C++ here
        Assert.Equal("text.html.markdown", StudioTextMate.ScopeFor(null, ".md"));   // a markdown cell
    }

    [Fact]
    public void Cpp_IncludesPrimitiveTypesAndCalls_HaveVsCodesColours()
    {
        const string code = "#include <iostream>\nstd::vector<long long> fib = { 0, 1 };\nint main() { for (int i = 2; i < 20; ++i) { fib.push_back(fib[i - 1]); } return 0; } // done\nstd::cout << \"a\\n\" << 0LL;\nclass Foo : public Bar { };";

        Assert.Equal(Control, ColorOf("source.cpp", code, "#"));       // the directive is pink, as in VS Code...
        Assert.Equal(Control, ColorOf("source.cpp", code, "include"));
        Assert.Equal(String, ColorOf("source.cpp", code, "iostream")); // ...and the header a string (it was one colour)
        Assert.Equal(Keyword, ColorOf("source.cpp", code, "long"));    // primitive types are keywords (they were types)
        Assert.Equal(Keyword, ColorOf("source.cpp", code, "int"));
        Assert.Equal(Type, ColorOf("source.cpp", code, "std"));
        Assert.Equal(Control, ColorOf("source.cpp", code, "for"));
        Assert.Equal(Control, ColorOf("source.cpp", code, "return"));
        Assert.Equal(Function, ColorOf("source.cpp", code, "main"));
        Assert.Equal(Function, ColorOf("source.cpp", code, "push_back"));
        Assert.Equal(Variable, ColorOf("source.cpp", code, "fib", 1));
        Assert.Equal(Number, ColorOf("source.cpp", code, "20"));
        Assert.Equal(Number, ColorOf("source.cpp", code, "LL"));
        Assert.Equal(Escape, ColorOf("source.cpp", code, "\\n"));
        Assert.Equal(Comment, ColorOf("source.cpp", code, "done"));
        Assert.Equal(Keyword, ColorOf("source.cpp", code, "class"));
        Assert.Equal(Type, ColorOf("source.cpp", code, "Foo"));
        Assert.Equal(Type, ColorOf("source.cpp", code, "Bar"));
    }

    [Fact]
    public void Go_HasVsCodesColours()
    {
        const string code = "package main\nimport \"fry\"\nfunc main() {\n\tm := [][]int{{10, 20}}\n\tfor i, v := range m { fmt.Printf(\"%d\\n\", v) }\n\tvar s string = nil\n}";

        Assert.Equal(Keyword, ColorOf("source.go", code, "package"));
        Assert.Equal(Keyword, ColorOf("source.go", code, "func"));
        Assert.Equal(Function, ColorOf("source.go", code, "main", 1));
        Assert.Equal(String, ColorOf("source.go", code, "fry"));
        Assert.Equal(Type, ColorOf("source.go", code, "int"));
        Assert.Equal(Type, ColorOf("source.go", code, "string"));
        Assert.Equal(Control, ColorOf("source.go", code, "for"));
        Assert.Equal(Control, ColorOf("source.go", code, "range"));
        Assert.Equal(Function, ColorOf("source.go", code, "Printf"));
        Assert.Equal(Variable, ColorOf("source.go", code, "%d"));
        Assert.Equal(Escape, ColorOf("source.go", code, "\\n"));
        Assert.Equal(Number, ColorOf("source.go", code, "10"));
        Assert.Equal(Keyword, ColorOf("source.go", code, "nil"));
    }

    [Fact]
    public void Python_Rust_Java_AndJavaScript_HaveVsCodesColours()
    {
        const string python = "import os\n@dataclass\nclass P(Base):\n    def run(self, x: int) -> str:\n        if x > 0 and not None:\n            return f\"v={x}\\n\"  # note\n        print(len(x), True)";
        Assert.Equal(Control, ColorOf("source.python", python, "import"));
        Assert.Equal(Function, ColorOf("source.python", python, "dataclass"));
        Assert.Equal(Keyword, ColorOf("source.python", python, "class"));
        Assert.Equal(Type, ColorOf("source.python", python, "P"));
        Assert.Equal(Keyword, ColorOf("source.python", python, "def"));
        Assert.Equal(Function, ColorOf("source.python", python, "run"));
        Assert.Equal(Variable, ColorOf("source.python", python, "self"));
        Assert.Equal(Type, ColorOf("source.python", python, "int"));
        Assert.Equal(Control, ColorOf("source.python", python, "if"));
        Assert.Equal(Keyword, ColorOf("source.python", python, "and"));
        Assert.Equal(Control, ColorOf("source.python", python, "return"));
        Assert.Equal(Escape, ColorOf("source.python", python, "\\n"));
        Assert.Equal(Comment, ColorOf("source.python", python, "note"));
        Assert.Equal(Function, ColorOf("source.python", python, "print"));
        Assert.Equal(Keyword, ColorOf("source.python", python, "True"));

        const string rust = "use std::io;\nfn main() -> Option<i32> {\n    let mut v: Vec<u8> = Vec::new();\n    println!(\"{}\", 42u8);\n    match v.len() { 0 => None, _ => Some(1) }\n}";
        Assert.Equal(Keyword, ColorOf("source.rust", rust, "fn"));
        Assert.Equal(Function, ColorOf("source.rust", rust, "main"));
        Assert.Equal(Type, ColorOf("source.rust", rust, "Option"));
        Assert.Equal(Type, ColorOf("source.rust", rust, "i32"));
        Assert.Equal(Keyword, ColorOf("source.rust", rust, "let"));
        Assert.Equal(Keyword, ColorOf("source.rust", rust, "mut"));
        Assert.Equal(Variable, ColorOf("source.rust", rust, "v"));
        Assert.Equal(Function, ColorOf("source.rust", rust, "new"));
        Assert.Equal(Function, ColorOf("source.rust", rust, "println!"));
        Assert.Equal(Number, ColorOf("source.rust", rust, "42"));
        Assert.Equal(Control, ColorOf("source.rust", rust, "match"));

        const string java = "import java.util.*;\npublic class A extends B {\n  public static void main(String[] args) {\n    List<Integer> n = new ArrayList<>();\n    for (int i = 0; i < 3; i++) System.out.println(\"x\\n\" + n.size());\n  }\n}";
        Assert.Equal(Keyword, ColorOf("source.java", java, "public"));
        Assert.Equal(Keyword, ColorOf("source.java", java, "class"));
        Assert.Equal(Type, ColorOf("source.java", java, "A"));
        Assert.Equal(Type, ColorOf("source.java", java, "void"));
        Assert.Equal(Function, ColorOf("source.java", java, "main"));
        Assert.Equal(Type, ColorOf("source.java", java, "String"));
        Assert.Equal(Variable, ColorOf("source.java", java, "args"));
        Assert.Equal(Control, ColorOf("source.java", java, "new"));
        Assert.Equal(Control, ColorOf("source.java", java, "for"));
        Assert.Equal(Function, ColorOf("source.java", java, "println"));
        Assert.Equal(Escape, ColorOf("source.java", java, "\\n"));

        const string js = "import { a } from 'b';\nconst x = 42; let y = `t ${x}\\n`;\nfunction f(a, b) { if (a) return this.g(b); }";
        Assert.Equal(Control, ColorOf("source.js", js, "import"));
        Assert.Equal(Keyword, ColorOf("source.js", js, "const"));
        Assert.Equal(Constant, ColorOf("source.js", js, "x"));
        Assert.Equal(Keyword, ColorOf("source.js", js, "let"));
        Assert.Equal(Keyword, ColorOf("source.js", js, "${"));
        Assert.Equal(Keyword, ColorOf("source.js", js, "function"));
        Assert.Equal(Function, ColorOf("source.js", js, "f"));
        Assert.Equal(Control, ColorOf("source.js", js, "return"));
    }

    // ── Themes ────────────────────────────────────────────────────────────────

    private static ThemeDefinition Theme(string id) => BuiltInThemes.All.Single(t => t.Id == id);

    [Fact]
    public void TheStudiosThemes_UseVsCodesOwnTheme_AndLightThemesColourLikeLightPlus()
    {
        Assert.Same(StudioTextMate.Load(ThemeName.DarkPlus), StudioTextMate.ThemeFor(Theme("dark-plus"), isDark: true));
        Assert.Same(StudioTextMate.Load(ThemeName.LightPlus), StudioTextMate.ThemeFor(Theme("light-plus"), isDark: false));
        Assert.Same(StudioTextMate.Load(ThemeName.Dracula), StudioTextMate.ThemeFor(Theme("dracula"), isDark: true));
        Assert.Same(StudioTextMate.Load(ThemeName.Monokai), StudioTextMate.ThemeFor(Theme("monokai"), isDark: true));
        Assert.Same(StudioTextMate.Load(ThemeName.OneDark), StudioTextMate.ThemeFor(Theme("one-dark"), isDark: true));
        Assert.Same(StudioTextMate.Load(ThemeName.DarkPlus), StudioTextMate.ThemeFor(Theme("cyberpunk"), isDark: true));

        const string code = "int main() { return 0; }";
        var light = StudioTextMate.Load(ThemeName.LightPlus);
        Assert.Equal("#0000FF", ColorOf("source.cpp", code, "int", theme: light));
        Assert.Equal("#AF00DB", ColorOf("source.cpp", code, "return", theme: light));
        Assert.Equal("#FF79C6", ColorOf("source.cpp", code, "return", theme: StudioTextMate.Load(ThemeName.Dracula)));
    }

    [Fact]
    public void AGeneratedTheme_ColoursEveryLanguage_WithItsSyntaxColours()
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = 20, Mode = ColorHarmonyMode.Triadic });
        var raw = StudioTextMate.ThemeFor(theme, theme.IsDark);
        Assert.Same(raw, StudioTextMate.ThemeFor(theme, theme.IsDark));
        string Role(string role) => "#" + theme.Colors[$"Syntax{role}Brush"].TrimStart('#')[^6..].ToUpperInvariant();

        const string code = "class Foo { };\nint main() { for (;;) { run(\"a\", 42); } } // c";
        Assert.Equal(Role("Keyword"), ColorOf("source.cpp", code, "for", theme: raw));
        Assert.Equal(Role("Keyword"), ColorOf("source.cpp", code, "int", theme: raw));
        Assert.Equal(Role("Type"), ColorOf("source.cpp", code, "Foo", theme: raw));
        Assert.Equal(Role("Function"), ColorOf("source.cpp", code, "run", theme: raw));
        Assert.Equal(Role("String"), ColorOf("source.cpp", code, "a", theme: raw));
        Assert.Equal(Role("Number"), ColorOf("source.cpp", code, "42", theme: raw));
        Assert.Equal(Role("Comment"), ColorOf("source.cpp", code, "c", theme: raw));

        var csharp = StudioTextMate.CSharpRoleColors(raw);
        Assert.Equal(Role("Type"), csharp[CSharpRole.Type][..7].ToUpperInvariant());
        Assert.Equal(Role("Function"), csharp[CSharpRole.Method][..7].ToUpperInvariant());
        Assert.Equal(Role("Variable"), csharp[CSharpRole.Variable][..7].ToUpperInvariant());
    }

    // ── C#: the roles of Roslyn's syntax tree, in the theme's colours ───────────

    [Fact]
    public void CSharp_RoleColours_AreVsCodes_InDarkPlusAndLightPlus()
    {
        var dark = StudioTextMate.CSharpRoleColors(DarkPlus);
        Assert.Equal(Type, dark[CSharpRole.Type].ToUpperInvariant());
        Assert.Equal(Type, dark[CSharpRole.Namespace].ToUpperInvariant());
        Assert.Equal(Function, dark[CSharpRole.Method].ToUpperInvariant());
        Assert.Equal(Variable, dark[CSharpRole.Variable].ToUpperInvariant());
        Assert.Equal(Constant, dark[CSharpRole.EnumMember].ToUpperInvariant());
        Assert.Equal(Keyword, dark[CSharpRole.Keyword].ToUpperInvariant());
        Assert.Equal(Control, dark[CSharpRole.ControlKeyword].ToUpperInvariant());
        Assert.Equal(String, dark[CSharpRole.String].ToUpperInvariant());
        Assert.Equal(Escape, dark[CSharpRole.StringEscape].ToUpperInvariant());
        Assert.Equal(Number, dark[CSharpRole.Number].ToUpperInvariant());
        Assert.Equal(Comment, dark[CSharpRole.Comment].ToUpperInvariant());

        var light = StudioTextMate.CSharpRoleColors(StudioTextMate.Load(ThemeName.LightPlus));
        Assert.Equal("#267F99", light[CSharpRole.Type].ToUpperInvariant());
        Assert.Equal("#795E26", light[CSharpRole.Method].ToUpperInvariant());
        Assert.Equal("#001080", light[CSharpRole.Variable].ToUpperInvariant());
        Assert.Equal("#0000FF", light[CSharpRole.Keyword].ToUpperInvariant());
        Assert.Equal("#AF00DB", light[CSharpRole.ControlKeyword].ToUpperInvariant());
        Assert.Equal("#A31515", light[CSharpRole.String].ToUpperInvariant());
    }

    /// <summary>Each word's role in <paramref name="code"/>: "word" → role, the n-th occurrence.</summary>
    private static CSharpRole? RoleOf(string code, string word, int occurrence = 0)
    {
        var tree = CSharpClassifier.Parse(code);
        var spans = CSharpClassifier.Classify(tree, new TextSpan(0, code.Length), CSharpDeclarations.Collect(tree.GetRoot()));
        var offset = -1;
        for (var i = 0; i <= occurrence; i++)
        {
            do offset = code.IndexOf(word, offset + 1, StringComparison.Ordinal);
            while (offset >= 0 && (offset > 0 && IsWordChar(code[offset - 1]) && IsWordChar(word[0]) || offset + word.Length < code.Length && IsWordChar(code[offset + word.Length]) && IsWordChar(word[^1])));
        }

        Assert.True(offset >= 0, $"'{word}' not in the code");
        var span = spans.FirstOrDefault(s => s.Start <= offset && s.End >= offset + word.Length);
        return span.Length == 0 ? null : span.Role;
    }

    private static CSharpRole? RoleAt(string code, int offset, int length)
    {
        var tree = CSharpClassifier.Parse(code);
        var span = CSharpClassifier.Classify(tree, new TextSpan(0, code.Length), CSharpDeclarations.Collect(tree.GetRoot()))
            .FirstOrDefault(s => s.Start <= offset && s.End >= offset + length);
        return span.Length == 0 ? null : span.Role;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    [Fact]
    public void CSharp_TheTwoSumExample_GetsVsCodesRoles()
    {
        const string code = """
            using System;
            using System.Collections.Generic;

            public class Solution
            {
                public int[] TwoSum(int[] nums, int target)
                {
                    var map = new Dictionary<int, int>();
                    for (int i = 0; i < nums.Length; i++)
                    {
                        int complement = target - nums[i];
                        if (map.TryGetValue(complement, out int index)) return new int[] { index, i };
                        map[nums[i]] = i;
                    }
                    return Array.Empty<int>();
                }
            }

            // Execute test cases
            var sol = new Solution();
            int[] test1 = sol.TwoSum(new int[] { 2, 7, 11, 15 }, 9);
            test1.Dump("Test Case 1");
            Console.WriteLine($"All {test1.Length} ok\n");
            """;

        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "using"));
        Assert.Equal(CSharpRole.Namespace, RoleOf(code, "System"));
        Assert.Equal(CSharpRole.Namespace, RoleOf(code, "Generic"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "class"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Solution"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "int"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "TwoSum"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "nums"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "target"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "var"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "map"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "new"));              // was coloured as a type
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Dictionary"));
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "for"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "Length"));          // a property, not a type
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "if"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "TryGetValue"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "out"));
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "return"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Array"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "Empty"));             // a generic method call
        Assert.Equal(CSharpRole.Comment, RoleOf(code, "Execute"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "sol"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Solution", 1));         // new Solution(): a type, not a method
        Assert.Equal(CSharpRole.Number, RoleOf(code, "11"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "Dump"));
        Assert.Equal(CSharpRole.String, RoleOf(code, "Test Case 1"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Console"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "WriteLine"));
        Assert.Equal(CSharpRole.String, RoleOf(code, "$\""));
        Assert.Equal(CSharpRole.String, RoleAt(code, code.IndexOf("{test1", StringComparison.Ordinal), 1));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "test1", 2));         // inside the interpolation: code
        Assert.Equal(CSharpRole.StringEscape, RoleOf(code, "\\n"));
    }

    [Fact]
    public void CSharp_EnumsConstantsDirectivesAttributesAndSwitches_GetTheirRoles()
    {
        const string code = """
            #region Setup
            enum Color { Red, Green }
            const int Max = 3;
            [Obsolete]
            static T First<T>(T[] items) => items[0];
            async Task Run(Color c)
            {
                await Task.Delay(Max);
                switch (c) { case Color.Red: break; default: return; }
                var d = default(int);
                Console.WriteLine(nameof(c) + Color.Green + @"C:\x" + 'q');
            }
            #endregion
            """;

        Assert.Equal(CSharpRole.Preprocessor, RoleOf(code, "#region"));
        Assert.Null(RoleOf(code, "Setup"));                                  // a region's name is plain text
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "enum"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Color"));
        Assert.Equal(CSharpRole.EnumMember, RoleOf(code, "Red"));
        Assert.Equal(CSharpRole.EnumMember, RoleOf(code, "Max"));
        Assert.Equal(CSharpRole.EnumMember, RoleOf(code, "Max", 1));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Obsolete"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "T"));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "First"));
        Assert.Equal(CSharpRole.Variable, RoleOf(code, "items", 1));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "async"));
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "await"));
        Assert.Equal(CSharpRole.Type, RoleOf(code, "Task", 1));
        Assert.Equal(CSharpRole.Method, RoleOf(code, "Delay"));
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "switch"));
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "case"));
        Assert.Equal(CSharpRole.EnumMember, RoleOf(code, "Red", 1));
        Assert.Equal(CSharpRole.ControlKeyword, RoleOf(code, "default"));    // default: in a switch
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "default", 1));        // default(int)
        Assert.Equal(CSharpRole.Keyword, RoleOf(code, "nameof"));
        Assert.Equal(CSharpRole.EnumMember, RoleOf(code, "Green", 1));
        Assert.Equal(CSharpRole.String, RoleOf(code, "@\"C:\\x\""));         // a verbatim string has no escapes
        Assert.Equal(CSharpRole.String, RoleOf(code, "'q'"));
        Assert.Equal(CSharpRole.Preprocessor, RoleOf(code, "#endregion"));
    }

    [Fact]
    public void CSharp_TheLiveTree_FollowsEdits()
    {
        var document = new TextDocument("var x = 1;\nConsole.WriteLine(x);");
        using var live = new CSharpLiveSyntax(document);
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(30)));
        Assert.True(live.HasTree);

        CSharpRole? At(string word, int occurrence = 0)
        {
            var offset = -1;
            for (var i = 0; i <= occurrence; i++) offset = document.Text.IndexOf(word, offset + 1, StringComparison.Ordinal);
            var line = document.GetLineByOffset(offset);
            var span = live.Classify(line.Offset, line.EndOffset).FirstOrDefault(s => s.Start <= offset && s.End >= offset + word.Length);
            return span.Length == 0 ? null : span.Role;
        }

        Assert.Equal(CSharpRole.Type, At("Console"));

        // Lines move: the old tree's roles move with their text until the new tree is ready, then they come from it.
        document.Insert(0, "// note\n");
        Assert.Equal(CSharpRole.Type, At("Console"));
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(30)));
        Assert.Equal(CSharpRole.Comment, At("note"));
        Assert.Equal(CSharpRole.Type, At("Console"));

        // An edit that changes a role: "x" becomes a declared variable's use, then a method.
        document.Replace(document.Text.IndexOf("(x)", StringComparison.Ordinal), 3, "(x())");
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(30)));
        Assert.Equal(CSharpRole.Variable, At("x", 1)); // a local holding a delegate stays a variable
    }

    [Fact]
    public void CSharp_AnEditInALargeFile_PicksUpTheNamesItDeclares()
    {
        var code = string.Concat(Enumerable.Range(0, 20_000).Select(i => $"var v{i} = new List<int> {{ {i} }}; Console.WriteLine(v{i}.Count);\n"));
        var document = new TextDocument(code);
        using var live = new CSharpLiveSyntax(document);
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(60)));

        // A capitalised local: only the file's declarations (collected from the edited part) say it's a variable, not a type.
        document.Insert(document.GetLineByNumber(10_000).Offset, "int Added = 1;\nAdded.ToString();\n");
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(60)));

        var use = document.GetLineByNumber(10_001);
        var added = live.Classify(use.Offset, use.EndOffset).First();
        Assert.Equal(use.Offset, added.Start);
        Assert.Equal(CSharpRole.Variable, added.Role);
    }

    [Fact(Skip = "Timing: not yet a reliable measure (passes even with whole-file name collection, and wall-clock times swing under full-suite load). To be reworked.")]
    public void CSharp_ALargeFile_IsReparsedInPartsAfterAnEdit()
    {
        var code = string.Concat(Enumerable.Range(0, 20_000).Select(i => $"var v{i} = new List<int> {{ {i} }}; Console.WriteLine(v{i}.Count);\n"));
        var document = new TextDocument(code);
        using var live = new CSharpLiveSyntax(document);
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(60)));
        var whole = live.LastParseDuration;

        document.Insert(document.GetLineByNumber(10_000).Offset, "int Added = 1;\n");
        Assert.True(live.WaitUntilIdle(TimeSpan.FromSeconds(60)));
        var edit = live.LastParseDuration;

        // The work should follow the edit, not the file (measured on the parser's own thread).
        Assert.True(edit < whole / 2, $"an edit took {edit.TotalMilliseconds:N0} ms to reparse; the whole file {whole.TotalMilliseconds:N0} ms");
    }

    [Fact]
    public void TheSharedGrammars_ColourTheSameFromManyEditorsAtOnce()
    {
        const string code = "int main() { for (int i = 0; i < 3; ++i) { run(\"a\\n\", i); } return 0; }";
        var expected = StudioTextMate.Colorize("source.cpp", code, DarkPlus).Select(t => (t.Text, t.Color)).ToList();
        var results = Enumerable.Range(0, 8).AsParallel()
            .Select(_ => StudioTextMate.Colorize("source.cpp", code, DarkPlus).Select(t => (t.Text, t.Color)).ToList())
            .ToList();
        Assert.All(results, r => Assert.Equal(expected, r));
    }
}
