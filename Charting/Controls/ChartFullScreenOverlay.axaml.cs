using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

/// <summary>
/// Shows a chart over the whole window. The full-screen copy draws the same data (and follows it when the program
/// updates the chart), but zooms and pans on its own and opens fitted to the window; a click on it reaches the program
/// like a click on the inline chart.
/// </summary>
public partial class ChartFullScreenOverlay : UserControl
{
    private const double Gutter = 12;

    private readonly InteractiveChartControl? _source;
    private readonly OverlayLayer? _layer;
    private readonly TopLevel? _topLevel;
    private readonly IInputElement? _focusToRestore;
    private IDisposable? _optionsSubscription;

    public InteractiveChartControl View { get; } = new() { IsFullScreenView = true };

    public ChartFullScreenOverlay()
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

    private ChartFullScreenOverlay(InteractiveChartControl source, OverlayLayer layer) : this()
    {
        _source = source;
        _layer = layer;
        _topLevel = TopLevel.GetTopLevel(source);
        _focusToRestore = _topLevel?.FocusManager?.GetFocusedElement();

        View.ViewState = new ChartViewState(source.ViewState);
        View.Options = source.Options;
        View.ExitFullScreenRequested += (_, _) => Close();
        View.ValueClicked += (_, e) => source.RaiseValueClicked(e);

        // A chart the program keeps updating is replaced in its output: the full-screen copy draws the new one too.
        _optionsSubscription = source.GetObservable(InteractiveChartControl.OptionsProperty).Subscribe(new OptionsObserver(this));

        // Tab cycles through the full-screen controls instead of wandering into the hidden window underneath.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);

        if (this.FindControl<Border>("ViewHost") is { } host) host.Child = View;
        else Content = View;
    }

    /// <summary>Opens <paramref name="source"/> over its window; null when it isn't in a window or one is already open.</summary>
    public static ChartFullScreenOverlay? Open(InteractiveChartControl source)
    {
        if (source.Options == null || OverlayLayer.GetOverlayLayer(source) is not { } layer) return null;
        if (layer.Children.OfType<ChartFullScreenOverlay>().Any()) return null;

        var overlay = new ChartFullScreenOverlay(source, layer);
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
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_topLevel != null) _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
        if (_source != null) _source.DetachedFromVisualTree += OnSourceDetached;
        FillWindow();
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel != null) _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
        if (_source != null) _source.DetachedFromVisualTree -= OnSourceDetached;
        _optionsSubscription?.Dispose();
        _optionsSubscription = null;
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

    private sealed class OptionsObserver(ChartFullScreenOverlay overlay) : IObserver<ChartOptions?>
    {
        public void OnNext(ChartOptions? value)
        {
            if (value != null && !ReferenceEquals(overlay.View.Options, value)) overlay.View.Options = value;
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
