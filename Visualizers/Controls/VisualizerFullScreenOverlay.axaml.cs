using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

/// <summary>
/// Shows a visualizer over the whole window. The full-screen copy shares the data and the playback with the inline one,
/// so stepping in either moves both (and the editor's line highlight, which the inline copy keeps driving), but it zooms
/// and pans on its own and opens fitted to the window.
/// </summary>
public partial class VisualizerFullScreenOverlay : UserControl
{
    private const double Gutter = 12;

    private readonly InteractiveVisualizerControl? _source;
    private readonly OverlayLayer? _layer;
    private readonly TopLevel? _topLevel;
    private readonly IInputElement? _focusToRestore;

    public InteractiveVisualizerControl View { get; } = new() { IsFullScreenView = true };

    public VisualizerFullScreenOverlay()
    {
        try
        {
            InitializeComponent();
        }
        catch
        {
            // Headless test runner
        }
    }

    // Loaded here rather than through the XAML source generator's method, which the IDE is slow to see for new views.
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private VisualizerFullScreenOverlay(InteractiveVisualizerControl source, OverlayLayer layer) : this()
    {
        _source = source;
        _layer = layer;
        _topLevel = TopLevel.GetTopLevel(source);
        _focusToRestore = _topLevel?.FocusManager?.GetFocusedElement();

        View.Options = source.Options!.CloneView();
        View.FitToView();
        View.ExitFullScreenRequested += (_, _) => Close();
        View.AddHandler(InteractiveVisualizerControl.StepSourceLineChangedEvent, OnViewStepLine);

        // Tab cycles through the full-screen controls instead of wandering into the hidden window underneath.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);

        if (this.FindControl<Border>("ViewHost") is { } host) host.Child = View;
        else Content = View;
    }

    /// <summary>Opens <paramref name="source"/> over its window; null when it isn't in a window or one is already open.</summary>
    public static VisualizerFullScreenOverlay? Open(InteractiveVisualizerControl source)
    {
        if (source.Options == null || OverlayLayer.GetOverlayLayer(source) is not { } layer) return null;
        if (layer.Children.OfType<VisualizerFullScreenOverlay>().Any()) return null;

        var overlay = new VisualizerFullScreenOverlay(source, layer);
        layer.Children.Add(overlay);
        return overlay;
    }

    /// <summary>Leaves full screen and puts keyboard focus back where it was.</summary>
    public void Close()
    {
        if (_layer == null || !_layer.Children.Remove(this)) return;

        if (_focusToRestore is Visual previous && TopLevel.GetTopLevel(previous) != null && _focusToRestore.Focusable)
        {
            _focusToRestore.Focus();
        }
        else
        {
            _source?.FocusCanvas();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_topLevel != null) _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
        if (_source != null) _source.DetachedFromVisualTree += OnSourceDetached;
        FillWindow();
        Dispatcher.UIThread.Post(View.FocusCanvas, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel != null) _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
        if (_source != null) _source.DetachedFromVisualTree -= OnSourceDetached;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    // The overlay layer is a canvas, so the overlay sizes itself to the window.
    private void FillWindow()
    {
        var size = _topLevel?.ClientSize ?? _layer?.Bounds.Size ?? default;
        Width = size.Width;
        Height = size.Height;

        double titleBar = (_topLevel as Window)?.WindowDecorationMargin.Top ?? 0;
        if (this.FindControl<Border>("ViewHost") is { } host) host.Padding = new Thickness(Gutter, Gutter + titleBar, Gutter, Gutter);
    }

    private void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty || e.Property == Window.WindowDecorationMarginProperty) FillWindow();
    }

    // The output it came from went away (a re-run cleared it), so there is nothing left to show full screen.
    private void OnSourceDetached(object? sender, VisualTreeAttachmentEventArgs e) => Close();

    // The inline copy keeps driving the editor's highlight; this one only hands over an explicit "show me the line".
    private void OnViewStepLine(object? sender, VisualizerStepLineEventArgs e)
    {
        e.Handled = true;
        if (!e.Reveal || _source == null) return;
        Close();
        _source.RaiseEvent(new VisualizerStepLineEventArgs(e.Line, e.SourceFile, reveal: true));
    }
}
