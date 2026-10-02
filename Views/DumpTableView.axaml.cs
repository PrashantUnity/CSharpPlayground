using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class DumpTableView : UserControl
{
    private Grid? _tableGrid;
    private ScrollViewer? _scrollViewer;
    private DumpTableResult? _currentTable;

    public DumpTableView()
    {
        InitializeComponent();
        _tableGrid = this.FindControl<Grid>("TableGrid");
        _scrollViewer = this.FindControl<ScrollViewer>("TableScrollViewer");

        if (_scrollViewer != null)
        {
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += (s, e) =>
        {
            if (DataContext is DumpTableResult table)
            {
                BuildTable(table);
            }
        };
    }

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && _tableGrid != null)
        {
            _tableGrid.MinWidth = e.NewSize.Width;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_scrollViewer == null)
        {
            _scrollViewer = this.FindControl<ScrollViewer>("TableScrollViewer");
            if (_scrollViewer != null)
            {
                _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
                if (_scrollViewer.Bounds.Width > 0 && _tableGrid != null)
                {
                    _tableGrid.MinWidth = _scrollViewer.Bounds.Width;
                }
            }
        }

        if (DataContext is DumpTableResult table)
        {
            HookTable(table);
            BuildTable(table);
        }
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
        UnhookTable();
        if (DataContext is DumpTableResult table)
        {
            HookTable(table);
            BuildTable(table);
        }
    }

    private void HookTable(DumpTableResult table)
    {
        _currentTable = table;
        _currentTable.RowsViewChanged += OnTableRowsViewChanged;
    }

    private void UnhookTable()
    {
        if (_currentTable != null)
        {
            _currentTable.RowsViewChanged -= OnTableRowsViewChanged;
            _currentTable = null;
        }
    }

    private void OnTableRowsViewChanged()
    {
        if (DataContext is DumpTableResult table)
        {
            BuildTable(table);
        }
    }

    private IBrush ResolveBrush(string resourceKey, string fallbackHex)
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

    private static async Task SetClipboardTextAsync(string text)
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
