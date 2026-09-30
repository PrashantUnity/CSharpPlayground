using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>The row above the first cell: the divider that inserts a cell at the beginning of the notebook.</summary>
public sealed class NotebookCanvasHeader
{
    public static NotebookCanvasHeader Instance { get; } = new();

    private NotebookCanvasHeader()
    {
    }
}

/// <summary>
/// The row below the last cell: the space at the end of the notebook. It is a row, not a margin on the list, because a
/// virtualizing list treats a margin as part of the area it fills, and would leave the strip that the margin covers empty
/// until it had scrolled that far.
/// </summary>
public sealed class NotebookCanvasFooter
{
    public static NotebookCanvasFooter Instance { get; } = new();

    private NotebookCanvasFooter()
    {
    }
}

/// <summary>
/// A notebook's cells as the flat list the canvas draws: the header row, one row per cell, then the footer row. Bound to a virtualizing
/// list, so opening a notebook builds the cells on screen and no others, however long it is (building each cell took
/// about 100 ms and 17 MB, so a 150-cell notebook took 15 seconds and 2.6 GB). It follows the cells: adding, removing or
/// moving one changes just its row.
/// </summary>
public sealed class NotebookCanvasRows
{
    private readonly ObservableCollection<NotebookCellViewModel> _cells;

    public NotebookCanvasRows(ObservableCollection<NotebookCellViewModel> cells)
    {
        _cells = cells;
        cells.CollectionChanged += OnCellsChanged;
        Rebuild();
    }

    /// <summary>The rows to draw, in order: <see cref="NotebookCanvasHeader"/>, the cells, then <see cref="NotebookCanvasFooter"/>.</summary>
    public RangeObservableCollection<object> Rows { get; } = new();

    private void Rebuild()
    {
        var rows = new List<object>(_cells.Count + 2) { NotebookCanvasHeader.Instance };
        rows.AddRange(_cells);
        rows.Add(NotebookCanvasFooter.Instance);
        Rows.ReplaceAll(rows);
    }

    private void OnCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Every row is one position further than its cell (the header comes first), and the footer stays last: cells are
        // inserted before it.
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null && e.NewStartingIndex >= 0:
                Rows.InsertRange(e.NewStartingIndex + 1, e.NewItems.Cast<object>().ToList());
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null && e.OldStartingIndex >= 0:
                Rows.RemoveRange(e.OldStartingIndex + 1, e.OldItems.Count);
                break;
            case NotifyCollectionChangedAction.Move when e.OldItems is { Count: 1 }:
                Rows.Move(e.OldStartingIndex + 1, e.NewStartingIndex + 1);
                break;
            default:
                Rebuild();
                break;
        }
    }
}
