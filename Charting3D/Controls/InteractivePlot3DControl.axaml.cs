using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;

public partial class InteractivePlot3DControl : UserControl
{
    public static readonly StyledProperty<Plot3DOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractivePlot3DControl, Plot3DOptions?>(nameof(Options));

    public Plot3DOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    private DispatcherTimer? _autoRotateTimer;
    private bool _isDraggingGrip;
    private double _gripStartY, _gripStartHeight;

    static InteractivePlot3DControl() =>
        OptionsProperty.Changed.AddClassHandler<InteractivePlot3DControl>((x, _) => x.ApplyOptions());

    public InteractivePlot3DControl()
    {
        try
        {
            InitializeComponent();
            AttachEventHandlers();
            if (CanvasControl != null) CanvasControl.PointClicked += (_, e) => PointClicked?.Invoke(this, e);
        }
        catch { /* Headless runner */ }
    }

    /// <summary>A point or node was clicked.</summary>
    public event EventHandler<ElementClickedEventArgs<Plot3DHitTestResult>>? PointClicked;

    public InteractivePlot3DControl(Plot3DOptions options) : this()
    {
        Options = options;
        ApplyOptions();
    }

    private void ApplyOptions()
    {
        if (CanvasControl != null) CanvasControl.Options = Options;
        UpdateUi();
    }

    private void AttachEventHandlers()
    {
        Bind("IsoViewBtn", () => SetCam(c => c.SetIsometric()));
        Bind("TopViewBtn", () => SetCam(c => c.SetTop()));
        Bind("FrontViewBtn", () => SetCam(c => c.SetFront()));
        Bind("SideViewBtn", () => SetCam(c => c.SetSide()));
        Bind("ResetCameraBtn", () => SetCam(c => c.Reset()));
        Bind("AutoRotateBtn", ToggleAutoRotate);
        Bind("ExportHtmlBtn", ExportStandaloneHtml);
        Bind("ProjectionToggleBtn", () => SetCam(c => c.IsOrthographic = !c.IsOrthographic));
        Bind("GridToggleBtn", () => { if (Options != null) { Options.ShowFloorGrid = !Options.ShowFloorGrid; Options.ShowBoundingBox = Options.ShowFloorGrid; Repaint(); } });
        Bind("WireframeToggleBtn", () => { if (Options != null) { Options.Wireframe = !Options.Wireframe; Repaint(); } });
        Bind("ColorMapCycleBtn", CycleColorMap);
        Bind("FullscreenBtn", OpenFullscreenWindow);
        AttachResizeGrip();
    }

    private void SetCam(Action<Camera3D> act) { if (Options != null) { act(Options.Camera); Repaint(); } }

    private void AttachResizeGrip()
    {
        var grip = this.FindControl<Border>("ResizeGripBorder");
        if (grip == null) return;
        grip.PointerPressed += (_, e) => { if (CanvasControl == null) return; _isDraggingGrip = true; _gripStartY = e.GetPosition(this).Y; _gripStartHeight = CanvasControl.Height > 0 ? CanvasControl.Height : CanvasControl.Bounds.Height; e.Pointer.Capture(grip); e.Handled = true; };
        grip.PointerMoved += (_, e) => { if (!_isDraggingGrip || CanvasControl == null) return; CanvasControl.Height = Math.Clamp(_gripStartHeight + (e.GetPosition(this).Y - _gripStartY), 180, 1200); e.Handled = true; };
        grip.PointerReleased += (_, e) => { _isDraggingGrip = false; e.Pointer.Capture(null); e.Handled = true; };
        grip.DoubleTapped += (_, e) => { if (CanvasControl != null) CanvasControl.Height = CanvasControl.Height > 400 ? 300 : 580; e.Handled = true; };
    }

    private void Bind(string name, Action act) { var b = this.FindControl<Button>(name); if (b != null) b.Click += (_, _) => act(); }

    private void OpenFullscreenWindow()
    {
        if (Options == null) return;
        var win = new InteractivePlot3DWindow(Options);
        if (TopLevel.GetTopLevel(this) is Window top) win.Show(top); else win.Show();
        win.Closed += (_, _) => Repaint();
    }

    private void CycleColorMap()
    {
        if (Options == null) return;
        Options.ColorMap = (ColorMapPreset)(((int)Options.ColorMap + 1) % Enum.GetValues<ColorMapPreset>().Length);
        Repaint();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Options?.AutoRotate == true) StartAutoRotateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); StopAutoRotateTimer(); }

    private void ToggleAutoRotate() { if (Options == null) return; Options.AutoRotate = !Options.AutoRotate; if (Options.AutoRotate) StartAutoRotateTimer(); else StopAutoRotateTimer(); }

    private void StartAutoRotateTimer()
    {
        if (_autoRotateTimer != null) return;
        _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _autoRotateTimer.Tick += (_, _) => {
            if (Options == null || !Options.AutoRotate) { StopAutoRotateTimer(); return; }
            if (!IsVisible || Bounds.Width < 10) return;
            Options.Camera.Orbit(Options.AutoRotateSpeed, 0);
            CanvasControl?.InvalidateVisual();
        };
        _autoRotateTimer.Start();
    }

    private void StopAutoRotateTimer() { _autoRotateTimer?.Stop(); _autoRotateTimer = null; }
    private void Repaint() { CanvasControl?.InvalidateVisual(); UpdateStats(); }

    private void UpdateUi()
    {
        if (Options == null) return;
        if (Plot3DTitleText != null) Plot3DTitleText.Text = Options.Title;
        if (Plot3DSubtitleText != null)
        {
            Plot3DSubtitleText.Text = Options.Subtitle;
            Plot3DSubtitleText.IsVisible = !string.IsNullOrWhiteSpace(Options.Subtitle);
        }

        if (CanvasControl != null && Options.Height > 100) CanvasControl.Height = Options.Height;
        UpdateStats();
        if (Options.AutoRotate) StartAutoRotateTimer();
    }

    private void UpdateStats()
    {
        if (Options == null || StatsSummaryText == null) return;
        string cam = Options.Camera.IsOrthographic ? "Ortho" : "Persp";
        int n = Options.Series.Count > 0 ? Options.Series[0].Points.Count :
                (Options.Surface != null ? Options.Surface.ResolutionX * Options.Surface.ResolutionY :
                (Options.Graph != null ? Options.Graph.Nodes.Count : 0));
        StatsSummaryText.Text = $"{Options.Type} | {Options.ColorMap} | {cam} | N: {n}";
    }

    private async void ExportStandaloneHtml()
    {
        if (Options == null) return;
        try {
            string html = Plot3DHtmlExporter.GenerateThreeJsHtml(Options);
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(html);
        } catch { }
    }
}
