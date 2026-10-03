using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.AI;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.AI;

public partial class StudioFloatingComposerControl : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private Thickness _startMargin;

    public StudioFloatingComposerControl()
    {
        InitializeComponent();

        var header = this.FindControl<Border>("HeaderBar");
        if (header != null)
        {
            header.PointerPressed += OnHeaderPointerPressed;
            header.PointerMoved += OnHeaderPointerMoved;
            header.PointerReleased += OnHeaderPointerReleased;
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var container = this.FindControl<Border>("ComposerContainer");
        if (container != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _startMargin = container.Margin;
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var container = this.FindControl<Border>("ComposerContainer");
            if (container != null)
            {
                var current = e.GetPosition(this);
                var deltaX = current.X - _dragStartPoint.X;
                var deltaY = current.Y - _dragStartPoint.Y;

                // Adjust right and top margin for fluid dragging
                var newRight = Math.Max(0, _startMargin.Right - deltaX);
                var newTop = Math.Max(24, _startMargin.Top + deltaY);

                container.Margin = new Thickness(0, newTop, newRight, 0);
            }
        }
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnPromptTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not AiComposerViewModel vm) return;

        // Cmd+Enter or Ctrl+Enter: Accept all changes
        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            if (vm.HasModifiedFiles)
            {
                vm.AcceptAllChanges();
                e.Handled = true;
                return;
            }
        }

        // Enter without shift: send message
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (!vm.IsGenerating)
            {
                _ = vm.SendMessageAsync();
                e.Handled = true;
            }
        }
    }
}
