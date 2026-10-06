using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using AvaloniaEdit.Highlighting;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Makes every language's highlighting follow the active theme. A theme that has syntax colours
/// (<c>Syntax{Role}Brush</c>, which generated themes do) recolours each language's named colours by role: comments,
/// strings, numbers, keywords, types, functions, preprocessor directives and punctuation. A theme without them gives each
/// language its own colours back. Colours are changed in place on the shared definitions (one per language and scheme),
/// so open editors only need to redraw: <see cref="EditorColorsChanged"/> tells them when.
/// </summary>
public static class SyntaxPaletteApplier
{
    public static readonly IReadOnlyList<string> Roles = ["Keyword", "Type", "Function", "String", "Number", "Preprocessor", "Variable", "Comment", "Punctuation"];

    private static readonly ConditionalWeakTable<IHighlightingDefinition, object> Definitions = new();
    private static readonly ConditionalWeakTable<HighlightingColor, StrongBox<HighlightingBrush?>> Originals = new();
    private static readonly object Gate = new();
    private static IReadOnlyDictionary<string, Color> _active = new Dictionary<string, Color>();
    private static int _subscribed;
    private static int _refreshQueued;

    /// <summary>
    /// The theme changed (raised on the UI thread, once per burst of changes, after the syntax colours were updated):
    /// editors re-read their colours and redraw.
    /// </summary>
    public static event Action? EditorColorsChanged;

    /// <summary>The role a language's named colour plays, or <c>null</c> when it has none (it keeps its own colour).</summary>
    public static string? RoleOf(string? colorName)
    {
        if (string.IsNullOrEmpty(colorName)) return null;
        bool Has(string part) => colorName.Contains(part, StringComparison.OrdinalIgnoreCase);
        if (Has("Comment")) return "Comment";
        if (Has("String") || Has("Char") || Has("Interpolation") || Has("Escape") || Has("Regex")) return "String";
        if (Has("Number")) return "Number";
        if (Has("Preprocessor") || Has("Directive") || Has("Annotation") || Has("Attribute")) return "Preprocessor";
        if (Has("Punctuation") || Has("Operator") && !Has("Keyword")) return "Punctuation";
        if (Has("Method") || Has("Function") || Has("Builtin")) return "Function";
        if (string.Equals(colorName, "Type", StringComparison.OrdinalIgnoreCase) || string.Equals(colorName, "Class", StringComparison.OrdinalIgnoreCase)) return "Type";
        if (Has("Variable") || Has("Parameter") && !Has("Modifier")) return "Variable";
        if (Has("Keyword") || Has("Modifier") || Has("Visibility") || Has("Control") || Has("Storage") || Has("Self") || Has("This")
            || Has("TrueFalse") || Has("Null") || Has("GetSet")) return "Keyword";
        return null;
    }

    /// <summary>Gives <paramref name="definition"/> the active theme's syntax colours and returns it (for chaining).</summary>
    public static IHighlightingDefinition? Themed(IHighlightingDefinition? definition)
    {
        if (definition == null) return null;
        EnsureSubscribed();
        lock (Gate)
        {
            Definitions.AddOrUpdate(definition, Gate);
            Apply(definition, _active);
        }

        return definition;
    }

    /// <summary>Applies the given role colours (an empty map restores every language's own colours). For tests and tools.</summary>
    public static void Apply(IHighlightingDefinition definition, IReadOnlyDictionary<string, Color> roleColors)
    {
        ArgumentNullException.ThrowIfNull(definition);
        foreach (var color in definition.NamedHighlightingColors)
        {
            var original = Originals.GetValue(color, c => new StrongBox<HighlightingBrush?>(c.Foreground));
            var role = RoleOf(color.Name);
            if (role != null && roleColors.TryGetValue(role, out var themed))
            {
                if (color.Foreground is not ThemedBrush current || current.Color != themed) color.Foreground = new ThemedBrush(themed);
            }
            else if (!ReferenceEquals(color.Foreground, original.Value))
            {
                color.Foreground = original.Value;
            }
        }
    }

    /// <summary>The active theme's syntax colours by role (empty when the theme has none).</summary>
    public static IReadOnlyDictionary<string, Color> ReadActiveTheme()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var role in Roles)
        {
            if (ThemeTokens.TryGet(engine, $"Syntax{role}Brush", out var hex) && Color.TryParse(hex, out var color)) colors[role] = color;
        }

        return colors;
    }

    /// <summary>Re-reads the active theme and recolours every definition in use. Returns whether the syntax colours changed.</summary>
    public static bool Refresh()
    {
        var next = ReadActiveTheme();
        lock (Gate)
        {
            if (SameColors(_active, next)) return false;
            _active = next;
            foreach (var (definition, _) in Definitions) Apply(definition, next);
        }

        return true;
    }

    /// <summary>Subscribes to theme changes (done on first use; editors call it when they attach).</summary>
    public static void EnsureSubscribed()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 1) return;
        _active = ReadActiveTheme();
        StudioAppContext.Instance.ThemeEngine.ThemeChanged += _ =>
        {
            // A slider drag changes the theme many times a second: recolour once per burst, on the UI thread, after the
            // theme's resources were committed.
            if (Avalonia.Application.Current == null)
            {
                Refresh();
                EditorColorsChanged?.Invoke();
                return;
            }

            if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                Refresh();
                EditorColorsChanged?.Invoke();
            }, DispatcherPriority.Background);
        };
    }

    private static bool SameColors(IReadOnlyDictionary<string, Color> a, IReadOnlyDictionary<string, Color> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (role, color) in a)
        {
            if (!b.TryGetValue(role, out var other) || other != color) return false;
        }

        return true;
    }

    // A theme colour on a highlighting rule (told apart from a language's own brush, which is put back when needed).
    private sealed class ThemedBrush(Color color) : HighlightingBrush
    {
        private readonly ImmutableSolidColorBrush _brush = new(color);

        public Color Color { get; } = color;

        public override IBrush GetBrush(AvaloniaEdit.Rendering.ITextRunConstructionContext context) => _brush;

        public override string ToString() => Color.ToString();
    }
}

/// <summary>Reads a token of the active theme without touching UI resources (safe on any thread).</summary>
public static class ThemeTokens
{
    public static bool TryGet(DynamicThemeEngine engine, string key, out string hex)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (engine.Overrides.TryGetValue(key, out var overridden) && !string.IsNullOrWhiteSpace(overridden))
        {
            hex = overridden;
            return true;
        }

        if (engine.GetTheme(engine.ActiveThemeId) is { } theme && theme.Colors.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            hex = value;
            return true;
        }

        hex = string.Empty;
        return false;
    }
}
