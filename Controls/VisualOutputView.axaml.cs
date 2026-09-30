using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Draws a <see cref="VisualOutput"/>: builds the drawing from the spec when it is shown (in the background for a big
/// one) and puts the chart, 3D or visualizer control in place, on the UI thread. It redraws when the spec is replaced
/// and lets go of everything when it leaves the screen.
/// </summary>
public partial class VisualOutputView : UserControl
{
    public static readonly StyledProperty<VisualOutput?> OutputProperty =
        AvaloniaProperty.Register<VisualOutputView, VisualOutput?>(nameof(Output));

    private VisualOutput? _watched;
    private readonly List<VisualEventTarget> _selection = [];
    // A live visual redraws at most this often, however fast its program updates it (the latest spec wins).
    internal static readonly TimeSpan MinRedrawInterval = TimeSpan.FromMilliseconds(33);

    private int _drawing;
    private int _redrawQueued; // 1 while a redraw waits (set from the thread that updated the spec)
    private long _drawnAt = long.MinValue / 2;

    public VisualOutputView()
    {
        InitializeComponent();
    }

    public VisualOutput? Output
    {
        get => GetValue(OutputProperty);
        set => SetValue(OutputProperty, value);
    }

    /// <summary>True while a big spec's drawing is being built in the background.</summary>
    public bool IsPreparing { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OutputProperty && VisualRoot != null) Watch(Output);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Output);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    private void Watch(VisualOutput? output)
    {
        if (!ReferenceEquals(_watched, output))
        {
            if (_watched != null) _watched.PropertyChanged -= OnOutputChanged;
            _watched = output;
            if (output != null) output.PropertyChanged += OnOutputChanged;
        }

        Draw();
    }

    // A spec replaced several times in a burst is drawn once, with the latest, and no sooner than MinRedrawInterval
    // after the last drawing.
    private void OnOutputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VisualOutput.Spec) || Interlocked.Exchange(ref _redrawQueued, 1) == 1) return;
        Dispatcher.UIThread.Post(() =>
        {
            var wait = MinRedrawInterval - TimeSpan.FromMilliseconds(Environment.TickCount64 - _drawnAt);
            if (wait > TimeSpan.Zero) DispatcherTimer.RunOnce(Draw, wait, DispatcherPriority.Background);
            else Draw();
        }, DispatcherPriority.Background);
    }

    private void Draw()
    {
        // Whatever asked for this drawing, it draws the latest spec: a redraw that waited is done (even one whose timer
        // was dropped when the view left the screen, or the view would never redraw again).
        Volatile.Write(ref _redrawQueued, 0);
        var drawing = ++_drawing;
        _drawnAt = Environment.TickCount64;
        Release();
        if (_watched is not { } output) return;

        var spec = output.Spec;
        if (VisualSize.Of(spec) <= VisualSize.BackgroundThreshold)
        {
            Show(drawing, spec, VisualDrawing.Prepare(spec));
            return;
        }

        IsPreparing = true;
        ShowStatus("Preparing the drawing…");
        // Prepare catches what building throws, so the result is always there.
        Task.Run(() => VisualDrawing.Prepare(spec))
            .ContinueWith(prepared => Dispatcher.UIThread.Post(() => Show(drawing, spec, prepared.Result)), TaskScheduler.Default);
    }

    private void Show(int drawing, VisualSpec spec, VisualDrawing prepared)
    {
        if (drawing != _drawing) return; // a newer spec is being drawn
        IsPreparing = false;
        if (prepared.Error != null)
        {
            ShowStatus($"This visual couldn't be drawn: {prepared.Error}");
            return;
        }

        Control? control;
        try
        {
            control = prepared.Model switch
            {
                ChartOptions chart => new InteractiveChartControl(chart),
                Plot3DOptions plot => new InteractivePlot3DControl(plot),
                VisualizerOptions visualizer => new InteractiveVisualizerControl(visualizer),
                _ => null
            };
        }
        catch (Exception ex)
        {
            // A visual is data from a program; one that trips up its control must not take the studio down.
            ShowStatus($"This visual couldn't be drawn: {ex.Message}");
            return;
        }

        // A spec's width is the widest it is drawn; it still fits narrower space. Without one it takes the space there is.
        if (control != null && spec.Width is { } width)
        {
            control.MaxWidth = width;
            control.HorizontalAlignment = HorizontalAlignment.Left;
        }

        if (control != null && prepared.Model != null) Listen(control, prepared.Model, spec);
        StatusText.IsVisible = false;
        NoticeText.Text = prepared.Notice;
        NoticeText.IsVisible = !string.IsNullOrEmpty(prepared.Notice);
        Host.Content = control;
    }

    // What the user does to the visual becomes its events, for whoever listens: a click on an element; the elements
    // picked with Ctrl-, Cmd- or Shift-click; the step a visualizer plays. Nothing is worked out when no one listens.
    private void Listen(Control control, object drawn, VisualSpec spec)
    {
        switch (control, drawn)
        {
            case (InteractiveChartControl chart, ChartOptions model):
                chart.ValueClicked += (_, e) => Clicked(VisualEventTargets.Of(e.Hit, model, spec as ChartSpec), e.Modifiers);
                break;
            case (InteractivePlot3DControl plot, Plot3DOptions model):
                plot.PointClicked += (_, e) => Clicked(VisualEventTargets.Of(e.Hit, model), e.Modifiers);
                break;
            case (InteractiveVisualizerControl visualizer, VisualizerOptions model):
                visualizer.ElementClicked += (_, e) => Clicked(VisualEventTargets.Of(e.Hit, model), e.Modifiers);
                if (model.Sequence is { } sequence)
                {
                    sequence.StepChanged += (_, index) =>
                    {
                        if (_watched is { } output && output.HasSubscriber(VisualEventKinds.Step)) output.Raise(VisualEvent.Step(index));
                    };
                }

                break;
        }
    }

    private void Clicked(VisualEventTarget? target, Avalonia.Input.KeyModifiers modifiers)
    {
        if (target == null || _watched is not { } output) return;
        if (ClickGesture.Selects(modifiers) && output.HasSubscriber(VisualEventKinds.Select))
        {
            // A pick toggles: a second Ctrl-click on an element takes it out again.
            if (!_selection.Remove(target)) _selection.Add(target);
            output.Raise(VisualEvent.Select(_selection.ToList()));
        }

        if (output.HasSubscriber(VisualEventKinds.Click)) output.Raise(VisualEvent.Click(target, ClickGesture.Names(modifiers)));
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.IsVisible = true;
    }

    // Timers in the old control (auto-rotate, playback) stop with it.
    private void Release()
    {
        IsPreparing = false;
        _selection.Clear();
        InteractiveControlLifecycle.DisposeIfNeeded(Host.Content as Control);
        Host.Content = null;
        NoticeText.IsVisible = false;
        StatusText.IsVisible = false;
    }
}
