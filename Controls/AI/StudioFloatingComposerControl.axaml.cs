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
    private enum ResizeDirection
    {
        None,
        Left,
        Right,
        Bottom,
        Top,
        BottomLeft,
        BottomRight
    }

    private bool _isDragging;
    private Point _dragStartPoint;
    private Thickness _startMargin;

    private bool _isResizing;
    private ResizeDirection _activeResizeDirection;
    private Point _resizeStartPoint;
    private double _startWidth;
    private double _startHeight;
    private Thickness _resizeStartMargin;

    private bool _isMaximized;
    private double _preMaximizeWidth;
    private double _preMaximizeHeight;
    private Thickness _preMaximizeMargin;

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

        SetupResizeHandles();

        AttachedToVisualTree += (_, _) =>
        {
            StudioAppContext.Instance.UI.ContributionsChanged += RefreshDynamicActions;
            RefreshDynamicActions();
            HookViewModel(DataContext as AiComposerViewModel);
        };

        DetachedFromVisualTree += (_, _) =>
        {
            StudioAppContext.Instance.UI.ContributionsChanged -= RefreshDynamicActions;
            HookViewModel(null);
        };

        DataContextChanged += (_, _) =>
        {
            HookViewModel(DataContext as AiComposerViewModel);
        };
    }

    private AiComposerViewModel? _subscribedVm;

    private void HookViewModel(AiComposerViewModel? vm)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.DockModeChanged -= OnDockModeChanged;
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        if (vm != null)
        {
            _subscribedVm = vm;
            _subscribedVm.DockModeChanged += OnDockModeChanged;
            _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateLayoutForDockMode();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AiComposerViewModel.IsExtracted)
            or nameof(AiComposerViewModel.DockMode)
            or nameof(AiComposerViewModel.IsVisible))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLayoutForDockMode);
        }
    }

    private void OnDockModeChanged(FrySharp.Sdk.AiDockMode mode)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLayoutForDockMode);
    }

    private void UpdateLayoutForDockMode()
    {
        var container = this.FindControl<Border>("ComposerContainer");
        if (container == null) return;

        var header = this.FindControl<Border>("HeaderBar");
        var promptDeck = this.FindControl<Border>("PromptDeckBorder");

        if (DataContext is AiComposerViewModel vm && (vm.IsExtracted || vm.DockMode != FrySharp.Sdk.AiDockMode.FloatingOverlay))
        {
            container.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            container.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            container.Margin = new Thickness(0);
            container.Width = double.NaN;
            container.Height = double.NaN;
            container.CornerRadius = new CornerRadius(0);
            container.BorderThickness = new Thickness(0);
            container.BoxShadow = new Avalonia.Media.BoxShadows();
            if (header != null) header.CornerRadius = new CornerRadius(0);
            if (promptDeck != null) promptDeck.CornerRadius = new CornerRadius(0);
            SetResizeHandlesVisible(false);
        }
        else if (DataContext is AiComposerViewModel normalVm)
        {
            container.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            container.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            container.Margin = new Thickness(0, 48, 24, 0);
            container.Width = normalVm.WindowWidth;
            container.Height = normalVm.WindowHeight;
            container.CornerRadius = new CornerRadius(10);
            container.BorderThickness = new Thickness(1.5);
            container.BoxShadow = Avalonia.Media.BoxShadows.Parse("0 10 30 0 #50000000");
            if (header != null) header.CornerRadius = new CornerRadius(9, 9, 0, 0);
            if (promptDeck != null) promptDeck.CornerRadius = new CornerRadius(0, 0, 9, 9);
            SetResizeHandlesVisible(true);
        }
    }

    private void SetResizeHandlesVisible(bool visible)
    {
        var names = new[]
        {
            "ResizeHandleLeft", "ResizeHandleRight", "ResizeHandleBottom",
            "ResizeHandleTop", "ResizeHandleBottomLeft", "ResizeHandleBottomRight",
            "MaximizeRestoreButton"
        };
        foreach (var name in names)
        {
            var control = this.FindControl<Control>(name);
            if (control != null) control.IsVisible = visible;
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

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
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

    private void SetupResizeHandles()
    {
        SetupHandle("ResizeHandleLeft", ResizeDirection.Left);
        SetupHandle("ResizeHandleRight", ResizeDirection.Right);
        SetupHandle("ResizeHandleBottom", ResizeDirection.Bottom);
        SetupHandle("ResizeHandleTop", ResizeDirection.Top);
        SetupHandle("ResizeHandleBottomLeft", ResizeDirection.BottomLeft);
        SetupHandle("ResizeHandleBottomRight", ResizeDirection.BottomRight);

        var maxBtn = this.FindControl<Button>("MaximizeRestoreButton");
        if (maxBtn != null)
        {
            maxBtn.Click += (_, _) => ToggleMaximize();
        }
    }

    private void SetupHandle(string name, ResizeDirection dir)
    {
        var handle = this.FindControl<Border>(name);
        if (handle == null) return;

        handle.PointerPressed += (s, e) => OnResizePointerPressed(s, e, dir);
        handle.PointerMoved += OnResizePointerMoved;
        handle.PointerReleased += OnResizePointerReleased;
        handle.PointerCaptureLost += OnResizePointerCaptureLost;
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e, ResizeDirection direction)
    {
        if (DataContext is AiComposerViewModel vm && (vm.IsExtracted || vm.DockMode != FrySharp.Sdk.AiDockMode.FloatingOverlay))
        {
            return;
        }

        var container = this.FindControl<Border>("ComposerContainer");
        if (container != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isResizing = true;
            _activeResizeDirection = direction;
            _resizeStartPoint = e.GetPosition(this);
            _startWidth = container.Bounds.Width > 0 ? container.Bounds.Width : ((DataContext as AiComposerViewModel)?.WindowWidth ?? 520);
            _startHeight = container.Bounds.Height > 0 ? container.Bounds.Height : ((DataContext as AiComposerViewModel)?.WindowHeight ?? 650);
            _resizeStartMargin = container.Margin;
            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing) return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isResizing = false;
            _activeResizeDirection = ResizeDirection.None;
            e.Pointer.Capture(null);
            return;
        }

        var container = this.FindControl<Border>("ComposerContainer");
        if (container == null) return;

        var current = e.GetPosition(this);
        var deltaX = current.X - _resizeStartPoint.X;
        var deltaY = current.Y - _resizeStartPoint.Y;

        double minW = 380;
        double minH = 320;
        double maxW = Bounds.Width > 0 ? Math.Max(minW, Bounds.Width - 24) : 1400;
        double maxH = Bounds.Height > 0 ? Math.Max(minH, Bounds.Height - 36) : 1000;

        double newW = _startWidth;
        double newH = _startHeight;
        double newRight = _resizeStartMargin.Right;
        double newTop = _resizeStartMargin.Top;

        switch (_activeResizeDirection)
        {
            case ResizeDirection.Left:
                newW = Math.Clamp(_startWidth - deltaX, minW, maxW);
                break;

            case ResizeDirection.Right:
                newRight = Math.Max(0, _resizeStartMargin.Right - deltaX);
                newW = Math.Clamp(_startWidth + (_resizeStartMargin.Right - newRight), minW, maxW);
                break;

            case ResizeDirection.Bottom:
                newH = Math.Clamp(_startHeight + deltaY, minH, maxH);
                break;

            case ResizeDirection.Top:
                newTop = Math.Clamp(_resizeStartMargin.Top + deltaY, 24, _resizeStartMargin.Top + _startHeight - minH);
                newH = Math.Clamp(_startHeight - (newTop - _resizeStartMargin.Top), minH, maxH);
                break;

            case ResizeDirection.BottomLeft:
                newW = Math.Clamp(_startWidth - deltaX, minW, maxW);
                newH = Math.Clamp(_startHeight + deltaY, minH, maxH);
                break;

            case ResizeDirection.BottomRight:
                newRight = Math.Max(0, _resizeStartMargin.Right - deltaX);
                newW = Math.Clamp(_startWidth + (_resizeStartMargin.Right - newRight), minW, maxW);
                newH = Math.Clamp(_startHeight + deltaY, minH, maxH);
                break;
        }

        container.Width = newW;
        container.Height = newH;
        container.Margin = new Thickness(0, newTop, newRight, 0);

        if (DataContext is AiComposerViewModel vm)
        {
            vm.WindowWidth = newW;
            vm.WindowHeight = newH;
        }

        e.Handled = true;
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            _activeResizeDirection = ResizeDirection.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnResizePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizing = false;
        _activeResizeDirection = ResizeDirection.None;
    }

    private void ToggleMaximize()
    {
        if (DataContext is AiComposerViewModel vm && (vm.IsExtracted || vm.DockMode != FrySharp.Sdk.AiDockMode.FloatingOverlay))
        {
            return;
        }

        var container = this.FindControl<Border>("ComposerContainer");
        if (container == null) return;

        var maxIcon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("MaximizeIcon");

        if (!_isMaximized)
        {
            _preMaximizeWidth = container.Bounds.Width > 0 ? container.Bounds.Width : ((DataContext as AiComposerViewModel)?.WindowWidth ?? 520);
            _preMaximizeHeight = container.Bounds.Height > 0 ? container.Bounds.Height : ((DataContext as AiComposerViewModel)?.WindowHeight ?? 650);
            _preMaximizeMargin = container.Margin;

            double targetW = Bounds.Width > 100 ? Bounds.Width - 32 : 1200;
            double targetH = Bounds.Height > 100 ? Bounds.Height - 64 : 850;

            container.Width = targetW;
            container.Height = targetH;
            container.Margin = new Thickness(0, 36, 16, 0);
            _isMaximized = true;

            if (maxIcon != null) maxIcon.Kind = Material.Icons.MaterialIconKind.WindowRestore;
        }
        else
        {
            container.Width = _preMaximizeWidth > 0 ? _preMaximizeWidth : 520;
            container.Height = _preMaximizeHeight > 0 ? _preMaximizeHeight : 650;
            container.Margin = _preMaximizeMargin;
            _isMaximized = false;

            if (maxIcon != null) maxIcon.Kind = Material.Icons.MaterialIconKind.WindowMaximize;
        }

        if (DataContext is AiComposerViewModel compVm)
        {
            compVm.WindowWidth = container.Width;
            compVm.WindowHeight = container.Height;
        }
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
