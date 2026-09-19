using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpNotebookStudioView : UserControl
{
    public CSharpNotebookStudioView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Unloaded += OnViewUnloaded;
    }

    private CSharpNotebookStudioViewModel? _subscribedVm;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_subscribedVm != null)
        {
            _subscribedVm.RequestScrollToCell -= OnRequestScrollToCell;
            _subscribedVm = null;
        }

        if (DataContext is CSharpNotebookStudioViewModel vm)
        {
            _subscribedVm = vm;
            vm.RequestScrollToCell += OnRequestScrollToCell;
        }
    }

    private void OnViewUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.RequestScrollToCell -= OnRequestScrollToCell;
            _subscribedVm = null;
        }
    }

    private void OnRequestScrollToCell(NotebookCellViewModel cell)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("NotebookCanvasScrollViewer");
            // Bring active cell into view smoothly if applicable
        });
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CSharpNotebookStudioViewModel vm) return;

        if (e.Source is TextBox tb && tb.DataContext is ExplorerItemViewModel itemVm && itemVm.IsRenaming)
        {
            if (e.Key == Key.Enter)
            {
                itemVm.CommitRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Escape)
            {
                itemVm.CancelRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        bool isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // VS Code Shortcut: Ctrl+B / Cmd+B -> Toggle Primary SideBar
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.B)
        {
            vm.ToggleSideBar();
            e.Handled = true;
            return;
        }

        // VS Code Shortcut: Ctrl+Shift+E -> Focus Explorer
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.E)
        {
            vm.SelectActivityBarItem(0);
            e.Handled = true;
            return;
        }

        // VS Code Shortcut: Ctrl+Shift+F -> Focus Search
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.F)
        {
            vm.SelectActivityBarItem(3);
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.O)
        {
            _ = OpenProjectOrFileDialogAsync();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Enter)
        {
            _ = vm.RunAllCellsAsync();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.S)
        {
            _ = vm.SaveAsync();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.B)
        {
            vm.AddCodeCell(vm.ActiveTab?.ActiveCell);
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.A)
        {
            vm.AddCellAbove();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D)
        {
            vm.DeleteActiveCell();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemOpenBrackets || e.Key == Key.Oem4))
        {
            vm.ActiveTab?.ActiveCell?.FoldAllCode();
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemCloseBrackets || e.Key == Key.Oem6))
        {
            vm.ActiveTab?.ActiveCell?.UnfoldAllCode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = vm.RunAllCellsAsync();
            e.Handled = true;
            return;
        }
    }

    public async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        await OpenProjectOrFileDialogAsync();
    }

    private async Task OpenProjectOrFileDialogAsync()
    {
        if (DataContext is not CSharpNotebookStudioViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Project or Notebook File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("FryPDF Project / Document (*.frycsproj, *.frynbproj, *.frycs, *.frynb, *.csproj, *.cs, *.csx, *.zip)")
                {
                    Patterns = new[] { "*.frycsproj", "*.frynbproj", "*.frycs", "*.frynb", "*.csproj", "*.cs", "*.csx", "*.zip" }
                },
                new("C# Notebooks (*.frynb, *.frynbproj)")
                {
                    Patterns = new[] { "*.frynb", "*.frynbproj" }
                },
                new("C# Files (*.cs, *.csx, *.frycs)")
                {
                    Patterns = new[] { "*.cs", "*.csx", "*.frycs" }
                },
                new("Project Archives (*.zip)")
                {
                    Patterns = new[] { "*.zip" }
                },
                new("All Files (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } filePath)
        {
            await vm.OpenExternalProjectAsync(filePath);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not CSharpNotebookStudioViewModel vm) return;

        if (e.DataTransfer.Contains(DataFormat.File))
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files != null)
            {
                var first = files.FirstOrDefault();
                if (first != null && first.TryGetLocalPath() is { } localPath)
                {
                    await vm.OpenExternalProjectAsync(localPath);
                    e.Handled = true;
                }
            }
        }
    }
}
