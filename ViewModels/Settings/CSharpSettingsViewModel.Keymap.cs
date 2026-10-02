using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

public sealed partial class KeymapCategoryChip : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public KeymapCategoryChip(string name, bool isSelected = false)
    {
        Name = name;
        _isSelected = isSelected;
    }
}

public partial class CSharpSettingsViewModel
{
    [ObservableProperty]
    private string _selectedKeymapCategory = "All";

    [ObservableProperty]
    private string _keymapSearchQuery = string.Empty;

    public ObservableCollection<KeymapShortcutItem> FilteredShortcuts { get; } = new();
    public ObservableCollection<KeymapCategoryChip> KeymapCategoryChips { get; } = new();

    private void InitializeKeymap()
    {
        KeymapCategoryChips.Clear();
        KeymapCategoryChips.Add(new KeymapCategoryChip("All", true));
        KeymapCategoryChips.Add(new KeymapCategoryChip("General"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Layout"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Editor"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Execution"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Debug"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Side Bar"));
        KeymapCategoryChips.Add(new KeymapCategoryChip("Bottom Deck"));

        ApplyKeymapFilter();
    }

    [RelayCommand]
    public void SelectKeymapCategory(KeymapCategoryChip? chip)
    {
        if (chip == null) return;
        SelectedKeymapCategory = chip.Name;

        foreach (var c in KeymapCategoryChips)
        {
            c.IsSelected = string.Equals(c.Name, chip.Name, StringComparison.OrdinalIgnoreCase);
        }

        ApplyKeymapFilter();
    }

    partial void OnKeymapSearchQueryChanged(string value)
    {
        ApplyKeymapFilter();
    }

    public void ApplyKeymapFilter()
    {
        FilteredShortcuts.Clear();
        var search = KeymapSearchQuery?.Trim() ?? string.Empty;
        var category = SelectedKeymapCategory ?? "All";

        foreach (var item in Shortcuts)
        {
            bool categoryMatch = category == "All" || string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase);
            if (!categoryMatch) continue;

            if (string.IsNullOrEmpty(search) ||
                item.Action.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Shortcut.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredShortcuts.Add(item);
            }
        }
    }
}
