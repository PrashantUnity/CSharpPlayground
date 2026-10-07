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
        // The layout's code font (Settings → Layout & Typography), and every change to it.
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.Tokens.SetFontFamily(this, "DsCodeFontFamily");
        // Code samples follow the layout's type ramp (body size).
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout.Tokens.SetFontSize(this, "DsFontSize300");

        Options.HighlightCurrentLine = false;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;

        ApplyThemeVariant();
        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EnsureSubscribed();
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EditorColorsChanged += OnEditorColorsChanged;
        ApplyThemeVariant();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.SyntaxPaletteApplier.EditorColorsChanged -= OnEditorColorsChanged;
    }

    private void OnEditorColorsChanged()
    {
        ApplyThemeVariant();
        TextArea.TextView.Redraw();
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
        SyntaxColoring.Apply(this, language, isDark);
        if (isDark)
        {
            Foreground = ResolveBrush("EditorFgBrush", s_darkForeground);
            TextArea.TextView.LinkTextForegroundBrush = ResolveBrush("EditorLinkBrush", s_darkLink);
        }
        else
        {
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
