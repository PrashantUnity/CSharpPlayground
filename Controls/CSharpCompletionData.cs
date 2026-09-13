using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class CSharpCompletionData : ICompletionData
{
    private readonly CSharpCompletionItem _item;
    private object? _cachedContent;
    private object? _cachedDescription;

    public CSharpCompletionData(CSharpCompletionItem item)
    {
        _item = item;
    }

    public IImage? Image => null;

    public string Text => _item.DisplayText;

    public double Priority => _item.Priority;

    public object Content => _cachedContent ??= BuildContentControl();

    public object Description => _cachedDescription ??= BuildDescriptionControl();

    private Control BuildContentControl()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Height = 22,
            Margin = new Thickness(2, 1)
        };

        var (iconKind, iconColor) = GetIconConfig(_item.Kind);

        var icon = new MaterialIcon
        {
            Kind = iconKind,
            Width = 14,
            Height = 14,
            Foreground = new SolidColorBrush(Color.Parse(iconColor)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var textBlock = new TextBlock
        {
            Text = _item.DisplayText,
            FontFamily = new FontFamily("JetBrains Mono, Menlo, Monaco, Consolas, monospace"),
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(Color.Parse("#E6EDF3")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        grid.Children.Add(textBlock);

        if (!string.IsNullOrEmpty(_item.ReturnType))
        {
            var returnTypeBlock = new TextBlock
            {
                Text = _item.ReturnType,
                FontFamily = new FontFamily("JetBrains Mono, Menlo, Monaco, Consolas, monospace"),
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.Parse("#8B949E")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 4, 0)
            };
            Grid.SetColumn(returnTypeBlock, 2);
            grid.Children.Add(returnTypeBlock);
        }

        return grid;
    }

    private Control BuildDescriptionControl()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            MaxWidth = 440,
            Margin = new Thickness(6, 4)
        };

        if (!string.IsNullOrEmpty(_item.Signature))
        {
            var sigBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#161B22")),
                BorderBrush = new SolidColorBrush(Color.Parse("#30363D")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5)
            };

            var sigBlock = new TextBlock
            {
                Text = _item.Signature,
                FontFamily = new FontFamily("JetBrains Mono, Menlo, Monaco, Consolas, monospace"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.Parse("#58A6FF")),
                TextWrapping = TextWrapping.Wrap
            };
            sigBorder.Child = sigBlock;
            panel.Children.Add(sigBorder);
        }

        if (!string.IsNullOrEmpty(_item.Documentation))
        {
            var docBlock = new TextBlock
            {
                Text = _item.Documentation,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.Parse("#C9D1D9")),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            panel.Children.Add(docBlock);
        }

        return panel;
    }

    private static (MaterialIconKind Kind, string ColorHex) GetIconConfig(CompletionItemKind kind) => kind switch
    {
        CompletionItemKind.Method => (MaterialIconKind.Function, "#BC8CFF"), // Purple
        CompletionItemKind.ExtensionMethod => (MaterialIconKind.LightningBolt, "#D2A8FF"), // Light Purple
        CompletionItemKind.Property => (MaterialIconKind.TagOutline, "#7EE787"), // Green
        CompletionItemKind.Field => (MaterialIconKind.CircleSmall, "#79C0FF"), // Blue
        CompletionItemKind.Class => (MaterialIconKind.CodeBraces, "#FFA657"), // Amber
        CompletionItemKind.Record => (MaterialIconKind.CodeBraces, "#FF7B72"), // Coral
        CompletionItemKind.Struct => (MaterialIconKind.AlphaSBoxOutline, "#FFA657"),
        CompletionItemKind.Interface => (MaterialIconKind.AlphaIBoxOutline, "#56D364"), // Emerald
        CompletionItemKind.Enum => (MaterialIconKind.FormatListBulleted, "#F2CC60"),
        CompletionItemKind.Keyword => (MaterialIconKind.KeyOutline, "#8B949E"), // Gray
        CompletionItemKind.Snippet => (MaterialIconKind.LightningBoltOutline, "#F0883E"), // Orange
        CompletionItemKind.Variable => (MaterialIconKind.Variable, "#79C0FF"),
        CompletionItemKind.Namespace => (MaterialIconKind.PackageVariantClosed, "#A5D6FF"),
        _ => (MaterialIconKind.CodeBraces, "#8B949E")
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var textToInsert = _item.InsertionText;
        int caretDelta = _item.CaretOffsetDelta;

        if (textToInsert.Contains("$0"))
        {
            int markerIdx = textToInsert.IndexOf("$0", StringComparison.Ordinal);
            textToInsert = textToInsert.Replace("$0", string.Empty);
            textArea.Document.Replace(completionSegment, textToInsert);
            textArea.Caret.Offset = completionSegment.Offset + markerIdx;
        }
        else
        {
            textArea.Document.Replace(completionSegment, textToInsert);
            if (caretDelta != 0)
            {
                textArea.Caret.Offset = Math.Max(0, completionSegment.Offset + textToInsert.Length + caretDelta);
            }
        }
    }
}
