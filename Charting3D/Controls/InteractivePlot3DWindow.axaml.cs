using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;

public partial class InteractivePlot3DWindow : Window
{
    private readonly Plot3DOptions _options;
    public Plot3DViewState ViewState { get; }
    private DispatcherTimer? _autoRotateTimer;

    public InteractivePlot3DWindow() : this(new Plot3DOptions()) { }

    public InteractivePlot3DWindow(Plot3DOptions options, Plot3DViewState? viewState = null)
    {
        _options = options ?? new Plot3DOptions();
        ViewState = viewState != null ? new Plot3DViewState(viewState) : new Plot3DViewState(_options);
        InitializeComponent();
        // A window of its own includes the palette; the current theme is shown on top of it.
        PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.StudioAppContext.Instance.ThemeEngine.TrackResourceRoot(this);
        Title = $"3D Visualizer — {_options.Title}";
        if (WindowTitleText != null) WindowTitleText.Text = _options.Title;
        if (CanvasControl != null)
        {
            CanvasControl.Options = _options;
            CanvasControl.ViewState = ViewState;
        }
        AttachHandlers();
        UpdateStats();
        if (ViewState.AutoRotate) StartAutoRotateTimer();
    }

    private void AttachHandlers()
    {
        KeyDown += OnWindowKeyDown;
        Closing += (_, _) => StopAutoRotateTimer();
        Bind("WinIsoViewBtn", () => { ViewState.Camera.SetIsometric(); Repaint(); });
        Bind("WinTopViewBtn", () => { ViewState.Camera.SetTop(); Repaint(); });
        Bind("WinFrontViewBtn", () => { ViewState.Camera.SetFront(); Repaint(); });
        Bind("WinSideViewBtn", () => { ViewState.Camera.SetSide(); Repaint(); });
        Bind("WinResetCameraBtn", () => { ViewState.Reset(_options); Repaint(); });
        Bind("WinAutoRotateBtn", ToggleAutoRotate);
        Bind("WinProjectionToggleBtn", () => { ViewState.Camera.IsOrthographic = !ViewState.Camera.IsOrthographic; Repaint(); });
        Bind("WinGridToggleBtn", () =>
        {
            bool cur = ViewState.EffectiveShowFloorGrid(_options);
            ViewState.OverrideShowFloorGrid = !cur;
            ViewState.OverrideShowBoundingBox = !cur;
            Repaint();
        });
        Bind("WinWireframeToggleBtn", () =>
        {
            ViewState.OverrideWireframe = !ViewState.EffectiveWireframe(_options);
            Repaint();
        });
        Bind("WinColorMapCycleBtn", CycleColorMap);
        Bind("WinExportHtmlBtn", ExportStandaloneHtml);
        Bind("WinFullscreenToggleBtn", ToggleFullscreen);
        Bind("WinCloseBtn", Close);
    }

    private void Bind(string name, Action act) { var b = this.FindControl<Button>(name); if (b != null) b.Click += (_, _) => act(); }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.F11: ToggleFullscreen(); e.Handled = true; break;
            case Key.Space: ToggleAutoRotate(); e.Handled = true; break;
            case Key.R: ViewState.Reset(_options); Repaint(); e.Handled = true; break;
            case Key.G:
                bool cur = ViewState.EffectiveShowFloorGrid(_options);
                ViewState.OverrideShowFloorGrid = !cur;
                ViewState.OverrideShowBoundingBox = !cur;
                Repaint();
                e.Handled = true;
                break;
            case Key.P: ViewState.Camera.IsOrthographic = !ViewState.Camera.IsOrthographic; Repaint(); e.Handled = true; break;
            case Key.W: ViewState.OverrideWireframe = !ViewState.EffectiveWireframe(_options); Repaint(); e.Handled = true; break;
            case Key.D1 or Key.NumPad1: ViewState.Camera.SetIsometric(); Repaint(); e.Handled = true; break;
            case Key.D2 or Key.NumPad2: ViewState.Camera.SetTop(); Repaint(); e.Handled = true; break;
            case Key.D3 or Key.NumPad3: ViewState.Camera.SetFront(); Repaint(); e.Handled = true; break;
            case Key.D4 or Key.NumPad4: ViewState.Camera.SetSide(); Repaint(); e.Handled = true; break;
        }
    }

    private void ToggleFullscreen()
    {
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        var icon = this.FindControl<MaterialIcon>("WinFullscreenIcon");
        if (icon != null) icon.Kind = WindowState == WindowState.FullScreen ? Material.Icons.MaterialIconKind.FullscreenExit : Material.Icons.MaterialIconKind.Fullscreen;
        Repaint();
    }

    private void ToggleAutoRotate()
    {
        ViewState.AutoRotate = !ViewState.AutoRotate;
        if (ViewState.AutoRotate) StartAutoRotateTimer(); else StopAutoRotateTimer();
    }

    private void CycleColorMap()
    {
        var cur = ViewState.EffectiveColorMap(_options);
        ViewState.OverrideColorMap = (ColorMapPreset)(((int)cur + 1) % Enum.GetValues<ColorMapPreset>().Length);
        Repaint();
    }

    private void Repaint() { CanvasControl?.InvalidateVisual(); UpdateStats(); }

    private void UpdateStats()
    {
        if (WindowStatsText != null) {
            string camStr = ViewState.Camera.IsOrthographic ? "Ortho" : "Persp";
            int ptCount = _options.Series.Count > 0 ? _options.Series[0].Points.Count :
                         (_options.Surface != null ? _options.Surface.ResolutionX * _options.Surface.ResolutionY :
                         (_options.Graph != null ? _options.Graph.Nodes.Count : 0));
            WindowStatsText.Text = $"{_options.Type} • {ViewState.EffectiveColorMap(_options)} • {camStr} • N={ptCount}";
        }
        if (WinCameraInfoText != null)
            WinCameraInfoText.Text = $"Pitch: {ViewState.Camera.Pitch:F1}°  Yaw: {ViewState.Camera.Yaw:F1}°  Dist: {ViewState.Camera.Distance:F1}";
    }

    private void StartAutoRotateTimer()
    {
        if (_autoRotateTimer != null) return;
        _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _autoRotateTimer.Tick += (_, _) => {
            if (!ViewState.AutoRotate) { StopAutoRotateTimer(); return; }
            ViewState.Camera.Orbit(_options.AutoRotateSpeed, 0);
            Repaint();
        };
        _autoRotateTimer.Start();
    }

    private void StopAutoRotateTimer() { _autoRotateTimer?.Stop(); _autoRotateTimer = null; }

    private async void ExportStandaloneHtml()
    {
        try {
            var effective = CanvasControl?.GetEffectiveOptions() ?? _options;
            string html = Plot3DHtmlExporter.GenerateThreeJsHtml(effective);
            if (Clipboard != null) await Clipboard.SetTextAsync(html);
        } catch { }
    }
}
