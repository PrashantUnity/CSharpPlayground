using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

public partial class InteractiveVisualizerControl : UserControl
{
    public const double DefaultCanvasHeight = 300;
    private const double MinCanvasHeight = 140;
    private const double MaxCanvasHeight = 1400;
    private const double ZoomStep = 1.2;

    public static readonly StyledProperty<VisualizerOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractiveVisualizerControl, VisualizerOptions?>(nameof(Options));

    /// <summary>Bubbles up to the hosting editor whenever the shown step changes, carrying the line that recorded it.</summary>
    public static readonly RoutedEvent<VisualizerStepLineEventArgs> StepSourceLineChangedEvent =
        RoutedEvent.Register<InteractiveVisualizerControl, VisualizerStepLineEventArgs>("StepSourceLineChanged", RoutingStrategies.Bubble);

    private VisualizerSequence? _observedSequence;
    private bool _isFullScreenView;

    public VisualizerOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>True for the copy the full-screen overlay shows: the canvas fills the window and there is no resize grip.</summary>
    public bool IsFullScreenView
    {
        get => _isFullScreenView;
        set
        {
            _isFullScreenView = value;
            ApplyViewMode();
        }
    }

    /// <summary>Raised by the full-screen copy when the learner asks to leave (Esc, the exit button, a double-click).</summary>
    public event EventHandler? ExitFullScreenRequested;

    private VisualizerCanvasControl? Canvas => this.FindControl<VisualizerCanvasControl>("CanvasControl");

    static InteractiveVisualizerControl()
    {
        OptionsProperty.Changed.AddClassHandler<InteractiveVisualizerControl>((control, _) => control.ApplyOptions());
    }

    public InteractiveVisualizerControl()
    {
        try
        {
            InitializeComponent();
            WireEvents();
        }
        catch
        {
            // Headless test runner
        }
    }

    public InteractiveVisualizerControl(VisualizerOptions options) : this()
    {
        Options = options;
        ApplyOptions();
    }

    private void WireEvents()
    {
        BindButton("ZoomInBtn", () => Canvas?.ZoomBy(ZoomStep));
        BindButton("ZoomOutBtn", () => Canvas?.ZoomBy(1 / ZoomStep));
        BindButton("ResetViewBtn", () => Canvas?.ResetView());
        BindButton("FitViewBtn", () => Canvas?.FitToView());
        BindButton("ToggleValuesBtn", ToggleValues);
        BindButton("ToggleCoordsBtn", ToggleCoords);
        BindButton("CopyDataBtn", CopyData);
        BindButton("FullScreenBtn", ToggleFullScreen);

        if (Canvas is { } canvas)
        {
            canvas.ViewportChanged += (_, _) => UpdateZoomLevel();
            canvas.DoubleTapped += (_, e) =>
            {
                ToggleFullScreen();
                e.Handled = true;
            };
        }

        WireResizeGrip();
    }

    private void BindButton(string name, Action action)
    {
        if (this.FindControl<Button>(name) is { } button) button.Click += (_, _) => action();
    }

    private void WireResizeGrip()
    {
        var grip = this.FindControl<Border>("ResizeGrip");
        var host = this.FindControl<Border>("CanvasHost");
        if (grip == null || host == null) return;

        bool dragging = false;
        double startHeight = 0;
        double startY = 0;

        // Measured against the window, so the drag stays steady while a notebook scrolls the cell underneath it.
        double PointerY(PointerEventArgs e) => e.GetPosition(TopLevel.GetTopLevel(this)).Y;

        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            dragging = true;
            startHeight = host.Bounds.Height;
            startY = PointerY(e);
            e.Pointer.Capture(grip);
            e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (dragging) host.Height = Math.Clamp(startHeight + PointerY(e) - startY, MinCanvasHeight, MaxCanvasHeight);
        };
        grip.PointerReleased += (_, e) =>
        {
            if (!dragging) return;
            dragging = false;
            e.Pointer.Capture(null);
        };
        grip.PointerCaptureLost += (_, _) => dragging = false;
        grip.DoubleTapped += (_, _) => host.Height = DefaultCanvasHeight;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        e.Handled = HandleViewerKey(e.Key);
    }

    // Arrows / Home / End / Space drive the playback, + - 0 F the view, and Esc leaves full screen.
    private bool HandleViewerKey(Key key)
    {
        var sequence = Options?.Sequence is { HasSteps: true } steps ? steps : null;
        switch (key)
        {
            case Key.Left when sequence != null: sequence.PrevStep(); return true;
            case Key.Right when sequence != null: sequence.NextStep(); return true;
            case Key.Home when sequence != null: sequence.FirstStep(); return true;
            case Key.End when sequence != null: sequence.LastStep(); return true;
            case Key.Space when sequence != null: sequence.TogglePlay(); return true;
            case Key.OemPlus or Key.Add: Canvas?.ZoomBy(ZoomStep); return true;
            case Key.OemMinus or Key.Subtract: Canvas?.ZoomBy(1 / ZoomStep); return true;
            case Key.D0 or Key.NumPad0: Canvas?.ResetView(); return true;
            case Key.F: Canvas?.FitToView(); return true;
            case Key.Escape when IsFullScreenView: ExitFullScreenRequested?.Invoke(this, EventArgs.Empty); return true;
            default: return false;
        }
    }

    /// <summary>Opens this visualizer over the whole window, or, for the full-screen copy, asks to close it.</summary>
    public void ToggleFullScreen()
    {
        if (IsFullScreenView)
        {
            ExitFullScreenRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        VisualizerFullScreenOverlay.Open(this);
    }

    /// <summary>Fits the drawing to the canvas, now and whenever the canvas resizes, until the learner zooms or pans.</summary>
    public void FitToView() => Canvas?.FitToView();

    /// <summary>Moves keyboard focus onto the drawing, so the playback and view keys work straight away.</summary>
    public void FocusCanvas() => Canvas?.Focus();

    private void ToggleValues()
    {
        if (Options == null) return;
        Options.ShowValues = !Options.ShowValues;
        if (Options.MatrixData != null) Options.MatrixData.ShowValues = Options.ShowValues;
        Canvas?.InvalidateVisual();
    }

    private void ToggleCoords()
    {
        if (Options == null) return;
        Options.ShowCoordinates = !Options.ShowCoordinates;
        if (Options.MatrixData != null) Options.MatrixData.ShowCoordinates = Options.ShowCoordinates;
        Canvas?.InvalidateVisual();
    }

    // Copies the grid as it is at the step on screen, not only the starting grid.
    private void CopyData()
    {
        var grid = Options?.Sequence?.CurrentStep?.Snapshot as GridMatrixData ?? Options?.MatrixData;
        if (grid != null) VisualizerExportService.CopyToClipboard(VisualizerExportService.ToCsv(grid));
    }

    private void UpdateZoomLevel()
    {
        if (Options != null && this.FindControl<TextBlock>("ZoomLevelText") is { } text)
        {
            text.Text = $"{Math.Round(Options.Zoom * 100)}%";
        }
    }

    private void ApplyViewMode()
    {
        // Inline, the card hugs its content; full screen, it fills the window and the canvas takes the spare height.
        VerticalAlignment = IsFullScreenView ? VerticalAlignment.Stretch : VerticalAlignment.Top;

        if (this.FindControl<Border>("CanvasHost") is { } host)
        {
            host.Height = IsFullScreenView ? double.NaN : DefaultCanvasHeight;
        }

        if (this.FindControl<Border>("ResizeGrip") is { } grip) grip.IsVisible = !IsFullScreenView;
        if (this.FindControl<VisualizerPlaybackControl>("PlaybackControl") is { } playback)
        {
            playback.Margin = new Thickness(0, IsFullScreenView ? 8 : 0, 0, 0);
            playback.SetStepLineTip(IsFullScreenView
                ? "Leave full screen and show the code line that recorded this step"
                : "Show the code line that recorded this step");
        }

        if (this.FindControl<MaterialIcon>("FullScreenIcon") is { } icon)
        {
            icon.Kind = IsFullScreenView ? MaterialIconKind.FullscreenExit : MaterialIconKind.Fullscreen;
        }

        if (this.FindControl<TextBlock>("ExitHintText") is { } hint) hint.IsVisible = IsFullScreenView;
        if (this.FindControl<Button>("FullScreenBtn") is { } button)
        {
            button.Padding = IsFullScreenView ? new Thickness(6, 0) : default;
            ToolTip.SetTip(button, IsFullScreenView ? "Exit Full Screen (Esc)" : "Full Screen (or double-click the drawing)");
        }
    }

    public void ApplyOptions()
    {
        if (Options == null) return;

        var titleBlock = this.FindControl<TextBlock>("VisualizerTitleText");
        var statsBlock = this.FindControl<TextBlock>("StatsSummaryText");
        var icon = this.FindControl<MaterialIcon>("HeaderVisualizerIcon");
        var canvas = Canvas;
        var playback = this.FindControl<VisualizerPlaybackControl>("PlaybackControl");

        if (titleBlock != null)
        {
            titleBlock.Text = string.IsNullOrEmpty(Options.Title) ? "Data Structure Visualizer" : Options.Title;
        }

        if (icon != null)
        {
            icon.Kind = Options.Kind switch
            {
                VisualizerKind.Matrix => MaterialIconKind.Grid,
                VisualizerKind.Islands => MaterialIconKind.Island,
                VisualizerKind.Tree => MaterialIconKind.FamilyTree,
                VisualizerKind.Graph => MaterialIconKind.Graph,
                VisualizerKind.LinkedList => MaterialIconKind.VectorLink,
                VisualizerKind.ArrayPointers => MaterialIconKind.FormatListNumbered,
                _ => MaterialIconKind.CodeBraces
            };
        }

        if (statsBlock != null)
        {
            statsBlock.Text = Options.GetSummaryText();
        }

        // Values, coordinates and CSV only mean something for a grid.
        if (this.FindControl<StackPanel>("MatrixToolsPanel") is { } matrixTools)
        {
            matrixTools.IsVisible = Options.MatrixData != null;
        }

        if (canvas != null)
        {
            canvas.Options = Options;
        }

        if (playback != null)
        {
            playback.Sequence = Options.Sequence;
            playback.IsVisible = Options.Sequence != null && Options.Sequence.HasSteps;
        }

        UpdateZoomLevel();
        ObserveSequence(VisualRoot != null ? Options.Sequence : null);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSequence(Options?.Sequence);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ObserveSequence(null);
    }

    // Only visible visualizers drive the editor highlight, so detached outputs from earlier runs stay silent.
    private void ObserveSequence(VisualizerSequence? sequence)
    {
        if (ReferenceEquals(_observedSequence, sequence)) return;
        if (_observedSequence != null) _observedSequence.StepChanged -= OnSequenceStepChanged;
        _observedSequence = sequence;
        if (_observedSequence != null) _observedSequence.StepChanged += OnSequenceStepChanged;
    }

    private void OnSequenceStepChanged(object? sender, int stepIndex)
    {
        var step = _observedSequence?.CurrentStep;
        RaiseEvent(new VisualizerStepLineEventArgs(step?.SourceLine ?? 0, step?.SourceFile, reveal: false));
    }
}

public sealed class VisualizerStepLineEventArgs : RoutedEventArgs
{
    public VisualizerStepLineEventArgs(int line, string? sourceFile, bool reveal)
        : base(InteractiveVisualizerControl.StepSourceLineChangedEvent)
    {
        Line = line;
        SourceFile = sourceFile;
        Reveal = reveal;
    }

    /// <summary>1-based source line of the current step; 0 clears the highlight.</summary>
    public int Line { get; }

    public string? SourceFile { get; }

    /// <summary>True when the learner explicitly asked to jump to the line.</summary>
    public bool Reveal { get; }
}
