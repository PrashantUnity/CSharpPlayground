using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class DumpTableView : UserControl
{
    private Grid? _tableGrid;
    private ScrollViewer? _scrollViewer;

    public DumpTableView()
    {
        InitializeComponent();
        _tableGrid = this.FindControl<Grid>("TableGrid");
        _scrollViewer = this.FindControl<ScrollViewer>("TableScrollViewer");

        if (_scrollViewer != null)
        {
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && _tableGrid != null)
        {
            _tableGrid.MinWidth = e.NewSize.Width;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_scrollViewer == null)
        {
            _scrollViewer = this.FindControl<ScrollViewer>("TableScrollViewer");
            if (_scrollViewer != null)
            {
                _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
                if (_scrollViewer.Bounds.Width > 0 && _tableGrid != null)
                {
                    _tableGrid.MinWidth = _scrollViewer.Bounds.Width;
                }
            }
        }

        if (DataContext is DumpTableResult table && (_tableGrid == null || _tableGrid.Children.Count == 0))
        {
            BuildTable(table);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DumpTableResult table)
        {
            BuildTable(table);
        }
    }

    public void BuildTable(DumpTableResult table)
    {
        _tableGrid ??= this.FindControl<Grid>("TableGrid");
        if (_tableGrid == null) return;

        _tableGrid.Children.Clear();
        _tableGrid.ColumnDefinitions.Clear();
        _tableGrid.RowDefinitions.Clear();

        int colCount = table.Columns.Count;
        if (colCount == 0) return;

        // Determine column layout mode:
        // Key/Value or Property/Value tables (e.g. anonymous object dump)
        bool isKeyValue = colCount == 2 &&
            (table.Columns[0].Header.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
             table.Columns[0].Header.Equals("Key", StringComparison.OrdinalIgnoreCase));

        if (isKeyValue)
        {
            // Property / Key column takes Auto (sized to header/content, e.g. 160px)
            // Value column takes 1* (expands to fill the entire remaining canvas width!)
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
            // All columns share equal * width, filling 100% of the canvas
            for (int i = 0; i < colCount; i++)
            {
                _tableGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star)
                {
                    MinWidth = 110
                });
            }
        }

        // Header Row definition
        _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var headerBrush = new SolidColorBrush(Color.Parse("#1A2232"));
        var borderBrush = new SolidColorBrush(Color.Parse("#2B3648"));
        var onSurfaceBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));
        var onSurfaceMutedBrush = new SolidColorBrush(Color.Parse("#94A3B8"));

        // Build Header Cells
        for (int c = 0; c < colCount; c++)
        {
            var col = table.Columns[c];
            var headerBorder = new Border
            {
                Background = headerBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, (c == colCount - 1 ? 0 : 1), 1),
                Padding = new Thickness(12, 6)
            };

            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            var headerText = new TextBlock
            {
                Text = col.Header,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = onSurfaceBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = col.IsNumeric ? TextAlignment.Right : TextAlignment.Left
            };
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            var icon = new MaterialIcon
            {
                Kind = MaterialIconKind.Menu,
                Width = 13,
                Height = 13,
                Foreground = onSurfaceMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(icon, 1);
            headerGrid.Children.Add(icon);

            headerBorder.Child = headerGrid;

            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c);
            _tableGrid.Children.Add(headerBorder);
        }

        // Data Rows definition and cells
        var rowBgEven = new SolidColorBrush(Color.Parse("#0F141E"));
        var rowBgOdd = new SolidColorBrush(Color.Parse("#141B28"));
        var monospaceFont = new FontFamily("Consolas, Menlo, Monaco, Roboto Mono, JetBrains Mono, monospace");

        for (int r = 0; r < table.Rows.Count; r++)
        {
            _tableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var row = table.Rows[r];
            var rowBg = (r % 2 == 0) ? rowBgEven : rowBgOdd;
            bool isLastRow = (r == table.Rows.Count - 1);

            for (int c = 0; c < colCount; c++)
            {
                var cell = (c < row.Cells.Count) ? row.Cells[c] : null;
                var cellText = cell?.DisplayText ?? string.Empty;

                var cellBorder = new Border
                {
                    Background = rowBg,
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, (c == colCount - 1 ? 0 : 1), isLastRow ? 0 : 1),
                    Padding = new Thickness(12, 5.5)
                };

                var textBlock = new SelectableTextBlock
                {
                    Text = cellText,
                    FontSize = 12,
                    FontFamily = monospaceFont,
                    Foreground = onSurfaceBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = (cell?.Alignment ?? TextAlignment.Left)
                };

                cellBorder.Child = textBlock;

                Grid.SetRow(cellBorder, r + 1);
                Grid.SetColumn(cellBorder, c);
                _tableGrid.Children.Add(cellBorder);
            }
        }
    }
}
