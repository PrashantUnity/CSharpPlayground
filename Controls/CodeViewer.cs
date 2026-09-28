using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

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

    public CodeViewer()
    {
        IsReadOnly = true;
        ShowLineNumbers = false;
        WordWrap = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Consolas, Menlo, monospace");
        FontSize = 12;

        Options.HighlightCurrentLine = false;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;

        ApplyThemeVariant();
        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();
    }

    private void ApplyThemeVariant()
    {
        bool isDark = ActualThemeVariant == ThemeVariant.Dark ||
                      (ActualThemeVariant != ThemeVariant.Light && (Application.Current?.ActualThemeVariant == ThemeVariant.Dark));

        var language = StudioLanguageServices.Default.Registry.Get(Language);
        if (isDark)
        {
            SyntaxHighlighting = language?.GetHighlighting(isDark: true) ?? CSharpSyntaxHighlightingTheme.GetDarkTheme();
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
            TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.Parse("#4FC1FF"));
        }
        else
        {
            SyntaxHighlighting = language?.GetHighlighting(isDark: false) ?? CSharpSyntaxHighlightingTheme.GetLightTheme();
            Foreground = new SolidColorBrush(Color.Parse("#1E293B"));
            TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.Parse("#2563EB"));
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
