using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// The Explorer's visible rows as one flat list: every item of the tree, depth first, leaving out what a collapsed folder
/// hides. It is what the Explorer binds to, so a virtualizing list only ever builds the rows on screen (a workspace with
/// thousands of files costs a screenful of controls, not thousands), instead of one nested control per item.
/// The list follows the tree by itself: adding, removing or re-sorting items rebuilds it, and expanding or collapsing a
/// folder inserts or removes just that folder's rows.
/// </summary>
public sealed class ExplorerRowList
{
    private readonly ObservableCollection<ExplorerItemViewModel> _roots;
    private int _suspended;
    private bool _dirty;

    public ExplorerRowList(ObservableCollection<ExplorerItemViewModel> roots)
    {
        _roots = roots;
        Watch(roots);
        Rebuild();
    }

    /// <summary>The rows to show, in order. Indentation comes from each item's own depth.</summary>
    public RangeObservableCollection<ExplorerItemViewModel> Rows { get; } = new();

    /// <summary>
    /// Holds off updates while the tree is changed in bulk (a rebuild, a sort); one rebuild follows when the returned scope
    /// is disposed. Without it, each of thousands of single changes would rebuild the list.
    /// </summary>
    public IDisposable Suspend()
    {
        _suspended++;
        return new Resume(this);
    }

    private sealed class Resume(ExplorerRowList owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            owner.EndSuspend();
        }
    }

    private void EndSuspend()
    {
        if (--_suspended == 0 && _dirty) Rebuild();
    }

    private void Invalidate()
    {
        if (_suspended > 0)
        {
            _dirty = true;
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        _dirty = false;
        var rows = new List<ExplorerItemViewModel>();
        Flatten(_roots, rows);
        Rows.ReplaceAll(rows);
    }

    private static void Flatten(IEnumerable<ExplorerItemViewModel> items, List<ExplorerItemViewModel> into)
    {
        foreach (var item in items)
        {
            into.Add(item);
            if (item.IsDirectory && item.IsExpanded) Flatten(item.Children, into);
        }
    }

    // Unsubscribe first: the same collection or item is watched again when a sort takes it out and puts it back.
    private void Watch(ObservableCollection<ExplorerItemViewModel> items)
    {
        items.CollectionChanged -= OnItemsChanged;
        items.CollectionChanged += OnItemsChanged;
        foreach (var item in items) WatchItem(item);
    }

    private void WatchItem(ExplorerItemViewModel item)
    {
        if (!item.IsDirectory) return;
        item.PropertyChanged -= OnItemPropertyChanged;
        item.PropertyChanged += OnItemPropertyChanged;
        Watch(item.Children);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ExplorerItemViewModel item in e.NewItems) WatchItem(item);
        }

        Invalidate();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExplorerItemViewModel.IsExpanded) || sender is not ExplorerItemViewModel folder) return;

        if (_suspended > 0)
        {
            _dirty = true;
            return;
        }

        var index = Rows.IndexOf(folder);
        if (index < 0) return; // Hidden by a collapsed ancestor: its own rows appear when that ancestor is expanded.

        if (folder.IsExpanded)
        {
            var children = new List<ExplorerItemViewModel>();
            Flatten(folder.Children, children);
            Rows.InsertRange(index + 1, children);
        }
        else
        {
            var count = 0;
            while (index + 1 + count < Rows.Count && Rows[index + 1 + count].Depth > folder.Depth) count++;
            Rows.RemoveRange(index + 1, count);
        }
    }
}
