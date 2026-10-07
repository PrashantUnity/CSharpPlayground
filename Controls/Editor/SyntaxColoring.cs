using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using TextMateSharp.Themes;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

/// <summary>
/// Colours an editor's code the way VS Code does: the language's TextMate grammar under the theme's TextMate colours
/// (<see cref="StudioTextMate"/>), and for C# the roles Roslyn's syntax tree gives each word on top, as VS Code's C#
/// extension does (its grammar alone can't follow top-level code, which every script and notebook cell is). A language
/// without a grammar keeps its own highlighting rules. Follows every theme switch.
/// </summary>
public static class SyntaxColoring
{
    private static readonly ConditionalWeakTable<TextEditor, EditorColoring> Editors = new();
    private static bool _textMateUnavailable;
    private static int _subscribed;
    private static int _refreshQueued;

    /// <summary>
    /// Colours <paramref name="editor"/> for <paramref name="language"/> (C# when null) under the active theme, or turns
    /// colouring off. <paramref name="fileExtension"/>: the open file's, which picks the grammar for a file opened as
    /// plain text (.json, .md, .yaml, ...). Cheap to call again: only what changed is redone. UI thread.
    /// </summary>
    public static void Apply(TextEditor editor, ILanguageDefinition? language, bool isDark, bool enabled = true, string? fileExtension = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        EnsureSubscribed();
        Editors.GetValue(editor, e => new EditorColoring(e)).Update(language, isDark, enabled, fileExtension);
    }

    /// <summary>Whether the editor is coloured by a TextMate grammar (false: off, or the language's own rules).</summary>
    public static bool UsesTextMate(TextEditor editor) => Editors.TryGetValue(editor, out var coloring) && coloring.Installation != null;

    /// <summary>The grammar scope the editor is coloured with, or null.</summary>
    public static string? ScopeOf(TextEditor editor) => Editors.TryGetValue(editor, out var coloring) ? coloring.Scope : null;

    /// <summary>The C# layer's tree is up to date with the editor's text (tests and tools).</summary>
    public static System.Threading.Tasks.Task WhenParsed(TextEditor editor) =>
        Editors.TryGetValue(editor, out var coloring) && coloring.Overlay?.Syntax is { } syntax ? syntax.WhenIdle() : System.Threading.Tasks.Task.CompletedTask;

    /// <summary>Gives every editor the active theme's colours again (done after each theme switch). UI thread.</summary>
    public static void RefreshThemes()
    {
        Interlocked.Exchange(ref _refreshQueued, 0);
        foreach (var (_, coloring) in Editors) coloring.RefreshTheme();
    }

    private static void EnsureSubscribed()
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 1) return;

        // One refresh per burst of theme changes (a theme, then its overrides), on the UI thread.
        StudioAppContext.Instance.ThemeEngine.ThemeChanged += _ =>
        {
            if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
            Dispatcher.UIThread.Post(RefreshThemes, DispatcherPriority.Background);
        };
    }

    private static ThemeDefinitionAndScheme ActiveTheme(bool isDark)
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        return new(engine.GetTheme(engine.ActiveThemeId), isDark);
    }

    private readonly record struct ThemeDefinitionAndScheme(FrySharp.Sdk.ThemeDefinition? Theme, bool IsDark);

    private sealed class EditorColoring(TextEditor editor)
    {
        public TextMate.Installation? Installation { get; private set; }
        public string? Scope { get; private set; }
        public CSharpRoleColorizer? Overlay { get; private set; }
        private IRawTheme? _theme;
        private bool _isDark;

        public void Update(ILanguageDefinition? language, bool isDark, bool enabled, string? fileExtension)
        {
            _isDark = isDark;
            var scope = enabled && !_textMateUnavailable ? StudioTextMate.ScopeFor(language, fileExtension) : null;
            if (!enabled || scope == null || !TryInstall())
            {
                Uninstall();
                editor.SyntaxHighlighting = enabled
                    ? SyntaxPaletteApplier.Themed(language != null ? language.GetHighlighting(isDark) : isDark ? CSharpSyntaxHighlightingTheme.GetDarkTheme() : CSharpSyntaxHighlightingTheme.GetLightTheme())
                    : null;
                return;
            }

            editor.SyntaxHighlighting = null;
            if (Scope != scope)
            {
                Installation!.SetGrammar(scope);
                Scope = scope;
            }

            var isCSharp = scope == StudioTextMate.ScopeForLanguageId("csharp");
            if (isCSharp && Overlay == null)
            {
                Overlay = new CSharpRoleColorizer(editor);
                editor.TextArea.TextView.LineTransformers.Add(Overlay);
                _theme = null; // gives the layer its colours below
            }
            else if (!isCSharp && Overlay != null)
            {
                RemoveOverlay();
            }

            RefreshTheme();
        }

        public void RefreshTheme()
        {
            if (Installation == null) return;
            var active = ActiveTheme(_isDark);
            var theme = StudioTextMate.ThemeFor(active.Theme, active.IsDark);
            if (ReferenceEquals(theme, _theme)) return;
            _theme = theme;
            Installation.SetTheme(theme);
            Overlay?.SetColors(StudioTextMate.CSharpRoleColors(theme));
        }

        private bool TryInstall()
        {
            if (Installation != null) return true;
            try
            {
                Installation = editor.InstallTextMate(StudioTextMate.Options, initCurrentDocument: true,
                    exceptionHandler: ex => Debug.WriteLine($"[SyntaxColoring] {ex.Message}"));
                Scope = null;
                _theme = null;
                return true;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
            {
                // The grammars' regex engine is native; without it for this platform, every language keeps its own rules.
                Debug.WriteLine($"[SyntaxColoring] TextMate unavailable: {ex.Message}");
                _textMateUnavailable = true;
                Installation = null;
                return false;
            }
        }

        private void Uninstall()
        {
            RemoveOverlay();
            Installation?.Dispose();
            Installation = null;
            Scope = null;
            _theme = null;
        }

        private void RemoveOverlay()
        {
            if (Overlay == null) return;
            editor.TextArea.TextView.LineTransformers.Remove(Overlay);
            Overlay.Dispose();
            Overlay = null;
        }
    }
}

/// <summary>
/// The C# layer over the grammar's colours: each word gets the colour of the role Roslyn's syntax tree gives it (type,
/// method, property, local, namespace, keyword, ...), from the same theme. Reads the tree kept by
/// <see cref="CSharpLiveSyntax"/>; nothing is parsed on the UI thread.
/// </summary>
public sealed class CSharpRoleColorizer : DocumentColorizingTransformer, IDisposable
{
    private readonly TextEditor _editor;
    private Dictionary<CSharpRole, IBrush> _brushes = new();
    private int _redrawQueued;

    public CSharpRoleColorizer(TextEditor editor)
    {
        _editor = editor;
        _editor.DocumentChanged += OnDocumentChanged;
        Attach(editor.Document);
    }

    public CSharpLiveSyntax? Syntax { get; private set; }

    public void SetColors(IReadOnlyDictionary<CSharpRole, string> colors)
    {
        var brushes = new Dictionary<CSharpRole, IBrush>();
        foreach (var (role, hex) in colors)
        {
            if (TryParse(hex, out var color)) brushes[role] = new ImmutableSolidColorBrush(color);
        }

        _brushes = brushes;
        _editor.TextArea.TextView.Redraw();
    }

    // TextMate writes #RRGGBBAA; Avalonia reads #AARRGGBB.
    private static bool TryParse(string hex, out Color color)
    {
        if (hex.Length == 9 && hex[0] == '#') hex = "#" + hex.Substring(7, 2) + hex.Substring(1, 6);
        return Color.TryParse(hex, out color);
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => Attach(_editor.Document);

    private void Attach(TextDocument? document)
    {
        if (Syntax != null)
        {
            Syntax.Updated -= OnUpdated;
            Syntax.Dispose();
        }

        Syntax = document == null ? null : new CSharpLiveSyntax(document);
        if (Syntax != null) Syntax.Updated += OnUpdated;
    }

    // A newer tree (background thread): one redraw of the visible lines.
    private void OnUpdated()
    {
        if (Interlocked.Exchange(ref _redrawQueued, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _redrawQueued, 0);
            _editor.TextArea.TextView.Redraw();
        }, DispatcherPriority.Background);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Syntax == null || _brushes.Count == 0 || line.Length == 0) return;
        foreach (var span in Syntax.Classify(line.Offset, line.EndOffset))
        {
            if (!_brushes.TryGetValue(span.Role, out var brush)) continue;
            ChangeLinePart(span.Start, span.End, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    public void Dispose()
    {
        _editor.DocumentChanged -= OnDocumentChanged;
        if (Syntax != null)
        {
            Syntax.Updated -= OnUpdated;
            Syntax.Dispose();
            Syntax = null;
        }
    }
}
