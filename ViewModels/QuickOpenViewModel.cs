using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public enum QuickOpenMode
{
    Files,
    Commands,
    GoToLine
}

public partial class QuickOpenViewModel : ObservableObject
{
    private readonly List<QuickOpenItem> _commandItems = new();
    private readonly List<QuickOpenItem> _documentItems = new();

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _placeholder = "Search files by name ('>' for commands, ':' for line)...";

    [ObservableProperty]
    private QuickOpenItem? _selectedItem;

    public ObservableCollection<QuickOpenItem> FilteredItems { get; } = new();

    public event Action<int>? RequestGoToLine;
    public event Action? RequestClose;

    partial void OnSearchTextChanged(string value)
    {
        FilterItems();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (!value)
        {
            SearchText = string.Empty;
        }
    }

    public void RegisterCommands(IEnumerable<QuickOpenItem> commands)
    {
        _commandItems.Clear();
        _commandItems.AddRange(commands);
    }

    public void RegisterDocuments(IEnumerable<QuickOpenItem> documents)
    {
        _documentItems.Clear();
        _documentItems.AddRange(documents);
    }

    public void Show(QuickOpenMode mode = QuickOpenMode.Files)
    {
        IsOpen = true;
        SearchText = mode switch
        {
            QuickOpenMode.Commands => ">",
            QuickOpenMode.GoToLine => ":",
            _ => string.Empty
        };
        FilterItems();
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void SelectNext()
    {
        if (FilteredItems.Count == 0) return;
        int nextIndex = SelectedItem != null ? FilteredItems.IndexOf(SelectedItem) + 1 : 0;
        if (nextIndex >= FilteredItems.Count) nextIndex = 0;
        SetSelectedIndex(nextIndex);
    }

    [RelayCommand]
    public void SelectPrevious()
    {
        if (FilteredItems.Count == 0) return;
        int prevIndex = SelectedItem != null ? FilteredItems.IndexOf(SelectedItem) - 1 : FilteredItems.Count - 1;
        if (prevIndex < 0) prevIndex = FilteredItems.Count - 1;
        SetSelectedIndex(prevIndex);
    }

    private void SetSelectedIndex(int index)
    {
        if (index >= 0 && index < FilteredItems.Count)
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = false;
            }
            SelectedItem = FilteredItems[index];
            SelectedItem.IsSelected = true;
        }
    }

    [RelayCommand]
    public async Task ExecuteSelectedAsync()
    {
        if (SelectedItem == null && FilteredItems.Count > 0)
        {
            SelectedItem = FilteredItems[0];
        }

        var item = SelectedItem;
        if (item == null) return;

        Close();

        if (item.Kind == QuickOpenItemKind.LineJump && item.Tag is int line)
        {
            RequestGoToLine?.Invoke(line);
            return;
        }

        if (item.ExecuteAction != null)
        {
            item.ExecuteAction.Invoke();
        }
        else if (item.ExecuteAsyncAction != null)
        {
            await item.ExecuteAsyncAction.Invoke();
        }
    }

    public void FilterItems()
    {
        FilteredItems.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        if (query.StartsWith(">"))
        {
            Placeholder = "Type a command to run...";
            var cmdQuery = query.Length > 1 ? query[1..].Trim() : string.Empty;

            IEnumerable<QuickOpenItem> matches = string.IsNullOrEmpty(cmdQuery)
                ? _commandItems
                : _commandItems
                    .Where(c => c.Title.Contains(cmdQuery, StringComparison.OrdinalIgnoreCase) ||
                                c.Category.Contains(cmdQuery, StringComparison.OrdinalIgnoreCase) ||
                                c.Subtitle.Contains(cmdQuery, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Title.StartsWith(cmdQuery, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);

            foreach (var item in matches.Take(30))
            {
                FilteredItems.Add(item);
            }
        }
        else if (query.StartsWith(":"))
        {
            Placeholder = "Type a line number between 1 and total lines to jump to...";
            var lineStr = query[1..].Trim();
            if (int.TryParse(lineStr, out var lineNum) && lineNum > 0)
            {
                var lineItem = new QuickOpenItem
                {
                    Title = $"Go to line {lineNum}",
                    Subtitle = $"Press Enter to navigate caret to line {lineNum}",
                    Category = "Navigation",
                    IconKind = "RayStartArrow",
                    IconColorHex = "#75D59A",
                    Kind = QuickOpenItemKind.LineJump,
                    Tag = lineNum
                };
                FilteredItems.Add(lineItem);
            }
            else
            {
                var promptItem = new QuickOpenItem
                {
                    Title = string.IsNullOrEmpty(lineStr) ? "Type a line number..." : $"Invalid line: '{lineStr}'",
                    Subtitle = "Enter a positive number to jump directly to that line",
                    Category = "Navigation",
                    IconKind = "RayStartArrow",
                    IconColorHex = "#8B949E",
                    Kind = QuickOpenItemKind.LineJump,
                    Tag = 1
                };
                FilteredItems.Add(promptItem);
            }
        }
        else
        {
            Placeholder = "Search files by name ('>' for commands, ':' for line)...";
            IEnumerable<QuickOpenItem> matches = string.IsNullOrEmpty(query)
                ? _documentItems
                : _documentItems
                    .Where(d => d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                d.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase);

            foreach (var item in matches.Take(30))
            {
                FilteredItems.Add(item);
            }
        }

        if (FilteredItems.Count > 0)
        {
            SetSelectedIndex(0);
        }
        else
        {
            SelectedItem = null;
        }
    }
}
