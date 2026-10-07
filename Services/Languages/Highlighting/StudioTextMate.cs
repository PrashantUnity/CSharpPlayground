using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Highlighting;

/// <summary>
/// The studio's syntax colours are VS Code's: its TextMate grammars and themes, through AvaloniaEdit's TextMate support
/// (TextMateSharp). This holds what every editor shares: the grammars (each read once), the grammar for a language, and
/// the TextMate theme for the studio's theme (VS Code's own for Dark+, Light+, Dracula, Monokai and One Dark; one made
/// from the theme's syntax colours for a generated theme). UI-free.
/// </summary>
public static class StudioTextMate
{
    private static readonly Lazy<CachingRegistryOptions> SharedOptions = new(() => new CachingRegistryOptions(new RegistryOptions(ThemeName.DarkPlus)));
    private static readonly ConcurrentDictionary<ThemeName, IRawTheme> BuiltInThemes = new();
    private static readonly ConcurrentDictionary<string, IRawTheme> GeneratedThemes = new(StringComparer.Ordinal);

    /// <summary>What every editor's TextMate registry reads grammars and themes from.</summary>
    public static IRegistryOptions Options => SharedOptions.Value;

    private static RegistryOptions Catalog => SharedOptions.Value.Inner;

    /// <summary>
    /// The TextMate scope of the grammar VS Code uses: the language's own (VS Code's language ids are the studio's: cpp,
    /// go, ...), else the open file's (<paramref name="fileExtension"/>: a .json or .md file opened as plain text), else
    /// one of the language's file types (a language from an extension). C# when there is neither a language nor a file.
    /// Plain text without a file of a known type has none.
    /// </summary>
    public static string? ScopeFor(ILanguageDefinition? language, string? fileExtension = null)
    {
        if (language != null && ScopeForLanguageId(language.Id) is { } byId) return byId;
        if (!string.IsNullOrEmpty(fileExtension) && ScopeForExtension(fileExtension) is { } byFile) return byFile;
        if (language == null) return string.IsNullOrEmpty(fileExtension) ? ScopeForLanguageId("csharp") : null;
        if (language.Id == LanguageIds.Text) return null;
        foreach (var extension in language.FileExtensions)
        {
            if (ScopeForExtension(extension) is { } scope) return scope;
        }

        return null;
    }

    public static string? ScopeForExtension(string extension)
    {
        var scope = Catalog.GetScopeByExtension(extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant());
        return string.IsNullOrEmpty(scope) ? null : scope;
    }

    public static string? ScopeForLanguageId(string languageId) =>
        Catalog.GetAvailableLanguages().Any(l => string.Equals(l.Id, languageId, StringComparison.OrdinalIgnoreCase))
            ? Catalog.GetScopeByLanguageId(languageId)
            : null;

    /// <summary>The studio themes that have a VS Code theme of their own.</summary>
    private static ThemeName? ThemeNameFor(string? themeId) => themeId switch
    {
        "dark-plus" => ThemeName.DarkPlus,
        "light-plus" => ThemeName.LightPlus,
        "dracula" => ThemeName.Dracula,
        "monokai" => ThemeName.Monokai,
        "one-dark" => ThemeName.OneDark,
        _ => null,
    };

    /// <summary>
    /// The TextMate theme for a studio theme: VS Code's own when there is one, one made from the theme's syntax colours
    /// when it has them (generated themes do), and otherwise Dark+ or Light+ for its scheme.
    /// </summary>
    public static IRawTheme ThemeFor(ThemeDefinition? theme, bool isDark)
    {
        if (theme != null && ThemeNameFor(theme.Id) is { } own) return Load(own);
        if (theme != null && SyntaxColors(theme) is { } roles)
        {
            var key = theme.Id + "|" + string.Join(",", roles.Select(r => r.Key + "=" + r.Value)) + "|" + Foreground(theme);
            return GeneratedThemes.GetOrAdd(key, _ => Generate(theme, roles));
        }

        return Load(theme?.IsDark ?? isDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
    }

    public static IRawTheme Load(ThemeName name) => BuiltInThemes.GetOrAdd(name, n => Catalog.LoadTheme(n));

    private static Dictionary<string, string>? SyntaxColors(ThemeDefinition theme)
    {
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var role in Extensibility.Theming.SyntaxPaletteApplier.Roles)
        {
            if (theme.Colors.TryGetValue($"Syntax{role}Brush", out var hex) && !string.IsNullOrWhiteSpace(hex)) roles[role] = hex;
        }

        return roles.Count == 0 ? null : roles;
    }

    private static string? Foreground(ThemeDefinition theme) =>
        theme.Colors.TryGetValue("EditorFgBrush", out var fg) ? fg : theme.Colors.TryGetValue("DsTextBrush", out var text) ? text : null;

    // Which TextMate scopes each syntax role colours (the scopes VS Code's themes give those colours).
    private static readonly (string Role, string[] Scopes)[] RoleScopes =
    [
        ("Comment", ["comment", "punctuation.definition.comment", "comment.block.documentation"]),
        ("String", ["string", "punctuation.definition.string", "constant.character", "constant.character.escape", "constant.other.placeholder", "string.regexp", "meta.interpolation punctuation.definition.interpolation"]),
        ("Number", ["constant.numeric", "keyword.other.unit", "constant.other.color"]),
        ("Keyword", ["keyword", "storage", "storage.type", "storage.modifier", "constant.language", "variable.language", "keyword.control", "keyword.operator.new", "keyword.operator.expression", "keyword.operator.wordlike", "keyword.operator.logical.python", "keyword.other.using", "keyword.other.directive.using"]),
        ("Type", ["entity.name.type", "entity.name.class", "entity.name.namespace", "entity.name.scope-resolution", "entity.other.inherited-class", "support.type", "support.class"]),
        ("Function", ["entity.name.function", "support.function", "meta.function-call entity.name.function", "entity.name.function.macro", "entity.name.function.preprocessor"]),
        ("Preprocessor", ["meta.preprocessor", "keyword.control.directive", "keyword.preprocessor", "punctuation.definition.directive", "meta.decorator", "punctuation.decorator", "storage.type.annotation", "meta.attribute", "entity.other.attribute-name"]),
        ("Variable", ["variable", "variable.parameter", "variable.other", "variable.other.property", "variable.other.object", "entity.name.variable", "support.variable", "meta.definition.variable.name", "variable.other.enummember", "variable.other.constant"]),
        ("Punctuation", ["punctuation", "keyword.operator", "meta.brace", "punctuation.separator", "punctuation.terminator"]),
    ];

    private static IRawTheme Generate(ThemeDefinition theme, IReadOnlyDictionary<string, string> roles)
    {
        var settings = new List<object>
        {
            new { settings = new { foreground = Hex(Foreground(theme)) ?? (theme.IsDark ? "#D4D4D4" : "#000000") } },
        };
        foreach (var (role, scopes) in RoleScopes)
        {
            if (roles.TryGetValue(role, out var hex) && Hex(hex) is { } color) settings.Add(new { scope = scopes, settings = new { foreground = color } });
        }

        var json = JsonSerializer.Serialize(new { name = theme.Name, type = theme.IsDark ? "dark" : "light", tokenColors = settings });
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        return ThemeReader.ReadThemeSync(reader);
    }

    // #AARRGGBB (the studio's) → #RRGGBBAA (TextMate's); #RRGGBB stays.
    private static string? Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hex = value.Trim();
        if (!hex.StartsWith('#')) return null;
        return hex.Length == 9 ? "#" + hex.Substring(3, 6) + hex.Substring(1, 2) : hex;
    }

    // ── The C# layer's colours: what the theme gives the scopes VS Code maps C#'s semantic roles to. ──

    private static readonly (CSharpRole Role, string Scope)[] CSharpRoleScopes =
    [
        (CSharpRole.Comment, "comment.line.double-slash.cs"),
        (CSharpRole.ExcludedCode, "comment.block.cs"),
        (CSharpRole.String, "string.quoted.double.cs"),
        (CSharpRole.StringEscape, "constant.character.escape.cs"),
        (CSharpRole.Number, "constant.numeric.decimal.cs"),
        (CSharpRole.Keyword, "keyword.other.cs"),
        (CSharpRole.ControlKeyword, "keyword.control.cs"),
        (CSharpRole.Preprocessor, "meta.preprocessor.cs"),
        (CSharpRole.Type, "entity.name.type.class.cs"),
        (CSharpRole.Namespace, "entity.name.type.namespace.cs"),
        (CSharpRole.Method, "entity.name.function.cs"),
        (CSharpRole.Variable, "variable.other.readwrite.cs"),
        (CSharpRole.EnumMember, "variable.other.enummember.cs"),
        (CSharpRole.Punctuation, "punctuation.terminator.statement.cs"),
    ];

    /// <summary>The colour (#RRGGBB or #RRGGBBAA) a theme gives each C# role, or none (the grammar's colour stays).</summary>
    public static IReadOnlyDictionary<CSharpRole, string> CSharpRoleColors(IRawTheme rawTheme)
    {
        var theme = Theme.CreateFromRawTheme(rawTheme, Options);
        var colors = new Dictionary<CSharpRole, string>();
        foreach (var (role, scope) in CSharpRoleScopes)
        {
            if (ColorOf(theme, ["source.cs", scope]) is { } color) colors[role] = color;
        }

        return colors;
    }

    /// <summary>The foreground a theme gives a scope path (root first), or null when only the default applies.</summary>
    public static string? ColorOf(Theme theme, IList<string> scopes)
    {
        foreach (var rule in theme.Match(scopes))
        {
            if (rule.foreground > 0) return theme.GetColor(rule.foreground);
        }

        return null;
    }

    /// <summary>
    /// Colours code the way an editor would (for tests and tools): every token's text and the theme's colour for it.
    /// </summary>
    public static List<(string Text, string? Color, IReadOnlyList<string> Scopes)> Colorize(string scope, string code, IRawTheme rawTheme)
    {
        var registry = new Registry(Options);
        registry.SetTheme(rawTheme);
        var grammar = registry.LoadGrammar(scope) ?? throw new ArgumentException($"No grammar for {scope}", nameof(scope));
        var theme = registry.GetTheme();
        var result = new List<(string, string?, IReadOnlyList<string>)>();
        IStateStack? state = null;
        foreach (var line in code.Replace("\r\n", "\n").Split('\n'))
        {
            var tokens = grammar.TokenizeLine(line, state, TimeSpan.FromSeconds(5));
            state = tokens.RuleStack;
            foreach (var token in tokens.Tokens)
            {
                var end = Math.Min(token.EndIndex, line.Length);
                if (end <= token.StartIndex) continue;
                result.Add((line[token.StartIndex..end], ColorOf(theme, token.Scopes), token.Scopes));
            }
        }

        return result;
    }

    /// <summary>Reads each grammar once for every editor (each editor's registry compiles it for itself).</summary>
    private sealed class CachingRegistryOptions(RegistryOptions inner) : IRegistryOptions
    {
        private readonly ConcurrentDictionary<string, IRawGrammar> _grammars = new(StringComparer.Ordinal);

        public RegistryOptions Inner => inner;

        public IRawTheme GetTheme(string scopeName) => inner.GetTheme(scopeName);

        public IRawGrammar GetGrammar(string scopeName) => _grammars.GetOrAdd(scopeName, inner.GetGrammar);

        public ICollection<string> GetInjections(string scopeName) => inner.GetInjections(scopeName);

        public IRawTheme GetDefaultTheme() => inner.GetDefaultTheme();
    }
}
