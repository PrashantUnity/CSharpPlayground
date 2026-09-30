using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpNotebookStudioView : UserControl
{
    public CSharpNotebookStudioView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(InteractiveVisualizerControl.StepSourceLineChangedEvent, OnVisualizerStepLine);
        Unloaded += OnViewUnloaded;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not CSharpNotebookStudioViewModel vm) return;

        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!isModifier) return;

        if (e.Delta.Y > 0)
        {
            vm.ZoomInCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Delta.Y < 0)
        {
            vm.ZoomOutCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Each cell compiles under its id, so a step's source file names the cell whose code recorded it.
    private void OnVisualizerStepLine(object? sender, VisualizerStepLineEventArgs e)
    {
        e.Handled = true;
        var editors = this.GetVisualDescendants().OfType<BindableTextEditor>().ToList();
        var target = e.Line > 0 && !string.IsNullOrEmpty(e.SourceFile)
            ? editors.FirstOrDefault(editor => editor.DataContext is NotebookCellViewModel cell && cell.Id == e.SourceFile)
            : null;

        foreach (var editor in editors)
        {
            editor.SetStepLine(editor == target ? e.Line : -1);
        }

        if (e.Reveal)
        {
            target?.RevealLine(e.Line);
        }
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

    // Unloaded releases the subscription when the studio's root is detached, but a kept-alive page comes back with the
    // same view (and the same DataContext, so OnDataContextChanged does not run again): subscribe again on attach.
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_subscribedVm == null && DataContext is CSharpNotebookStudioViewModel vm)
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

        // ── Typography & Font Zoom (Ctrl+= / Ctrl+- / Ctrl+0) ──
        if (isCmdOrCtrl && (e.Key == Key.OemPlus || e.Key == Key.Add || e.PhysicalKey == PhysicalKey.Equal || e.PhysicalKey == PhysicalKey.NumPadAdd))
        {
            vm.ZoomInCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemMinus || e.Key == Key.Subtract || e.PhysicalKey == PhysicalKey.Minus || e.PhysicalKey == PhysicalKey.NumPadSubtract))
        {
            vm.ZoomOutCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.D0 || e.Key == Key.NumPad0 || e.PhysicalKey == PhysicalKey.Digit0 || e.PhysicalKey == PhysicalKey.NumPad0))
        {
            vm.ResetZoomCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // VS Code Quick Open (Ctrl+P / Cmd+P)
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.P)
        {
            vm.ShowQuickOpen();
            e.Handled = true;
            return;
        }

        // VS Code Command Palette (Ctrl+Shift+P / Cmd+Shift+P)
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.P)
        {
            vm.ShowCommandPalette();
            e.Handled = true;
            return;
        }

        // VS Code Close Tab (Ctrl+W / Cmd+W)
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.W)
        {
            if (vm.ActiveTab != null)
            {
                vm.CloseTab(vm.ActiveTab);
            }
            e.Handled = true;
            return;
        }

        // VS Code & JetBrains Open Settings (Ctrl+, / Cmd+,)
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemComma || e.Key == Key.Oem1))
        {
            vm.NavigateToSettings();
            e.Handled = true;
            return;
        }

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
                }
            }
            .Concat(LanguageFileTypes.PerLanguage(StudioLanguageServices.Default.Registry))
            .Concat(new List<FilePickerFileType>
            {
                new("Project Archives (*.zip)")
                {
                    Patterns = new[] { "*.zip" }
                },
                new("All Files (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }).ToList()
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

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            if (sender is Visual v && v.DataContext is NotebookTabViewModel tabVm)
            {
                tabVm.CloseTabCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
