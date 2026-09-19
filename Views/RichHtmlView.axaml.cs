using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// Dual-mode HTML renderer for C# notebook cell outputs and scratchpads:
/// 1. Full HTML documents, games, dashboards, and interactive scripts (containing &lt;canvas&gt;,
///    &lt;script&gt;, &lt;style&gt;, or full HTML5 page markup) are hosted in a real NativeWebView
///    (backed by WKWebView on macOS and WebView2 on Windows) with toolbar controls for reload,
///    external browser launch, and viewport height expansion.
/// 2. Simple markdown-derived markup (&lt;h1-6&gt;, &lt;p&gt;, &lt;ul&gt;, bold, italic, code, links)
///    is rendered via lightweight native Avalonia text blocks with M3 theme brushes and full
///    sanitization (swallowing unknown tags and stripping raw CSS/JS blocks so code never leaks as text).
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
    private static readonly Regex CommentRegex = new(@"<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex StyleBlockRegex = new(@"<style[^>]*>.*?</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex ScriptBlockRegex = new(@"<script[^>]*>.*?</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex DoctypeRegex = new(@"<!DOCTYPE[^>]*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex TitleRegex = new(@"<\s*title[^>]*>(?<title>.*?)<\s*/\s*title\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly FontFamily MonospaceFont =
        new("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace");

    private const double CompactHeight = 500;
    private const double ExpandedHeight = 750;

    private IBrush _onSurfaceBrush = Brushes.White;
    private IBrush _linkBrush = Brushes.CornflowerBlue;
    private IBrush _codeBrush = Brushes.Orange;

    private NativeWebView? _currentWebView;
    private string? _currentHtml;
    private bool _isExpanded;

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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CleanupWebView();
    }

    /// <summary>
    /// Determines whether the HTML string represents a full HTML5 document, game, or rich web app
    /// requiring a real browser engine (NativeWebView) instead of plain text blocks.
    /// </summary>
    public static bool IsFullHtmlDocument(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return false;

        // Check for full document markers or interactive/scripted components
        if (DoctypeRegex.IsMatch(html)) return true;
        if (Regex.IsMatch(html, @"<\s*html\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*head\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*body\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*canvas\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*script\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*style\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*iframe\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(html, @"<\s*svg\b", RegexOptions.IgnoreCase)) return true;

        return false;
    }

    /// <summary>
    /// Extracts the &lt;title&gt; text from HTML if available, or returns a fallback descriptor.
    /// </summary>
    public static string ExtractTitle(string? html, string fallback = "HTML5 Interactive Web Document")
    {
        if (string.IsNullOrWhiteSpace(html)) return fallback;
        var match = TitleRegex.Match(html);
        if (match.Success)
        {
            var title = WebUtility.HtmlDecode(match.Groups["title"].Value).Trim();
            if (!string.IsNullOrEmpty(title)) return title;
        }
        return fallback;
    }

    /// <summary>
    /// Strips raw &lt;style&gt; and &lt;script&gt; blocks (including their inner code) so stylesheets
    /// and JS functions never leak as literal text in the fallback text renderer.
    /// </summary>
    public static string SanitizeHtmlForText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var sanitized = CommentRegex.Replace(html, string.Empty);
        sanitized = StyleBlockRegex.Replace(sanitized, string.Empty);
        sanitized = ScriptBlockRegex.Replace(sanitized, string.Empty);
        sanitized = DoctypeRegex.Replace(sanitized, string.Empty);
        return sanitized;
    }

    private IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b) return b;
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush appB) return appB;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void Rebuild()
    {
        var textContainer = this.FindControl<Border>("TextContainer");
        var webContainer = this.FindControl<Border>("WebContainer");
        var root = this.FindControl<StackPanel>("RootPanel");

        if (DataContext is not string html || string.IsNullOrWhiteSpace(html))
        {
            if (textContainer != null) textContainer.IsVisible = false;
            if (webContainer != null) webContainer.IsVisible = false;
            root?.Children.Clear();
            CleanupWebView();
            return;
        }

        _currentHtml = html;
        _onSurfaceBrush = ResolveBrush("M3OnSurfaceBrush", "#E2E8F0");
        _linkBrush = ResolveBrush("M3PrimaryBrush", "#7C9CFF");
        _codeBrush = ResolveBrush("M3TertiaryBrush", "#F4B860");

        if (IsFullHtmlDocument(html))
        {
            try
            {
                RenderWebView(html);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RichHtmlView] NativeWebView initialization fallback: {ex.Message}");
                // Fall back gracefully to sanitized text rendering
            }
        }

        RenderText(html);
    }

    private void RenderWebView(string html)
    {
        var textContainer = this.FindControl<Border>("TextContainer");
        var webContainer = this.FindControl<Border>("WebContainer");
        var webViewHost = this.FindControl<Border>("WebViewHost");
        var titleText = this.FindControl<TextBlock>("WebTitleText");
        var reloadBtn = this.FindControl<Button>("ReloadButton");
        var openBrowserBtn = this.FindControl<Button>("OpenBrowserButton");
        var heightToggleBtn = this.FindControl<Button>("HeightToggleButton");

        if (textContainer != null) textContainer.IsVisible = false;
        if (webContainer != null) webContainer.IsVisible = true;

        if (titleText != null)
        {
            titleText.Text = ExtractTitle(html);
        }

        if (webViewHost != null)
        {
            CleanupWebView();

            _currentWebView = new NativeWebView
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            webViewHost.Child = _currentWebView;
            _currentWebView.NavigateToString(html);
        }

        if (reloadBtn != null)
        {
            reloadBtn.Click -= OnReloadClicked;
            reloadBtn.Click += OnReloadClicked;
        }

        if (openBrowserBtn != null)
        {
            openBrowserBtn.Click -= OnOpenBrowserClicked;
            openBrowserBtn.Click += OnOpenBrowserClicked;
        }

        if (heightToggleBtn != null)
        {
            heightToggleBtn.Click -= OnHeightToggleClicked;
            heightToggleBtn.Click += OnHeightToggleClicked;
        }
    }

    private void OnReloadClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentWebView != null && !string.IsNullOrEmpty(_currentHtml))
        {
            _currentWebView.NavigateToString(_currentHtml);
        }
    }

    private void OnOpenBrowserClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentHtml)) return;

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"frypdf_preview_{Guid.NewGuid():N}.html");
            File.WriteAllText(tempFile, _currentHtml, System.Text.Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RichHtmlView] Could not open preview in external browser: {ex.Message}");
        }
    }

    private void OnHeightToggleClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var webViewHost = this.FindControl<Border>("WebViewHost");
        var symbol = this.FindControl<TextBlock>("HeightToggleSymbol");
        if (webViewHost == null) return;

        _isExpanded = !_isExpanded;
        webViewHost.Height = _isExpanded ? ExpandedHeight : CompactHeight;

        if (symbol != null)
        {
            symbol.Text = _isExpanded ? "⤡" : "⤢";
        }
    }

    private void CleanupWebView()
    {
        var webViewHost = this.FindControl<Border>("WebViewHost");
        if (webViewHost != null)
        {
            webViewHost.Child = null;
        }

        if (_currentWebView is IDisposable disposable)
        {
            try { disposable.Dispose(); }
            catch { /* best-effort cleanup */ }
        }
        _currentWebView = null;
    }

    private void RenderText(string html)
    {
        var textContainer = this.FindControl<Border>("TextContainer");
        var webContainer = this.FindControl<Border>("WebContainer");
        var root = this.FindControl<StackPanel>("RootPanel");

        CleanupWebView();

        if (webContainer != null) webContainer.IsVisible = false;
        if (textContainer != null) textContainer.IsVisible = true;
        if (root == null) return;

        root.Children.Clear();

        var sanitized = SanitizeHtmlForText(html);

        try
        {
            RenderBlocks(root, sanitized);
        }
        catch
        {
            root.Children.Clear();
            root.Children.Add(new SelectableTextBlock
            {
                Text = TagStripRegex.Replace(sanitized, string.Empty),
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
