using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

/// <summary>
/// Minimal read-only, syntax-highlighted code viewer (C# unless <see cref="Language"/> says otherwise) for static snippets
/// (e.g. documentation cards). Unlike <see cref="BindableTextEditor"/> it has
/// no folding, completion, search panel, or debugging chrome.
/// </summary>
public class CodeViewer : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodeViewer, string?>(nameof(Code));

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>The snippet's language ("csharp", "python"…), which picks its highlighting; C# when unset or unknown.</summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<CodeViewer, string?>(nameof(Language));

    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    private static readonly FontFamily s_defaultCodeFont = new("JetBrains Mono, Menlo, Monaco, Consolas, Roboto Mono, monospace");
    private static readonly IBrush s_darkForeground = new SolidColorBrush(Color.Parse("#D4D4D4"));
    private static readonly IBrush s_darkLink = new SolidColorBrush(Color.Parse("#4FC1FF"));
    private static readonly IBrush s_lightForeground = new SolidColorBrush(Color.Parse("#1E293B"));
    private static readonly IBrush s_lightLink = new SolidColorBrush(Color.Parse("#2563EB"));

    public CodeViewer()
    {
        IsReadOnly = true;
        ShowLineNumbers = false;
        WordWrap = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Background = Brushes.Transparent;
        FontFamily = Application.Current != null && Application.Current.TryFindResource("DsCodeFontFamily", out var fontRes) && fontRes is FontFamily ff ? ff : s_defaultCodeFont;
        FontSize = 12;

        Options.HighlightCurrentLine = false;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;

        ApplyThemeVariant();
        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush brush)
            return brush;
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush appBrush)
            return appBrush;
        return fallback;
    }

    private void ApplyThemeVariant()
    {
        bool isDark = ActualThemeVariant == ThemeVariant.Dark ||
                      (ActualThemeVariant != ThemeVariant.Light && (Application.Current?.ActualThemeVariant == ThemeVariant.Dark));

        var language = StudioLanguageServices.Default.Registry.Get(Language);
        if (isDark)
        {
            SyntaxHighlighting = language?.GetHighlighting(isDark: true) ?? CSharpSyntaxHighlightingTheme.GetDarkTheme();
            Foreground = ResolveBrush("EditorFgBrush", s_darkForeground);
            TextArea.TextView.LinkTextForegroundBrush = ResolveBrush("EditorLinkBrush", s_darkLink);
        }
        else
        {
            SyntaxHighlighting = language?.GetHighlighting(isDark: false) ?? CSharpSyntaxHighlightingTheme.GetLightTheme();
            Foreground = ResolveBrush("EditorFgBrush", s_lightForeground);
            TextArea.TextView.LinkTextForegroundBrush = ResolveBrush("EditorLinkBrush", s_lightLink);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CodeProperty)
        {
            Text = change.GetNewValue<string?>() ?? string.Empty;
        }
        else if (change.Property == LanguageProperty)
        {
            ApplyThemeVariant();
        }
    }
}
