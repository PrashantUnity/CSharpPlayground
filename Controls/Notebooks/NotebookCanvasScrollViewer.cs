using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Notebooks;

/// <summary>
/// A specialized ScrollViewer for the Notebook Canvas that completely prevents automatic
/// viewport jumping or BringIntoView scrolling when cells, buttons, or child editors gain focus or are clicked.
/// Scrolling is exclusively driven by user gestures (mouse wheel, touch, scrollbar dragging)
/// or explicit programmatic navigation.
/// </summary>
public class NotebookCanvasScrollViewer : ScrollViewer
{
    private bool _isUserOrProgrammaticScroll;

    static NotebookCanvasScrollViewer()
    {
        BringIntoViewOnFocusChangeProperty.OverrideDefaultValue<NotebookCanvasScrollViewer>(false);

        RequestBringIntoViewEvent.AddClassHandler<Control>((control, e) =>
        {
            if (IsInNotebookCanvasScrollViewer(control))
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public static bool IsInNotebookCanvasScrollViewer(Visual visual)
    {
        if (visual is NotebookCanvasScrollViewer) return true;
        if (visual.FindAncestorOfType<NotebookCanvasScrollViewer>() != null) return true;
        if (visual is ILogical logical && logical.FindLogicalAncestorOfType<NotebookCanvasScrollViewer>() != null) return true;
        return false;
    }

    public NotebookCanvasScrollViewer()
    {
        BringIntoViewOnFocusChange = false;
        SetBringIntoViewOnFocusChange(this, false);

        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        // Do NOT call base.OnGotFocus(e) to completely prevent automatic BringIntoView of focused controls.
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _isUserOrProgrammaticScroll = true;
        try
        {
            base.OnPointerWheelChanged(e);
        }
        finally
        {
            _isUserOrProgrammaticScroll = false;
        }
    }

    public void ProgrammaticScroll(Action scrollAction)
    {
        _isUserOrProgrammaticScroll = true;
        try
        {
            scrollAction();
        }
        finally
        {
            _isUserOrProgrammaticScroll = false;
        }
    }

    public void SetScrollOffset(Vector targetOffset)
    {
        _isUserOrProgrammaticScroll = true;
        try
        {
            Offset = targetOffset;
        }
        finally
        {
            _isUserOrProgrammaticScroll = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var offsetBefore = Offset;
        var result = base.ArrangeOverride(finalSize);
        if (!_isUserOrProgrammaticScroll && Offset != offsetBefore)
        {
            var maxOffsetY = Math.Max(0, Extent.Height - Viewport.Height);
            var maxOffsetX = Math.Max(0, Extent.Width - Viewport.Width);
            var clampedY = Math.Clamp(offsetBefore.Y, 0, maxOffsetY);
            var clampedX = Math.Clamp(offsetBefore.X, 0, maxOffsetX);
            Offset = new Vector(clampedX, clampedY);
        }
        return result;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        BringIntoViewOnFocusChange = false;
        SetBringIntoViewOnFocusChange(this, false);

        if (e.NameScope.Find<ScrollBar>("PART_VerticalScrollBar") is { } vBar)
        {
            vBar.AddHandler(PointerPressedEvent, (_, _) => _isUserOrProgrammaticScroll = true, RoutingStrategies.Tunnel);
            vBar.AddHandler(PointerReleasedEvent, (_, _) => _isUserOrProgrammaticScroll = false, RoutingStrategies.Tunnel);
            vBar.AddHandler(PointerCaptureLostEvent, (_, _) => _isUserOrProgrammaticScroll = false, RoutingStrategies.Tunnel);
        }

        if (Presenter != null)
        {
            SetBringIntoViewOnFocusChange(Presenter, false);
            Presenter.AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);

            Presenter.PropertyChanged += (s, args) =>
            {
                if (args.Property == ContentPresenter.ChildProperty && args.NewValue is Control c)
                {
                    HookContentControl(c);
                }
            };

            if (Presenter.Child is Control contentControl)
            {
                HookContentControl(contentControl);
            }
        }

        if (Content is Control directContent)
        {
            HookContentControl(directContent);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BringIntoViewOnFocusChange = false;
        SetBringIntoViewOnFocusChange(this, false);

        if (Presenter != null)
        {
            SetBringIntoViewOnFocusChange(Presenter, false);
            Presenter.AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
            if (Presenter.Child is Control contentControl)
            {
                HookContentControl(contentControl);
            }
        }

        if (Content is Control directContent)
        {
            HookContentControl(directContent);
        }
    }

    private void HookContentControl(Control control)
    {
        SetBringIntoViewOnFocusChange(control, false);
        control.AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        // Mark handled so that ScrollContentPresenter.BringIntoViewRequested never alters the canvas offset
        e.Handled = true;
    }
}
