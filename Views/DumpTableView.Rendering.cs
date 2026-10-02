using System;
using System.Collections.Generic;
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
    public void BuildTable(DumpTableResult table)
    {
        _tableGrid ??= this.FindControl<Grid>("TableGrid");
        if (_tableGrid == null) return;

        _tableGrid.Children.Clear();
        _tableGrid.ColumnDefinitions.Clear();
        _tableGrid.RowDefinitions.Clear();

        int colCount = table.Columns.Count;
        if (colCount == 0) return;

        // Column 0: Row Index (#)
        _tableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)
        {
            MinWidth = 36,
            MaxWidth = 60
        });

        // Determine layout for data columns
        bool isKeyValue = colCount == 2 &&
            (table.Columns[0].Header.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
             table.Columns[0].Header.Equals("Key", StringComparison.OrdinalIgnoreCase));

        if (isKeyValue)
        {
            _tableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)
            {
                MinWidth = 150,
                MaxWidth = 360
            });
            _tableGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star)
            {
                MinWidth = 200
            });
        }
        else
        {
            for (int i = 0; i < colCount; i++)
            {
                _tableGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star)
                {
                    MinWidth = 100
                });
            }
        }

        // Brushes
        var headerBrush = ResolveBrush("M3SurfaceContainerHighBrush", "#252C36");
        var headerHoverBrush = ResolveBrush("M3SurfaceContainerHighestBrush", "#2F3642");
        var borderBrush = ResolveBrush("M3OutlineVariantBrush", "#3D4450");
        var onSurfaceBrush = ResolveBrush("M3OnSurfaceBrush", "#E2E2E6");
        var onSurfaceMutedBrush = ResolveBrush("M3OnSurfaceVariantBrush", "#9BA1AD");
        var primaryBrush = ResolveBrush("M3PrimaryBrush", "#007ACC");
        var rowBgEven = ResolveBrush("M3SurfaceContainerLowestBrush", "#0B0E11");
        var rowBgOdd = ResolveBrush("M3SurfaceContainerLowBrush", "#161A1F");
        var rowHoverBrush = ResolveBrush("M3SurfaceContainerHighestBrush", "#252C36");
        var rowSelectedBrush = ResolveBrush("M3PrimaryContainerBrush", "#192B42");
        var monospaceFont = new FontFamily("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace");

        // Row 0: Header Row
        _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Header Cell 0: Row number indicator (#)
        var numHeaderBorder = new Border
        {
            Background = headerBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(6, 6)
        };
        numHeaderBorder.Child = new TextBlock
        {
            Text = "#",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = onSurfaceMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(numHeaderBorder, 0);
        Grid.SetColumn(numHeaderBorder, 0);
        _tableGrid.Children.Add(numHeaderBorder);

        // Header Data Cells (Sortable & Clickable)
        for (int c = 0; c < colCount; c++)
        {
            var col = table.Columns[c];
            int colIndex = c;
            bool isSortedCol = table.SortColumnIndex == c;

            var headerBorder = new Border
            {
                Background = isSortedCol ? headerHoverBrush : headerBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, (c == colCount - 1 ? 0 : 1), 1),
                Padding = new Thickness(10, 6),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            var headerText = new TextBlock
            {
                Text = col.Header,
                FontSize = 12,
                FontWeight = isSortedCol ? FontWeight.Bold : FontWeight.SemiBold,
                Foreground = isSortedCol ? primaryBrush : onSurfaceBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = col.IsNumeric ? TextAlignment.Right : TextAlignment.Left
            };
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            var sortIcon = new MaterialIcon
            {
                Kind = isSortedCol
                    ? (table.IsSortDescending ? MaterialIconKind.ArrowDown : MaterialIconKind.ArrowUp)
                    : MaterialIconKind.SwapVertical,
                Width = 13,
                Height = 13,
                Foreground = isSortedCol ? primaryBrush : onSurfaceMutedBrush,
                Opacity = isSortedCol ? 1.0 : 0.45,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(sortIcon, 1);
            headerGrid.Children.Add(sortIcon);

            headerBorder.Child = headerGrid;

            string sortTip = isSortedCol
                ? (table.IsSortDescending ? $"Sorted descending by {col.Header}. Click to clear sort." : $"Sorted ascending by {col.Header}. Click to sort descending.")
                : $"Click to sort by {col.Header}";
            ToolTip.SetTip(headerBorder, sortTip);

            headerBorder.PointerPressed += (s, e) => table.SortByColumn(colIndex);
            headerBorder.PointerEntered += (s, e) =>
            {
                headerBorder.Background = headerHoverBrush;
                sortIcon.Opacity = 1.0;
            };
            headerBorder.PointerExited += (s, e) =>
            {
                headerBorder.Background = isSortedCol ? headerHoverBrush : headerBrush;
                sortIcon.Opacity = isSortedCol ? 1.0 : 0.45;
            };

            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c + 1);
            _tableGrid.Children.Add(headerBorder);
        }

        // Data Rows
        var displayRows = table.FilteredRows.Count > 0 || table.IsFiltered
            ? (IReadOnlyList<DumpTableRow>)table.FilteredRows
            : (IReadOnlyList<DumpTableRow>)table.Rows;

        if (displayRows.Count == 0)
        {
            _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var emptyBorder = new Border
            {
                Background = rowBgEven,
                Padding = new Thickness(16, 14),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            emptyBorder.Child = new TextBlock
            {
                Text = table.IsFiltered ? "No rows matching filter." : "Table contains no rows.",
                FontSize = 12,
                FontStyle = FontStyle.Italic,
                Foreground = onSurfaceMutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(emptyBorder, 1);
            Grid.SetColumn(emptyBorder, 0);
            Grid.SetColumnSpan(emptyBorder, colCount + 1);
            _tableGrid.Children.Add(emptyBorder);
            return;
        }

        for (int r = 0; r < displayRows.Count; r++)
        {
            _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            int rowGridIndex = _tableGrid.RowDefinitions.Count - 1;

            var row = displayRows[r];
            int rowIndex = r;
            bool isSelected = table.SelectedRowIndex == r;
            var rowBg = isSelected ? rowSelectedBrush : (r % 2 == 0 ? rowBgEven : rowBgOdd);
            bool isLastRow = (r == displayRows.Count - 1);
            var rowBorders = new List<Border>(colCount + 1);

            // Column 0: Row Index
            var indexBorder = new Border
            {
                Background = rowBg,
                BorderBrush = isSelected ? primaryBrush : borderBrush,
                BorderThickness = new Thickness(isSelected ? 3 : 0, 0, 1, isLastRow ? 0 : 1),
                Padding = new Thickness(6, 5.5)
            };
            indexBorder.Child = new TextBlock
            {
                Text = (r + 1).ToString(),
                FontSize = 11,
                FontFamily = monospaceFont,
                Foreground = isSelected ? primaryBrush : onSurfaceMutedBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(indexBorder, rowGridIndex);
            Grid.SetColumn(indexBorder, 0);
            _tableGrid.Children.Add(indexBorder);
            rowBorders.Add(indexBorder);

            // Data Cells
            for (int c = 0; c < colCount; c++)
            {
                var cell = (c < row.Cells.Count) ? row.Cells[c] : null;
                var cellText = cell?.DisplayText ?? string.Empty;

                var cellBorder = new Border
                {
                    Background = rowBg,
                    BorderBrush = isSelected ? primaryBrush : borderBrush,
                    BorderThickness = new Thickness(0, 0, (c == colCount - 1 ? 0 : 1), isLastRow ? 0 : 1),
                    Padding = new Thickness(10, 5.5)
                };

                var textBlock = new SelectableTextBlock
                {
                    Text = cellText,
                    FontSize = 12,
                    FontFamily = monospaceFont,
                    Foreground = cell?.IsNull == true ? onSurfaceMutedBrush : onSurfaceBrush,
                    FontStyle = cell?.IsNull == true ? FontStyle.Italic : FontStyle.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = (cell?.Alignment ?? TextAlignment.Left)
                };

                // Context Menu on Cells
                var menu = new ContextMenu();
                if (cell != null)
                {
                    AppendNestedTableMenuItems(menu, cell, table);
                }

                var copyCell = new MenuItem { Header = "Copy Cell Value" };
                copyCell.Click += async (s, e) => await SetClipboardTextAsync(cellText);
                menu.Items.Add(copyCell);

                var copyRowTsv = new MenuItem { Header = "Copy Row (TSV)" };
                copyRowTsv.Click += async (s, e) =>
                {
                    var line = string.Join("\t", row.Cells.Select(x => x.DisplayText));
                    await SetClipboardTextAsync(line);
                };
                menu.Items.Add(copyRowTsv);

                var copyRowJson = new MenuItem { Header = "Copy Row (JSON)" };
                copyRowJson.Click += async (s, e) =>
                {
                    var dict = new Dictionary<string, object?>();
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        var h = table.Columns[i].Header;
                        var val = (i < row.Cells.Count) ? (row.Cells[i].RawValue ?? row.Cells[i].DisplayText) : null;
                        dict[h] = val;
                    }
                    var json = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await SetClipboardTextAsync(json);
                };
                menu.Items.Add(copyRowJson);

                if (!string.IsNullOrWhiteSpace(cellText))
                {
                    menu.Items.Add(new Separator());
                    string preview = cellText.Length > 20 ? cellText.Substring(0, 18) + "…" : cellText;
                    var filterItem = new MenuItem { Header = $"Filter by \"{preview}\"" };
                    filterItem.Click += (s, e) => table.FilterText = cellText;
                    menu.Items.Add(filterItem);
                }

                cellBorder.ContextMenu = menu;
                cellBorder.Child = CreateCellControl(cell, cellText, table, textBlock, primaryBrush, onSurfaceBrush, onSurfaceMutedBrush, borderBrush);

                Grid.SetRow(cellBorder, rowGridIndex);
                Grid.SetColumn(cellBorder, c + 1);
                _tableGrid.Children.Add(cellBorder);
                rowBorders.Add(cellBorder);
            }

            // Hover and Click Selection for each row
            foreach (var b in rowBorders)
            {
                b.PointerEntered += (s, e) =>
                {
                    if (table.SelectedRowIndex != rowIndex)
                    {
                        foreach (var rb in rowBorders) rb.Background = rowHoverBrush;
                    }
                };
                b.PointerExited += (s, e) =>
                {
                    if (table.SelectedRowIndex != rowIndex)
                    {
                        var origBg = (rowIndex % 2 == 0) ? rowBgEven : rowBgOdd;
                        foreach (var rb in rowBorders) rb.Background = origBg;
                    }
                };
                b.PointerPressed += (s, e) =>
                {
                    if (e.GetCurrentPoint(b).Properties.IsLeftButtonPressed)
                    {
                        table.SelectedRowIndex = (table.SelectedRowIndex == rowIndex) ? -1 : rowIndex;
                        BuildTable(table);
                    }
                };
            }

            // Inline Nested Table Expansion Card (when any cell in this row is expanded)
            for (int c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                if (cell.IsNestedExpanded && cell.NestedTable != null)
                {
                    _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    int subGridRow = _tableGrid.RowDefinitions.Count - 1;
                    var colHeader = (c < table.Columns.Count) ? table.Columns[c].Header : "Nested";
                    var nestedCard = CreateNestedTableCard(colHeader, cell, table, primaryBrush, onSurfaceBrush, onSurfaceMutedBrush);

                    Grid.SetRow(nestedCard, subGridRow);
                    Grid.SetColumn(nestedCard, 0);
                    Grid.SetColumnSpan(nestedCard, colCount + 1);
                    _tableGrid.Children.Add(nestedCard);
                }
            }
        }
    }
}
