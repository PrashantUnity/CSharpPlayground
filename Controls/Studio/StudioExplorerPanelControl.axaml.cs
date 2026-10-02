using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class StudioExplorerPanelControl : UserControl
{
    // Marks the menu entries added for each source-file language, so they're replaced rather than repeated.
    private const string NewFileEntryTag = "new-source-file";

    public event EventHandler<RoutedEventArgs>? OpenProjectRequested;

    public StudioExplorerPanelControl()
    {
        InitializeComponent();
    }

    private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ExplorerItemViewModel vm && vm.IsRenaming)
        {
            vm.CommitRename();
        }
    }

    private void OnOpenProjectMenuClick(object? sender, RoutedEventArgs e)
    {
        OpenProjectRequested?.Invoke(this, e);
    }

    // The Explorer's own menu: "New Python File" and friends after "New Script", for the selected folder.
    private void OnRootMenuOpening(object? sender, EventArgs e)
    {
        if (sender is MenuFlyout menu) AddNewFileEntries(menu, target: null, afterIndex: 0);
    }

    // A folder's menu: the same entries after "New Script Here", creating the file in that folder.
    private void OnItemMenuOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu || menu.Target?.DataContext is not ExplorerItemViewModel item) return;
        AddNewFileEntries(menu, item.IsManageableDirectory ? item : null, afterIndex: 1, show: item.IsManageableDirectory);
    }

    private void AddNewFileEntries(MenuFlyout menu, ExplorerItemViewModel? target, int afterIndex, bool show = true)
    {
        foreach (var old in menu.Items.OfType<MenuItem>().Where(m => Equals(m.Tag, NewFileEntryTag)).ToList())
        {
            menu.Items.Remove(old);
        }

        if (!show || DataContext is not IExplorerNewFileHost host) return;

        var index = Math.Min(afterIndex + 1, menu.Items.Count);
        foreach (var option in host.NewFileOptions)
        {
            var icon = new MaterialIcon { Width = 14, Height = 14 };
            if (Enum.TryParse<MaterialIconKind>(option.IconKind, out var kind)) icon.Kind = kind;
            if (Color.TryParse(option.AccentHex, out var color)) icon.Foreground = new SolidColorBrush(color);

            menu.Items.Insert(index++, new MenuItem
            {
                Header = target == null ? option.Label : option.Label + " Here",
                Icon = icon,
                Command = option.Command,
                CommandParameter = target,
                Tag = NewFileEntryTag
            });
        }
    }
}
