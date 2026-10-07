using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

/// <summary>
/// A <see cref="DumpTableResult"/> as a table: header, sortable columns, filter, row selection, nested tables. The rows
/// are a virtualized list (<see cref="DumpTableRowsList"/>): only the rows on screen exist as controls, so opening a
/// 100,000-row CSV or dumping a big list costs about what 30 rows do, and selecting a row repaints the visible rows
/// instead of rebuilding the table.
/// </summary>
public partial class DumpTableView : UserControl
{
    /// <summary>
    /// The rows fill the height the table is given (the CSV preview). Otherwise (the Results panel, a notebook output,
    /// both already inside a scrolling list) the rows scroll inside the table, at most <see cref="RowsMaxHeight"/> tall.
    /// </summary>
    public static readonly StyledProperty<bool> FillHeightProperty =
        AvaloniaProperty.Register<DumpTableView, bool>(nameof(FillHeight));

    /// <summary>How tall the rows may grow inside a host that scrolls itself.</summary>
    public const double RowsMaxHeight = 480;

    private Grid? _tableGrid;
    private Grid? _tableHost;
    private Border? _emptyRow;
    private TextBlock? _emptyText;
    private DumpTableRowsList? _rowsList;
    private ScrollViewer? _scrollViewer;
    private DumpTableResult? _currentTable;
    private bool _refreshQueued;

    public DumpTableView()
    {
        InitializeComponent();
        _tableGrid = this.FindControl<Grid>("TableGrid");
        _tableHost = this.FindControl<Grid>("TableHost");
        _emptyRow = this.FindControl<Border>("EmptyRow");
        _emptyText = this.FindControl<TextBlock>("EmptyText");
        _rowsList = this.FindControl<DumpTableRowsList>("RowsPart");
        _scrollViewer = this.FindControl<ScrollViewer>("TableScrollViewer");
        if (_rowsList != null) _rowsList.Owner = this;

        if (_scrollViewer != null)
        {
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += (s, e) =>
        {
            TableBrushes = null;
            if (_currentTable != null) Refresh();
        };
    }

    public bool FillHeight
    {
        get => GetValue(FillHeightProperty);
        set => SetValue(FillHeightProperty, value);
    }

    /// <summary>The table shown (the rows read it).</summary>
    internal DumpTableResult? Table => _currentTable;

    /// <summary>The theme brushes the rows paint with (resolved once per theme).</summary>
    internal DumpTableBrushes? TableBrushes { get; private set; }

    /// <summary>The column widths every row shares, so the virtualized rows line up under the header.</summary>
    internal DumpTableLayout Layout { get; private set; } = DumpTableLayout.Empty;

    /// <summary>The rows list (tests and tools count its realized rows).</summary>
    public DumpTableRowsList? RowsList => _rowsList;

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The columns stretch to the width available: work them out again when it changes.
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5 && _currentTable != null) QueueRefresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillHeightProperty && _rowsList != null)
        {
            _rowsList.MaxHeight = FillHeight ? double.PositiveInfinity : RowsMaxHeight;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is DumpTableResult table) Attach(table, refreshNow: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnhookTable();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is DumpTableResult table)
        {
            Attach(table, refreshNow: true);
        }
        else
        {
            UnhookTable();
            if (_rowsList != null) _rowsList.ItemsSource = null;
            _tableGrid?.Children.Clear();
        }
    }

    /// <summary>Shows <paramref name="table"/> now (nested tables and tools call this directly).</summary>
    public void BuildTable(DumpTableResult table) => Attach(table, refreshNow: true);

    private void Attach(DumpTableResult table, bool refreshNow)
    {
        if (!ReferenceEquals(_currentTable, table))
        {
            UnhookTable();
            _currentTable = table;
            _currentTable.RowsViewChanged += OnTableRowsViewChanged;
            if (_rowsList != null)
            {
                _rowsList.MaxHeight = FillHeight ? double.PositiveInfinity : RowsMaxHeight;
                _rowsList.ItemsSource = table.FilteredRows;
            }
        }

        if (refreshNow) Refresh();
    }

    private void UnhookTable()
    {
        if (_currentTable != null)
        {
            _currentTable.RowsViewChanged -= OnTableRowsViewChanged;
            _currentTable = null;
        }
    }

    // The rows themselves follow FilteredRows on their own; the header (sort marks), the column widths and the empty
    // state catch up once per UI turn, however many rows were added meanwhile.
    private void OnTableRowsViewChanged() => QueueRefresh();

    private void QueueRefresh()
    {
        // No UI loop to post to (a test building the view by hand): catch up at once.
        if (Application.Current == null)
        {
            Refresh();
            return;
        }

        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            Refresh();
        }, DispatcherPriority.Background);
    }

    /// <summary>Brings the header, the column widths, the empty state and the visible rows up to date.</summary>
    public void Refresh()
    {
        var table = _currentTable;
        if (table == null || _tableGrid == null) return;

        TableBrushes ??= DumpTableBrushes.Resolve(this);
        double viewport = _scrollViewer?.Bounds.Width ?? 0;
        var layout = DumpTableLayout.Compute(table, viewport);
        bool widthsChanged = !layout.SameWidths(Layout);
        Layout = layout;
        if (_tableHost != null) _tableHost.Width = layout.TotalWidth;

        BuildHeader(table, TableBrushes);

        bool empty = table.FilteredRows.Count == 0 && table.Columns.Count > 0;
        if (_emptyRow != null)
        {
            _emptyRow.IsVisible = empty;
            _emptyRow.Background = TableBrushes.RowEven;
        }

        if (_emptyText != null) _emptyText.Text = table.IsFiltered ? "No rows matching filter." : "Table contains no rows.";
        _rowsList?.RefreshRealizedRows(remeasure: widthsChanged);
    }

    /// <summary>Selects (or unselects) the row shown at <paramref name="index"/> and repaints the visible rows.</summary>
    internal void ToggleSelection(int index)
    {
        if (_currentTable == null) return;
        _currentTable.SelectedRowIndex = _currentTable.SelectedRowIndex == index ? -1 : index;
        _rowsList?.RefreshRealizedRows(remeasure: false);
    }

    /// <summary>A nested table was opened or closed: the visible rows rebuild their content (nothing else does).</summary>
    internal void NestedToggled() => _rowsList?.RefreshRealizedRows(remeasure: true, rebuild: true);

    internal IBrush ResolveBrush(string resourceKey, string fallbackHex)
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

    internal static async Task SetClipboardTextAsync(string text)
    {
        try
        {
            TopLevel? topLevel = null;
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var win = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                if (win != null) topLevel = TopLevel.GetTopLevel(win);
            }
            else if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime singleView && singleView.MainView != null)
            {
                topLevel = TopLevel.GetTopLevel(singleView.MainView);
            }

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(text);
            }
        }
        catch { }
    }
}
