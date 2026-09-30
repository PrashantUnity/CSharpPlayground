using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

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

    public Plot3DViewState ViewState { get; set; } = new();
    private DispatcherTimer? _autoRotateTimer;

    private T? Find<T>(string name) where T : Control { try { return this.FindControl<T>(name); } catch { return null; } }
    private Plot3DCanvasControl? Canvas => Find<Plot3DCanvasControl>("CanvasControl");

    static InteractivePlot3DControl() =>
        OptionsProperty.Changed.AddClassHandler<InteractivePlot3DControl>((x, _) => x.ApplyOptions());

    public InteractivePlot3DControl()
    {
        try { InitializeComponent(); SetupChrome(); WireEvents(); }
        catch { /* Headless runner */ }
    }

    public InteractivePlot3DControl(Plot3DOptions options) : this()
    {
        Options = options;
        ApplyOptions();
    }

    public event EventHandler<ElementClickedEventArgs<Plot3DHitTestResult>>? PointClicked;

    private void SetupChrome()
    {
        if (Find<VisualChromeControl>("Chrome") is not { } chrome) return;
        if (Find<StackPanel>("KindToolsPanel") is { } tools) { tools.IsVisible = true; chrome.SetKindTools(tools); }
        if (Find<Border>("CanvasContainer") is { } cont) { cont.IsVisible = true; chrome.SetCanvasContent(cont); }
        chrome.SpecGetter = () => Options != null ? Plot3DOptionsConverter.ToSpec(Canvas?.GetEffectiveOptions() ?? Options) : null;
        chrome.DataCsvGetter = () => Options != null ? Plot3DExportService.ToCsv(Canvas?.GetEffectiveOptions() ?? Options) : null;
        chrome.ResetFitRequested += (_, _) => { ViewState.Reset(Options); Repaint(); };
        chrome.FullscreenRequested += (_, _) => OpenFullscreenWindow();
    }

    private void WireEvents()
    {
        if (Canvas != null) Canvas.PointClicked += (_, e) => PointClicked?.Invoke(this, e);
        Bind("IsoViewBtn", () => SetCam(c => c.SetIsometric()));
        Bind("TopViewBtn", () => SetCam(c => c.SetTop()));
        Bind("FrontViewBtn", () => SetCam(c => c.SetFront()));
        Bind("SideViewBtn", () => SetCam(c => c.SetSide()));
        Bind("AutoRotateBtn", ToggleAutoRotate);
        Bind("ExportHtmlBtn", ExportStandaloneHtml);
        Bind("ProjectionToggleBtn", () => SetCam(c => c.IsOrthographic = !c.IsOrthographic));
        Bind("GridToggleBtn", () => { if (Options == null) return; bool cur = ViewState.EffectiveShowFloorGrid(Options); ViewState.OverrideShowFloorGrid = !cur; ViewState.OverrideShowBoundingBox = !cur; Repaint(); });
        Bind("WireframeToggleBtn", () => { if (Options == null) return; ViewState.OverrideWireframe = !ViewState.EffectiveWireframe(Options); Repaint(); });
        Bind("ColorMapCycleBtn", CycleColorMap);
    }

    private void SetCam(Action<Camera3D> act) { act(ViewState.Camera); Repaint(); }
    private void Bind(string name, Action act) { if (Find<Button>(name) is { } b) b.Click += (_, _) => act(); }

    public void ApplyOptions()
    {
        var opts = Options;
        if (opts == null) return;
        if (Canvas != null) { Canvas.Options = opts; Canvas.ViewState = ViewState; if (opts.Height > 100) Canvas.Height = Math.Max(Canvas.MinHeight, opts.Height); }
        UpdateHeader();
        if (ViewState.AutoRotate || opts.AutoRotate) StartAutoRotateTimer();
    }

    private void CycleColorMap()
    {
        if (Options == null) return;
        var cur = ViewState.EffectiveColorMap(Options);
        ViewState.OverrideColorMap = (ColorMapPreset)(((int)cur + 1) % Enum.GetValues<ColorMapPreset>().Length);
        Repaint();
    }

    private void ToggleAutoRotate()
    {
        ViewState.AutoRotate = !ViewState.AutoRotate;
        if (ViewState.AutoRotate) StartAutoRotateTimer(); else StopAutoRotateTimer();
    }

    private void StartAutoRotateTimer()
    {
        if (_autoRotateTimer != null) return;
        _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _autoRotateTimer.Tick += (_, _) => { if (!ViewState.AutoRotate || !IsVisible || Bounds.Width < 10) return; ViewState.Camera.Orbit(Options?.AutoRotateSpeed ?? 1.0, 0); Canvas?.InvalidateVisual(); };
        _autoRotateTimer.Start();
    }

    private void StopAutoRotateTimer() { _autoRotateTimer?.Stop(); _autoRotateTimer = null; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); if (ViewState.AutoRotate) StartAutoRotateTimer(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); StopAutoRotateTimer(); }
    private void Repaint() { Canvas?.InvalidateVisual(); UpdateHeader(); }

    private void UpdateHeader()
    {
        var opts = Options;
        if (opts == null) return;
        string cam = ViewState.Camera.IsOrthographic ? "Ortho" : "Persp";
        int n = opts.Series.Count > 0 ? opts.Series[0].Points.Count : (opts.Surface != null ? opts.Surface.ResolutionX * opts.Surface.ResolutionY : (opts.Graph != null ? opts.Graph.Nodes.Count : 0));
        Find<VisualChromeControl>("Chrome")?.SetHeader(opts.Title, opts.Subtitle, $"{opts.Type} | {ViewState.EffectiveColorMap(opts)} | {cam} | N: {n}", opts.Notice, MaterialIconKind.CubeOutline);
    }

    private void OpenFullscreenWindow()
    {
        if (Options == null) return;
        var win = new InteractivePlot3DWindow(Options, ViewState);
        if (TopLevel.GetTopLevel(this) is Window top) win.Show(top); else win.Show();
        win.Closed += (_, _) => Repaint();
    }

    private async void ExportStandaloneHtml()
    {
        try {
            var effective = Canvas?.GetEffectiveOptions() ?? Options;
            if (effective != null && TopLevel.GetTopLevel(this) is { Clipboard: { } cb })
                await cb.SetTextAsync(Plot3DHtmlExporter.GenerateThreeJsHtml(effective));
        } catch { }
    }
}
