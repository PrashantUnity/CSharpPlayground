using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// The rows of a <see cref="DumpTableView"/>: a virtualized list over the table's shown rows. Only the rows on screen
/// exist as controls (<see cref="DumpTableRowControl"/>), and scrolling re-uses them for the next rows.
/// </summary>
public sealed class DumpTableRowsList : ItemsControl
{
    public DumpTableRowsList()
    {
        // A local value: the theme's ItemsControl style sets a plain StackPanel, which would build every row.
        ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());

        // The list scrolls its own rows; the table around it scrolls sideways.
        Template = new FuncControlTemplate<DumpTableRowsList>((_, scope) => new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // The presenter builds the panel; a template must hand it the virtualizing one or it makes a StackPanel.
            Content = new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            }.RegisterInNameScope(scope),
        });
    }

    internal DumpTableView? Owner { get; set; }

    /// <summary>The row controls that exist now (the visible rows and a few around them).</summary>
    public IReadOnlyList<DumpTableRowControl> RealizedRows => GetRealizedContainers().OfType<DumpTableRowControl>().ToList();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = DefaultRecycleKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new DumpTableRowControl();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is DumpTableRowControl row && Owner != null) row.Show(Owner, item as DumpTableRow, index);
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        if (container is DumpTableRowControl row) row.SetIndex(newIndex);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is DumpTableRowControl row) row.Clear();
    }

    /// <summary>Repaints the realized rows (selection, widths); <paramref name="rebuild"/> also rebuilds their cells.</summary>
    internal void RefreshRealizedRows(bool remeasure, bool rebuild = false)
    {
        foreach (var row in GetRealizedContainers().OfType<DumpTableRowControl>())
        {
            if (rebuild) row.Rebuild();
            row.UpdateLook();
            if (remeasure) row.InvalidateMeasure();
        }
    }
}

/// <summary>
/// One table row: the row number and one text per column, laid out at the table's shared column widths, with grid lines
/// drawn rather than built from a border per cell. Its right-click menu is made when it is opened, for the cell under
/// the pointer. An opened nested table shows below the row.
/// </summary>
public sealed class DumpTableRowControl : Control
{
    private readonly TextBlock _indexText = new()
    {
        FontSize = 11,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly List<Control> _cells = new();
    private readonly List<Control> _nested = new();
    private DumpTableView? _owner;
    private DumpTableRow? _row;
    private int _index = -1;
    private bool _hover;
    private double _lineHeight;
    private IBrush? _background;

    public DumpTableRowControl()
    {
        AddChild(_indexText);
        // Tunnel and handled-too: a click that starts a text selection in a cell still selects the row.
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ContextRequestedEvent, OnContextRequested, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The shown position of the row (0-based), or -1 when the control is not showing a row.</summary>
    public int Index => _index;

    public DumpTableRow? Row => _row;

    internal void Show(DumpTableView owner, DumpTableRow? row, int index)
    {
        _owner = owner;
        _index = index;
        if (!ReferenceEquals(_row, row))
        {
            _row = row;
            Rebuild();
        }

        SetIndex(index);
    }

    internal void SetIndex(int index)
    {
        _index = index;
        _indexText.Text = (index + 1).ToString();
        UpdateLook();
    }

    internal void Clear()
    {
        _row = null;
        _index = -1;
        _hover = false;
        RemoveCells();
    }

    private void AddChild(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    private void RemoveChild(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    private void RemoveCells()
    {
        foreach (var c in _cells) RemoveChild(c);
        foreach (var n in _nested) RemoveChild(n);
        _cells.Clear();
        _nested.Clear();
    }

    /// <summary>The row's fill (zebra, hover or selected).</summary>
    public IBrush? Background => _background;

    /// <summary>Builds the cell controls again (a nested table was opened or closed, or the row changed).</summary>
    internal void Rebuild()
    {
        RemoveCells();
        var owner = _owner;
        var table = owner?.Table;
        var brushes = owner?.TableBrushes;
        if (owner == null || table == null || brushes == null || _row == null) return;

        _indexText.FontFamily = brushes.Monospace;
        for (int c = 0; c < table.Columns.Count; c++)
        {
            var cell = c < _row.Cells.Count ? _row.Cells[c] : null;
            var control = owner.CreateCellContent(cell, brushes);
            _cells.Add(control);
            AddChild(control);
        }

        for (int c = 0; c < _row.Cells.Count; c++)
        {
            var cell = _row.Cells[c];
            if (cell.IsNestedExpanded && cell.NestedTable != null)
            {
                var header = c < table.Columns.Count ? table.Columns[c].Header : "Nested";
                var card = owner.CreateNestedTableCard(header, cell, brushes);
                _nested.Add(card);
                AddChild(card);
            }
        }

        InvalidateMeasure();
    }

    internal void UpdateLook()
    {
        var brushes = _owner?.TableBrushes;
        var table = _owner?.Table;
        if (brushes == null || table == null) return;
        bool selected = _index >= 0 && table.SelectedRowIndex == _index;
        _background = selected ? brushes.RowSelected : _hover ? brushes.RowHover : (_index % 2 == 0 ? brushes.RowEven : brushes.RowOdd);
        _indexText.Foreground = selected ? brushes.Primary : brushes.OnSurfaceMuted;
        InvalidateVisual();
    }

    private bool IsSelected => _owner?.Table is { } table && _index >= 0 && table.SelectedRowIndex == _index;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        UpdateLook();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        UpdateLook();
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_owner == null || _index < 0) return;
        // Only the row's own line selects it; clicks inside an opened nested table belong to that table.
        if (e.GetPosition(this).Y > _lineHeight) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) _owner.ToggleSelection(_index);
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_owner == null || _row == null || e.Handled) return;
        bool hasPoint = e.TryGetPosition(this, out var point);
        if (hasPoint && point.Y > _lineHeight) return;
        var menu = _owner.CreateCellMenu(_row, _owner.Layout.ColumnAt(hasPoint ? point.X : 0));
        menu.Open(this);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = _owner?.Layout ?? DumpTableLayout.Empty;
        double padX = DumpTableLayout.CellPaddingX;
        double padY = DumpTableLayout.CellPaddingY;
        _indexText.Measure(new Size(Math.Max(0, layout.IndexWidth - 12), double.PositiveInfinity));
        double line = _indexText.DesiredSize.Height;
        for (int c = 0; c < _cells.Count; c++)
        {
            double width = c < layout.Widths.Length ? layout.Widths[c] : 0;
            _cells[c].Measure(new Size(Math.Max(0, width - 2 * padX), double.PositiveInfinity));
            line = Math.Max(line, _cells[c].DesiredSize.Height);
        }

        _lineHeight = line + 2 * padY;
        double height = _lineHeight;
        foreach (var card in _nested)
        {
            card.Measure(new Size(layout.TotalWidth, double.PositiveInfinity));
            height += card.DesiredSize.Height;
        }

        return new Size(layout.TotalWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = _owner?.Layout ?? DumpTableLayout.Empty;
        double padX = DumpTableLayout.CellPaddingX;
        double padY = DumpTableLayout.CellPaddingY;
        double inner = _lineHeight - 2 * padY;
        _indexText.Arrange(new Rect(6, padY, Math.Max(0, layout.IndexWidth - 12), inner));
        for (int c = 0; c < _cells.Count && c < layout.Widths.Length; c++)
        {
            _cells[c].Arrange(new Rect(layout.Offsets[c] + padX, padY, Math.Max(0, layout.Widths[c] - 2 * padX), inner));
        }

        double y = _lineHeight;
        foreach (var card in _nested)
        {
            card.Arrange(new Rect(0, y, finalSize.Width, card.DesiredSize.Height));
            y += card.DesiredSize.Height;
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var brushes = _owner?.TableBrushes;
        var layout = _owner?.Layout;
        if (_background != null) context.FillRectangle(_background, new Rect(Bounds.Size));
        if (brushes == null || layout == null) return;

        bool selected = IsSelected;
        var pen = new Pen(selected ? brushes.Primary : brushes.Border, 1);
        double bottom = Math.Round(_lineHeight) - 0.5;
        context.DrawLine(pen, new Point(0, bottom), new Point(layout.TotalWidth, bottom));
        context.DrawLine(pen, new Point(layout.IndexWidth - 0.5, 0), new Point(layout.IndexWidth - 0.5, _lineHeight));
        for (int c = 0; c < layout.Widths.Length - 1; c++)
        {
            double x = Math.Round(layout.Offsets[c] + layout.Widths[c]) - 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, _lineHeight));
        }

        if (selected)
        {
            context.FillRectangle(brushes.Primary, new Rect(0, 0, 3, _lineHeight));
        }
    }
}
