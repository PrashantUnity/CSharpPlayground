using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Docs;

/// <summary>
/// Renders standard CommonMark and GitHub-Flavored Markdown (GFM) powered by Markdig:
/// headings (H1-H6), paragraphs, **bold**, *italic*, ~~strikethrough~~, `code`,
/// bullet and numbered lists, &gt; blockquotes, ``` fenced code blocks, GFM pipe tables,
/// and horizontal rules.
/// </summary>
public class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly FontFamily Monospace = new("Cascadia Code, Consolas, Menlo, monospace");

    private static readonly IBrush CodeBrush = new SolidColorBrush(Color.FromArgb(46, 128, 128, 128));
    private static readonly IBrush CodeBlockBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
    private static readonly IBrush QuoteBrush = new SolidColorBrush(Color.FromArgb(22, 88, 166, 255));
    private static readonly IBrush QuoteAccentBrush = new SolidColorBrush(Color.FromArgb(170, 88, 166, 255));
    private static readonly IBrush RuleBrush = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128));

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

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
        var raw = Markdown ?? string.Empty;

        try
        {
            var document = Markdig.Markdown.Parse(raw, Pipeline);
            foreach (var block in RenderBlocks(document))
            {
                panel.Children.Add(block);
            }
        }
        catch
        {
            panel.Children.Add(new SelectableTextBlock { Text = raw, TextWrapping = TextWrapping.Wrap });
        }

        Content = panel;
    }

    private IEnumerable<Control> RenderBlocks(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                {
                    int level = heading.Level;
                    double size = FontSize * (level switch { 1 => 1.55, 2 => 1.32, 3 => 1.16, _ => 1.05 });
                    var text = RenderInlines(heading.Inline, size, FontWeight.Bold);
                    text.Margin = new Thickness(0, level <= 2 ? 8 : 4, 0, 2);
                    yield return text;
                    break;
                }

                case ParagraphBlock paragraph:
                {
                    yield return RenderInlines(paragraph.Inline, FontSize, FontWeight.Normal);
                    break;
                }

                case FencedCodeBlock fenced:
                {
                    var lines = fenced.Lines.Lines
                        .Take(fenced.Lines.Count)
                        .Select(l => l.ToString());
                    yield return CodeBlock(string.Join("\n", lines));
                    break;
                }

                case CodeBlock code:
                {
                    var lines = code.Lines.Lines
                        .Take(code.Lines.Count)
                        .Select(l => l.ToString());
                    yield return CodeBlock(string.Join("\n", lines));
                    break;
                }

                case QuoteBlock quote:
                {
                    var quotePanel = new StackPanel { Spacing = 6 };
                    foreach (var inner in RenderBlocks(quote))
                    {
                        quotePanel.Children.Add(inner);
                    }
                    yield return new Border
                    {
                        Background = QuoteBrush,
                        BorderBrush = QuoteAccentBrush,
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        CornerRadius = new CornerRadius(0, 6, 6, 0),
                        Padding = new Thickness(12, 8),
                        Margin = new Thickness(0, 4, 0, 4),
                        Child = quotePanel
                    };
                    break;
                }

                case ListBlock list:
                {
                    yield return RenderList(list);
                    break;
                }

                case Table table:
                {
                    yield return RenderTable(table);
                    break;
                }

                case ThematicBreakBlock:
                {
                    yield return new Border
                    {
                        Height = 1,
                        Background = RuleBrush,
                        Margin = new Thickness(0, 6)
                    };
                    break;
                }
            }
        }
    }

    private Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 };
        int itemIndex = 1;

        foreach (var item in list)
        {
            if (item is ListItemBlock listItem)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                var marker = list.IsOrdered ? $"{itemIndex++}." : "•";
                var bullet = new TextBlock
                {
                    Text = marker,
                    FontSize = FontSize,
                    FontWeight = list.IsOrdered ? FontWeight.SemiBold : FontWeight.Normal,
                    MinWidth = list.IsOrdered ? 22 : 14,
                    LineHeight = FontSize * 1.45,
                    VerticalAlignment = VerticalAlignment.Top
                };

                var itemPanel = new StackPanel { Spacing = 4 };
                foreach (var inner in RenderBlocks(listItem))
                {
                    itemPanel.Children.Add(inner);
                }

                Grid.SetColumn(itemPanel, 1);
                row.Children.Add(bullet);
                row.Children.Add(itemPanel);
                panel.Children.Add(row);
            }
        }
        return panel;
    }

    private Control RenderTable(Table table)
    {
        var headers = new List<string>();
        var alignments = new List<TextAlignment>();
        var rows = new List<string[]>();

        if (table.ColumnDefinitions != null)
        {
            foreach (var col in table.ColumnDefinitions)
            {
                alignments.Add(col.Alignment switch
                {
                    TableColumnAlign.Center => TextAlignment.Center,
                    TableColumnAlign.Right => TextAlignment.Right,
                    _ => TextAlignment.Left
                });
            }
        }

        foreach (var block in table)
        {
            if (block is TableRow row)
            {
                var cellTexts = new List<string>();
                foreach (var cellObj in row)
                {
                    if (cellObj is TableCell cell)
                    {
                        var cellStr = new StringBuilder();
                        foreach (var inner in cell)
                        {
                            if (inner is ParagraphBlock p && p.Inline != null)
                            {
                                foreach (var inline in p.Inline)
                                {
                                    ExtractInlineText(inline, cellStr);
                                }
                            }
                        }
                        cellTexts.Add(cellStr.ToString().Trim());
                    }
                }

                if (row.IsHeader)
                {
                    headers.AddRange(cellTexts);
                }
                else
                {
                    rows.Add(cellTexts.ToArray());
                }
            }
        }

        return TableControl(headers.ToArray(), alignments.ToArray(), rows);
    }

    private static void ExtractInlineText(Markdig.Syntax.Inlines.Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline lit:
                sb.Append(lit.Content);
                break;
            case CodeInline code:
                sb.Append(code.Content);
                break;
            case ContainerInline container:
                foreach (var child in container) ExtractInlineText(child, sb);
                break;
            default:
                var s = inline.ToString();
                if (!string.IsNullOrEmpty(s)) sb.Append(s);
                break;
        }
    }

    private Control TableControl(string[] headers, TextAlignment[] alignments, List<string[]> rows)
    {
        if (headers.Length == 0 && rows.Count == 0) return new Panel();

        int colCount = Math.Max(headers.Length, rows.Count > 0 ? rows.Max(r => r.Length) : 0);
        if (colCount == 0) return new Panel();

        var grid = new Grid();
        for (int c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        int totalRows = (headers.Length > 0 ? 1 : 0) + rows.Count;
        for (int r = 0; r < totalRows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        int rowIndex = 0;
        if (headers.Length > 0)
        {
            var headerBg = new Border
            {
                Background = CodeBlockBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = RuleBrush
            };
            Grid.SetRow(headerBg, 0);
            Grid.SetColumnSpan(headerBg, colCount);
            grid.Children.Add(headerBg);

            for (int c = 0; c < headers.Length; c++)
            {
                var align = c < alignments.Length ? alignments[c] : TextAlignment.Left;
                var text = Text(headers[c], FontSize, FontWeight.SemiBold);
                text.TextAlignment = align;

                var cell = new Border
                {
                    Padding = new Thickness(12, 7),
                    BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, 0),
                    BorderBrush = RuleBrush,
                    Child = text
                };
                Grid.SetRow(cell, 0);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
            rowIndex++;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            var rowData = rows[r];
            bool isLastRow = (r == rows.Count - 1);
            int curGridRow = rowIndex + r;

            if (r % 2 == 1)
            {
                var zebraBg = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(14, 128, 128, 128))
                };
                Grid.SetRow(zebraBg, curGridRow);
                Grid.SetColumnSpan(zebraBg, colCount);
                grid.Children.Add(zebraBg);
            }

            for (int c = 0; c < colCount; c++)
            {
                string cellText = c < rowData.Length ? rowData[c] : string.Empty;
                var align = c < alignments.Length ? alignments[c] : TextAlignment.Left;
                var text = Text(cellText, FontSize, FontWeight.Normal);
                text.TextAlignment = align;

                var cell = new Border
                {
                    Padding = new Thickness(12, 6),
                    BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, isLastRow ? 0 : 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(35, 128, 128, 128)),
                    Child = text
                };
                Grid.SetRow(cell, curGridRow);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return new Border
        {
            BorderBrush = RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            ClipToBounds = true,
            Margin = new Thickness(0, 4, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = grid
        };
    }

    private Control CodeBlock(string code) => new Border
    {
        Background = CodeBlockBrush,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(12, 10),
        Margin = new Thickness(0, 4, 0, 4),
        Child = new SelectableTextBlock
        {
            Text = code,
            FontFamily = Monospace,
            FontSize = Math.Max(10, FontSize - 0.5),
            TextWrapping = TextWrapping.Wrap
        }
    };

    private SelectableTextBlock RenderInlines(ContainerInline? inlines, double fontSize, FontWeight weight)
    {
        var block = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = weight,
            LineHeight = fontSize * 1.45
        };

        if (inlines != null)
        {
            foreach (var inline in inlines)
            {
                foreach (var avaloniaInline in ConvertInline(inline))
                {
                    block.Inlines!.Add(avaloniaInline);
                }
            }
        }

        return block;
    }

    private IEnumerable<AvaloniaInline> ConvertInline(MdInline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                yield return new Run(lit.Content.ToString());
                break;

            case CodeInline code:
                yield return new Run(code.Content) { FontFamily = Monospace, Background = CodeBrush };
                break;

            case EmphasisInline emp:
                if (emp.DelimiterCount == 2)
                {
                    var bold = new Bold();
                    foreach (var child in emp)
                        foreach (var inner in ConvertInline(child))
                            bold.Inlines.Add(inner);
                    yield return bold;
                }
                else
                {
                    var italic = new Italic();
                    foreach (var child in emp)
                        foreach (var inner in ConvertInline(child))
                            italic.Inlines.Add(inner);
                    yield return italic;
                }
                break;

            case LinkInline link:
                var underline = new Underline();
                foreach (var child in link)
                    foreach (var inner in ConvertInline(child))
                        underline.Inlines.Add(inner);
                yield return underline;
                break;

            case LineBreakInline:
                yield return new LineBreak();
                break;

            case ContainerInline container:
                foreach (var child in container)
                    foreach (var inner in ConvertInline(child))
                        yield return inner;
                break;

            default:
                var str = inline.ToString();
                if (!string.IsNullOrEmpty(str)) yield return new Run(str);
                break;
        }
    }

    private static SelectableTextBlock Text(string text, double fontSize, FontWeight weight)
    {
        return new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = weight,
            LineHeight = fontSize * 1.45
        };
    }
}
