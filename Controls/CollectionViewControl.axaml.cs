using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class CollectionViewControl : UserControl
{
    private DebugVariableItem? _currentItem;
    private readonly List<string> _headers = new();
    private readonly List<List<string>> _allRows = new();

    public event Action? CloseRequested;

    internal IReadOnlyList<string> Headers => _headers;
    internal IReadOnlyList<List<string>> AllRows => _allRows;

    public CollectionViewControl()
    {
        InitializeComponent();

        CloseButton.Click += (s, e) => CloseRequested?.Invoke();
        CopyTableButton.Click += OnCopyTableClicked;
        FilterTextBox.TextChanged += (s, e) => RenderTableRows();
    }

    public void SetData(DebugVariableItem item)
    {
        _currentItem = item;
        TitleText.Text = $"Collection View: {item.Name}";

        ExtractHeadersAndRows(item);
        RowCountText.Text = $"{_allRows.Count} rows";
        FilterTextBox.Text = string.Empty;

        RenderTableRows();
    }

    private void ExtractHeadersAndRows(DebugVariableItem item)
    {
        _headers.Clear();
        _allRows.Clear();

        var (headers, rows) = ExtractData(item);
        _headers.AddRange(headers);
        _allRows.AddRange(rows);
    }

    public static (List<string> Headers, List<List<string>> Rows) ExtractData(DebugVariableItem item) =>
        CollectionViewDataExtractor.Extract(item);

    private Grid CreateRowGrid()
    {
        var grid = new Grid();
        for (int i = 0; i < _headers.Count; i++)
        {
            grid.ColumnDefinitions.Add(i == 0 ? new ColumnDefinition(GridLength.Auto) : new ColumnDefinition(1, GridUnitType.Star));
        }
        return grid;
    }

    private void RenderTableRows()
    {
        TableContentPanel.Children.Clear();
        string filter = FilterTextBox.Text?.Trim() ?? string.Empty;
        var filteredRows = string.IsNullOrWhiteSpace(filter)
            ? _allRows
            : _allRows.Where(r => r.Any(cell => cell.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();

        RowCountText.Text = $"{filteredRows.Count} rows";
        if (_headers.Count == 0) return;

        var headerFg = ResolveBrush("CodeTypeBrush", "#4EC9B0");
        var headerBg = ResolveBrush("M3SurfaceContainerHighBrush", "#28FFFFFF");
        var indexFg = ResolveBrush("M3OnSurfaceVariantBrush", "#8B949E");
        var cellFg = ResolveBrush("M3OnSurfaceBrush", "#E6EDF3");
        var zebraBg = ResolveBrush("DsSurfaceHoverBrush", "#10FFFFFF");
        var monoFont = new FontFamily("JetBrains Mono, Menlo, monospace");

        // Render Header Row
        var headerGrid = CreateRowGrid();
        for (int i = 0; i < _headers.Count; i++)
        {
            var headerBlock = new TextBlock
            {
                Text = _headers[i],
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = headerFg,
                FontFamily = monoFont,
                Margin = new Thickness(8, 4),
                MinWidth = i == 0 ? 30 : 60
            };
            Grid.SetColumn(headerBlock, i);
            headerGrid.Children.Add(headerBlock);
        }
        TableContentPanel.Children.Add(new Border
        {
            Background = headerBg,
            CornerRadius = new CornerRadius(4),
            Child = headerGrid,
            Margin = new Thickness(0, 0, 0, 3)
        });

        // Render Data Rows
        int rowIndex = 0;
        foreach (var row in filteredRows)
        {
            var rowGrid = CreateRowGrid();
            for (int i = 0; i < row.Count && i < _headers.Count; i++)
            {
                var cellBlock = new TextBlock
                {
                    Text = row[i],
                    FontSize = 11,
                    FontFamily = monoFont,
                    Foreground = i == 0 ? indexFg : cellFg,
                    Margin = new Thickness(8, 3),
                    MinWidth = i == 0 ? 30 : 60,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(cellBlock, i);
                rowGrid.Children.Add(cellBlock);
            }
            TableContentPanel.Children.Add(new Border
            {
                Background = rowIndex % 2 == 1 ? zebraBg : Brushes.Transparent,
                CornerRadius = new CornerRadius(3),
                Child = rowGrid,
                Margin = new Thickness(0, 1)
            });
            rowIndex++;
        }
    }

    private IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (this.TryFindResource(resourceKey, out var res) && res is IBrush b)
        {
            return b;
        }
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var appRes) && appRes is IBrush appB)
        {
            return appB;
        }
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private async void OnCopyTableClicked(object? sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", _headers));
        foreach (var row in _allRows) sb.AppendLine(string.Join("\t", row));

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(sb.ToString());
    }
}
