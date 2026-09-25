using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Renders the Markdown the app writes (problem statements, hints, notebook notes): headings, paragraphs, **bold**,
/// *italic*, `code`, bullet and numbered lists (nested by indentation), &gt; quotes, ``` code blocks and --- rules.
/// Anything else is shown as plain text, so nothing written is ever lost.
/// </summary>
public class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly FontFamily Monospace = new("Cascadia Code, Consolas, Menlo, monospace");

    // Translucent so they sit well on both the light and the dark theme.
    private static readonly IBrush CodeBrush = new SolidColorBrush(Color.FromArgb(46, 128, 128, 128));
    private static readonly IBrush CodeBlockBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
    private static readonly IBrush QuoteBrush = new SolidColorBrush(Color.FromArgb(22, 88, 166, 255));
    private static readonly IBrush QuoteAccentBrush = new SolidColorBrush(Color.FromArgb(170, 88, 166, 255));
    private static readonly IBrush RuleBrush = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128));

    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex ListItem = new(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex InlineToken = new(
        @"(`[^`]+`)|(\*\*(?=\S)(.+?)(?<=\S)\*\*)|((?<![\*\w])\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?![\*\w]))|(\[([^\]]+)\]\(([^)\s]+)\))",
        RegexOptions.Compiled);

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static MarkdownView()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
        FontSizeProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Rebuild());
    }

    private void Rebuild()
    {
        var panel = new StackPanel { Spacing = 7 };
        foreach (var block in Blocks(Markdown ?? string.Empty))
        {
            panel.Children.Add(block);
        }
        Content = panel;
    }

    private IEnumerable<Control> Blocks(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();

        IEnumerable<Control> Flush()
        {
            if (paragraph.Count == 0) yield break;
            yield return Text(string.Join(" ", paragraph), FontSize, FontWeight.Normal);
            paragraph.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                foreach (var block in Flush()) yield return block;
                var code = new List<string>();
                while (++i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal)) code.Add(lines[i]);
                yield return CodeBlock(string.Join("\n", code));
                continue;
            }

            if (trimmed.Length == 0)
            {
                foreach (var block in Flush()) yield return block;
                continue;
            }

            if (Heading.Match(trimmed) is { Success: true } heading)
            {
                foreach (var block in Flush()) yield return block;
                int level = heading.Groups[1].Value.Length;
                double size = FontSize * (level switch { 1 => 1.55, 2 => 1.32, 3 => 1.16, _ => 1.05 });
                var text = Text(heading.Groups[2].Value, size, FontWeight.Bold);
                text.Margin = new Thickness(0, level <= 2 ? 6 : 4, 0, 0);
                yield return text;
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                foreach (var block in Flush()) yield return block;
                yield return new Border { Height = 1, Background = RuleBrush, Margin = new Thickness(0, 4) };
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                foreach (var block in Flush()) yield return block;
                var quote = new List<string>();
                for (; i < lines.Length && lines[i].TrimStart().StartsWith(">", StringComparison.Ordinal); i++)
                {
                    string inner = lines[i].TrimStart()[1..];
                    quote.Add(inner.StartsWith(' ') ? inner[1..] : inner);
                }
                i--;
                yield return Quote(string.Join("\n", quote));
                continue;
            }

            if (ListItem.IsMatch(line))
            {
                foreach (var block in Flush()) yield return block;
                var items = new List<(int Indent, string Marker, string Text)>();
                for (; i < lines.Length; i++)
                {
                    var match = ListItem.Match(lines[i]);
                    if (match.Success)
                    {
                        items.Add((match.Groups[1].Value.Length, match.Groups[2].Value, match.Groups[3].Value.Trim()));
                    }
                    else if (lines[i].Trim().Length > 0 && lines[i].StartsWith("  ", StringComparison.Ordinal) && items.Count > 0)
                    {
                        // An indented line continues the previous item.
                        var last = items[^1];
                        items[^1] = (last.Indent, last.Marker, $"{last.Text} {lines[i].Trim()}");
                    }
                    else
                    {
                        break;
                    }
                }
                i--;
                yield return List(items);
                continue;
            }

            paragraph.Add(trimmed);
        }

        foreach (var block in Flush()) yield return block;
    }

    private Control List(List<(int Indent, string Marker, string Text)> items)
    {
        var panel = new StackPanel { Spacing = 4 };
        var levels = items.Select(i => i.Indent).Distinct().OrderBy(i => i).ToList();
        foreach (var (indent, marker, text) in items)
        {
            int depth = levels.IndexOf(indent);
            bool numbered = char.IsDigit(marker[0]);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(depth * 18, 0, 0, 0) };
            var bullet = new TextBlock
            {
                Text = numbered ? marker : depth == 0 ? "•" : "◦",
                FontSize = FontSize,
                FontWeight = numbered ? FontWeight.SemiBold : FontWeight.Normal,
                MinWidth = numbered ? 22 : 14,
                LineHeight = FontSize * 1.45, // same as the item text, so the marker sits on its first line
                VerticalAlignment = VerticalAlignment.Top
            };
            var content = Text(text, FontSize, FontWeight.Normal);
            Grid.SetColumn(content, 1);
            row.Children.Add(bullet);
            row.Children.Add(content);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Control Quote(string markdown)
    {
        var inner = new MarkdownView { Markdown = markdown, FontSize = FontSize };
        return new Border
        {
            Background = QuoteBrush,
            BorderBrush = QuoteAccentBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            Padding = new Thickness(10, 7),
            Child = inner
        };
    }

    private Control CodeBlock(string code) => new Border
    {
        Background = CodeBlockBrush,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(10, 8),
        Child = new SelectableTextBlock
        {
            Text = code,
            FontFamily = Monospace,
            FontSize = Math.Max(10, FontSize - 0.5),
            TextWrapping = TextWrapping.Wrap
        }
    };

    private static SelectableTextBlock Text(string markdown, double fontSize, FontWeight weight)
    {
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = fontSize, FontWeight = weight, LineHeight = fontSize * 1.45 };
        block.Inlines!.AddRange(Inlines(markdown));
        return block;
    }

    // `code`, **bold**, *italic* and [text](link); bold and italic may contain the others.
    private static IEnumerable<Inline> Inlines(string text)
    {
        int position = 0;
        foreach (Match match in InlineToken.Matches(text))
        {
            if (match.Index > position) yield return new Run(text[position..match.Index]);

            if (match.Groups[1].Success)
            {
                yield return new Run(match.Value[1..^1]) { FontFamily = Monospace, Background = CodeBrush };
            }
            else if (match.Groups[2].Success)
            {
                var bold = new Bold();
                bold.Inlines.AddRange(Inlines(match.Groups[3].Value));
                yield return bold;
            }
            else if (match.Groups[4].Success)
            {
                var italic = new Italic();
                italic.Inlines.AddRange(Inlines(match.Groups[5].Value));
                yield return italic;
            }
            else
            {
                var link = new Underline();
                link.Inlines.Add(new Run(match.Groups[7].Value));
                yield return link;
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length) yield return new Run(text[position..]);
    }
}
