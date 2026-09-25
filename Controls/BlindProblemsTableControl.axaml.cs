using System;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class BlindProblemsTableControl : UserControl
{
    // The divider being dragged and where the pointer started, in header coordinates (the header never moves, so
    // every move is measured from the same place however the columns have shifted since).
    private Border? _dragDivider;
    private double _dragStartX;

    public BlindProblemsTableControl()
    {
        InitializeComponent();
        HeaderGrid.SizeChanged += (_, e) => UpdateAvailableWidth(e.NewSize.Width);
    }

    private BlindProblemTableColumns? Columns => (DataContext as CSharpBlindProblemsViewModel)?.Columns;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateAvailableWidth(HeaderGrid.Bounds.Width);
    }

    private void UpdateAvailableWidth(double width)
    {
        if (Columns is { } columns && width > 0) columns.AvailableWidth = width;
    }

    private void OnDividerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border divider || Columns is not { } columns ||
            !e.GetCurrentPoint(divider).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            columns.Reset();
            return;
        }

        UpdateAvailableWidth(HeaderGrid.Bounds.Width);
        columns.BeginResize();
        _dragDivider = divider;
        _dragStartX = e.GetPosition(HeaderGrid).X;
        divider.Classes.Add("dragging");
        e.Pointer.Capture(divider);
    }

    private void OnDividerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragDivider == null || !ReferenceEquals(sender, _dragDivider) || Columns is not { } columns) return;
        columns.Resize(DividerNumber(_dragDivider), e.GetPosition(HeaderGrid).X - _dragStartX);
        e.Handled = true;
    }

    private void OnDividerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragDivider == null) return;
        e.Pointer.Capture(null);
        EndDrag();
        e.Handled = true;
    }

    private void OnDividerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    private void EndDrag()
    {
        if (_dragDivider == null) return;
        _dragDivider.Classes.Remove("dragging");
        _dragDivider = null;
        Columns?.EndResize();
    }

    private static int DividerNumber(Control divider) => divider.Tag is string tag && int.TryParse(tag, out int number) ? number : 0;
}
