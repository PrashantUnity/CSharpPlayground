using System.Net;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A Markdown display, the same from every language: C# <c>Display.Markdown</c>, Python <c>fry.markdown</c> and JavaScript
/// <c>Display.markdown</c> (which send <c>text/markdown</c>). The output keeps the Markdown text, which a host with a real
/// Markdown renderer shows (the browser studio, Jupyter), and a basic HTML rendering of it for the desktop's HTML view.
/// </summary>
public static partial class MarkdownOutput
{
    /// <param name="markdown">The Markdown text.</param>
    public static RichCellOutput Create(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return new RichCellOutput
        {
            Kind = CellOutputKind.Html,
            HtmlContent = ToBasicHtml(markdown),
            MarkdownSource = markdown,
        };
    }

    /// <summary>
    /// Headers, bold, italic, inline code and links as HTML; everything else as text. Links go only to web and mail
    /// addresses: a <c>javascript:</c> link is shown as its text.
    /// </summary>
    public static string ToBasicHtml(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        // Encode the raw text first so the tags added below are the only real HTML: Markdown must never be able to
        // smuggle a <script> tag into the output.
        var html = WebUtility.HtmlEncode(markdown);
        html = Header().Replace(html, match => $"<h{match.Groups[1].Length}>{match.Groups[2].Value}</h{match.Groups[1].Length}>");
        html = Bold().Replace(html, "<b>$1</b>");
        html = Italic().Replace(html, "<i>$1</i>");
        html = InlineCode().Replace(html, "<code>$1</code>");
        html = Link().Replace(html, match => IsSafeLink(match.Groups[2].Value)
            ? $"<a href=\"{match.Groups[2].Value}\">{match.Groups[1].Value}</a>"
            : match.Groups[1].Value);
        return html.Replace("\r\n", "\n").Replace("\n", "<br/>");
    }

    // A link with no scheme (a page anchor or a relative path) or a web or mail address.
    private static bool IsSafeLink(string url) =>
        !url.Contains(':')
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^(#{1,6}) (.*)$", RegexOptions.Multiline)]
    private static partial Regex Header();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex Italic();

    [GeneratedRegex("`(.+?)`")]
    private static partial Regex InlineCode();

    // The address has no spaces and may hold one level of brackets, as CommonMark allows: wiki/C_(programming_language).
    [GeneratedRegex(@"\[(.+?)\]\(((?:[^()\s]|\([^()\s]*\))+)\)")]
    private static partial Regex Link();
}
