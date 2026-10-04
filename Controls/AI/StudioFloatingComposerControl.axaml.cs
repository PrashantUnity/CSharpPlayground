using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
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
            header.PointerCaptureLost += OnHeaderPointerCaptureLost;
        }

        AttachedToVisualTree += (_, _) =>
        {
            StudioAppContext.Instance.UI.ContributionsChanged += RefreshDynamicActions;
            RefreshDynamicActions();
            UpdateLayoutForDockMode();

            if (DataContext is AiComposerViewModel vm)
            {
                vm.DockModeChanged += OnDockModeChanged;
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            StudioAppContext.Instance.UI.ContributionsChanged -= RefreshDynamicActions;
            if (DataContext is AiComposerViewModel vm)
            {
                vm.DockModeChanged -= OnDockModeChanged;
            }
        };

        DataContextChanged += (_, _) => UpdateLayoutForDockMode();
    }

    private void OnDockModeChanged(FrySharp.Sdk.AiDockMode mode)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLayoutForDockMode);
    }

    private void UpdateLayoutForDockMode()
    {
        var container = this.FindControl<Border>("ComposerContainer");
        if (container == null) return;

        if (DataContext is AiComposerViewModel vm && (vm.IsExtracted || vm.DockMode != FrySharp.Sdk.AiDockMode.FloatingOverlay))
        {
            container.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            container.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            container.Margin = new Thickness(0);
            container.Width = double.NaN;
            container.Height = double.NaN;
        }
        else if (DataContext is AiComposerViewModel normalVm)
        {
            container.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            container.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            container.Margin = new Thickness(0, 48, 24, 0);
            container.Width = normalVm.WindowWidth;
            container.Height = normalVm.WindowHeight;
        }
    }

    private void RefreshDynamicActions()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var host = this.FindControl<StackPanel>("DynamicActionsHost");
            if (host == null) return;

            host.Children.Clear();
            var actions = StudioAppContext.Instance.UI.ComposerActions;

            foreach (var action in actions)
            {
                if (!action.IsVisible) continue;

                if (action.CustomContentFactory != null)
                {
                    var content = action.CustomContentFactory();
                    if (content is Control ctrl) host.Children.Add(ctrl);
                    else host.Children.Add(new ContentControl { Content = content });
                }
                else
                {
                    var btn = new Button
                    {
                        Classes = { "composer-btn" },
                        Padding = new Thickness(5, 2),
                        Content = new TextBlock
                        {
                            Text = action.Title,
                            FontSize = 10.5,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    };
                    ToolTip.SetTip(btn, action.Tooltip ?? action.Title);
                    btn.Click += (_, _) => action.OnClick?.Invoke();
                    host.Children.Add(btn);
                }
            }
        });
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't drag if clicking buttons, toggles, comboboxes or other interactive controls in header
        if (e.Source is Visual visual &&
            (visual.FindAncestorOfType<Button>(includeSelf: true) != null ||
             visual.FindAncestorOfType<ToggleButton>(includeSelf: true) != null ||
             visual.FindAncestorOfType<ComboBox>(includeSelf: true) != null ||
             visual.FindAncestorOfType<TextBox>(includeSelf: true) != null))
        {
            return;
        }

        if (DataContext is AiComposerViewModel vm && (vm.IsExtracted || vm.DockMode != FrySharp.Sdk.AiDockMode.FloatingOverlay))
        {
            return;
        }

        var container = this.FindControl<Border>("ComposerContainer");
        if (container != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _startMargin = container.Margin;
            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        // If mouse button is no longer pressed, abort drag immediately
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            return;
        }

        var container = this.FindControl<Border>("ComposerContainer");
        if (container != null)
        {
            var current = e.GetPosition(this);
            var deltaX = current.X - _dragStartPoint.X;
            var deltaY = current.Y - _dragStartPoint.Y;

            // Smooth, clamped positioning
            var newRight = Math.Max(0, _startMargin.Right - deltaX);
            var newTop = Math.Max(24, _startMargin.Top + deltaY);

            if (Bounds.Width > 0 && container.Bounds.Width > 0)
            {
                var maxRight = Math.Max(0, Bounds.Width - container.Bounds.Width);
                newRight = Math.Clamp(newRight, 0, maxRight);
            }
            if (Bounds.Height > 0)
            {
                var maxTop = Math.Max(24, Bounds.Height - 60);
                newTop = Math.Clamp(newTop, 24, maxTop);
            }

            container.Margin = new Thickness(0, newTop, newRight, 0);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
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
