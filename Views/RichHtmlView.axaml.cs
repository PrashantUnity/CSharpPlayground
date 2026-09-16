using System;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// Lenient, never-throwing renderer for the small HTML subset Display.Html/Display.Markdown
/// produce: headings, bold/italic, inline code, links (styled only, not clickable), paragraphs/
/// line breaks, and simple lists. Rebuilt imperatively from a plain string DataContext, the same
/// "bare panel shell rebuilt on DataContextChanged" pattern DumpTableView uses for tables.
/// Unrecognized tags are silently swallowed (not rendered as literal text) and any parse failure
/// falls back to plain, tag-stripped text — this must never throw into the UI.
/// </summary>
public partial class RichHtmlView : UserControl
{
    private static readonly Regex BlockRegex = new(
        @"<h(?<level>[1-6])[^>]*>(?<hcontent>.*?)</h\k<level>>" +
        @"|<p[^>]*>(?<pcontent>.*?)</p>" +
        @"|<ul[^>]*>(?<ulcontent>.*?)</ul>" +
        @"|<ol[^>]*>(?<olcontent>.*?)</ol>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex ListItemRegex = new(
        @"<li[^>]*>(?<content>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex InlineTokenRegex = new(
        @"(?<tag><\s*(?<close>/)?\s*(?<name>[a-zA-Z0-9]+)\b[^>]*>)|(?<text>[^<]+)",
        RegexOptions.IgnoreCase);

    private static readonly Regex HrefRegex = new(
        "href\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);

    private static readonly Regex TagStripRegex = new("<[^>]*>", RegexOptions.Singleline);

    private static readonly FontFamily MonospaceFont =
        new("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace");

    private IBrush _onSurfaceBrush = Brushes.White;
    private IBrush _linkBrush = Brushes.CornflowerBlue;
    private IBrush _codeBrush = Brushes.Orange;

    public RichHtmlView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebuild();
        ActualThemeVariantChanged += (_, _) => Rebuild();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b) return b;
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush appB) return appB;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void Rebuild()
    {
        var root = this.FindControl<StackPanel>("RootPanel");
        if (root == null) return;

        root.Children.Clear();

        if (DataContext is not string html || string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        _onSurfaceBrush = ResolveBrush("M3OnSurfaceBrush", "#E2E8F0");
        _linkBrush = ResolveBrush("M3PrimaryBrush", "#7C9CFF");
        _codeBrush = ResolveBrush("M3TertiaryBrush", "#F4B860");

        try
        {
            RenderBlocks(root, html);
        }
        catch
        {
            root.Children.Clear();
            root.Children.Add(new SelectableTextBlock
            {
                Text = TagStripRegex.Replace(html, string.Empty),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = _onSurfaceBrush
            });
        }
    }

    private void RenderBlocks(StackPanel root, string html)
    {
        var matches = BlockRegex.Matches(html);
        if (matches.Count == 0)
        {
            AppendParagraph(root, html);
            return;
        }

        int pos = 0;
        foreach (Match m in matches)
        {
            if (m.Index > pos)
            {
                var loose = html.Substring(pos, m.Index - pos);
                if (TagStripRegex.Replace(loose, string.Empty).Trim().Length > 0)
                {
                    AppendParagraph(root, loose);
                }
            }

            if (m.Groups["level"].Success)
            {
                AppendHeading(root, int.Parse(m.Groups["level"].Value), m.Groups["hcontent"].Value);
            }
            else if (m.Groups["pcontent"].Success)
            {
                AppendParagraph(root, m.Groups["pcontent"].Value);
            }
            else if (m.Groups["ulcontent"].Success)
            {
                AppendList(root, m.Groups["ulcontent"].Value, ordered: false);
            }
            else if (m.Groups["olcontent"].Success)
            {
                AppendList(root, m.Groups["olcontent"].Value, ordered: true);
            }

            pos = m.Index + m.Length;
        }

        if (pos < html.Length)
        {
            var tail = html.Substring(pos);
            if (TagStripRegex.Replace(tail, string.Empty).Trim().Length > 0)
            {
                AppendParagraph(root, tail);
            }
        }
    }

    private void AppendHeading(StackPanel root, int level, string innerHtml)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = level switch { 1 => 22, 2 => 19, 3 => 17, 4 => 15.5, 5 => 14.5, _ => 14 },
            FontWeight = FontWeight.Bold,
            Foreground = _onSurfaceBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, level <= 2 ? 8 : 4, 0, 6)
        };
        AppendInlines(tb.Inlines!, innerHtml);
        if (tb.Inlines!.Count > 0)
        {
            root.Children.Add(tb);
        }
    }

    private void AppendParagraph(StackPanel root, string innerHtml)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = 13,
            Foreground = _onSurfaceBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        AppendInlines(tb.Inlines!, innerHtml);
        if (tb.Inlines!.Count > 0)
        {
            root.Children.Add(tb);
        }
    }

    private void AppendList(StackPanel root, string innerHtml, bool ordered)
    {
        var items = ListItemRegex.Matches(innerHtml);
        if (items.Count == 0)
        {
            AppendParagraph(root, innerHtml);
            return;
        }

        var listPanel = new StackPanel { Margin = new Thickness(4, 0, 0, 8), Spacing = 3 };
        int i = 1;
        foreach (Match li in items)
        {
            var line = new SelectableTextBlock
            {
                FontSize = 13,
                Foreground = _onSurfaceBrush,
                TextWrapping = TextWrapping.Wrap
            };
            line.Inlines!.Add(new Run(ordered ? $"{i}. " : "•  ") { FontWeight = FontWeight.SemiBold });
            AppendInlines(line.Inlines, li.Groups["content"].Value);
            listPanel.Children.Add(line);
            i++;
        }
        root.Children.Add(listPanel);
    }

    private void AppendInlines(InlineCollection inlines, string innerHtml)
    {
        bool bold = false, italic = false, code = false;
        string? pendingHref = null;

        foreach (Match tok in InlineTokenRegex.Matches(innerHtml))
        {
            if (tok.Groups["text"].Success)
            {
                var text = WebUtility.HtmlDecode(tok.Groups["text"].Value);
                if (text.Length == 0) continue;
                inlines.Add(BuildRun(text, bold, italic, code, pendingHref != null));
                continue;
            }

            var name = tok.Groups["name"].Value.ToLowerInvariant();
            bool isClose = tok.Groups["close"].Success;

            switch (name)
            {
                case "b":
                case "strong":
                    bold = !isClose;
                    break;
                case "i":
                case "em":
                    italic = !isClose;
                    break;
                case "code":
                    code = !isClose;
                    break;
                case "a":
                    if (isClose)
                    {
                        pendingHref = null;
                    }
                    else
                    {
                        var hrefMatch = HrefRegex.Match(tok.Groups["tag"].Value);
                        pendingHref = hrefMatch.Success ? hrefMatch.Groups[1].Value : string.Empty;
                    }
                    break;
                case "br":
                    if (!isClose) inlines.Add(new LineBreak());
                    break;
                default:
                    // Unknown/unsupported tag (e.g. <div>, <span>, <table>) — swallow the tag
                    // itself rather than leaking it as literal text; its own text content (if
                    // any) still comes through via the surrounding "text" token matches.
                    break;
            }
        }
    }

    private Run BuildRun(string text, bool bold, bool italic, bool code, bool isLink)
    {
        var run = new Run(text)
        {
            Foreground = isLink ? _linkBrush : (code ? _codeBrush : _onSurfaceBrush),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal
        };
        if (code)
        {
            run.FontFamily = MonospaceFont;
        }
        if (isLink)
        {
            run.TextDecorations = TextDecorations.Underline;
        }
        return run;
    }
}
