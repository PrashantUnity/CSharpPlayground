using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can change many items with a single notification. Adding N items one
/// by one raises N change events, and every one of them makes a bound list re-measure; a range operation raises one.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces the whole content with <paramref name="items"/> (one Reset).</summary>
    public void ReplaceAll(IReadOnlyList<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        Notify(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Inserts <paramref name="items"/> at <paramref name="index"/> (one Add).</summary>
    public void InsertRange(int index, IReadOnlyList<T> items)
    {
        if (items.Count == 0) return;
        CheckReentrancy();
        if (Items is List<T> list)
        {
            list.InsertRange(index, items);
        }
        else
        {
            for (var i = 0; i < items.Count; i++) Items.Insert(index + i, items[i]);
        }

        Notify(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new List<T>(items), index));
    }

    /// <summary>Removes <paramref name="count"/> items starting at <paramref name="index"/> (one Remove).</summary>
    public void RemoveRange(int index, int count)
    {
        if (count <= 0) return;
        CheckReentrancy();
        var removed = new List<T>(count);
        for (var i = 0; i < count; i++) removed.Add(Items[index + i]);
        if (Items is List<T> list)
        {
            list.RemoveRange(index, count);
        }
        else
        {
            for (var i = 0; i < count; i++) Items.RemoveAt(index);
        }

        Notify(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
    }

    private void Notify(NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(args);
    }
}
