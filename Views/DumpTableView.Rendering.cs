using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// The theme brushes and layout values (type sizes, weights, line widths, radii, the code font) a table paints with,
/// resolved once per theme or layout instead of once per cell.
/// </summary>
internal sealed record DumpTableBrushes(
    IBrush Header,
    IBrush HeaderHover,
    IBrush Border,
    IBrush OnSurface,
    IBrush OnSurfaceMuted,
    IBrush Primary,
    IBrush RowEven,
    IBrush RowOdd,
    IBrush RowHover,
    IBrush RowSelected,
    FontFamily Monospace,
    double TextSize,
    double SmallSize,
    double TinySize,
    FontWeight Emphasis,
    FontWeight Strong,
    double Line,
    double Accent,
    double SmallRadius,
    double MediumRadius)
{
    public static DumpTableBrushes Resolve(DumpTableView view) => new(
        view.ResolveBrush("M3SurfaceContainerHighBrush", "#252C36"),
        view.ResolveBrush("M3SurfaceContainerHighestBrush", "#2F3642"),
        view.ResolveBrush("M3OutlineVariantBrush", "#3D4450"),
        view.ResolveBrush("M3OnSurfaceBrush", "#E2E2E6"),
        view.ResolveBrush("M3OnSurfaceVariantBrush", "#9BA1AD"),
        view.ResolveBrush("M3PrimaryBrush", "#007ACC"),
        view.ResolveBrush("M3SurfaceContainerLowestBrush", "#0B0E11"),
        view.ResolveBrush("M3SurfaceContainerLowBrush", "#161A1F"),
        view.ResolveBrush("M3SurfaceContainerHighestBrush", "#252C36"),
        view.ResolveBrush("M3PrimaryContainerBrush", "#192B42"),
        view.ResolveToken("DsCodeFontFamily", new FontFamily("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace")),
        view.ResolveToken("DsFontSize300", 12.0),
        view.ResolveToken("DsFontSize200", 11.0),
        view.ResolveToken("DsFontSize075", 9.5),
        view.ResolveToken("DsWeightEmphasis", FontWeight.SemiBold),
        view.ResolveToken("DsWeightStrong", FontWeight.Bold),
        view.ResolveToken("DsBorderThin", new Thickness(1)).Left,
        view.ResolveToken("DsBorderAccentLeft", new Thickness(2, 0, 0, 0)).Left,
        view.ResolveToken("DsRadiusSM", new CornerRadius(4)).TopLeft,
        view.ResolveToken("DsRadiusMD", new CornerRadius(6)).TopLeft);
}

/// <summary>
/// The column widths a table's rows share. A virtualized row cannot measure the other rows to agree on widths, so the
/// widths come from the header and a sample of rows, and stretch to fill the width available (like the star columns of
/// the old grid). Text wider than its column is trimmed with an ellipsis; "Copy Cell Value" gives all of it.
/// </summary>
internal sealed class DumpTableLayout
{
    /// <summary>How many rows the widths are worked out from (the rest are trimmed if wider).</summary>
    public const int SampleRows = 200;

    public const double CellPaddingX = 10;
    public const double CellPaddingY = 5.5;
    private const double MonoCharWidth = 7.2;
    private const double HeaderCharWidth = 7.4;
    private const double SortIconWidth = 19;
    private const double MinColumn = 90;
    private const double MaxColumn = 460;
    private const double NestedButtonWidth = 96;

    public static readonly DumpTableLayout Empty = new(36, Array.Empty<double>());

    private DumpTableLayout(double indexWidth, double[] widths)
    {
        IndexWidth = indexWidth;
        Widths = widths;
        Offsets = new double[widths.Length];
        double x = indexWidth;
        for (int i = 0; i < widths.Length; i++)
        {
            Offsets[i] = x;
            x += widths[i];
        }

        TotalWidth = x;
    }

    public double IndexWidth { get; }
    public double[] Widths { get; }

    /// <summary>Where each data column starts (the index column is at 0).</summary>
    public double[] Offsets { get; }

    public double TotalWidth { get; }

    public bool SameWidths(DumpTableLayout other) =>
        Math.Abs(IndexWidth - other.IndexWidth) < 0.5 && Widths.Length == other.Widths.Length &&
        Widths.Zip(other.Widths).All(p => Math.Abs(p.First - p.Second) < 0.5);

    /// <summary>The column under <paramref name="x"/> (-1 for the index column).</summary>
    public int ColumnAt(double x)
    {
        for (int i = Widths.Length - 1; i >= 0; i--)
        {
            if (x >= Offsets[i]) return i;
        }

        return -1;
    }

    public static DumpTableLayout Compute(DumpTableResult table, double viewportWidth)
    {
        int columns = table.Columns.Count;
        int digits = Math.Max(2, (Math.Max(1, table.FilteredRows.Count)).ToString().Length);
        double indexWidth = Math.Max(36, digits * MonoCharWidth + 14);
        if (columns == 0) return new DumpTableLayout(indexWidth, Array.Empty<double>());

        var widths = new double[columns];
        for (int c = 0; c < columns; c++)
        {
            widths[c] = table.Columns[c].Header.Length * HeaderCharWidth + SortIconWidth + 2 * CellPaddingX;
        }

        var rows = table.FilteredRows;
        int sample = Math.Min(rows.Count, SampleRows);
        for (int r = 0; r < sample; r++)
        {
            var cells = rows[r].Cells;
            for (int c = 0; c < columns && c < cells.Count; c++)
            {
                var cell = cells[c];
                double w = Math.Min(cell.DisplayText?.Length ?? 0, 64) * MonoCharWidth + 2 * CellPaddingX;
                if (cell.HasNestedTable) w += NestedButtonWidth;
                if (w > widths[c]) widths[c] = w;
            }
        }

        bool isKeyValue = columns == 2 &&
            (table.Columns[0].Header.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
             table.Columns[0].Header.Equals("Key", StringComparison.OrdinalIgnoreCase));
        if (isKeyValue)
        {
            widths[0] = Math.Clamp(widths[0], 150, 360);
            widths[1] = Math.Clamp(widths[1], 200, MaxColumn * 2);
        }
        else
        {
            for (int c = 0; c < columns; c++) widths[c] = Math.Clamp(widths[c], MinColumn, MaxColumn);
        }

        // Fill the width available, as the old star columns did: the value column of a key/value table takes it all.
        // (A pixel short of the width, so rounding never shows a horizontal scrollbar for nothing.)
        double extra = Math.Floor(viewportWidth) - 1 - indexWidth - widths.Sum();
        if (extra > 1)
        {
            if (isKeyValue)
            {
                widths[1] += extra;
            }
            else
            {
                double sum = widths.Sum();
                for (int c = 0; c < columns; c++) widths[c] += extra * widths[c] / sum;
            }
        }

        return new DumpTableLayout(indexWidth, widths);
    }
}

public partial class DumpTableView
{
    private void BuildHeader(DumpTableResult table, DumpTableBrushes brushes)
    {
        var grid = _tableGrid!;
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        int colCount = table.Columns.Count;
        if (colCount == 0) return;

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Layout.IndexWidth)));
        for (int c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Layout.Widths[c])));
        }

        // Header Cell 0: Row number indicator (#)
        var numHeaderBorder = new Border
        {
            Background = brushes.Header,
            BorderBrush = brushes.Border,
            BorderThickness = new Thickness(0, 0, brushes.Line, brushes.Line),
            Padding = new Thickness(6, 6),
            Child = new TextBlock
            {
                Text = "#",
                FontSize = brushes.SmallSize,
                FontWeight = brushes.Emphasis,
                Foreground = brushes.OnSurfaceMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(numHeaderBorder, 0);
        grid.Children.Add(numHeaderBorder);

        // Header Data Cells (Sortable & Clickable)
        for (int c = 0; c < colCount; c++)
        {
            var col = table.Columns[c];
            int colIndex = c;
            bool isSortedCol = table.SortColumnIndex == c;

            var headerBorder = new Border
            {
                Background = isSortedCol ? brushes.HeaderHover : brushes.Header,
                BorderBrush = brushes.Border,
                BorderThickness = new Thickness(0, 0, (c == colCount - 1 ? 0 : brushes.Line), brushes.Line),
                Padding = new Thickness(DumpTableLayout.CellPaddingX, 6),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var headerText = new TextBlock
            {
                Text = col.Header,
                FontSize = brushes.TextSize,
                FontWeight = isSortedCol ? brushes.Strong : brushes.Emphasis,
                Foreground = isSortedCol ? brushes.Primary : brushes.OnSurface,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
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
                Foreground = isSortedCol ? brushes.Primary : brushes.OnSurfaceMuted,
                Opacity = isSortedCol ? 1.0 : 0.45,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(sortIcon, 1);
            headerGrid.Children.Add(sortIcon);
            headerBorder.Child = headerGrid;

            ToolTip.SetTip(headerBorder, isSortedCol
                ? (table.IsSortDescending ? $"Sorted descending by {col.Header}. Click to clear sort." : $"Sorted ascending by {col.Header}. Click to sort descending.")
                : $"Click to sort by {col.Header}");

            headerBorder.PointerPressed += (s, e) => table.SortByColumn(colIndex);
            headerBorder.PointerEntered += (s, e) =>
            {
                headerBorder.Background = brushes.HeaderHover;
                sortIcon.Opacity = 1.0;
            };
            headerBorder.PointerExited += (s, e) =>
            {
                headerBorder.Background = isSortedCol ? brushes.HeaderHover : brushes.Header;
                sortIcon.Opacity = isSortedCol ? 1.0 : 0.45;
            };

            Grid.SetColumn(headerBorder, c + 1);
            grid.Children.Add(headerBorder);
        }
    }

    /// <summary>What a row shows in one cell: its text, or a nested table's toggle and its text.</summary>
    internal Control CreateCellContent(DumpTableCell? cell, DumpTableBrushes brushes)
    {
        var cellText = cell?.DisplayText ?? string.Empty;
        var textBlock = new SelectableTextBlock
        {
            Text = cellText,
            FontSize = brushes.TextSize,
            FontFamily = brushes.Monospace,
            Foreground = cell?.IsNull == true ? brushes.OnSurfaceMuted : brushes.OnSurface,
            FontStyle = cell?.IsNull == true ? FontStyle.Italic : FontStyle.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = cell?.Alignment ?? TextAlignment.Left,
            // Right-click reaches the row's cell menu (which has Copy Cell Value) instead of a bare Copy flyout.
            ContextFlyout = null,
        };

        return CreateCellControl(cell, textBlock, brushes);
    }

    /// <summary>The right-click menu of one cell, built when it is opened (not for every cell up front).</summary>
    internal ContextMenu CreateCellMenu(DumpTableRow row, int columnIndex)
    {
        var table = _currentTable!;
        var cell = columnIndex >= 0 && columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
        var cellText = cell?.DisplayText ?? string.Empty;
        var menu = new ContextMenu();
        if (cell != null)
        {
            AppendNestedTableMenuItems(menu, cell);
        }

        var copyCell = new MenuItem { Header = "Copy Cell Value", IsEnabled = cell != null };
        copyCell.Click += async (s, e) => await SetClipboardTextAsync(cellText);
        menu.Items.Add(copyCell);

        var copyRowTsv = new MenuItem { Header = "Copy Row (TSV)" };
        copyRowTsv.Click += async (s, e) => await SetClipboardTextAsync(string.Join("\t", row.Cells.Select(x => x.DisplayText)));
        menu.Items.Add(copyRowTsv);

        var copyRowJson = new MenuItem { Header = "Copy Row (JSON)" };
        copyRowJson.Click += async (s, e) =>
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < table.Columns.Count; i++)
            {
                dict[table.Columns[i].Header] = i < row.Cells.Count ? (row.Cells[i].RawValue ?? row.Cells[i].DisplayText) : null;
            }

            await SetClipboardTextAsync(System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
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

        return menu;
    }
}
