using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class DumpTableView
{
    private Control CreateCellControl(DumpTableCell? cell, SelectableTextBlock textBlock, DumpTableBrushes brushes)
    {
        if (cell?.HasNestedTable != true || cell.NestedTable == null)
        {
            return textBlock;
        }

        var primaryBrush = brushes.Primary;
        var onSurfaceBrush = brushes.OnSurface;
        var onSurfaceMutedBrush = brushes.OnSurfaceMuted;
        var borderBrush = brushes.Border;

        var cellPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        var expandBtn = new Button
        {
            Background = cell.IsNestedExpanded
                ? primaryBrush
                : ResolveBrush("M3SurfaceContainerHighestBrush", "#2D3748"),
            Foreground = cell.IsNestedExpanded ? Brushes.White : onSurfaceBrush,
            BorderBrush = cell.IsNestedExpanded ? primaryBrush : borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center
        };

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        btnStack.Children.Add(new MaterialIcon
        {
            Kind = MaterialIconKind.TableLarge,
            Width = 12,
            Height = 12,
            Foreground = cell.IsNestedExpanded ? Brushes.White : primaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        int nestedCount = cell.NestedTable.Rows.Count;
        string countText = nestedCount > 0 ? $"{nestedCount} rows" : "Table";
        btnStack.Children.Add(new TextBlock
        {
            Text = countText,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        btnStack.Children.Add(new TextBlock
        {
            Text = cell.IsNestedExpanded ? "▲" : "▼",
            FontSize = 9,
            Foreground = cell.IsNestedExpanded ? Brushes.White : onSurfaceMutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        expandBtn.Content = btnStack;
        ToolTip.SetTip(expandBtn, "Click to toggle inline nested table");

        expandBtn.Click += (s, e) =>
        {
            cell.IsNestedExpanded = !cell.IsNestedExpanded;
            NestedToggled();
        };

        cellPanel.Children.Add(expandBtn);
        cellPanel.Children.Add(textBlock);
        return cellPanel;
    }

    private void AppendNestedTableMenuItems(ContextMenu menu, DumpTableCell cell)
    {
        if (!cell.HasNestedTable || cell.NestedTable == null) return;

        var toggleNestedItem = new MenuItem
        {
            Header = cell.IsNestedExpanded ? "Collapse Nested Table" : "Expand Nested Table Inline"
        };
        toggleNestedItem.Click += (s, e) =>
        {
            cell.IsNestedExpanded = !cell.IsNestedExpanded;
            NestedToggled();
        };
        menu.Items.Add(toggleNestedItem);

        var copyNestedMd = new MenuItem { Header = "Copy Nested Table as Markdown" };
        copyNestedMd.Click += async (s, e) => await SetClipboardTextAsync(cell.NestedTable.ToMarkdown());
        menu.Items.Add(copyNestedMd);

        var copyNestedTsv = new MenuItem { Header = "Copy Nested Table (TSV)" };
        copyNestedTsv.Click += async (s, e) => await SetClipboardTextAsync(cell.NestedTable.ToTsv());
        menu.Items.Add(copyNestedTsv);

        var copyNestedJson = new MenuItem { Header = "Copy Nested Table (JSON)" };
        copyNestedJson.Click += async (s, e) => await SetClipboardTextAsync(cell.NestedTable.ToJson());
        menu.Items.Add(copyNestedJson);

        menu.Items.Add(new Separator());
    }

    internal Border CreateNestedTableCard(string colHeader, DumpTableCell cell, DumpTableBrushes brushes)
    {
        var primaryBrush = brushes.Primary;
        var onSurfaceBrush = brushes.OnSurface;
        var onSurfaceMutedBrush = brushes.OnSurfaceMuted;
        var nestedCard = new Border
        {
            Background = ResolveBrush("M3SurfaceContainerLowBrush", "#14181F"),
            BorderBrush = primaryBrush,
            BorderThickness = new Thickness(2, 0, 0, 1),
            Margin = new Thickness(36, 4, 12, 10),
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            Padding = new Thickness(10, 8)
        };

        var cardStack = new StackPanel { Spacing = 6 };

        // Sub-header bar
        var subHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var subTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        subTitle.Children.Add(new MaterialIcon
        {
            Kind = MaterialIconKind.TableLarge,
            Width = 14,
            Height = 14,
            Foreground = primaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        subTitle.Children.Add(new TextBlock
        {
            Text = $"{colHeader}: {cell.NestedTable!.FullHeaderTitle}",
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = onSurfaceBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        subTitle.Children.Add(new TextBlock
        {
            Text = cell.NestedTable.SummaryText,
            FontSize = 11,
            Foreground = onSurfaceMutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(subTitle, 0);
        subHeader.Children.Add(subTitle);

        var closeBtn = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        closeBtn.Content = new MaterialIcon
        {
            Kind = MaterialIconKind.Close,
            Width = 12,
            Height = 12,
            Foreground = onSurfaceMutedBrush
        };
        closeBtn.Click += (s, e) =>
        {
            cell.IsNestedExpanded = false;
            NestedToggled();
        };
        Grid.SetColumn(closeBtn, 2);
        subHeader.Children.Add(closeBtn);

        cardStack.Children.Add(subHeader);

        var childTableView = new DumpTableView
        {
            DataContext = cell.NestedTable,
            Margin = new Thickness(0, 2, 0, 0)
        };
        childTableView.BuildTable(cell.NestedTable);
        cardStack.Children.Add(childTableView);

        nestedCard.Child = cardStack;
        return nestedCard;
    }
}
